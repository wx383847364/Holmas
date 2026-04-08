---
name: ui-prefab-pipeline
description: Use for this project when defining, reviewing, or validating the UI prefab generation pipeline from DesignPacket to UiPrefabSpec to PrefabBindingManifest and validation. Enforces spec authority, generation boundaries, manifest structure, trial samples, and deterministic regression expectations.
---

# UI Prefab Pipeline

Use this skill for any task that touches:
- `DesignPacket`
- `UiPrefabSpec`
- `PrefabBindingManifest`
- generator flow
- validator flow
- golden cases
- deterministic regression
- sample spec or sample manifest outputs

## Goal

Keep the pipeline explicit, reviewable, and deterministic.
Treat `UiPrefabSpec` as the only machine-authoritative intermediate layer.

## Core Rules

- Images are design references, not machine authority.
- `UiPrefabSpec` is the only machine-authoritative intermediate spec.
- First version supports Unity UGUI only.
- First version outputs prefab drafts and `PrefabBindingManifest` only.
- The system must not auto-write business Presenter logic or gameplay wiring.
- Validation must catch illegal nodes, illegal components, missing asset slots, and naming conflicts before promotion.
- A local spec change should only cause local output drift.

## Pipeline Boundaries

- `DesignPacket` captures design input, states, rules, asset-slot hints, and notes.
- `UiPrefabSpec` captures structure, components, bindings, resource slots, and event exits.
- `PrefabBindingManifest` captures what was generated and what still needs manual wiring.
- `ProjectUiProfile` is project-facing configuration and belongs to adapter work, not to core schema ownership.

## Skill Trigger Guide

Reach for this skill when the user asks things like:
- “补 DesignPacket 和 UiPrefabSpec 的契约”
- “规划 prefab 草稿生成和 manifest 输出”
- “给 UI 自动生成系统补 validator 和 deterministic 回归规则”

## Required Workflow

1. Normalize design input into `DesignPacket`.
2. Convert it into an explicit `UiPrefabSpec`.
3. Require human review before generation.
4. Generate prefab draft and manifest from the approved spec.
5. Run validation and regression before any promotion or business-side adoption.

## Trial Sample Rules

- `Samples~/Holmas` is for trial inputs, sample manifests, adapter samples, and golden fixtures.
- `Documentation~` should only hold subsystem-specific notes, not duplicate the main long-term docs.
- `Subagent 5` should not fully engage until there is at least one approved sample spec and one sample manifest.

## Do Not

- Do not skip directly from image to formal prefab.
- Do not mix spec, generation, and validation into the same ownership area.
- Do not let generator code depend on Holmas gameplay logic.
- Do not let adapter policy redefine core schema.

## Validation

Before finishing:
- Check that generator and validator consume `UiPrefabSpec`, not raw images.
- Check that the manifest fully describes generated nodes, components, asset slots, and manual wiring gaps.
- Check that there is at least one repeatable sample path for validation.
- Check that failure reporting distinguishes schema, generator, adapter, and fixture issues.

Read these references when needed:
- `references/contracts.md`
- `references/review-checklist.md`
- `references/golden-cases.md`
