@echo off
rem Draws run review maps of a fight: its overview, flagged moments, or any moment from its recording.
rem Runs maps.ps1 without needing PowerShell's script execution to be allowed. Same arguments as maps.ps1:
rem   tools\maps.cmd latest strip=356
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0maps\maps.ps1" %*
