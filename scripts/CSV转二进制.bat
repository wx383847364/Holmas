@echo off
setlocal

cd /d "%~dp0\.." || exit /b 1

set "PYTHON_CMD="
where py >nul 2>nul
if %ERRORLEVEL%==0 (
    set "PYTHON_CMD=py -3"
) else (
    where python >nul 2>nul
    if %ERRORLEVEL%==0 (
        set "PYTHON_CMD=python"
    )
)

if "%PYTHON_CMD%"=="" (
    echo [error] 未找到 py 或 python。
    echo.
    pause
    exit /b 1
)

call %PYTHON_CMD% scripts\export_holmas_config.py %*
set "STATUS=%ERRORLEVEL%"

echo.
if %STATUS%==0 (
    echo [info] 导出完成。
) else (
    echo [error] 导出失败，退出码：%STATUS%
)

echo.
pause
exit /b %STATUS%
