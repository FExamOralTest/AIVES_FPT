@echo off
setlocal enabledelayedexpansion
title AIVES - Stop All Services
chcp 65001 >nul

echo ================================================================
echo               AIVES - DUNG TAT CA DICH VU
echo ================================================================
echo.

set "PORTS=5173 7223 5116 8000"

for %%p in (%PORTS%) do (
    echo [*] Kiem tra port %%p...
    for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr /r /c:":%%p " ^| findstr LISTENING') do (
        echo     -> Dang dong tien trinh PID %%a tren port %%p...
        taskkill /F /PID %%a >nul 2>&1
    )
)

echo.
echo ================================================================
echo               TAT CA DICH VU DA DUOC DUNG!
echo ================================================================
echo.
pause
