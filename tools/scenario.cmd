@echo off
rem Runs scenario.ps1 without needing PowerShell's script execution to be allowed on this computer.
rem The bypass applies to this one run only and changes no settings. Same arguments as scenario.ps1:
rem   tools\scenario.cmd -Window pilot=average build=greedy frames=20000
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scenario\scenario.ps1" %*
