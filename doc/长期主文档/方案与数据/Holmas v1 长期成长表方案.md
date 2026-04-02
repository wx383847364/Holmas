# Holmas v1 长期成长表方案

## Summary

v1 的长期成长正式改为配置驱动，不再继续沿用默认写死策略。

本方案锁定这几个决策：

- `Holmas_MetaLevelTable` 使用 `playerLevel` 承载玩家等级门槛与长期成长参数
- 侦探社建筑升级配置拆到独立表，不再混在 meta 表中
- 成长曲线采用“前快后慢”
- 本轮只补长期成长数值，不接 UI，不扩赌博任务
- 长期成长表进入现有核心配置导出包，不单独拆第三个 bytes 文件

目标是把当前只包含 `AgencyLevel=1` 的兜底配置，推进成可和 20 级任务表、地图表以及侦探社建筑升级表一起工作的正式 v1 配置。

## 当前现状

当前代码中的长期成长运行时状态已经具备基础字段：

- `Experience`
- `AgencyLevel`
- `CompletedMapCount`
- `ClaimedTaskCount`
- `OfflineRewardTotal`
- `LastOfflineSettlementAtUtcMilliseconds`
- `CatDiscoveryCounts`

当前缺口主要有两类：

- 配置层只有 `AgencyLevel + MinExperience`
- 建筑升级表、离线收益和广告解锁时长都仍未正式落表或落文档

当前默认公式为：

- 建筑升级经验：每次升级任意一个侦探社功能获得 `1` 点
- 离线奖励：每 10 分钟 1 点
- 广告解锁时长：24 小时

其中玩家经验来源改为建筑升级事件驱动；任务、地图、离线结算不再直接提供玩家经验。

## 表结构

建议新增原始 CSV：

- `Assets/Config/Holmas_MetaLevelTable.csv`
- `Assets/Config/Holmas_AgencyBuildingTable.csv`

字段固定为：

- `playerLevel`
  - 玩家等级，范围 1-20
- `minExperience`
  - 达到该玩家等级所需的累计经验门槛
- `offlineRewardPerHour`
  - 每小时离线奖励
- `adUnlockHours`
  - 当前玩家等级广告槽位单次解锁持续时间
- `notes`
  - 策划备注列，不进入运行时导出

运行时只消费前 4 个字段，`notes` 仅保留在原始 CSV 中。

建筑升级表字段固定为：

- `agencyStageId`
  - 当前侦探社阶段标识，按阶段一行组织建筑升级配置
- `buildingIds`
  - 当前阶段下需要升级的建筑集合
- `buildingUpgradeLevelCaps`
  - 与 `buildingIds` 对齐，表示每个建筑在该阶段最多可升几级
- `buildingUpgradeCosts`
  - 与 `buildingIds` 对齐的二维费用数组，每个建筑内部再按升级档位依次列出单货币费用
- `notes`
  - 策划备注列，不进入运行时导出

建筑升级规则固定为：

- 玩家经验输入来自“侦探社建筑升级事件”
- 每完成一次单独建筑升级动作，固定获得 `1` 点玩家经验
- 同一侦探社阶段能提供多少次经验事件，取决于该阶段全部建筑的升级档位总数
- 不同侦探社阶段允许配置不同建筑集合，不强制固定为 5 个建筑

## 等级关系

从 v1 长期成长表开始，玩家升级门槛与侦探社建筑配置拆分为两套配置来源。

规则固定为：

- 外层需要任务/地图等级时，统一消费玩家等级语义
- 现有 `HolmasTaskProgressService`、`HolmasLevelRequestGenerator` 继续保留 `playerLevel` 参数名
- `Holmas_MetaLevelTable` 负责 `playerLevel -> minExperience / offlineRewardPerHour / adUnlockHours`
- `Holmas_AgencyBuildingTable` 负责侦探社阶段下的建筑集合、升级级数和费用
- 本轮不要求运行时字段名立即全部切换，只先统一方案语义
- 本轮不额外定义“玩家等级必须始终等于侦探社等级”

这样处理的原因：

- 避免 `agencyLevel` 同时承担等级门槛和建筑配置两种职责
- 让等级门槛与建筑成长各自独立配置，后续更容易扩展
- 同时给建筑系统保留独立推进节奏，不提前锁死双等级完全绑定

## 分层与数值节奏

等级分 4 段：

- `Lv1-5`
  - 新手段，升级快，反馈密集
- `Lv6-10`
  - 成长期，平滑拉长升级间隔
- `Lv11-15`
  - 中后期，成长压力加大
- `Lv16-20`
  - 后期，门槛明显变长，但资源收益同步上调

累计经验门槛固定为：

- `Lv1-20 minExperience`
- `0, 80, 170, 270, 380, 520, 690, 890, 1120, 1390, 1700, 2060, 2480, 2960, 3510, 4140, 4860, 5680, 6610, 7660`

各成长参数固定为：

- `buildingUpgradeExp`
- 全等级统一为 `1`

- `offlineRewardPerHour`
- `6, 6, 7, 7, 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 22, 24, 27, 30, 33, 36`

- `adUnlockHours`
- 全等级统一为 `24`

## 运行时公式

当前默认代码中的成长口径，统一替换成文档明确规则加表驱动：

### 建筑升级经验

- `buildingUpgradeExp = 1`
- 每次完成一次单独建筑升级动作时结算一次

### 建筑升级次数

- 当前侦探社阶段的经验事件次数
- `sum(all building upgrade tiers in Holmas_AgencyBuildingTable for current agencyStageId)`

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
  - 需要改为消费建筑升级输入，并分别读取等级门槛表与建筑升级表

- `HolmasGameBootstrap`
  - 不再只塞 `AgencyLevel=1`
  - 要改为同时加载完整成长表和建筑升级表

- 现有导表链
  - 在 `CSV转二进制` 流程中加入 meta 表与建筑升级表导出
  - 继续合并进核心配置包，不额外拆第三个 bytes 文件

## 测试要求

落地时必须补并通过这些验证：

- `playerLevel` 唯一且从 1 连续到 20
- `minExperience` 严格递增
- `buildingUpgradeExp = 1`
- `adUnlockHours > 0`
- `offlineRewardPerHour >= 0`

- `agencyStageId` 唯一
- `buildingIds`、`buildingUpgradeLevelCaps`、`buildingUpgradeCosts` 数量对齐
- 每个建筑的费用档位数等于对应 `buildingUpgradeLevelCaps`
- 所有升级费用为非负整数

- `Experience` 增长后，`playerLevel` 能按表正确提升
- 边界经验值不会跳错级
- 初始状态正确落在 `Lv1`

- 玩家升级任意一个侦探社功能时，经验增加 `1`
- 完成某个侦探社阶段全部建筑升级后，累计经验增加值等于该阶段全部升级档位总数
- 完成任务、完成地图、离线结算时，不直接增加玩家经验
- 离线奖励按小时速率计算
- 广告槽位解锁时长从表里读取，而不是继续写死 24 小时

- `playerLevel` 提升后，任务补位和地图请求继续使用同等级配置
- `Lv1/3/5` 的地图解锁节奏继续成立
- 与当前 20 级任务/地图内容表及建筑升级表组合后，`CSV转二进制` 和 `run_holmas_validation.sh` 继续通过

## 本轮不做

- 不接 UI
- 不扩赌博任务
- 不把 icon 资源完整性当成长期成长表的前置阻塞
- 不拆第二套等级体系
- 不新建单独的 meta bytes 文件

## 默认结论

这份方案的默认实现顺序是：

1. 新增长期成长原始 CSV
2. 新增侦探社建筑升级原始 CSV
3. 将两张表纳入现有 CSV->json/bytes 导出链
4. 扩展 `HolmasMetaProgressionDefinition` 与建筑升级读取结构
5. 接入建筑升级驱动的经验输入，并替换默认经验源与 1 级兜底 meta catalog
6. 复用现有 20 级内容表做整体验证

在这套方案落地前，当前 3 张 icon 资源只作为展示占位，不阻塞成长表主线推进。
