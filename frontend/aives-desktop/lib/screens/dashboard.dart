import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../app.dart';
import '../local_store.dart';
import '../models.dart';
import '../widgets.dart';
import 'device_check.dart';
import 'exam.dart';
import 'reauth.dart';
import 'result.dart';

class DashboardScreen extends StatefulWidget {
  final AivesApi api;
  final Student student;
  final LocalStore store;
  final CloseGuard closeGuard;
  final VoidCallback onLogout;
  const DashboardScreen({
    super.key,
    required this.api,
    required this.student,
    required this.store,
    required this.closeGuard,
    required this.onLogout,
  });
  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  final _code = TextEditingController();
  List<ExamSession> _sessions = [];
  bool _loading = true, _joining = false, _entering = false;
  String? _error, _notice;
  Timer? _clock;
  @override
  void initState() {
    super.initState();
    _load();
    _clock = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _code.dispose();
    _clock?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final sessions = await widget.api.sessions();
      sessions.sort((a, b) => b.start.compareTo(a.start));
      if (mounted) setState(() => _sessions = sessions);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _join() async {
    if (_joining || _code.text.trim().isEmpty) return;
    setState(() {
      _joining = true;
      _error = null;
      _notice = null;
    });
    try {
      await widget.api.enroll(_code.text);
      _code.clear();
      if (mounted) {
        setState(
          () => _notice =
              'Session joined. Wait for the proctor to open the session.',
        );
      }
      await _load();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _joining = false);
    }
  }

  Future<void> _enter(ExamSession enrollment) async {
    if (_entering) return;
    setState(() => _entering = true);
    try {
      final ready = await showDialog<DeviceReady>(
        context: context,
        barrierDismissible: false,
        builder: (_) =>
            DeviceCheck(store: widget.store, closeGuard: widget.closeGuard),
      );
      if (!mounted || ready == null) return;
      final accepted = await confirm(
        context,
        'Start this exam?',
        'The timer starts when you first enter the exam and keeps running when the app is closed.',
        action: 'Enter exam',
      );
      if (!mounted || !accepted) return;
      final session = await widget.api.start(enrollment);
      final folder = await widget.store.sessionFolder(
        widget.api.http.options.baseUrl,
        widget.student.id,
        session.id,
      );
      if (!mounted) return;
      await Navigator.push(
        context,
        MaterialPageRoute<void>(
          builder: (_) => ExamScreen(
            api: widget.api,
            student: widget.student,
            session: session,
            draft: AnswerDraft(folder),
            device: ready.microphone,
            closeGuard: widget.closeGuard,
          ),
        ),
      );
      if (mounted) await _load();
    } catch (e) {
      if (mounted) showFailure(context, e);
    } finally {
      if (mounted) setState(() => _entering = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      toolbarHeight: 78,
      title: const BrandMark(),
      actions: [
        TextButton.icon(
          onPressed: _entering || _joining
              ? null
              : () async {
                  await reauthenticate(context, widget.api, widget.student);
                  if (mounted) await _load();
                },
          icon: const Icon(Icons.key),
          label: const Text('Verify account'),
        ),
        TextButton.icon(
          onPressed: _entering || _joining ? null : widget.onLogout,
          icon: const Icon(Icons.logout),
          label: const Text('Sign out'),
        ),
        const SizedBox(width: 16),
      ],
    ),
    body: Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1120),
        child: ListView(
          padding: const EdgeInsets.all(28),
          children: [
            Container(
              padding: const EdgeInsets.all(30),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(22),
                color: const Color(0xff18366a),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'YOUR EXAM WORKSPACE',
                    style: TextStyle(
                      color: Color(0xff9fc7ff),
                      letterSpacing: 2,
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'Hello, ${widget.student.name}',
                    style: const TextStyle(
                      fontSize: 29,
                      fontWeight: FontWeight.w700,
                      color: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 10),
                  const Text(
                    'Prepare your headphones and microphone. Each answer is recorded and submitted to AIVES.',
                    style: TextStyle(fontSize: 15, color: Color(0xffdfebff)),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            Surface(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Join an exam session',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Enter the session code provided by your proctor.',
                    style: TextStyle(color: muted),
                  ),
                  const SizedBox(height: 18),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _code,
                          enabled: !_joining && !_entering,
                          onSubmitted: (_) => _join(),
                          decoration: const InputDecoration(
                            labelText: 'Session code',
                            prefixIcon: Icon(
                              Icons.confirmation_number_outlined,
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 14),
                      FilledButton(
                        onPressed: _joining || _entering ? null : _join,
                        child: Text(_joining ? 'Joining...' : 'Join'),
                      ),
                    ],
                  ),
                ],
              ),
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
            Row(
              children: [
                const Expanded(
                  child: Text(
                    'My exam sessions',
                    style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700),
                  ),
                ),
                OutlinedButton.icon(
                  onPressed: _loading || _entering ? null : _load,
                  icon: const Icon(Icons.refresh),
                  label: const Text('Refresh'),
                ),
              ],
            ),
            const SizedBox(height: 18),
            if (_loading) const LinearProgressIndicator(),
            if (!_loading && _sessions.isEmpty && _error == null)
              const Surface(
                child: Padding(
                  padding: EdgeInsets.all(24),
                  child: Center(
                    child: Text(
                      'You have not joined any exam sessions. Enter a session code above.',
                    ),
                  ),
                ),
              ),
            ..._sessions.map(
              (session) => Padding(
                padding: const EdgeInsets.only(bottom: 14),
                child: Surface(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: const Color(0xffeaf3ff),
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: const Icon(
                              Icons.record_voice_over_outlined,
                              color: brand,
                            ),
                          ),
                          const SizedBox(width: 16),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  session.title,
                                  style: const TextStyle(
                                    fontSize: 19,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                                const SizedBox(height: 5),
                                Text(
                                  '${session.course} · ${session.name}',
                                  style: const TextStyle(color: muted),
                                ),
                              ],
                            ),
                          ),
                          Chip(label: Text(session.label)),
                        ],
                      ),
                      const SizedBox(height: 18),
                      Wrap(
                        spacing: 20,
                        runSpacing: 12,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          Text(
                            '${_date(session.start)} — ${_date(session.end)}',
                            style: const TextStyle(color: muted),
                          ),
                          if (session.canEnter)
                            FilledButton.icon(
                              onPressed: _entering
                                  ? null
                                  : () => _enter(session),
                              icon: const Icon(Icons.arrow_forward),
                              label: Text(
                                _entering
                                    ? 'Opening...'
                                    : session.enrollmentStatus == 'IN_PROGRESS'
                                    ? 'Resume exam'
                                    : 'Enter exam',
                              ),
                            ),
                          if (session.completed)
                            OutlinedButton.icon(
                              onPressed: _entering
                                  ? null
                                  : () => Navigator.push(
                                      context,
                                      MaterialPageRoute<void>(
                                        builder: (_) => ResultScreen(
                                          api: widget.api,
                                          sessionId: session.id,
                                        ),
                                      ),
                                    ),
                              icon: const Icon(Icons.assignment_outlined),
                              label: const Text('View results'),
                            ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 18),
            Text(
              'Server: ${widget.api.http.options.baseUrl}',
              style: const TextStyle(color: muted, fontSize: 12),
            ),
          ],
        ),
      ),
    ),
  );
}

String _date(DateTime value) {
  final local = value.toLocal();
  String p(int n) => n.toString().padLeft(2, '0');
  return '${p(local.day)}/${p(local.month)}/${local.year} ${p(local.hour)}:${p(local.minute)}';
}
