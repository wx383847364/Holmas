#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_VERSION_FILE="${REPO_ROOT}/ProjectSettings/ProjectVersion.txt"

EDITOR_PATH="${TUANJIE_EDITOR_PATH:-}"
TASK_DIR=""
TEMP_PROJECT_DIR=""
REPORT_PATH=""
KEEP_TEMP_ON_SUCCESS=0
LOG_PREFIX="ui_prefab_task_auto_analysis_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="/tmp"
BATCH_METHOD="UiPrefabGenerator.Editor.Analysis.UiPrefabGeneratorAnalysisBatch.RunTaskAutoAnalysisBatch"
SUCCESS_PATTERN="UiPrefabGenerator task auto analysis finished\\."
TEMP_CREATED_BY_SCRIPT=0

usage() {
    cat <<'EOF'
用法：
  tools/ui_prefab_generator/run_task_auto_analysis.sh --task-dir Assets/UiPrefabGeneratorData/Tasks/<task_id>
                                                       [--editor /path/to/Tuanjie]
                                                       [--temp-dir /private/tmp/xxx]
                                                       [--report-path /tmp/ui_prefab_task_auto_analysis_report.json]
                                                       [--log-prefix ui_prefab_task_auto_analysis_xxx]
                                                       [--log-dir /tmp]
                                                       [--keep-temp-on-success]

说明：
  - 自动复制当前项目到临时目录执行 batchmode 分析
  - 读取指定 task 目录下的 request.json
  - 在临时工程内生成 core artifacts、review-only evidence/gating artifacts 和 structured preview artifacts
  - 成功后把这些产物同步回原 task 目录
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

normalize_task_dir() {
    if [[ -z "${TASK_DIR}" ]]; then
        error "必须指定 --task-dir。"
        usage
        exit 1
    fi

    case "${TASK_DIR}" in
        /*)
            case "${TASK_DIR}" in
                "${REPO_ROOT}/"*)
                    TASK_DIR="${TASK_DIR#${REPO_ROOT}/}"
                    ;;
                *)
                    error "task-dir 必须位于仓库内，当前值: ${TASK_DIR}"
                    exit 1
                    ;;
            esac
            ;;
    esac

    if [[ "${TASK_DIR}" != Assets/* ]]; then
        error "task-dir 必须是 Assets 开头的仓库相对路径，当前值: ${TASK_DIR}"
        exit 1
    fi
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
        --task-dir)
            TASK_DIR="${2:-}"
            shift 2
            ;;
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

normalize_task_dir
detect_editor_path
mkdir -p "${LOG_DIR}"

if [[ -z "${TEMP_PROJECT_DIR}" ]]; then
    TEMP_PROJECT_DIR="$(mktemp -d "/private/tmp/ui_prefab_task_auto_analysis_XXXXXX")"
    TEMP_CREATED_BY_SCRIPT=1
else
    mkdir -p "${TEMP_PROJECT_DIR}"
fi

if [[ -z "${REPORT_PATH}" ]]; then
    REPORT_PATH="${LOG_DIR}/${LOG_PREFIX}_report.json"
fi

PIPELINE_LOG="${LOG_DIR}/${LOG_PREFIX}.log"
TEMP_TASK_DIR="${TEMP_PROJECT_DIR}/${TASK_DIR}"
SOURCE_TASK_DIR="${REPO_ROOT}/${TASK_DIR}"

if [[ ! -f "${SOURCE_TASK_DIR}/request.json" ]]; then
    error "未找到 request.json：${SOURCE_TASK_DIR}/request.json"
    exit 1
fi

log "使用编辑器：${EDITOR_PATH}"
log "任务目录：${SOURCE_TASK_DIR}"
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
        UI_PREFAB_TASK_AUTO_ANALYSIS_TASK_DIR="${TASK_DIR}" \
        UI_PREFAB_TASK_AUTO_ANALYSIS_REPORT_PATH="${REPORT_PATH}" \
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

sync_analysis_outputs() {
    local source_dir="${TEMP_TASK_DIR}"
    local target_dir="${SOURCE_TASK_DIR}"
    local files=(
        "visual_understanding.json"
        "visual_review_report.json"
        "design_packet.json"
        "design_packet_intake_assessment.json"
        "gating_report.json"
        "ui_prefab_spec.json"
        "resource_match_report.json"
        "preview_render_plan.json"
        "preview_render.png"
        "preview_diff_report.json"
        "analysis_result.json"
        "analysis_summary.md"
    )

    for file in "${files[@]}"; do
        local source_file="${source_dir}/${file}"
        local target_file="${target_dir}/${file}"
        if [[ ! -f "${source_file}" ]]; then
            error "临时工程缺少输出文件：${source_file}"
            exit 1
        fi
        cp "${source_file}" "${target_file}"
    done
}

log "开始执行 task auto analysis..."
run_batchmode "${BATCH_METHOD}" "${PIPELINE_LOG}"
assert_log_contains "${SUCCESS_PATTERN}" "${PIPELINE_LOG}"
assert_log_contains "Exiting batchmode successfully now!" "${PIPELINE_LOG}"

if [[ ! -f "${REPORT_PATH}" ]]; then
    error "未找到 auto analysis report：${REPORT_PATH}"
    exit 1
fi

if ! file_contains_pattern '"Success": true' "${REPORT_PATH}"; then
    error "auto analysis report 未通过成功断言：${REPORT_PATH}"
    exit 1
fi

sync_analysis_outputs

log "task auto analysis 通过，日志：${PIPELINE_LOG}"
log "task auto analysis report：${REPORT_PATH}"
