import 'package:flutter/material.dart';

import '../api.dart';
import '../models.dart';
import '../widgets.dart';

class ResultScreen extends StatefulWidget {
  final AivesApi api;
  final int sessionId;
  const ResultScreen({super.key, required this.api, required this.sessionId});
  @override
  State<ResultScreen> createState() => _ResultScreenState();
}

class _ResultScreenState extends State<ResultScreen> {
  late Future<Json> _result = widget.api.result(widget.sessionId);
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Exam results'),
      actions: [
        IconButton(
          tooltip: 'Refresh',
          onPressed: () =>
              setState(() => _result = widget.api.result(widget.sessionId)),
          icon: const Icon(Icons.refresh),
        ),
      ],
    ),
    body: FutureBuilder<Json>(
      future: _result,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        return Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 860),
            child: ListView(
              padding: const EdgeInsets.all(28),
              children: [
                if (snapshot.hasError)
                  Notice(snapshot.error.toString(), error: true)
                else ...[
                  Text(
                    snapshot.data!['examTitle'] as String? ?? 'Results',
                    style: const TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 20),
                  if (snapshot.data!['isPublished'] != true)
                    Notice(
                      snapshot.data!['message'] as String? ?? 'Your exam has been submitted and is awaiting review. Scores will appear after publication.',
                    )
                  else ...[
                    Surface(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'Official score',
                            style: TextStyle(color: muted),
                          ),
                          Text(
                            '${snapshot.data!['finalScore'] ?? '—'}',
                            style: const TextStyle(
                              fontSize: 52,
                              fontWeight: FontWeight.w700,
                              color: brand,
                            ),
                          ),
                          Text(
                            snapshot.data!['finalFeedback'] as String? ?? '',
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 20),
                    for (final answer
                        in (snapshot.data!['answers'] as List? ?? [])) ...[
                      Surface(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              answer['questionText'] as String? ?? '',
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 10),
                            SelectableText(
                              answer['transcript'] as String? ??
                                  answer['studentAnswer'] as String? ??
                                  '',
                            ),
                            const SizedBox(height: 10),
                            Text('Score: ${answer['finalScore'] ?? '—'}'),
                            if (answer['teacherComment'] != null)
                              Text(answer['teacherComment'] as String),
                          ],
                        ),
                      ),
                      const SizedBox(height: 12),
                    ],
                  ],
                ],
              ],
            ),
          ),
        );
      },
    ),
  );
}
