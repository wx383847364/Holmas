#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-$(pwd)}"
EXIT_CODE=0

if ! command -v rg >/dev/null 2>&1; then
  echo "[ERROR] rg is required for check_boundary.sh"
  exit 1
fi

echo "[info] checking boundary rules under: $ROOT"

check() {
  local label="$1"
  local pattern="$2"
  shift 2
  local matches
  matches="$(rg -n "$pattern" "$@" || true)"
  if [[ -n "$matches" ]]; then
    echo
    echo "[warn] $label"
    echo "$matches"
    EXIT_CODE=1
  fi
}

check "Runtime code should not use Resources.Load for formal features" \
  'Resources\.Load' \
  "$ROOT/Assets"

check "Runtime code should not use UnityEditor outside editor folders" \
  'using UnityEditor|UnityEditor\.' \
  "$ROOT/Assets" \
  -g '!**/Editor/**' \
  -g '!**/*.meta'

check "App.Shared should not depend on MonoBehaviour" \
  'MonoBehaviour' \
  "$ROOT/Assets/Scripts/App.Shared"

check "App.Shared should not depend on ScriptableObject" \
  'ScriptableObject' \
  "$ROOT/Assets/Scripts/App.Shared"

check "UI code may contain business rules; review these keywords manually" \
  'reward|Reward|Generate|generate|TaskInstance|task generation|levelRewardFactor|catCount' \
  "$ROOT/Assets" \
  -g '*UI*.cs' \
  -g '*View*.cs' \
  -g '*Presenter*.cs' \
  -g '*Controller*.cs'

check "HotUpdate should not directly depend on App.AOT concrete namespaces" \
  'using App\.AOT|App\.AOT\.' \
  "$ROOT/Assets/HotUpdateContent/Script/App.HotUpdate"

echo
if [[ "$EXIT_CODE" -eq 0 ]]; then
  echo "[ok] no obvious boundary violations found"
else
  echo "[done] review warnings above"
fi

exit "$EXIT_CODE"
