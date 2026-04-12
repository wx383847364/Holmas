# Holmas v1 本地模拟服务器与角色档案方案

**Summary**

- 这轮按项目规则判定为：主线程直做设计，不启动实现型真实 subagent；只在方案完成后启一个 `Agent 6` 风格的只读审查 subagent 做挑刺复核。复核结论是：方向可行，但必须补齐“断点续玩流转”“档案根结构”“保存失败与合并写入策略”这三块。
- 第一阶段只做“本地 authoritative archive”，不接 `NetClient` 假 HTTP。服务器下发数据在本地表现为一份固定结构的玩家档案，启动时读取，运行中持续回写。
- 覆盖范围固定为：长期角色进度、任务栏、当前未完成关卡。默认档案固定为“纯新号”：`PlayerLevel=1`、`AgencyStageId=1`、`Experience=0`、`Gold=0`、两格默认槽位开启、首次启动自动补任务、无当前关卡。

**Key Changes**

- `App.Shared` 新增服务器档案根 DTO，作为未来真服返回结构的本地镜像：
  - `HolmasPlayerArchiveRoot`
  - `HolmasProgressionArchiveData`
  - `HolmasTaskBarArchiveData`
  - `HolmasTaskRuntimeArchiveData`
  - `HolmasArchiveCounterEntry`
  - `HolmasPromotionLevelEntry`
- `HolmasPlayerArchiveRoot` 固定字段：
  - `playerId`
  - `schemaVersion`
  - `revision`
  - `savedAtUtcMilliseconds`
  - `progression`
  - `taskBar`
  - `currentLevel`
- 继续复用现有 `TaskInstanceData`、`TaskSlotState`、`LevelSnapshot`；`LevelSnapshot` 只是档案中的一段，不单独充当整份角色数据。
- `App.HotUpdate` 新增 `PlayerData` 模块，职责固定：
  - `IHolmasPlayerArchiveGateway`：加载/保存 archive，不暴露玩法规则。
  - `HolmasLocalMockServerGateway`：基于 `IPersistence` + JSON 的本地假服实现。
  - `HolmasPlayerArchiveMapper`：`archive <-> HolmasMetaProgressionState / HolmasTaskBarState / LevelSnapshot` 双向转换。
  - `HolmasPlayerArchiveSyncService`：订阅运行时变更、维护 dirty 状态、执行单飞保存。
- `AOT` 不新增任何玩法规则；只继续提供 `IPersistence`。新号默认值、首次补任务、档案回退策略都放在 HotUpdate。
- `HolmasGameplayRuntime` 增加三类能力：
  - 构造时接收初始 `MetaProgressionState` 与 `TaskBarState`。
  - `RestoreLevelAsync(LevelSnapshot snapshot)`，按 `terrainPath` 恢复当前局。
  - `StateChanged` 事件，翻格、插旗、领奖、升级、离线结算、开新局、结算当前局、结束当前局时统一发脏信号。
- 保存策略固定为“单飞合并写入”，不是每次同步落盘：
  - 任意状态变更只标记 dirty。
  - 若当前没有进行中的保存，就导出最新 archive 并异步保存。
  - 保存期间再次变更，只保留一个 pending dirty；当前保存结束后再补一轮最新快照。
  - 保存失败时保留 dirty，不清空内存态；下一次状态变更或下次启动继续重试。
- 启动流程固定：
  - 先读本地 archive。
  - 读不到、损坏、`schemaVersion` 不兼容时，回退生成默认 archive 并记录 warning。
  - 用 archive 还原 `GameplayRuntime`。
  - 若 archive 含未完成 `currentLevel`，启动时先恢复该局；否则不自动开新局。
  - 只有“已解锁槽位为空且 archive 中无任务”时，才执行首次补任务；禁止恢复档案后再被启动逻辑二次补位污染。
- UI/流程固定改法：
  - `StartBattleAsync` 分成两条路径：有未完成局则继续，没有才新开局。
  - `ExitBattleToMainAsync` 遇到未完成局不清 session，只释放表现层；只有已完成局才清 `CurrentLevelSession`。
  - 这样主界面返回战斗等于断点续玩，不再默认为放弃当前局。
- 明确保留 `Holmas_v1` 规则：
  - 玩家经验只来自建筑升级。
  - 任务领奖、地图完成、离线结算不直接加经验。
- 本阶段固定不持久化旗标状态：
  - 只恢复 `LevelSnapshot` 里已有的 `revealedCells`、`spawnedCats`、`completed`。
  - 重新进入当前局时，旗标全部重置。
  - 这是显式产品规则，不是遗漏。

**Test Plan**

- 冷启动无 archive：生成纯新号；再次启动能读回同一份角色状态。
- 冷启动有 archive：正确恢复等级、经验、金币、任务栏、广告槽位到期时间、当前未完成关卡。
- 恢复档案后，启动链不会重复补任务或覆盖现有任务栏。
- `StateChanged -> archive save` 覆盖：翻格、插旗、领奖、升级、离线结算、开新局、结束当前局。
- 保存合并策略：连续多次翻格只产生单飞串行保存，最终磁盘内容等于最新内存态。
- 保存失败策略：失败后不丢当前内存状态；后续再次触发保存可恢复。
- 战斗流：未完成局返回主界面后再次进入会继续原局；已完成局返回后再次进入会新开一局。
- 旗标规则：恢复当前局后，已翻开格保留、旗标重置。
- 成长规则回归：建筑升级加 1 经验；地图完成、领奖、离线结算经验仍为 0。

**Assumptions**

- 第一阶段不做网络层假接口，不改 `NetClient` 协议面。
- archive `schemaVersion` 固定独立版本，例如 `holmas.v1.local-mock`；旧版本或坏档案直接安全回退，不做复杂迁移。
- `playerId` 固定为单本地账号；暂不做多角色、多区服、多存档位。
- 当前局恢复以 `LevelSnapshot` 为准；若未来要保旗标，再单独扩 `LevelSnapshot`，本轮不提前改 `Holmas_v1` 公共数据定义。
- 如果后续进入实现阶段，再按 `Agent 1` 冻结 Shared archive DTO、`Agent 3` 落 HotUpdate 存档/进度实现、`Agent 5` 补测试，最后由 `Agent 6` 复审。
