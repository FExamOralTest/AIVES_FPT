import 'dart:convert';
import 'dart:io';

import 'models.dart';

/// Durable, cumulative exam backup. WAV files are retained beside exam.json;
/// clearing the current answer draft never deletes this archive.
class ExamArchive {
  final Directory folder;
  ExamArchive(this.folder);

  File get manifest => File('${folder.path}/exam.json');
  File get _pending => File('${folder.path}/exam.tmp');

  Future<Json?> read() async {
    var found = false;
    for (final file in [_pending, manifest]) {
      if (!await file.exists()) continue;
      found = true;
      try {
        return jsonDecode(await file.readAsString()) as Json;
      } on FormatException {
        continue;
      } on TypeError {
        continue;
      }
    }
    if (found) {
      throw const FormatException(
        'The local exam backup cannot be read. Contact the proctor before submitting.',
      );
    }
    return null;
  }

  Future<void> prepare({
    required String server,
    required Student student,
    required VivaSession session,
    Question? question,
    String? audio,
    bool finishing = false,
  }) async {
    await folder.create(recursive: true);
    final previous = await read();
    if (previous != null &&
        (previous['server'] != server ||
            previous['studentId'] != student.id ||
            previous['sessionId'] != session.id ||
            previous['version'] != 1)) {
      throw const FormatException(
        'The local exam backup belongs to a different exam.',
      );
    }
    final answers = List<Json>.from(previous?['answers'] as List? ?? []);
    final questions = Map<String, dynamic>.from(
      previous?['questions'] as Map? ?? {},
    );
    final now = DateTime.now().toUtc().toIso8601String();
    if (question != null) questions[question.key] = question.toJson();
    if (audio != null) {
      if (question == null) throw StateError('The recording has no question.');
      final file = File(audio);
      final bytes = await _flushRecording(file);
      final name = file.uri.pathSegments.last;
      // Retain different attempts and follow-ups, without duplicating retries
      // of the same stopped recording.
      if (!answers.any((entry) => entry['audioFile'] == name)) {
        answers.add({
          'question': question.toJson(),
          'audioFile': name,
          'bytes': bytes,
          'savedAtUtc': now,
        });
      }
    }
    // Before grading, ensure the complete local backup still has its audio.
    // A missing/deleted recording must not be silently treated as backed up.
    for (final entry in answers) {
      final name = entry['audioFile'] as String;
      final bytes = await _flushRecording(File('${folder.path}/$name'));
      if (bytes != entry['bytes']) {
        throw FileSystemException('A backed-up recording has changed.', name);
      }
    }
    final archive = <String, dynamic>{
      'version': 1,
      'server': server,
      'studentId': student.id,
      'studentName': student.name,
      'sessionId': session.id,
      'sessionName': session.name,
      'examTitle': session.title,
      'totalQuestions': session.total,
      'deadlineUtc': session.deadline.toIso8601String(),
      'createdAtUtc': previous?['createdAtUtc'] ?? now,
      'savedAtUtc': now,
      'questions': questions,
      'answers': answers,
      // This is an upload intention, not a claim that the server accepted it.
      'submissionState': finishing ? 'savedBeforeGrading' : 'savedBeforeUpload',
      if (finishing) 'finishRequestedAtUtc': now,
    };
    await _pending.writeAsString(
      const JsonEncoder.withIndent('  ').convert(archive),
      flush: true,
    );
    await _pending.rename(manifest.path);
  }

  Future<int> _flushRecording(File file) async {
    final directoryPath = await folder.resolveSymbolicLinks();
    final filePath = await file.resolveSymbolicLinks();
    final parent = File(filePath).parent.path;
    final sameFolder = Platform.isWindows
        ? parent.toLowerCase() == directoryPath.toLowerCase()
        : parent == directoryPath;
    if (!sameFolder) {
      throw FileSystemException(
        'The recording must be inside the local exam folder.',
        file.path,
      );
    }
    final handle = await File(filePath).open(mode: FileMode.append);
    try {
      final bytes = await handle.length();
      if (bytes <= 44 || bytes > 48 * 1024 * 1024) {
        throw FileSystemException(
          'The recording is empty or exceeds 48 MB.',
          file.path,
        );
      }
      await handle.flush();
      return bytes;
    } finally {
      await handle.close();
    }
  }
}
