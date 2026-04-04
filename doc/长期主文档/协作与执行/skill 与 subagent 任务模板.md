# skill 与 subagent 任务模板

## 1. 当前三份 skill

当前项目已经落地三份可用 skill：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`
- `unity-ugui-flow-integration`

这三份 skill 的使用原则是：

- 所有正式功能开发默认先带 `unity-hotupdate-boundary`
- 涉及表结构、权重、任务生成、地图生成、奖励公式时，再叠加 `findcat-config-pipeline`
- 涉及 UGUI、Prefab、Presenter、流程接线时，再叠加 `unity-ugui-flow-integration`

## 2. 每份 skill 的定位

### `unity-hotupdate-boundary`

适用场景：

- 改 `App.Shared`
- 改 `App.AOT`
- 改 `App.HotUpdate`
- 做 HotUpdate 业务模块拆分
- 做 YooAssets 资源接入
- 做 `MinesweeperTerrainData` 接入
- 做 subagent 分工和边界审查

核心作用：

- 约束 `App.AOT / App.Shared / App.HotUpdate` 的职责
- 禁止把业务逻辑写进 AOT 和 UI
- 强制把 `MinesweeperTerrainData` 当成地图模板，而不是运行时状态

### `findcat-config-pipeline`

适用场景：

- 改地图表、猫表、任务表、玩家等级表
- 改任务栏填充规则
- 改地图生成规则
- 改奖励公式
- 改权重和随机逻辑
- 做配置校验和模拟生成

核心作用：

- 固化表字段含义
- 固化任务栏去重规则
- 固化地图生成必须围绕任务栏猫种池的规则
- 保证配置输入和运行时状态分离

### `unity-ugui-flow-integration`

适用场景：

- 改找猫界面
- 改任务栏、领奖、锁槽位、弹窗
- 改侦探社界面
- 改 Prefab 和场景绑定
- 改 Presenter / Controller
- 做 UI 冒烟联调

核心作用：

- 强制 UI 只做表现和交互编排
- 禁止把奖励公式、生成逻辑、持久化逻辑写进 UI
- 约束 Prefab、Presenter 和流程接线的职责边界

## 3. 三份 skill 的组合方式

### 组合 A：架构与边界

使用：

- `unity-hotupdate-boundary`

适合：

- 冻结 DTO
- 搭模块骨架
- 写服务注册和组合层
- 审查分层是否越界

### 组合 B：配置与生成

使用：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

适合：

- 实现地图表、猫表、任务表、等级表
- 实现任务生成、地图生成、奖励计算
- 写配置加载器、校验器、模拟器

### 组合 C：UI 与流程

使用：

- `unity-hotupdate-boundary`
- `unity-ugui-flow-integration`

适合：

- 实现 UGUI 页面
- 绑定 Prefab
- 做找猫和侦探社流程接线
- 做冒烟联调

## 4. 推荐开发顺序

第一阶段：

- 先用 `unity-hotupdate-boundary`
- 冻结 `App.Shared` DTO
- 冻结 HotUpdate 入口和模块骨架

第二阶段：

- 配置和生成线使用 `unity-hotupdate-boundary + findcat-config-pipeline`
- UI 线使用 `unity-hotupdate-boundary + unity-ugui-flow-integration`

第三阶段：

- 做统一集成
- 跑配置校验
- 跑 UI 和流程冒烟

## 5. 第一批 subagent 任务模板

第一批建议按 5 个 subagent 开工。  
这是当前项目最稳的起步方式。

### Agent 1：边界与骨架

skill 组合：

- `unity-hotupdate-boundary`

职责：

- 冻结 `App.Shared` 的最小 DTO、接口、事件
- 搭 `App.HotUpdate` 正式模块骨架
- 定义组合层、服务注册和入口接线
- 统一审核其他 agent 的跨层改动需求

允许写入：

- `Assets/Scripts/App.Shared`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Entry`
- `Assets/HotUpdateContent/Script/App.HotUpdate` 下的组合层和模块根目录

禁止写入：

- UI prefab
- 地图生成细节
- 任务公式
- 侦探社业务细节

交付物：

- 冻结后的 DTO 列表
- 模块目录结构
- HotUpdate 入口接线代码
- 其他 agent 依赖的接口清单

可直接使用的任务模板：

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
```

### Agent 2：地图与棋盘

skill 组合：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责：

- 接入 `MinesweeperTerrainData`
- 实现 `BoardTemplate`
- 实现 `LevelSnapshot`
- 实现有效格、猫布点、数字计算、揭示、扩散、通关判定
- 完成 terrain -> runtime board template 的转换

允许写入：

- `App.HotUpdate` 下的棋盘逻辑目录
- 地图生成目录
- terrain adapter

禁止写入：

- `App.Shared`
- HotUpdate 入口
- UI prefab
- 任务栏规则
- 广告和离线逻辑

交付物：

- `BoardTemplate`
- `LevelSnapshot`
- 地图生成输入输出接口
- 可供 UI 消费的格子状态输出

可直接使用的任务模板：

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
- 说明 UI 将如何消费你的格子状态和关卡状态
```

### Agent 3：任务与长期进度

skill 组合：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责：

- 实现任务栏 5 槽规则
- 实现任务抽取、去重、奖励计算、领奖补位
- 实现侦探社成长、家具/猫窝/养猫的元进度服务
- 预留广告解锁、离线收益、经验接入口
- 与地图完成事件联动推进任务和长期进度

允许写入：

- `App.HotUpdate` 下的任务服务目录
- 长期进度目录
- 奖励和任务进度目录

禁止写入：

- `App.Shared`
- HotUpdate 入口
- UI prefab
- 棋盘底层揭示逻辑

交付物：

- `TaskInstanceData`
- `TaskSlotState`
- 奖励公式实现
- 任务推进接口
- 长期进度更新接口

可直接使用的任务模板：

```text
你负责本项目的任务与长期进度实现。请遵循 $unity-hotupdate-boundary 和 $findcat-config-pipeline。

目标：
1. 实现任务栏 5 槽规则与去重逻辑
2. 实现普通任务奖励公式和任务补位
3. 实现侦探社成长、家具/猫窝/养猫的元进度服务
4. 为广告解锁、离线收益、经验来源预留清晰接口

约束：
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口
- 不要碰 UI prefab
- 不要实现棋盘揭示与数字算法
- UI 只消费你的状态，不由你写 UI

交付：
- 列出你修改的文件
- 说明任务实例、槽位状态、奖励和元进度的接口
- 说明地图完成后如何推进任务和长期进度
```

### Agent 4：UI 与验证

skill 组合：

- `unity-hotupdate-boundary`
- `unity-ugui-flow-integration`

职责：

- 实现找猫主界面、任务栏、领奖、广告锁位、结算面板
- 实现侦探社界面、家具、猫窝、养猫展示
- 做 Presenter / Controller / 绑定逻辑
- 做基础联调和冒烟流程

允许写入：

- `App.HotUpdate` 下的 UI、Presenter、Controller 目录
- UI prefab
- scene binding

禁止写入：

- `App.Shared`
- HotUpdate 入口
- 奖励公式
- 任务生成
- 地图生成
- 存档和持久化规则

交付物：

- 主流程 UI
- 页面流转和交互绑定
- 冒烟验证清单

可直接使用的任务模板：

```text
你负责本项目的 UI 与流程接线。请遵循 $unity-hotupdate-boundary 和 $unity-ugui-flow-integration。

目标：
1. 实现找猫、任务栏、领奖、结算和侦探社相关界面
2. 实现 Presenter / Controller 和 Prefab 绑定
3. 跑通玩家主流程，并输出冒烟验证结果

约束：
- 你是唯一允许主改 UI prefab 和场景绑定的 agent
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口
- 不要把奖励公式、任务生成、地图生成、存档规则写进 UI
- UI 只消费运行时状态

交付：
- 列出你修改的文件
- 说明每个主界面的状态来源和动作出口
- 给出一份可复现的冒烟流程
```

### Agent 5：测试与质量保障

skill 组合：

- 默认：`unity-hotupdate-boundary`
- 测地图、任务、配置时：再叠加 `findcat-config-pipeline`
- 测 UI 流程时：再叠加 `unity-ugui-flow-integration`

职责：

- 写单元测试、集成测试和验证脚本
- 校验地图、任务、奖励、时间规则和配置抽取是否正确
- 验证其他 agent 的实现是否符合边界和输入输出约定
- 做冒烟测试、回归测试和专项质量保障

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

交付物：

- 单元测试或集成测试代码
- 覆盖面说明
- 失败项和风险清单
- 需要回给哪个 agent 修复的问题列表

可直接使用的任务模板：

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

### Agent 6：挑刺与问题审查

skill 组合：

- 默认：`unity-hotupdate-boundary`
- 审地图、任务、配置时：再叠加 `findcat-config-pipeline`
- 审 UI 流程时：再叠加 `unity-ugui-flow-integration`

职责：

- 独立挑刺，找明显 bug、回归、越界、误解需求和缺关键验证
- 给出 `通过 / 通过，但有非阻塞建议 / 未通过，退回修复` 的结论
- 把问题退回给原实现 agent 或主控继续修复
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
- 如果是复审，要明确说明哪些问题已关闭、哪些问题仍阻塞

可直接使用的任务模板：

```text
你负责本项目的挑刺与问题审查。请遵循 $unity-hotupdate-boundary。
如果审查对象涉及地图、任务、配置，请额外遵循 $findcat-config-pipeline。
如果审查对象涉及 UI 流程，请额外遵循 $unity-ugui-flow-integration。

目标：
1. 独立审查当前交付是否存在明显 bug、回归、越界、误解需求或缺关键验证
2. 给出明确结论：通过 / 通过，但有非阻塞建议 / 未通过，退回修复
3. 指明问题归属给哪个实现 agent 或主控继续修复

约束：
- 不要修改 App.Shared
- 不要修改 HotUpdate 入口
- 不要主改 UI prefab
- 不要把发现的问题直接改成新的业务实现
- 你的职责是挑刺、裁定和退回，不是接管实现

交付：
- 列出你审查的对象和范围
- 给出审查结论
- 列出发现的问题、严重级别和是否阻塞
- 标出退回给谁修，以及复审条件
- 如果这是复审，优先沿用你上一次的审查口径继续裁定
```

## 6. 主控 agent 的集成规则

主控 agent 负责：

- 先判断当前线程是否开启默认真实 subagent 自动闭环
- 维护线程级真实 agent 注册表，记录每个真实 subagent 的职责、agent id、状态和是否原实现方
- 先让 Agent 1 冻结 DTO 和模块骨架
- 再让 Agent 2、Agent 3 并行
- Agent 5 可提前搭测试骨架，也可在功能线产出后补专项验证
- 每个阶段产出默认交给 Agent 6 做挑刺审查
- 如果 Agent 6 不通过，必须先按职责归属路由回实现线；修完后默认交回同一个 Agent 6 复审
- 等核心状态接口稳定后，再启动 Agent 4 的全量接线
- 最后统一 review、集成和回归

主控集成检查：

- `App.Shared` 是否被多方同时改动
- 运行时状态是否被写回配置或 terrain 资产
- UI 是否只消费状态，不生成业务结果
- 奖励公式是否只存在于服务层
- Prefab 是否只由 UI 线统一维护

主控在 Agent 6 退回 findings 后，固定按下面顺序推进：

1. 逐条回显 findings
2. 逐条判定归属职责
3. 如果一个 finding 横跨多职责，先拆成多条修复链
4. 查询线程级真实 agent 注册表
5. 如果已有原实现真实 subagent，直接退回原实现方
6. 如果没有原实现真实 subagent，但当前线程已开启默认真实 subagent 自动闭环，自动补起同职责真实 subagent
7. 如果当前线程未授权自动闭环，明确回显“当前线程未授权自动补起真实 subagent”，再由主线程兜底或等待用户指令
8. 修复完成后，先做基础验收与必要验证
9. 默认交回同一个 Agent 6 复审

默认问题归属映射表：

- Shared / Entry / 组合层 / AOT-HotUpdate 边界：`Agent 1`
- 地图 / 棋盘 / terrain / board runtime：`Agent 2`
- 任务 / 奖励 / 成长 / 配置恢复业务：`Agent 3`
- UI / Presenter / Prefab / 绑定 / 联调流程：`Agent 4`
- 单测 / 集成测试 / smoke / 验证脚本 / 回归链路：`Agent 5`
- 审查结论 / 阻塞裁定 / 复审：`Agent 6`

## 7. 推荐给我的启动指令

后续你可以直接这样对我说：

```text
这次按 6 个 subagent 开工。
默认启动真实 subagent，并开启默认真实 subagent 自动闭环。
全部默认遵循 $unity-hotupdate-boundary。
地图和任务相关额外遵循 $findcat-config-pipeline。
UI 相关额外遵循 $unity-ugui-flow-integration。
测试和挑刺审查按对象叠加 $findcat-config-pipeline 或 $unity-ugui-flow-integration。
App.Shared 和 HotUpdate 入口只能边界 agent 改，UI prefab 只能 UI agent 改。
先冻结 DTO，再并行开发；每个阶段产出默认先交给 Agent 6 挑刺审查，通过后再继续推进。
如果 Agent 6 不通过，先退回原实现方修复；如果当前没有原实现真实 subagent，就自动补起同职责真实 subagent 接手修复。
修完后默认交回同一个 Agent 6 复审，不要新开新的审查 agent。
```

## 8. 下一步建议

最推荐的下一步是：

- 先启动 Agent 1，冻结第一批 DTO 和 HotUpdate 模块骨架
- 骨架稳定后，再启动 Agent 2 和 Agent 3 并行
- 同时启动 Agent 5，先补单测和验证脚本，再持续跟进功能回归
- 每条功能线完成后，先交给 Agent 6 做挑刺审查；不通过就退回原实现线修复，修完后默认交回同一个 Agent 6 复审
- 最后再让 Agent 4 做 UI 接线和冒烟

这样三份 skill 能真正参与协作，而不是只停留在文档层。
