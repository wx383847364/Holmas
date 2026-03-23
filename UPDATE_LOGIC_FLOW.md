# 更新逻辑（抽离版）

## 启动顺序
1. `GameBootstrap.Start()`
2. 初始化基础设施：Logger / DI / Tick / EventBus / Persistence
3. 初始化平台桥接：`WeChatBridge`
4. 初始化网络层：`NetClient`
5. 初始化资源系统：`YooAssetsRuntime.InitializeAsync()`
6. 初始化热更加载：`HybridClrLoader.LoadAsync()`
7. 调用热更入口：`App.HotUpdate.Entry.HotUpdateEntry.Start(container)`

## YooAssets更新链路
入口：`YooAssetsRuntime.RunPatchFlowAsync(packageVersion)`

内部流程：
1. `PatchOperationHandler.CheckVersionAsync()`
2. `PatchOperationHandler.DownloadManifestAsync()`
3. `PatchOperationHandler.DownloadAssetsAsync()`
4. 成功后返回 `true`

说明：
- 当前 `CheckVersionAsync` 是模拟实现（TODO），需替换为真实版本服务。
- `YooAssetsRuntime` 包含 Editor 与 Runtime 两套分支逻辑。

## HybridCLR热更链路
入口：`HybridClrLoader.LoadAsync()`

- Editor：直接反射调用 `HotUpdateEntry.Start`。
- Runtime：
  1. `LoadAOTMetadataAsync()`（当前为占位）
  2. `LoadHotUpdateDllAsync()`（从 YooAssets 加载 `App.HotUpdate.dll`）
  3. 反射调用 `HotUpdateEntry.Start(IServiceContainer)`

## 需要你在新项目里优先改的点
1. `YooAssetsRuntime` 的 CDN 地址常量。
2. `PatchOperationHandler.CheckVersionAsync()` 对接你的版本API。
3. `HybridClrLoader.LoadAOTMetadataAsync()` 接入真实 HybridCLR metadata 加载。
4. `HotUpdateEntry` 中替换为你的业务模块注册。
