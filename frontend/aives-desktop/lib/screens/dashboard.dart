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
  final _form = GlobalKey<FormState>();
  final _code = TextEditingController();
  List<ExamSession> _sessions = [];
  int? _selectedId;
  bool _loading = true, _joining = false, _entering = false;
  String? _error;
  Timer? _clock;
  bool get _busy => _joining || _entering;

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
    if (_busy || _loading || !_form.currentState!.validate()) return;
    setState(() {
      _joining = true;
      _error = null;
      _selectedId = null;
    });
    try {
      final id = await widget.api.enroll(_code.text);
      if (!mounted) return;
      setState(() => _selectedId = id);
      await _load();
      if (!mounted) return;
      final selected = _sessions.where((s) => s.id == id).firstOrNull;
      if (selected == null && _error == null) {
        setState(
          () => _error = 'Exam joined. Refresh joined exams to continue.',
        );
      } else if (selected?.canEnter == true) {
        await _enter(selected!);
      }
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
        builder: (_) => DeviceCheck(
          store: widget.store,
          closeGuard: widget.closeGuard,
          resuming: enrollment.enrollmentStatus == 'IN_PROGRESS',
        ),
      );
      if (!mounted || ready == null) return;
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
  Widget build(BuildContext context) {
    final selected = _sessions.where((s) => s.id == _selectedId).firstOrNull;
    return Scaffold(
      appBar: AppBar(
        title: const BrandMark(),
        actions: [
          TextButton.icon(
            onPressed: _busy
                ? null
                : () async {
                    await reauthenticate(context, widget.api, widget.student);
                    if (mounted) await _load();
                  },
            icon: const Icon(Icons.help_outline, size: 20),
            label: const Text('Account help'),
          ),
          TextButton(
            onPressed: _busy ? null : widget.onLogout,
            child: const Text('Sign out'),
          ),
          const SizedBox(width: 16),
        ],
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(28),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  widget.student.name,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: muted),
                ),
                const SizedBox(height: 20),
                Surface(
                  padding: const EdgeInsets.all(32),
                  child: Form(
                    key: _form,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'Enter your exam code',
                          style: TextStyle(
                            fontSize: 26,
                            fontWeight: FontWeight.w700,
                            color: ink,
                          ),
                        ),
                        const SizedBox(height: 10),
                        const Text(
                          'Use the code provided by your proctor.',
                          style: TextStyle(color: muted),
                        ),
                        const SizedBox(height: 28),
                        TextFormField(
                          controller: _code,
                          autofocus: true,
                          enabled: !_busy,
                          textCapitalization: TextCapitalization.characters,
                          textInputAction: TextInputAction.go,
                          onFieldSubmitted: (_) => _join(),
                          decoration: const InputDecoration(
                            labelText: 'Exam code',
                          ),
                          validator: (v) => v == null || v.trim().isEmpty
                              ? 'Enter the exam code from your proctor.'
                              : null,
                        ),
                        const SizedBox(height: 18),
                        FilledButton(
                          onPressed: _busy || _loading ? null : _join,
                          child: Text(
                            _joining || _entering
                                ? 'Opening exam...'
                                : 'Continue',
                          ),
                        ),
                        if (_loading) ...[
                          const SizedBox(height: 16),
                          const LinearProgressIndicator(),
                        ],
                        if (_error != null) ...[
                          const SizedBox(height: 18),
                          Notice(_error!, error: true),
                          TextButton.icon(
                            onPressed: _busy || _loading ? null : _load,
                            icon: const Icon(Icons.refresh),
                            label: const Text('Refresh joined exams'),
                          ),
                        ],
                        if (selected != null) ...[
                          const SizedBox(height: 24),
                          _sessionTile(selected),
                        ],
                      ],
                    ),
                  ),
                ),
                if (_sessions.any((s) => s.id != _selectedId)) ...[
                  const SizedBox(height: 16),
                  ExpansionTile(
                    title: const Text('Joined exams'),
                    subtitle: const Text('Continue an exam you already joined'),
                    tilePadding: const EdgeInsets.symmetric(horizontal: 8),
                    children: [
                      ..._sessions
                          .where((s) => s.id != _selectedId)
                          .map(_sessionTile),
                      TextButton.icon(
                        onPressed: _loading || _busy ? null : _load,
                        icon: const Icon(Icons.refresh, size: 18),
                        label: const Text('Refresh'),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _sessionTile(ExamSession session) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          session.title,
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 4),
        Text(session.name, style: const TextStyle(color: muted)),
        const SizedBox(height: 8),
        Text(session.label, style: const TextStyle(color: muted)),
        if (!session.canEnter && !session.completed) ...[
          const SizedBox(height: 4),
          Text(
            '${_date(session.start)} – ${_date(session.end)}',
            style: const TextStyle(color: muted, fontSize: 12),
          ),
        ],
        if (session.canEnter) ...[
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _busy || _loading ? null : () => _enter(session),
            child: Text(
              session.enrollmentStatus == 'IN_PROGRESS'
                  ? 'Resume exam'
                  : 'Continue to microphone check',
            ),
          ),
        ] else if (session.completed) ...[
          const SizedBox(height: 4),
          const Text(
            'View official results on the AIVES website.',
            style: TextStyle(color: muted, fontSize: 12),
          ),
        ] else if (session.label == 'Waiting for session to open')
          TextButton.icon(
            onPressed: _busy || _loading ? null : _load,
            icon: const Icon(Icons.refresh, size: 18),
            label: const Text('Check again'),
          ),
      ],
    ),
  );
}

String _date(DateTime value) {
  final local = value.toLocal();
  String p(int n) => n.toString().padLeft(2, '0');
  return '${p(local.day)}/${p(local.month)}/${local.year} ${p(local.hour)}:${p(local.minute)}';
}
