import 'package:flutter/material.dart';

import 'audio_capture.dart';

const brand = Color(0xff0f6ce6);
const ink = Color(0xff14213b);
const muted = Color(0xff64748b);

class Surface extends StatelessWidget {
  final Widget child;
  final EdgeInsets padding;
  const Surface({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(24),
  });
  @override
  Widget build(BuildContext context) => Container(
    padding: padding,
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: const Color(0xffe2e8f0)),
    ),
    child: child,
  );
}

class Notice extends StatelessWidget {
  final String text;
  final bool error;
  const Notice(this.text, {super.key, this.error = false});
  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: error ? const Color(0xfffef2f2) : const Color(0xffeaf3ff),
      borderRadius: BorderRadius.circular(12),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          error ? Icons.error_outline : Icons.info_outline,
          color: error ? Colors.red.shade800 : brand,
          size: 22,
        ),
        const SizedBox(width: 12),
        Expanded(child: SelectableText(text)),
      ],
    ),
  );
}

class BrandMark extends StatelessWidget {
  final bool light;
  const BrandMark({super.key, this.light = false});
  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Container(
        padding: const EdgeInsets.all(6),
        decoration: BoxDecoration(
          color: light
              ? Colors.white.withValues(alpha: .15)
              : const Color(0xffe5f1ff),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Icon(
          Icons.graphic_eq_rounded,
          color: light ? Colors.white : brand,
          size: 22,
        ),
      ),
      const SizedBox(width: 10),
      Text(
        'AIVES',
        style: TextStyle(
          fontSize: 20,
          fontWeight: FontWeight.w700,
          letterSpacing: .6,
          color: light ? Colors.white : ink,
        ),
      ),
    ],
  );
}

Future<bool> confirm(
  BuildContext context,
  String title,
  String message, {
  String action = 'Confirm',
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Back'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(action),
          ),
        ],
      ),
    ) ??
    false;
void showFailure(BuildContext context, Object error) {
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(error.toString()),
      duration: const Duration(seconds: 7),
      showCloseIcon: true,
    ),
  );
}

class RecordingPanel extends StatelessWidget {
  final AudioCapture capture;
  final VoidCallback? onStart, onStop, onPlay;
  final bool enabled;
  const RecordingPanel({
    super.key,
    required this.capture,
    this.onStart,
    this.onStop,
    this.onPlay,
    this.enabled = true,
  });
  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: capture,
    builder: (context, _) => Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(
              capture.recording ? Icons.mic : Icons.mic_none,
              color: capture.recording ? Colors.red : brand,
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                capture.recording
                    ? 'Recording'
                    : capture.path != null
                    ? 'Recording saved locally'
                    : 'Ready to record',
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ),
            Text(
              clockText(capture.seconds),
              style: const TextStyle(fontSize: 23, fontWeight: FontWeight.w700),
            ),
          ],
        ),
        const SizedBox(height: 16),
        LinearProgressIndicator(
          value: capture.recording ? capture.level : 0,
          minHeight: 8,
          borderRadius: BorderRadius.circular(8),
          color: capture.recording ? Colors.redAccent : brand,
          backgroundColor: const Color(0xffe2e8f0),
        ),
        const SizedBox(height: 20),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            if (capture.recording)
              FilledButton.icon(
                onPressed: enabled && !capture.busy ? onStop : null,
                icon: const Icon(Icons.stop_rounded),
                label: const Text('Stop recording'),
              )
            else
              FilledButton.icon(
                onPressed: enabled && !capture.busy ? onStart : null,
                icon: const Icon(Icons.mic),
                label: Text(
                  capture.path == null ? 'Start recording' : 'Record again',
                ),
              ),
            OutlinedButton.icon(
              onPressed:
                  enabled &&
                      !capture.busy &&
                      !capture.recording &&
                      capture.path != null
                  ? onPlay
                  : null,
              icon: Icon(capture.playing ? Icons.stop : Icons.headphones),
              label: Text(capture.playing ? 'Stop playback' : 'Play recording'),
            ),
          ],
        ),
      ],
    ),
  );
}
