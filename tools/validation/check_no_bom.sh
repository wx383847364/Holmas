#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
MODE="tracked"

usage() {
    cat <<'EOF'
Usage:
  tools/validation/check_no_bom.sh
  tools/validation/check_no_bom.sh --workspace

Checks files for a UTF-8 BOM (EF BB BF) at the beginning.

Modes:
  default      Scan git-tracked files.
  --workspace  Scan common text/source/generated-text extensions in the
               workspace, excluding transient Unity output directories.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --workspace)
            MODE="workspace"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "[error] Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

has_utf8_bom() {
    local path="$1"
    [[ -f "${path}" ]] || return 1
    [[ "$(head -c 3 "${path}" | xxd -p)" == "efbbbf" ]]
}

check_path() {
    local path="$1"
    if has_utf8_bom "${path}"; then
        printf '%s\n' "${path}"
        return 2
    fi

    return 0
}

scan_tracked() {
    local failed=0
    while IFS= read -r -d '' path; do
        if ! check_path "${path}"; then
            failed=1
        fi
    done < <(git ls-files -z)
    return "${failed}"
}

scan_workspace() {
    local failed=0
    while IFS= read -r -d '' path; do
        if ! check_path "${path}"; then
            failed=1
        fi
    done < <(
        find . \
            -path './.git' -prune -o \
            -path './Library' -prune -o \
            -path './Temp' -prune -o \
            -path './Logs' -prune -o \
            -path './Builds' -prune -o \
            -path './obj' -prune -o \
            -type f \( \
                -name '*.asset' -o -name '*.c' -o -name '*.cginc' -o -name '*.config' -o \
                -name '*.cpp' -o -name '*.cs' -o -name '*.csproj' -o -name '*.csv' -o \
                -name '*.h' -o -name '*.hpp' -o -name '*.js' -o -name '*.jslib' -o \
                -name '*.json' -o -name '*.md' -o -name '*.meta' -o -name '*.py' -o \
                -name '*.shader' -o -name '*.sh' -o -name '*.sln' -o -name '*.txt' -o \
                -name '*.uss' -o -name '*.uxml' -o -name '*.xml' -o -name '*.yaml' -o \
                -name '*.yml' \
            \) -print0
    )
    return "${failed}"
}

cd "${REPO_ROOT}"

if [[ "${MODE}" == "workspace" ]]; then
    echo "[info] Scanning workspace text/source/generated-text files for UTF-8 BOM..."
    if scan_workspace; then
        echo "[ok] No UTF-8 BOM found in workspace scan."
        exit 0
    fi
else
    echo "[info] Scanning git-tracked files for UTF-8 BOM..."
    if scan_tracked; then
        echo "[ok] No UTF-8 BOM found in tracked files."
        exit 0
    fi
fi

echo "[error] UTF-8 BOM found. Remove EF BB BF from the files listed above." >&2
exit 2
