import 'dart:io';

import 'package:dio/dio.dart';

import 'models.dart';

class ApiFailure implements Exception {
  final String message;
  final bool uncertain;
  final int? status;
  const ApiFailure(this.message, {this.uncertain = false, this.status});
  @override
  String toString() => message;
}

class AivesApi {
  final Dio http;
  String? token;
  AivesApi(String server, {Dio? client}) : http = client ?? Dio() {
    http.options = BaseOptions(
      baseUrl: validateServer(server),
      connectTimeout: const Duration(seconds: 15),
      sendTimeout: const Duration(minutes: 3),
      receiveTimeout: const Duration(minutes: 5),
      followRedirects: false,
    );
    http.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          if (token != null) options.headers['Authorization'] = 'Bearer $token';
          handler.next(options);
        },
      ),
    );
  }

  static String validateServer(String raw) {
    final uri = Uri.tryParse(raw.trim());
    if (uri == null ||
        !['http', 'https'].contains(uri.scheme) ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        (uri.path.isNotEmpty && uri.path != '/') ||
        uri.hasQuery ||
        uri.hasFragment) {
      throw const ApiFailure(
        'Enter the backend URL, for example http://localhost:5116 (without /api).',
      );
    }
    if (uri.scheme != 'https' &&
        !['localhost', '127.0.0.1', '::1'].contains(uri.host)) {
      throw const ApiFailure(
        'Use HTTPS for remote servers. HTTP is allowed only for localhost.',
      );
    }
    return raw.trim().replaceFirst(RegExp(r'/+$'), '');
  }

  Future<dynamic> _request(
    String method,
    String path, {
    dynamic data,
    void Function(int, int)? onSendProgress,
  }) async {
    try {
      final response = await http.request<dynamic>(
        path,
        data: data,
        options: Options(method: method),
        onSendProgress: onSendProgress,
      );
      return response.data;
    } on DioException catch (e) {
      final body = e.response?.data;
      final errors = body is Map ? body['errors'] : null;
      final details = errors is Map
          ? errors.values.expand((v) => v is List ? v : [v]).join('\n')
          : null;
      final status = e.response?.statusCode;
      final message = status == 401
          ? path == '/api/auth/login'
                ? 'Incorrect email or password. Use an account registered on this server.'
                : 'Your session has expired. Sign in again to continue.'
          : body is Map
          ? (details ?? body['message'] ?? body['detail'] ?? body['title'])
                ?.toString()
          : null;
      throw ApiFailure(
        message ?? 'Cannot connect to the backend. Check the server URL, network and HTTPS certificate.',
        status: status,
        uncertain: status == null || status >= 500,
      );
    }
  }

  Future<Student> login(String email, String password) async {
    final json = await _request(
      'POST',
      '/api/auth/login',
      data: {'email': email.trim(), 'password': password},
    ) as Json;
    final student = Student.fromJson(json['user'] as Json);
    if (student.role != 'STUDENT') {
      throw const ApiFailure(
        'This app is for students. Other roles should use the AIVES website.',
      );
    }
    token = json['accessToken'] as String;
    return student;
  }

  Future<List<ExamSession>> sessions() async =>
      (await _request('GET', '/api/student/enrollments') as List)
          .map((e) => ExamSession.fromJson(e as Json))
          .toList();
  Future<void> enroll(String code) async {
    await _request(
      'POST',
      '/api/enroll/session',
      data: {'sessionCode': code.trim()},
    );
  }

  Future<VivaSession> start(ExamSession session) async => VivaSession.fromJson(
    await _request('POST', '/api/student/viva-sessions/${session.id}/start')
        as Json,
    session.end,
  );
  Future<Json> submit(
    int sessionId,
    Question question,
    String path, {
    void Function(int, int)? onSendProgress,
  }) async {
    final file = File(path);
    if (!await file.exists() || await file.length() <= 44) {
      throw const ApiFailure(
        'The recording is empty or missing. Record your answer again.',
      );
    }
    if (await file.length() > 48 * 1024 * 1024) {
      throw const ApiFailure(
        'The recording exceeds 48 MB. Contact the proctor; your recording is saved locally.',
      );
    }
    final form = FormData.fromMap({
      'questionId': question.id,
      'isFollowUp': question.followUp.toString(),
      'audioFile': await MultipartFile.fromFile(
        path,
        filename: 'answer_${question.key}.wav',
        contentType: DioMediaType('audio', 'wav'),
      ),
    });
    final result = await _request(
      'POST',
      '/api/student/viva-sessions/$sessionId/submit-answer',
      data: form,
      onSendProgress: onSendProgress,
    ) as Json;
    if (result['success'] != true) {
      throw ApiFailure(
        result['message'] as String? ??
            'The backend has not confirmed the answer.',
        uncertain: true,
      );
    }
    return result;
  }

  Future<Json> finish(int sessionId) async =>
      await _request('POST', '/api/student/viva-sessions/$sessionId/finish')
          as Json;
  Future<Json> result(int sessionId) async =>
      await _request('GET', '/api/student/results/$sessionId') as Json;
  void close() => http.close();
}
