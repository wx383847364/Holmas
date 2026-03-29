# 长期 subagent 编组方案

## Summary

按你刚刚确认的“完整长期版”范围来拆，我建议用 **8 个执行型 subagent + 1 个主控 agent**。  
这个数量是比较稳的平衡点：

- 少于 8 个，会把“侦探社成长 / 离线收益 / 广告解锁 / 持久化 / UI”这些高耦合模块硬塞在一起，后期集成会堵住。
- 多于 8 个，真正会争抢的写入面会迅速增多，尤其是 `App.Shared`、`HotUpdate` 组合层、UI 预制体和存档模型。

这次拆分默认覆盖：
- 地图、猫、任务栏、等级表、`MinesweeperTerrainData` 接入
- 侦探社家具/星级/猫窝/养猫培养
- 离线拜访、广告解锁、时间规则、持久化
- 整体 UI 流程和 HotUpdate 正式业务入口

## subagent 拆分

### 1. Foundation / Boundary agent

职责：
- 建立正式 `App.HotUpdate` 模块骨架和组合层
- 冻结 Shared DTO、跨层接口、事件类型
- 定义服务注册、模块装配、资源访问约定
- 统一处理任何必须落到 `App.Shared` 或 AOT 边界的变更

独占写入范围：
- `App.Shared`
- `App.HotUpdate` 入口与 composition root
- 必要的 AOT/Shared 边界接口

这是唯一必须先串行启动的 agent。

### 2. Config / Content Pipeline agent

职责：
- 落地图表、猫表、任务表、玩家等级表、家具/猫窝/培养配置、离线收益配置
- 约定 `terrainPath`、`iconPath`、奖励字段、权重字段、资源 key 的读法
- 管理 `MinesweeperTerrainData` 作为地图模板的接入方式
- 做配置加载、解析、校验、默认值策略

独占写入范围：
- 配置模型
- 配置加载器与校验器
- 地图模板到配置输入的映射层

### 3. Board / Level Generation agent

职责：
- 实现棋盘纯逻辑模型
- 把 `MinesweeperTerrainData` 转成 `BoardTemplate`
- 处理有效格、猫布点、数字计算、揭示、扩散、通关判定
- 生成 `LevelSnapshot` 和本局地图态数据

独占写入范围：
- Board model
- Level generation
- Runtime map state

它不负责任务、UI、存档。

### 4. Task / Mission agent

职责：
- 实现任务栏 5 槽规则
- 实现任务抽取、猫种去重、奖励计算、领奖补位
- 处理地图完成后的任务推进
- 管理普通任务实例和槽位状态

独占写入范围：
- `TaskInstanceData`
- `TaskSlotState`
- 任务生成与奖励服务

它不负责广告时钟和长期存档结算。

### 5. Detective Agency / Meta Progression agent

职责：
- 实现侦探社家具、星级/等级关系、猫窝解锁
- 实现养猫/培养和长期经济消耗点
- 接收任务与关卡结算结果，更新成长状态
- 预留外部经验系统接入口

独占写入范围：
- 家具/侦探社成长服务
- 猫窝与猫培养数据
- 元进度结算逻辑

### 6. Time / Persistence / Ads / Offline agent

职责：
- 统一时间服务、跨天判定、24 小时广告槽位到期
- 实现看广告解锁槽位的持续时间规则
- 实现离线拜访、每日上限、2 小时周期结算
- 负责持久化模型、存档恢复、版本兼容

独占写入范围：
- Save model
- Time provider
- Offline settlement
- Ad unlock state

注意：
- 若需要新增 Shared 时间/平台接口，由 1 号 agent 统一落边界，6 号 agent 只消费。

### 7. UI / Flow agent

职责：
- 负责找猫主界面、任务栏、领奖、地图结算、广告锁槽位展示
- 负责侦探社界面、家具升级、猫窝/养猫面板
- 只写 Presenter / Controller / 绑定逻辑，不写核心规则
- 串起完整用户流程：进图 -> 找猫 -> 领奖 -> 回侦探社 -> 离线收益提示

独占写入范围：
- UGUI 面板
- Prefab 绑定
- 场景流与交互编排

UI 资源必须由它独占，避免 prefab 冲突。

### 8. QA / Simulation / Validation agent

职责：
- 写核心规则测试、deterministic simulation、配置校验
- 验证权重逻辑、去重逻辑、奖励公式、时间到期、离线结算
- 做集成回归：地图生成、任务生成、任务推进、侦探社结算、存档恢复
- 在最终集成阶段做独立审查

独占写入范围：
- 测试代码
- 校验脚本
- 集成验证工具

## 并行与串行

### 必须串行

1. `Foundation / Boundary` 先完成最小公共 DTO、接口、目录和组合入口。
2. 公共类型冻结后，其他 agent 才能并行。
3. UI 绑定必须等核心数据流稳定后再进入全量接线。
4. 最后由主控 agent 做统一集成和冲突仲裁。
5. QA 的最终回归必须在所有模块集成后完成。

### 第一批可并行

在 1 号 agent 完成边界冻结后，同时启动：
- 2 `Config / Content Pipeline`
- 3 `Board / Level Generation`
- 4 `Task / Mission`
- 5 `Detective Agency / Meta Progression`
- 6 `Time / Persistence / Ads / Offline`

### 第二批半并行

7 `UI / Flow` 可在以下条件满足后启动全速开发：
- Shared DTO 名称和字段冻结
- Board 输出接口稳定
- Task 输出接口稳定
- Agency / Offline 的展示数据结构已定

### 持续并行

8 `QA / Simulation / Validation` 从中期就可以开始，不必等功能全部完成。  
它应先写模拟器和校验器，再接完整回归。

## 集成顺序

推荐集成顺序固定为：

1. 1 号 agent 冻结边界和组合层
2. 接入 2 号配置链路，保证数据能读
3. 接入 3 号棋盘与地图生成，做到“能进图、能揭示、能通关”
4. 接入 4 号任务系统，做到“找猫能推进任务”
5. 接入 5 号侦探社成长，做到“结算后有长期进度”
6. 接入 6 号时间/广告/离线/存档，做到“长期系统闭环”
7. 接入 7 号 UI 全流程
8. 8 号做完整回归和压力验证

## 最容易冲突的写入范围

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
- 只有 1 号 agent 能直接改 Shared 和组合入口
- 只有 7 号 agent 能直接改 UI prefab
- 只有 6 号 agent 能主写存档结构
- 其他 agent 若需要跨边界改动，必须通过主控或 1 号 agent 合并

## Test Plan

完成长期版前，至少要通过这些验收面：

- 同一等级下，任务、地图、猫种权重抽取稳定且可复现
- 当前任务栏猫种不重复，广告槽位独立过期
- 地图只在有效格生成猫，完成条件只看“猫是否找完”
- 家具、星级、猫窝、培养的长期进度能正确结算
- 离线拜访 2 小时周期、每日上限、跨天逻辑正确
- 存档恢复后，任务槽位、离线时间、侦探社成长、地图状态一致
- UI 流程能完整覆盖：进图、找猫、领奖、回侦探社、广告解锁、离线收益提示
- 配置表缺项、权重异常、路径错误时能被校验器提前拦住

## Assumptions

- 这是按你刚确认的“完整长期版”来拆，不是只按当前 v1 核心玩法。
- 赌博任务先不作为首轮实现重点，保留接口即可。
- 经验来源仍由外部建设功能提供，当前只留接入口。
- `MinesweeperTerrainData` 继续是地图模板工具链，不单独拆一个长期 subagent。
- “代码边界规范”不单独占一个执行 agent，而是由主控 agent + skill 共同约束。
