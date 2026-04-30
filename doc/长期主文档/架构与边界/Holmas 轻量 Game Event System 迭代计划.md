# Holmas 轻量 Game Event System 迭代计划

## Summary

首轮做“代码事件基建 + Runtime 领域事件桥接 + 单链路试迁移 + 非核心 ScriptableObject EventChannel”。采纳 Agent 6 审查意见：不全量迁移 UI/Tutorial，不让 SO 资产进入核心玩法链路；先确保兼容、可退订、可测试，再扩大范围。

## Key Changes

- 保留现有 `IEventBus.Subscribe/Unsubscribe/Publish` 签名，新增 `IEventSubscription` 和 `SubscribeScoped<T>(handler, priority, condition)`。
- 增强 AOT `EventBus`：发布前 handler 快照、相同优先级按订阅顺序执行、handler/condition 异常隔离、`Dispose()` 自动退订、旧 API 继续可用。
- 在 HotUpdate 新增 Holmas 领域事件 DTO：至少覆盖 `GameplayStateChanged`、`EnergyChanged`、`TaskRewardTipChanged`、`TaskBarChanged`、`LevelStateChanged`，payload 使用运行时快照，不只传 reason。
- `HolmasGameplayRuntime.NotifyStateChanged` 保持先触发旧 `StateChanged`，再通过可选 `IEventBus` 发布新领域事件；现有构造函数保持兼容，Bootstrap 注入 event bus。
- 首轮只试迁移 1 条低风险 UI 链路：优先 `BattlePageController` 的 `EnergyChanged/TaskRewardTipChanged`；保留导航页过滤，避免后台页刷新。
- 新增最小 SO EventChannel：`HolmasVoidEventChannel`、`HolmasStringEventChannel`、对应 listener 组件；只用于非核心演示/调试/UI 外围触发，不接管存档、关卡、教程主流程。
- 文档新增事件系统迭代方案，任务完成后按项目规则执行文档收尾和 Agent 6 审查记录。

## Iterations

- 迭代 0：EventBus 基建  
  完成接口扩展、AOT 实现、测试 FakeEventBus 兼容更新、基础单测。

- 迭代 1：Runtime 桥接  
  添加 Holmas 领域事件 DTO，Bootstrap 注入 event bus，Runtime 同时保留旧 `StateChanged` 与新事件发布。

- 迭代 2：单链路试迁移  
  迁移 `BattlePageController` 中能量/任务奖励提示相关监听；不迁移存档同步、TutorialOverlay、Main/AgencyMain 全量刷新。

- 迭代 3：非核心 SO EventChannel  
  加最小 ScriptableObject 事件资产与 listener 组件，作为调试或 UI 外围事件能力验证。

- 迭代 4：Agent 6 复审与扩大范围建议  
  复查生命周期、发布顺序、重复刷新、存档 dirty 兼容；通过后再计划 Main/AgencyMain/Tutorial 的后续迁移。

## Test Plan

- EventBus 单测：优先级顺序、同优先级订阅顺序、condition false 跳过、condition 异常隔离、handler 异常隔离、Dispose 自动退订、handler 内退订不破坏发布。
- Runtime 桥接测试：旧 `StateChanged` 仍触发；新领域事件 payload 正确；旧事件先于新事件；无 event bus 时 runtime 正常工作。
- UI 回归：Battle 能量变化和任务奖励提示仍刷新；非当前页不刷新；重复打开/关闭页面不重复订阅。
- SO EventChannel 测试：Raise 能触发 listener；禁用/销毁 listener 后不再响应；不依赖核心 gameplay 事件。
- 回归执行：优先跑现有 Holmas EditMode 测试和新增事件测试；若 Unity CLI 可用，再跑相关测试程序集。

## Assumptions

- 当前不购买 GES 插件，自研实现只借鉴其资源驱动、优先级、条件监听和可诊断思想。
- 首轮不做可视化 FlowGraph、引用查找器、表达式树动态条件编辑器。
- SO EventChannel 首轮只验证资产驱动能力，不进入核心玩法链路。
- 当前处于计划模式，本计划定稿后再切换到执行模式落地；落地完成后必须按项目文档规则走 Agent 6 审查与文档收尾。

## 完成情况

- 2026-04-30：已完成首轮落地。
  - `IEventBus` 保留旧签名并新增 `IEventSubscription` / `SubscribeScoped<T>`。
  - AOT `EventBus` 已支持发布快照、优先级稳定顺序、条件监听、异常隔离和幂等退订。
  - `HolmasGameplayRuntime` 已在旧 `StateChanged` 之后桥接发布 Holmas 领域事件，Bootstrap 已注入 `IEventBus`。
  - `BattlePageController` 已试迁移能量与任务奖励提示链路，并避免旧事件重复刷新。
  - HotUpdate/Holmas 侧已新增非核心 `HolmasVoidEventChannel`、`HolmasStringEventChannel` 和 listener 组件。
  - 已补充 EventBus、Runtime 桥接和 SO listener 生命周期测试。
  - 已通过 `tools/validation/run_holmas_validation.sh --log-prefix holmas_event_system_smoke` 的 EditMode 与 smoke 验证。
