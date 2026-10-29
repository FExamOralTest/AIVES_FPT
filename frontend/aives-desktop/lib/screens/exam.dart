import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:record/record.dart';

import '../api.dart';
import '../app.dart';
import '../audio_capture.dart';
import '../local_store.dart';
import '../models.dart';
import '../widgets.dart';
import 'reauth.dart';

class ExamScreen extends StatefulWidget {
  final AivesApi api;
  final Student student;
  final VivaSession session;
  final AnswerDraft draft;
  final InputDevice? device;
  final CloseGuard closeGuard;
  const ExamScreen({
    super.key,
    required this.api,
    required this.student,
    required this.session,
    required this.draft,
    required this.closeGuard,
    this.device,
  });
  @override
  State<ExamScreen> createState() => _ExamScreenState();
}

class _ExamScreenState extends State<ExamScreen> {
  final _capture = AudioCapture();
  Question? _question;
  Timer? _timer;
  bool _busy = true, _ready = false, _done = false, _uncertain = false;
  bool _expiryHandled = false, _recordAction = false, _promptOpen = false;
  String? _error, _notice, _transcript;
  double? _progress;
  int get _remaining =>
      widget.session.deadline.difference(DateTime.now().toUtc()).inSeconds;
  bool get _expired => _remaining <= 0;
  @override
  void initState() {
    super.initState();
    _question = widget.session.question;
    widget.closeGuard.handler = _leave;
    _capture.onSaved = (_) => _save();
    _restore();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted || _done) return;
      setState(() {});
      if (_ready &&
          _expired &&
          !_busy &&
          !_recordAction &&
          !_promptOpen &&
          !_capture.busy &&
          !_expiryHandled) {
        _expiryHandled = true;
        _timeUp();
      }
    });
  }

  Future<void> _restore() async {
    try {
      final saved = await widget.draft.read();
      if (saved != null && saved['question'] != null && _question != null) {
        final question = Question.fromJson(saved['question'] as Json);
        // The backend exposes the base question on resume. Restore a locally
        // acknowledged follow-up only while its base index is still current.
        if (question.id == _question!.id &&
            question.index == _question!.index) {
          _question = question;
          _uncertain = saved['uncertain'] == true;
          final path = saved['audio'] as String?;
          if (path != null && await widget.draft.containsRecording(path)) {
            _capture.path = path;
            _notice = 'An unsubmitted recording was restored from this computer. Play it back before submitting.';
          }
        }
      }
      await _save();
    } catch (e) {
      _error =
          'Cannot read or save the draft: $e. Resolve this error before recording.';
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
      return;
    }
    if (mounted) {
      setState(() {
        _busy = false;
        _ready = true;
      });
    }
  }

  Future<void> _save() =>
      widget.draft.save(_question, _capture.path, uncertain: _uncertain);
  Future<void> _record(Future<void> Function() operation) async {
    if (_recordAction || _busy || _capture.busy) return;
    setState(() {
      _recordAction = true;
      _error = null;
    });
    try {
      await operation();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _recordAction = false);
    }
  }

  Future<void> _startRecording() async {
    if (_expired || _question == null || _uncertain) return;
    if (_capture.path != null) {
      final again = await confirm(
        context,
        'Record again?',
        'The new recording will be submitted for this question. The previous recording remains in the local folder.',
        action: 'Record again',
      );
      if (!again || !mounted || _expired) return;
    }
    final path =
        '${widget.draft.folder.path}${Platform.pathSeparator}q_${_question!.key}_${DateTime.now().millisecondsSinceEpoch}.wav';
    await _capture.start(path, device: widget.device);
    await _save();
  }

  Future<void> _submit({bool finishAfter = false}) async {
    if (_done ||
        _busy ||
        _recordAction ||
        _capture.busy ||
        _uncertain ||
        _question == null) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
      _progress = 0;
    });
    var accepted = false;
    var sendStarted = false;
    try {
      if (_capture.recording) await _capture.stop();
      await _capture.stopPlayback();
      final path = _capture.path;
      if (path == null) {
        throw const ApiFailure('Record your answer before submitting.');
      }
      await widget.draft.archive.prepare(
        server: widget.api.http.options.baseUrl,
        student: widget.student,
        session: widget.session,
        question: _question,
        audio: path,
      );
      // Persist uncertainty BEFORE sending. A process crash after the server
      // accepts an answer must never cause an automatic duplicate POST.
      await widget.draft.save(_question, path, uncertain: true);
      sendStarted = true;
      final response = await widget.api.submit(
        widget.session.id,
        _question!,
        path,
        onSendProgress: (sent, total) {
          if (mounted) {
            setState(() => _progress = total > 0 ? sent / total : null);
          }
        },
      );
      accepted = true;
      _transcript = response['transcript'] as String?;
      _question = response['nextQuestion'] == null
          ? null
          : Question.fromJson(response['nextQuestion'] as Json);
      await _capture.clear();
      _uncertain = false;
      _notice = response['shouldFollowUp'] == true
          ? 'Answer received. Please answer the follow-up question below.'
          : 'The backend confirmed your answer.';
      await _save();
      if (response['isCompleted'] == true ||
          _question == null ||
          finishAfter ||
          _expired) {
        await _finishRequest();
      }
    } catch (e) {
      if (!accepted) {
        _uncertain = sendStarted && (e is! ApiFailure || e.uncertain);
        try {
          await _save();
        } catch (_) {
          /* The pre-send journal remains. */
        }
      }
      if (mounted) {
        setState(
          () => _error = accepted
              ? 'Your answer was received, but the next step failed: $e'
              : e.toString(),
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
          _progress = null;
        });
      }
    }
  }

  Future<void> _finishRequest() async {
    // This gate is outside backend error reconciliation: a local write failure
    // must never send a grading request or be mistaken for a network failure.
    await widget.draft.archive.prepare(
      server: widget.api.http.options.baseUrl,
      student: widget.student,
      session: widget.session,
      question: _question,
      finishing: true,
    );
    try {
      final response = await widget.api.finish(widget.session.id);
      if (response['status'] != 'COMPLETED' &&
          response['status'] != 'PENDING_REVIEW') {
        // The backend returns the result submission status. Only accept
        // a different representation after verifying the persisted enrollment.
        final sessions = await widget.api.sessions();
        if (!sessions.any((s) => s.id == widget.session.id && s.completed)) {
          throw const ApiFailure(
            'The backend has not confirmed the exam submission.',
          );
        }
      }
      _done = true;
      _notice =
          response['message'] as String? ??
          'The backend confirmed that your exam is complete.';
    } catch (e) {
      // Finish persists completion before waiting for AI; a lost response can
      // therefore still be verified without invoking grading a second time.
      try {
        final sessions = await widget.api.sessions();
        if (sessions.any((s) => s.id == widget.session.id && s.completed)) {
          _done = true;
          _notice = 'The backend received your submission. Results are being processed and are awaiting review.';
          return;
        }
      } catch (_) {
        /* Preserve the original failure. */
      }
      rethrow;
    }
  }

  Future<void> _finish({bool ask = true}) async {
    if (_done ||
        _busy ||
        _recordAction ||
        _capture.busy ||
        _uncertain ||
        _promptOpen) {
      return;
    }
    if (ask) {
      _promptOpen = true;
      final ok = await confirm(
        context,
        'Finish and submit the exam?',
        _capture.path != null || _capture.recording
            ? 'The app will submit your current recording and finish the exam. You cannot edit answers after submission.'
            : 'Unanswered questions will remain blank. You cannot edit answers after submission.',
        action: 'Submit exam',
      );
      _promptOpen = false;
      if (!mounted || !ok || _done || _busy) return;
    }
    if (_question != null && (_capture.path != null || _capture.recording)) {
      await _submit(finishAfter: true);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await _finishRequest();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _timeUp() async {
    if (_uncertain) {
      setState(
        () => _error = 'Time is up. The previous submission status is unknown; contact the proctor. Your recording is saved locally.',
      );
      return;
    }
    setState(
      () => _notice = 'Time is up. Submitting your current recording and finishing the exam...',
    );
    await _finish(ask: false);
  }

  Future<void> _reconcile() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final sessions = await widget.api.sessions();
      final enrollment = sessions.firstWhere((s) => s.id == widget.session.id);
      if (enrollment.completed) {
        _done = true;
        _notice = 'The backend confirmed your submission.';
        return;
      }
      final current = await widget.api.start(enrollment);
      if (current.question == null ||
          (_question != null && current.question!.index > _question!.index)) {
        _question = current.question;
        await _capture.clear();
        _uncertain = false;
        await _save();
        _notice = 'The backend has advanced to the next question. State synchronized without resubmitting the previous answer.';
      } else {
        _error = 'The backend cannot confirm whether the previous answer was received; a follow-up may have been created. Automatic resubmission is disabled. Ask the proctor to check. Your recording is preserved.';
      }
    } catch (e) {
      _error = e.toString();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _leave() async {
    if (_busy || _recordAction || _capture.busy) {
      showFailure(
        context,
        'A recording or submission is being processed. Please wait until it finishes.',
      );
      return false;
    }
    if (_done) return true;
    final ok = await confirm(
      context,
      'Leave the exam?',
      'The exam timer will keep running. Your recording is saved locally but has not been submitted. Return to complete your exam.',
      action: 'Save and leave',
    );
    if (!ok || !mounted) return false;
    try {
      if (_capture.recording) await _capture.stop();
      await _capture.stopPlayback();
      await _save();
      return true;
    } catch (e) {
      if (mounted) showFailure(context, e);
      return false;
    }
  }

  @override
  void dispose() {
    widget.closeGuard.handler = null;
    _timer?.cancel();
    _capture.dispose();
    super.dispose();
  }

  Future<void> _showRecordings() => showDialog<void>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('Local recordings'),
      content: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SelectableText(
                widget.draft.folder.path,
                style: const TextStyle(color: muted, fontSize: 12),
              ),
              const SizedBox(height: 12),
              const Text(
                'Recordings stay on this computer. Quit AIVES before opening this folder in Windows.',
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Close'),
        ),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: _done,
    onPopInvokedWithResult: (didPop, result) async {
      if (!didPop && await _leave() && context.mounted) {
        Navigator.of(context).pop();
      }
    },
    child: Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: false,
        toolbarHeight: 64,
        title: const BrandMark(),
        actions: [
          PopupMenuButton<String>(
            tooltip: 'Exam help',
            enabled: !_busy && !_recordAction && !_capture.recording,
            onSelected: (value) async {
              if (value == 'account') {
                await reauthenticate(context, widget.api, widget.student);
              } else {
                await _showRecordings();
              }
            },
            itemBuilder: (_) => [
              if (!_done)
                const PopupMenuItem(
                  value: 'account',
                  child: Text('Verify account'),
                ),
              const PopupMenuItem(
                value: 'recordings',
                child: Text('Local recordings'),
              ),
            ],
            icon: const Icon(Icons.help_outline),
          ),
          TextButton(
            onPressed: _busy || _recordAction
                ? null
                : () async {
                    if (await _leave() && context.mounted) {
                      Navigator.of(context).pop();
                    }
                  },
            child: Text(_done ? 'Back to exam code' : 'Leave exam'),
          ),
          const SizedBox(width: 18),
        ],
      ),
      body: Align(
        alignment: Alignment.topCenter,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 820),
          child: ListView(
            padding: const EdgeInsets.all(28),
            children: [
              if (_done) ...[
                const SizedBox(height: 50),
                const Icon(
                  Icons.check_circle_rounded,
                  color: Color(0xff067647),
                  size: 86,
                ),
                const SizedBox(height: 24),
                const Text(
                  'Exam submitted',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 34,
                    fontWeight: FontWeight.w700,
                    color: ink,
                  ),
                ),
                const SizedBox(height: 16),
                Notice(_notice ?? 'The backend confirmed your submission.'),
                const SizedBox(height: 16),
                const Text('A copy of your exam is saved on this computer.'),
                const SizedBox(height: 8),
                SelectableText(
                  widget.draft.folder.path,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: muted, fontSize: 12),
                ),
                const SizedBox(height: 16),
                const Text(
                  'You can close the app. Official results will be available on the AIVES website.',
                  textAlign: TextAlign.center,
                ),
              ] else ...[
                Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            widget.session.title,
                            style: const TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            '${widget.session.name} · ${widget.student.name}',
                            style: const TextStyle(color: muted),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 16),
                    Chip(
                      avatar: Icon(
                        Icons.timer_outlined,
                        color: _remaining < 60 ? Colors.red : brand,
                      ),
                      label: Text(
                        clockText(_remaining),
                        style: TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.w700,
                          color: _remaining < 60 ? Colors.red : ink,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                if (_error != null) ...[
                  Notice(_error!, error: true),
                  const SizedBox(height: 16),
                ],
                if (_notice != null) ...[
                  Notice(_notice!),
                  const SizedBox(height: 16),
                ],
                if (_uncertain) ...[
                  const Notice(
                    'The previous submission may have reached the server. Resubmission is temporarily disabled to prevent duplicate answers.',
                    error: true,
                  ),
                  const SizedBox(height: 12),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: OutlinedButton.icon(
                      onPressed: _busy ? null : _reconcile,
                      icon: const Icon(Icons.sync),
                      label: const Text('Check server status'),
                    ),
                  ),
                  const SizedBox(height: 16),
                ],
                Surface(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              _question == null
                                  ? 'All questions answered'
                                  : 'QUESTION ${_question!.index + 1} / ${widget.session.total}',
                              style: const TextStyle(
                                color: brand,
                                fontWeight: FontWeight.w700,
                                letterSpacing: 1,
                              ),
                            ),
                          ),
                          if (_question?.followUp == true)
                            const Chip(label: Text('Follow-up question')),
                        ],
                      ),
                      const SizedBox(height: 20),
                      SelectableText(
                        _question?.text ??
                            'Select Finish and submit to complete your exam.',
                        style: const TextStyle(
                          fontSize: 22,
                          height: 1.5,
                          fontWeight: FontWeight.w600,
                          color: ink,
                        ),
                      ),

                      if (_question != null) ...[
                        const Padding(
                          padding: EdgeInsets.symmetric(vertical: 24),
                          child: Divider(height: 1),
                        ),
                        RecordingPanel(
                          capture: _capture,
                          enabled:
                              _ready && !_busy && !_recordAction && !_uncertain,
                          onStart: _expired
                              ? null
                              : () => _record(_startRecording),
                          onStop: () => _record(_capture.stop),
                          onPlay: () => _record(() async {
                            if (_capture.playing) {
                              await _capture.stopPlayback();
                            } else {
                              await _capture.play();
                            }
                          }),
                        ),
                        const SizedBox(height: 24),
                        ListenableBuilder(
                          listenable: _capture,
                          builder: (context, _) => FilledButton.icon(
                            onPressed:
                                _ready &&
                                    !_busy &&
                                    !_recordAction &&
                                    !_capture.busy &&
                                    !_capture.recording &&
                                    !_uncertain &&
                                    _capture.path != null
                                ? () => _submit()
                                : null,
                            icon: const Icon(Icons.send_rounded),
                            label: const Text('Submit answer'),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                if (_busy) ...[
                  const SizedBox(height: 18),
                  LinearProgressIndicator(
                    value: _progress == 1 ? null : _progress,
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Processing... Please keep the app open.',
                    textAlign: TextAlign.center,
                  ),
                ],
                if (_transcript != null && _transcript!.isNotEmpty) ...[
                  const SizedBox(height: 20),
                  ExpansionTile(
                    title: const Text('Transcript of your latest answer'),
                    children: [
                      Padding(
                        padding: const EdgeInsets.all(16),
                        child: SelectableText(_transcript!),
                      ),
                    ],
                  ),
                ],
                const SizedBox(height: 24),
                Align(
                  alignment: Alignment.centerRight,
                  child: _question == null
                      ? FilledButton.icon(
                          onPressed:
                              _ready && !_busy && !_recordAction && !_uncertain
                              ? () => _finish()
                              : null,
                          icon: const Icon(Icons.task_alt),
                          label: const Text('Finish and submit'),
                        )
                      : TextButton(
                          onPressed:
                              _ready && !_busy && !_recordAction && !_uncertain
                              ? () => _finish()
                              : null,
                          child: const Text('Submit exam early'),
                        ),
                ),
              ],
              const SizedBox(height: 28),
            ],
          ),
        ),
      ),
    ),
  );
}
