@echo off
python "%~dp0package-windows.py" %*
exit /b %ERRORLEVEL%
