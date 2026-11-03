import 'dart:async';
import 'dart:io';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';
import 'package:record/record.dart';

class AudioCapture extends ChangeNotifier {
  final AudioRecorder _recorder = AudioRecorder();
  AudioPlayer? _player;
  Timer? _clock;
  StreamSubscription<Amplitude>? _amplitude;
  StreamSubscription<PlayerState>? _playback;
  bool recording = false, busy = false, playing = false, _disposed = false;
  int seconds = 0;
  double level = 0;
  String? path;
  DateTime? _started;
  Future<void> Function(String)? onSaved;
  void _notify() {
    if (!_disposed) notifyListeners();
  }

  Future<List<InputDevice>> devices() => _recorder.listInputDevices();
  Future<void> start(String output, {InputDevice? device}) async {
    if (busy || recording) return;
    busy = true;
    _notify();
    try {
      await stopPlayback();
      if (!await _recorder.hasPermission()) {
        throw StateError(
          'Microphone permission is missing. Open Windows Settings > Privacy & security > Microphone and allow desktop apps.',
        );
      }
      if (!await _recorder.isEncoderSupported(AudioEncoder.wav)) {
        throw StateError(
          'This device does not support WAV recording. Select another microphone.',
        );
      }
      await _recorder.start(
        RecordConfig(
          encoder: AudioEncoder.wav,
          sampleRate: 16000,
          numChannels: 1,
          device: device,
        ),
        path: output,
      );
      path = output;
      seconds = 0;
      _started = DateTime.now();
      recording = true;
      _amplitude = _recorder
          .onAmplitudeChanged(const Duration(milliseconds: 150))
          .listen((a) {
            level = ((a.current + 60) / 60).clamp(0.0, 1.0);
            _notify();
          }, onError: (Object _) {});
      _clock = Timer.periodic(const Duration(seconds: 1), (_) {
        seconds = DateTime.now().difference(_started!).inSeconds;
        _notify();
      });
    } finally {
      busy = false;
      _notify();
    }
  }

  Future<void> stop() async {
    if (!recording || busy) return;
    busy = true;
    _notify();
    try {
      final saved = await _recorder.stop();
      recording = false;
      _clock?.cancel();
      await _amplitude?.cancel();
      level = 0;
      path = saved ?? path;
      if (path != null) await onSaved?.call(path!);
    } finally {
      busy = false;
      _notify();
    }
  }

  Future<void> play() async {
    if (path == null || recording || busy) return;
    if (!await File(path!).exists()) {
      throw StateError('Recording file not found.');
    }
    _player ??= AudioPlayer();
    _playback ??= _player!.onPlayerStateChanged.listen((state) {
      playing = state == PlayerState.playing;
      _notify();
    });
    await _player!.play(DeviceFileSource(path!));
  }

  Future<void> stopPlayback() async {
    await _player?.stop();
    playing = false;
    _notify();
  }

  Future<void> clear() async {
    path = null;
    seconds = 0;
    _notify();
    await stopPlayback();
  }

  @override
  void dispose() {
    _disposed = true;
    _clock?.cancel();
    unawaited(_amplitude?.cancel());
    unawaited(_playback?.cancel());
    unawaited(_recorder.dispose());
    unawaited(_player?.dispose());
    super.dispose();
  }
}

String clockText(int seconds) {
  final value = seconds < 0 ? 0 : seconds;
  return '${(value ~/ 60).toString().padLeft(2, '0')}:${(value % 60).toString().padLeft(2, '0')}';
}
