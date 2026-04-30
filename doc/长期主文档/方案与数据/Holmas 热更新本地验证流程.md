# Holmas 热更新本地验证流程

## 目标

本流程用于验证 Holmas 在本地热更模式下的最小闭环：

- `Assets/HotUpdateContent` 作为热更资源与热更代码内容根。
- YooAssets 构建 `DefaultPackage` 本地包，并能从包内加载配置、地图、UI prefab、字体、图标、HotUpdate DLL 与 AOT metadata。
- BootstrapScene 在 PlayMode/batchmode 下走 `GameBootstrap -> YooAssetsRuntime -> HybridClrLoader -> HotUpdateEntry -> HolmasGameBootstrap -> UI/runtime`。

当前阶段已接入 HybridCLR package/config 与 IL2CPP player smoke 脚本；本机验证仍受限于是否已执行 `HybridCLR/Installer...`。远端 CDN 与微信真机仍不在本地脚本覆盖范围内。

## 热更内容边界

运行时正式内容应放在 `Assets/HotUpdateContent` 下：

- 配置运行时产物：`Assets/HotUpdateContent/Config/*.bytes`
- 地图、图标、教程、UI prefab、字体：`Assets/HotUpdateContent/Res/**`
- 热更脚本源码：`Assets/HotUpdateContent/Script/App.HotUpdate/**`
- 测试期临时复制的 DLL/metadata 目标：`Assets/HotUpdateContent/Res/HotUpdate/**`

`Assets/Config/*.xlsx` 仍是编辑器源表，不作为运行时热更加载入口。

## HybridCLR 正式配置

项目已固定接入官方 HybridCLR package：

- `Packages/manifest.json`：`com.code-philosophy.hybridclr` -> `https://github.com/focus-creative-games/hybridclr_unity.git#v8.11.0`
- `ProjectSettings/HybridCLRSettings.asset`
  - `hotUpdateAssemblies`: `App.HotUpdate`
  - `patchAOTAssemblies`: `mscorlib`, `System`, `System.Core`, `UnityEngine.CoreModule`, `UnityEngine.UI`, `Unity.TextMeshPro`, `App.Shared`
  - HotUpdate DLL 输出根：`HybridCLRData/HotUpdateDlls`
  - AOT metadata 输出根：`HybridCLRData/AssembliesPostIl2CppStrip`

Holmas 专用菜单：

- `Holmas/HotUpdate/Configure HybridCLR Settings`
- `Holmas/HotUpdate/Generate And Copy HybridCLR Assets`
- `Holmas/HotUpdate/Prepare Local Validation Assets`
- `Holmas/Validation/Build IL2CPP Player Smoke`

严格 HybridCLR 流程会执行 `HybridCLR/Generate/All`，再把：

- `HybridCLRData/HotUpdateDlls/{BuildTarget}/App.HotUpdate.dll`
- `HybridCLRData/AssembliesPostIl2CppStrip/{BuildTarget}/{AOT}.dll`

复制到：

- `Assets/HotUpdateContent/Res/HotUpdate/App.HotUpdate.dll.bytes`
- `Assets/HotUpdateContent/Res/HotUpdate/Metadata/*.dll.bytes`

这些 DLL、metadata、YooAssets buildin 包、player 输出都视为本地构建产物，不提交。`.gitignore` 已忽略 `HybridCLRData/`、`HybridCLRGenerate/`、`Assets/StreamingAssets/yoo/`。

## 一键验证

优先使用新增热更专项脚本：

```bash
bash tools/validation/run_holmas_hotupdate_validation.sh
```

脚本会复制当前工程到 `/private/tmp` 临时目录，再在临时工程内执行：

1. `bash tools/validation/check_boundary.sh`
2. 调用 `HolmasHybridClrBuildPipeline.PrepareHotUpdateAssetsForLocalValidation`
   - 若 HybridCLR package 已安装且已完成 `HybridCLR/Installer...`，走严格 `HybridCLR/Generate/All` + 复制流程
   - 否则仅用于 Editor 本地包验证，回退复制 `Library/ScriptAssemblies` 中可用的 DLL/metadata
3. 准备 `Assets/HotUpdateContent/Res/HotUpdate/App.HotUpdate.dll.bytes` 与可用 AOT metadata `.bytes`
4. 配置 YooAssets collector：`Assets/HotUpdateContent/Config` 与 `Assets/HotUpdateContent/Res`
5. 构建 YooAssets `DefaultPackage` 到临时工程的 `Library/HolmasHotUpdate/YooBuild`
6. PlayMode 下用 OfflinePlayMode 从 `Library/HolmasHotUpdate/Buildin/DefaultPackage` 加载包内容
7. 执行 BootstrapScene PlayMode probe

验证通过后，脚本会删除本次创建的临时工程；失败时会保留临时工程和 `/tmp/holmas_hotupdate_validation_*` 日志。

## 基线验证

热更专项之外，仍需保证原有验证脚本语义不变：

```bash
bash tools/validation/check_boundary.sh
bash tools/validation/run_holmas_validation.sh
```

## IL2CPP Player Smoke

新增严格 player smoke 入口：

```bash
bash tools/validation/run_holmas_il2cpp_player_smoke.sh --build-target StandaloneOSX
```

脚本会复制工程到 `/private/tmp`，在临时工程中：

1. 要求 HybridCLR package 可用且已执行 `HybridCLR/Installer...`
2. 执行严格 HybridCLR 生成与复制流程
3. 构建 YooAssets `DefaultPackage` 到 `Assets/StreamingAssets/yoo/DefaultPackage`
4. 给 player 构建追加 `HOLMAS_YOO_OFFLINE_PLAYMODE`
5. 使用 IL2CPP 构建 standalone player
6. 启动 player，并在日志里等待 `HybridClrLoader: HybridCLR热更代码加载完成` 或 `GameBootstrap: 初始化完成`

2026-04-30 本机尝试结果：

- `run_holmas_il2cpp_player_smoke.sh --build-target StandaloneOSX --keep-temp-on-success` 未进入 player 构建阶段。
- 失败点：临时工程已解析 HybridCLR package，但当前机器/工程尚未执行 `HybridCLR/Installer...`，严格生成报错 `HybridCLR has not been initialized. Run HybridCLR/Installer before strict generation or IL2CPP player smoke.`
- 下一步：在可写且允许初始化 HybridCLRData 的 Unity/Tuanjie 环境执行 `HybridCLR/Installer...`，再重跑 IL2CPP smoke；通过后再推进微信真机或 CDN 验证。

## 已知边界

- `HybridClrLoader` 的非 Editor 真实加载路径已固定为 `Assets/HotUpdateContent/Res/HotUpdate/App.HotUpdate.dll.bytes` 和 `Assets/HotUpdateContent/Res/HotUpdate/Metadata/*.dll.bytes`。
- AOT metadata 清单已扩展到当前目标平台的保守真实清单：`mscorlib`、`System`、`System.Core`、`UnityEngine.CoreModule`、`UnityEngine.UI`、`Unity.TextMeshPro`、`App.Shared`。
- 本地热更专项验证仍允许 Editor fallback；严格 IL2CPP/player smoke 必须先完成 `HybridCLR/Installer...`。
- 真机微信环境和远端 CDN 仍是后续验收项。
