import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:aives_desktop/app.dart';

void main() {
  testWidgets('AIVES login validates input and fits the desktop window', (
    WidgetTester tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(const AivesApp());

    expect(find.text('AIVES'), findsOneWidget);
    expect(find.text('Welcome back'), findsOneWidget);
    expect(find.text('Flutter Demo Home Page'), findsNothing);

    await tester.tap(find.text('Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Enter your AIVES account email.'), findsOneWidget);
    expect(find.text('Enter your password.'), findsOneWidget);
    expect(tester.takeException(), isNull);
    tester.view.physicalSize = const Size(800, 600);
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}
