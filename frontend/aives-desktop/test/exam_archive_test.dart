import 'dart:convert';
import 'dart:io';

import 'package:aives_desktop/exam_archive.dart';
import 'package:aives_desktop/local_store.dart';
import 'package:aives_desktop/models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late Directory folder;
  late ExamArchive archive;
  final student = Student.fromJson({
    'userId': 7,
    'fullName': 'Student A',
    'role': 'STUDENT',
  });
  final question = Question.fromJson({
    'questionId': 10,
    'questionIndex': 0,
    'totalQuestions': 1,
    'questionText': 'Explain layered architecture.',
    'isFollowUp': false,
  });
  final session = VivaSession.fromJson({
    'sessionId': 5,
    'totalQuestions': 1,
    'sessionName': 'Exam session',
    'examTitle': 'SWD392',
    'startedAt': '2026-10-29T01:00:00Z',
    'durationMinutes': 30,
  }, DateTime.utc(2026, 10, 29, 2));
  setUp(() async {
    folder = await Directory.systemTemp.createTemp('aives_archive_');
    archive = ExamArchive(folder);
  });
  tearDown(() async => folder.delete(recursive: true));

  Future<File> wav(String name) =>
      File('${folder.path}/$name')
          .writeAsBytes(List<int>.filled(128, 1), flush: true);
  Future<void> prepare({
    Question? answer,
    String? audio,
    bool finishing = false,
  }) => archive.prepare(
    server: 'https://api.example.com',
    student: student,
    session: session,
    question: answer,
    audio: audio,
    finishing: finishing,
  );

  test(
    'Windows exam location requires AppData on C regardless of install drive',
    () {
      validateWindowsExamRoot(r'C:\Users\Student\AppData\Roaming\AIVES');
      validateWindowsExamRoot('c:/Users/Student/AppData/Local/AIVES');
      for (final path in [
        r'D:\Users\Student\AppData\AIVES',
        r'C:\AIVES',
        'relative/AppData',
      ]) {
        expect(
          () => validateWindowsExamRoot(path),
          throwsA(isA<FileSystemException>()),
        );
      }
    },
  );

  test('cumulative archive survives restart, rerecords, follow-ups and draft clearing', () async {
    final first = await wav('first.wav');
    final second = await wav('second.wav');
    final followupAudio = await wav('followup.wav');
    final followup = Question.fromJson({
      ...question.toJson(),
      'isFollowUp': true,
      'questionText': 'Give an example.',
    });
    await prepare(answer: question, audio: first.path);
    archive = ExamArchive(folder);
    await prepare(answer: question, audio: first.path);
    await prepare(answer: question, audio: second.path);
    await prepare(answer: followup, audio: followupAudio.path);
    await AnswerDraft(folder).save(null, null);
    await prepare(finishing: true);
    final saved = await ExamArchive(folder).read();
    expect(saved!['studentId'], 7);
    expect(saved['sessionId'], 5);
    expect(saved['examTitle'], 'SWD392');
    expect(saved['submissionState'], 'savedBeforeGrading');
    expect(saved['finishRequestedAtUtc'], isNotNull);
    expect((saved['questions'] as Map).length, 2);
    final answers = List<Json>.from(saved['answers'] as List);
    expect(answers.map((e) => e['audioFile']), [
      'first.wav',
      'second.wav',
      'followup.wav',
    ]);
    expect(answers.last['question']['isFollowUp'], isTrue);
    for (final file in [first, second, followupAudio]) {
      expect(await file.length(), 128);
    }
  });

  test(
    'missing or modified audio blocks finishing without replacing the backup',
    () async {
      final file = await wav('answer.wav');
      await prepare(answer: question, audio: file.path);
      final before = await archive.manifest.readAsString();
      await file.writeAsBytes([1, 2, 3]);
      await expectLater(
        prepare(finishing: true),
        throwsA(isA<FileSystemException>()),
      );
      expect(await archive.manifest.readAsString(), before);
      await file.delete();
      await expectLater(
        prepare(finishing: true),
        throwsA(isA<FileSystemException>()),
      );
      expect(await archive.manifest.readAsString(), before);
    },
  );

  test(
    'a flushed journal is recovered after interruption before rename',
    () async {
      await prepare(answer: question, audio: (await wav('answer.wav')).path);
      await archive.manifest.rename('${folder.path}/exam.tmp');
      await prepare(finishing: true);
      expect((await archive.read())!['answers'], hasLength(1));
      expect(await File('${folder.path}/exam.tmp').exists(), isFalse);
    },
  );

  test(
    'corrupt backup and different student identity cannot be overwritten',
    () async {
      await prepare();
      final saved = (await archive.read())!..['studentId'] = 999;
      await archive.manifest.writeAsString(jsonEncode(saved));
      await expectLater(prepare(), throwsA(isA<FormatException>()));
      await archive.manifest.writeAsString('corrupt');
      await expectLater(prepare(), throwsA(isA<FormatException>()));
    },
  );

  test('a failed disk write leaves the previous exam and WAV intact', () async {
    final file = await wav('answer.wav');
    await prepare(answer: question, audio: file.path);
    final before = await archive.manifest.readAsString();
    await Directory('${folder.path}/exam.tmp').create();
    await expectLater(
      prepare(finishing: true),
      throwsA(isA<FileSystemException>()),
    );
    expect(await archive.manifest.readAsString(), before);
    expect(await file.length(), 128);
  });

  test('recordings outside the exam folder cannot be archived', () async {
    final outside = await Directory.systemTemp.createTemp('aives_outside_');
    try {
      final file = await File('${outside.path}/outside.wav')
          .writeAsBytes(List.filled(128, 1));
      await expectLater(
        prepare(answer: question, audio: file.path),
        throwsA(isA<FileSystemException>()),
      );
      expect(await archive.manifest.exists(), isFalse);
    } finally {
      await outside.delete(recursive: true);
    }
  });
}
