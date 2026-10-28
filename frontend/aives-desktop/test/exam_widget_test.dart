import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:aives_desktop/api.dart';
import 'package:aives_desktop/app.dart';
import 'package:aives_desktop/local_store.dart';
import 'package:aives_desktop/models.dart';
import 'package:aives_desktop/screens/exam.dart';

class _MemoryDraft extends AnswerDraft {
  _MemoryDraft() : super(Directory('unused-test-recordings'));
  @override
  Future<Json?> read() async => null;
  @override
  Future<void> save(
    Question? question,
    String? audio, {
    bool uncertain = false,
  }) async {}
}

class _ExamApi extends AivesApi {
  final bool fail;
  int finishCalls = 0;
  _ExamApi({this.fail = false}) : super('http://localhost:5116');
  @override
  Future<Json> finish(int sessionId) async {
    finishCalls++;
    if (fail) {
      throw const ApiFailure(
        'The server has not confirmed the submission.',
        uncertain: true,
      );
    }
    return {
      'status': 'PENDING_REVIEW',
      'message': 'The backend received your submission.',
    };
  }

  @override
  Future<List<ExamSession>> sessions() async => [];
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const recordChannel = MethodChannel('com.llfbandit.record/messages');
  setUp(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(recordChannel, (_) async => null);
  });
  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(recordChannel, null);
  });

  Future<void> mount(
    WidgetTester tester,
    _ExamApi api, {
    bool withQuestion = false,
  }) async {
    tester.view.physicalSize = const Size(800, 600);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final end = DateTime.now().toUtc().add(const Duration(hours: 1));
    final session = VivaSession.fromJson({
      'sessionId': 5,
      'sessionName': 'SWD392 exam session',
      'examTitle': 'Software Architecture',
      'totalQuestions': 1,
      'durationMinutes': 30,
      'startedAt': DateTime.now().toUtc().toIso8601String(),
      'currentQuestion': withQuestion
          ? {
              'questionId': 10,
              'questionIndex': 0,
              'totalQuestions': 1,
              'questionText': 'Explain layered architecture.',
              'isFollowUp': false,
            }
          : null,
    }, end);
    await tester.pumpWidget(
      MaterialApp(
        home: ExamScreen(
          api: api,
          student: Student.fromJson({
            'userId': 7,
            'fullName': 'Student A',
            'email': 'a@example.com',
            'role': 'STUDENT',
          }),
          session: session,
          draft: _MemoryDraft(),
          closeGuard: CloseGuard(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    addTearDown(api.close);
  }

  testWidgets(
    'room renders a real question and recorder controls at minimum size',
    (tester) async {
      await mount(tester, _ExamApi(), withQuestion: true);
      expect(find.text('Explain layered architecture.'), findsOneWidget);
      expect(find.text('Start recording'), findsOneWidget);
      expect(find.text('Exam submitted'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      await tester.pump();
    },
  );

  testWidgets(
    'completion requires the final confirmation and server acknowledgement',
    (tester) async {
      final api = _ExamApi();
      await mount(tester, api);
      await tester.tap(find.text('Finish and submit'));
      await tester.pumpAndSettle();
      expect(api.finishCalls, 0);
      await tester.tap(find.widgetWithText(FilledButton, 'Submit exam'));
      await tester.pumpAndSettle();
      expect(api.finishCalls, 1);
      expect(find.text('Exam submitted'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      await tester.pump();
    },
  );

  testWidgets('finish failure never displays successful completion', (
    tester,
  ) async {
    final api = _ExamApi(fail: true);
    await mount(tester, api);
    await tester.tap(find.text('Finish and submit'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Submit exam'));
    await tester.pumpAndSettle();
    expect(api.finishCalls, 1);
    expect(find.text('Exam submitted'), findsNothing);
    expect(
      find.text('The server has not confirmed the submission.'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    await tester.pump();
  });
}
