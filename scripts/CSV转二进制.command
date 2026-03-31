#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}" || exit 1

if command -v python3 >/dev/null 2>&1; then
    PYTHON_CMD="python3"
elif command -v python >/dev/null 2>&1; then
    PYTHON_CMD="python"
else
    echo "[error] 未找到 python3 或 python。"
    echo
    read -r -p "按回车关闭..." _
    exit 1
fi

"${PYTHON_CMD}" scripts/export_holmas_config.py "$@"
STATUS=$?

echo
if [ ${STATUS} -eq 0 ]; then
    echo "[info] 导出完成。"
else
    echo "[error] 导出失败，退出码：${STATUS}"
fi

echo
read -r -p "按回车关闭..." _

if [ -n "${TERM_PROGRAM:-}" ] && [ "${TERM_PROGRAM}" = "Apple_Terminal" ]; then
    osascript >/dev/null 2>&1 <<'APPLESCRIPT'
tell application "Terminal"
    try
        close front window
    end try
end tell
APPLESCRIPT
fi

exit ${STATUS}
