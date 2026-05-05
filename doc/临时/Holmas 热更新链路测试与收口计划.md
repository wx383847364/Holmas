# Holmas 热更新链路测试与收口计划

## Summary
- 当前基线：`check_boundary.sh`、`run_holmas_validation.sh`、`run_holmas_hotupdate_validation.sh` 均已通过；BootstrapScene 的 PlayMode probe 已跑到 `GameBootstrap -> YooAssetsRuntime -> HybridClrLoader -> HotUpdateEntry -> HolmasGameBootstrap -> UI/runtime`。
- 当前真实热更新进展：已接入 HybridCLR package/config、Holmas 专用生成/复制菜单、AOT metadata 保守清单、YooAssets 本地包构建与 IL2CPP player smoke 脚本。
- 当前真实热更新阻塞：临时工程已能解析 HybridCLR package，但本机/工程尚未执行 `HybridCLR/Installer...`，严格 IL2CPP player smoke 在生成阶段失败，未进入 player 构建。
- 运行时 UI prefab、UI 贴图依赖已迁到 `Assets/HotUpdateContent/Res/**`，字体源文件统一放在 `Assets/Res/Font`，本地包验证已覆盖 prefab、字体、图标、HotUpdate DLL 与 metadata。

## Key Changes
- 保持 `Assets/Scripts/App.AOT` 只做宿主、YooAssets、HybridCLR、平台与持久化；保持 `Assets/Scripts/App.Shared` 只放稳定 DTO/接口。若某个 Shared 类型需要热更改行为，迁回 `App.HotUpdate`，不要把玩法规则留在 Shared。
- 将正式热更资源统一迁到 `Assets/HotUpdateContent`：
  - `Assets/Res/Perfabs/UI` -> `Assets/HotUpdateContent/Res/Perfabs/UI`
  - `Assets/Res/Perfabs/Generated/Holmas/Portrait` -> `Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait`
  - `Assets/Resources/Fonts/NotoSansSC.ttf` -> `Assets/Res/Font/NotoSansSC.ttf`
  - 更新 generated binding 常量、UI prefab generator 模板根目录、相关测试期望。
- 增加独立热更新工具链，不改坏现有 `bash tools/validation/run_holmas_validation.sh`：
  - 新增 `tools/validation/run_holmas_hotupdate_validation.sh`
  - 新增 Editor 构建入口，负责复制/生成 HotUpdate DLL、AOT metadata、YooAssets 包和 batchmode probe。
  - 新增 `.gitignore` 条目排除 YooAssets 构建输出、HybridCLR 生成输出、临时 player/build 日志。
- 接入真实 HybridCLR：
  - 在 `Packages/manifest.json` 加官方 package `com.code-philosophy.hybridclr`，固定 `v8.11.0`。
  - 新增 `ProjectSettings/HybridCLRSettings.asset`，配置 `App.HotUpdate` 与 AOT metadata 清单。
  - `HybridClrLoader` 增加 metadata 文件清单和 `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly` 调用。
  - HotUpdate DLL 统一作为 `.bytes` 放入 `Assets/HotUpdateContent/Res/HotUpdate/App.HotUpdate.dll.bytes`，通过 YooAssets 地址加载。
- 接入真实 YooAssets 包测试：
  - 通过 `HolmasHybridClrBuildPipeline.ConfigureCollectors` 配置 `DefaultPackage`，收集 `Assets/HotUpdateContent/Config`、`Assets/HotUpdateContent/Res` 与 `Assets/Res/Font`。
  - `YooAssetsRuntime` 增加 `HOLMAS_YOO_OFFLINE_PLAYMODE`，用于 player smoke 从内置 YooAssets 包离线启动。
  - 构建输出只落在临时目录或明确的本地 build output，不提交资源包产物。

## Test Plan
- 保留并先跑现有基线：
  - `bash tools/validation/check_boundary.sh`
  - `bash tools/validation/run_holmas_validation.sh`
- 修复并固定 PlayMode probe：
  - 不带 `-quit` 运行 batchmode probe，让 probe 自己 `EditorApplication.Exit`
  - 失败时也必须退出并写 `result.json`
  - 修复或放宽“自动领奖状态文本”断言，使它反映当前设计口径
- 热更新包验证分三层跑：
  - Editor direct：证明现有编辑器直连链路仍绿，已通过 `run_holmas_validation.sh`
  - Editor offline package：YooAssets 从本地包加载 HotUpdateContent 资源和 DLL，已通过 `run_holmas_hotupdate_validation.sh`
  - IL2CPP/HybridCLR smoke：脚本已接入；当前阻塞在 `HybridCLR/Installer...` 未执行
- 失败归因按固定标记拆分：
  - YooAssets 包：包初始化、manifest、资源地址、prefab/font 缺失
  - HybridCLR DLL：DLL 地址、Assembly.Load、入口类型/方法
  - AOT metadata：metadata 文件缺失或 `LoadMetadataForAOTAssembly` 失败
  - 入口初始化：`HotUpdateEntry.Start`、容器依赖缺失
  - Holmas bootstrap/UI：配置加载、archive 恢复、UiRoot/Main 状态

## Assumptions
- 不提交临时构建产物，只提交必要代码、配置、测试脚本和文档。
- `Assets/Config/*.xlsx` 和 `Assets/Config/json/*` 作为编辑器源/导出中间产物保留在当前目录；运行时只认 `Assets/HotUpdateContent/Config/*.bytes`。
- HybridCLR 官方 Unity package 使用 `com.code-philosophy.hybridclr`，参考官方文档：[HybridCLR package manual](https://www.hybridclr.cn/en/docs/basic/com.code-philosophy.hybridclr)。
- 实施阶段先采用“主线程直做”；若迁移资源依赖或 HybridCLR player 构建暴露大范围问题，再按长期主文档启动复核 agent。

## 2026-04-30 更新

- 已新增 `Assets/Editor/Holmas/HotUpdate/HolmasHybridClrBuildPipeline.cs`，提供 HybridCLR settings 配置、严格生成复制、YooAssets collector/buildin 包构建。
- 已新增 `Assets/Editor/Holmas/HotUpdate/HolmasIl2CppPlayerSmoke.cs` 与 `tools/validation/run_holmas_il2cpp_player_smoke.sh`。
- 已确认 `run_holmas_il2cpp_player_smoke.sh --build-target StandaloneOSX` 失败点为 `HybridCLR has not been initialized. Run HybridCLR/Installer before strict generation or IL2CPP player smoke.`。
- 下一步是在可执行 HybridCLR 初始化的环境里跑 `HybridCLR/Installer...`，然后重跑 IL2CPP player smoke；通过后再进入微信真机/CDN 验证。
