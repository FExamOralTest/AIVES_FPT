import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:aives_desktop/api.dart';
import 'package:aives_desktop/app.dart';
import 'package:aives_desktop/local_store.dart';
import 'package:aives_desktop/exam_archive.dart';
import 'package:aives_desktop/models.dart';
import 'package:aives_desktop/screens/exam.dart';

class _MemoryDraft extends AnswerDraft {
  final _MemoryArchive backup;
  final Json? restored;
  _MemoryDraft({
    bool failBackup = false,
    List<String>? events,
    Directory? folder,
    this.restored,
  }) : backup = _MemoryArchive(failBackup, events ?? []),
       super(folder ?? Directory('unused-test-recordings'));
  @override
  ExamArchive get archive => backup;
  @override
  Future<Json?> read() async => restored;
  @override
  Future<bool> containsRecording(String path) async =>
      restored?['audio'] == path;
  @override
  Future<void> save(
    Question? question,
    String? audio, {
    bool uncertain = false,
  }) async {}
}

class _MemoryArchive extends ExamArchive {
  bool fail;
  final List<String> events;
  _MemoryArchive(this.fail, this.events)
    : super(Directory('unused-test-recordings'));
  @override
  Future<void> prepare({
    required String server,
    required Student student,
    required VivaSession session,
    Question? question,
    String? audio,
    bool finishing = false,
  }) async {
    events.add('backup');
    if (fail) throw const FileSystemException('Local backup failed: disk full');
  }
}

class _ExamApi extends AivesApi {
  final bool fail;
  int finishCalls = 0;
  final List<String> events;
  _ExamApi({this.fail = false, List<String>? events})
    : events = events ?? [],
      super('http://localhost:5116');
  @override
  Future<Json> finish(int sessionId) async {
    finishCalls++;
    events.add('finish');
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
  @override
  Future<Json> submit(
    int sessionId,
    Question question,
    String path, {
    void Function(int, int)? onSendProgress,
  }) async {
    events.add('upload');
    return {
      'success': true,
      'isCompleted': false,
      'shouldFollowUp': true,
      'nextQuestion': {...question.toJson(), 'isFollowUp': true},
    };
  }
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
    _MemoryDraft? draft,
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
          draft: draft ?? _MemoryDraft(),
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

  testWidgets('recording recovery remains available through exam help', (
    tester,
  ) async {
    await mount(tester, _ExamApi(), withQuestion: true);
    expect(find.text('Local recordings'), findsNothing);
    await tester.tap(find.byTooltip('Exam help'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Local recordings'));
    await tester.pumpAndSettle();
    expect(find.text('unused-test-recordings'), findsOneWidget);
    expect(find.text('Open recordings folder'), findsNothing);
    expect(
      find.text(
        'Recordings stay on this computer. Quit AIVES before opening this folder in Windows.',
      ),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
    await tester.tap(find.text('Close'));
    await tester.pumpAndSettle();
    await tester.pumpWidget(const SizedBox());
    await tester.pump();
  });

  testWidgets(
    'completion requires the final confirmation and server acknowledgement',
    (tester) async {
      final api = _ExamApi();
      await mount(tester, api, draft: _MemoryDraft(events: api.events));
      await tester.tap(find.text('Finish and submit'));
      await tester.pumpAndSettle();
      expect(api.finishCalls, 0);
      await tester.tap(find.widgetWithText(FilledButton, 'Submit exam'));
      await tester.pumpAndSettle();
      expect(api.finishCalls, 1);
      expect(api.events, ['backup', 'finish']);
      expect(find.text('Exam submitted'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      await tester.pump();
    },
  );

  testWidgets(
    'failed local backup blocks grading until retry saves successfully',
    (tester) async {
      final api = _ExamApi();
      final draft = _MemoryDraft(failBackup: true, events: api.events);
      await mount(tester, api, draft: draft);
      await tester.tap(find.text('Finish and submit'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Submit exam'));
      await tester.pumpAndSettle();
      expect(api.finishCalls, 0);
      expect(
        find.textContaining('Local backup failed: disk full'),
        findsOneWidget,
      );
      expect(find.text('Exam submitted'), findsNothing);
      draft.backup.fail = false;
      await tester.tap(find.text('Finish and submit'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Submit exam'));
      await tester.pumpAndSettle();
      expect(api.events, ['backup', 'backup', 'finish']);
      expect(find.text('Exam submitted'), findsOneWidget);
      expect(tester.takeException(), isNull);
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

  for (final failBackup in [false, true]) {
    testWidgets(
      'answer upload requires a saved backup (disk failure: $failBackup)',
      (tester) async {
        final api = _ExamApi();
        final draft = _MemoryDraft(
          events: api.events,
          failBackup: failBackup,
          restored: {
            'question': {
              'questionId': 10,
              'questionIndex': 0,
              'totalQuestions': 1,
              'questionText': 'Explain layered architecture.',
              'isFollowUp': false,
            },
            'audio': 'restored-answer.wav',
            'uncertain': false,
          },
        );
        await mount(tester, api, withQuestion: true, draft: draft);
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('Submit answer'));
        await tester.tap(find.text('Submit answer'));
        await tester.pumpAndSettle();
        expect(api.events, failBackup ? ['backup'] : ['backup', 'upload']);
        if (failBackup) {
          expect(find.textContaining('Local backup failed'), findsOneWidget);
          expect(find.text('Check server status'), findsNothing);
          expect(
            tester
                .widget<FilledButton>(
                  find.widgetWithText(FilledButton, 'Submit answer'),
                )
                .onPressed,
            isNotNull,
          );
        }
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
        await tester.pump();
      },
    );
  }
}
