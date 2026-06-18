# Holmas Foundation UI binding prefab 迁移计划

## 完成情况

- 当前状态：已完成
- 进度说明：Foundation UI binding prefab 第二阶段已完成；本轮补充静态 collector 强校验和下一阶段 UI flow/Foundation 边界评估。
- 最近更新：2026-06-18，Foundation UI binding prefab 第二阶段已完成；本轮补充静态 collector 强校验和下一阶段 UI flow/Foundation 边界评估。

## 目标

把 Holmas 第一阶段保留下来的 prefab 兼容层彻底收口：

- 将正式 UI prefab 上的旧 Holmas collector 序列化数据迁移到 Foundation collector。
- 删除 Holmas 旧 binding adapter。
- 保持现有 UI screen flow、`UiRoot`、`UiScreenService` 和业务 UI 流程不变。
- 让 Holmas 成为 `WX.Foundation.UI.Binding` 的直接消费者，而不是维护自己的 binding 基础设施。

## 背景

第一阶段已经完成公共 UI binding 原语接入：

- `UiBindingManifest` 改用 `WX.Foundation.UI.Binding.UiBindingManifest`。
- `UiRuntimeScreenDescriptor` 改用 `WX.Foundation.UI.Binding.UiRuntimeScreenDescriptor`。
- `UiScreenDefinition.BindingManifest` 已使用 Foundation 类型。
- `App.HotUpdate` 直接引用 `WX.Foundation.UI`。
- Foundation package 引用已切到可复现的 `v0.1.5` Git tag。

第一阶段暂时保留了这些 Holmas adapter：

- `App.HotUpdate.Holmas.UI.Binding.UiBindingEntry`
- `App.HotUpdate.Holmas.UI.Binding.UiReferenceCollector`
- `App.HotUpdate.Holmas.UI.Binding.UiBindingResolver`

保留原因是当时 prefab 仍序列化旧 Holmas collector，直接替换会影响脚本 GUID 和 prefab 字段保存。第二阶段就是移除这个原因。

## 迁移范围

本阶段只覆盖 UI binding 的 prefab 序列化层。

已迁移 prefab：

- `Assets/HotUpdateContent/Res/Perfabs/UI/MainPanel.prefab`
- `Assets/HotUpdateContent/Res/Perfabs/UI/BattlePanel.prefab`
- `Assets/HotUpdateContent/Res/Perfabs/UI/LoadingPanel.prefab`
- `Assets/HotUpdateContent/Res/Perfabs/UI/LeadbroadPanel.prefab`
- `Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab`

不在本阶段范围：

- 不迁移 `UiRoot`。
- 不迁移 `UiScreenService`。
- 不迁移页面流程和 screen flow。
- 不迁移 YooAssets、HybridCLR、Archive、Config、Leaderboard 业务逻辑、玩法、棋盘、找猫、新手引导。
- 不引入 YokiFrame。

## 已完成改动

### 1. prefab collector 迁移

正式 UI prefab 已不再挂旧 Holmas `UiReferenceCollector`，改为挂 Foundation collector。

迁移保持以下字段语义：

- binding key
- component type
- node path
- event name
- target reference

迁移期间不应改变 prefab 的视觉参数、布局、颜色、alpha、材质和层级结构。

### 2. 旧 Holmas adapter 删除

第二阶段提交已删除：

- `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Binding/UiBindingEntry.cs`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Binding/UiReferenceCollector.cs`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Binding/UiBindingResolver.cs`
- `Assets/Editor/Holmas/UiBindingEntryDrawer.cs`
- `Assets/Tests/UiFoundationAdapterTests.cs`

对应 `.meta` 和空目录 meta 也已同步删除。

### 3. 运行时消费 Foundation 类型

核心入口已直接使用 Foundation binding：

- `UiLoadedPrefabHandle.GetReferenceCollector()` 返回 Foundation `UiReferenceCollector`。
- `UiScreenController.BindingResolver` 使用 Foundation `UiBindingResolver`。
- Main / Battle / Loading / Leaderboard / AgencyMain binding 解析入口使用 Foundation resolver。

### 4. Editor authoring 工具同步

这些 authoring 工具已切到 Foundation collector / resolver：

- `Assets/Editor/Holmas/HolmasStaticBindingAuthoring.cs`
- `Assets/Editor/Holmas/BattlePanelStaticBindingAuthoring.cs`
- `Assets/Editor/Holmas/MainPanelStaticTaskTextAuthoring.cs`
- `Assets/Editor/Holmas/AgencyMainFormalPrefabAuthoring.cs`

### 5. 迁移工具

新增迁移工具：

- `tools/migrate_ui_binding_collectors.py`

用途：

- 扫描指定正式 UI prefab 的 legacy collector 和 Foundation collector 状态。
- 将旧 Holmas collector block 转换为 Foundation collector block。
- 使用稳定 fileID 生成，避免重复迁移和 fileID 冲突。

当前用途是 Holmas 第二阶段迁移专用工具，不是通用 Foundation 发布工具。

### 6. 静态 collector 强校验

正式 UI prefab 的 binding surface 进入强约束：

- Main / Battle / Loading / Leaderboard / AgencyMain View 的 editor authoring 入口遇到缺失 `UiReferenceCollector` 时直接报错。
- `MainPanelStaticTaskTextAuthoring` 刷新静态任务文本时要求 MainPanel 已有静态 collector。
- `AgencyMainFormalPrefabAuthoring` 保留新建正式 prefab 时显式创建 collector 的能力，因为这是 prefab 构建步骤，不是运行时或刷新兜底。

运行时页面 Controller 已继续通过完整 binding 校验保护正式 UI，缺 collector、entry target 丢失或 manifest 不匹配都会在页面绑定阶段暴露。

## 当前验收口径

后续接手时先检查：

```bash
git status --short --branch
git log --oneline -5
```

预期最近应能看到：

```text
[26000010] UI：迁移 prefab 绑定到 Foundation collector
[52000011] 修复：同步 Tuanjie 编辑器版本与微信小游戏预览构建路径
[63000008] 工具：增加提交标题 CI 校验
[26000009] UI：接入 Foundation UI binding v0.1.5
```

推荐静态检查：

```bash
rg -n "App.HotUpdate.Holmas.UI.Binding|Holmas.UI.Binding" Assets/HotUpdateContent/Script/App.HotUpdate Assets/Editor Assets/Tests
rg -n "UiBindingEntry|UiBindingResolver|UiReferenceCollector" Assets/HotUpdateContent/Script/App.HotUpdate Assets/Editor Assets/Tests
```

第二条仍会命中 Foundation 类型的短类名使用，这是正常的。重点确认不再引用 `App.HotUpdate.Holmas.UI.Binding` 命名空间。

推荐验证：

```bash
tools/validation/check_boundary.sh
tools/validation/run_holmas_validation.sh
tools/validation/run_holmas_playmode_probe.sh
```

如果验证失败，先判断是否与 Foundation collector 迁移有关。若是 binding resolve、prefab 缺 collector、entry target 丢失、manifest entry 对不上，应优先按本方案排查。

## 后续下一步

第二阶段已经收口，后续建议不要重复迁移 prefab collector。下一步按优先级处理：

1. 在 GitHub ruleset 中保留本地 hook 为主、CI check 为提示的策略，避免直接 push main 时被 PR 流程打断。
2. 继续观察 `tools/migrate_ui_binding_collectors.py` 是否还需要保留。若迁移完成后长期不再需要，可在后续清理批次删除。
3. 后续若要继续抽公共 UI 能力，应另开阶段评估 `UiRoot`、`UiScreenService`、screen flow 是否适合进入 Foundation。不要把这件事混入 binding prefab 迁移。

## 下一阶段 UI flow / Foundation 边界评估

评估结论：下一阶段可以开始，但不建议直接把 Holmas 的 `UiRoot`、`UiScreenService` 和 screen flow 整体搬入 Foundation。更稳妥的方向是先抽通用契约和适配边界，再让 Holmas 按阶段替换内部实现。

### 建议进入 Foundation 的部分

- `UiScreenKind`、`UiCachePolicy`、`UiNavigationState` 这类纯 UI 导航状态和枚举。
- `UiScreenDefinition` 的通用字段：screen id、prefab location、kind、cache policy、sheet group、transition input lock、click outside close。
- `IUiPrefabLoader` / loaded prefab handle 的通用加载契约。
- `UiScreenService` 的通用能力：注册 definition、按 kind 打开/关闭、缓存策略、导航状态更新、layer resolver。

这些部分与 Holmas 业务上下文关系较弱，Foundation 仓也已经存在同名或相近实现，适合做 API 对齐和增量增强。

### 暂时留在 Holmas 的部分

- `UiRoot` 的项目级 Canvas 参数、安全区结构、字体预加载、GM 手势、popup backdrop 视觉和 input blocker 具体层级。
- `HolmasFlowCoordinator`、启动默认页流程、Loading 到 Main 的业务切换。
- Page / Popup / Overlay Controller 的具体业务行为，例如 Main 打开 Battle / Leaderboard、Tutorial、GM 工具。
- YooAssets、HybridCLR、Archive、Config、Leaderboard 业务、玩法、棋盘、找猫、新手引导。

这些逻辑要么依赖 `HolmasApplicationContext` 和项目服务，要么直接承载游戏流程，不适合作为 Foundation 通用 API 的第一批迁移对象。

### 推荐阶段拆分

1. Foundation screen contract 对齐：比较 Holmas `UiScreenDefinition` / `UiScreenKind` / `UiCachePolicy` / `UiNavigationState` 与 `WX.Foundation.UI.Screens` 现有 API，补齐 Holmas 需要但通用的字段，保持向后兼容。
2. Holmas adapter 层试接：在 Holmas 内用小适配层消费 Foundation screen contract，但暂不替换 `UiRoot` 的业务搭建、字体、安全区和启动流程。
3. Service 行为对齐：把 Holmas `UiScreenService` 的 payload、controller attach、input lock、popup backdrop、overlay 单例语义逐项拆分，能通用的进入 Foundation，业务回调留在 Holmas。
4. 最后再评估 `UiRoot`：只有当 layer resolver、安全区、input blocker、popup backdrop 都形成通用扩展点后，才考虑 Foundation 化 root builder。

### 下一阶段验收口径

- Holmas 仍能复跑 `check_boundary.sh`、`run_holmas_validation.sh`、`run_holmas_playmode_probe.sh`。
- Foundation API 不要求 Holmas 引入 YokiFrame，不要求迁移 YooAssets / HybridCLR / Archive / Config / 玩法逻辑。
- UI screen flow 迁移必须能逐项回滚，不和 prefab collector、binding entry、视觉 prefab 修改混在一起。
- 若 Foundation 需要新增 API，应先在 ScrollworksFoundationKit 单独提交和验证，再在 Holmas 通过 package 版本或明确引用接入。

## 风险与注意事项

- 不要重新引入 Holmas `UI.Binding` adapter。
- 不要把 Foundation collector 迁移和页面流程迁移混在一个提交里。
- 不要在 View / Controller 中新增运行时节点查找来绕过 binding 缺失。
- 如果 prefab 上出现 Missing Script，优先检查 Foundation package 是否正确解析到 `com.wx.foundation.ui#v0.1.5` 或后续明确版本。
- 如果 GitHub ruleset 后续开启 PR required check，Codex 工作流需要改为分支 + PR，不再直接 push main。
