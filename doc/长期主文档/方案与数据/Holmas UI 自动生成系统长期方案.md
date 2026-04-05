# Holmas UI 自动生成系统长期方案

## Summary

把这件事定义成一条长期的 `UI 生成流水线`，而不是单次“让 agent 直接吐 prefab”。

第一阶段按你刚锁定的方向落地：
- 先在 Holmas 仓库内做成 **可拆出的独立模块**
- 只服务 **Unity UGUI**
- 输入是 **设计图 + 标注包 + 结构化 spec**
- 发布流程是 **agent 先产 spec，人审后再生成 prefab**
- 第一版输出 **Prefab 骨架 + 绑定清单**，不自动生成完整业务 Presenter，不直接覆盖正式资产

建议架构分成 4 层：
1. `Design Intake`
设计图、状态图、交互规则、资源命名、标注说明的标准输入包。
2. `Spec Layer`
把设计输入转成稳定、可版本化、可 diff 的 `UiPrefabSpec`。
3. `Generation Layer`
由确定性的 Unity Editor 生成器把 spec 落成 UGUI prefab 草稿。
4. `Validation Layer`
校验命名、层级、组件、资源槽位、绑定、回归重建是否稳定。

## Key Changes

### 1. 模块边界

第一版直接按“未来可抽离小项目”组织，建议做成 **本地 UPM 包**，而不是散落在业务目录里。

包内拆 4 个子域：
- `Schema`
定义 `DesignPacket`, `UiPrefabSpec`, `PrefabBindingManifest`, 校验结果类型。
- `Interpreter`
负责把“设计图 + 标注包”解析成 spec。这里允许接 UI agent。
- `UnityUguiGenerator`
纯 Editor 侧，把 spec 生成为 prefab 草稿。
- `Validators`
做 spec 校验、生成结果校验、可重复生成校验。

Holmas 主项目只保留一层很薄的 `Project Adapter`：
- 目录规则
- 命名规则
- 默认字体/组件白名单
- Holmas 自己的 binding 命名约定

### 2. 稳定数据契约

第一版冻结 3 个核心契约，后续所有 agent 和工具都围绕它们工作：

- `DesignPacket`
  - 设计图路径
  - 页面/弹窗标识
  - 状态列表
  - 资源命名表
  - 交互规则
  - 标注说明

- `UiPrefabSpec`
  - `page_id`
  - `prefab_name`
  - `root_node`
  - `nodes[]`
  - `component_specs[]`
  - `layout_specs[]`
  - `asset_slots[]`
  - `bindings[]`
  - `interactions[]`
  - `generation_profile`

- `PrefabBindingManifest`
  - 节点路径
  - 组件类型
  - 资源槽位
  - 事件出口
  - 数据绑定位
  - 未完成项/人工补位项

建议第一版用 **JSON** 作为权威机器格式。
原因是 Unity/C# 解析稳定、跨项目迁移简单、回归检查容易。
如果需要可读性，再额外生成一份 Markdown 预览，不把 Markdown 当权威输入。

### 3. 工作流定义

冻结成两阶段，不让“理解设计”和“生成 prefab”混在一起：

1. `Spec Draft`
UI agent 读取 `DesignPacket`，生成 `UiPrefabSpec` 草稿和预览清单。
2. `Human Review`
人工只审结构、命名、层级、资源槽位、绑定位，不审 Unity 细节实现。
3. `Prefab Generate`
Unity Editor 工具根据已批准 spec 生成 prefab 草稿和绑定清单。
4. `Prefab Validate`
自动检查是否缺组件、缺节点、命名不合规、路径漂移、重复生成不稳定。
5. `Promote`
人工把草稿 prefab 提升为正式资产，或在正式目录覆盖更新。

第一版不允许：
- 直接从图片跳过 spec 生成正式 prefab
- 直接全自动覆盖正式 prefab
- 在生成器里混入业务规则
- 让生成器直接产完整业务 Presenter

### 4. V1 生成范围

第一版生成器只负责这些内容：
- prefab 根层级
- 节点命名
- UGUI 基础组件
  - `RectTransform`
  - `Image`
  - `Text` 或项目统一文本组件
  - `Button`
  - `ScrollRect`
  - 常用 Layout 组件
- 占位资源槽位
- 绑定清单
- 事件出口位
- 页面状态位

第一版明确不做：
- 像素级还原设计图
- 自动切图/自动九宫格推断
- 自动写业务 Presenter 逻辑
- 自动接 `HolmasApplicationContext`
- 自动生成整场景流转

### 5. 与 Holmas 当前状态的连接方式

当前仓库已经有 `Holmas/UI` 的过渡态代码生成 UI 根，也有长期文档要求最终收敛到正式 prefab/presenter/scene 资产。第一版系统不要直接替代全部现有 UI 代码，而是把 Holmas 当作首个试点项目。

试点顺序固定为：
1. 先选一个最小页面
建议选 `任务栏 + 主操作区`，不要一上来做整套 agency/battle 流程。
2. 用设计图和标注包生成首个 `UiPrefabSpec`
3. 产出 prefab 草稿与 binding manifest
4. 人工确认后，再由 Holmas 的 UI 实现方接业务逻辑

这样能先验证“规格层 + 生成层 + 校验层”是否成立，再逐步扩大页面覆盖。

## APIs / Interfaces

第一版建议显式定义这些接口，避免实现时再拍脑袋：

- `IDesignPacketLoader`
加载设计输入包。
- `IUiSpecInterpreter`
把设计输入转成 `UiPrefabSpec`。
- `IUiSpecValidator`
校验 spec 的结构完整性和项目约束。
- `IUnityPrefabGenerator`
把 spec 生成到指定草稿目录。
- `IPrefabValidationRunner`
检查 prefab 与 manifest 是否匹配。
- `IProjectUiProfile`
定义项目级目录、命名、默认组件、资源规则。
- `IGenerationPromotionService`
把草稿产物提升到正式目录，第一版可先保留接口，不一定实现自动 promotion。

## Test Plan

第一版必须覆盖这些场景：

- 同一份 spec 连续生成两次，输出稳定，不出现随机路径或随机命名。
- spec 改一个节点，只影响对应子树，不导致整 prefab 大面积漂移。
- 缺少资源槽位时，生成器报显式错误或警告，不静默跳过。
- 非法组件类型、非法布局配置、非法节点命名会被 validator 拦下。
- 绑定 manifest 能准确列出每个节点路径、组件、资源位和事件位。
- Holmas 试点页面能生成 prefab 草稿，并能被人工接入现有 UI 流程。
- 生成结果满足项目命名规范、目录规范、组件白名单。
- 回归测试能比较 spec 版本升级前后的 prefab 差异。

## Assumptions

- 第一版只做 Unity UGUI，不为 UI Toolkit 或跨引擎输出做实现承诺。
- 设计侧未来能提供“设计图 + 标注包”，不是只有效果图。
- UI agent 的职责是先产 `UiPrefabSpec`，不是直接改 Unity 资产。
- 人工审核发生在 spec 阶段和 prefab 草稿阶段，两次都保留。
- 第一版以 Holmas 为试点，但代码组织按“可独立抽出”为目标，不把业务逻辑耦合进生成器。
- Presenter/业务接线不纳入第一版自动生成范围；只通过 binding manifest 给实现方稳定接口。
