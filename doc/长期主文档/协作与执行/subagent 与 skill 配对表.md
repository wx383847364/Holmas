# subagent 与 skill 配对表

## Summary

这版是给你实际开工时直接使用的配对方案。  
原则固定为：

- 所有 subagent 默认都带 `unity-hotupdate-boundary`
- 配置/生成类工作再叠加 `findcat-config-pipeline`
- UI/流程类工作再叠加 `unity-ugui-flow-integration`
- 任何 UI 修改、prefab 绑定或 UI 业务逻辑工作必须叠加 `ui-prefab-governance`
- 涉及 manifest、collector、generated bindings、spec、生成或验证时再叠加 `ui-prefab-pipeline`
- `App.Shared`、`HotUpdate` 入口、UI prefab 这 3 类高冲突区域必须独占

推荐先按 **6 个 subagent** 起步；等你用顺手了，再扩成 9 个长期版编组。

这页已经吸收长期版编组说明，后续优先把这里当作编组与 skill 的权威速查页。

## UI 自动生成系统专项

当任务属于 `doc/长期主文档/UI自动生成系统` 或 `Assets/Tools/UiPrefabGenerator` 时，优先切到这套专项组合，而不是默认套 Holmas gameplay 编组。

### 专项 skill

- `ui-prefab-governance`
  - 管专区落位、派工单格式、asmdef 分层、迁移边界、旧稿跳转页和 Holmas 试点角色
- `ui-prefab-pipeline`
  - 管 `DesignPacket -> UiPrefabSpec -> PrefabBindingManifest -> validation`

### 专项 subagent

1. `Subagent 1 / Foundation-Contracts`
- skill：`ui-prefab-governance`
- 独占：`Runtime/Core/Contracts`、`*.asmdef`、专区执行派工单正文

2. `Subagent 2 / Design-Intake-Spec`
- skill：`ui-prefab-pipeline`
- 独占：`Runtime/Core/Intake`、`04_输入规格_DesignPacket.md`、`05_中间规格_UiPrefabSpec.md`

3. `Subagent 3 / Generator-Manifest`
- skill：`ui-prefab-pipeline`
- 独占：`Runtime/Core/Manifest`、`Editor/Generation`、manifest 与生成样例

4. `Subagent 4 / Holmas-Adapter-Profile`
- skill：`ui-prefab-governance` + `ui-prefab-pipeline`
- 只有触碰 Holmas 接入代码时才额外叠加：`unity-hotupdate-boundary`
- 独占：`Runtime/HolmasAdapter`、Holmas profile、目录映射和消费约束

5. `Subagent 5 / Validation-Regression`
- skill：`ui-prefab-pipeline`
- 独占：`Editor/Validation`、`Tests`、golden fixtures、validation baseline

6. `Subagent 6 / Review-Acceptance`
- skill：`ui-prefab-governance`
- 按审查对象叠加：`ui-prefab-pipeline`
- 独占：迭代记录中的审查结论、review note、复审条件

### 专项启动顺序

1. 先起 `Subagent 1 / Foundation-Contracts`
2. 契约冻结后并行起 `Subagent 2 / 3 / 4`
3. 至少有 1 份 approved sample spec 和 1 份 sample manifest 后，`Subagent 5` 再全面介入
4. `Subagent 6` 只在阶段里程碑后介入

## 6-Agent 实操版

### 1. 边界与骨架 Agent

skill 组合：
- `unity-hotupdate-boundary`

职责：
- 冻结 `App.Shared` DTO、接口、事件
- 搭 `App.HotUpdate` 模块骨架与组合层
- 定义服务注册、模块入口、跨层依赖规则
- 审核其他 agent 提出的跨层改动需求

允许写入：
- `Assets/Scripts/App.Shared`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Entry`
- `Assets/HotUpdateContent/Script/App.HotUpdate` 的 composition root
- 必要的 service locator / module bootstrap

禁止写入：
- UI prefab
- 地图生成细节
- 任务公式
- 侦探社业务逻辑

交付物：
- 冻结后的 DTO 列表
- 模块目录结构
- HotUpdate 入口接线方案
- 其他 agent 可依赖的接口清单

### 2. 地图与棋盘 Agent

skill 组合：
- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责：
- 接入 `MinesweeperTerrainData`
- 实现 `BoardTemplate`、`LevelSnapshot`
- 处理有效格、猫布点、数字计算、揭示、扩散、通关判定
- 完成 terrain -> runtime board template 的转换

允许写入：
- Board model
- Level generation
- Terrain adapter
- 纯逻辑地图态数据

禁止写入：
- `App.Shared`
- HotUpdate 入口
- UI prefab
- 任务栏规则
- 广告/离线/持久化

交付物：
- `BoardTemplate`
- `LevelSnapshot`
- 猫生成输入输出接口
- 可供 UI 消费的格子状态输出

### 3. 任务与长期进度 Agent

skill 组合：
- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责：
- 实现任务栏 5 槽规则
- 实现任务抽取、猫种去重、奖励计算、领奖补位
- 实现侦探社成长、家具/猫窝/养猫的元进度服务
- 实现广告槽位 24 小时规则和离线收益的业务口
- 与地图完成事件联动推进任务和长期进度

允许写入：
- Task service
- Progression service
- Offline/ad business service
- 配置读取与奖励计算逻辑

禁止写入：
- `App.Shared`
- HotUpdate 入口
- UI prefab
- 棋盘核心揭示逻辑

交付物：
- `TaskInstanceData`
- `TaskSlotState`
- 奖励公式实现
- 任务完成后的进度更新接口
- 离线/广告状态服务接口

### 4. UI 与验证 Agent

skill 组合：
- `unity-hotupdate-boundary`
- `unity-ugui-flow-integration`
- `ui-prefab-governance`
- 涉及 manifest / collector / generated bindings / spec / prefab 生成或验证时再叠加 `ui-prefab-pipeline`

职责：
- 负责找猫主界面、任务栏、领奖、广告锁位、结算面板
- 负责侦探社界面、家具升级、猫窝、养猫展示
- 做 Presenter / Controller / 绑定逻辑
- 做基础联调验证和冒烟流程

允许写入：
- UGUI 面板
- prefab
- scene binding
- presenter / controller
- UI smoke tests

禁止写入：
- `App.Shared`
- HotUpdate 入口
- 核心奖励公式
- 棋盘底层算法
- 配置字段定义
- 未经明确要求，不得改动原 prefab 的颜色、透明度、tint 默认值、材质颜色或 `CanvasGroup.alpha`

交付物：
- 可跑通的主流程 UI
- 页面流转和按钮绑定
- 冒烟验证清单
- 静态绑定完整性说明，以及未改动 prefab 视觉参数的确认

### 5. 测试与质量保障 Agent

skill 组合：
- 默认：`unity-hotupdate-boundary`
- 测地图、任务、配置时：再叠加 `findcat-config-pipeline`
- 测 UI 流程时：再叠加 `unity-ugui-flow-integration`

职责：
- 写单元测试、集成测试和验证脚本
- 校验地图、任务、奖励、时间和配置逻辑是否正确
- 验证其他 agent 的输入输出、边界和接线是否一致
- 做冒烟测试和回归验证

允许写入：
- 测试代码
- 校验脚本
- 模拟器
- QA 文档

禁止写入：
- `App.Shared`
- HotUpdate 入口
- UI prefab
- 核心业务实现目录

交付物：
- 单元测试或集成测试代码
- 覆盖面说明
- 失败项和风险清单
- 需要回给哪个 agent 修复的问题列表

### 6. 挑刺与问题审查 Agent

skill 组合：
- 默认：`unity-hotupdate-boundary`
- 审地图、任务、配置时：再叠加 `findcat-config-pipeline`
- 审 UI 流程时：再叠加 `unity-ugui-flow-integration`

职责：
- 独立挑刺，找明显 bug、回归、越界、需求理解偏差和缺关键验证问题
- 给出 `通过 / 通过，但有非阻塞建议 / 未通过，退回修复` 的结论
- 把问题明确退回给原实现 agent 或主控继续修复
- 作为阶段里程碑继续推进前的默认审查门，而不是每个中间结果的默认同步等待门
- 在同一条审查链里默认优先复用原审查实例负责复审；只有原实例超时、不可用或上下文明显失真时，才允许同职责 reviewer 接手同一 `review_chain_id`

允许写入：
- review 文档
- 审查脚本
- QA 结论文档

禁止写入：
- `App.Shared`
- HotUpdate 入口
- UI prefab
- 核心业务实现目录

交付物：
- 审查对象和范围
- 审查结论
- 问题列表、严重级别和是否阻塞
- 退回给谁修以及复审条件
- 如果是复审，要明确说明是否沿用上一次审查结论，以及哪些问题已关闭、哪些问题仍阻塞

## 9-Agent 长期版速查

当你熟悉 subagent 后，再升级为这套：

1. `Foundation / Boundary`
- skill：`unity-hotupdate-boundary`
- 独占：`App.Shared`、HotUpdate 入口、组合层

2. `Config / Content Pipeline`
- skill：`unity-hotupdate-boundary` + `findcat-config-pipeline`
- 独占：表结构、配置加载、资源 key、校验器

3. `Board / Level Generation`
- skill：`unity-hotupdate-boundary` + `findcat-config-pipeline`
- 独占：地图模板接入、棋盘逻辑、关卡生成

4. `Task / Mission`
- skill：`unity-hotupdate-boundary` + `findcat-config-pipeline`
- 独占：任务栏、奖励、补位、去重

5. `Detective Agency / Progression`
- skill：`unity-hotupdate-boundary`
- 独占：家具、猫窝、养猫、长期成长

6. `Time / Persistence / Ads / Offline`
- skill：`unity-hotupdate-boundary`
- 独占：时间规则、广告解锁、离线收益、存档模型

7. `UI / Flow`
- skill：`unity-hotupdate-boundary` + `unity-ugui-flow-integration`
- 独占：UI prefab、Presenter、流程接线

8. `QA / Simulation / Validation`
- skill：`unity-hotupdate-boundary` + `findcat-config-pipeline`
- 独占：模拟器、配置校验、集成回归

9. `Critic / Defect Review`
- skill：`unity-hotupdate-boundary`
- 按对象叠加：`findcat-config-pipeline` 或 `unity-ugui-flow-integration`
- 独占：问题审查、阻塞结论、退回修复归属

## 9-Agent 的设计理由

按完整长期版范围拆分时，推荐用 **9 个执行型 subagent + 1 个主控 agent**。

这个数量的平衡点是：

- 少于 9 个，容易把“测试验证”和“独立挑刺审查”混成一个角色，后期很容易既写测试又自己放过问题
- 多于 9 个，真正会争抢的写入面会迅速增多，尤其是 `App.Shared`、`HotUpdate` 组合层、UI prefab 和存档模型

长期版默认覆盖：

- 地图、猫、任务栏、等级表、`MinesweeperTerrainData` 接入
- 侦探社家具/星级/猫窝/养猫培养
- 离线拜访、广告解锁、时间规则、持久化
- 整体 UI 流程和 HotUpdate 正式业务入口

## 6-Agent 实操版启动顺序

固定顺序建议：

1. 先启动 `边界与骨架 Agent`
2. 它冻结 DTO 和模块边界后
3. 再并行启动：
- 地图与棋盘
- 任务与长期进度
- 测试与质量保障
4. 每个阶段里程碑完成后，默认交给挑刺与问题审查 agent 做审查
5. 如果挑刺与问题审查 agent 不通过，修完后默认优先交还同一审查链复审
6. UI agent 在核心输出接口稳定后再全速启动
7. 测试 agent 在功能线产出后持续做验证和回归
8. 最后统一集成和回归

## 长期版并行与串行

### 必须串行

1. `Foundation / Boundary` 先完成最小公共 DTO、接口、目录和组合入口。
2. 公共类型冻结后，其他长期版 subagent 才能并行。
3. UI 绑定必须等核心数据流稳定后再进入全量接线。
4. 最后由主控 agent 做统一集成和冲突仲裁。
5. 每个阶段里程碑交付都必须先经过 `Critic / Defect Review` 审查；中间修复和局部验证默认继续当前实现/验证链，不同步卡住主线。
6. QA 的最终回归必须在所有模块集成后完成。

### 第一批可并行

在 `Foundation / Boundary` 完成边界冻结后，同时启动：

- `Config / Content Pipeline`
- `Board / Level Generation`
- `Task / Mission`
- `Detective Agency / Progression`
- `Time / Persistence / Ads / Offline`

### 第二批半并行

`UI / Flow` 在下面条件满足后再全速启动：

- Shared DTO 名称和字段冻结
- Board 输出接口稳定
- Task 输出接口稳定
- Agency / Offline 的展示数据结构已定

### 持续并行

- `QA / Simulation / Validation` 从中期就可以开始，不必等功能全部完成；应先写模拟器和校验器，再接完整回归。
- `Critic / Defect Review` 在每个阶段里程碑交付后都要介入；它不做实现，只做挑刺、裁定和退回修复。

## 长期版集成顺序

推荐集成顺序固定为：

1. `Foundation / Boundary` 冻结边界和组合层
2. 接入 `Config / Content Pipeline`，保证数据能读
3. 接入 `Board / Level Generation`，做到“能进图、能揭示、能通关”
4. 接入 `Task / Mission`，做到“找猫能推进任务”
5. 接入 `Detective Agency / Progression`，做到“结算后有长期进度”
6. 接入 `Time / Persistence / Ads / Offline`，做到“长期系统闭环”
7. 接入 `UI / Flow` 全流程
8. `QA / Simulation / Validation` 做完整回归和压力验证
9. `Critic / Defect Review` 对阶段里程碑交付做阻塞性审查，未过审不进入下一阶段

## 高冲突写入范围速查

高风险冲突区：

- `Assets/Scripts/App.Shared`
- `Assets/HotUpdateContent/Script/App.HotUpdate` 的入口与组合层
- UI prefab / 场景绑定 / 统一 Presenter 目录
- 存档模型和版本迁移模型

低风险、适合并行区：

- 纯逻辑棋盘模块
- 配置模型与校验器
- 任务服务实现
- 侦探社成长服务
- 测试与模拟器

固定协作规则：

- 只有 `Foundation / Boundary` 能直接改 Shared 和组合入口
- 只有 `UI / Flow` 能直接改 UI prefab
- 只有 `Time / Persistence / Ads / Offline` 能主写存档结构
- 只有 `Critic / Defect Review` 能给出默认的阻塞性审查结论
- 其他 subagent 若需要跨边界改动，必须通过主控或边界线合并

## 模板入口

这页只保留编组与配对信息。

如需可直接复制的模板或覆盖模板，统一跳到：

- [skill 与 subagent 任务模板](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/skill%20与%20subagent%20任务模板.md)

## Test Plan

这套配对方案是否成功，按这几条验收：

- Shared 变更只来自边界 agent，没有被其他 agent 乱改
- 地图 agent 没把任务或 UI 逻辑写进棋盘模型
- 任务 agent 没把 Presenter/UI 逻辑写进服务层
- UI agent 没把奖励公式、去重、地图生成写进界面代码
- 测试 agent 能根据 skill 规则识别越层引用、配置污染运行时、UI 直接改业务状态等问题
- 挑刺 agent 会在阶段里程碑结束后给出明确的 通过 / 退回 结论，而不是只给模糊建议
- 集成阶段不会频繁出现 DTO 改名、入口冲突、prefab 覆盖
- 原 reviewer 超时后，新 reviewer 接手同一 `review_chain_id` 时，脚本口径和线程级注册表口径保持一致，不会把 `待回补复审` 误判成 `已通过`
- 长期版并行阶段不会把 Shared、组合层、UI prefab、存档模型改成多人共享写入

## Assumptions

- 你是第一次正式使用 subagent + skill 协作，所以默认先推荐 6-Agent 实操版。
- `unity-hotupdate-boundary` 是所有 agent 的基础 skill，不单独省略。
- `findcat-config-pipeline` 主要服务于表结构、权重、生成、奖励和配置校验。
- `unity-ugui-flow-integration` 主要服务于 UI 接线和流程编排，不允许它承载核心业务规则。
