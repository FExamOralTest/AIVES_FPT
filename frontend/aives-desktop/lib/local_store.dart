import 'dart:convert';
import 'dart:io';

import 'package:path_provider/path_provider.dart';

import 'models.dart';

class LocalStore {
  final Directory root;
  LocalStore(this.root);
  static Future<LocalStore> open() async {
    final base = await getApplicationSupportDirectory();
    final root = Directory('${base.path}${Platform.pathSeparator}AIVES');
    await root.create(recursive: true);
    return LocalStore(root);
  }

  Future<String?> readServer() async {
    final file = File('${root.path}/server.json');
    if (!await file.exists()) return null;
    try {
      return (jsonDecode(await file.readAsString()) as Json)['server']
          as String?;
    } on FormatException {
      return null;
    }
  }

  Future<void> saveServer(String server) =>
      File('${root.path}/server.json')
          .writeAsString(jsonEncode({'server': server}));

  Future<Directory> sessionFolder(
    String server,
    int userId,
    int sessionId,
  ) async {
    final host = base64UrlEncode(utf8.encode(server)).replaceAll('=', '');
    return Directory(
      '${root.path}/recordings/$host/student_$userId/session_$sessionId',
    )..createSync(recursive: true);
  }
}

/// No access tokens/passwords are persisted. Each stopped answer is accompanied
/// by its question so follow-up questions can be restored after a restart.
class AnswerDraft {
  final Directory folder;
  AnswerDraft(this.folder);
  File get manifest => File('${folder.path}/draft.json');
  Future<void> save(
    Question? question,
    String? audio, {
    bool uncertain = false,
  }) async {
    final temp = File('${folder.path}/draft.tmp');
    await temp.writeAsString(
      jsonEncode({
        'question': question?.toJson(),
        'audio': audio,
        'uncertain': uncertain,
      }),
      flush: true,
    );
    await temp.rename(manifest.path);
  }

  Future<Json?> read() async {
    // A flushed temporary journal may survive a process shutdown before rename.
    for (final file in [File('${folder.path}/draft.tmp'), manifest]) {
      if (!await file.exists()) continue;
      try {
        return jsonDecode(await file.readAsString()) as Json;
      } on FormatException {
        continue;
      }
    }
    return null;
  }
}
