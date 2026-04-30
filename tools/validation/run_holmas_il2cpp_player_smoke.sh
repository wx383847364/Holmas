#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_VERSION_FILE="${REPO_ROOT}/ProjectSettings/ProjectVersion.txt"

EDITOR_PATH="${TUANJIE_EDITOR_PATH:-}"
TEMP_PROJECT_DIR=""
KEEP_TEMP_ON_SUCCESS=0
BUILD_ONLY=0
BUILD_TARGET=""
LOG_PREFIX="holmas_il2cpp_player_smoke_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="/tmp"
SMOKE_METHOD="HolmasIl2CppPlayerSmoke.RunRequestedSmoke"
TEMP_CREATED_BY_SCRIPT=0

usage() {
    cat <<'EOF'
用法：
  tools/validation/run_holmas_il2cpp_player_smoke.sh [--editor /path/to/Tuanjie] [--temp-dir /private/tmp/xxx]
                                                     [--build-target StandaloneOSX]
                                                     [--log-prefix holmas_xxx] [--log-dir /tmp]
                                                     [--build-only] [--keep-temp-on-success]

说明：
  - 自动复制当前项目到临时目录执行 IL2CPP player smoke，避免提交 HybridCLRData、StreamingAssets、Player 等临时产物
  - 构建前会执行 Holmas/HotUpdate/Generate And Copy HybridCLR Assets 的严格流程
  - 构建时追加 HOLMAS_YOO_OFFLINE_PLAYMODE，使 player 从内置 YooAssets 包启动
  - 成功构建后默认尝试启动 Standalone player 并检查日志中的 GameBootstrap/HybridCLR 成功标记
EOF
}

log() {
    printf '[info] %s\n' "$1"
}

error() {
    printf '[error] %s\n' "$1" >&2
}

detect_editor_path() {
    if [[ -n "${EDITOR_PATH}" ]]; then
        return 0
    fi

    if [[ ! -f "${PROJECT_VERSION_FILE}" ]]; then
        error "找不到 ProjectVersion.txt，无法自动推断编辑器路径。"
        exit 1
    fi

    local editor_version
    editor_version="$(awk -F': ' '/^m_EditorVersion: / { print $2; exit }' "${PROJECT_VERSION_FILE}")"
    if [[ -z "${editor_version}" ]]; then
        error "无法从 ProjectVersion.txt 读取 m_EditorVersion。"
        exit 1
    fi

    local tuanjie_candidate="/Applications/Tuanjie/Hub/Editor/${editor_version}/Tuanjie.app/Contents/MacOS/Tuanjie"
    local unity_candidate="/Applications/Unity/Hub/Editor/${editor_version}/Unity.app/Contents/MacOS/Unity"

    if [[ -x "${tuanjie_candidate}" ]]; then
        EDITOR_PATH="${tuanjie_candidate}"
        return 0
    fi

    if [[ -x "${unity_candidate}" ]]; then
        EDITOR_PATH="${unity_candidate}"
        return 0
    fi

    error "未找到匹配版本的团结/Unity 编辑器，请用 --editor 显式传入。"
    exit 1
}

cleanup_temp_project() {
    if [[ "${KEEP_TEMP_ON_SUCCESS}" -eq 1 ]]; then
        return 0
    fi

    if [[ "${TEMP_CREATED_BY_SCRIPT}" -ne 1 ]]; then
        return 0
    fi

    if [[ -n "${TEMP_PROJECT_DIR}" && -d "${TEMP_PROJECT_DIR}" ]]; then
        rm -rf "${TEMP_PROJECT_DIR}"
        log "验证通过，已删除临时工程：${TEMP_PROJECT_DIR}"
    fi
}

on_exit() {
    local exit_code=$?
    if [[ ${exit_code} -eq 0 ]]; then
        cleanup_temp_project
    elif [[ -n "${TEMP_PROJECT_DIR}" ]]; then
        error "验证失败，已保留临时工程：${TEMP_PROJECT_DIR}"
    fi
    exit ${exit_code}
}

trap on_exit EXIT

while [[ $# -gt 0 ]]; do
    case "$1" in
        --editor)
            EDITOR_PATH="${2:-}"
            shift 2
            ;;
        --temp-dir)
            TEMP_PROJECT_DIR="${2:-}"
            shift 2
            ;;
        --build-target)
            BUILD_TARGET="${2:-}"
            shift 2
            ;;
        --log-prefix)
            LOG_PREFIX="${2:-}"
            shift 2
            ;;
        --log-dir)
            LOG_DIR="${2:-}"
            shift 2
            ;;
        --build-only)
            BUILD_ONLY=1
            shift
            ;;
        --keep-temp-on-success)
            KEEP_TEMP_ON_SUCCESS=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            error "未知参数：$1"
            usage
            exit 1
            ;;
    esac
done

detect_editor_path
mkdir -p "${LOG_DIR}"

if [[ -z "${TEMP_PROJECT_DIR}" ]]; then
    TEMP_PROJECT_DIR="$(mktemp -d "/private/tmp/holmas_il2cpp_player_smoke_XXXXXX")"
    TEMP_CREATED_BY_SCRIPT=1
else
    mkdir -p "${TEMP_PROJECT_DIR}"
fi

BUILD_LOG="${LOG_DIR}/${LOG_PREFIX}_build.log"
PLAYER_LOG="${LOG_DIR}/${LOG_PREFIX}_player.log"
REQUEST_PATH="${TEMP_PROJECT_DIR}/Library/holmas_il2cpp_player_smoke_request.json"
RESULT_PATH="${TEMP_PROJECT_DIR}/Library/holmas_il2cpp_player_smoke_result.json"
PLAYER_OUTPUT_DIR="${TEMP_PROJECT_DIR}/Library/HolmasIl2CppPlayerSmoke/Player"
BATCHMODE_TIMEOUT_SECONDS="${HOLMAS_IL2CPP_SMOKE_BUILD_TIMEOUT_SECONDS:-2400}"
PLAYER_TIMEOUT_SECONDS="${HOLMAS_IL2CPP_SMOKE_PLAYER_TIMEOUT_SECONDS:-90}"

log "使用编辑器：${EDITOR_PATH}"
log "准备临时工程：${TEMP_PROJECT_DIR}"

rsync -a --delete \
    --exclude '.git' \
    --exclude 'Library' \
    --exclude 'Temp' \
    --exclude 'Logs' \
    --exclude 'obj' \
    --exclude 'HybridCLRData' \
    --exclude 'Assets/StreamingAssets' \
    --exclude '.DS_Store' \
    "${REPO_ROOT}/" "${TEMP_PROJECT_DIR}/"

mkdir -p "${TEMP_PROJECT_DIR}/Library"
python3 - "${REQUEST_PATH}" "${PLAYER_OUTPUT_DIR}" "${BUILD_TARGET}" <<'PY'
import json
import sys
from pathlib import Path

Path(sys.argv[1]).write_text(json.dumps({
    "OutputDirectory": sys.argv[2],
    "BuildTargetName": sys.argv[3],
}, ensure_ascii=False, indent=2), encoding="utf-8")
PY

run_build() {
    perl -e '
        my $timeout = shift @ARGV;
        my $pid = fork();
        die "fork failed: $!\n" unless defined $pid;
        if ($pid == 0) {
            exec @ARGV or die "exec failed: $!\n";
        }
        $SIG{ALRM} = sub {
            kill "TERM", $pid;
            sleep 2;
            kill "KILL", $pid;
            exit 124;
        };
        alarm $timeout;
        waitpid($pid, 0);
        exit($? >> 8);
    ' "${BATCHMODE_TIMEOUT_SECONDS}" \
        env -i \
        HOME="${HOME}" \
        PATH="/usr/bin:/bin:/usr/sbin:/sbin" \
        LANG="en_US.UTF-8" \
        LC_ALL="en_US.UTF-8" \
        "${EDITOR_PATH}" \
        -batchmode \
        -quit \
        -projectPath "${TEMP_PROJECT_DIR}" \
        -executeMethod "${SMOKE_METHOD}" \
        -logFile "${BUILD_LOG}"
}

log "开始构建 IL2CPP player smoke..."
if ! run_build; then
    error "IL2CPP player smoke 构建失败，请检查日志：${BUILD_LOG}"
    exit 1
fi

PLAYER_PATH="$(python3 - "${RESULT_PATH}" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
if not path.exists():
    raise SystemExit(f"smoke result missing: {path}")

data = json.loads(path.read_text(encoding="utf-8"))
if not data.get("Success"):
    raise SystemExit(data.get("FailureReason") or "IL2CPP player smoke build failed")
print(data.get("PlayerPath", ""))
PY
)"

if [[ -z "${PLAYER_PATH}" ]]; then
    error "IL2CPP player smoke 构建未返回 player 路径。"
    exit 1
fi

log "IL2CPP player smoke 构建通过：${PLAYER_PATH}"

if [[ "${BUILD_ONLY}" -eq 1 ]]; then
    log "已按 --build-only 跳过 player 启动。"
    exit 0
fi

resolve_player_executable() {
    local player_path="$1"
    if [[ -d "${player_path}" && "${player_path}" == *.app ]]; then
        find "${player_path}/Contents/MacOS" -maxdepth 1 -type f -perm +111 | head -n 1
        return 0
    fi

    if [[ -x "${player_path}" ]]; then
        printf '%s\n' "${player_path}"
        return 0
    fi

    return 1
}

PLAYER_EXECUTABLE="$(resolve_player_executable "${PLAYER_PATH}")"
if [[ -z "${PLAYER_EXECUTABLE}" || ! -x "${PLAYER_EXECUTABLE}" ]]; then
    error "找不到可执行 player：${PLAYER_PATH}"
    exit 1
fi

log "开始启动 player smoke：${PLAYER_EXECUTABLE}"
rm -f "${PLAYER_LOG}"
"${PLAYER_EXECUTABLE}" -batchmode -nographics -logFile "${PLAYER_LOG}" &
PLAYER_PID=$!

deadline=$((SECONDS + PLAYER_TIMEOUT_SECONDS))
player_success=0
while [[ ${SECONDS} -lt ${deadline} ]]; do
    if [[ -f "${PLAYER_LOG}" ]] && rg -q "HybridClrLoader: HybridCLR热更代码加载完成|GameBootstrap: 初始化完成" "${PLAYER_LOG}"; then
        player_success=1
        break
    fi

    if ! kill -0 "${PLAYER_PID}" 2>/dev/null; then
        break
    fi

    sleep 2
done

if kill -0 "${PLAYER_PID}" 2>/dev/null; then
    kill "${PLAYER_PID}" 2>/dev/null || true
    sleep 1
    kill -9 "${PLAYER_PID}" 2>/dev/null || true
fi

if [[ "${player_success}" -ne 1 ]]; then
    error "player smoke 未在 ${PLAYER_TIMEOUT_SECONDS}s 内看到启动成功标记，请检查日志：${PLAYER_LOG}"
    exit 1
fi

log "IL2CPP player smoke 启动验证通过，日志：${PLAYER_LOG}"
