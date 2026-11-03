import 'package:flutter_test/flutter_test.dart';
import 'package:aives_desktop/api.dart';
import 'package:aives_desktop/models.dart';

void main() {
  test('timezone-less server timestamps are interpreted as UTC', () {
    expect(apiDate('2026-10-26T09:30:00'), DateTime.utc(2026, 10, 26, 9, 30));
    expect(
      apiDate('2026-10-26T16:30:00+07:00'),
      DateTime.utc(2026, 10, 26, 9, 30),
    );
  });
  test('resume preserves the original duration and caps it to session end', () {
    final end = DateTime.utc(2026, 10, 26, 10);
    final json = {
      'sessionId': 1,
      'totalQuestions': 1,
      'sessionName': 'Test',
      'examTitle': 'SWD392',
      'durationMinutes': 30,
      'startedAt': '2026-10-26T09:00:00',
      'currentQuestion': null,
    };
    expect(
      VivaSession.fromJson(json, end).deadline,
      DateTime.utc(2026, 10, 26, 9, 30),
    );
    expect(
      VivaSession.fromJson({...json, 'durationMinutes': 90}, end).deadline,
      end,
    );
  });
  test('invalid or insecure remote endpoints cannot receive credentials', () {
    for (final address in [
      'https://api.example.com/api',
      'http://api.example.com',
      'https://user:secret@example.com',
      'not-a-url',
      'https://example.com?token=x',
    ]) {
      expect(
        () => AivesApi.validateServer(address),
        throwsA(isA<ApiFailure>()),
      );
    }
    expect(
      AivesApi.validateServer(' https://api.example.com/ '),
      'https://api.example.com',
    );
    expect(
      AivesApi.validateServer('http://localhost:5116'),
      'http://localhost:5116',
    );
  });
}
