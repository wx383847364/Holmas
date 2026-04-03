# subagent 与 skill 配对表

## Summary

这版是给你实际开工时直接使用的配对方案。  
原则固定为：

- 所有 subagent 默认都带 `unity-hotupdate-boundary`
- 配置/生成类工作再叠加 `findcat-config-pipeline`
- UI/流程类工作再叠加 `unity-ugui-flow-integration`
- `App.Shared`、`HotUpdate` 入口、UI prefab 这 3 类高冲突区域必须独占

推荐先按 **6 个 subagent** 起步；等你用顺手了，再扩成 9 个长期版编组。

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

交付物：
- 可跑通的主流程 UI
- 页面流转和按钮绑定
- 冒烟验证清单

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
- 给出 `通过 / 通过，但有非阻塞建议 / 不通过，退回修复` 的结论
- 把问题明确退回给原实现 agent 或主控继续修复
- 作为继续推进前的默认强制审查门
- 在同一条审查链里默认持续负责复审，不因修复轮次增加而默认换新的审查 agent

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

## 9-Agent 长期版

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

## 启动顺序

固定顺序建议：

1. 先启动 `边界与骨架 Agent`
2. 它冻结 DTO 和模块边界后
3. 再并行启动：
- 地图与棋盘
- 任务与长期进度
- 测试与质量保障
4. 每个阶段产出完成后，默认交给挑刺与问题审查 agent 做强制审查
5. 如果挑刺与问题审查 agent 不通过，修完后默认交还同一个挑刺 agent 复审
6. UI agent 在核心输出接口稳定后再全速启动
7. 测试 agent 在功能线产出后持续做验证和回归
8. 最后统一集成和回归

## 你给我的指令模板

### 轻量版模板

“这次按 6 个 subagent 开工。  
全部默认遵循 `unity-hotupdate-boundary`。  
地图和任务相关额外遵循 `findcat-config-pipeline`。  
UI 相关额外遵循 `unity-ugui-flow-integration`。  
测试和挑刺审查按对象叠加 `findcat-config-pipeline` 或 `unity-ugui-flow-integration`。  
`App.Shared` 和 HotUpdate 入口只能边界 agent 改，UI prefab 只能 UI agent 改。  
先冻结 DTO，再并行开发；每个阶段产出默认先交给 Agent 6 挑刺审查，通过后再继续推进。”

### 长期版模板

“这次按 9 个 subagent 长期版分工。  
所有 agent 默认遵循 `unity-hotupdate-boundary`。  
配置/地图/任务/QA/审查 额外按对象叠加 `findcat-config-pipeline`。  
UI 和 UI 审查额外遵循 `unity-ugui-flow-integration`。  
`App.Shared`、HotUpdate 入口、UI prefab、存档模型分别独占，不允许多人同时修改。”

## Test Plan

这套配对方案是否成功，按这几条验收：

- Shared 变更只来自边界 agent，没有被其他 agent 乱改
- 地图 agent 没把任务或 UI 逻辑写进棋盘模型
- 任务 agent 没把 Presenter/UI 逻辑写进服务层
- UI agent 没把奖励公式、去重、地图生成写进界面代码
- 测试 agent 能根据 skill 规则识别越层引用、配置污染运行时、UI 直接改业务状态等问题
- 挑刺 agent 会在阶段结束后给出明确的 通过 / 退回 结论，而不是只给模糊建议
- 集成阶段不会频繁出现 DTO 改名、入口冲突、prefab 覆盖

## Assumptions

- 你是第一次正式使用 subagent + skill 协作，所以默认先推荐 6-Agent 实操版。
- `unity-hotupdate-boundary` 是所有 agent 的基础 skill，不单独省略。
- `findcat-config-pipeline` 主要服务于表结构、权重、生成、奖励和配置校验。
- `unity-ugui-flow-integration` 主要服务于 UI 接线和流程编排，不允许它承载核心业务规则。
