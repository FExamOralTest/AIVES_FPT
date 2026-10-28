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
7. Close the app or return to the exam code screen. View official results after publication on the website.

The home screen focuses on the exam code. Expand **Joined exams** to resume an existing enrollment, including after an already-enrolled response. Use **Account help** to verify an expired login. The exam toolbar's **Exam help** menu contains account verification and local recording recovery. Server settings remain collapsed on the sign-in screen.

The timer uses backend startedAt and session end time; reopening does not reset it. Early submission and leaving require confirmation. At timeout, the app attempts to submit the current recording and finish; failures preserve the recording. Session codes are existing enrollment codes, not separate one-time activation codes. Admission and scheduling remain backend responsibilities.

## Recordings and recovery

Stopped recordings and draft journals are saved in the Windows Application Support directory. Use **Exam help > Local recordings > Open recordings folder** to locate them. Data is separated by backend, student and session. Passwords and tokens are not persisted.

Resume on the same computer, account and backend to recover a draft. Normal window closure stops and saves recording. Forced termination or power failure can leave an incomplete WAV. Recovery does not provide offline exams: questions and submissions require the backend. Recordings are not automatically deleted; define a retention policy. The upload limit is 48 MB per answer.

The backend lacks submission idempotency and complete follow-up recovery. After an uncertain upload failure, the app preserves WAV and blocks automatic resubmission. Use **Check server status**. If the server advanced or completed the exam, the app synchronizes. Otherwise ask the proctor to reconcile. Use **Verify account** with the same student when authentication expires.

## Validation

```powershell
flutter analyze
flutter test
flutter build windows --release
```

Tests use a local HTTP stub without a live database or AI service. They cover API contracts, uploads, follow-ups, uncertain submissions, drafts, deadlines and desktop layouts. Manual checks: docs/desktop-test-flows.md. Shared Neon demo: docs/desktop-neon-demo.md.

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
