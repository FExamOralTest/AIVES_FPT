# AIVES Student Desktop

Native Flutter/Dart Windows client for student oral examinations, using the same ASP.NET Core backend as the AIVES website. Staff, examiners and moderators use the web app.

## Requirements

- Flutter stable supporting Dart ^3.13.5 (developed with Flutter 3.47.7).
- Visual Studio: Desktop development with C++, Windows SDK and CMake.
- Windows Developer Mode for plugin symlinks: open Run and enter ms-settings:developers.
- Microphone access enabled for desktop apps in Windows privacy settings.

Android SDK is not required for Windows builds. Backend, AI services and database are separate services, not packaged into the executable.

## Run and build

From the repository root:

```powershell
cd frontend/aives-desktop
flutter pub get
flutter run -d windows
flutter build windows --release
```

Close the executable before rebuilding. Output: build/windows/x64/runner/Release.
Distribute the entire Release folder, including aives_desktop.exe, DLLs and data. Recipients do not need Flutter or Visual Studio. They need compatible Windows, backend access, microphone permission and Microsoft Visual C++ Redistributable if its runtime is missing. An installer and automatic updates are not implemented.

Commit source files, Windows configuration and pubspec.lock. Do not commit .env, credentials, recordings, .dart_tool or build outputs.

## Fullscreen examination mode and Quit

The Windows app starts in a borderless fullscreen window covering the entire main display, including the taskbar area. Additional displays are covered by opaque windows so they cannot be used to interact with the desktop. Display resolution, scaling and monitor changes are handled while the app is open. The window cannot be resized or minimized through its normal controls. Common switching shortcuts, including the Windows keys, Alt+Tab, Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc and Alt+F4, are suppressed while the app is running.

**Quit** remains visible above every screen and dialog. On sign-in or the home screen, confirm **Quit AIVES?** to return to Windows. During an unfinished exam, the existing **Save and leave** confirmation stops recording, preserves the local draft and leaves the backend timer running. Wait for recording or submission operations to finish before quitting. After confirmed completion, Quit closes the app. Keyboard restrictions and display covers are removed when the app exits.

These are application-level restrictions. A normal desktop process cannot guarantee that a student can access nothing else: Ctrl+Alt+Del, the Windows secure desktop and UAC, administrator actions, forced termination, power controls and some hardware or accessibility features remain operating-system concerns. Windows can also remove an unresponsive keyboard hook. School-managed computers requiring enforced lockdown need an administrator-configured Windows kiosk environment and applicable Keyboard Filter policies; this app does not change machine policy. See Microsoft's [kiosk hardening recommendations](https://learn.microsoft.com/en-us/windows/configuration/assigned-access/recommendations), [Keyboard Filter documentation](https://learn.microsoft.com/en-us/windows/configuration/keyboard-filter/) and [keyboard hook lifetime limitations](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

## Backend and shared Neon data

Open **Server settings** and enter the backend origin without /api.

- Default: http://localhost:5116, matching the backend HTTP development profile.
- Local Docker: http://localhost:5000 when host port 5000 maps to container port 8080.
- Remote backend: HTTPS with a valid certificate. HTTP is allowed only for loopback hosts.
- Enter the backend URL, not the website, AI service or Neon connection string.
- Localhost refers to the computer running the desktop client. Each developer must run a local backend or use the shared HTTPS backend.
- Desktop and web share data when their backends use the same Neon project, branch and database. Database credentials belong only on the backend.
- Sign out and sign in again after switching backend databases.
- The URL saved after successful login takes precedence over the build default.

Override the default server:

```powershell
flutter run -d windows --dart-define=API_BASE_URL=https://api.example.com
flutter build windows --release --dart-define=API_BASE_URL=https://api.example.com
```

Start the backend only after explicitly configuring a development database. Follow docs/codex-cloud.md. Do not run setup.ps1 on an existing checkout because it overwrites files. Check startup seeding configuration before connecting to a shared database. Use mock providers for ordinary development checks.

## Examination workflow

1. Sign in with a STUDENT account; access tokens remain in memory.
2. Enter the exam code and select **Continue**. An open session goes directly to the microphone check. If the proctor has not opened it, select **Check again** when instructed.
3. Record a microphone test, listen through headphones and confirm that the recording is clear. Select **Start exam** to start the timer.
4. Read the question and record WAV mono 16 kHz answers in the same panel, play back or record again, then submit.
5. Answer follow-ups returned by the backend.
6. Finish and wait for backend confirmation.
7. Select **Quit** or return to the exam code screen. View official results after publication on the website.

The home screen focuses on the exam code. Expand **Joined exams** to resume an existing enrollment, including after an already-enrolled response. Use **Account help** to verify an expired login. The exam toolbar's **Exam help** menu contains account verification and local recording recovery. Server settings remain collapsed on the sign-in screen.

The timer uses backend startedAt and session end time; reopening does not reset it. Early submission and leaving require confirmation. At timeout, the app attempts to submit the current recording and finish; failures preserve the recording. Session codes are existing enrollment codes, not separate one-time activation codes. Admission and scheduling remain backend responsibilities.

## Recordings and recovery

Stopped recordings and draft journals are saved in the Windows Application Support directory. **Exam help > Local recordings** displays the folder path. Quit the app before opening that folder in File Explorer; the exam screen cannot launch external applications. Data is separated by backend, student and session. Passwords and tokens are not persisted.

Before every answer upload, the desktop app flushes the stopped WAV to disk and writes a cumulative `exam.json` backup. Before requesting final grading, it checks the archived WAV files again and saves the completed exam snapshot. If local saving fails (for example, disk full or denied access), the app does not send that upload or grading request. Existing question-by-question uploads remain necessary for server-generated follow-ups; the app does not wait until the end to upload all answers.

On Windows, the default folder is under `C:\Users\<Windows user>\AppData\Roaming\AIVES\AIVES Desktop\AIVES\recordings\<encoded backend>\student_<id>\session_<id>`. Windows resolves AppData independently of the drive containing the executable. If AppData is redirected away from drive C, exam entry is blocked with a message to contact the proctor. The completion screen displays the actual local folder.

The folder retains `exam.json` (student/session identity, question text including follow-ups, recording filenames, byte lengths and backup timestamps), the WAV files and the separate `draft.json` recovery journal. Recording again keeps previous attempts. Uploading, clearing the current draft and finishing never delete the archive. `savedBeforeUpload` and `savedBeforeGrading` describe local backup state; they do not prove server acceptance. A new app launch can recover a flushed `exam.tmp` left by an interruption before rename.

Resume on the same computer, account and backend to recover a draft. Quitting through **Save and leave** stops and saves recording. Forced termination or power failure can leave an incomplete WAV. Recovery does not provide offline exams: questions and submissions require the backend. Recordings are not automatically deleted; define a retention policy. The upload limit is 48 MB per answer.

The backend lacks submission idempotency and complete follow-up recovery. After an uncertain upload failure, the app preserves WAV and blocks automatic resubmission. Use **Check server status**. If the server advanced or completed the exam, the app synchronizes. Otherwise ask the proctor to reconcile. Use **Verify account** with the same student when authentication expires.

## Validation

```powershell
flutter analyze
flutter test
flutter build windows --release
```

Tests use a local HTTP stub without a live database or AI service. They cover API contracts, uploads, follow-ups, uncertain submissions, drafts, deadlines and desktop layouts. Manual checks: docs/desktop-test-flows.md. Shared Neon demo: docs/desktop-neon-demo.md.

GitHub Actions runs the **Desktop Windows** workflow on desktop pull requests and pushes to `d_desktop-*`, `dev` and `main`. It installs Flutter 3.47.7, analyzes, tests and builds a Windows release without database credentials. Download **AIVES-Student-Windows** from the successful run's Artifacts section, extract the complete folder and run `aives_desktop.exe`. Artifacts expire after 14 days.

Pushes to `dev` or `main`, and manual **Run workflow** dispatches, also publish a GitHub Release. The zip is attached to that release and does not expire with the artifact. Releases from `dev` and `main` are marked latest, so the download link is the repository **Releases** page (`/releases/latest`). A manual run from any other branch is published as a pre-release and does not replace latest. Recipients who watch the repository with release notifications enabled receive GitHub's release notification. The workflow builds the app but does not launch it or verify Windows shortcut interception and monitor coverage.

Mock speech recognition/scoring verifies integration, not voice accuracy. Question text-to-speech is not implemented. Two-examiner and moderator processing belongs to backend/web.

Client-owned text, comments, tests and documentation are English. Names, questions, transcripts and feedback are displayed as supplied by the shared backend. Existing database content is not translated by this client.

## API endpoints

| Operation | Endpoint |
|---|---|
| Sign in | POST /api/auth/login |
| Enroll | POST /api/enroll/session |
| Sessions | GET /api/student/enrollments |
| Start/resume | POST /api/student/viva-sessions/{id}/start |
| Upload WAV | POST /api/student/viva-sessions/{id}/submit-answer |
| Finish | POST /api/student/viva-sessions/{id}/finish |
| Results | GET /api/student/results/{id} |
