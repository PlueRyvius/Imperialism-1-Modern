@echo off
rem Double-click this to start the map editor.
rem
rem You can also drag a .map file onto it to open that map directly; otherwise
rem a file dialog asks which map you want.
rem
rem The console window that appears IS the editor. Closing it stops the editor.

title Imperialism Map Editor

rem Prefer the py launcher, which the python.org installer sets up; fall back
rem to whatever "python" resolves to on PATH.
set PY=
where py >nul 2>&1 && set PY=py -3
if not defined PY where python >nul 2>&1 && set PY=python

if not defined PY (
    echo Python does not appear to be installed, or is not on your PATH.
    echo.
    echo Install Python 3.10 or newer from https://www.python.org/downloads/
    echo and tick "Add python.exe to PATH" in the installer.
    echo.
    pause
    exit /b 1
)

echo Starting the Imperialism map editor...
echo Keep this window open. Press Ctrl+C or close it to stop.
echo.

%PY% "%~dp0server.py" %*

rem Only reached if the server exits. Hold the window open so any error
rem message is readable rather than vanishing with the console.
echo.
echo The editor has stopped.
pause
