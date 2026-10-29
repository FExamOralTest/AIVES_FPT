import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:window_manager/window_manager.dart';

import 'api.dart';
import 'local_store.dart';
import 'models.dart';
import 'screens/login.dart';
import 'screens/dashboard.dart';
import 'widgets.dart';

class CloseGuard {
  Future<bool> Function()? handler;
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  if (Platform.isWindows) {
    await windowManager.ensureInitialized();
    await windowManager.setTitle('AIVES • Oral Examination');
    await windowManager.setPreventClose(true);
  }
  final store = await LocalStore.open();
  final savedServer = await store.readServer();
  runApp(AivesApp(store: store, initialServer: savedServer));
}

class AivesApp extends StatefulWidget {
  final LocalStore? store;
  final String? initialServer;
  final CloseGuard? closeGuard;
  const AivesApp({super.key, this.store, this.initialServer, this.closeGuard});
  @override
  State<AivesApp> createState() => _AivesAppState();
}

class _AivesAppState extends State<AivesApp> with WindowListener {
  static const _windowChannel = MethodChannel('aives/window');
  final _navigatorKey = GlobalKey<NavigatorState>();
  late final CloseGuard _closeGuard;
  bool _closing = false;
  AivesApi? _api;
  Student? _student;
  @override
  void initState() {
    super.initState();
    _closeGuard = widget.closeGuard ?? CloseGuard();
    windowManager.addListener(this);
  }

  @override
  void onWindowClose() => _quit();

  Future<void> _quit() async {
    if (_closing) return;
    setState(() => _closing = true);
    try {
      final handler = _closeGuard.handler;
      final context = _navigatorKey.currentContext;
      if (context == null) return;
      final allowed = handler != null
          ? await handler()
          : await confirm(
              context,
              'Quit AIVES?',
              'The app will close and you will return to Windows.',
              action: 'Quit',
            );
      if (allowed && mounted) {
        await _windowChannel.invokeMethod<void>('quit');
      }
    } catch (error) {
      final context = _navigatorKey.currentContext;
      if (context != null && context.mounted) showFailure(context, error);
    } finally {
      if (mounted) setState(() => _closing = false);
    }
  }

  @override
  void dispose() {
    windowManager.removeListener(this);
    _api?.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
    navigatorKey: _navigatorKey,
    title: 'AIVES Desktop',
    debugShowCheckedModeBanner: false,
    builder: (context, child) => Column(
      children: [
        Material(
          color: const Color(0xfffafbfc),
          child: SafeArea(
            bottom: false,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton.icon(
                    key: const Key('quit-app'),
                    onPressed: _closing ? null : _quit,
                    icon: const Icon(Icons.power_settings_new, size: 20),
                    label: const Text('Quit'),
                  ),
                ],
              ),
            ),
          ),
        ),
        Expanded(child: child ?? const SizedBox.shrink()),
      ],
    ),
    theme: ThemeData(
      useMaterial3: true,
      fontFamily: 'Segoe UI',
      scaffoldBackgroundColor: const Color(0xfffafbfc),
      colorScheme: ColorScheme.fromSeed(
        seedColor: brand,
        primary: brand,
        surface: Colors.white,
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xfffafbfc),
        surfaceTintColor: Colors.transparent,
        toolbarHeight: 64,
        elevation: 0,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: const Color(0xfff8fafc),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
        enabledBorder: OutlineInputBorder(
          borderSide: const BorderSide(color: Color(0xffdce3ee)),
          borderRadius: BorderRadius.circular(8),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
    ),
    home: _student == null
        ? LoginScreen(
            initialServer: widget.initialServer,
            store: widget.store,
            onLogin: (api, student) {
              setState(() {
                _api = api;
                _student = student;
              });
            },
          )
        : DashboardScreen(
            api: _api!,
            student: _student!,
            store: widget.store!,
            closeGuard: _closeGuard,
            onLogout: () {
              _api?.close();
              setState(() {
                _api = null;
                _student = null;
              });
            },
          ),
  );
}
