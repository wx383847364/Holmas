# 竖屏 EditorWindow 与任务目录设计

## 目标

这页固定竖屏小游戏版 UI 生成工具的产品化落地方式。

本页回答 4 个问题：

- Unity 里从哪里操作
- 模板和参数存在哪里
- Codex 与 Unity 之间如何交接
- 多 agent 实现时如何分工，避免目录互踩

## 主入口

Unity 侧唯一主入口固定为：

```text
UiPrefabGenerator/Window/Portrait Generator
```

这个窗口负责：

- 选择或保存模板
- 拖入设计图
- 填写 `page_id / page_title / prefab_name`
- 生成 `request.json`
- 触发本地自动分析并回写结果
- 刷新分析结果
- 确认后生成 prefab
- 展示 `generation_result.json`

不负责：

- 直接理解图片内容
- 直接请求业务逻辑代码
- 直接绕过 `UiPrefabSpec`

## 目录落地

### 模块代码目录

```text
Assets/Tools/UiPrefabGenerator
├── Runtime
│   ├── Core
│   │   ├── Schema
│   │   ├── Intake
│   │   ├── Interpretation
│   │   ├── Manifest
│   │   ├── Validation
│   │   ├── Profile
│   │   ├── Request
│   │   ├── Result
│   │   └── ResourceMatch
│   └── HolmasAdapter
├── Editor
│   ├── Analysis
│   ├── Window
│   ├── Bridge
│   ├── Template
│   ├── Generation
│   ├── ResourceMatch
│   ├── Validation
│   └── Preview
├── Tests
│   ├── EditMode
│   └── Fixtures
├── Documentation~
└── Samples~/Holmas
```

### 项目数据目录

```text
Assets/UiPrefabGeneratorData
├── Templates
│   ├── ProjectDefaults
│   └── PageTypes
├── Tasks
│   └── <task_id>
│       ├── request.json
│       ├── source_image.png
│       ├── design_packet.json
│       ├── ui_prefab_spec.json
│       ├── resource_match_report.json
│       ├── analysis_result.json
│       ├── prefab_binding_manifest.json
│       ├── generation_result.json
│       └── analysis_summary.md
└── Cache
```

### 生成输出目录

Holmas 竖屏 profile 的 prefab 输出目录固定为：

```text
Assets/Res/Perfabs/Generated/Holmas/Portrait
```

## 默认模板

默认模板文件固定为：

```text
Assets/UiPrefabGeneratorData/Templates/ProjectDefaults/holmas_portrait_wechat_default.json
```

默认字段如下：

```json
{
  "template_name": "holmas_portrait_wechat_default",
  "profile_id": "holmas_ugui_portrait",
  "target_platform": "wechat_minigame",
  "orientation": "portrait",
  "reference_resolution_width": 1080,
  "reference_resolution_height": 1920,
  "canvas_scale_mode": "ScaleWithScreenSize",
  "match_mode": "match_height",
  "match_width_or_height": 1.0,
  "safe_area_mode": "simulate_mobile",
  "root_layout_mode": "fullscreen_mobile",
  "page_type": "mobile_fullscreen",
  "visual_density": "normal",
  "asset_root": "Assets/Res",
  "draft_prefab_root": "Assets/Res/Perfabs/Generated/Holmas/Portrait",
  "runtime_binding_namespace": "App.HotUpdate.Holmas.UI.Generated",
  "resource_match_extensions": [".png", ".jpg", ".prefab", ".asset"],
  "resource_match_strictness": "balanced",
  "node_name_style": "PascalCase",
  "binding_key_style": "snake_case",
  "text_strategy": "placeholder_only",
  "manual_review_required": true,
  "auto_ping_prefab_after_generation": true,
  "auto_open_preview_after_generation": false
}
```

## 协议分层

### 请求层

`request.json` 只表达本次生成意图，不表达分析结果，也不表达最终 prefab。

至少包含：

- `task_id`
- `template_name`
- `profile_id`
- `source_image_asset_path`
- `page_id`
- `page_title`
- `prefab_name`
- `target_platform`
- `orientation`
- `reference_resolution_width`
- `reference_resolution_height`
- `asset_root`
- `draft_prefab_root`
- `notes`

### 分析结果层

本地自动分析 CLI 默认回写以下文件；必要时仍可手动回写：

- `design_packet.json`
- `ui_prefab_spec.json`
- `resource_match_report.json`
- `analysis_result.json`
- `analysis_summary.md`

这层是“图片分析 + 结构化结果”，仍不是最终 prefab。

### 生成结果层

Unity 在读取分析结果后自动生成 prefab，并回写：

- `prefab_binding_manifest.json`
- `generation_result.json`

这层才记录真正生成出的 prefab 路径、资源自动绑定结果、人工接线缺口和校验结果。

## 生成工作流

1. 在 `Portrait Generator` 中选择模板并拖入设计图。
2. 点击 `生成请求`，写入任务目录和 `request.json`。
3. 点击 `自动分析并回写结果`，由本地 `run_task_auto_analysis.sh` 在临时工程里执行 batch 分析并回写 5 份分析产物。
4. 自动分析成功后，窗口自动刷新分析结果；如自动分析不可用，仍可手动回写分析产物后点击 `刷新分析结果`。
5. 在窗口中查看节点树、资源候选和风险项。
6. 点击 `确认并生成 Prefab`。
7. Unity 根据分析结果自动生成 prefab、manifest 和 `generation_result.json`。
8. 如有需要，点击 `定位生成的 Prefab` 做人工复核。

## 多 Agent 协作边界

实现阶段推荐固定为 6 个角色：

- `Agent 1`：模板、请求、结果 DTO 和 JSON 协议
- `Agent 2`：`EditorWindow` 与桥接工作流
- `Agent 3`：竖屏 profile、模板参数、输出路径
- `Agent 4`：资源匹配报告与 prefab 资源绑定应用
- `Agent 5`：EditMode、sample pipeline、deterministic 回归
- `Agent 6`：收口审查，检查边界、回归、文档与可拆性

执行边界固定：

- 不让多个 agent 同时修改 `Editor/Window`
- 不让 Holmas adapter 越过 `Runtime/HolmasAdapter`
- 不让 `Core` 依赖 `HolmasAdapter`
- 不让新竖屏入口覆盖旧 `Samples~/Holmas` fixture

## 完成情况

- 已实现竖屏默认模板和项目数据目录
- 已实现 `Portrait Generator` EditorWindow
- 已实现本地 `run_task_auto_analysis.sh`、batch 分析入口和最小自动分析服务
- 已实现窗口侧自动分析桥接、日志落盘和成功后自动刷新分析结果
- 已实现请求文件写入、分析结果读取和生成结果回写
- 已实现 Holmas 竖屏 profile
- 已保留旧 Holmas 横屏 sample 作为回归，不再作为新入口
- 手动回写分析结果仍保留为兜底，不强制依赖自动分析链路
