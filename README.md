# UnityProject_CoreExtract

本目录是从当前项目抽离出的“项目结构 + 更新逻辑”最小骨架，已移除具体游戏业务（战斗、技能、角色成长、UI业务等）。

## 你能直接复用的内容
- AOT 启动骨架（`GameBootstrap`）
- 基础设施（DI、日志、Tick、事件总线、持久化）
- 网络基础层（`NetClient` + `HttpTransport`）
- 平台桥接占位（`WeChatBridge`）
- 资源更新链路（`YooAssetsRuntime` + `PatchOperationHandler`）
- HybridCLR 热更加载链路（`HybridClrLoader`）
- 热更层入口占位（`HotUpdateEntry`，无业务）

## 已明确剔除
- Battle 相关所有代码
- Skill / Effect / Timeline 等技能业务代码
- 具体玩法模块（Inventory/Role/Equipment/Settlement/Payment等）

## 快速接入到新Unity项目
1. 新建Unity项目。
2. 将本目录下 `Assets/` 全量拷贝到新项目 `Assets/`。
3. 在新项目安装依赖：`YooAsset`（以及你需要的 HybridCLR 工作流）。
4. 在首场景挂载 `GameBootstrap` 组件。
5. 根据你的环境修改 `YooAssetsRuntime` 里的 CDN 地址。
6. 在 HotUpdate 层逐步补业务模块（从 `HotUpdateEntry` 开始）。

更多细节见：
- `PROJECT_STRUCTURE.md`
- `UPDATE_LOGIC_FLOW.md`
