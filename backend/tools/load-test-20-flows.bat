@echo off
setlocal
chcp 65001 >nul
title AIVES - Load test 20 luongs cham bai

set "ROOT_DIR=%~dp0..\.."
cd /d "%ROOT_DIR%"

set "BASE_URL=http://localhost:5116"
set "ADMIN_USER=loadadmin"
set "ADMIN_PASS=Load@12345"

REM Neu truyen doi so thi ghi de cac tham so mac dinh.
if not "%~1"=="" set "BASE_URL=%~1"
if not "%~2"=="" set "ADMIN_USER=%~2"
if not "%~3"=="" set "ADMIN_PASS=%~3"

echo ================================================================
echo   AIVES - LOAD TEST: 20 LUONG CHAM BAI SONG SONG
echo ================================================================
echo   Base URL   : %BASE_URL%
echo   Admin      : %ADMIN_USER%
echo   Luong mac dinh: 20 sinh vien x 3 cau = 60 cau cham
echo.
echo   Them tham so PowerShell:
echo     load-test-20-flows.bat <baseUrl> <adminUser> <adminPass> -StudentCount 50
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "backend\tools\load-test-20-flows.ps1" ^
  -BaseUrl "%BASE_URL%" ^
  -AdminUser "%ADMIN_USER%" ^
  -AdminPassword "%ADMIN_PASS%"

echo.
echo ================================================================
echo   Ket thuc. Huong dan: backend\tools\README-load-test.md
echo ================================================================
pause
