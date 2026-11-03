typedef Json = Map<String, dynamic>;

DateTime apiDate(String value) {
  // ASP.NET can serialize UTC timestamps without a trailing timezone.
  final hasZone = RegExp(
    r'(Z|[+-]\d\d:\d\d)$',
    caseSensitive: false,
  ).hasMatch(value);
  return DateTime.parse(hasZone ? value : '${value}Z').toUtc();
}

String sessionStatus(dynamic value) => switch (value) {
  0 => 'NOT_STARTED',
  1 => 'IN_PROGRESS',
  2 => 'COMPLETED',
  3 => 'CANCELLED',
  _ => value?.toString() ?? 'NOT_STARTED',
};

class Student {
  final int id;
  final String name;
  final String email;
  final String role;
  Student.fromJson(Json json)
    : id = json['userId'] as int,
      name = json['fullName'] as String? ?? json['username'] as String? ?? '',
      email = json['email'] as String? ?? '',
      role = json['role'] as String;
}

class ExamSession {
  final int id;
  final String name, title, course, status, enrollmentStatus;
  final DateTime start, end;
  final bool published;
  ExamSession.fromJson(Json json)
    : id = json['sessionId'] as int,
      name = json['sessionName'] as String? ?? '',
      title = json['examTitle'] as String? ?? '',
      course = json['courseName'] as String? ?? '',
      status = sessionStatus(json['sessionStatus']),
      enrollmentStatus = sessionStatus(json['enrollmentStatus']),
      start = apiDate(json['startTime'] as String),
      end = apiDate(json['endTime'] as String),
      published = json['isPublished'] == true;

  bool get completed => published || enrollmentStatus == 'COMPLETED';
  bool get canEnter =>
      !completed &&
      enrollmentStatus != 'CANCELLED' &&
      status == 'IN_PROGRESS' &&
      !DateTime.now().toUtc().isBefore(start) &&
      DateTime.now().toUtc().isBefore(end);
  String get label {
    if (published) return 'Results published';
    if (completed) return 'Submitted';
    if (status == 'CANCELLED' || enrollmentStatus == 'CANCELLED') {
      return 'Cancelled';
    }
    if (status == 'COMPLETED' || !DateTime.now().toUtc().isBefore(end)) {
      return 'Closed';
    }
    if (canEnter) {
      return enrollmentStatus == 'IN_PROGRESS' ? 'In progress' : 'Ready';
    }
    return 'Waiting for session to open';
  }
}

class Question {
  final int id, index, total;
  final String text;
  final bool followUp;
  Question.fromJson(Json json)
    : id = json['questionId'] as int,
      index = json['questionIndex'] as int,
      total = json['totalQuestions'] as int,
      text = json['questionText'] as String,
      followUp = json['isFollowUp'] == true;
  Json toJson() => {
    'questionId': id,
    'questionIndex': index,
    'totalQuestions': total,
    'questionText': text,
    'isFollowUp': followUp,
  };
  String get key => '${id}_${index}_${followUp ? 'followup' : 'main'}';
}

class VivaSession {
  final int id, total;
  final String name, title;
  final DateTime deadline;
  final Question? question;
  VivaSession.fromJson(Json json, DateTime sessionEnd)
    : id = json['sessionId'] as int,
      total = json['totalQuestions'] as int,
      name = json['sessionName'] as String,
      title = json['examTitle'] as String,
      question = json['currentQuestion'] == null
          ? null
          : Question.fromJson(json['currentQuestion'] as Json),
      deadline = _deadline(json, sessionEnd);
  static DateTime _deadline(Json json, DateTime end) {
    final start = json['startedAt'];
    if (start == null) {
      throw const FormatException(
        'The backend did not return the exam start time.',
      );
    }
    final durationEnd = apiDate(start as String)
        .add(Duration(minutes: json['durationMinutes'] as int));
    return durationEnd.isBefore(end) ? durationEnd : end;
  }
}
