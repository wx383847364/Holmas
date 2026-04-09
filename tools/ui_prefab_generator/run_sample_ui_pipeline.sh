#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_VERSION_FILE="${REPO_ROOT}/ProjectSettings/ProjectVersion.txt"

EDITOR_PATH="${TUANJIE_EDITOR_PATH:-}"
TEMP_PROJECT_DIR=""
REPORT_PATH=""
KEEP_TEMP_ON_SUCCESS=0
LOG_PREFIX="ui_prefab_sample_pipeline_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="/tmp"
PIPELINE_METHOD="UiPrefabGenerator.Editor.Generation.SampleUiPipelineBatch.RunHolmasSamplePipelineBatch"
PIPELINE_SUCCESS_PATTERN="UiPrefabGenerator sample pipeline finished\\."
TEMP_CREATED_BY_SCRIPT=0

usage() {
    cat <<'EOF'
用法：
  tools/ui_prefab_generator/run_sample_ui_pipeline.sh [--editor /path/to/Tuanjie]
                                                        [--temp-dir /private/tmp/xxx]
                                                        [--report-path /tmp/ui_prefab_sample_pipeline_report.json]
                                                        [--log-prefix ui_prefab_sample_pipeline_xxx]
                                                        [--log-dir /tmp]
                                                        [--keep-temp-on-success]

说明：
  - 自动复制当前项目到临时目录执行 sample UI pipeline，避免和主工程抢项目锁
  - 固定跑 Holmas sample DesignPacket -> spec -> prefab 草稿 -> manifest -> adapter -> report
  - 通过后默认清理临时工程
EOF
}

log() {
    printf '[info] %s\n' "$1"
}

error() {
    printf '[error] %s\n' "$1" >&2
}

file_contains_pattern() {
    local pattern="$1"
    local file_path="$2"

    if command -v rg >/dev/null 2>&1; then
        rg -q -- "${pattern}" "${file_path}"
        return $?
    fi

    grep -E -q -- "${pattern}" "${file_path}"
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
        --report-path)
            REPORT_PATH="${2:-}"
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
    TEMP_PROJECT_DIR="$(mktemp -d "/private/tmp/ui_prefab_sample_pipeline_XXXXXX")"
    TEMP_CREATED_BY_SCRIPT=1
else
    mkdir -p "${TEMP_PROJECT_DIR}"
fi

if [[ -z "${REPORT_PATH}" ]]; then
    REPORT_PATH="${LOG_DIR}/${LOG_PREFIX}_report.json"
fi

PIPELINE_LOG="${LOG_DIR}/${LOG_PREFIX}.log"

log "使用编辑器：${EDITOR_PATH}"
log "准备临时工程：${TEMP_PROJECT_DIR}"
log "输出报告：${REPORT_PATH}"

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
        UI_PREFAB_SAMPLE_PIPELINE_REPORT_PATH="${REPORT_PATH}" \
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
    if ! file_contains_pattern "${pattern}" "${log_file}"; then
        error "日志未找到预期成功标记：${pattern}"
        error "请检查日志：${log_file}"
        exit 1
    fi
}

log "开始执行 sample UI pipeline..."
run_batchmode "${PIPELINE_METHOD}" "${PIPELINE_LOG}"
assert_log_contains "${PIPELINE_SUCCESS_PATTERN}" "${PIPELINE_LOG}"
assert_log_contains "Exiting batchmode successfully now!" "${PIPELINE_LOG}"

if [[ ! -f "${REPORT_PATH}" ]]; then
    error "未找到 sample pipeline 报告：${REPORT_PATH}"
    exit 1
fi

if ! file_contains_pattern '"Success": true' "${REPORT_PATH}"; then
    error "sample pipeline 报告未通过成功断言：${REPORT_PATH}"
    exit 1
fi

log "sample UI pipeline 通过，日志：${PIPELINE_LOG}"
log "sample UI pipeline 报告：${REPORT_PATH}"
