# AIVES – AI-powered Viva Exam System

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)
![Flutter](https://img.shields.io/badge/Flutter-Windows-02569B?logo=flutter&logoColor=white)
![FastAPI](https://img.shields.io/badge/FastAPI-Python-009688?logo=fastapi&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)

The main idea is to **run oral (viva) exams with an AI examiner**: students answer questions by voice, the AI scores the answers against the course material, and a human examiner reviews the result before it is published.

AIVES is the SWD392 project of the FPT team. It is split into a web app, a Windows desktop client for students, an ASP.NET Core API and a Python AI microservice.

## Features

- **Voice-based viva**: a student enters a join code, listens to the AI's questions and answers with the microphone.
- **Questions from course content**: questions and rubrics are generated from the uploaded course documents and learning outcomes.
- **Speech-to-text and text-to-speech**: answers are transcribed and the questions are read aloud.
- **AI scoring by rubric**: every answer and the whole session are evaluated per criterion.
- **Examiner review before publishing**: the examiner listens to the original audio, reads the transcript, adjusts the score and publishes the official result.
- **Separation of duties**: course staff prepare the exam, examiners approve the score, so the same person never does both.
- **Retake and review flows**: exam periods, retakes and score review requests.
- **Audio kept for audit**: original recordings are stored in Cloudflare R2, with chunked upload for long answers.
- **Load-tested**: k6 scripts, Prometheus and a Grafana dashboard in [`monitoring/`](monitoring).

## Roles

| Role | What they do |
|---|---|
| **Student** | Joins the exam, answers by voice, reads the published result |
| **Staff** | Manages courses, documents, the question bank and exam sessions |
| **Examiner** | Reviews transcript, audio and AI score, then publishes the result |
| **Admin** | Manages users, course assignments and system settings |

## Architecture

The frontend never calls the AI service or the storage directly. Every request goes through the backend API.

```text
React web app ─────┐
                   ├──(HTTPS / JWT)──► ASP.NET Core 8 API ──► Neon PostgreSQL
Flutter desktop ───┘                        │
                                            ├──► Cloudflare R2   (answer audio)
                                            ├──► Hangfire        (background jobs)
                                            └──► FastAPI AI service
                                                   ├── speech-to-text / text-to-speech
                                                   └── question generation and scoring (LLM)
```

## Tech stack

| Part | Technology |
|---|---|
| Web | React 19, TypeScript, Vite, Tailwind CSS 4, shadcn/ui, React Router |
| Desktop (students) | Flutter / Dart, Windows |
| Backend | ASP.NET Core 8, Entity Framework Core, Npgsql, Hangfire, JWT, AWSSDK.S3 |
| AI service | Python, FastAPI, Pydantic, httpx |
| Database | Neon serverless PostgreSQL |
| Storage | Cloudflare R2 |
| Monitoring | k6, Prometheus, Grafana |
| Deployment | Docker Compose |

## Project structure

```text
AIVES_FPT/
├── backend/               ASP.NET Core solution (Api, Application, Domain, Infrastructure, Tests)
├── frontend/
│   ├── aives-web/         React web app
│   └── aives-desktop/     Flutter Windows client for students
├── ai-service/            FastAPI AI microservice
├── monitoring/            k6 load tests, Prometheus, Grafana dashboard and results
├── docs/                  Architecture, API, RBAC, database and deployment docs
├── scripts/               Setup, seed and test helper scripts
├── docker-compose.yml
└── system_overview_and_flows.md
```

## Getting started

### Requirements

- .NET SDK 8
- Node.js 20+ and npm
- Python 3.11+
- Docker (optional, for the Compose setup)
- Flutter stable (only for the desktop client)

### 1. Configure the environment

```powershell
copy .env.example .env
```

Fill in the database connection and, for audio upload, the `CloudflareR2__*` values. Keep `.env` private and never commit it.

### 2. Run everything with Docker

```powershell
docker compose up --build -d
```

| Service | Default address |
|---|---|
| Web app | http://localhost:5173 |
| Backend API | http://localhost:5000 |
| AI service | http://localhost:8000 |

Docker Compose reads `DATABASE_CONNECTION` from the root `.env` file, in the Npgsql format `Host=...;Database=...;Username=...;Password=...`. The shared database must already have the schema the backend expects, because Compose does not migrate it.

To use an isolated local PostgreSQL instead of Neon:

```powershell
docker compose --profile local-db up --build -d
```

### 3. Or run each part by hand

**Backend** (http://localhost:5116)

```powershell
cd backend
dotnet run --project Aives.Api --launch-profile http
```

**AI service** (http://localhost:8000)

```powershell
cd ai-service
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

**Web app** (http://localhost:5173)

```powershell
cd frontend/aives-web
npm install
npm run dev
```

**Desktop client**

```powershell
cd frontend/aives-desktop
flutter pub get
flutter run -d windows
```

On Windows, `run-all.bat` starts the services together and `stop-all.bat` stops them.

## Tests

```powershell
dotnet test backend/Aives.slnx
cd ai-service; pytest
cd frontend/aives-web; npm run lint; npm run build
```

## Documentation

- [System overview and flows](system_overview_and_flows.md)
- [Backend API guide](backend/API_GUIDE.md)
- [Authentication and RBAC](docs/authentication-rbac.md)
- [Exam period review flow](docs/exam-period-review-flow.md)
- [Retake exams](docs/retake-exams.md)
- [Question bank APIs](docs/question-bank-apis.md)
- [Course document APIs](docs/course-document-apis.md)
- [Desktop client](frontend/aives-desktop/README.md) and [AI service](ai-service/README.md)
- [Load test results](monitoring/monitoring-results.md)

## Team

Built by the FExamOralTest team for the SWD392 course at FPT University.
