# Holmas v1 长期成长表方案

## Summary

v1 的长期成长正式改为配置驱动，不再继续沿用默认写死策略。

本方案锁定这几个决策：

- `AgencyLevel` 与 `playerLevel` 合并为一套等级语义
- 使用一张完整成长表承载等级门槛与成长参数
- 成长曲线采用“前快后慢”
- 本轮只补长期成长数值，不接 UI，不扩赌博任务
- 长期成长表进入现有核心配置导出包，不单独拆第三个 bytes 文件

目标是把当前只包含 `AgencyLevel=1` 的兜底配置，推进成可和 20 级任务表、地图表一起工作的正式 v1 配置。

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
- 经验、离线收益和广告解锁时长都仍写死在默认策略中

当前默认公式为：

- 任务领奖经验：`task.Reward / 10`
- 地图完成经验：`5 * CompletedMapCount + SpawnedCatCount`
- 离线经验：每 30 分钟 1 点
- 离线奖励：每 10 分钟 1 点
- 广告解锁时长：24 小时

本方案的目标就是把这些默认口径迁到正式表里。

## 表结构

建议新增原始 CSV：

- `Assets/Config/Holmas_MetaLevelTable.csv`

字段固定为：

- `agencyLevel`
  - 侦探社等级，范围 1-20
- `minExperience`
  - 达到该等级所需的累计经验门槛
- `taskClaimExpFactor`
  - 任务领奖经验系数，按 `taskReward * factor` 计算
- `mapCompletionBaseExp`
  - 单局完成地图的基础经验
- `mapCompletionCatExp`
  - 单局每只猫额外提供的经验
- `offlineExpPerHour`
  - 每小时离线经验
- `offlineRewardPerHour`
  - 每小时离线奖励
- `adUnlockHours`
  - 当前等级广告槽位单次解锁持续时间
- `notes`
  - 策划备注列，不进入运行时导出

运行时只消费前 8 个字段，`notes` 仅保留在原始 CSV 中。

## 等级关系

从 v1 长期成长表开始，`AgencyLevel` 与 `playerLevel` 视为同一套等级。

规则固定为：

- 外层需要任务/地图等级时，统一使用 `MetaProgressionState.AgencyLevel`
- 现有 `HolmasTaskProgressService`、`HolmasLevelRequestGenerator` 继续保留 `playerLevel` 参数名
- 调用方统一传入当前 `AgencyLevel`
- 本轮不删除旧字段和旧方法名，只统一语义

这样处理的原因：

- 避免长期成长表与任务/地图表出现两套等级映射
- 避免后续 UI 和外层流程再维护一层 `AgencyLevel -> playerLevel` 转换
- 更符合当前 v1 的轻量产品阶段

## 分层与数值节奏

等级分 4 段：

- `Lv1-5`
  - 新手段，升级快，反馈密集
- `Lv6-10`
  - 成长期，平滑拉长升级间隔
- `Lv11-15`
  - 中后期，成长压力加大
- `Lv16-20`
  - 后期，门槛明显变长，但离线收益与任务领奖经验同步上调

累计经验门槛固定为：

- `Lv1-20 minExperience`
- `0, 80, 170, 270, 380, 520, 690, 890, 1120, 1390, 1700, 2060, 2480, 2960, 3510, 4140, 4860, 5680, 6610, 7660`

各成长参数固定为：

- `taskClaimExpFactor`
- `0.10, 0.10, 0.11, 0.11, 0.12, 0.12, 0.13, 0.13, 0.14, 0.14, 0.15, 0.15, 0.16, 0.16, 0.17, 0.17, 0.18, 0.18, 0.19, 0.20`

- `mapCompletionBaseExp`
- `4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 14`

- `mapCompletionCatExp`
- `1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4`

- `offlineExpPerHour`
- `2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7, 7, 8, 8, 9`

- `offlineRewardPerHour`
- `6, 6, 7, 7, 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 22, 24, 27, 30, 33, 36`

- `adUnlockHours`
- 全等级统一为 `24`

## 运行时公式

当前默认代码中的经验/离线口径，统一替换成表驱动：

### 任务领奖经验

- `taskClaimExp = round(task.Reward * taskClaimExpFactor)`
- 最低保底为 `1`

### 地图完成经验

- `mapCompletionExp = mapCompletionBaseExp + spawnedCatCount * mapCompletionCatExp`

### 离线经验

- `offlineExp = floor(offlineHours * offlineExpPerHour)`

### 离线奖励

- `offlineReward = floor(offlineHours * offlineRewardPerHour)`

### 广告解锁时长

- `unlockExpireAt = now + adUnlockHours`

## 接入要求

本方案要求扩展以下配置结构：

- `HolmasMetaProgressionDefinition`
  - 从仅包含 `AgencyLevel + MinExperience`
  - 扩展为完整成长参数定义

- `IHolmasMetaCatalog`
  - 继续保持按 `agencyLevel` 取定义即可

- 默认成长策略实现
  - 需要替换成读取成长表的正式表驱动实现

- `HolmasGameBootstrap`
  - 不再只塞 `AgencyLevel=1`
  - 要改为加载完整成长表

- 现有导表链
  - 在 `CSV转二进制` 流程中加入 meta 表导出
  - 继续合并进核心配置包，不额外拆第三个 bytes 文件

## 测试要求

落地时必须补并通过这些验证：

- `agencyLevel` 唯一且从 1 连续到 20
- `minExperience` 严格递增
- 各成长参数非负
- `adUnlockHours > 0`

- `Experience` 增长后，`AgencyLevel` 能按表正确提升
- 边界经验值不会跳错级
- 初始状态正确落在 `Lv1`

- 任务领奖经验按 `task.Reward * taskClaimExpFactor` 计算
- 地图完成经验按 `base + catCount * perCat` 计算
- 离线经验与离线奖励按小时速率计算
- 广告槽位解锁时长从表里读取，而不是继续写死 24 小时

- `AgencyLevel` 提升后，任务补位和地图请求继续使用同等级配置
- `Lv1/3/5` 的地图解锁节奏继续成立
- 与当前 20 级任务/地图内容表组合后，`CSV转二进制` 和 `run_holmas_validation.sh` 继续通过

## 本轮不做

- 不接 UI
- 不扩赌博任务
- 不把 icon 资源完整性当成长期成长表的前置阻塞
- 不拆第二套等级体系
- 不新建单独的 meta bytes 文件

## 默认结论

这份方案的默认实现顺序是：

1. 新增长期成长原始 CSV
2. 将成长表纳入现有 CSV->json/bytes 导出链
3. 扩展 `HolmasMetaProgressionDefinition` 与运行时读取结构
4. 用成长表替换默认经验源与 1 级兜底 meta catalog
5. 复用现有 20 级内容表做整体验证

在这套方案落地前，当前 3 张 icon 资源只作为展示占位，不阻塞成长表主线推进。
