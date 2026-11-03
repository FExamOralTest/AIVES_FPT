@echo off
setlocal
chcp 65001 >nul
set "ROOT_DIR=%~dp0"

echo ================================================================
echo     AIVES - KHOI DONG TRONG WINDOWS TERMINAL (GOM 3 TABS)
echo ================================================================
echo.

wt.exe -w 0 nt --title "Backend API" cmd /k "title AIVES - Backend && cd /d "%ROOT_DIR%backend\Aives.Api" && echo === BACKEND (.NET) === && dotnet run --launch-profile https" ; ^
       nt --title "AI Service" cmd /k "title AIVES - AI Service && cd /d "%ROOT_DIR%ai-service" && echo === AI SERVICE (FASTAPI) === && call .venv\Scripts\activate.bat && python main.py" ; ^
       nt --title "Frontend" cmd /k "title AIVES - Frontend && cd /d "%ROOT_DIR%frontend\aives-web" && echo === FRONTEND (REACT) === && npm run dev"

echo Da mo cac Tab trong Windows Terminal!
timeout /t 3 >nul
