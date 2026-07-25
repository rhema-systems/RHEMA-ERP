@echo off
setlocal
cd /d "%~dp0..\src\ErpSystem.Api"
set ASPNETCORE_ENVIRONMENT=Development
set DOTNET_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5031
bin\Debug\net8.0\ErpSystem.Api.exe
