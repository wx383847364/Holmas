# Holmas v1 城市宣传成长主线落地方案

## Summary

后续实现以 `Holmas_AgencyBuildingTable.xlsx` 表内城市宣传数据为准，不再沿用“5 阶段建筑内容表”方案，也不在代码或测试中写死宣传阶段总数。

本次落地不是只换表内容，而是正式把导表协议、运行时主链和测试口径都切到“严格按表驱动”：

- 地方宣传阶段改为 `agencyStageId`，个数根据表中数据判断
- 每个地方有几个宣传功能读表Holmas_AgencyBuildingTable.xlsx中的promotionIds字段
- 每个宣传功能升级次数读表Holmas_AgencyBuildingTable.xlsx中的promotionLevelCaps字段
- 每次升级固定 `+1` 玩家经验
- 总升级经验读表Holmas_PlayerLevelTable.xlsx中的minExperience
- 导表协议严格匹配 xlsx 表名和技术表头，不再包装成 `AgencyBuildings`、`AgencyPromotionStages` 等业务别名
- runtime 只消费表数据，不反向包装、改名或用旧代码里的固定数量限制表

## 完成情况

- 当前状态：进行中
- 进度说明：已完成 BattlePanel 地图点击、底部 promotion slots、StageBar 连接进度和静态绑定约束调整。
- 最近更新：2026-05-08，已完成 BattlePanel 地图点击、底部 promotion slots、StageBar 连接进度和静态绑定约束调整。

## 实施目标

本轮目标是把当前“建筑成长”整体升级为 `v1` 正式城市宣传成长系统，同时把导表协议收敛为表格镜像，而不是在旧命名上继续加兼容补丁。

需要同时完成：

1. 配置表切到宣传字段
2. 导表、JSON 预览、bytes 与 C#/Python 配置模型严格匹配表文件名和技术表头
3. 运行时业务模型从 `building` 切到 `promotion`，但不得改变导表协议命名
4. `Bootstrap`、组合层和 smoke 统一切到宣传语义
5. 测试、日志、错误文案统一切换

## 配置表方案

当前正式配置只保留一张成长表和一张宣传表：

- `Assets/Config/Holmas_PlayerLevelTable.xlsx`
- `Assets/Config/Holmas_AgencyBuildingTable.xlsx`

### Holmas_PlayerLevelTable.xlsx

成长字段保持：

- `playerLevel`
- `minExperience`
- `offlineRewardPerHour`
- `adUnlockHours`
- `notes`

累计经验门槛完全读取表内 `minExperience`，不在代码、导表工具、测试里写死总经验池或等级总数。

### Holmas_AgencyBuildingTable.xlsx

虽然文件名暂时保留，但表内字段正式改成：

- `agencyStageId`
- `stageName`
- `stageImage`
- `promotionIds`
- `promotionLevelCaps`
- `promotionUpgradeCosts`
- `notes`

字段含义固定为：

- `agencyStageId`：从 `1` 开始连续递增的地区阶段，个数读表确定
- `stageName`：地区名称，必须唯一
- `stageImage`：地区阶段在宣传界面地图上的按钮图片，供 stage 按钮直接读取
- `promotionIds`：宣传功能可随时增加
- `promotionLevelCaps`：不固定
- `promotionUpgradeCosts`：不固定

## 导表与校验协议

`Xlsx导出二进制` 需要扩展以下规则：

- `Holmas_PlayerLevelTable.xlsx` 行数不固定
- `Holmas_PlayerLevelTable.xlsx` 的玩家等级和经验门槛按表数据校验，不写死总经验池或等级总数
- 导出协议严格镜像 xlsx 表名和技术表头
- JSON 顶层集合名使用表文件基础名，不包含 `.xlsx` 后缀：
  - `Holmas_PlayerLevelTable`
  - `Holmas_AgencyBuildingTable`
- JSON 行字段使用第二行技术表头原名，不使用 C# 属性名或业务包装名
- `agencyStageId` 必须从 `1` 开始连续递增
- `stageName` 不允许重复
- `stageImage` 不允许为空，且必须能映射到正式资源
- `promotionIds` 长度不固定
- `promotionLevelCaps` 长度不固定
- `promotionUpgradeCosts` 外层长度不固定
- 每组 `promotionUpgradeCosts` 内层长度由同位置 `promotionLevelCaps` 决定
- 每个 `promotionLevelCaps` 必须 `> 0`
- 所有升级费用必须 `> 0`

核心配置导出包继续保持两份正式产物：

- `holmas_core_config.bytes`
- `holmas_cat_meta.bytes`

`core config` 不再使用 `AgencyBuildings[]` 作为正式协议字段，也不新增 `AgencyPromotionStages` 这类业务包装名。

正式协议约束为：

- 表集合名严格等于表文件基础名：例如 `Holmas_AgencyBuildingTable`
- 行字段严格等于技术表头：例如 `agencyStageId`、`stageName`、`stageImage`、`promotionIds`、`promotionLevelCaps`、`promotionUpgradeCosts`、`notes`
- 二进制 bytes 可以继续按固定顺序写入数据，但 C#/Python 源模型命名与字段含义必须能一一追溯到表名和表头
- 检测到旧 JSON 字段 `AgencyBuildings` 时视为旧协议，必须报错并要求重新导表

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

- 不保留 `building` 与 `promotion` 双主语义
- 不保留旧 `AgencyBuildings` 协议兼容路径；旧 JSON 字段只用于识别并报错
- runtime 可以把 `Holmas_AgencyBuildingTable` 的行数据转换成升级服务内部对象
- 转换层不得改变导表协议命名，不得把表重新包装成 `AgencyBuildings`、`AgencyPromotionStages` 或固定阶段数组
- `TryUpgradePromotion`、`PromotionLevels` 等业务 API 保留宣传语义，并且只消费严格导出的表数据

## 成长主链

成长逻辑固定为：

- 每次宣传升级成功：
  - 扣金币
  - 对应宣传等级 `+1`
  - 玩家经验 `+1`
  - 重算 `playerLevel`
- 当前地区所有宣传功能全部达到 `cap`
  - 才推进到下一 `agencyStageId`
- 如果当前是最后一个地区阶段
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

- 从导出配置恢复 `Holmas_PlayerLevelTable` 与 `Holmas_AgencyBuildingTable`
- 不再组装“建筑成长”服务
- 对外暴露宣传升级入口
- 当前无 UI 主链依然能：
  - 补任务
  - 开地图
  - 领奖
  - 结算离线收益
  - 升级宣传功能

## 宣传界面设计

宣传界面正式改为“地图 + stage 按钮 + stagebar”的主视图，不再只用纯文本摘要承载阶段信息。

### 地图与 stage 按钮

- 界面中显示一张宣传地图
- 地图上固定展示 `5` 个 stage 按钮
- stage 按钮严格按 `Holmas_AgencyBuildingTable.xlsx` 中的行顺序读取，不允许在 UI 层重新排序
- 每个 stage 按钮的信息由以下三项组成：
  - `agencyStageId`
  - `stageName`
  - `stageImage`
- `agencyStageId` 用于按钮编号与当前阶段判断
- `stageName` 用于按钮上的地区名展示
- `stageImage` 用于按钮主视觉或对应地区缩略图
- 底部城市建设按钮必须使用当前查看城市的 `stageImage`，不得使用与该城市无关的静态升级图标

stage 按钮的锁定规则固定为：

- `Stage 1` 在新档或首次进入宣传界面时直接解锁，不显示锁定态
- 除 `Stage 1` 之外，其他 stage 按钮默认显示 `lock`
- 只有当玩家当前进度已经推进到对应 `agencyStageId` 时，该 stage 按钮才解锁
- 未解锁 stage 仍可显示按钮位置、城市图和城市名，但必须覆盖锁定态，不允许直接交互进入
- 已解锁但尚未完成的当前 stage 处于可点击状态
- 已经完成并推进过去的 stage 处于已解锁状态，可按需要显示“已完成”或普通可回看态

当总阶段数大于 `5` 时，界面按当前阶段生成连续的 `5` 个可见 stage：

- 优先保证当前 `agencyStageId` 落在这 `5` 个按钮内
- 中间阶段尽量显示为“前 2 个 + 当前阶段 + 后 2 个”
- 当当前阶段靠近开头或结尾时，顺延为连续的前 `5` 个或后 `5` 个阶段

### 城市建设按钮与星级

- `Build_btn` 表示当前查看城市的建设入口
- `Build_btn/Image` 必须显示当前查看城市的 `stageImage`
- 星星表示当前查看城市的宣传总进度，不是静态装饰
- 星级范围固定为 `0..5`
  - 当前城市 `宣传总等级 = 0` 时显示 `0` 颗
  - 第一次升级成功后至少显示 `1` 颗
  - 当前城市全部宣传功能满级时显示 `5` 颗
- 当前城市是否满级按该城市所有 `promotionLevelCaps` 总和判断
  - 例如某城市总 cap 为 `15`，显示 `15/15` 后才算该城市满级
  - 满级后推进到下一 `agencyStageId`，下一城市图标解锁并隐藏锁定态

### stagebar 进度条

- 每两个相邻 stage 按钮之间放置一个 `stagebar`
- `stagebar` 只连接表顺序中相邻的两个阶段，不允许跨阶段连线
- `stagebar` 百分比根据“当前地区宣传进度”实时计算

当前地区宣传进度定义为：

- 当前地区所有宣传功能当前等级之和 / 当前地区所有宣传功能等级上限之和
- 即：
  - 分子：当前 `agencyStageId` 下全部 `promotionIds` 的当前等级总和
  - 分母：当前 `agencyStageId` 下全部 `promotionLevelCaps` 的总和

在地图中的显示规则固定为：

- 已经完成并推进过去的阶段之间：`stagebar = 100%`
- 当前阶段连接下一阶段的 `stagebar = 当前地区宣传进度百分比`
- 尚未到达的后续阶段之间：`stagebar = 0%`

例如当前处于 `Stage 3`：

- `Stage 1 -> Stage 2` 的 `stagebar` 显示 `100%`
- `Stage 2 -> Stage 3` 的 `stagebar` 显示 `100%`
- `Stage 3 -> Stage 4` 的 `stagebar` 显示当前地区宣传进度
- `Stage 4 -> Stage 5` 的 `stagebar` 显示 `0%`

### UI 主链要求

- 界面初始化时，先从 `Holmas_AgencyBuildingTable` 恢复全部阶段顺序数据
- stage 按钮与 stagebar 的显示不得写死总阶段数、固定城市数或固定按钮内容
- stage 按钮的锁定态必须严格跟随当前 `agencyStageId`
  - `Stage 1` 始终解锁
  - 其他 stage 只有在玩家推进到对应地图阶段时才解锁
- 当前阶段宣传升级成功后，界面需要同步刷新：
  - 当前 stage 的宣传进度
  - 当前 stage 到下一 stage 的 `stagebar` 百分比
  - 当阶段完成时，刷新新的当前 `agencyStageId` 与可见的 `5` 个 stage 按钮窗口
  - 如果推进到了新的地图阶段，同时刷新新阶段按钮的 `lock` 状态
- 当最后一个阶段完成后，不再继续生成新的后续 stagebar 进度

## 测试与验证

必须覆盖：

### 配表校验

- `Holmas_AgencyBuildingTable.xlsx` 行数不固定，读表确定
- `agencyStageId` 从 `1` 开始连续递增
- `stageName` 唯一
- `stageImage` 非空，且能映射到正式资源
- `promotionIds` 长度读表确定
- 每个 `promotionId` 的 `cap` 读表确定
- 每组 `promotionUpgradeCosts` 长度必须等于同位置 `cap`，且全部 `> 0`
- JSON 顶层存在 `Holmas_AgencyBuildingTable`
- JSON 顶层不存在 `AgencyBuildings`
- JSON 行字段严格使用 xlsx 技术表头原名

### 等级校验

- `playerLevel` 连续 `1..N`
- `minExperience` 严格递增
- 不写死玩家等级总数和总经验池

### 宣传升级逻辑

- 金币不足失败
- 单项宣传满级后不可继续升级
- 当前城市未全满时不能推进下一城市
- 当前城市全满后推进下一城市
- 最后一个城市升满后不越界
- `Stage 1` 初始解锁
- 其他 stage 只有推进到对应 `agencyStageId` 后才解锁
- 推进到新阶段时，对应 stage 按钮的 `lock` 状态同步解除

### 经验验证

- 每次宣传升级固定 `+1`
- 任务领奖不加经验
- 地图完成不加经验
- 离线结算不加经验

### 端到端

- `python3 tools/config_export/export_holmas_config.py`
- `bash tools/validation/run_holmas_validation.sh`
- 从正式导出配置恢复后直接跑一条宣传升级 smoke

## 执行顺序

建议按以下顺序实施：

1. 切配置表字段读取与导表协议镜像规则
2. 切 JSON/bytes 模型与 runtime config 恢复
3. 重构长期成长纯逻辑到宣传语义
4. 接入 `Bootstrap`、`GameplayRuntime`、`ApplicationContext`
5. 更新测试与 smoke
6. 跑导表与全量验证
7. 执行文档维护并补提交建议

## 本轮不做

- 不扩博彩任务
- 不增加宣传效果值字段
- 不引入第二货币
- 不做地图自由拖拽、缩放和分层特效
- 不处理 stage 按钮的高级演出、点击动画和美术精修
