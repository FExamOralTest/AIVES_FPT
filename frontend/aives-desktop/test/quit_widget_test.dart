import 'dart:async';

import 'package:aives_desktop/app.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const channel = MethodChannel('aives/window');
  var quitCalls = 0;

  setUp(() {
    quitCalls = 0;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, (call) async {
          expect(call.method, 'quit');
          quitCalls++;
          return null;
        });
  });
  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  Future<void> mount(WidgetTester tester, {CloseGuard? guard}) async {
    tester.view.physicalSize = const Size(800, 600);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(AivesApp(closeGuard: guard));
    await tester.pumpAndSettle();
  }

  testWidgets('Quit stays visible above routes and modal dialogs', (
    tester,
  ) async {
    await mount(tester);
    final navigator = tester.state<NavigatorState>(
      find.byType(Navigator).first,
    );
    unawaited(
      navigator.push<void>(
        MaterialPageRoute(
          builder: (context) => Scaffold(
            body: TextButton(
              onPressed: () => showDialog<void>(
                context: context,
                builder: (_) =>
                    const AlertDialog(title: Text('Microphone dialog')),
              ),
              child: const Text('Open dialog'),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Open dialog'));
    await tester.pumpAndSettle();
    expect(find.text('Microphone dialog'), findsOneWidget);
    await tester.tap(find.byKey(const Key('quit-app')));
    await tester.pumpAndSettle();
    expect(find.text('Quit AIVES?'), findsOneWidget);
    expect(quitCalls, 0);
    await tester.tap(find.text('Back'));
    await tester.pumpAndSettle();
    expect(quitCalls, 0);
    expect(find.text('Microphone dialog'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('Quit requires confirmation before calling the native runner', (
    tester,
  ) async {
    await mount(tester);
    await tester.tap(find.byKey(const Key('quit-app')));
    await tester.pumpAndSettle();
    expect(quitCalls, 0);
    await tester.tap(find.widgetWithText(FilledButton, 'Quit'));
    await tester.pumpAndSettle();
    expect(quitCalls, 1);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'Quit waits for the recording guard and ignores repeated clicks',
    (tester) async {
      final saved = Completer<bool>();
      var guardCalls = 0;
      final guard = CloseGuard()
        ..handler = () {
          guardCalls++;
          return saved.future;
        };
      await mount(tester, guard: guard);
      await tester.tap(find.byKey(const Key('quit-app')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('quit-app')));
      await tester.pump();
      expect(guardCalls, 1);
      expect(quitCalls, 0);
      saved.complete(true);
      await tester.pumpAndSettle();
      expect(quitCalls, 1);
    },
  );

  testWidgets('Cancelled or failed recording save keeps the app open', (
    tester,
  ) async {
    final guard = CloseGuard()..handler = () async => false;
    await mount(tester, guard: guard);
    await tester.tap(find.byKey(const Key('quit-app')));
    await tester.pumpAndSettle();
    expect(quitCalls, 0);
    guard.handler = () async => throw StateError('Cannot save the recording');
    await tester.tap(find.byKey(const Key('quit-app')));
    await tester.pumpAndSettle();
    expect(quitCalls, 0);
    expect(find.textContaining('Cannot save the recording'), findsOneWidget);
    expect(
      tester.widget<TextButton>(find.byKey(const Key('quit-app'))).onPressed,
      isNotNull,
    );
    expect(tester.takeException(), isNull);
  });
}
