import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:aives_desktop/api.dart';
import 'package:aives_desktop/local_store.dart';
import 'package:aives_desktop/models.dart';

void main() {
  late HttpServer server;
  late AivesApi api;
  late Directory folder;
  late File wav;
  final requests = <String>[];
  final uploads = <String>[];
  var failUpload = false;
  var rejectLogin = false;
  var rejectSessions = false;
  var loginRole = 'STUDENT';
  var index = 0;
  var completed = false;
  final question = <String, dynamic>{
    'questionId': 10,
    'questionIndex': 0,
    'totalQuestions': 1,
    'questionText': 'Explain software architecture.',
    'isFollowUp': false,
  };

  setUp(() async {
    requests.clear();
    uploads.clear();
    failUpload = false;
    rejectLogin = false;
    rejectSessions = false;
    loginRole = 'STUDENT';
    index = 0;
    completed = false;
    folder = await Directory.systemTemp.createTemp('aives_desktop_test_');
    wav = File('${folder.path}/answer.wav');
    await wav.writeAsBytes(List<int>.filled(128, 0));
    server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    api = AivesApi('http://127.0.0.1:${server.port}');
    server.listen((request) async {
      requests.add('${request.method} ${request.uri.path}');
      final body = await utf8.decoder.bind(request).join();
      request.response.headers.contentType = ContentType.json;
      dynamic response;
      switch (request.uri.path) {
        case '/api/auth/login':
          if (rejectLogin) {
            request.response.statusCode = 401;
            response = {'message': 'Invalid username or password.'};
            break;
          }
          expect(jsonDecode(body), {
            'email': 'student@example.com',
            'password': 'password',
          });
          response = {
            'accessToken': 'test-token',
            'user': {
              'userId': 7,
              'fullName': 'Student A',
              'email': 'student@example.com',
              'role': loginRole,
            },
          };
        case '/api/student/enrollments':
          if (rejectSessions) {
            request.response.statusCode = 401;
            response = {'message': 'Unauthorized'};
            break;
          }
          expect(request.headers.value('authorization'), 'Bearer test-token');
          response = [
            {
              'sessionId': 5,
              'sessionName': 'Exam session 1',
              'examTitle': 'SWD392',
              'courseName': 'Software Architecture',
              'sessionStatus': 1,
              'enrollmentStatus': completed ? 2 : 1,
              'startTime': DateTime.now()
                  .toUtc()
                  .subtract(const Duration(minutes: 5))
                  .toIso8601String(),
              'endTime': DateTime.now()
                  .toUtc()
                  .add(const Duration(hours: 1))
                  .toIso8601String(),
              'isPublished': false,
            },
          ];
        case '/api/enroll/session':
          expect(jsonDecode(body), {'sessionCode': 'JOIN123'});
          response = {'sessionId': 5};
        case '/api/student/viva-sessions/5/start':
          response = {
            'sessionId': 5,
            'sessionName': 'Exam session 1',
            'examTitle': 'SWD392',
            'durationMinutes': 30,
            'totalQuestions': 1,
            'currentQuestionIndex': index,
            'startedAt': DateTime.now()
                .toUtc()
                .subtract(const Duration(minutes: 1))
                .toIso8601String(),
            'currentQuestion': index == 0 ? question : null,
          };
        case '/api/student/viva-sessions/5/submit-answer':
          expect(request.headers.value('authorization'), 'Bearer test-token');
          expect(request.headers.contentType?.mimeType, 'multipart/form-data');
          uploads.add(body);
          if (failUpload) {
            request.response.statusCode = 503;
            response = {'message': 'AI temporarily unavailable'};
          } else if (body.contains('\r\ntrue\r\n')) {
            index = 1;
            response = {
              'success': true,
              'isCompleted': true,
              'shouldFollowUp': false,
              'nextQuestion': null,
              'transcript': 'Follow-up answer',
            };
          } else {
            response = {
              'success': true,
              'isCompleted': false,
              'shouldFollowUp': true,
              'nextQuestion': {
                ...question,
                'isFollowUp': true,
                'questionText': 'Give an example.',
              },
              'transcript': 'First answer',
            };
          }
        case '/api/student/viva-sessions/5/finish':
          completed = true;
          response = {'status': 'PENDING_REVIEW', 'message': 'Exam submitted.'};
        case '/api/student/results/5':
          response = {
            'isPublished': false,
            'message': 'Awaiting review',
            'answers': [],
          };
        default:
          request.response.statusCode = 400;
          response = {
            'errors': {
              'sessionCode': ['Invalid session code.'],
            },
          };
      }
      request.response.write(jsonEncode(response));
      await request.response.close();
    });
  });
  tearDown(() async {
    api.close();
    await server.close(force: true);
    await folder.delete(recursive: true);
  });

  test('real HTTP contract: login, enroll, WAV answer, follow-up, finish and result', () async {
    final user = await api.login(' student@example.com ', 'password');
    expect(user.id, 7);
    await api.enroll(' JOIN123 ');
    final sessions = await api.sessions();
    expect(sessions.single.canEnter, isTrue);
    final session = await api.start(sessions.single);
    final first = await api.submit(session.id, session.question!, wav.path);
    expect(first['shouldFollowUp'], isTrue);
    final followUp = Question.fromJson(first['nextQuestion'] as Json);
    expect(followUp.followUp, isTrue);
    final second = await api.submit(session.id, followUp, wav.path);
    expect(second['isCompleted'], isTrue);
    expect(uploads.first, contains('name="questionId"\r\n\r\n10'));
    expect(uploads.first.toLowerCase(), contains('content-type: audio/wav'));
    expect(uploads.last, contains('name="isFollowUp"\r\n\r\ntrue'));
    expect((await api.start(sessions.single)).question, isNull);
    expect((await api.finish(session.id))['status'], 'PENDING_REVIEW');
    expect((await api.sessions()).single.completed, isTrue);
    expect((await api.result(session.id))['isPublished'], isFalse);
  });

  test('an uncertain upload is not retried and preserves the WAV', () async {
    await api.login('student@example.com', 'password');
    failUpload = true;
    await expectLater(
      api.submit(5, Question.fromJson(question), wav.path),
      throwsA(
        isA<ApiFailure>().having((e) => e.uncertain, 'uncertain', isTrue),
      ),
    );
    expect(uploads.length, 1);
    expect(await wav.exists(), isTrue);
  });

  test(
    'non-student login is rejected without issuing a session token',
    () async {
      loginRole = 'ADMIN';
      await expectLater(
        api.login('student@example.com', 'password'),
        throwsA(isA<ApiFailure>()),
      );
      expect(api.token, isNull);
    },
  );

  test(
    'login 401 reports invalid credentials rather than expired session',
    () async {
      rejectLogin = true;
      await expectLater(
        api.login('student@example.com', 'password'),
        throwsA(
          isA<ApiFailure>()
              .having((e) => e.status, 'status', 401)
              .having(
                (e) => e.message,
                'message',
                contains('Incorrect email or password'),
              ),
        ),
      );
      expect(api.token, isNull);
    },
  );

  test('protected endpoint 401 still asks to authenticate again', () async {
    await api.login('student@example.com', 'password');
    rejectSessions = true;
    await expectLater(
      api.sessions(),
      throwsA(
        isA<ApiFailure>()
            .having((e) => e.status, 'status', 401)
            .having(
              (e) => e.message,
              'message',
              contains('Your session has expired'),
            ),
      ),
    );
  });

  test('empty audio is rejected before any upload', () async {
    await wav.writeAsBytes([]);
    await expectLater(
      api.submit(5, Question.fromJson(question), wav.path),
      throwsA(isA<ApiFailure>()),
    );
    expect(uploads, isEmpty);
  });

  test(
    'draft retains follow-up identity and pre-send uncertainty across restart',
    () async {
      final draft = AnswerDraft(folder);
      final followUp = Question.fromJson({
        ...question,
        'isFollowUp': true,
        'questionText': 'Why?',
      });
      await draft.save(followUp, wav.path, uncertain: true);
      final saved = await AnswerDraft(folder).read();
      expect(Question.fromJson(saved!['question'] as Json).followUp, isTrue);
      expect(saved['audio'], wav.path);
      expect(saved['uncertain'], isTrue);
      expect(await File(wav.path).exists(), isTrue);
    },
  );
}
