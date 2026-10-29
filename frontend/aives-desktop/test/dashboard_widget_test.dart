import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:aives_desktop/api.dart';
import 'package:aives_desktop/app.dart';
import 'package:aives_desktop/local_store.dart';
import 'package:aives_desktop/models.dart';
import 'package:aives_desktop/screens/dashboard.dart';
import 'package:aives_desktop/screens/device_check.dart';

ExamSession session(
  int id, {
  String status = 'IN_PROGRESS',
  String enrollment = 'NOT_STARTED',
}) {
  final now = DateTime.now().toUtc();
  return ExamSession.fromJson({
    'sessionId': id,
    'examTitle': 'Exam $id',
    'sessionName': 'Session $id',
    'courseName': 'Software Architecture',
    'sessionStatus': status,
    'enrollmentStatus': enrollment,
    'startTime': now.subtract(const Duration(minutes: 5)).toIso8601String(),
    'endTime': now.add(const Duration(hours: 1)).toIso8601String(),
  });
}

class _JoinApi extends AivesApi {
  List<ExamSession> enrollments = [];
  ExamSession joined = session(5);
  String? failure;
  int enrollCalls = 0, startCalls = 0;
  String? code;
  _JoinApi() : super('http://localhost:5116');

  @override
  Future<List<ExamSession>> sessions() async => [...enrollments];

  @override
  Future<int> enroll(String value) async {
    enrollCalls++;
    code = value;
    if (failure != null) throw ApiFailure(failure!);
    enrollments = [...enrollments, joined];
    return joined.id;
  }

  @override
  Future<VivaSession> start(ExamSession session) async {
    startCalls++;
    throw StateError('An exam must not start before microphone confirmation.');
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const channel = MethodChannel('com.llfbandit.record/messages');
  setUp(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          channel,
          (call) async =>
              call.method == 'listInputDevices' ? <dynamic>[] : null,
        );
  });
  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  Future<void> mount(WidgetTester tester, _JoinApi api) async {
    tester.view.physicalSize = const Size(780, 600);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(api.close);
    await tester.pumpWidget(
      MaterialApp(
        home: DashboardScreen(
          api: api,
          student: Student.fromJson({
            'userId': 7,
            'fullName': 'Student A',
            'role': 'STUDENT',
          }),
          store: LocalStore(Directory.systemTemp),
          closeGuard: CloseGuard(),
          onLogout: () {},
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> unmount(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
    await tester.pump();
  }

  testWidgets(
    'home focuses on code, validates empty input and collapses joined exams',
    (tester) async {
      final api = _JoinApi()
        ..enrollments = [session(3, enrollment: 'IN_PROGRESS')];
      await mount(tester, api);
      expect(find.text('Enter your exam code'), findsOneWidget);
      expect(find.text('Exam 3').hitTestable(), findsNothing);
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(
        find.text('Enter the exam code from your proctor.'),
        findsOneWidget,
      );
      expect(api.enrollCalls, 0);
      expect(tester.takeException(), isNull);
      await unmount(tester);
    },
  );

  testWidgets(
    'valid code opens microphone check for the returned session only',
    (tester) async {
      final api = _JoinApi()..enrollments = [session(3)];
      await mount(tester, api);
      await tester.enterText(find.byType(TextFormField), 'JOIN5');
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(api.code, 'JOIN5');
      expect(find.byType(DeviceCheck), findsOneWidget);
      expect(api.startCalls, 0);
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'Start exam'),
            )
            .onPressed,
        isNull,
      );
      await tester.tap(find.text('Back'));
      await tester.pumpAndSettle();
      expect(find.text('Exam 5').hitTestable(), findsOneWidget);
      expect(find.text('Exam 3').hitTestable(), findsNothing);
      expect(tester.takeException(), isNull);
      await unmount(tester);
    },
  );

  testWidgets(
    'waiting session can refresh and continue without entering its code again',
    (tester) async {
      final api = _JoinApi()..joined = session(5, status: 'NOT_STARTED');
      await mount(tester, api);
      await tester.enterText(find.byType(TextFormField), 'JOIN5');
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(find.byType(DeviceCheck), findsNothing);
      expect(find.text('Waiting for session to open'), findsOneWidget);
      api.enrollments = [session(5)];
      await tester.ensureVisible(find.text('Check again'));
      await tester.tap(find.text('Check again'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Continue to microphone check'));
      await tester.tap(find.text('Continue to microphone check'));
      await tester.pumpAndSettle();
      expect(find.byType(DeviceCheck), findsOneWidget);
      expect(api.enrollCalls, 1);
      expect(api.startCalls, 0);
      expect(tester.takeException(), isNull);
      await unmount(tester);
    },
  );

  testWidgets('invalid code keeps the student on the entry screen', (
    tester,
  ) async {
    final api = _JoinApi()..failure = 'Invalid or expired Enroll Code.';
    await mount(tester, api);
    await tester.enterText(find.byType(TextFormField), 'INVALID');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();
    expect(find.text(api.failure!), findsOneWidget);
    expect(find.byType(DeviceCheck), findsNothing);
    expect(api.startCalls, 0);
    expect(tester.takeException(), isNull);
    await unmount(tester);
  });

  testWidgets(
    'existing attempt resumes through joined exams and explains its running timer',
    (tester) async {
      final api = _JoinApi()
        ..enrollments = [session(3, enrollment: 'IN_PROGRESS')];
      await mount(tester, api);
      await tester.tap(find.text('Joined exams'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Resume exam'));
      await tester.tap(find.text('Resume exam'));
      await tester.pumpAndSettle();
      expect(
        tester.widget<DeviceCheck>(find.byType(DeviceCheck)).resuming,
        isTrue,
      );
      expect(
        find.textContaining('Your exam timer is still running.'),
        findsOneWidget,
      );
      expect(api.enrollCalls, 0);
      expect(api.startCalls, 0);
      expect(tester.takeException(), isNull);
      await unmount(tester);
    },
  );
}
