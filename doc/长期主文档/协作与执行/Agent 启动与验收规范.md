# Agent 启动与验收规范

这页只负责启动、调度、职责、验收和 Agent 6 闭环规则。

这页不重复提供：

- 新会话第一句怎么说
- 可直接复制的口令模板
- 固定三段收尾和 Git 提交流程

对应入口分别看：

- [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md)
- [Agent 启动口令清单](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动口令清单.md)
- [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

## Summary

这份规范固定 Holmas 项目的 Agent 启动方式和验收方式。  
目标不是“每次临时想一遍怎么分工”，而是把启动协议、写入边界、验收门和修复闭环收成一套稳定规则。

## 新会话默认入口

如果新会话只输入：

```text
按长期主文档规则执行。
```

主线程固定按两阶段推进：

- `briefing`
  - 只读项目总览、迭代记录索引和最新迭代摘要
  - 输出当前主线、当前阻塞、任务建议
- `execution dispatch`
  - 只有在用户确认开始执行某个任务后进入
  - 主线程才去读协作文档、检查总注册表，并判断主线程直做、复用 helper 或启动真实 subagent

如果用户直接说：

```text
按长期主文档规则执行，默认启动subagent。
```

表示跳过 `briefing`，直接进入 `execution dispatch`，并优先按长期编组方案判断是否拉起真实 subagent。

## 启动前检查

主控在真正启动任何 Agent 之前，必须先完成这组检查：

1. 当前处于哪个项目阶段
- 边界冻结
- 并行开发
- UI 接线 / 集成 / 回归

2. Shared DTO 是否已冻结
- `App.Shared` 未冻结时，不允许地图线、任务线、UI 线一起开工
- UI 线默认要等核心输出接口稳定后再全速启动

3. 高冲突目录是否已独占
- `App.Shared`
- `Assets/HotUpdateContent/Script/App.HotUpdate/Entry`
- HotUpdate composition root
- UI prefab / scene binding

4. 本轮每个 Agent 的写入边界是否明确
- 能写哪些目录
- 不能写哪些目录
- 哪些类型只能读取不能改

## 进入 Agent 4 / 联调前置条件

这组条件只负责定义“什么时候可以进入 UI 联调”；当前轮次是否已经满足，统一写在最新迭代记录里，不写死在项目总览。

- Shared DTO 字段命名继续保持冻结，不再为 Agent 2 / Agent 3 回改
- Agent 2 与 Agent 3 的首批纯逻辑已完成一次真实编辑器编译验证
- 地图完成 -> 任务推进 -> 长期进度的调用链已经接入组合层，并可在无 UI 环境下执行
- Agent 5 的基础测试、EditMode 执行入口与 smoke validation 已完成首轮验证

## 启动协议

每次启动 Agent，主控都必须一次说清：

- Agent 名称
- 本轮目标
- 必带 skill
- 允许写入范围
- 禁止写入范围
- 依赖前提
- 明确交付物
- 完成后的汇报格式

固定组织顺序：

1. 你是谁
2. 你必须遵循哪些 skill
3. 你的目标是什么
4. 你的边界是什么
5. 你的交付物是什么
6. 你的汇报方式是什么

## 6-Agent 固定规范

当前项目默认先使用 6-Agent 实操版。

- `Agent 1`：边界与骨架
- `Agent 2`：地图与棋盘
- `Agent 3`：任务与长期进度
- `Agent 4`：UI 与验证
- `Agent 5`：测试与质量保障
- `Agent 6`：挑刺与问题审查

## 角色与实例模型

固定区分下面三层概念：

- `Agent 1~6` 是职责模板，不等于真实 subagent 实例
- “执行 Agent X”默认表示主线程先按该职责推进，不预设一定新开真实 subagent
- 新会话只说 `按长期主文档规则执行。` 时，默认先走 `briefing`；任务一旦确认，主线程就可以按长期规则自动判断是否委派真实 subagent
- 只有用户显式补充“这轮不要开真实 subagent”时，主线程才必须禁用真实 subagent 委派
- `Agent 6` 永远只负责审查、裁定、退回和复审，不接管实现

## 线程级调度阶段与真实 subagent 模式

主线程每轮固定先判断当前线程处于哪个调度阶段。

- `thread_dispatch_stage = briefing`
  - 默认值
  - 只做低 token 主线判断
  - 不扫代码
  - 不直接拉起真实 subagent
- `thread_dispatch_stage = execution`
  - 用户确认某个任务后进入
  - 主线程必须再读协作文档、检查总注册表，并判断是否需要 helper 或真实 subagent
  - 某条实现链达到阶段里程碑后，默认进入 Agent 6 审查闭环

只有进入 `thread_dispatch_stage = execution` 后，主线程才判断真实 subagent 模式：

- `thread_real_subagent_mode = disabled`
  - 用户显式要求“这轮不要开真实 subagent”
  - 主线程不得自动补起新的真实 subagent
- `thread_real_subagent_mode = auto`
  - 默认值
  - 主线程按长期编组方案和线程级总注册表自动判断是否需要真实 subagent
  - 如果 Agent 6 退回 findings 且当前没有原实现真实 subagent，允许自动补起同职责真实 subagent 继续闭环
- `thread_real_subagent_mode = preferred`
  - 用户直接使用 `按长期主文档规则执行，默认启动subagent。`
  - 可跳过 `briefing`，直接进入 `execution`，并优先使用真实 subagent 承担实现职责

## 线程级真实 agent 注册表

只要当前线程使用了真实 subagent，主线程就必须维护一份线程级注册表。

如果当前线程同时维护 helper role，则真实 agent 注册表应视为线程级总注册表中的 `execution` 子集；helper role 的调度、复用、关闭与压缩计数规则，统一看 [线程级辅助 subagent 角色](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/线程级辅助 subagent 角色.md)。

每个真实 agent 至少记录：

- 职责编号
- agent id / nickname
- 是否为真实 subagent
- 当前状态
- 是否是当前修复链的原实现方

推荐状态：

- `not_started`
- `in_progress`
- `in_review`
- `returned_for_fix`
- `fixing`
- `pending_re_review`
- `passed`
- `closed`

## 迭代文档默认分工状态

这一节专门给 `scripts/update_project_docs.py` 读取。  
如果后续要调整迭代记录里的默认分工状态，只改这里，不要去改脚本里的自由文本。

- Agent 1：Shared 与骨架继续保持冻结，本轮未改
- Agent 2：地图与棋盘纯逻辑作为被验证对象，本轮未改生产逻辑
- Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑
- Agent 4：UI 与验证，暂未启动
- Agent 5：测试与质量保障，默认按需启动，本轮未启动
- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动

## 职责与边界

### Agent 1：边界与骨架

必带 skill：

- `unity-hotupdate-boundary`

职责：

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

- 无，默认第一启动位

验收重点：

- Shared 侧类型是否最小化
- AOT 是否没有被塞进玩法逻辑
- HotUpdate 入口是否稳定可挂接

### Agent 2：地图与棋盘

必带 skill：

- `unity-hotupdate-boundary`
- `findcat-config-pipeline`

职责：

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

职责：

- 实现任务栏 5 槽规则
- 实现任务抽取、去重、奖励计算、领奖补位
- 实现长期成长相关服务
- 预留广告解锁、离线收益、经验接入口

允许写入：

- 任务服务目录
- 奖励和任务进度目录
- 长期进度目录

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

启动前提：

- 地图线和任务线的核心输出接口已稳定
- 本轮不再频繁改 DTO
- 进入 UI 联调前还必须满足本页“进入 Agent 4 / 联调前置条件”

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

职责：

- 可提前并行搭测试骨架，也可在阶段产出后补专项验证
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

启动前提：

- Agent 1 已冻结最小边界和 DTO
- 至少已有一条功能线产出可测试结果，或需要提前搭测试骨架

验收重点：

- 测试是否真的覆盖关键规则，而不是空壳
- 是否能指出越界、错接线、字段不一致问题
- 是否把问题回给对应实现线，而不是顺手主改业务实现

### Agent 6：挑刺与问题审查

必带 skill：

- `unity-hotupdate-boundary`

按审查对象叠加：

- 审地图、任务、配置时，加 `findcat-config-pipeline`
- 审 UI 流程时，加 `unity-ugui-flow-integration`

职责：

- 独立审查当前交付是否存在 bug、回归、越界、误解需求、缺关键验证或交接不完整
- 只输出问题、风险、严重级别、退回建议和修复归属
- 作为阶段里程碑继续推进前的默认审查门
- 同一条审查链里默认优先复用原 Agent 6 持续负责复审

允许写入：

- 审查结论文档
- QA / review 文档
- 审查脚本或只读检查脚本

禁止写入：

- `App.Shared`
- HotUpdate 入口
- UI prefab
- 核心业务实现目录
- 用“顺手修一下”的方式直接替代原实现 agent 修问题

启动前提：

- 至少已有一份明确阶段产出
- 主控已经完成基础验收，确认交付范围和依赖说明可审

阶段里程碑默认包括：

- 某条功能线完成并已通过本地基础验证
- 准备做最终汇总
- 准备切下一阶段
- 准备对用户宣告“本阶段已通过”

验收重点：

- 是否明确给出 `通过 / 通过，但有非阻塞建议 / 未通过，退回修复`
- 是否把问题归类到 bug、回归、越界、需求理解、验证缺失或交接缺失
- 是否明确指出问题归属给哪个实现 agent 修

## 验收规范

主控验收 Agent 结果时，必须至少检查：

- 改动是否落在允许写入范围内
- 有没有碰到明确禁止的目录或模块
- 有没有遗漏要求的 skill 边界
- 有没有说明修改文件、输入输出、未完成项、风险和阻塞
- 有没有擅自扩 DTO、改入口、改 prefab

不通过的典型情况：

- 地图 agent 顺手改了 `App.Shared`
- 任务 agent 顺手改了 HotUpdate 入口
- UI agent 把奖励计算写进 Presenter
- 测试 agent 直接越俎代庖主改业务实现
- 挑刺 agent 直接越俎代庖主改业务实现
- agent 汇报里只说“完成了”，但没有列文件、依赖、风险
- 上游接口未稳定，却已经提前全速启动 UI

## 主控对外可见状态规则

只要当前有 subagent 仍在处理中，就要对用户显示等待提示。

推荐格式：

```text
等待 Agent 2（Kepler）修复问题中...
等待 Agent 4（Bacon）验证结果中...
```

关于审查结果回显：

- `pending` 只表示“审查已发起、结果未回”
- `review_deferred` 的对外文案固定写成 `待回补复审`
- `Agent 6` 每次返回 findings 后，主控都必须把结论明确回显给用户
- 每条问题至少包含严重级别、文件路径、核心问题描述
- 如果结论为无阻塞问题，也要明确写：

```text
Agent 6 审查结果：通过
```

## 主控执行规则

主控在实际协作时固定按下面顺序推进：

1. 先判断当前阶段和 DTO 状态
2. 再判断当前线程是 `briefing` 还是 `execution`
3. 如果已进入 `execution`，再判断真实 subagent 模式是 `disabled / auto / preferred`
4. 再决定由主线程按哪个职责执行，或启动哪一个真实 subagent
5. 启动时按本规范给全目标、边界、交付物和汇报方式
6. 实现线返回后，先按验收规范检查
7. 某条实现线达到阶段里程碑并通过基础验收后，默认交给 Agent 6 做挑刺与问题审查
8. Agent 6 未通过时，先按固定路由规则退回实现方修复；修完后默认优先交回同一审查链复审
9. 只有所有相关阶段里程碑都通过 Agent 6 后，才决定是否进入下一个阶段或做最终汇总
10. 本轮结束后，执行 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

如果这轮同时有多个实现 subagent，则每条链都按下面的闭环方式执行：

- `subagent 阶段里程碑完成 -> Agent 6 审查 -> 原 subagent 修复 -> 同一审查链复审 -> 通过`
- 多个 subagent 并行时，每条链各自闭环，不能把 A 的问题混给 B 修
- 某条链还没通过 Agent 6 时，这条链只允许继续修复和复审，不视为“已收口”

## 问题归属映射表

主线程收到 Agent 6 findings 后，默认按下面的固定映射判定修复归属：

- Shared / Entry / 组合层 / AOT-HotUpdate 边界：`Agent 1`
- 地图 / 棋盘 / terrain / board runtime：`Agent 2`
- 任务 / 奖励 / 成长 / 配置恢复业务：`Agent 3`
- UI / Presenter / Prefab / 绑定 / 联调流程：`Agent 4`
- 单测 / 集成测试 / smoke / 验证脚本 / 回归链路：`Agent 5`
- 审查结论 / 阻塞裁定 / 复审：`Agent 6`

如果一个 finding 横跨多职责，主线程必须先拆成多条修复链，再分别路由；不允许把跨职责问题含糊退回给一个 agent 全包。

## Agent 6 退回后的强制动作顺序

只要 Agent 6 返回的是 `未通过，退回修复`，主线程必须按下面顺序执行：

1. 逐条回显 findings
2. 逐条判定归属职责；如果跨职责，先拆链
3. 查询线程级真实 agent 注册表
4. 如果已有该职责的原实现真实 subagent，直接退回原 subagent
5. 如果没有原实现真实 subagent，但 `thread_real_subagent_mode = auto / preferred`，自动补起同职责真实 subagent
6. 如果当前仍在 `briefing`，或 `thread_real_subagent_mode = disabled`，必须明确回显“当前线程未授权自动补起真实 subagent”，再由主线程兜底或等待用户指令
7. 等待修复中的实现线时，持续对外显示等待状态
8. 修复完成后，主线程先做基础验收与必要验证
9. 自动优先送回同一审查链复审

补充规则：

- 默认优先复用同一个、已经启动过的 Agent 6 做复审
- 只有原 Agent 6 明确不可用、上下文明显失真，或主控明确要求换审时，才允许同职责 reviewer 接手同一 `review_chain_id`
- 如果当前没有原实现真实 subagent，但线程已进入 `execution` 且 `thread_real_subagent_mode = auto / preferred`，主线程必须自动补起同职责真实 subagent，而不是让链路停住
- 如果当前没有原实现真实 subagent，且线程仍在 `briefing` 或 `thread_real_subagent_mode = disabled`，主线程必须明确回显“当前线程未授权自动补起真实 subagent”
- 只有两种例外允许主线程接管修复：
  - 用户明确要求主线程接管
  - 原实现真实 subagent 已明确不可用，且主线程已向用户说明这次是兜底接管
