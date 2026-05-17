# Holmas 八位提交编号方案

## Summary

我建议把提交编号升级成 **8 位**，格式固定为：

```text
[TMMSSSSS]
```

含义固定为：

- `T`：一级大类
- `MM`：二级模块
- `SSSSS`：该模块内递增流水号

例如：

- `[13000012]`：代码类，`App.HotUpdate / Holmas gameplay` 模块，第 12 次提交
- `[25000007]`：文档类，`策划配表文档` 模块，第 7 次提交
- `[32000003]`：资源类，`HotUpdate Res` 模块，第 3 次提交

这样做比 6 位更合理，因为：

- 模块前缀仍然短，前 3 位就能定位模块
- 每个模块有 **99999** 个流水号，足够长期使用
- 可以通过编号前缀直接查模块历史，不需要每次再靠标题猜

## Key Changes

### 1. 一级大类保持 6 类最合理

我建议 Holmas 先固定 **6 个一级大类**，已经够覆盖当前仓库，不需要再拆更多一级类。

```text
1xxxxxxx 代码
2xxxxxxx 文档
3xxxxxxx 资源 / 美术 / Unity 资产
4xxxxxxx 配置 / 数据
5xxxxxxx 测试 / 验证
6xxxxxxx 工具 / 生成器 / 项目脚本
```

原因：

- 仓库现状天然就是这 6 大块
- 再少会太粗
- 再多会增加记忆成本，反而不利于你长期手工判断

### 2. 文档类要单独补上“策划方案”和“策划配表文档”

你特别提到的这块，我建议在 `2xxxxxxx` 下明确拆开，不要混进普通文档。

```text
2xxxxxxx 文档

210xxxxx 项目总览 / 主文档索引 / 入口页
220xxxxx 架构与边界文档
230xxxxx 协作与执行 / agent / skill / 收尾流程
240xxxxx 策划方案文档
250xxxxx 策划配表文档 / 数据方案文档
260xxxxx 迭代记录 / 启动卡 / 交接
270xxxxx UI 自动生成系统专区文档
```

这样区分最符合你现在的仓库结构：

- `240xxxxx`
  - 放玩法方案、系统落地方案、长期方案
- `250xxxxx`
  - 放配表规则、表结构设计、数据口径文档

按现在仓库内容举例：

- [Holmas_v1方案](Holmas_v1方案.md)
  - 更适合 `240xxxxx`
- [Holmas v1 城市宣传成长主线落地方案](Holmas%20v1%20城市宣传成长主线落地方案.md)
  - 更适合 `240xxxxx`
- [Holmas v1 正式建筑内容表方案](Holmas%20v1%20正式建筑内容表方案.md)
  - 更适合 `250xxxxx`
- [Holmas v1 长期成长表方案](Holmas%20v1%20长期成长表方案.md)
  - 更适合 `250xxxxx`

### 3. 完整推荐编号表

这是我建议直接冻结的一版：

```text
1xxxxxxx 代码
110xxxxx App.Shared / Contracts
120xxxxx App.AOT / Bootstrap / Infrastructure
130xxxxx App.HotUpdate / Holmas gameplay
140xxxxx Minesweeper 接入
150xxxxx 运行时 UI 代码 / Presenter / Controller

2xxxxxxx 文档
210xxxxx 项目总览 / 主文档索引 / 入口页
220xxxxx 架构与边界
230xxxxx 协作与执行 / agent / skill / 收尾流程
240xxxxx 策划方案文档
250xxxxx 策划配表文档 / 数据方案文档
260xxxxx 迭代记录 / 启动卡 / 交接
270xxxxx UI 自动生成系统专区文档

3xxxxxxx 资源 / 美术 / Unity 资产
310xxxxx 图标 / 贴图 / 美术资源
320xxxxx HotUpdate Res
330xxxxx Scene / Prefab / Unity 资产
340xxxxx 地图 / Terrain / 关卡资源

4xxxxxxx 配置 / 数据
410xxxxx 原始配置表 / xlsx
420xxxxx 配置导出 / 转换脚本
430xxxxx json / bytes / catalog 产物
440xxxxx 配置协议 / schema / 数据结构

5xxxxxxx 测试 / 验证
510xxxxx 单元 / 集成测试
520xxxxx smoke / validation / 回归
530xxxxx 边界检查 / QA 脚本

6xxxxxxx 工具 / 生成器 / 项目脚本
610xxxxx 通用项目脚本
620xxxxx UiPrefabGenerator
630xxxxx Editor 工具 / 导入器 / 生成辅助
```

### 4. 日常使用规则

## 提交编号速查表

### 完整速查表

```text
1xxxxxxx 代码
110xxxxx App.Shared / Contracts
120xxxxx App.AOT / Bootstrap / Infrastructure
130xxxxx App.HotUpdate / Holmas gameplay
140xxxxx Minesweeper 接入
150xxxxx 运行时 UI / Presenter / Controller

2xxxxxxx 文档
210xxxxx 项目总览 / 主文档索引 / 入口页
220xxxxx 架构与边界
230xxxxx 协作与执行 / agent / skill / 收尾流程
240xxxxx 策划方案文档
250xxxxx 策划配表文档 / 数据方案文档
260xxxxx 迭代记录 / 启动卡 / 交接
270xxxxx UI 自动生成系统专区文档

3xxxxxxx 资源 / 美术 / Unity 资产
310xxxxx 图标 / 贴图 / 美术资源
320xxxxx HotUpdate Res
330xxxxx Scene / Prefab / Unity 资产
340xxxxx 地图 / Terrain / 关卡资源

4xxxxxxx 配置 / 数据
410xxxxx 原始配置表 / xlsx
420xxxxx 配置导出 / 转换脚本
430xxxxx json / bytes / catalog 产物
440xxxxx 配置协议 / schema / 数据结构

5xxxxxxx 测试 / 验证
510xxxxx 单元 / 集成测试
520xxxxx smoke / validation / 回归
530xxxxx 边界检查 / QA 脚本

6xxxxxxx 工具 / 生成器 / 项目脚本
610xxxxx 通用项目脚本
620xxxxx UiPrefabGenerator
630xxxxx Editor 工具 / 导入器 / 生成辅助
```

### 最常用一屏版

```text
130 Holmas gameplay 主代码
230 协作流程 / 收尾 / agent 规则
240 策划方案
250 配表 / 数据方案
260 迭代记录 / 交接
270 UI 自动生成系统文档
320 HotUpdate Res
330 Prefab / Unity 资产 / meta
410 xlsx / 原始配置
420 配置导出脚本
520 测试 / 回归 / validation
620 UiPrefabGenerator
```

### 当前已使用示例

```text
[62000001] 架构：补齐 UI prefab intake 与 manifest 主链
[62000002] UI：补齐 Holmas adapter 与 sample 输出样例
[52000001] 测试：补齐 UI prefab validation 与 sample-driven regression
[27000001] 文档：同步 UI 自动生成系统规格与迭代记录
[23000001] 流程：提交编号与收尾规则
[23000002] 流程：重写线程级辅助 subagent 角色规则
[23000003] 流程：迁移旧 PLAN 并新增八位提交编号方案
[33000001] 资源：补齐 Holmas UI 与测试文件 meta
```

使用建议：

- 在 GitHub Desktop 里先看前 3 位模块号，再看中文前缀
- 在 `git log` 里优先按模块前缀检索，例如 `620`、`230`、`330`
- 新提交先判断模块，再在该模块下递增 `SSSSS`

后续提交标题统一变成：

```text
[23000001] 流程：收紧半收尾失败态规则入口
[25000001] 文档：补齐正式建筑内容表方案
[32000001] 玩法：补齐 Res 目录资源 meta 文件
[41000001] 配置：更新 Holmas_TaskTable.xlsx
[62000001] UI：补 sample manifest 输出校验
```

使用方式固定为：

- 看前 1 位：知道大类
- 看前 3 位：知道模块
- 看完整 8 位：知道这个模块内是第几次提交

检索时你以后可以直接按模块前缀找：

- `250`：策划配表文档
- `320`：HotUpdate `Res`
- `620`：`UiPrefabGenerator`
- `130`：Holmas gameplay 主代码

## Test Plan

验收标准建议固定为：

- 同一模块的提交号能通过前 3 位聚合检索
- 不同模块不会共用同一前缀
- 文档中的“策划方案”和“策划配表”能明确区分
- 资源类和配置类不会混在一个模块里
- 以后新提交只需要先判断模块，再在该模块下递增，不需要重新想一套编号规则

## Assumptions

- 你最看重的是“按模块查历史”，不是只区分代码/文档/美术
- 旧提交不回填，新的八位编号从启用那一刻开始使用
- `SSSSS` 采用模块内递增，不做全仓库全局递增
- 标题里的 `文档 / 流程 / 玩法 / 配置 / UI / 架构` 这些中文前缀继续保留，只是变成数字编号的第二层说明
- 当前仓库最合理的是 6 个一级类，文档类里额外细分出策划方案和策划配表文档
