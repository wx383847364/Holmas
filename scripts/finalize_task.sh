#!/usr/bin/env bash

set -euo pipefail

# 统一的任务收尾脚本。
# 目标是把“更新迭代记录 + 同步索引 + 暂存文档改动”收敛成一个固定动作，
# 避免每次完成任务后只记得改代码，却忘了把本轮结果写回文档体系。

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOC_ROOT="${REPO_ROOT}/doc"

TARGET_MODE="latest"
TARGET_FILE=""
SUMMARY=""
DONE_ITEMS=()
RISK_ITEMS=()
NEXT_ITEMS=()
SKIP_TEMP_CLEANUP=0
FORCE_LOG=0

usage() {
    cat <<'EOF'
用法：
  scripts/finalize_task.sh --summary "本轮摘要" [--done "完成项"] [--risk "风险项"] [--next "下一步"]
  scripts/finalize_task.sh --file "doc/迭代记录/迭代记录_YYYYMMDD_001.md" --summary "本轮摘要"
  scripts/finalize_task.sh --summary "本轮摘要" --skip-temp-cleanup
  scripts/finalize_task.sh --summary "更新提交说明" --force-log

说明：
  - 默认写入最新一轮迭代记录
  - 会先判断这次是否属于“值得进入项目文档”的任务；纯事务性协助会自动跳过
  - 会自动同步主文档索引和迭代记录索引
  - 如果当前目录在 Git 仓库中，会自动暂存 doc/ 下被本次更新影响的文件
  - 默认会在收尾末尾自动尝试清理 /tmp 或 /private/tmp 下的 Holmas 临时验证工程
  - 如果 Hub 正在运行或清理失败，只会给出提示，不会中断文档收尾
  - 如需强制写入，可显式传 --force-log
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --latest)
            TARGET_MODE="latest"
            shift
            ;;
        --file)
            TARGET_MODE="file"
            TARGET_FILE="${2:-}"
            shift 2
            ;;
        --summary)
            SUMMARY="${2:-}"
            shift 2
            ;;
        --done)
            DONE_ITEMS+=("${2:-}")
            shift 2
            ;;
        --risk)
            RISK_ITEMS+=("${2:-}")
            shift 2
            ;;
        --next)
            NEXT_ITEMS+=("${2:-}")
            shift 2
            ;;
        --skip-temp-cleanup)
            SKIP_TEMP_CLEANUP=1
            shift
            ;;
        --force-log)
            FORCE_LOG=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "[error] 未知参数: $1" >&2
            usage
            exit 1
            ;;
    esac
done

if [[ -z "${SUMMARY}" ]]; then
    echo "[error] --summary 是必填项，用来记录这轮任务完成了什么。" >&2
    usage
    exit 1
fi

if [[ "${TARGET_MODE}" == "file" && -z "${TARGET_FILE}" ]]; then
    echo "[error] 使用 --file 时必须提供目标迭代记录文件。" >&2
    exit 1
fi

should_skip_doc_logging() {
    if [[ "${FORCE_LOG}" -eq 1 ]]; then
        return 1
    fi

    local combined_text
    combined_text="${SUMMARY}"$'\n'
    if ((${#DONE_ITEMS[@]} > 0)); then
        combined_text+=$(printf '%s\n' "${DONE_ITEMS[@]}")
    fi
    if ((${#RISK_ITEMS[@]} > 0)); then
        combined_text+=$(printf '%s\n' "${RISK_ITEMS[@]}")
    fi
    if ((${#NEXT_ITEMS[@]} > 0)); then
        combined_text+=$(printf '%s\n' "${NEXT_ITEMS[@]}")
    fi

    local trivial_pattern='提交日志|提交标题|commit message|commit title|pr 标题|pr 描述|写日志和标题|写一下标题和内容|润色提交说明|总结一下修改了什么|快捷键|剪切|复制|翻译|改写文案'
    local substantive_pattern='新增|接入|实现|修复|重构|迁移|导入|配置表|csv|资源|地图|猫表|任务表|等级表|测试|验证|脚本|流程|规则|自动维护|项目总览|迭代记录|清理临时|hotupdate|yooassets'

    if printf '%s' "${combined_text}" | grep -Eqi "${trivial_pattern}"; then
        if ! printf '%s' "${combined_text}" | grep -Eqi "${substantive_pattern}"; then
            return 0
        fi
    fi

    return 1
}

if should_skip_doc_logging; then
    echo "[skip] 当前任务被判定为事务性协助，无需写入项目总览或迭代记录。"
    echo "[skip] 如需强制记录，请重新执行并追加 --force-log。"
    exit 0
fi

APPEND_ARGS=(
    python3
    "${REPO_ROOT}/scripts/update_project_docs.py"
    --doc-root "${DOC_ROOT}"
    append-iteration
)

if [[ "${TARGET_MODE}" == "latest" ]]; then
    APPEND_ARGS+=(--latest)
else
    APPEND_ARGS+=(--file "${TARGET_FILE}")
fi

APPEND_ARGS+=(--summary "${SUMMARY}")

if ((${#DONE_ITEMS[@]} > 0)); then
    for item in "${DONE_ITEMS[@]}"; do
        APPEND_ARGS+=(--done "${item}")
    done
fi

if ((${#RISK_ITEMS[@]} > 0)); then
    for item in "${RISK_ITEMS[@]}"; do
        APPEND_ARGS+=(--risk "${item}")
    done
fi

if ((${#NEXT_ITEMS[@]} > 0)); then
    for item in "${NEXT_ITEMS[@]}"; do
        APPEND_ARGS+=(--next "${item}")
    done
fi

echo "[info] 追加迭代记录..."
"${APPEND_ARGS[@]}"

echo "[info] 同步文档索引..."
python3 "${REPO_ROOT}/scripts/update_project_docs.py" --doc-root "${DOC_ROOT}" sync

# 如果当前目录在 Git 仓库中，顺手把文档改动暂存起来。
# 这样做可以让“任务收尾”和“提交前文档齐全”尽量靠近。
if git -C "${REPO_ROOT}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "[info] 暂存文档改动..."
    git -C "${REPO_ROOT}" add doc
fi

if [[ "${SKIP_TEMP_CLEANUP}" -ne 1 ]]; then
    CLEAN_SCRIPT="${REPO_ROOT}/scripts/clean_hub_temp_projects.sh"
    if [[ -f "${CLEAN_SCRIPT}" ]]; then
        echo "[info] 自动清理历史临时验证工程..."
        if ! bash "${CLEAN_SCRIPT}"; then
            echo "[warn] 临时验证工程自动清理未完成，请按提示决定是否手动执行 scripts/clean_hub_temp_projects.sh。" >&2
        fi
    fi
fi

echo "[ok] 文档维护流程已执行完成。"
