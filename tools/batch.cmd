@echo off
rem Runs a batch of automated fights and writes a report. Same arguments as tools\batch.py; for help:
rem   tools\batch.cmd --help
python "%~dp0batch.py" %*
