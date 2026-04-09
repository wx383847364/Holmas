@echo off
setlocal EnableExtensions

chcp 65001 >nul

cd /d "%~dp0\.." || exit /b 1

set "PYTHON_EXE="
set "PYTHON_ARGS="
where py >nul 2>nul
if %ERRORLEVEL%==0 (
    set "PYTHON_EXE=py"
    set "PYTHON_ARGS=-3"
) else (
    where python >nul 2>nul
    if %ERRORLEVEL%==0 (
        set "PYTHON_EXE=python"
    )
)

if "%PYTHON_EXE%"=="" (
    echo [error] 未找到 py 或 python。
    echo.
    pause
    exit /b 1
)

"%PYTHON_EXE%" %PYTHON_ARGS% scripts\export_holmas_config.py %*
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
