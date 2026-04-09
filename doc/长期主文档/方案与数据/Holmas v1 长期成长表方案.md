# Holmas v1 长期成长表方案

## Summary

`v1` 的长期成长正式改为配置驱动，并与“城市宣传系统”统一语义。

本方案锁定以下结论：

- `Holmas_MetaLevelTable` 使用 `playerLevel` 承载玩家等级门槛与长期成长参数
- 宣传升级配置拆到独立表 `Holmas_AgencyBuildingTable`
- 玩家经验来源统一收口为“宣传升级成功事件”
- 每次任意宣传升级成功，玩家固定获得 `1` 点经验
- 任务、地图、离线结算不直接提供玩家经验
- 长期成长表继续进入现有核心配置导出包，不额外拆第三个 bytes 文件

当前要对齐的基础规则是：

- 总城市阶段数为 `100`
- 每个城市固定 `4` 个宣传功能
- 每个宣传功能固定升级 `5` 次
- 因此单个城市总共有 `20` 次升级机会
- 整个 `v1` 总共有 `100 x 20 = 2000` 点可获得经验

## 完成情况

- 当前状态：进行中
- 进度说明：已完成等级经验与长期成长配置规则定义，待运行时实现和存档链完全对齐。

## 当前定义

长期成长运行时状态继续保留这些字段：

- `Experience`
- `AgencyLevel`
- `CompletedMapCount`
- `ClaimedTaskCount`
- `OfflineRewardTotal`
- `LastOfflineSettlementAtUtcMilliseconds`
- `CatDiscoveryCounts`

其中经验规则统一调整为：

- 宣传升级经验：每次升级任意一个宣传功能获得 `1` 点经验
- 离线奖励：继续由 `offlineRewardPerHour` 控制，只产出货币或其他外部收益
- 广告解锁时长：继续由 `adUnlockHours` 控制

明确约束：

- 玩家经验不再来自任务领奖
- 玩家经验不再来自地图完成
- 玩家经验不再来自离线结算
- 玩家经验只来自宣传升级成功

## 表结构

建议继续使用两张原始 xlsx：

- `Assets/Config/Holmas_MetaLevelTable.xlsx`
- `Assets/Config/Holmas_AgencyBuildingTable.xlsx`

`Holmas_MetaLevelTable.xlsx` 字段固定为：

- `playerLevel`
  - 玩家等级，范围 `1-20`
- `minExperience`
  - 达到该玩家等级所需的累计经验门槛
- `offlineRewardPerHour`
  - 每小时离线奖励
- `adUnlockHours`
  - 当前玩家等级广告槽位单次解锁持续时间
- `notes`
  - 策划备注列，不进入运行时导出

运行时只消费前 `4` 个字段，`notes` 仅保留在原始 xlsx 中。

`Holmas_AgencyBuildingTable.xlsx` 语义同步改为宣传系统：

- `agencyStageId`
  - 当前城市阶段标识，按城市一行组织宣传升级配置
- `stageName`
  - 城市名称
- `promotionIds`
  - 当前城市下固定的宣传功能集合
- `promotionLevelCaps`
  - 与 `promotionIds` 对齐，固定为 `5;5;5;5`
- `promotionUpgradeCosts`
  - 与 `promotionIds` 对齐的二维费用数组，每个宣传功能内部按 `5` 次升级依次列出金币消耗
- `notes`
  - 策划备注列，不进入运行时导出

宣传升级规则固定为：

- 玩家经验输入来自“城市宣传升级事件”
- 每完成一次单独宣传升级动作，固定获得 `1` 点玩家经验
- 同一城市阶段总共有 `20` 次经验事件
- `100` 个城市阶段总共提供 `2000` 次经验事件

## 等级关系

从 `v1` 长期成长表开始，玩家升级门槛与宣传配置拆分为两套配置来源。

规则固定为：

- 外层需要任务/地图等级时，统一消费玩家等级语义
- `Holmas_MetaLevelTable` 负责 `playerLevel -> minExperience / offlineRewardPerHour / adUnlockHours`
- `Holmas_AgencyBuildingTable` 负责城市阶段下的宣传功能集合、升级级数和费用
- 本轮不要求运行时字段名立即全部切换，只要方案语义统一
- 本轮不额外定义“玩家等级必须始终等于城市阶段等级”

这样处理的原因：

- 避免 `agencyLevel` 同时承担等级门槛和宣传配置两种职责
- 让等级门槛与宣传成长各自独立配置，后续更容易扩展
- 保留宣传系统独立推进节奏，不提前锁死双等级完全绑定

## 分层与数值节奏

玩家等级继续按 `20` 级设计，但累计经验门槛要与总经验上限 `2000` 对齐。

等级分 `4` 段：

- `Lv1-5`
  - 新手阶段，升级较快，用于建立前期反馈
- `Lv6-10`
  - 成长阶段，逐步拉开升级间隔
- `Lv11-15`
  - 中期阶段，开始要求持续投入多个城市
- `Lv16-20`
  - 后期阶段，覆盖长期完成目标

推荐累计经验门槛如下：

- `Lv1-20 minExperience`
- `0, 40, 85, 135, 190, 250, 320, 400, 490, 590, 700, 825, 965, 1120, 1290, 1475, 1675, 1840, 1930, 2000`

这组门槛的含义是：

- `Lv1` 从 `0` 经验开始
- `Lv20` 对应完整跑完 `100` 城市宣传升级后的总经验 `2000`
- 前期升级更快，后期升级更慢，但不会超过 `v1` 总经验池

各成长参数建议为：

- `buildingUpgradeExp`
  - 固定解释为“每次宣传升级经验”
  - 全等级统一为 `1`

- `offlineRewardPerHour`
  - `6, 6, 7, 7, 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 22, 24, 27, 30, 33, 36`

- `adUnlockHours`
  - 全等级统一为 `24`

## 运行时公式

当前默认代码中的成长口径，统一替换成文档明确规则加表驱动：

### 宣传升级经验

- `promotionUpgradeExp = 1`
- 每次完成一次单独宣传升级动作时结算一次

### 宣传升级次数

- 当前城市阶段的经验事件次数固定为 `20`
- 公式可表达为：
  - `sum(all promotion level caps in Holmas_AgencyBuildingTable for current agencyStageId)`
- 在当前方案下恒等于：
  - `5 + 5 + 5 + 5 = 20`

### 任务 / 地图 / 离线结算

- 任务领奖、地图完成、离线结算不直接增加玩家经验
- 它们仍可继续产出货币、地图结算收益或其他外部资源

### 离线奖励

- `offlineReward = floor(offlineHours * offlineRewardPerHour)`

### 广告解锁时长

- `unlockExpireAt = now + adUnlockHours`

## 接入要求

本方案要求扩展以下配置结构：

- `HolmasMetaProgressionDefinition`
  - 从仅包含 `AgencyLevel + MinExperience`
  - 扩展为 `playerLevel + minExperience + offlineRewardPerHour + adUnlockHours`

- `IHolmasMetaCatalog`
  - 需要改为按 `playerLevel` 取定义

- 默认成长策略实现
  - 需要改为消费宣传升级输入，并分别读取等级门槛表与宣传升级表

- `HolmasGameBootstrap`
  - 不再只兜底 `AgencyLevel=1`
  - 要改为同时加载完整成长表和宣传升级表

- 现有导表链
  - 在 `Xlsx导出二进制` 流程中加入 meta 表与宣传升级表导出
  - 继续合并进核心配置包，不额外拆第三个 bytes 文件

## 测试要求

落地时必须补并通过这些验证：

- `playerLevel` 唯一且从 `1` 连续到 `20`
- `minExperience` 严格递增
- `Lv20.minExperience = 2000`
- `promotionUpgradeExp = 1`
- `adUnlockHours > 0`
- `offlineRewardPerHour >= 0`

- `agencyStageId` 唯一且从 `1` 连续到 `100`
- `stageName` 不重复
- `promotionIds`、`promotionLevelCaps`、`promotionUpgradeCosts` 数量对齐
- 每个宣传功能的费用档位数等于对应 `promotionLevelCaps`
- 所有升级费用为非负整数

- `Experience` 增长后，`playerLevel` 能按表正确提升
- 边界经验值不会跳错级
- 初始状态正确落在 `Lv1`

- 玩家升级任意一个宣传功能时，经验增加 `1`
- 完成某个城市阶段全部宣传升级后，累计经验增加值固定为 `20`
- 完成全部 `100` 城市宣传升级后，累计经验增加值固定为 `2000`
- 完成任务、完成地图、离线结算时，不直接增加玩家经验
- 离线奖励按小时速率计算
- 广告槽位解锁时长从表里读取，而不是继续写死 `24` 小时

- `playerLevel` 提升后，任务补位和地图请求继续使用同等级配置
- 与当前内容表、地图表及宣传升级表组合后，`Xlsx导出二进制` 和 `run_holmas_validation.sh` 继续通过

## 本轮不做

- 不接 UI
- 不扩博彩任务
- 不把 icon 资源完整性当长期成长表的前置阻塞
- 不拆第二套等级体系
- 不新增独立的 meta bytes 文件

## 默认结论

这份方案的默认实现顺序是：

1. 新增长期成长原始 xlsx
2. 新增城市宣传升级原始 xlsx
3. 将两张表纳入现有 `xlsx -> json / bytes` 导出链
4. 扩展 `HolmasMetaProgressionDefinition` 与宣传升级读取结构
5. 接入宣传升级驱动的经验输入，并替换默认经验源与 `1` 级兜底 meta catalog
6. 复用现有内容表做整体验证

在这套方案落地前，当前展示资源只作为占位，不阻塞长期成长表主线推进。
