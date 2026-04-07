# Holmas 高精度 UI 识别与重建双输出链路方案

## Summary

这页固定 Holmas 试点阶段的高精度 UI 识别与重建方案。

目标不是把当前系统改成“图片直接出正式 prefab”的黑盒，而是在现有

`DesignPacket -> UiPrefabSpec -> PrefabBindingManifest -> validation`

主链路前面补一层 `review-only 视觉证据层`，再逐步把当前最小规则解释器升级成更可靠的结构化识别链路。

本方案固定采用：

- `结构化 spec + 结构化预览` 双输出闭环
- `UiPrefabSpec` 继续作为唯一机器权威
- Holmas 继续只作为试点 adapter，不作为系统本体目录
- 首轮先做 MVP，不一次性接通真实 OpenAI provider、高拟真预览和逐元素人工编辑

## 现状问题

当前 `Portrait Generator` 已经打通：

- `request.json`
- 本地自动分析 batch
- `design_packet.json / ui_prefab_spec.json / resource_match_report.json / analysis_result.json`
- prefab 生成与 `generation_result.json`

但当前自动分析本质上仍是：

- `request -> 最小 DesignPacket`
- `DesignPacket -> 最小规则解释器`

导致常见弱结果是：

- 只有 `root`
- 最多补一个 `panel_bg`
- `Bindings / Interactions / 高价值节点` 缺失
- 窗口能看出“成功但结果弱”，但无法拿到足够证据指导下一步修正

所以本轮高精度方案的重点不是“直接让 AI 画页面”，而是：

1. 先产出稳定、可审阅、可落盘的视觉证据
2. 再把证据归一化为 `DesignPacket`
3. 再稳定产出更强的 `UiPrefabSpec`
4. 最后用结构化预览辅助人工验收

## 冻结原则

### 机器权威不变

- `UiPrefabSpec` 仍是唯一机器权威中间层
- generator / validator / adapter 只消费 `UiPrefabSpec`
- 图片、视觉证据、预览图都不能绕开 spec 直接进入 generator

### Holmas 角色不变

- Holmas 仍只是首个试点项目
- 新能力只允许写入：
  - `Assets/Tools/UiPrefabGenerator`
  - `Assets/UiPrefabGeneratorData`
  - `doc/长期主文档/UI自动生成系统`
- 不允许写入：
  - `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI`
  - `Assets/Scripts/App.Shared`
  - 现有 Holmas gameplay 目录

### MVP 优先

不允许在首轮一次性并入下面所有重改：

- 真实 OpenAI 云 provider
- 高拟真 preview renderer
- 窗口逐元素 bbox/文本/slot/binding 编辑
- 全量高精度规则族
- 多套 portrait golden fixtures 同步冻结

首轮必须先做协议和 gating 的可落地 MVP。

## 目标链路

### 目标总链路

固定为：

`source image -> visual evidence -> visual review report -> normalized DesignPacket -> UiPrefabSpec -> prefab/manifest`

并新增一条并行审阅支线：

`visual evidence -> PreviewRenderPlan -> preview_render.png -> preview_diff_report.json`

### 双输出定义

#### 输出 1：结构化主链

- `DesignPacket`
- `UiPrefabSpec`
- `PrefabBindingManifest`
- `generation_result.json`

这是现有可执行生成主链，继续作为正式输出。

#### 输出 2：结构化预览链

- `PreviewRenderPlan`
- `preview_render.png`
- `preview_diff_report.json`

这是人工审阅辅助链，只负责说明：

- 识别到了什么
- 哪里没识别好
- 与原图差异主要在哪里

不负责成为正式 prefab 生成权威。

## MVP 范围

### 首轮必做

#### 1. Review-only 视觉证据层

新增 task 目录产物：

- `visual_understanding.json`
- `visual_review_report.json`

新增公开契约：

- `VisualUnderstandingBundle`
- `VisualElementEvidence`
- `VisualTextEvidence`
- `VisualStyleEvidence`
- `VisualHierarchyEdge`
- `VisualConfidenceSummary`

职责固定：

- 存视觉理解证据
- 存 bbox / 类型 / 文本 / 层级 / 置信度
- 存低置信与未决项
- 不直接驱动 generator

#### 2. Intake / gating 扩展

扩展 intake analyzer，使其能稳定表达：

- 低置信文本
- 关键控件缺失
- 资源槽歧义
- 布局冲突
- 需要人工决策的元素语义冲突

这些问题必须进入结构化 unresolved items，不允许只落自由文本 warning。

#### 3. 更强的 spec 解释器

首轮只扩第一批高价值规则：

- `panel_background`
- `title_text`
- `primary_button`
- `numeric_value_display`

目标是让常见 portrait 页面不再只退化成：

- `root`
- `panel_bg`

而是至少能补出：

- 主标题
- 主按钮
- 关键数值展示

#### 4. 窗口证据摘要

`Portrait Generator` 首轮只新增：

- 视觉证据摘要
- low-confidence 列表
- unresolved / blocking 统计

首轮不做：

- 逐元素 bbox 编辑
- 逐元素文本修正
- slot/binding 手工回写 UI

### 首轮不做

- 真实 OpenAI provider 必接
- 高拟真 preview renderer
- 云端直接补整页图
- 逐元素人工纠正 UI
- 全量 11 个高精度规则族

## 分阶段落地

### Phase 1 / Evidence MVP

目标：

- 打通 `visual_understanding.json` 和 `visual_review_report.json`
- 打通 task 目录读写和 batch 回写
- 默认 provider 先用本地 deterministic/mock 生成器占位

完成标准：

- 新证据产物可稳定写入 task 目录
- 窗口能显示证据摘要和低置信列表
- 分析失败原因比当前更可解释

### Phase 2 / Intake And Spec Upgrade

目标：

- 扩展 intake unresolved item 类型
- 扩展 evidence -> DesignPacket 归一化
- 扩展高价值规则解释器

完成标准：

- 常见 portrait 页面不再只产 root
- spec 能稳定产出 title / primary button / numeric display 等节点

### Phase 3 / Structured Preview

目标：

- 新增 `PreviewRenderPlan`
- 新增结构化预览和 diff 产物

完成标准：

- 可以肉眼看出“结构是否接近原图”
- diff 能明确提示未还原区域

注意：

- 这一阶段先做结构可视化预览
- 不追求高拟真到像素级接近原图

### Phase 4 / Real OpenAI Provider

目标：

- 在协议和 task 目录稳定后，接入真实 OpenAI provider

完成标准：

- provider 有明确配置入口
- 有失败降级策略
- 不阻塞本地 mock provider 路线

### Phase 5 / Manual Element Correction

目标：

- 在中间契约冻结后，再做逐元素人工纠正

完成标准：

- 编辑格式稳定
- 不反向污染 `DesignPacket / UiPrefabSpec` 权威边界

## Subagent 编组建议

本方案首轮推荐固定为 `6 个 subagent`：

- `5 个执行/验证`
- `1 个纯审查`

不建议少于 5 个，也不建议多于 6 个。

原因：

- 少于 5 个会导致 `Analysis / Intake / Window / Tests` 串行过长
- 多于 6 个会让 `Editor/Analysis`、`Editor/Window`、`Runtime/Core/Intake` 这些高冲突区切得过细，容易互踩

### Subagent 1 / Evidence-Contracts

目标：

- 冻结 review-only 证据层契约和 task 目录新产物

允许写入范围：

- `Assets/Tools/UiPrefabGenerator/Runtime/Core/Schema`
- `Assets/Tools/UiPrefabGenerator/Runtime/Core/Result`
- `doc/长期主文档/UI自动生成系统`

禁止写入范围：

- `Runtime/Core/Intake`
- `Editor/Analysis`
- `Editor/Window`
- `Tests`

交付物：

- `VisualUnderstandingBundle`
- `VisualReviewReport`
- `PreviewRenderPlan`
- `PreviewDiffReport`
- task 目录文件命名和字段说明

验收点：

- 新证据层是 review-only
- `UiPrefabSpec` 机器权威不变

### Subagent 2 / Analysis-Orchestrator

目标：

- 升级自动分析主链，生成视觉证据和 review report

允许写入范围：

- `Assets/Tools/UiPrefabGenerator/Editor/Analysis`
- `Assets/Tools/UiPrefabGenerator/Editor/Bridge`
- 必要时 `scripts/ui_prefab_generator`

禁止写入范围：

- `Runtime/Core/Intake`
- `Editor/Window`
- `Tests`
- `Runtime/HolmasAdapter`

交付物：

- 视觉证据构建器
- task 目录回写逻辑
- batch/report 扩展

验收点：

- provider 失败时有明确报错
- 不直接生成 prefab

### Subagent 3 / Intake-Spec

目标：

- 扩 intake/gating
- 扩 evidence -> DesignPacket 归一化
- 扩第一批高价值规则解释器

允许写入范围：

- `Assets/Tools/UiPrefabGenerator/Runtime/Core/Intake`

禁止写入范围：

- `Runtime/Core/Schema`
- `Editor/*`
- `Tests`
- `HolmasAdapter`

交付物：

- 新 intake issue 类型
- 归一化逻辑
- 第一批高价值规则支持

验收点：

- 常见 portrait 页面不再只有 `root + panel_bg`
- unresolved / blocking 判定明确

### Subagent 4 / Review-UX

目标：

- 把 `Portrait Generator` 升级成证据审阅器

允许写入范围：

- `Assets/Tools/UiPrefabGenerator/Editor/Window`
- `Assets/Tools/UiPrefabGenerator/Editor/Preview`

禁止写入范围：

- `Runtime/Core/*`
- `Editor/Analysis`
- `Tests`

交付物：

- source / preview / diff 审阅入口
- low-confidence 列表
- gating 摘要展示

验收点：

- 人工能快速判断“结果弱在哪里”
- 窗口不成为新的机器权威层

### Subagent 5 / Validation-Regression

目标：

- 冻结 MVP 的测试、样例和回归口径

允许写入范围：

- `Assets/Tools/UiPrefabGenerator/Tests`
- `Assets/Tools/UiPrefabGenerator/Samples~/Holmas`
- 必要时 `Assets/Tools/UiPrefabGenerator/Editor/Validation`

禁止写入范围：

- `Runtime/Core/*`
- `Editor/Analysis`
- `Editor/Window`
- `HolmasAdapter`

交付物：

- 新契约 round-trip 测试
- intake/spec 增强测试
- analysis artifact 测试
- MVP golden fixtures

验收点：

- 同一输入重复分析结果稳定
- 新证据产物和 spec 产物都可回归

### Subagent 6 / Review-Acceptance

目标：

- 独立审查边界、覆盖和阶段结论

允许写入范围：

- `doc/迭代记录`

禁止写入范围：

- 长期主文档正文
- 任何实现目录

交付物：

- findings
- 通过 / 退回结论
- 下一阶段建议

验收点：

- 审查历史只落迭代记录
- 不回写长期规则正文

## 启动顺序

固定为：

1. `Subagent 1 / Evidence-Contracts`
2. 契约基本冻结后，并行启动：
   - `Subagent 2 / Analysis-Orchestrator`
   - `Subagent 3 / Intake-Spec`
   - `Subagent 4 / Review-UX`
3. `Subagent 2 / 3` 有稳定 task 产物后，再启动 `Subagent 5 / Validation-Regression`
4. 阶段性交付后，由 `Subagent 6 / Review-Acceptance` 做独立审查

## 验收口径

MVP 通过标准固定为：

- 不再出现“只有 `root + panel_bg` 也顺利通过主链”的弱结果
- 新视觉证据层能解释当前失败或弱结果的原因
- intake unresolved / blocking 能稳定提示关键缺失项
- `UiPrefabSpec` 仍是唯一机器权威
- Holmas gameplay 目录零写入

## 完成情况

当前状态：

- 本页已冻结高精度双输出链路的总体方向
- 已明确首轮只做 MVP，不一次性并入真实云 provider 和高拟真 preview
- 已明确 `6 个 subagent` 的推荐编组和启动顺序
- 后续实现应优先做 `review-only evidence contract + intake/gating 扩展 + 更强的 spec 解释器`
- 配套 Mermaid 流程图正文见：[14_Holmas高精度UI识别与重建流程图](./14_Holmas高精度UI识别与重建流程图.md)
