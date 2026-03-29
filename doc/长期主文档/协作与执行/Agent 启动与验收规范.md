# Agent 启动与验收规范

## Summary

这份文档固定 Holmas 项目的 Agent 启动方式和验收方式。
目标不是“每次临时想一遍怎么分工”，而是把 Agent 的启动协议固定下来，让每次启动都尽量稳定、可重复、可审查。

这份规范重点解决 3 个问题：

- 启动 Agent 前，主控必须先确认什么
- 启动 Agent 时，任务指令必须包含什么
- Agent 完成后，按什么标准验收，才能继续下一步

从现在开始，这个项目默认遵循下面的原则：

- Agent 不是自由发挥，而是按固定职责实现
- skill 不是可选提示，而是启动协议的一部分
- 写入边界、禁止边界、交付物、验收项，必须在启动时一次说清
- 未通过验收的 Agent 结果，不视为可直接集成

## 启动前检查

主控在真正启动任何 Agent 之前，必须先完成这组检查：

1. 先确认当前项目阶段

- 是边界冻结阶段
- 还是并行开发阶段
- 还是 UI 接线 / 集成 / 回归阶段

2. 先确认 Shared DTO 是否已冻结

- 如果 `App.Shared` 还没定，不允许地图线、任务线、UI 线一起开工
- 如果 Shared DTO 已冻结，地图线和任务线才能并行
- UI 线默认要等核心输出接口稳定后再全速启动

3. 先确认高冲突目录是否已经独占

- `App.Shared`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Entry`
- HotUpdate composition root
- UI prefab / scene binding

4. 先确认本轮每个 Agent 的写入边界

- 能写哪些目录
- 不能写哪些目录
- 哪些类型只能读取不能改

如果上面 4 项还没清楚，就不应该直接启动 Agent。

## 启动协议

每次启动 Agent，主控都必须把下面这些信息一次性给全。
缺任何一项，都容易导致 Agent 跑偏。

### 必填项

- Agent 名称
- 本轮目标
- 必带 skill
- 允许写入范围
- 禁止写入范围
- 依赖前提
- 明确交付物
- 完成后的汇报格式

### 固定格式

推荐按这个顺序组织启动指令：

1. 你是谁
- 例如：`你负责本项目的地图与棋盘实现`

2. 你必须遵循哪些 skill
- 例如：`请遵循 $unity-hotupdate-boundary 和 $findcat-config-pipeline`

3. 你的目标是什么
- 只写本轮真正要完成的内容
- 不要把后续阶段的工作一起塞进去

4. 你的边界是什么
- 明确写出允许改哪些目录
- 明确写出禁止改哪些目录
- 明确写出不能碰的跨层区域

5. 你的交付物是什么
- 要列出修改文件
- 要说明输入输出接口
- 要说明其他 agent 如何接你的结果

6. 你的汇报方式是什么
- 必须说明做了什么
- 必须说明未完成什么
- 必须说明风险或阻塞

## 5-Agent 固定规范

当前项目默认先使用 5-Agent 实操版。
如果没有特别说明，就按这一版启动。

### Agent 1：边界与骨架

必带 skill：

- `unity-hotupdate-boundary`

职责固定为：

- 冻结 `App.Shared` DTO、接口、事件
- 搭 `App.HotUpdate` 模块骨架与组合层
- 定义跨层依赖和入口接线
- 审核其他 agent 的跨层改动需求

允许写入：

- `Assets/Scripts/App.Shared`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Entry`
- `Assets/HotUpdateContent/Script/App.HotUpdate` 的组合层和模块根目录

禁止写入：

- UI prefab
- 地图生成细节
- 任务奖励公式
- 侦探社业务细节

启动前提：

- 无，默认是第一启动位

验收重点：

- Shared 侧类型是否最小化
- AOT 是否没有被塞进玩法逻辑
- HotUpdate 入口是否稳定可挂接

### Agent 2：地图与棋盘

必带 skill：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责固定为：

- 接入 `MinesweeperTerrainData`
- 实现 `BoardTemplate`、`LevelSnapshot`
- 实现有效格、猫布点、数字计算、揭示、扩散、通关判定
- 完成 terrain -> runtime board template 的转换

允许写入：

- 地图生成目录
- 棋盘逻辑目录
- terrain adapter
- 纯逻辑运行时地图数据

禁止写入：

- `App.Shared`
- HotUpdate 入口
- UI prefab
- 任务栏规则
- 广告 / 离线 / 持久化业务

启动前提：

- Shared DTO 已冻结
- HotUpdate 骨架已就位

验收重点：

- 运行时状态没有写回 terrain 资产
- 没有把任务或 UI 逻辑塞进棋盘模型
- 输出接口足够给 UI 和任务线消费

### Agent 3：任务与长期进度

必带 skill：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责固定为：

- 实现任务栏 5 槽规则
- 实现任务抽取、去重、奖励计算、领奖补位
- 实现长期成长相关服务
- 预留广告解锁、离线收益、经验接入口

允许写入：

- 任务服务目录
- 奖励和任务进度目录
- Meta progression 目录

禁止写入：

- `App.Shared`
- HotUpdate 入口
- UI prefab
- 棋盘底层揭示逻辑

启动前提：

- Shared DTO 已冻结
- 任务线依赖的槽位与任务实例字段已稳定

验收重点：

- 奖励公式没有写进 UI
- 任务服务没有直接侵入棋盘底层实现
- 输出结果能稳定供 UI 和结算消费

### Agent 4：UI 与验证

必带 skill：

- `unity-hotupdate-boundary`
- `unity-ugui-flow-integration`

职责固定为：

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

启动前提：

- 地图线和任务线的核心输出接口已稳定
- 本轮不再频繁改 DTO

验收重点：

- UI 只做表现和交互编排
- 没有把业务核心逻辑写进 Presenter / Controller
- Prefab 和场景绑定改动集中在 UI 线独占处理

### Agent 5：测试与质量保障

必带 skill：

- `unity-hotupdate-boundary`

按测试对象叠加：

- 测地图、任务、配置时，加 `findcat-config-pipeline`
- 测 UI 流程时，加 `unity-ugui-flow-integration`

职责固定为：

- 写单元测试、集成测试和验证脚本
- 校验地图、任务、奖励、时间规则和配置抽取是否正确
- 验证其他 agent 的实现是否符合边界和输入输出约定
- 做冒烟测试、回归测试和独立审查

允许写入：

- 测试目录
- 校验脚本
- 模拟器
- QA 文档

禁止写入：

- `App.Shared`
- HotUpdate 入口
- UI prefab
- 核心业务实现目录

启动前提：

- Agent 1 已冻结最小边界和 DTO
- 至少已有一条功能线产出可测试结果，或需要提前搭测试骨架

验收重点：

- 测试是否真的覆盖关键规则，而不是只做空壳
- 是否能指出其他 agent 的越界、错接线、字段不一致问题
- 是否把问题反馈给对应实现线，而不是顺手主改业务实现

## 标准启动模板

主控每次启动 Agent，默认使用下面这类模板。
不要只说“你去实现某某功能”，而要把边界和交付说清楚。

### 通用模板

```text
你负责本项目的【Agent 名称】实现。请遵循【skill 列表】。

目标：
1. 【本轮目标 1】
2. 【本轮目标 2】
3. 【本轮目标 3】

约束：
- 你只能主写【允许写入范围】
- 不要修改【禁止写入范围】
- 你依赖的前提是【DTO / 入口 / 上游输出】
- 如果发现需要新增跨层类型或突破写入边界，先停下并上报，不要自行扩边界

交付：
- 列出你修改的文件
- 说明输入、输出和依赖接口
- 说明还有哪些未完成项
- 说明风险、阻塞和需要主控确认的点
```

### Agent 1 启动模板

```text
你负责本项目的边界与骨架实现。请遵循 $unity-hotupdate-boundary。

目标：
1. 冻结 App.Shared 中本期需要的最小 DTO、接口、事件
2. 在 App.HotUpdate 中建立正式业务模块骨架和组合层
3. 保持 AOT 只做宿主基础设施，不承载寻猫业务

约束：
- 你是唯一允许主写 App.Shared 的 agent
- 不要修改 UI prefab
- 不要实现地图生成、任务奖励、侦探社业务细节
- 如需新增跨层类型，保持最小化

交付：
- 列出你修改的文件
- 说明冻结后的 DTO 和入口依赖关系
- 标出其他 agent 可以依赖的接口
- 说明还有哪些边界未冻结
```

### Agent 2 启动模板

```text
你负责本项目的地图与棋盘实现。请遵循 $unity-hotupdate-boundary 和 $findcat-config-pipeline。

目标：
1. 接入 MinesweeperTerrainData 作为地图模板输入
2. 实现 BoardTemplate、LevelSnapshot 和地图运行时状态
3. 实现有效格布点、猫生成、数字计算、揭示、扩散和通关判定

约束：
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口和组合层
- 不要碰 UI prefab
- 不要实现任务奖励和广告逻辑
- 运行时状态不能回写到 terrain 资产

交付：
- 列出你修改的文件
- 说明地图生成输入、输出和依赖 DTO
- 说明 UI 和任务线将如何消费你的格子状态和关卡状态
- 标出你还缺哪些上游前提
```

### Agent 3 启动模板

```text
你负责本项目的任务与长期进度实现。请遵循 $unity-hotupdate-boundary 和 $findcat-config-pipeline。

目标：
1. 实现任务栏 5 槽规则和任务补位
2. 实现任务抽取、去重、奖励计算、领奖推进
3. 预留广告解锁、离线收益和长期成长接入口

约束：
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口
- 不要碰 UI prefab
- 不要改棋盘底层揭示逻辑
- 如果发现当前 DTO 不够，先上报边界缺口，不要自行扩 Shared

交付：
- 列出你修改的文件
- 说明任务输入、输出和依赖 DTO
- 说明地图完成后如何推进任务和长期进度
- 标出还缺哪些时间、广告或持久化前提
```

### Agent 4 启动模板

```text
你负责本项目的 UI 与验证实现。请遵循 $unity-hotupdate-boundary 和 $unity-ugui-flow-integration。

目标：
1. 接主界面、任务栏、领奖、广告锁位和结算流程
2. 接侦探社相关页面和展示
3. 做基础联调和冒烟验证

约束：
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口
- 不要实现核心奖励公式和棋盘底层算法
- UI 只做表现和交互编排，不承载核心业务规则

交付：
- 列出你修改的文件
- 说明页面流转和绑定关系
- 说明依赖了哪些地图线和任务线输出
- 给出一份冒烟验证清单
```

### Agent 5 启动模板

```text
你负责本项目的测试与质量保障实现。请遵循 $unity-hotupdate-boundary。
如果测试对象涉及地图、任务、配置，请额外遵循 $findcat-config-pipeline。
如果测试对象涉及 UI 流程，请额外遵循 $unity-ugui-flow-integration。

目标：
1. 为当前阶段的核心规则补单元测试、集成测试或验证脚本
2. 校验其他 agent 的输入输出、边界和关键逻辑是否正确
3. 输出明确的通过项、失败项、风险和回归建议

约束：
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口
- 不要主改 UI prefab
- 不要把发现的问题直接改成新的业务实现，优先回给对应 agent 修

交付：
- 列出你修改的文件
- 说明这轮覆盖了哪些测试面
- 列出失败项、风险和回归建议
- 标出需要哪个 agent 继续处理
```

## 验收规范

主控验收 Agent 结果时，必须至少检查下面这些内容。

### 通用验收项

- 改动是否落在允许写入范围内
- 有没有碰到明确禁止的目录或模块
- 有没有遗漏要求的 skill 边界
- 有没有说明修改文件、输入输出、未完成项、风险和阻塞
- 有没有擅自扩 DTO、改入口、改 prefab

### 边界验收项

- `App.Shared` 是否只被边界 agent 改动
- `App.HotUpdate` 是否没有直接依赖 AOT 具体实现
- UI 是否没有承载奖励公式、地图生成、任务生成
- `MinesweeperTerrainData` 是否仍然只是模板输入

### 交付验收项

- 代码是否能被下游 agent 明确消费
- 命名和输出结构是否稳定，不会马上返工改名
- 是否留下了足够的交接说明

### 不通过的典型情况

- 地图 agent 顺手改了 `App.Shared`
- 任务 agent 顺手改了 HotUpdate 入口
- UI agent 把奖励计算写进 Presenter
- 测试 agent 直接越俎代庖主改业务实现
- agent 汇报里只说“完成了”，但没有列文件、依赖、风险
- 上游接口未稳定，却已经提前全速启动 UI

只要出现上面这些情况，就不应直接视为通过。

## 主控执行规则

主控在实际协作时固定按下面顺序推进：

1. 先判断当前阶段和 DTO 状态
2. 再决定启动哪一个 agent
3. 启动时使用固定模板，不临时口述
4. agent 返回后，先按验收规范检查
5. 验收通过后，再决定是否启动下一个 agent 或交给 Agent 5 做验证
6. 本轮结束后，执行文档维护流程

这条顺序不能反过来。  
尤其不能先把 agent 全放出去，再回来补边界和验收。

## Assumptions

- 当前项目默认先按 5-Agent 实操版执行，8-Agent 长期版仍保留给后续扩编时使用。
- `unity-hotupdate-boundary` 是所有 agent 的基础 skill，不单独省略。
- Shared DTO、HotUpdate 入口、UI prefab 这三类高冲突区域继续保持独占写入。
- 这份规范是长期规则，后续如果 agent 分工方式变化，应优先更新本文件。
