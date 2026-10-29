import 'dart:io';

import 'package:flutter/material.dart';
import 'package:record/record.dart';

import '../app.dart';
import '../audio_capture.dart';
import '../local_store.dart';
import '../widgets.dart';

class DeviceReady {
  final InputDevice? microphone;
  const DeviceReady(this.microphone);
}

class DeviceCheck extends StatefulWidget {
  final LocalStore store;
  final CloseGuard closeGuard;
  final bool resuming;
  const DeviceCheck({
    super.key,
    required this.store,
    required this.closeGuard,
    this.resuming = false,
  });
  @override
  State<DeviceCheck> createState() => _DeviceCheckState();
}

class _DeviceCheckState extends State<DeviceCheck> {
  final _capture = AudioCapture();
  List<InputDevice> _devices = [];
  InputDevice? _selected;
  String? _error;
  bool _heard = false, _played = false, _action = false;
  @override
  void initState() {
    super.initState();
    widget.closeGuard.handler = _closeWindow;
    _loadDevices();
  }

  Future<void> _loadDevices() async {
    try {
      final devices = await _capture.devices();
      if (mounted) setState(() => _devices = devices);
    } catch (e) {
      if (mounted) setState(() => _error = 'Cannot access microphones: $e');
    }
  }

  Future<bool> _closeWindow() async {
    if (_action || _capture.busy) return false;
    if (_capture.recording) await _capture.stop();
    await _capture.stopPlayback();
    return true;
  }

  Future<void> _run(Future<void> Function() work) async {
    if (_action) return;
    setState(() {
      _action = true;
      _error = null;
    });
    try {
      await work();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _action = false);
    }
  }

  Future<void> _start() async {
    setState(() {
      _heard = false;
      _played = false;
    });
    final folder = Directory('${widget.store.root.path}/device-check');
    await folder.create(recursive: true);
    await _capture.start('${folder.path}/test.wav', device: _selected);
  }

  Future<void> _done(bool ready) async {
    await _run(() async {
      if (_capture.recording) await _capture.stop();
      await _capture.stopPlayback();
      if (mounted) {
        Navigator.pop(context, ready ? DeviceReady(_selected) : null);
      }
    });
  }

  @override
  void dispose() {
    widget.closeGuard.handler = null;
    _capture.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: false,
    child: Dialog(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: ListenableBuilder(
          listenable: _capture,
          builder: (context, _) => Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Flexible(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.fromLTRB(24, 24, 24, 8),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Check your microphone',
                        style: TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'Record a short test and listen with headphones.',
                      ),
                      const SizedBox(height: 20),
                      DropdownButtonFormField<String>(
                        initialValue: '',
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Microphone',
                        ),
                        items: [
                          const DropdownMenuItem(
                            value: '',
                            child: Text('Windows default device'),
                          ),
                          ..._devices.map(
                            (d) => DropdownMenuItem(
                              value: d.id,
                              child: Text(
                                d.label,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ),
                        ],
                        onChanged: _capture.recording || _action
                            ? null
                            : (id) {
                                _run(() async {
                                  await _capture.clear();
                                  if (!mounted) return;
                                  setState(() {
                                    _selected = id == ''
                                        ? null
                                        : _devices.firstWhere(
                                            (d) => d.id == id,
                                          );
                                    _heard = false;
                                    _played = false;
                                  });
                                });
                              },
                      ),
                      const SizedBox(height: 20),
                      if (_error != null) ...[
                        Notice(_error!, error: true),
                        const SizedBox(height: 16),
                      ],
                      RecordingPanel(
                        capture: _capture,
                        enabled: !_action,
                        onStart: () => _run(_start),
                        onStop: () => _run(_capture.stop),
                        onPlay: () => _run(() async {
                          if (_capture.playing) {
                            await _capture.stopPlayback();
                          } else {
                            await _capture.play();
                            if (mounted) setState(() => _played = true);
                          }
                        }),
                      ),
                      const SizedBox(height: 12),
                      CheckboxListTile(
                        contentPadding: EdgeInsets.zero,
                        value: _heard,
                        title: const Text('I can hear my recording clearly'),
                        onChanged: _played && !_capture.recording && !_action
                            ? (v) => setState(() => _heard = v ?? false)
                            : null,
                      ),
                    ],
                  ),
                ),
              ),
              const Divider(height: 1),
              Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      widget.resuming ? 'Your exam timer is still running.' : 'Start exam begins the timer. It keeps running if you close the app.',
                      style: const TextStyle(color: muted, fontSize: 12),
                    ),
                    const SizedBox(height: 16),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        TextButton(
                          onPressed: _action ? null : () => _done(false),
                          child: const Text('Back'),
                        ),
                        const SizedBox(width: 12),
                        FilledButton(
                          onPressed: _heard && !_capture.recording && !_action
                              ? () => _done(true)
                              : null,
                          child: Text(
                            widget.resuming ? 'Resume exam' : 'Start exam',
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}
