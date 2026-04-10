# Codex 新会话必读

这页是 Holmas 新会话的默认入口页。

目标只有一个：让新会话最快知道项目现在在做什么、接下来优先干什么，以及开始任务和结束任务时必须遵守什么规则。

这页不负责：

- 完整介绍全项目
- 展开 `subagent` 的全部模板和细节
- 展开收尾、Git、文档维护的完整 SOP

如果当前任务已经明确，先读完这页，再按任务类型跳到对应文档和代码；不要先扫全仓库。

## 这页怎么用

- 先看这页，再决定是否需要继续看 [项目总览](/Users/bruce/work/Holmas/doc/长期主文档/项目总览.md) 和 [迭代记录索引](/Users/bruce/work/Holmas/doc/迭代记录/迭代记录索引.md)
- 默认不要先扫全项目代码
- 默认不要先通读全量长期主文档
- 默认不要先通读全部迭代记录
- 默认不要在任务未明确前先看实现
- 默认不要机械开 `subagent`

项目 30 秒速览：

- 项目名称：Holmas / 福尔猫斯-寻猫侦探社
- 核心玩法：异形扫雷棋盘上的找猫、任务推进和侦探社成长
- 正式业务分层：`App.AOT / App.Shared / App.HotUpdate`
- 当前高频工作区：Holmas UI runtime、UI prefab generator、配置导表与验证链
- 编辑器判断规则：优先按 `ProjectSettings/ProjectVersion.txt` 找团结编辑器，不要先猜官方 Unity

默认策略：

- 先看入口页和当前主线
- 再按任务分流去看相关文档
- 最后才看对应代码

## 最小阅读路径

新会话默认只读下面几份：

1. 本页
2. [项目总览](/Users/bruce/work/Holmas/doc/长期主文档/项目总览.md)
3. [迭代记录索引](/Users/bruce/work/Holmas/doc/迭代记录/迭代记录索引.md)
4. 最新迭代记录里只看：
   - `当前状态`
   - `关键结论`
   - `风险与阻塞`
   - `下一步`

默认不需要：

- 通读完整迭代记录
- 通读整个 `doc/长期主文档`
- 在任务未明确前通读实现代码

## 当前主线与下一步

当前主线：

- Holmas UI runtime 框架继续收口，重点补正式行为和页面样例
- UI prefab generator 正式链路继续收口，重点稳住 trial adapter、样例和运行时接缝
- 验证链与 batchmode/tooling 稳定化，减少环境噪音对回归的影响

当前阻塞：

- Holmas 总验证仍有 `1` 个既有 EditMode 失败项：`auto-analysis bridge` 组合执行失败
- 团结 batchmode 需要在干净环境下运行，不能直接复用当前终端环境
- 微信真实 `JSBridge` 尚未接入，当前安全区和窗口信息仍有 fallback 路径

推荐下一步：

- 优先修 Holmas 总验证中剩余的 `auto-analysis bridge` 失败，恢复总验证全绿
- 继续补 Holmas UI 的 `popup / sheet / overlay` 样例与行为验证
- 继续收口 generated runtime descriptor 与运行时 binding 正式链

当前建议起点：

- 如果没有更明确的新任务，默认先从 `UI 与验证` 方向继续推进

## 按任务类型跳转

如果这轮任务已经明确，按下面路径继续看：

- UI runtime：
  - 文档先看 [15_Holmas业务侧UI框架最小落地方案.md](/Users/bruce/work/Holmas/doc/长期主文档/UI自动生成系统/15_Holmas业务侧UI框架最小落地方案.md)
  - 代码再看 `Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI`
- UI prefab generator：
  - 先看 [UI 自动生成系统总览](/Users/bruce/work/Holmas/doc/长期主文档/UI自动生成系统/00_总览.md)
  - 代码再看 `Assets/Tools/UiPrefabGenerator`
- 配置 / 表：
  - 先看 `Assets/Config/*.xlsx`
  - 再看 `tools/config_export` 和 `tools/validation`
- 架构边界：
  - 先看 [热更新边界规范_v1.md](/Users/bruce/work/Holmas/doc/长期主文档/架构与边界/热更新边界规范_v1.md)
- 协作 / 执行：
  - 先看 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
  - 收尾时看 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)
  - 如果本轮要按确认词直接提交或推送，再看 [Git 提交建议与确认规则](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Git%20提交建议与确认规则.md)

## 执行硬规则

### 开始执行前

- 只要进入任何实际任务执行，都必须先做一次 `subagent` 判断
- 主线程必须先判断这轮采用：
  - `主线程直做`
  - `主线程 + helper`
  - `主线程 + 真实 subagent`
- 这个判断默认由主线程自己完成，不要把决定丢回给用户
- 如果启用真实 `subagent`，再按 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md) 的细则执行

本页不展开：

- 情况 A / B
- 详细判断树
- 详细模板
- 详细 dispatch 格式

### 任务结束后

- 只要一轮任务结束，就必须进入 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md) 的统一收尾规范
- 如果这轮要沿提交确认词继续执行 `git commit / push`，具体提示词和执行口径统一看 [Git 提交建议与确认规则](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Git%20提交建议与确认规则.md)
- 收尾时必须处理：
  - 文档维护
  - 验证结论
  - Git 提交建议
  - 会话交接建议
- 默认收尾入口是：

```bash
bash tools/doc_maintenance/finalize_task.sh
```

- 收尾默认会自动尝试清理 `/tmp` 或 `/private/tmp` 下的 Holmas 临时验证工程；如果这轮需要保留现场排查，显式追加 `--skip-temp-cleanup`
- 不要把“代码写完”“文档写完”“测试跑完”当成任务结束
- 不要把“是否 git commit”写死为固定结论；所有任务都必须进入收尾规范，再由收尾规范判断是否适合提交、是否需要补文档和同步记录

本页不展开：

- 收尾脚本参数
- Git 提交建议与确认规则细则
- 文档维护命令细节
- 最终回复模板
