# Holmas v1 方案

## Summary

v1 的正式数据链路定为：

- 地图模板来自 `@minesweeper` 产出的 `MinesweeperTerrainData`
- 地图表只负责选择哪张模板图、以及这一局允许生成多少只猫
- 猫表定义猫种类、权重、价格、资源
- 任务表定义“这一类任务可能抽到哪些猫、数量范围、奖励系数”
- 玩家等级表决定“当前等级能抽哪些任务、能抽哪些地图，以及各自权重”
- 任务栏先生成，地图再生成；地图里的猫优先从当前任务栏的猫种池中出，保证玩家找猫能推进任务

v1 只做普通任务，不做赌博任务。  
玩家等级经验来源明确接入侦探社建筑升级，不再留空接口。

## Key Changes

### 1. 地图相关

地图表字段固定为：

- `mapId`
- `terrainPath`
- `catCountMax`
- `catCountMin`

字段含义固定为：

- `terrainPath` 指向 `MinesweeperTerrainData` 的资源地址或 YooAssets key，不是场景路径
- `catCountMin/Max` 表示这一局地图最终生成的猫总数范围

运行时流程：

1. 根据玩家等级表随机出一个 `mapId`
2. 查地图表得到 `terrainPath`
3. 通过 YooAssets 加载 `MinesweeperTerrainData`
4. 读取其中 `rows / cols / valid cells / blockColor`
5. 在有效格中随机生成猫

边界规则：

- `MinesweeperTerrainData` 只负责地图形状和颜色
- 猫位置、任务进度、奖励结果都不写回 terrain 资产
- 若地图有效格数量小于 `catCountMax`，则自动下修到有效格上限

### 2. 猫相关

猫表字段固定为：

- `catId`
- `iconPath`
- `rarity`
- `weight`
- `price`

用途固定为：

- `weight` 用于地图生成时，同一任务猫种池内的出现概率
- `price` 用于普通任务奖励计算
- `iconPath` 走 YooAssets 加载

### 3. 任务模板表

任务表字段固定为：

- `taskTypeId`
- `catIdList`
- `countMax`
- `countMin`
- `rewardArray`
- `levelRewardFactor`

v1 规则固定为：

- `catIdList` 是候选猫种数组
- 生成单个任务实例时，只会从 `catIdList` 中抽 1 个 `catId`
- `countMin/Max` 用于生成该任务实例的目标数量
- `levelRewardFactor` 参与最终普通任务奖励计算
- `rewardArray` 字段保留，但 v1 普通任务不启用；留给后续赌博任务或特殊奖励覆盖

普通任务最终奖励公式：

- `reward = cat.price * targetCount * levelRewardFactor`

最终任务实例字段固定为：

- `taskInstanceId`
- `sourceTaskTypeId`
- `taskKind`
- `catId`
- `targetCount`
- `currentCount`
- `reward`
- `slotIndex`
- `expireAt`（仅广告解锁槽位需要）

### 4. 玩家等级表

玩家等级表字段固定为：

- `playerLevel`
- `upgradeExp`
- `taskTypeIds`
- `taskTypeWeights`
- `mapIds`
- `mapWeights`

v1 解释固定为：

- `upgradeExp` 只保留升级门槛定义
- `upgradeExp` 对应 `Holmas_MetaLevelTable.playerLevel -> minExperience`
- 玩家经验由侦探社建筑升级提供，每次完成一次单独建筑升级动作固定增加 `1` 点经验
- 侦探社建筑配置由 `Holmas_AgencyBuildingTable` 定义，按侦探社阶段给出建筑集合、升级级数和每级费用
- 建筑升级经验与现有玩家升级体系并存，但本阶段不强制定义“玩家等级 = 侦探社等级”
- `taskTypeIds + taskTypeWeights` 用于任务栏补任务时抽模板
- `mapIds + mapWeights` 用于生成新地图时抽地图模板

### 5. 任务栏规则

任务栏总槽位固定为 5：

- 2 个默认开启
- 3 个默认锁定

广告解锁规则：

- 每看 1 次广告，解锁 1 个锁定槽位，持续 24 小时
- 3 个额外槽位的到期时间独立计算
- 槽位一旦解锁，立即按同样规则补 1 个任务

任务生成规则：

1. 先取当前玩家等级配置
2. 按 `taskTypeIds + taskTypeWeights` 抽一个 `taskTypeId`
3. 从该任务模板的 `catIdList` 里抽 1 个 `catId`
4. 再按 `countMin~countMax` 生成目标数量
5. 计算奖励，生成任务实例
6. 填入空槽位

去重规则：

- 当前任务栏内，任务实例的 `catId` 不能重复
- 若抽中重复 `catId`，则重抽
- 若当前等级可用猫种不足以填满全部已解锁槽位，则只填到可用上限，不强行重复

任务刷新规则：

- 默认开启槽位：任务完成并领奖后立即补新任务
- 广告槽位：解锁后立即补任务；到期时若任务未完成，直接清空并重新锁定
- v1 不做赌博任务，只产出普通任务实例

### 6. 地图生成与任务联动

地图生成顺序固定为：

1. 先看当前任务栏，收集所有任务实例的唯一 `catId`
2. 再按玩家等级表随机一张地图
3. 按地图表 `catCountMin~Max` 生成本局猫总数
4. 从“当前任务栏猫种池”中，按猫表 `weight` 分配本局猫种
5. 将猫放入 `MinesweeperTerrainData` 的有效格中
6. 计算棋盘数字提示

这样处理的好处：

- 玩家当前看到的任务，一定能在当前或后续地图里持续推进
- 地图和任务用同一套等级门槛控制
- `@minesweeper` 地形工具仍然只负责底板，不被任务系统污染

关卡完成条件固定为：

- 玩家找出本局地图中生成的所有猫

完成后流程：

- 结算本局地图
- 更新任务进度
- 若有已完成任务，允许领奖并补新任务
- 再按玩家等级表生成下一张地图

## Public APIs / Data

运行时核心数据固定为：

- `BoardTemplate`
  - `rows`
  - `cols`
  - `validMask`
  - `blockColors`

- `LevelSnapshot`
  - `mapId`
  - `terrainPath`
  - `seed`
  - `spawnedCats`
  - `revealedCells`
  - `completed`

- `SpawnedCatData`
  - `catId`
  - `cellIndex`

- `TaskInstanceData`
  - `taskInstanceId`
  - `sourceTaskTypeId`
  - `taskKind`
  - `catId`
  - `targetCount`
  - `currentCount`
  - `reward`
  - `slotIndex`
  - `expireAt`

- `TaskSlotState`
  - `slotIndex`
  - `isUnlocked`
  - `unlockExpireAt`
  - `taskInstanceId`

接口边界固定为：

- AOT 不解释地图、任务、奖励
- HotUpdate 负责 terrain -> board template -> level snapshot 的转换
- Shared 只放纯数据，不放 editor 类和 `ScriptableObject` 逻辑

## Test Plan

必须覆盖的测试：

- 玩家升级任意一个侦探社功能时，经验固定增加 `1`
- 完成某个侦探社阶段全部建筑升级后，累计经验增加值等于该阶段全部升级档位总数
- 玩家经验达到 `upgradeExp` 门槛时，按等级表正常升级
- 同一等级下，任务栏按任务权重正常生成
- 当前任务栏内 `catId` 不重复
- 广告槽位独立计时，过期后只影响对应槽位
- 普通任务奖励按 `cat.price * targetCount * levelRewardFactor` 正确计算
- 地图按等级表中的 `mapIds + mapWeights` 正确抽取
- 地图猫总数落在 `catCountMin~Max` 范围内，且不超过有效格数量
- 地图生成的猫只落在有效格
- 地图生成的猫种来自当前任务栏猫种池
- 找出全部猫后地图完成
- 任务进度随找到对应 `catId` 的猫而推进
- 完成任务、完成地图、离线结算时，不直接增加玩家经验
- `MinesweeperTerrainData` 只作为模板，不被运行时状态污染

## Assumptions

- v1 只做普通任务，赌博任务相关字段先保留不用。
- `rewardArray` 暂不参与普通任务计算，留给后续扩展。
- 玩家经验来源固定为侦探社建筑升级事件，每次建筑升级提供 `1` 点经验。
- `Holmas_MetaLevelTable` 使用 `playerLevel` 承载升级门槛与长期成长参数。
- 侦探社建筑配置通过独立的 `Holmas_AgencyBuildingTable` 提供，不再写死每级固定 5 个建筑。
- 暂不锁定“玩家等级 = 侦探社等级”，只锁定建筑升级会驱动玩家经验增长。
- 地图中的猫种优先围绕当前任务栏生成，以保证任务推进体验。
- 若等级配置下可用猫种不足，允许任务栏不满，不允许生成重复猫任务。
