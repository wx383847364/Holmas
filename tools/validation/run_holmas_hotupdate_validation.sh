#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_VERSION_FILE="${REPO_ROOT}/ProjectSettings/ProjectVersion.txt"

EDITOR_PATH="${TUANJIE_EDITOR_PATH:-}"
TEMP_PROJECT_DIR=""
KEEP_TEMP_ON_SUCCESS=0
SKIP_BOUNDARY=0
SKIP_PLAYMODE=0
LOG_PREFIX="holmas_hotupdate_validation_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="/tmp"
PACKAGE_METHOD="HolmasHotUpdatePackageValidation.RunHotUpdatePackageValidation"
PROBE_METHOD="HolmasPlayModeVerificationProbe.RunBatchModeRequestedProbe"
TEMP_CREATED_BY_SCRIPT=0

usage() {
    cat <<'EOF'
用法：
  tools/validation/run_holmas_hotupdate_validation.sh [--editor /path/to/Tuanjie] [--temp-dir /private/tmp/xxx]
                                                     [--log-prefix holmas_xxx] [--log-dir /tmp]
                                                     [--skip-boundary] [--skip-playmode]
                                                     [--keep-temp-on-success]

说明：
  - 热更专项验证，不改变 run_holmas_validation.sh / check_boundary.sh 语义
  - 顺序执行：边界检查 -> HotUpdate DLL/metadata 准备 -> YooAssets 本地包构建与加载校验 -> BootstrapScene PlayMode probe
  - 资源包和 DLL/metadata 产物只写入临时工程；失败时保留临时工程和日志
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
        --skip-boundary)
            SKIP_BOUNDARY=1
            shift
            ;;
        --skip-playmode)
            SKIP_PLAYMODE=1
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

if [[ "${SKIP_BOUNDARY}" -eq 0 ]]; then
    log "开始执行边界检查..."
    bash "${SCRIPT_DIR}/check_boundary.sh"
    log "边界检查通过。"
else
    log "已跳过边界检查。"
fi

if [[ -z "${TEMP_PROJECT_DIR}" ]]; then
    TEMP_PROJECT_DIR="$(mktemp -d "/private/tmp/holmas_hotupdate_validation_XXXXXX")"
    TEMP_CREATED_BY_SCRIPT=1
else
    mkdir -p "${TEMP_PROJECT_DIR}"
fi

PACKAGE_LOG="${LOG_DIR}/${LOG_PREFIX}_yooassets_package.log"
PROBE_LOG="${LOG_DIR}/${LOG_PREFIX}_playmode_probe.log"
PROBE_REQUEST_PATH="${TEMP_PROJECT_DIR}/Library/holmas_playmode_probe_request.json"
PROBE_OUTPUT_DIR="${TEMP_PROJECT_DIR}/Library/holmas_playmode_probe"
BATCHMODE_TIMEOUT_SECONDS="${HOLMAS_HOTUPDATE_VALIDATION_TIMEOUT_SECONDS:-1200}"

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

run_batchmode_quit() {
    local method="$1"
    local log_file="$2"

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
        -executeMethod "${method}" \
        -logFile "${log_file}"
}

run_batchmode_noquit() {
    local method="$1"
    local log_file="$2"

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
        -projectPath "${TEMP_PROJECT_DIR}" \
        -executeMethod "${method}" \
        -logFile "${log_file}"
}

assert_log_contains() {
    local pattern="$1"
    local log_file="$2"
    local failure_area="$3"
    if ! rg -q "${pattern}" "${log_file}"; then
        error "${failure_area} 验证失败：日志未找到预期标记 ${pattern}"
        error "请检查日志：${log_file}"
        exit 1
    fi
}

log "开始执行 YooAssets 本地热更包构建与加载验证..."
run_batchmode_noquit "${PACKAGE_METHOD}" "${PACKAGE_LOG}"
assert_log_contains "Holmas hotupdate package validation passed\\." "${PACKAGE_LOG}" "YooAssets包/DLL/metadata"
assert_log_contains "YooAssets local package load checks passed" "${PACKAGE_LOG}" "YooAssets包"
log "YooAssets 本地包与 HotUpdate DLL/metadata 验证通过，日志：${PACKAGE_LOG}"

if [[ "${SKIP_PLAYMODE}" -eq 0 ]]; then
    mkdir -p "${TEMP_PROJECT_DIR}/Library"
    python3 - "${PROBE_REQUEST_PATH}" "${PROBE_OUTPUT_DIR}" <<'PY'
import json
import sys
from pathlib import Path

Path(sys.argv[1]).write_text(json.dumps({"OutputDirectory": sys.argv[2]}, ensure_ascii=False, indent=2), encoding="utf-8")
PY

    log "开始执行 BootstrapScene PlayMode/batchmode 启动验证..."
    run_batchmode_noquit "${PROBE_METHOD}" "${PROBE_LOG}"

    RESULT_JSON="${PROBE_OUTPUT_DIR}/result.json"
    python3 - "${RESULT_JSON}" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
if not path.exists():
    raise SystemExit(f"入口初始化/Holmas bootstrap 验证失败：probe result missing: {path}")

data = json.loads(path.read_text(encoding="utf-8"))
if not data.get("Success"):
    raise SystemExit(data.get("FailureReason") or "入口初始化/Holmas bootstrap 验证失败")
PY

    assert_log_contains "Play Mode probe finished successfully" "${PROBE_LOG}" "入口初始化/Holmas bootstrap/UI"
    log "BootstrapScene PlayMode/batchmode 启动验证通过，日志：${PROBE_LOG}"
else
    log "已跳过 BootstrapScene PlayMode/batchmode 启动验证。"
fi

log "Holmas 热更专项验证全部通过。"
