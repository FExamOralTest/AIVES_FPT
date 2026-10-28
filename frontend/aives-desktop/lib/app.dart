import 'dart:io';

import 'package:flutter/material.dart';
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
    await windowManager.setMinimumSize(const Size(780, 600));
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
  const AivesApp({super.key, this.store, this.initialServer});
  @override
  State<AivesApp> createState() => _AivesAppState();
}

class _AivesAppState extends State<AivesApp> with WindowListener {
  final _closeGuard = CloseGuard();
  bool _closing = false;
  AivesApi? _api;
  Student? _student;
  @override
  void initState() {
    super.initState();
    windowManager.addListener(this);
  }

  @override
  void onWindowClose() async {
    if (_closing) return;
    _closing = true;
    try {
      if (await _closeGuard.handler?.call() ?? true) {
        await windowManager.destroy();
      }
    } finally {
      _closing = false;
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
    title: 'AIVES Desktop',
    debugShowCheckedModeBanner: false,
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
