import 'package:flutter/material.dart';

import '../api.dart';
import '../local_store.dart';
import '../models.dart';
import '../widgets.dart';

const defaultServer = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5116',
);

class LoginScreen extends StatefulWidget {
  final String? initialServer;
  final LocalStore? store;
  final void Function(AivesApi, Student) onLogin;
  const LoginScreen({
    super.key,
    this.initialServer,
    this.store,
    required this.onLogin,
  });
  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController(), _password = TextEditingController();
  late final _server = TextEditingController(
    text: widget.initialServer ?? defaultServer,
  );
  bool _busy = false, _obscure = true;
  String? _error;
  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _server.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    AivesApi? api;
    try {
      api = AivesApi(_server.text);
      final student = await api.login(_email.text, _password.text);
      await widget.store?.saveServer(api.http.options.baseUrl);
      if (!mounted) {
        api.close();
        return;
      }
      _password.clear();
      widget.onLogin(api, student);
    } catch (e) {
      api?.close();
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(36),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Form(
            key: _form,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const BrandMark(),
                const SizedBox(height: 40),
                const Text(
                  'Student sign in',
                  style: TextStyle(
                    fontSize: 28,
                    fontWeight: FontWeight.w700,
                    color: ink,
                  ),
                ),
                const SizedBox(height: 10),
                const Text(
                  'Sign in, enter your exam code and begin.',
                  style: TextStyle(color: muted, fontSize: 16),
                ),
                const SizedBox(height: 26),
                if (_error != null) ...[
                  Notice(_error!, error: true),
                  const SizedBox(height: 18),
                ],
                TextFormField(
                  controller: _email,
                  enabled: !_busy,
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.email],
                  decoration: const InputDecoration(
                    labelText: 'Email',
                    prefixIcon: Icon(Icons.alternate_email),
                  ),
                  validator: (v) => v == null || !v.trim().contains('@')
                      ? 'Enter your AIVES account email.'
                      : null,
                ),
                const SizedBox(height: 18),
                TextFormField(
                  controller: _password,
                  enabled: !_busy,
                  obscureText: _obscure,
                  onFieldSubmitted: (_) => _login(),
                  decoration: InputDecoration(
                    labelText: 'Password',
                    prefixIcon: const Icon(Icons.lock_outline),
                    suffixIcon: IconButton(
                      tooltip: _obscure ? 'Show password' : 'Hide password',
                      onPressed: () => setState(() => _obscure = !_obscure),
                      icon: Icon(
                        _obscure
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                    ),
                  ),
                  validator: (v) =>
                      v == null || v.isEmpty ? 'Enter your password.' : null,
                ),
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: _busy ? null : _login,
                  icon: _busy
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.login),
                  label: Text(_busy ? 'Signing in...' : 'Sign in'),
                ),
                const SizedBox(height: 18),
                ExpansionTile(
                  title: const Text('Server settings'),
                  tilePadding: EdgeInsets.zero,
                  childrenPadding: const EdgeInsets.only(bottom: 16),
                  children: [
                    TextFormField(
                      controller: _server,
                      enabled: !_busy,
                      decoration: const InputDecoration(
                        labelText: 'Backend URL',
                        hintText: 'https://api.example.com',
                      ),
                      validator: (v) {
                        try {
                          AivesApi.validateServer(v ?? '');
                          return null;
                        } catch (e) {
                          return e.toString();
                        }
                      },
                    ),
                    const SizedBox(height: 10),
                    const Text(
                      'Enter the AIVES backend URL, not the website or AI service URL. HTTP is allowed only for localhost.',
                      style: TextStyle(color: muted, fontSize: 12),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}
