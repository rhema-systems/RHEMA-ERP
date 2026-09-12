@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "LOG=C:\RhemaERP\logs\renew-ip-cert.log"
set "CERTIFICATE=C:\RhemaERP\certs\lego-ip\certificates\rhemaerp-ip.crt"
set "RESTART_FLAG=C:\RhemaERP\logs\renew-ip-cert.restart-required"
set "HOME=C:\RhemaERP\certs\lego-ip"
set "USERPROFILE=C:\RhemaERP\certs\lego-ip"
set "TEMP=C:\RhemaERP\tmp\certificate-renewal"
set "TMP=C:\RhemaERP\tmp\certificate-renewal"

if not exist "C:\RhemaERP\logs" mkdir "C:\RhemaERP\logs"
if not exist "%TEMP%" mkdir "%TEMP%"

echo [%DATE% %TIME%] Starting IP certificate renewal check.>>"%LOG%"

set "BEFORE_CERTIFICATE="
if exist "%CERTIFICATE%" for %%I in ("%CERTIFICATE%") do set "BEFORE_CERTIFICATE=%%~zI^|%%~tI"

C:\RhemaERP\tools\lego\lego.exe run ^
  --path C:\RhemaERP\certs\lego-ip ^
  --email admin@rhemasystems.edu.gh ^
  --accept-tos ^
  --server letsencrypt ^
  --profile shortlived ^
  --domains 149.102.145.190 ^
  --cert.name rhemaerp-ip ^
  --http ^
  --http.webroot C:\RhemaERP\acme-webroot ^
  --pem ^
  --renew-days 4 ^
  --no-random-sleep >>"%LOG%" 2>&1
set "LEGO_EXIT=!ERRORLEVEL!"

if not "!LEGO_EXIT!"=="0" (
  echo [%DATE% %TIME%] Renewal command failed with exit code !LEGO_EXIT!.>>"%LOG%"
  exit /b !LEGO_EXIT!
)

set "AFTER_CERTIFICATE="
if exist "%CERTIFICATE%" for %%I in ("%CERTIFICATE%") do set "AFTER_CERTIFICATE=%%~zI^|%%~tI"

if not defined AFTER_CERTIFICATE (
  echo [%DATE% %TIME%] Renewal completed without a readable certificate file.>>"%LOG%"
  exit /b 23
)

if not "!BEFORE_CERTIFICATE!"=="!AFTER_CERTIFICATE!" (
  echo [%DATE% %TIME%] Certificate changed; HTTPS proxy reload requested.>>"%LOG%"
  >"%RESTART_FLAG%" echo Certificate renewed at %DATE% %TIME%.
) else (
  echo [%DATE% %TIME%] Certificate unchanged; proxy restart skipped.>>"%LOG%"
)

exit /b 0
