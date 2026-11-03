@echo off
setlocal enabledelayedexpansion
title AIVES - System Launcher
chcp 65001 >nul

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

echo ================================================================
echo               AIVES - HE THONG KHOI DONG DICH VU
echo ================================================================
echo.

:: ------------------------------------------------------------------
:: 1. KIEM TRA VA CHUAN BI CAU HINH CHO AI SERVICE
:: ------------------------------------------------------------------
echo [*] Kiem tra AI Service...
if not exist "ai-service\.env" (
    if exist "ai-service\.env.example" (
        echo     -> Tao file ai-service\.env tu .env.example
        copy "ai-service\.env.example" "ai-service\.env" >nul
    )
)

if not exist "ai-service\.venv\Scripts\python.exe" (
    echo     -> Chua co moi truong ao (.venv). Dang khoi tao...
    where py >nul 2>&1
    if !errorlevel! equ 0 (
        py -m venv ai-service\.venv
    ) else (
        python -m venv ai-service\.venv
    )
    echo     -> Dang cai dat dependencies cho AI Service...
    ai-service\.venv\Scripts\pip.exe install -r ai-service\requirements.txt
)

:: ------------------------------------------------------------------
:: 2. KIEM TRA VA CHUAN BI CHO FRONTEND
:: ------------------------------------------------------------------
echo [*] Kiem tra Frontend (aives-web)...
if not exist "frontend\aives-web\.env" (
    if exist "frontend\aives-web\.env.example" (
        echo     -> Tao file frontend\aives-web\.env tu .env.example
        copy "frontend\aives-web\.env.example" "frontend\aives-web\.env" >nul
    )
)

if not exist "frontend\aives-web\node_modules" (
    echo     -> Chua cai dat node_modules cho frontend. Dang chay npm install...
    cd /d "%ROOT_DIR%frontend\aives-web"
    call npm install
    cd /d "%ROOT_DIR%"
)

echo.
echo ================================================================
echo           DANG KHOI CHAY 3 DICH VU TRONG CAC CUA SO RIENG
echo ================================================================

:: 1. KHOI CHAY BACKEND (.NET WEB API)
echo [1/3] Khoi dong Backend (.NET Web API)...
start "AIVES - Backend API [Port 7223/5116]" cmd /k "title AIVES - Backend API && cd /d "%ROOT_DIR%backend\Aives.Api" && echo === AIVES BACKEND API (ASP.NET CORE) === && dotnet run --launch-profile https"

:: 2. KHOI CHAY AI SERVICE (FASTAPI)
echo [2/3] Khoi dong AI Service (FastAPI)...
start "AIVES - AI Service [Port 8000]" cmd /k "title AIVES - AI Service && cd /d "%ROOT_DIR%ai-service" && echo === AIVES AI SERVICE (FASTAPI) === && call .venv\Scripts\activate.bat && python main.py"

:: 3. KHOI CHAY FRONTEND (VITE REACT)
echo [3/3] Khoi dong Frontend (React Vite)...
start "AIVES - Frontend [Port 5173]" cmd /k "title AIVES - Frontend && cd /d "%ROOT_DIR%frontend\aives-web" && echo === AIVES FRONTEND (VITE REACT) === && npm run dev"

echo.
echo ================================================================
echo             TAT CA DICH VU DA DUOC KHOI CHAY THANH CONG!
echo ================================================================
echo.
echo   [1] Frontend Web   : http://localhost:5173
echo   [2] Backend API    : https://localhost:7223 (hoac http://localhost:5116)
echo       Swagger Docs   : https://localhost:7223/swagger
echo   [3] AI Service     : http://localhost:8000
echo       API Docs       : http://localhost:8000/docs
echo.
echo ----------------------------------------------------------------
echo   De dung tat ca dich vu, ban co the chay file 'stop-all.bat'
echo ----------------------------------------------------------------
echo.

:: Tuy chon mo browser nhanh
set /p OPEN_BROWSER="Ban co muon mo giao dien Web ngay tren Browser khong? (Y/N, mac dinh Y): "
if /i "%OPEN_BROWSER%"=="" set OPEN_BROWSER=Y
if /i "%OPEN_BROWSER%"=="Y" (
    timeout /t 2 >nul
    start http://localhost:5173
)

echo Cua so nay se giu lai. Bam phim bat ky de thoat trinh khoi dong...
pause >nul