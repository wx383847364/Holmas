#!/usr/bin/env bash

set -euo pipefail

# 统一的任务收尾脚本。
# 目标是把“更新迭代记录 + 同步索引 + 暂存文档改动”收敛成一个固定动作，
# 避免每次完成任务后只记得改代码，却忘了把本轮结果写回文档体系。

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOC_ROOT="${REPO_ROOT}/doc"
SKILLS_ROOT="${DOC_ROOT}/长期主文档/协作与执行/skills"

TARGET_MODE="latest"
TARGET_FILE=""
SUMMARY=""
DONE_ITEMS=()
RISK_ITEMS=()
NEXT_ITEMS=()
AGENT_STATUS_ITEMS=()
SESSION_MODE="auto"
ITERATION_MODE="auto"
AGENT6_REVIEW="pending"
CONTEXT_COMPRESSED=0
NEXT_SESSION_TITLE=""
NEXT_SESSION_GOAL=""
NEXT_SESSION_DOCS=()
SKIP_TEMP_CLEANUP=0
FORCE_LOG=0
SKIP_DOC_LOGGING=0

usage() {
    cat <<'EOF'
用法：
  scripts/finalize_task.sh --summary "本轮摘要" [--done "完成项"] [--risk "风险项"] [--next "下一步"]
  scripts/finalize_task.sh --summary "本轮摘要" --agent-status "Agent 5：已启动并完成边界验证"
  scripts/finalize_task.sh --summary "本轮摘要" --agent6-review passed --next "下一阶段目标"
  scripts/finalize_task.sh --summary "本轮摘要" --context-compressed
  scripts/finalize_task.sh --file "doc/迭代记录/迭代记录_YYYYMMDD_001.md" --summary "本轮摘要"
  scripts/finalize_task.sh --summary "本轮摘要" --skip-temp-cleanup
  scripts/finalize_task.sh --summary "更新提交说明" --force-log

说明：
  - 默认写入最新一轮迭代记录
  - 会先判断这次是否属于“值得进入项目文档”的任务；纯事务性协助会自动跳过
  - `--agent-status` 只做显式透传；只有本轮确实改变某个 Agent 的分工状态时才传
  - `--agent6-review` 用来声明 Agent 6 当前审查状态：passed / passed-with-suggestions / failed / pending / not-required
  - `--context-compressed` 用来声明本会话已经出现过自动压缩背景信息，或你已经明确感知到上下文质量下降
  - `--session-mode` 和 `--iteration-mode` 分别控制“会话建议”和“迭代记录建议”，两者彼此独立
  - 默认会在收尾末尾输出“会话建议 + 迭代记录建议 + 启动卡”，即使这轮被判定为事务性协助也会给出建议
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
        --agent-status)
            AGENT_STATUS_ITEMS+=("${2:-}")
            shift 2
            ;;
        --session-mode)
            SESSION_MODE="${2:-}"
            shift 2
            ;;
        --iteration-mode)
            ITERATION_MODE="${2:-}"
            shift 2
            ;;
        --agent6-review)
            AGENT6_REVIEW="${2:-}"
            shift 2
            ;;
        --context-compressed)
            CONTEXT_COMPRESSED=1
            shift
            ;;
        --next-session-title)
            NEXT_SESSION_TITLE="${2:-}"
            shift 2
            ;;
        --next-session-goal)
            NEXT_SESSION_GOAL="${2:-}"
            shift 2
            ;;
        --next-session-doc)
            NEXT_SESSION_DOCS+=("${2:-}")
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

has_skill_source_changes() {
    if ! git -C "${REPO_ROOT}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        return 1
    fi

    if [[ ! -d "${SKILLS_ROOT}" ]]; then
        return 1
    fi

    if [[ -n "$(git -C "${REPO_ROOT}" status --porcelain --untracked-files=all -- "${SKILLS_ROOT}")" ]]; then
        return 0
    fi

    return 1
}

if should_skip_doc_logging; then
    SKIP_DOC_LOGGING=1
    echo "[skip] 当前任务被判定为事务性协助，无需写入项目总览或迭代记录。"
    echo "[skip] 如需强制记录，请重新执行并追加 --force-log。"
fi
if [[ "${SKIP_DOC_LOGGING}" -ne 1 ]]; then
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

    if ((${#AGENT_STATUS_ITEMS[@]} > 0)); then
        for item in "${AGENT_STATUS_ITEMS[@]}"; do
            APPEND_ARGS+=(--agent-status "${item}")
        done
    fi

    echo "[info] 追加迭代记录..."
    "${APPEND_ARGS[@]}"

    echo "[info] 同步文档索引..."
    python3 "${REPO_ROOT}/scripts/update_project_docs.py" --doc-root "${DOC_ROOT}" sync

    if has_skill_source_changes; then
        SKILL_SYNC_SCRIPT="${REPO_ROOT}/scripts/sync_codex_skills.sh"
        if [[ -f "${SKILL_SYNC_SCRIPT}" ]]; then
            echo "[info] 检测到项目 skill 真源有改动，自动同步到 ~/.codex/skills ..."
            if ! bash "${SKILL_SYNC_SCRIPT}"; then
                echo "[warn] 项目 skill 自动同步未完成，请按提示决定是否手动执行 scripts/sync_codex_skills.sh。" >&2
            fi
        else
            echo "[warn] 检测到项目 skill 真源有改动，但未找到 scripts/sync_codex_skills.sh，已跳过自动同步。" >&2
        fi
    fi

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
fi

HANDOFF_ARGS=(
    python3
    "${REPO_ROOT}/scripts/update_project_docs.py"
    --doc-root "${DOC_ROOT}"
    suggest-handoff
    --summary "${SUMMARY}"
    --agent6-review "${AGENT6_REVIEW}"
    --session-mode "${SESSION_MODE}"
    --iteration-mode "${ITERATION_MODE}"
)

if ((${#DONE_ITEMS[@]} > 0)); then
    for item in "${DONE_ITEMS[@]}"; do
        HANDOFF_ARGS+=(--done "${item}")
    done
fi

if ((${#RISK_ITEMS[@]} > 0)); then
    for item in "${RISK_ITEMS[@]}"; do
        HANDOFF_ARGS+=(--risk "${item}")
    done
fi

if ((${#NEXT_ITEMS[@]} > 0)); then
    for item in "${NEXT_ITEMS[@]}"; do
        HANDOFF_ARGS+=(--next "${item}")
    done
fi

if [[ -n "${NEXT_SESSION_TITLE}" ]]; then
    HANDOFF_ARGS+=(--next-session-title "${NEXT_SESSION_TITLE}")
fi

if [[ -n "${NEXT_SESSION_GOAL}" ]]; then
    HANDOFF_ARGS+=(--next-session-goal "${NEXT_SESSION_GOAL}")
fi

if ((${#NEXT_SESSION_DOCS[@]} > 0)); then
    for item in "${NEXT_SESSION_DOCS[@]}"; do
        HANDOFF_ARGS+=(--next-session-doc "${item}")
    done
fi

if [[ "${SKIP_DOC_LOGGING}" -eq 1 ]]; then
    HANDOFF_ARGS+=(--doc-log-skipped)
fi

if [[ "${CONTEXT_COMPRESSED}" -eq 1 ]]; then
    HANDOFF_ARGS+=(--context-compressed)
fi

echo
echo "[info] 会话衔接建议..."
"${HANDOFF_ARGS[@]}"
