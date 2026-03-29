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

usage() {
    cat <<'EOF'
用法：
  scripts/finalize_task.sh --summary "本轮摘要" [--done "完成项"] [--risk "风险项"] [--next "下一步"]
  scripts/finalize_task.sh --file "doc/迭代记录/迭代记录_YYYYMMDD_001.md" --summary "本轮摘要"

说明：
  - 默认写入最新一轮迭代记录
  - 会自动同步主文档索引和迭代记录索引
  - 如果当前目录在 Git 仓库中，会自动暂存 doc/ 下被本次更新影响的文件
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

echo "[ok] 文档维护流程已执行完成。"
