import 'package:flutter/material.dart';

import '../api.dart';
import '../models.dart';
import '../widgets.dart';

Future<void> reauthenticate(
  BuildContext context,
  AivesApi api,
  Student student,
) async {
  await showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) => _ReauthDialog(api: api, student: student),
  );
}

class _ReauthDialog extends StatefulWidget {
  final AivesApi api;
  final Student student;
  const _ReauthDialog({required this.api, required this.student});
  @override
  State<_ReauthDialog> createState() => _ReauthDialogState();
}

class _ReauthDialogState extends State<_ReauthDialog> {
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;
  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_busy || _password.text.isEmpty) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final oldToken = widget.api.token;
    try {
      final user = await widget.api.login(widget.student.email, _password.text);
      if (user.id != widget.student.id) {
        widget.api.token = oldToken;
        throw const ApiFailure(
          'Sign in with the same account that started this exam.',
        );
      }
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Verify your account again'),
    content: SizedBox(
      width: 400,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(widget.student.email),
          const SizedBox(height: 16),
          if (_error != null) ...[
            Notice(_error!, error: true),
            const SizedBox(height: 12),
          ],
          TextField(
            controller: _password,
            obscureText: true,
            enabled: !_busy,
            onSubmitted: (_) => _submit(),
            decoration: const InputDecoration(labelText: 'Password'),
          ),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: _busy ? null : () => Navigator.pop(context),
        child: const Text('Close'),
      ),
      FilledButton(
        onPressed: _busy ? null : _submit,
        child: Text(_busy ? 'Verifying...' : 'Verify'),
      ),
    ],
  );
}
