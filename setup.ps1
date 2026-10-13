``$ErrorActionPreference = "Stop"

$baseDir = $PSScriptRoot
cd $baseDir

Write-Host "Creating Monorepo Root files..."
New-Item -ItemType File -Name ".gitignore" -Force | Out-Null
Set-Content -Path ".gitignore" -Value @'
.env
.env.*
!.env.example

node_modules/

bin/
obj/

venv/
.venv/````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````
__pycache__/

.vs/
.vscode/

*.user
*.suo

dist/
build/
'@

New-Item -ItemType File -Name "README.md" -Force | Out-Null
Set-Content -Path "README.md" -Value @'
# AIVES – AI-powered Viva Exam System

Frontend: React + TypeScript
Backend: ASP.NET Core Web API
AI: Python FastAPI
Database: Neon PostgreSQL

## Architecture
```text
React
  ↓
ASP.NET Core
  ├──→ Neon PostgreSQL
  │
  └──→ Python FastAPI
          ↓
         AI
```
'@

New-Item -ItemType File -Name "docker-compose.yml" -Force | Out-Null
Set-Content -Path "docker-compose.yml" -Value @'
version: '3.8'

services:
  backend:
    build:
      context: ./backend
      dockerfile: Aives.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - ai-service

  ai-service:
    build:
      context: ./ai-service
    ports:
      - "8000:8000"
    environment:
      - ENV=development
'@

Write-Host "Creating Docs..."
New-Item -ItemType Directory -Path "docs/architecture" -Force | Out-Null
New-Item -ItemType File -Path "docs/architecture/architecture.md" -Force | Out-Null
New-Item -ItemType Directory -Path "docs/api" -Force | Out-Null
New-Item -ItemType File -Path "docs/api/api-contract.md" -Force | Out-Null
New-Item -ItemType Directory -Path "docs/database" -Force | Out-Null
New-Item -ItemType File -Path "docs/database/database.md" -Force | Out-Null
New-Item -ItemType Directory -Path "docs/ai" -Force | Out-Null
New-Item -ItemType File -Path "docs/ai/ai-service.md" -Force | Out-Null

Write-Host "Creating Backend..."
New-Item -ItemType Directory -Path "backend" -Force | Out-Null
cd backend

dotnet new sln -n Aives
dotnet new webapi -n Aives.Api --use-controllers -f net8.0
Remove-Item Aives.Api/WeatherForecast.cs -ErrorAction SilentlyContinue
Remove-Item Aives.Api/Controllers/WeatherForecastController.cs -ErrorAction SilentlyContinue

dotnet new classlib -n Aives.Application -f net8.0
Remove-Item Aives.Application/Class1.cs -ErrorAction SilentlyContinue

dotnet new classlib -n Aives.Domain -f net8.0
Remove-Item Aives.Domain/Class1.cs -ErrorAction SilentlyContinue

dotnet new classlib -n Aives.Infrastructure -f net8.0
Remove-Item Aives.Infrastructure/Class1.cs -ErrorAction SilentlyContinue

dotnet sln add Aives.Api/Aives.Api.csproj
dotnet sln add Aives.Application/Aives.Application.csproj
dotnet sln add Aives.Domain/Aives.Domain.csproj
dotnet sln add Aives.Infrastructure/Aives.Infrastructure.csproj

dotnet add Aives.Api/Aives.Api.csproj reference Aives.Application/Aives.Application.csproj
dotnet add Aives.Application/Aives.Application.csproj reference Aives.Domain/Aives.Domain.csproj
dotnet add Aives.Infrastructure/Aives.Infrastructure.csproj reference Aives.Application/Aives.Application.csproj
dotnet add Aives.Infrastructure/Aives.Infrastructure.csproj reference Aives.Domain/Aives.Domain.csproj

# Backend folders
$apiDirs = "Controllers", "Middleware", "Extensions"
foreach ($dir in $apiDirs) { New-Item -ItemType Directory -Path "Aives.Api/$dir" -Force | Out-Null }

$appDirs = "Auth", "Users", "Courses", "Exams", "ExamSessions", "Questions", "Answers", "Results", "AI", "Common"
foreach ($dir in $appDirs) { New-Item -ItemType Directory -Path "Aives.Application/$dir" -Force | Out-Null }

$domainDirs = "Entities", "Enums", "Interfaces", "Common"
foreach ($dir in $domainDirs) { New-Item -ItemType Directory -Path "Aives.Domain/$dir" -Force | Out-Null }

$infraDirs = "Persistence/Configurations", "Persistence/Migrations", "Repositories", "AI", "Storage", "Services"
foreach ($dir in $infraDirs) { New-Item -ItemType Directory -Path "Aives.Infrastructure/$dir" -Force | Out-Null }

# Entities
$entities = "User", "Course", "Document", "Exam", "ExamSession", "Question", "Answer", "Result"
foreach ($entity in $entities) {
    Set-Content -Path "Aives.Domain/Entities/$entity.cs" -Value @"
namespace Aives.Domain.Entities;

public class $entity
{
}
"@
}

dotnet add Aives.Infrastructure/Aives.Infrastructure.csproj package Microsoft.EntityFrameworkCore -v 8.0.0

Set-Content -Path "Aives.Infrastructure/Persistence/AivesDbContext.cs" -Value @'
using Microsoft.EntityFrameworkCore;
using Aives.Domain.Entities;

namespace Aives.Infrastructure.Persistence;

public class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options) : base(options)
    {
    }
}
'@

Set-Content -Path "Aives.Api/appsettings.example.json" -Value @'
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "AI": {
    "BaseUrl": "http://localhost:8000"
  },
  "Jwt": {
    "Secret": ""
  }
}
'@

Set-Content -Path "Aives.Infrastructure/AI/IAiServiceClient.cs" -Value @'
namespace Aives.Infrastructure.AI;

public interface IAiServiceClient
{
    void GenerateQuestion();
    void GenerateFollowUp();
    void SpeechToText();
    void TextToSpeech();
    void EvaluateAnswer();
}
'@

Set-Content -Path "Aives.Infrastructure/AI/AiServiceClient.cs" -Value @'
namespace Aives.Infrastructure.AI;

public class AiServiceClient : IAiServiceClient
{
    public void GenerateQuestion() {}
    public void GenerateFollowUp() {}
    public void SpeechToText() {}
    public void TextToSpeech() {}
    public void EvaluateAnswer() {}
}
'@

Write-Host "Creating AI Service..."
cd $baseDir
New-Item -ItemType Directory -Path "ai-service" -Force | Out-Null
cd ai-service

New-Item -ItemType File -Name "requirements.txt" -Force | Out-Null
Set-Content -Path "requirements.txt" -Value @'
fastapi
uvicorn
pydantic
'@

New-Item -ItemType File -Name ".env.example" -Force | Out-Null
New-Item -ItemType File -Name "README.md" -Force | Out-Null

$aiDirs = "app", "app/api", "app/services", "app/models", "app/prompts", "app/core"
foreach ($dir in $aiDirs) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$pyFiles = "app/api/interview.py", "app/api/speech.py", "app/api/evaluation.py",
           "app/services/interview_service.py", "app/services/question_service.py", "app/services/followup_service.py",
           "app/services/speech_service.py", "app/services/tts_service.py", "app/services/evaluation_service.py", "app/services/scoring_service.py",
           "app/models/request.py", "app/models/response.py",
           "app/core/config.py", "app/core/ai_client.py"
foreach ($file in $pyFiles) { New-Item -ItemType File -Path $file -Force | Out-Null }

$txtFiles = "app/prompts/question_prompt.txt", "app/prompts/followup_prompt.txt", "app/prompts/scoring_prompt.txt"
foreach ($file in $txtFiles) { New-Item -ItemType File -Path $file -Force | Out-Null }

Set-Content -Path "app/main.py" -Value @'
from fastapi import FastAPI

app = FastAPI(title="AIVES AI Service")

@app.get("/health")
def health_check():
    return {"status": "ok", "service": "aives-ai"}

@app.post("/ai/interview/question")
def generate_question():
    return {"success": True, "message": "AI endpoint placeholder"}

@app.post("/ai/interview/follow-up")
def generate_followup():
    return {"success": True, "message": "AI endpoint placeholder"}

@app.post("/ai/speech-to-text")
def speech_to_text():
    return {"success": True, "message": "AI endpoint placeholder"}

@app.post("/ai/text-to-speech")
def text_to_speech():
    return {"success": True, "message": "AI endpoint placeholder"}

@app.post("/ai/evaluate")
def evaluate_answer():
    return {"success": True, "message": "AI endpoint placeholder"}
'@

Write-Host "Creating Frontend..."
cd $baseDir
New-Item -ItemType Directory -Path "frontend" -Force | Out-Null
cd frontend
# Create Vite project in aives-web
npx --yes create-vite@latest aives-web --template react-ts

cd aives-web
$srcDirs = "src/api", "src/components/common", "src/components/auth", "src/components/admin", "src/components/viva", "src/pages/Login", "src/pages/admin/Dashboard", "src/pages/admin/Courses", "src/pages/admin/Exams", "src/pages/student/JoinExam", "src/pages/student/VivaExam", "src/pages/student/Result", "src/hooks", "src/stores", "src/types", "src/utils"
foreach ($dir in $srcDirs) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

$apiFiles = "src/api/authApi.ts", "src/api/courseApi.ts", "src/api/examApi.ts", "src/api/examSessionApi.ts", "src/api/resultApi.ts"
foreach ($file in $apiFiles) { New-Item -ItemType File -Path $file -Force | Out-Null }

New-Item -ItemType File -Path ".env.example" -Force | Out-Null

Write-Host "Done!"
