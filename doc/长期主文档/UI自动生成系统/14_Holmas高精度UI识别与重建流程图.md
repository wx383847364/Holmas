# Holmas 高精度 UI 识别与重建流程图

这页固定展示 Holmas 高精度 UI 识别与重建方案的 Mermaid 正文版流程图。

用途固定为：

- 让阅读者快速看懂从设计图到最终 prefab 的完整链路
- 明确当前轮次实际要做的是哪一段
- 说明高精度前半段 MVP 的 `6 个 subagent` 阶段性编组

## 审查结论

在将流程图固化为长期文档前，已按 `Agent 6 / Review-Acceptance` 口径补过一轮审查。  
审查后的冻结约束如下：

- 本图中的 `6 个 subagent` 仅表示“高精度前半段 MVP”的阶段性编组，不替代 [03_Agent编组方案](./03_Agent编组方案.md) 中的长期固定编组。
- 端到端主链必须显式包含 `intake / gating`，不能把 `DesignPacket -> UiPrefabSpec` 画成无门禁直通。
- `preview` 支线必须标注为 `Phase 3+ / review-only`，避免被误解成首轮必交付。
- 后半段必须显式拆成 `generation -> PrefabBindingManifest -> validation -> generation_result`，不能把“生成 + 校验”折叠成黑盒。

## 图例

- 主生成链：从设计图进入 `UiPrefabSpec` 再到 prefab 和 manifest 的正式链路
- review-only 支线：只用于审阅和验收，不直接驱动 generator
- 本轮范围：当前收缩版 MVP 重点增强的前半段
- Phase 3+：后续阶段，不要求首轮一次性实现

## 端到端总流程图

```mermaid
flowchart TD
    A["设计图 / 页面截图"] --> B["创建任务<br/>request.json"]
    B --> C["视觉证据层<br/>VisualUnderstandingBundle"]
    C --> D["视觉审阅层<br/>VisualReviewReport"]
    D --> E["归一化输入<br/>DesignPacket"]
    E --> F["Intake / Gating<br/>blocking / unresolved / ready"]
    F --> G["机器权威层<br/>UiPrefabSpec"]
    G --> H["人工审阅 Spec"]
    H --> I["Prefab 草稿生成"]
    I --> J["PrefabBindingManifest"]
    J --> K["结构 / Manifest 校验"]
    K --> L["Holmas Adapter 消费约束"]
    L --> M["生成结果<br/>generation_result.json"]
    M --> N["业务侧人工接线 / 接 Presenter"]

    C --> P["Phase 3+ / Review-only<br/>PreviewRenderPlan"]
    P --> Q["Phase 3+ / Review-only<br/>preview_render.png"]
    Q --> R["Phase 3+ / Review-only<br/>preview_diff_report.json"]
    R --> H

    subgraph CURRENT["本轮范围 / 高精度前半段 MVP"]
        C
        D
        E
        F
        G
    end
```

## 当前轮次在做哪一段

当前轮次不重做整条链，而是集中增强最薄弱的前半段：

```mermaid
flowchart LR
    A["设计图"] --> B["视觉证据层"]
    B --> C["视觉审阅层"]
    C --> D["DesignPacket"]
    D --> E["Intake / Gating"]
    E --> F["UiPrefabSpec"]
```

这段增强完成后，现有的后半段仍沿用当前系统已有能力：

- `UiPrefabSpec -> prefab 草稿`
- `PrefabBindingManifest`
- validation
- `generation_result.json`
- Holmas adapter 消费约束

## 高精度前半段 MVP 的 6 个 Subagent 分工流程图

注意：这张图只适用于高精度前半段 MVP 的阶段性执行编组。  
它不替代 [03_Agent编组方案](./03_Agent编组方案.md) 中冻结的长期固定编组。

```mermaid
flowchart TD
    S1["Subagent 1<br/>Evidence-Contracts<br/>冻结 review-only 证据层契约"] --> S2["Subagent 2<br/>Analysis-Orchestrator<br/>生成 visual artifacts / review report"]
    S1 --> S3["Subagent 3<br/>Intake-Spec<br/>evidence -> DesignPacket -> UiPrefabSpec"]
    S1 --> S4["Subagent 4<br/>Review-UX<br/>窗口审阅入口 / source-preview-diff 摘要"]
    S2 --> S5["Subagent 5<br/>Validation-Regression<br/>tests / samples / golden / regression"]
    S3 --> S5
    S4 --> S5
    S2 --> S6["Subagent 6<br/>Review-Acceptance<br/>阶段审查 / findings / 退回条件"]
    S3 --> S6
    S4 --> S6
    S5 --> S6
```

## 各 Subagent 的写入边界摘要

### Subagent 1 / Evidence-Contracts

- 只冻结 `VisualUnderstandingBundle`、`VisualReviewReport`、`PreviewRenderPlan`、`PreviewDiffReport`
- 只写 `Runtime/Core/Schema`、`Runtime/Core/Result` 和长期文档

### Subagent 2 / Analysis-Orchestrator

- 只负责 `Editor/Analysis` 和 task artifact 回写
- 不定义机器权威 spec 规则

### Subagent 3 / Intake-Spec

- 只负责 `Runtime/Core/Intake`
- 只做 evidence -> DesignPacket -> `UiPrefabSpec`

### Subagent 4 / Review-UX

- 只负责 `Editor/Window` 和 `Editor/Preview`
- 只读证据和 plan，不反向定义契约

### Subagent 5 / Validation-Regression

- 只负责 `Tests`、`Samples~/Holmas`、必要的 `Editor/Validation`
- 不临时改主实现来“修绿”测试

### Subagent 6 / Review-Acceptance

- 只负责 `doc/迭代记录`
- 不改长期主文档正文和生产实现目录

## 阶段映射

固定映射如下：

- `Phase 1`：evidence MVP
- `Phase 2`：intake / spec upgrade
- `Phase 3`：structured preview
- `Phase 4+`：真实 provider、逐元素人工纠正 UI

## 与现有系统关系

- 后半段 `generator / manifest / validation / Holmas adapter` 仍按现有系统执行
- 本轮主要增强前半段输入质量和可审阅性
- `UiPrefabSpec` 仍是唯一机器权威
- preview 支线始终是 `review-only`，不直接驱动 generator

## 完成情况

- 已固定端到端总流程图
- 已固定“当前轮次范围”流程图
- 已固定高精度前半段 MVP 的 `6 个 subagent` 阶段性编组流程图
- 已补充与长期固定编组、当前轮次范围、preview 阶段边界的说明
