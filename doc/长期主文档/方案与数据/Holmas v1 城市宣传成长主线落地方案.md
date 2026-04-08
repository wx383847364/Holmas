# Holmas v1 城市宣传成长主线落地方案

## Summary

后续实现以 `100` 城市宣传方案为准，不再沿用“5 阶段建筑内容表”方案。

本次落地不是只换表内容，而是正式把协议、语义和运行时主链都切到宣传系统：

- 长期成长继续保留 `20` 个玩家等级
- 城市宣传阶段改为 `100` 个 `agencyStageId`
- 每个城市固定 `4` 个宣传功能
- 每个宣传功能固定升级 `5` 次
- 每次升级固定 `+1` 玩家经验
- 总经验池固定为 `100 x 4 x 5 = 2000`
- 表结构、导表协议、运行时模型、错误文案、测试口径统一改成“宣传”命名

## 完成情况

- 当前状态：进行中
- 进度说明：已完成宣传语义、协议与主链接线方案定义，待持久化、UI 和整体验证链完全闭环。

## 实施目标

本轮目标是把当前“建筑成长”整体升级为 `v1` 正式城市宣传成长系统，而不是在旧命名上继续加兼容补丁。

需要同时完成：

1. 配置表切到宣传字段
2. 导表与二进制协议切到宣传字段
3. 运行时模型从 `building` 切到 `promotion`
4. `Bootstrap`、组合层和 smoke 统一切到宣传语义
5. 测试、日志、错误文案统一切换

## 配置表方案

继续保留两张原始表：

- `Assets/Config/Holmas_MetaLevelTable.csv`
- `Assets/Config/Holmas_AgencyBuildingTable.csv`

### Holmas_MetaLevelTable.csv

字段保持：

- `playerLevel`
- `minExperience`
- `offlineRewardPerHour`
- `adUnlockHours`
- `notes`

累计经验门槛改为 `2000` 总经验池版本：

```text
0,40,85,135,190,250,320,400,490,590,700,825,965,1120,1290,1475,1675,1840,1930,2000
```

### Holmas_AgencyBuildingTable.csv

虽然文件名暂时保留，但表内字段正式改成：

- `agencyStageId`
- `stageName`
- `promotionIds`
- `promotionLevelCaps`
- `promotionUpgradeCosts`
- `notes`

字段含义固定为：

- `agencyStageId`：`1..100` 的城市阶段
- `stageName`：城市名称，必须唯一
- `promotionIds`：固定四个宣传功能
  - `leaflet`
  - `radio`
  - `online`
  - `tv`
- `promotionLevelCaps`：固定 `5;5;5;5`
- `promotionUpgradeCosts`：4 组升级费用，每组固定 `5` 档

## 导表与校验协议

`CSV转二进制` 需要扩展以下规则：

- `Holmas_PlayerLevelTable.csv` 行数必须锁死为 `20`
- `Holmas_PlayerLevelTable.csv` 必须与 `Holmas_MetaLevelTable.csv` 行数完全一致
- `agencyStageId` 必须连续 `1..100`
- `stageName` 不允许重复
- `promotionIds` 长度固定为 `4`
- `promotionLevelCaps` 长度固定为 `4`，且值固定全为 `5`
- `promotionUpgradeCosts` 外层长度固定为 `4`
- 每组 `promotionUpgradeCosts` 内层长度固定为 `5`
- 所有升级费用必须 `> 0`
- `Lv20.minExperience` 必须等于 `2000`

核心配置导出包继续保持两份正式产物：

- `holmas_core_config.bytes`
- `holmas_cat_meta.bytes`

但 `core config` 的模型需要从 `AgencyBuildings[]` 语义切为“宣传配置”语义。

## 运行时语义切换

当前旧“建筑”语义必须整体切换为“宣传”语义。

重点替换方向：

- `HolmasAgencyBuildingDefinition` -> 宣传定义
- `BuildingLevels` -> `PromotionLevels`
- `BuildingId` -> `PromotionId`
- `TryUpgradeBuilding` -> `TryUpgradePromotion`
- `GetBuildingLevel` / `SetBuildingLevel` -> 对应宣传等级方法
- 日志、错误文案、测试名统一切成宣传语义

默认要求：

- 不长期保留 `building` 与 `promotion` 双命名并存
- 若短期兼容不可避免，也只能作为过渡层，不能继续作为主语义

## 成长主链

成长逻辑固定为：

- 每次宣传升级成功：
  - 扣金币
  - 对应宣传等级 `+1`
  - 玩家经验 `+1`
  - 重算 `playerLevel`
- 当前城市 `4` 个宣传功能全部达到 `5/5`
  - 才推进到下一 `agencyStageId`
- 如果当前是最后一个城市阶段
  - 满级后不再越界推进

经验来源规则继续保持：

- 任务领奖不直接增加玩家经验
- 地图完成不直接增加玩家经验
- 离线结算不直接增加玩家经验
- 玩家经验只来自宣传升级成功事件

金币来源继续保持：

- 任务领奖
- 离线结算

## 主链接线

`Bootstrap` 与组合层需要同步切换：

- 从导出配置恢复 `MetaLevels` 与宣传配置
- 不再组装“建筑成长”服务
- 对外暴露宣传升级入口
- 当前无 UI 主链依然能：
  - 补任务
  - 开地图
  - 领奖
  - 结算离线收益
  - 升级宣传功能

## 测试与验证

必须覆盖：

### 配表校验

- `Holmas_AgencyBuildingTable.csv` 共 `100` 行
- `agencyStageId` 连续 `1..100`
- `stageName` 唯一
- 每行固定 `4` 个 `promotionIds`
- 每个 `promotionId` 的 `cap` 固定为 `5`
- 每组 `promotionUpgradeCosts` 固定 `5` 档且全部 `> 0`

### 等级校验

- `playerLevel` 连续 `1..20`
- `minExperience` 严格递增
- `Lv20.minExperience == 2000`

### 宣传升级逻辑

- 金币不足失败
- 单项宣传满级后不可继续升级
- 当前城市未全满时不能推进下一城市
- 当前城市全满后推进下一城市
- 最后一个城市升满后不越界

### 经验验证

- 每次宣传升级固定 `+1`
- 单个城市总经验固定 `20`
- 全部 `100` 城市总经验固定 `2000`
- 任务领奖不加经验
- 地图完成不加经验
- 离线结算不加经验

### 端到端

- `python3 scripts/export_holmas_config.py`
- `bash scripts/run_holmas_validation.sh`
- 从正式导出配置恢复后直接跑一条宣传升级 smoke

## 执行顺序

建议按以下顺序实施：

1. 切配置表字段与导表协议
2. 切二进制模型与 runtime config 恢复
3. 重构长期成长纯逻辑到宣传语义
4. 接入 `Bootstrap`、`GameplayRuntime`、`ApplicationContext`
5. 更新测试与 smoke
6. 跑导表与全量验证
7. 执行文档维护并补提交建议

## 本轮不做

- 不接 UI
- 不扩博彩任务
- 不增加宣传效果值字段
- 不引入第二货币
- 不处理宣传按钮视觉与美术表现
