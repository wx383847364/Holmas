# Holmas v1 正式建筑内容表方案

## Summary

这是 `v1` 方案范围内的正式内容表补齐。

依据 [Holmas_v1方案.md](/Users/bruce/work/Holmas/doc/长期主文档/方案与数据/Holmas_v1方案.md) 当前已经锁定：

- 玩家经验只来自侦探社建筑升级
- 建筑配置由 `Holmas_AgencyBuildingTable` 提供
- 本阶段不接 UI，先把纯逻辑和内容表做完整

这一步不只改单张建筑表，而是把 **建筑表 + 等级门槛表** 一起收口。
原因是当前运行时固定“每次建筑升级只给 `1` 点经验”，所以正式建筑内容表的关键不是单次经验值，而是：

- 一共分几阶段
- 每阶段有多少次升级动作
- 每阶段总共能提供多少经验
- 这些经验如何对齐 `playerLevel` 的升级门槛

本方案固定采用：

- `5` 个侦探社阶段
- 与现有 `20` 级内容表对齐成 `5 段 x 4 级`
- 总升级次数固定为 `76`
- 每次升级 `+1` 经验，因此总可获得 `76` 点成长经验
- 同步把 `Holmas_MetaLevelTable.minExperience` 改成与 `76` 点经验总量一致的累计门槛

## 建筑阶段设计

阶段固定为 `5 段`，每段定位如下：

- `Stage 1`
  - 新手建设期
  - 目标：快速理解“任务拿金币 -> 升建筑 -> 长等级”
- `Stage 2`
  - 成长期
  - 目标：建筑数增加，金币消耗开始拉开
- `Stage 3`
  - 中期
  - 目标：开始形成明显的建设深度
- `Stage 4`
  - 中后期
  - 目标：建筑数量和成本同时抬高
- `Stage 5`
  - 后期
  - 目标：作为 `v1` 终盘建设目标

阶段与等级带对齐固定为：

- `Stage 1`：覆盖 `Lv1-Lv4`
- `Stage 2`：覆盖 `Lv5-Lv8`
- `Stage 3`：覆盖 `Lv9-Lv12`
- `Stage 4`：覆盖 `Lv13-Lv16`
- `Stage 5`：覆盖 `Lv17-Lv20`

## 正式建筑表内容

`Holmas_AgencyBuildingTable.csv` 直接改成下面这 `5` 行正式内容：

```text
agencyStageId,buildingIds,buildingUpgradeLevelCaps,buildingUpgradeCosts,notes
1,lobby;desk;boardroom,3;3;2,10;15;20|12;18;24|14;22,新手阶段
2,archive;lab;display;lounge,3;3;3;3,26;34;42|28;38;48|24;32;40|22;30;38,成长期
3,training;research;security;storage,4;4;4;4,45;58;72;88|48;62;78;96|40;52;66;82|38;50;64;80,中期阶段
4,observatory;hall;dataCenter;recruit;publicity,4;4;4;4;4,78;96;116;138|72;90;110;132|84;104;126;150|68;86;106;128|64;82;102;124,中后期阶段
5,caseHub;intelligence;workshop;trophy;command,4;4;4;4;4,120;145;172;200|114;138;164;192|108;130;154;180|96;118;142;168|126;152;180;210,后期阶段
```

每阶段经验产出固定为：

- `Stage 1`：`8` 次升级 = `8` 经验
- `Stage 2`：`12` 次升级 = `12` 经验
- `Stage 3`：`16` 次升级 = `16` 经验
- `Stage 4`：`20` 次升级 = `20` 经验
- `Stage 5`：`20` 次升级 = `20` 经验

累计总经验固定为：

- `8 + 12 + 16 + 20 + 20 = 76`

每阶段金币总成本大致为：

- `Stage 1`：`135`
- `Stage 2`：`402`
- `Stage 3`：`1019`
- `Stage 4`：`2056`
- `Stage 5`：`3009`

累计总金币成本：

- `6621`

默认解释固定为：

- 不新增建筑效果字段
- `v1` 建筑只承担“成长推进器 + 阶段内容密度”
- `buildingId` 保持 ASCII 命名，不做中文 id
- 当前阶段全部建筑达到 cap 后，才推进到下一阶段

## 同步修正 MetaLevel 门槛

为了和“每次升级只给 `1` 点经验”一致，`Holmas_MetaLevelTable.csv` 的 `minExperience` 必须同步改成下面这组累计门槛：

```text
playerLevel,minExperience
1,0
2,2
3,4
4,6
5,8
6,11
7,14
8,17
9,20
10,24
11,28
12,32
13,36
14,41
15,46
16,51
17,56
18,61
19,68
20,76
```

这组门槛的固定含义是：

- `Lv5` 正好对应 `Stage 1` 升满
- `Lv9` 正好对应 `Stage 2` 升满
- `Lv13` 正好对应 `Stage 3` 升满
- `Lv17` 正好对应 `Stage 4` 升满
- `Lv20` 正好对应 `Stage 5` 升满

所以这一步的真正实现不是“只补建筑表”，而是：

1. 更新 `Holmas_AgencyBuildingTable.csv`
2. 同步更新 `Holmas_MetaLevelTable.csv` 的 `minExperience`
3. 重新导出 `json / bytes`
4. 重新验证成长主链

## 运行时与内容约束

这一步不新增运行时接口，只收口现有语义：

- 建筑升级仍然调用现有 `TryUpgradeBuilding(buildingId)`
- 每次成功升级仍固定：
  - 扣金币
  - 建筑等级 `+1`
  - 玩家经验 `+1`
  - 重算 `playerLevel`
- `agencyStageId` 继续按“当前阶段全部满级后推进”
- 任务、地图、离线结算的经验口径保持不变：
  - 任务领奖不加经验
  - 地图完成不加经验
  - 离线结算不加经验
- 金币来源仍然只依赖：
  - 任务领奖
  - 离线奖励

默认不做：

- 不新增建筑被动收益
- 不新增建筑功能解锁字段
- 不新增第二种建设货币
- 不让建筑直接改任务/地图公式

## Test Plan

必须补并通过这些验证：

- 导表校验
  - `Holmas_AgencyBuildingTable` `5` 个阶段都能通过长度、cap、cost 校验
  - `Holmas_MetaLevelTable.minExperience` 严格递增
  - `playerLevel` 门槛与 `upgradeExp` 镜像字段一致
- 成长阶段验证
  - `Stage 1` 升满后恰好达到 `Lv5`
  - `Stage 2` 升满后恰好达到 `Lv9`
  - `Stage 3` 升满后恰好达到 `Lv13`
  - `Stage 4` 升满后恰好达到 `Lv17`
  - `Stage 5` 升满后恰好达到 `Lv20`
- 建筑升级验证
  - 每次升级只增加 `1` 经验
  - 金币不足时失败
  - 达到 cap 后失败
  - 最后一个阶段升满后不越界推进
- 端到端验证
  - 从正式导出的 core config 恢复 `MetaLevels + AgencyBuildings`
  - 直接执行建筑升级
  - 检查 `GoldBalance -> BuildingLevels -> Experience -> PlayerLevel -> AgencyStageId`
- 回归验证
  - `python3 scripts/export_holmas_config.py`
  - `bash scripts/run_holmas_validation.sh`

## Assumptions

- 这一步属于 `v1` 正式内容补齐，不是 `v2` 扩展
- 正式建筑内容表和等级门槛表必须一起改，不能只改单边
- 当前 `49` 张 icon 已经齐，不阻塞建筑内容表推进
- 当前阶段仍然不接 UI
- 当前阶段仍然不扩赌博任务
- 当前阶段仍然不增加建筑功能效果，只先把成长与内容密度做正确
