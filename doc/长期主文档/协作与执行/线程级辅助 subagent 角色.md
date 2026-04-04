# 线程级辅助 subagent 角色

## Summary

这份文档定义 Holmas 项目中的线程级辅助 subagent 角色。  
它们不是新的官方 `Agent 1~6` 编号，而是主线程为了稳定做文档梳理、主线判断、规则审计而维护的常驻 helper role。

固定目标：

- 把“是否可复用、何时换新、为什么关闭”从主线程的临时判断，变成可追踪的线程内事实
- 保持 helper role 只做只读判断和审计，不污染执行型 Agent 分工
- 让换环境后也能按职责模板和启动口令，重新拉起相同功能的 helper

固定原则：

- helper role 默认常驻，但不等于每轮都要新起实例
- 实例昵称如 `Bohr`、`Poincare` 只用于线程内识别，不是长期规则主语
- 长期文档以 `role_name`、职责模板和启动口令为准，不依赖旧实例名
- helper role 不写入迭代记录默认分工状态，不占用新的官方 Agent 编号

## 与官方 Agent 的边界

- `Agent 1~6` 和长期 `9-agent` 编组继续只表示执行型职责
- helper role 负责：
  - 读取长期主文档、迭代记录和必要代码入口
  - 输出主线判断、阻塞判断、下一步建议
  - 审计协作规则、收尾规则和流程漏洞
- helper role 不负责：
  - 主写业务实现
  - 代替 `Agent 6` 做代码审查结论
  - 代替执行型 Agent 完成修复闭环

## 默认常驻角色

当前第一版固定为两类 helper role：

### 1. 文档 / 主线判断 helper

固定 `role_name`：

- `文档 / 主线判断 helper`

固定 `helper_kind`：

- `mainline_judge`

适用场景：

- 回看长期主文档和迭代记录，判断现在该继续什么
- 需要快速说明当前主线、当前阻塞、下一步建议
- 需要判断更适合从哪个执行型 Agent 继续推进
- 需要做交接摘要或阶段定位

固定边界：

- 只读
- 不改 repo 文件
- 不主导业务实现

固定输入：

- [项目总览](/Users/bruce/work/Holmas/doc/长期主文档/项目总览.md)
- [主文档索引](/Users/bruce/work/Holmas/doc/长期主文档/主文档索引.md)
- 最新迭代记录和迭代记录索引
- 必要时读取少量代码入口做现状核对

固定输出：

- 当前主线
- 当前阻塞
- 下一步建议
- 建议从哪个执行型 Agent 继续

### 2. 规则 / 流程审计 helper

固定 `role_name`：

- `规则 / 流程审计 helper`

固定 `helper_kind`：

- `process_auditor`

适用场景：

- 审计收尾规则、文档维护规则、subagent 复用规则
- 判断当前规则是否存在歧义或防漏缺口
- 提出规则补强建议，但不直接修改文档

固定边界：

- 只读
- 不改 repo 文件
- 不替代 `Agent 6` 的代码 review 职责

固定输入：

- `doc/长期主文档/协作与执行/` 下相关规则文档
- 当前线程的实际执行情况
- 必要的迭代记录和收尾记录

固定输出：

- 当前规则缺口
- 容易误判的位置
- 最值得补的规则项
- 建议修改落点

## 线程级总注册表

helper role 与执行型真实 subagent 共用一份线程级总注册表。  
这份注册表是线程内运行规则，不是 repo 持久化数据结构，也不是项目存档模型。

### 通用字段

每条记录固定包含：

- `role_name`
- `role_type`
  - `execution` / `helper`
- `agent_id`
- `nickname`
- `status`
- `write_boundary`
- `last_task_type`
- `reusable`
  - `yes` / `no`
- `context_compression_count`
  - 默认 `0`
- `created_at`
- `last_active_at`
- `closed_reason`
  - 仅 `status = closed` 时填写

### execution 专属字段

仅执行型角色填写：

- `is_real_subagent`
- `is_original_implementer`
- `review_chain_id`

### helper 专属字段

仅 helper role 填写：

- `helper_kind`
  - `mainline_judge` / `process_auditor`
- `source_scope`
  - 例如 `docs_only` / `docs+code`
- `output_contract`
  - 固定填 `主线程初步判断 / subagent 结论 / 最终整合结论`

## 状态机

### execution 状态

执行型真实 subagent 继续沿用官方长期规则，推荐状态为：

- `not_started`
- `in_progress`
- `in_review`
- `review_deferred`
- `returned_for_fix`
- `fixing`
- `pending_re_review`
- `passed`
- `closed`

说明：

- `closed` 是总注册表中的补充状态，用来表示旧实例不再复用，但关闭历史仍保留
- `review_deferred` 表示审查已发起，但当前不再同步等待结果；对外文案固定写成 `待回补复审`
- 如果原 reviewer 超时后由新 reviewer 接手同一 `review_chain_id`，只允许更新 reviewer 实例信息，不允许把 execution 状态误重置成新链
- 不改变既有 `Agent 6` 审查闭环语义

### helper 状态

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

### helper 状态流转

- 新建 helper：`ready`
- 接到任务：`ready -> in_progress`
- 正常完成且仍可复用：`in_progress -> waiting`
- 下一次同职责任务继续复用：`waiting -> in_progress`
- 出现明确压缩信号但未达阈值：状态不变，仅 `context_compression_count + 1`
- `context_compression_count >= 2`：`ready / waiting / in_progress -> stale`
- 主线程决定停用并换新：`stale -> closed`
- 只有在主线程明确判断质量仍稳定，且用户允许继续复用时，才允许 `stale -> waiting`
  - 这是例外，不是默认路径

## 调度与复用规则

主线程每次准备使用 subagent 时，固定按下面顺序判断：

1. 先查线程级总注册表里是否已有同职责记录
2. 过滤掉 `closed`
3. 默认优先选择同时满足下面条件的实例：
   - `reusable = yes`
   - `status` 属于 `ready` 或 `waiting`
   - `context_compression_count < 2`
4. 只有下面情况才允许新开：
   - 任务类型变了
   - 需要不同写入边界
   - 原实例已结束且上下文不再适合复用
   - `context_compression_count >= 2`
5. 如果旧实例因为压缩两次以上被换掉：
   - 先标成 `stale`
   - 写明 `closed_reason`
   - 再转成 `closed`
   - 新实例以同一 `role_name`、新的 `agent_id` 进入总注册表

固定约束：

- 主线程只有在查过总注册表后，才允许新起同职责 helper
- 默认不因为“感觉输出变差”就直接新起；要么有明确压缩信号，要么明确把旧实例标记为 `stale`
- `closed` 条目保留历史，但默认不再参与复用

## 压缩计数规则

`context_compression_count` 只在下面两种情况加 `1`：

- 主线程明确观察到“自动压缩背景信息”信号
- 该 agent 明确自报发生过上下文压缩，且主线程接受这次记录

固定不做：

- 不因“感觉输出变差”直接加 `1`
- 不因任务变复杂自动加 `1`
- 不把“长时间未使用”当成压缩

如果只是怀疑上下文退化，但没有明确压缩信号：

- 可以把 `reusable` 调成 `no`
- 或把状态改成 `stale`
- 但不增加 `context_compression_count`

## 固定回传格式

helper role 的回传固定使用三段式：

- `主线程初步判断：……`
- `subagent 结论：……`
- `最终整合结论：……`

固定要求：

- 如果 subagent 还没回传，要明确写 `subagent 结论：尚未回传`
- 最终对用户的结论必须区分主线程自己的判断、subagent 回传内容，以及整合后的最终结论
- 主线程准备新开同职责 helper 前，应先对用户说明：
  - 当前可复用的是谁
  - 它的 `status`
  - 它的 `context_compression_count`
  - 为什么继续复用，或为什么关闭并换新

## 跨环境恢复方式

换环境后，如果线程里没有旧实例，不需要依赖 `Bohr`、`Poincare` 这类旧名字。  
固定按职责模板和启动口令重新拉起同功能 helper。

### 文档 / 主线判断 helper 启动模板

```text
按长期主文档规则执行。
启动一个 文档 / 主线判断 helper。
目标：只读查看长期主文档、迭代记录和必要代码入口，输出当前主线、当前阻塞、下一步建议，以及建议从哪个执行型 Agent 继续。
约束：不要改文件；输出固定使用 主线程初步判断 / subagent 结论 / 最终整合结论。
```

### 规则 / 流程审计 helper 启动模板

```text
按长期主文档规则执行。
启动一个 规则 / 流程审计 helper。
目标：只读审计协作规则、收尾规则、文档维护规则和 subagent 复用规则，输出规则缺口、误判点和最值得补的规则项。
约束：不要改文件；输出固定使用 主线程初步判断 / subagent 结论 / 最终整合结论。
```

## 与其他长期文档的关系

- 执行型真实 subagent 继续优先遵循 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
- 新会话默认入口继续看 [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md)
- 文档收尾与 Git 提交建议继续看 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

helper role 只是补充线程内的辅助判断和复用机制，不改变官方执行型 Agent 分工，不替代现有文档维护机制。
