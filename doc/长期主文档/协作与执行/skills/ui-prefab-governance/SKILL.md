---
name: ui-prefab-governance
description: Use for this project when planning, revising, dispatching, implementing, or reviewing UI prefab generation, UI prefab binding, or UI business logic work. Enforces doc-zone ownership, subagent dispatch format, static UI binding rules, prefab visual preservation, asmdef and directory isolation, migration boundaries, and Holmas trial-project constraints.
---

# UI Prefab Governance

Use this skill for any task that touches:
- `doc/长期主文档/UI自动生成系统`
- UI 自动生成系统长期方案或执行派工单
- UI 业务逻辑实现、View/Controller 绑定、prefab 引用修复或页面状态接入
- subagent 任务拆分
- `Assets/Tools/UiPrefabGenerator` 的目录边界或 asmdef 分层
- Holmas 试点接入边界
- 旧稿跳转页和专区权威入口

## Goal

Keep the UI generation system isolated, migratable, and dispatchable.
Prefer one authoritative doc zone over scattered planning notes.

## Core Rules

- The authoritative long-term docs for this system live only in `doc/长期主文档/UI自动生成系统`.
- Legacy files under `doc/长期主文档/方案与数据` should only remain as jump pages once the content is migrated.
- System code belongs under `Assets/Tools/UiPrefabGenerator`.
- Holmas is the first trial project, not the system body.
- Holmas may only connect through adapter, profile, generated-result consumption, and trial samples.
- UI 运行时节点获取必须走静态绑定：`UiReferenceCollector`、generated bindings、manifest 是唯一入口。禁止在 View/Controller 里用 `Transform.Find`、`GameObject.Find`、递归 `GetComponentsInChildren` 补节点。
- 编写 UI 的 agent 必须先补齐 prefab/manifest/collector 静态绑定；审核 agent 必须把运行时查找节点视为阻断问题，除非该查找只存在于一次性 Editor authoring/migration 脚本且产物已固化到 prefab。
- 编写 UI 或接入 UI 业务逻辑时，默认保持原 prefab 的视觉参数不变，尤其是颜色、透明度、`CanvasGroup.alpha`、`Graphic.color`、材质颜色和状态层默认 tint。除非用户明确要求调整视觉表现，否则不得为了“顺手整理”“禁用态”“选中态”“遮罩态”等原因改动原 prefab 的颜色或透明度。
- Every execution-style subagent dispatch must explicitly include:
  - goal
  - allowed write scope
  - forbidden write scope
  - deliverables
  - acceptance checks

## Ownership Rules

- Use subdirectory-exclusive ownership for high-conflict areas.
- `Runtime/Core/Contracts` belongs to contract freezing work.
- `Runtime/Core/Intake` belongs to DesignPacket and UiPrefabSpec intake work.
- `Runtime/Core/Manifest` and `Editor/Generation` belong to generation work.
- `Runtime/HolmasAdapter` belongs to Holmas profile and mapping work.
- `Editor/Validation` and `Tests` belong to validation work.
- Review history belongs in `doc/迭代记录`, not in long-term docs.

## Skill Trigger Guide

Reach for this skill when the user asks things like:
- “把 UI 自动生成系统方案改成可执行 subagent 派工单”
- “收紧 UI 自动生成系统的目录边界和写入范围”
- “检查这套 UI 生成系统方案是否符合长期主文档隔离规则”
- “实现某个 UI 页面/弹窗/按钮的业务逻辑，不要乱动 prefab 样式”
- “让 subagent 做 UI 绑定、collector、manifest 或 View/Controller 接入”

## Required Workflow

1. Confirm the authoritative doc path is in the UI system zone.
2. Convert legacy duplicated docs into jump pages instead of leaving two competing sources of truth.
3. Freeze directory, asmdef, and migration boundaries before parallel implementation guidance.
4. Keep Samples and Documentation ownership explicit instead of treating them as shared dump folders.
5. Put review findings in iteration logs, not in stable design docs.

## Do Not

- Do not leave execution rules only in `方案与数据`.
- Do not let multiple subagents co-own a high-conflict directory.
- Do not put Holmas gameplay or App.Shared ownership into the generator system by default.
- Do not let review-only work rewrite long-term system rules.
- Do not accept UI runtime code that “先查找节点再绑定”。缺少静态 binding 时应修 prefab/manifest，而不是在页面打开时兜底查找。
- Do not change original prefab colors, alpha, tint defaults, material colors, or `CanvasGroup.alpha` during UI logic work unless the user explicitly requests that visual change.

## Validation

Before finishing:
- Check that there is only one authoritative long-term entry for this system.
- Check that old files now act as redirects or index pages.
- Check that each subagent has a concrete write boundary.
- Check that Holmas is still framed as a trial adapter, not as the core system.
- Check that UI writer/reviewer tasks explicitly include static binding acceptance: all runtime-touched nodes are in collector/manifest, and runtime lookup is absent.
- Check that UI logic changes preserve original prefab colors and transparency unless the task explicitly asked for visual/style changes.

Read these references when needed:
- `references/checklist.md`
- `references/ownership.md`
- `references/dispatch-template.md`
