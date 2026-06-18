# Holmas Foundation UI binding prefab 迁移计划

## 完成情况

- 当前状态：已完成
- 进度说明：Holmas UI binding 第二阶段已完成 prefab 序列化层迁移。正式 UI prefab 已从旧 Holmas `UiReferenceCollector` 迁移到 `WX.Foundation.UI.Binding.UiReferenceCollector`，旧 Holmas binding adapter 已删除。
- 最近更新：2026-06-18 完成第二阶段迁移提交 `[26000010] UI：迁移 prefab 绑定到 Foundation collector`，并补充本文档作为后续工作入口。

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
3. 审查 Main / Battle / Loading / Leaderboard / AgencyMain View 中 `GetComponent<UiReferenceCollector>() ?? AddComponent<UiReferenceCollector>()` 的兜底逻辑：
   - 如果仍需要支持测试或临时运行时创建的 View，可以保留。
   - 如果决定强制所有正式 UI 都必须 prefab 静态挂载 collector，可以把兜底改成明确报错。
4. 后续若要继续抽公共 UI 能力，应另开阶段评估 `UiRoot`、`UiScreenService`、screen flow 是否适合进入 Foundation。不要把这件事混入 binding prefab 迁移。

## 风险与注意事项

- 不要重新引入 Holmas `UI.Binding` adapter。
- 不要把 Foundation collector 迁移和页面流程迁移混在一个提交里。
- 不要在 View / Controller 中新增运行时节点查找来绕过 binding 缺失。
- 如果 prefab 上出现 Missing Script，优先检查 Foundation package 是否正确解析到 `com.wx.foundation.ui#v0.1.5` 或后续明确版本。
- 如果 GitHub ruleset 后续开启 PR required check，Codex 工作流需要改为分支 + PR，不再直接 push main。

