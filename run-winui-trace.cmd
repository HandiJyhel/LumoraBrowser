@echo off
rem Lance Lumora comme run-winui.cmd, mais avec le journal de debogage
rem (winui-runtime-trace.log) active dans le dossier de lancement.
set LUMORA_TRACE_STARTUP=1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-winui.ps1"
