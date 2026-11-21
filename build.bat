@echo off
rem Double-click to build Builds\Windows\FX_master.exe without opening the Unity editor.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
pause
