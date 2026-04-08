# 线程级辅助 subagent 角色

这页只负责 helper role 的定义、注册表、复用、关闭和压缩计数规则。

这页不重复解释：

- 新会话默认入口和阶段判断总规则
- 执行型 `Agent 1 ~ 6` 的职责边界和验收规则
- helper 的覆盖模板
- 固定三段收尾和 Git 提交流程

相关入口分别看：

- [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md)
- [skill 与 subagent 任务模板](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/skill%20与%20subagent%20任务模板.md)
- [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
- [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

## Summary

这份文档定义 Holmas 项目里的线程级辅助 subagent 角色。  
helper role 不是新的官方 `Agent` 编号，也不是执行型真实 subagent 的替代品，而是主线程为了低成本做主线判断和规则审计而维护的只读辅助角色。

固定目标：

- 把 helper 的复用、换新和关闭规则收成线程内可追踪事实
- 保持 helper 只做只读分析和审计，不污染执行型 Agent 分工
- 让换环境后仍然能按 `role_name` 稳定恢复同职责 helper

## 与执行型 Agent 的边界

- 执行型 `Agent 1 ~ 6` 的职责、写入边界和验收规则统一看 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
- helper role 负责：
  - 读取长期主文档、迭代记录和必要代码入口
  - 输出主线判断、阻塞判断、下一步建议
  - 审计协作规则、收尾规则和流程漏洞
- helper role 不负责：
  - 主写业务实现
  - 代替 `Agent 6` 输出代码审查结论
  - 代替执行型 Agent 接管修复闭环

## helper 在默认工作流中的位置

新会话只要已经按 [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md) 进入默认入口，主线程就必须自己判断是否需要 helper，不需要用户额外补口令。

- `briefing`
  - 默认允许主线程自行完成
  - 只有在需要低成本梳理主线、阻塞和下一步建议时，才按需复用或启动 `文档 / 主线判断 helper`
  - 目标是低 token 判断当前主线，不直接进入代码执行
- `execution`
  - 默认只在确有必要时使用 helper
  - 典型场景是：
    - 先做只读规则审计
    - 核对线程内已有注册表状态
    - 给主线程补一轮主线或流程判断

helper role 只补辅助判断，不改变阶段判断权和执行调度权；这些仍由主线程掌握。

固定要求：

- 不要把“要不要开 helper”交还给用户
- 不要因为 helper 有模板就机械启动
- 如果主线程一个人就能低成本完成判断，默认不必额外起 helper

## 默认 helper 角色

主线程只有在下面情况之一成立时，才值得启用 helper：

- 需要快速只读梳理长期文档和迭代记录
- 需要单独做规则审计，但主线程仍负责最终裁定
- 需要把只读分析和实现动作分开，降低主线程上下文噪音

如果只是单点任务、上下文清晰、主线程已足够判断，本轮默认由主线程直做，不额外启 helper。

### 文档 / 主线判断 helper

固定字段：

- `role_name = 文档 / 主线判断 helper`
- `helper_kind = mainline_judge`

适用场景：

- 回看长期主文档和迭代记录，判断当前该继续什么
- 需要快速说明当前主线、当前阻塞和下一步建议
- 需要判断更适合从哪个执行型 Agent 继续推进
- 需要做交接摘要或阶段定位

默认输入：

- [项目总览](/Users/bruce/work/Holmas/doc/长期主文档/项目总览.md)
- [主文档索引](/Users/bruce/work/Holmas/doc/长期主文档/主文档索引.md)
- [迭代记录索引](/Users/bruce/work/Holmas/doc/迭代记录/迭代记录索引.md)
- 最新迭代记录中的摘要段落
- 必要时读取少量代码入口做现状核对

默认输出：

- 当前主线
- 当前阻塞
- 下一步建议
- 建议从哪个执行型 Agent 继续

### 规则 / 流程审计 helper

固定字段：

- `role_name = 规则 / 流程审计 helper`
- `helper_kind = process_auditor`

适用场景：

- 审计协作规则、收尾规则和 subagent 复用规则
- 判断当前规则是否存在歧义、漏口或容易误判的点
- 输出规则补强建议，但不直接替代主线程改文档

默认输入：

- `doc/长期主文档/协作与执行/` 下相关规则文档
- 当前线程的实际执行情况
- 必要的迭代记录和收尾记录

默认输出：

- 当前规则缺口
- 容易误判的位置
- 最值得补的规则项
- 建议修改落点

## 线程级总注册表中的 helper 记录

helper role 与执行型真实 subagent 共用一份线程级总注册表。  
执行型记录的字段和状态语义仍以 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md) 为准；本页只补 helper 专属要求。

### helper 固定字段

每条 helper 记录至少包含：

- `role_name`
- `role_type = helper`
- `agent_id`
- `nickname`
- `status`
- `write_boundary`
  - 固定为只读
- `last_task_type`
- `reusable`
  - `yes / no`
- `context_compression_count`
  - 默认 `0`
- `created_at`
- `last_active_at`
- `closed_reason`
  - 仅 `status = closed` 时填写
- `helper_kind`
  - `mainline_judge / process_auditor`
- `source_scope`
  - 例如 `docs_only / docs+code`
- `output_contract`
  - 固定填 `主线程初步判断 / subagent 结论 / 最终整合结论`

## helper 状态机

helper role 固定使用简化状态机：

- `ready`
  - 已创建、可复用、当前未处理任务
- `in_progress`
  - 正在处理当前轮次的只读分析或审计任务
- `waiting`
  - 当前轮次已给出结果，线程内可继续复用
- `stale`
  - 仍存在，但默认不再优先复用
- `closed`
  - 已关闭，只保留历史，不再调度

### 状态流转

- 新建 helper：`ready`
- 接到任务：`ready -> in_progress`
- 正常完成且仍可复用：`in_progress -> waiting`
- 下一次同职责任务继续复用：`waiting -> in_progress`
- 出现明确压缩信号但未达阈值：状态不变，仅 `context_compression_count + 1`
- `context_compression_count >= 2`：`ready / waiting / in_progress -> stale`
- 主线程决定停用并换新：`stale -> closed`
- 只有主线程明确判断质量仍稳定，且用户允许继续复用时，才允许 `stale -> waiting`

## 复用与关闭规则

主线程每次准备使用 helper 时，固定按下面顺序判断：

1. 先查线程级总注册表里是否已有同职责 helper
2. 过滤掉 `closed`
3. 默认优先复用同时满足下面条件的实例：
   - `reusable = yes`
   - `status = ready / waiting`
   - `context_compression_count < 2`
4. 只有下面情况才允许新开：
   - 任务类型变了
   - 需要不同的只读输入范围
   - 原实例已结束且上下文不再适合复用
   - `context_compression_count >= 2`
5. 如果旧实例因为压缩两次以上被换掉：
   - 先标成 `stale`
   - 写明 `closed_reason`
   - 再转成 `closed`
   - 新实例以同一 `role_name`、新的 `agent_id` 进入总注册表

固定约束：

- 主线程只有在查过总注册表后，才允许新起同职责 helper
- 默认不因为“感觉输出变差”就直接新起
- `closed` 条目保留历史，但默认不再参与复用

## 压缩计数规则

`context_compression_count` 只在下面两种情况加 `1`：

- 主线程明确观察到“自动压缩背景信息”信号
- 该 helper 明确自报发生过上下文压缩，且主线程接受这次记录

固定不做：

- 不因“感觉输出变差”直接加 `1`
- 不因任务变复杂自动加 `1`
- 不把“长时间未使用”当成压缩

如果只是怀疑上下文退化，但没有明确压缩信号：

- 可以把 `reusable` 调成 `no`
- 或把状态改成 `stale`
- 但不增加 `context_compression_count`

## 主线程回传要求

helper role 的回传固定使用三段式：

- `主线程初步判断：……`
- `subagent 结论：……`
- `最终整合结论：……`

固定要求：

- 如果 helper 还没回传，要明确写 `subagent 结论：尚未回传`
- 最终对用户的结论必须区分主线程自己的判断、helper 回传内容，以及整合后的最终结论
- 主线程准备新开同职责 helper 前，应先对用户说明：
  - 当前可复用的是谁
  - 它的 `status`
  - 它的 `context_compression_count`
  - 为什么继续复用，或为什么关闭并换新
- 如果主线程判断这轮不需要 helper，也应直接说明“本轮主线程直做即可，helper 成本高于收益”

## 跨环境恢复方式

- helper 的长期主语是 `role_name`，不是旧实例昵称
- 换环境后，如果线程里没有旧实例，直接按 `role_name` 恢复同职责 helper
- 如需显式覆盖默认行为，再看 [skill 与 subagent 任务模板](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/skill%20与%20subagent%20任务模板.md) 中的 helper 模板
- helper 只是线程内辅助判断机制，不改变官方执行型 Agent 分工

## 与其他长期文档的关系

- 新会话默认入口继续看 [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md)
- 执行型真实 subagent 的启动、调度、职责、验收和 Agent 6 闭环继续看 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
- helper 的覆盖模板继续看 [skill 与 subagent 任务模板](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/skill%20与%20subagent%20任务模板.md)
- 文档收尾与 Git 提交建议继续看 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)
