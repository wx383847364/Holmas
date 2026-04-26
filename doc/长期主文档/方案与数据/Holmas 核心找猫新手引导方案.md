# Holmas 核心找猫新手引导方案

## Summary

- 第一版采用非阻断式轻引导：新号首次进入 `Main` 主界面后，系统直接启动一张教程棋盘，并用浮层提示教玩家找猫。
- 引导范围覆盖核心找猫闭环：棋盘翻格、固定猫位、任务栏、Walk/Find 模式、体力、自动领奖、宣传升级入口和帮助入口。
- 教程棋盘是真实玩法局：找猫、体力消耗、任务进度、自动领奖都走现有 `HolmasGameplayRuntime` 链路。
- 教程框架按“步骤配置 + 条件监听 + 目标定位”组织，后续新增按钮说明或成长系统引导时，只追加步骤定义和目标引用，不重写控制器主流程。

## Key Changes

- 新增一个 `Overlay` 类型页面：`tutorial.core_find_cat.overlay`，放在 `App.HotUpdate/Holmas/UI/Screens/Tutorial`。
- 新增 `TutorialOverlayController / TutorialOverlayView / TutorialOverlayVm`：
  - `Controller` 读取步骤定义、监听 runtime/UI 事件、判断步骤完成、更新 VM，不写玩法规则。
  - `View` 只负责浮层、高亮框、文案、按钮、收起态入口和占位图。
  - `Vm` 固定承载当前步骤标题、正文、按钮文本、目标区域、目标格子、是否允许点击穿透、是否可跳过。
- 在 `MainView` 暴露只读引导目标引用：
  - `TaskBar`
  - `WalkToggle`
  - `FindToggle`
  - `EnergyArea`
  - `PromotionButton`
  - `HelpButton`
  - `BoardCell:{cellIndex}`
- 在 `MainPageController.OnOpen/OnResume` 后触发教程检查：
  - 如果教程未完成，直接启动固定教程棋盘并打开非阻断式 Overlay。
  - 如果已有未完成教程棋盘，恢复棋盘并继续教程。
  - 如果已有未完成普通棋盘，也强制切到固定教程棋盘；Main 内正式 `BoardContainer` 隐藏，教程 `TutorialBoardContainer` 显示，教程结束后再启动并显示正式棋盘。
- 新增 HotUpdate UI 层教程进度存储：
  - 使用现有 `IPersistence` 独立 key：`holmas.tutorial.core_find_cat.v1`。
  - 保存 `started/completed/skipped/currentStepIndex/currentStepId/completedStepIndex/completedStepId/updatedAt/completedAt` 等字段。
  - 通过 `CoreFindCatTutorialProgressService` 串行化并单调合并：`completed=true` 后普通步骤保存不能降级，步骤索引默认只能前进。
  - 旧版普通棋盘轻提示字段 `dismissedNormalBoardHint` 仅做存储兼容，不再驱动未完成教程入口。
  - 不写入任务、地图、奖励、体力等玩法状态。
- 新增 `CoreFindCatTutorialCoordinator`，负责教程入口判定、教程棋盘启动、Overlay payload 组装和图片配置加载；`MainPageController` 只转发按钮、显示 Overlay、刷新主界面。
- 在主界面增加两个教程入口：
  - `开始引导`：正式环境可见，点击后从第 `0` 步重新进入教程流程。
  - `?` 帮助：重看说明，不写完成进度。
- 开发/测试环境额外显示步骤输入框，默认 `0`；非法输入按 `0`，越界 clamp 到最后一步。正式环境不显示步骤输入框。
- `+5体力` 调试入口归入同一开发开关，正式环境隐藏且不绑定点击事件。
- AOT 层 `FilePersistenceProvider` 加固：
  - key 做文件名安全转换。
  - 保存先写 `.tmp` 再替换正式文件。
  - 读取兼容旧的未安全转换文件路径。
  - 文件 IO 失败时使用 PlayerPrefs base64 fallback。
  - 删除同时清理正式文件、临时文件和 PlayerPrefs。
- 新增 `TutorialVisualConfig` 图片配置资产和 Editor 工具：
  - 配置资产路径：`Assets/HotUpdateContent/Res/Tutorial/CoreFindCatTutorialVisualConfig.asset`。
  - 临时占位图路径：`Assets/HotUpdateContent/Res/Tutorial/Placeholder/tutorial_placeholder.png`。
  - Editor 工具路径：`Assets/Editor/Holmas/Tutorial/HolmasTutorialVisualConfigWindow.cs`。
  - 每个 StepId 可配置 `MainImagePath/DialogBackgroundPath/TipsIconPath/FingerIconPath/HighlightSpritePath/ArrowSpritePath`。
  - 运行时加载统一走 `IAssetsRuntime.LoadAssetAsync(path)`；缺图或加载失败时显示内置 fallback，不阻塞教程。

## Tutorial Board

教程棋盘用于保证第一段找猫体验可控、可高亮、可验证，但不污染正式地图生成规则。

- 固定 `mapId = tutorial_core_find_cat_v1`。
- 固定使用现有新手地形：`Assets/HotUpdateContent/Res/Map/11-8-8.asset`。
- 固定猫位：
  - 第一只猫：`cellIndex = 27`，即 `row = 3, col = 3`。
  - 第二只猫：`cellIndex = 44`，即 `row = 5, col = 4`。
- `cellIndex` 继续使用现有一维索引规则：`row * cols + col`。
- 猫种从当前任务栏活跃任务里取：
  - 启动教程棋盘前先补齐默认开启任务槽。
  - 按任务槽顺序读取当前活跃 `catId`。
  - 第一只猫使用第一个活跃任务猫。
  - 第二只猫使用第二个活跃任务猫；如果只有一个活跃猫，则复用第一个 `catId`。
  - 如果没有任何活跃任务猫，不启动教程棋盘，显示任务栏初始化异常提示。
- 教程棋盘不加入玩家等级 `MapIds + MapWeights`，普通关卡仍走现有配置和随机规则。
- 固定猫位只写入教程专用 `LevelSnapshot.SpawnedCats`，不写入 `MinesweeperTerrainData`。
- 教程棋盘可以显式写入 `SpawnedCatData.CatId` 以保证教学目标稳定；普通棋盘只预生成猫位，猫种在揭示时按当前未完成任务池解析。
- 启动时由教程服务加载 terrain，转换为 `BoardTemplate`，构造 `LevelSnapshot`，再调用 `HolmasGameplayRuntime.StartLevel(BoardTemplate, LevelSnapshot)`。

## Extensible Design

新增教程步骤模型，例如 `TutorialStepDefinition`：

- `StepId`
- `TargetKey`
- `Title`
- `Body`
- `TriggerCondition`
- `CompleteCondition`
- `AllowPassThroughInput`
- `CanSkip`
- `CollapsedHintText`
- `StepIndex`
- `VisualKey`
- `RequiresTutorialBoard`

`TargetKey` 统一通过 `MainView` 暴露的只读目标解析：

- `BoardCell:27`
- `TaskBar`
- `WalkToggle`
- `FindToggle`
- `EnergyArea`
- `PromotionButton`
- `HelpButton`

教程控制器只负责：

- 读取步骤定义。
- 监听 runtime/UI 事件。
- 判断步骤是否完成。
- 更新 Overlay VM。
- 在玩家收起引导后保留小提示入口。
- 通过 `CoreFindCatTutorialProgressService` 写入教程进度。

教程控制器不负责：

- 生成普通地图。
- 修改任务规则。
- 直接结算奖励。
- 遍历私有 UI 层级。
- 把教程状态写进玩法 runtime。
- 直接访问 `System.IO`、`PlayerPrefs` 或平台存储 API。

教程打开模式统一用 `TutorialRunMode` 区分：

- `FullTutorial`：新号或手动开始，进入完整步骤链。
- `Replay`：帮助入口重看，只显示说明，不写完成进度。
- `NormalBoardHint`：旧兼容模式，不再作为未完成教程入口；未完成教程应进入固定教程棋盘。
- `DebugStartAtStep`：开发/测试环境从指定步骤开始，允许强制重置步骤索引。

## Tutorial Steps

1. **找到第一只猫**
   - 触发条件：教程棋盘已启动，且 `cellIndex=27` 尚未揭示。
   - 高亮 `BoardCell:27`。
   - 允许点击穿透，不阻断玩家操作。
   - 文案：`这格里藏着第一只猫。点一下格子，看看线索怎么展开。`
   - 完成条件：`cellIndex=27` 被揭示，或棋盘中任意教程目标猫被找到。

2. **任务目标**
   - 触发条件：玩家找到至少一只教程猫。
   - 高亮 `TaskBar`。
   - 文案：`这里显示正在寻找的猫、进度和金币奖励。找到对应猫后会自动推进，完成后自动领奖。`
   - 完成条件：玩家停留阅读后点击下一步，或首个任务完成。

3. **选择模式**
   - 高亮 `WalkToggle / FindToggle`。
   - 文案：`行走模式适合试探：翻到普通格不耗体力，遇到猫耗 2 点。寻找模式更直接：每翻一个有效格耗 1 点。`
   - 完成条件：玩家切换任意模式，或点击下一步。

4. **继续找猫**
   - 高亮棋盘区域，优先高亮 `BoardCell:44`。
   - 允许点击穿透。
   - 文案：`继续翻格寻找隐藏猫。数字会提示附近线索；找到本局全部猫后，会自动进入下一局。`
   - 完成条件：首个任务完成并触发自动领奖，或教程棋盘完成。

5. **体力提示**
   - 高亮 `EnergyArea`。
   - 文案：`翻格需要体力。体力会随时间恢复；体力不足时不能继续翻格。`
   - 完成条件：玩家点击下一步。

6. **金币用途**
   - 触发条件：首个任务完成并自动领奖后。
   - 高亮 `PromotionButton`。
   - 文案：`任务奖励会变成金币。金币可以用于宣传升级，让侦探社继续成长。`
   - 完成条件：玩家点击下一步；不要求实际点击升级按钮。

7. **帮助入口**
   - 高亮 `HelpButton`。
   - 文案：`之后想重看找猫说明，可以点这里打开帮助。`
   - 完成条件：玩家点击完成，并写入教程完成标记。

## Non-Blocking Rules

- Overlay 默认不使用全屏输入阻断，棋盘和主界面按钮仍可正常点击。
- 高亮表示推荐目标，不表示唯一可点区域。
- 玩家点到非目标格不失败、不重置教程。
- 玩家提前完成任务或通关教程棋盘时，教程按已满足的完成条件向后推进。
- 每一步都可以收起或跳过；收起后保留小提示入口，避免长期遮挡操作。
- 跳过整段教程时，只写入教程完成标记，不回滚或改写当前玩法状态。
- 已有普通未完成棋盘时，未完成教程仍强制进入固定教程棋盘；普通棋盘 UI 在教程期间隐藏，教程完成或跳过后再启动并显示正式棋盘。
- 只有教程棋盘流程完成，或玩家显式点击“跳过教程”，才写 `completed=true`。
- 图片资源加载失败、目标 Rect 缺失、存储写入失败都不能卡死教程；应降级为纯色/无高亮/仅内存流程提示。

## Test Plan

- 单元测试教程存储：
  - 首次无数据时返回未完成。
  - 标记完成后再次读取返回已完成。
  - 存储损坏、空字节或缺少步骤字段时按未完成处理，不阻塞主流程。
  - `completed=true` 后再保存旧步骤，最终仍保持完成。
  - 步骤索引只能前进；开发强制启动除外。
  - 旧版 `dismissedNormalBoardHint=true` 不能等同于 `completed=true`。
- AOT 存储测试：
  - key 安全转换后不生成非法路径。
  - 文件写入失败时 fallback 到 PlayerPrefs。
  - load 优先读文件，再读 PlayerPrefs。
  - delete 同时清理文件与 PlayerPrefs。
  - 旧文件路径仍可读取。
- 图片配置测试：
  - `TutorialVisualConfig` 覆盖所有教程步骤。
  - 配置路径必须位于 `Assets/HotUpdateContent/Res/`。
  - 临时占位资源可被 Editor 测试加载。
  - 运行时缺图或加载失败时显示 fallback。
- 固定棋盘测试：
  - 教程启动后 `CurrentLevelSnapshot.MapId == tutorial_core_find_cat_v1`。
  - `SpawnedCats` 固定包含 `cellIndex=27` 和 `cellIndex=44`。
  - 两个固定格必须是 `11-8-8.asset` 的有效格。
  - 教程猫种来自当前任务栏活跃猫池。
  - 教程棋盘不修改 `MinesweeperTerrainData`，不加入普通地图权重池。
- UI/流程测试：
  - 新号首次进入 `Main` 后自动打开教程 Overlay，并自动启动教程棋盘。
  - 不出现旧的“开始找猫/继续找猫”开局按钮相关步骤。
  - 主界面 `开始引导` 按钮可以手动重开教程。
  - 正式环境不显示步骤输入框和 `+5体力` 调试入口。
  - 开发环境显示步骤输入框，非法/负数/越界输入都能稳定进入可用步骤。
  - Overlay 展示时，玩家仍能点击棋盘和主界面按钮。
  - 玩家点非目标格不失败、不重置教程。
  - 玩家找到目标猫后，教程自动进入下一步。
  - 首个任务完成并自动领奖后，教程继续介绍主界面按钮功能。
  - 点击“跳过”关闭并写入完成标记。
  - 已完成玩家再次进入 `Main` 不自动弹出。
  - 点击帮助入口可以强制重看教程。
- 边界检查：
  - `Tutorial` 流程代码只在 `App.HotUpdate` UI/应用层；`App.AOT` 只承载通用持久化加固。
  - 不把教程完成状态塞进玩法 runtime。
  - 不把固定猫位写回 terrain 资产。
  - Overlay 不进入 Page 历史栈，不影响 `BackAsync()` 的页面返回语义。
  - Editor 工具只放在 `Assets/Editor`，运行时代码不引用 `UnityEditor`。
- 人工验收：
  - 竖屏真机或 Play Mode 下确认浮层不挡住主要棋盘点击。
  - 确认中文文案不溢出，收起入口在小屏上仍可点。
  - 确认暂无图片资源时占位表现可接受，后续图片替换位清晰。

## Assumptions

- 第一版采用非阻断式轻引导，不做强制点击路线。
- 教程棋盘是真实玩法局，找猫、体力消耗、任务进度、自动领奖都走现有正式 runtime。
- 玩家完成首个任务后，教程继续讲按钮功能；不要求玩家必须实际点击每个按钮才算完成。
- 固定猫位只服务教程棋盘，普通关卡继续按现有配置和随机规则生成。
- 第一版不讲离线收益、广告槽完整规则，只在金币用途步骤带到宣传升级。
- 后续图片资源进入后，替换每一步的插图/猫图占位，不改变步骤模型和存储 key。
- `Battle` 页保持兼容，不作为第一版新手引导主路径；当前正式体验以 `Main` 内嵌找猫为准。
