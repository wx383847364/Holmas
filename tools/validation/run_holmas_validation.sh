#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_VERSION_FILE="${REPO_ROOT}/ProjectSettings/ProjectVersion.txt"

EDITOR_PATH="${TUANJIE_EDITOR_PATH:-}"
TEMP_PROJECT_DIR=""
KEEP_TEMP_ON_SUCCESS=0
SKIP_SMOKE=0
LOG_PREFIX="holmas_validation_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="/tmp"
EDITMODE_METHOD="HolmasEditModeTestRunner.RunHolmasEditModeTests"
SMOKE_METHOD="HolmasCoreValidationMenu.RunCoreLogicSmokeTest"
EDITMODE_SUCCESS_PATTERN="Holmas EditMode tests finished\\."
SMOKE_SUCCESS_PATTERN="Holmas smoke test passed\\."
TEMP_CREATED_BY_SCRIPT=0

usage() {
    cat <<'EOF'
用法：
  tools/validation/run_holmas_validation.sh [--editor /path/to/Tuanjie] [--temp-dir /private/tmp/xxx]
                                   [--log-prefix holmas_xxx] [--log-dir /tmp]
                                   [--skip-smoke] [--keep-temp-on-success]

说明：
  - 自动复制当前项目到临时目录执行 batchmode 验证，避免和正在打开的主工程抢项目锁
  - 默认先跑 Holmas EditMode 测试，再跑 core logic smoke test
  - 验证全部通过后，会自动删除本次创建的临时工程
  - 任一步失败时，会保留临时工程和日志，方便回头排查
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
        --log-prefix)
            LOG_PREFIX="${2:-}"
            shift 2
            ;;
        --log-dir)
            LOG_DIR="${2:-}"
            shift 2
            ;;
        --skip-smoke)
            SKIP_SMOKE=1
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
    TEMP_PROJECT_DIR="$(mktemp -d "/private/tmp/holmas_validation_XXXXXX")"
    TEMP_CREATED_BY_SCRIPT=1
else
    mkdir -p "${TEMP_PROJECT_DIR}"
fi

EDITMODE_LOG="${LOG_DIR}/${LOG_PREFIX}_editmode.log"
SMOKE_LOG="${LOG_DIR}/${LOG_PREFIX}_smoke.log"

log "使用编辑器：${EDITOR_PATH}"
log "准备临时工程：${TEMP_PROJECT_DIR}"

rsync -a --delete \
    --exclude '.git' \
    --exclude 'Library' \
    --exclude 'Temp' \
    --exclude 'Logs' \
    --exclude 'obj' \
    --exclude '.DS_Store' \
    "${REPO_ROOT}/" "${TEMP_PROJECT_DIR}/"

run_batchmode() {
    local method="$1"
    local log_file="$2"

    env -i \
        HOME="${HOME}" \
        PATH="/usr/bin:/bin:/usr/sbin:/sbin" \
        LANG="en_US.UTF-8" \
        LC_ALL="en_US.UTF-8" \
        "${EDITOR_PATH}" \
        -batchmode \
        -quit \
        -projectPath "${TEMP_PROJECT_DIR}" \
        -executeMethod "${method}" \
        -logFile "${log_file}"
}

assert_log_contains() {
    local pattern="$1"
    local log_file="$2"
    if ! rg -q "${pattern}" "${log_file}"; then
        error "日志未找到预期成功标记：${pattern}"
        error "请检查日志：${log_file}"
        exit 1
    fi
}

log "开始执行 EditMode 验证..."
run_batchmode "${EDITMODE_METHOD}" "${EDITMODE_LOG}"
assert_log_contains "${EDITMODE_SUCCESS_PATTERN}" "${EDITMODE_LOG}"
assert_log_contains "Exiting batchmode successfully now!" "${EDITMODE_LOG}"
log "EditMode 验证通过，日志：${EDITMODE_LOG}"

if [[ "${SKIP_SMOKE}" -eq 0 ]]; then
    log "开始执行 smoke 验证..."
    run_batchmode "${SMOKE_METHOD}" "${SMOKE_LOG}"
    assert_log_contains "${SMOKE_SUCCESS_PATTERN}" "${SMOKE_LOG}"
    assert_log_contains "Exiting batchmode successfully now!" "${SMOKE_LOG}"
    log "Smoke 验证通过，日志：${SMOKE_LOG}"
else
    log "已跳过 smoke 验证。"
fi

log "Holmas 验证全部通过。"
