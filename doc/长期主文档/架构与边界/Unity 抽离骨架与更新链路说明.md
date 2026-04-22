# Unity 抽离骨架与更新链路说明

## Summary

这份文档合并并替代根目录旧文档：

- `README.md`
- `PROJECT_STRUCTURE.md`
- `UPDATE_LOGIC_FLOW.md`

它描述的是一个从 Holmas / Unity 项目中抽离出来的“项目结构 + 更新逻辑”最小骨架，而不是具体玩法方案。

这套骨架已经移除具体游戏业务，例如战斗、技能、角色成长、背包、结算、支付和业务 UI，只保留可复用的启动、基础设施、资源更新与 HybridCLR 热更加载链路。

## 可复用内容

可以直接复用的部分：

- AOT 启动骨架：`GameBootstrap`
- 基础设施：DI、日志、Tick、事件总线、持久化
- 网络基础层：`NetClient` 与 `HttpTransport`
- 平台桥接占位：`WeChatBridge`
- 资源更新链路：`YooAssetsRuntime` 与 `PatchOperationHandler`
- HybridCLR 热更加载链路：`HybridClrLoader`
- 热更层入口占位：`HotUpdateEntry`

明确不包含的部分：

- Battle 相关代码
- Skill / Effect / Timeline 等技能业务代码
- Inventory / Role / Equipment / Settlement / Payment 等具体玩法模块
- 具体 Holmas 玩法、任务、地图、奖励和 UI 业务实现

## 抽离骨架结构

```text
UnityProject_CoreExtract
├── Assets
│   ├── Scripts
│   │   ├── App.Shared
│   │   │   ├── App.Shared.asmdef
│   │   │   └── Contracts
│   │   │       ├── IService.cs
│   │   │       └── ITransport.cs
│   │   └── App.AOT
│   │       ├── App.AOT.asmdef
│   │       ├── Bootstrap
│   │       │   ├── GameBootstrap.cs
│   │       │   └── AppLifetime.cs
│   │       ├── Infrastructure
│   │       │   ├── DI/ServiceContainer.cs
│   │       │   ├── EventBus/EventBus.cs
│   │       │   ├── Logger/UnityLogger.cs
│   │       │   ├── Persistence/FilePersistenceProvider.cs
│   │       │   └── Tick/TickManager.cs
│   │       ├── Networking
│   │       │   ├── NetClient.cs
│   │       │   ├── Auth/AuthContext.cs
│   │       │   └── Transport/HttpTransport.cs
│   │       ├── Platform
│   │       │   └── WeChat/WeChatBridge.cs
│   │       ├── YooRuntimeAssets
│   │       │   ├── YooAssetsRuntime.cs
│   │       │   └── PatchFlow/PatchOperationHandler.cs
│   │       └── HotUpdate
│   │           └── HybridClrLoader.cs
│   └── HotUpdateContent
│       └── Script
│           └── App.HotUpdate
│               ├── App.HotUpdate.asmdef
│               └── Entry/HotUpdateEntry.cs
```

## 分层说明

- `App.Shared`
  - 跨层协议与接口定义。
  - AOT 与 HotUpdate 都可以引用。
  - 只放稳定契约，不放具体业务实现。

- `App.AOT`
  - 宿主层。
  - 负责启动、基础设施、平台桥接、网络、YooAssets 资源更新、HybridCLR 热更加载。
  - 不写具体玩法规则。

- `App.HotUpdate`
  - 热更层入口。
  - 抽离骨架里只保留 `HotUpdateEntry` 占位。
  - 新项目接入时，从这里开始补业务模块注册和运行时流程。

## 新 Unity 项目接入步骤

1. 新建 Unity 项目。
2. 将抽离骨架中的 `Assets/` 全量拷贝到新项目 `Assets/`。
3. 安装依赖：`YooAsset`，以及项目需要的 HybridCLR 工作流。
4. 在首场景挂载 `GameBootstrap` 组件。
5. 按项目环境修改 `YooAssetsRuntime` 中的 CDN 地址。
6. 在 `HotUpdateEntry` 中逐步补充业务模块注册与启动逻辑。

## 启动顺序

固定启动链路：

1. `GameBootstrap.Start()`
2. 初始化基础设施：Logger / DI / Tick / EventBus / Persistence
3. 初始化平台桥接：`WeChatBridge`
4. 初始化网络层：`NetClient`
5. 初始化资源系统：`YooAssetsRuntime.InitializeAsync()`
6. 初始化热更加载：`HybridClrLoader.LoadAsync()`
7. 调用热更入口：`App.HotUpdate.Entry.HotUpdateEntry.Start(container)`

## YooAssets 更新链路

入口：

```text
YooAssetsRuntime.RunPatchFlowAsync(packageVersion)
```

内部流程：

1. `PatchOperationHandler.CheckVersionAsync()`
2. `PatchOperationHandler.DownloadManifestAsync()`
3. `PatchOperationHandler.DownloadAssetsAsync()`
4. 成功后返回 `true`

当前限制：

- `CheckVersionAsync` 仍是模拟实现，需要替换为真实版本服务。
- `YooAssetsRuntime` 包含 Editor 与 Runtime 两套分支逻辑。

## HybridCLR 热更链路

入口：

```text
HybridClrLoader.LoadAsync()
```

Editor 分支：

- 直接反射调用 `HotUpdateEntry.Start`。

Runtime 分支：

1. `LoadAOTMetadataAsync()`
2. `LoadHotUpdateDllAsync()`，从 YooAssets 加载 `App.HotUpdate.dll`
3. 反射调用 `HotUpdateEntry.Start(IServiceContainer)`

## 接入时优先替换的点

新项目最先处理下面四件事：

- 修改 `YooAssetsRuntime` 的 CDN 地址常量。
- 将 `PatchOperationHandler.CheckVersionAsync()` 接入真实版本 API。
- 将 `HybridClrLoader.LoadAOTMetadataAsync()` 接入真实 HybridCLR metadata 加载。
- 将 `HotUpdateEntry` 中的占位逻辑替换为项目业务模块注册。

## 与 Holmas 当前项目的关系

这份文档只记录抽离骨架的稳定结构和更新链路。

如果要理解 Holmas 当前正式业务，应优先阅读：

- [Holmas 三层架构读码路线图](/Users/bruce/work/Holmas/doc/长期主文档/架构与边界/Holmas%20三层架构读码路线图.md)
- [热更新边界规范 v1](/Users/bruce/work/Holmas/doc/长期主文档/架构与边界/热更新边界规范_v1.md)
- [Holmas 当前游戏策划设计文档（基于现有实现）](/Users/bruce/work/Holmas/doc/长期主文档/方案与数据/Holmas%20当前游戏策划设计文档_基于现有实现.md)
