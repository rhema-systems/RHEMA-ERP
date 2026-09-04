@echo off
setlocal EnableExtensions

set "LOG=C:\RhemaERP\logs\renew-ip-cert.log"
set "RESTART_FLAG=C:\RhemaERP\logs\renew-ip-cert.restart-required"

if not exist "%RESTART_FLAG%" exit /b 0

echo [%DATE% %TIME%] Reloading RhemaERPHTTPSIPProxy after certificate renewal.>>"%LOG%"
net.exe stop RhemaERPHTTPSIPProxy /y >>"%LOG%" 2>&1
if errorlevel 1 exit /b 21

net.exe start RhemaERPHTTPSIPProxy >>"%LOG%" 2>&1
if errorlevel 1 exit /b 22

del /q "%RESTART_FLAG%" >nul 2>&1
echo [%DATE% %TIME%] HTTPS proxy certificate reload completed.>>"%LOG%"
exit /b 0
