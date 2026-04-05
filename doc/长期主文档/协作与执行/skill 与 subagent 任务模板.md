# skill 与 subagent 任务模板

这页只负责三件事：

- 选 skill
- 组合 skill
- 给执行型 agent、测试 agent、审查 agent 提供可复用任务模板

这页不重复解释：

- 新会话入口和 `briefing -> execution dispatch` 两阶段切换
- `Agent 1 ~ Agent 6` 的职责边界、允许写入范围和验收规则
- 固定三段收尾、`finalize_task.sh` 和 Git 提交流程

详细规则分别看：

- [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md)
- [Agent 启动口令清单](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动口令清单.md)
- [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
- [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

## 当前核心 skill

- `unity-hotupdate-boundary`
  - 正式功能开发默认先带这份 skill
  - 负责 `App.AOT / App.Shared / App.HotUpdate` 边界、组合层、热更新职责约束
- `findcat-config-pipeline`
  - 涉及地图表、猫表、任务表、奖励公式、权重、任务填充、地图生成时叠加
  - 负责配置输入、模板数据和运行时状态分离
- `unity-ugui-flow-integration`
  - 涉及 UGUI、Prefab、Presenter、Controller、页面流转、UI 冒烟时叠加
  - 负责 UI 表现层边界和流程接线约束
- `ui-prefab-governance`
  - 涉及 `UI自动生成系统` 专区、执行派工单、旧稿跳转页、asmdef 分层、目录隔离与 Holmas 试点接入时使用
  - 负责专区权威入口、subagent 派工格式、迁移边界和目录所有权约束
- `ui-prefab-pipeline`
  - 涉及 `DesignPacket / UiPrefabSpec / PrefabBindingManifest / validation` 时使用
  - 负责 spec 权威层、生成流程、manifest 结构、golden case 与 deterministic 回归约束

## 选 skill 的固定顺序

1. 先按 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md) 判断这轮由哪个 `Agent` 职责承接。
2. 如果任务属于 `doc/长期主文档/UI自动生成系统` 或 `Assets/Tools/UiPrefabGenerator`，优先切到 UI 自动生成系统专项：
   - 规划、派工、隔离、跳转页、asmdef 分层先带 `ui-prefab-governance`
   - spec、生成、manifest、校验、回归先带 `ui-prefab-pipeline`
   - 只有触碰 Holmas 接入代码时，才额外叠加 `unity-hotupdate-boundary`
3. 如果不是 UI 自动生成系统专项，只要属于正式功能开发、测试或审查，默认先带 `unity-hotupdate-boundary`。
4. 涉及配置、权重、任务生成、地图生成、奖励公式时，再叠加 `findcat-config-pipeline`。
5. 涉及 UGUI、Prefab、Presenter、页面流转和 UI 联调时，再叠加 `unity-ugui-flow-integration`。
6. 测试或审查线默认镜像被测对象的 skill 组合，而不是临时自定义一套新边界。

## 常用组合

- `UI 自动生成系统 / 规划与派工`
  - `ui-prefab-governance`
- `UI 自动生成系统 / spec / 生成 / 回归`
  - `ui-prefab-pipeline`
- `UI 自动生成系统 / Holmas 试点接入`
  - `ui-prefab-governance + ui-prefab-pipeline`
  - 只有触碰 Holmas 接入代码时再叠加 `unity-hotupdate-boundary`
- `边界与骨架`
  - `unity-hotupdate-boundary`
- `地图 / 棋盘 / 任务 / 奖励 / 配置`
  - `unity-hotupdate-boundary + findcat-config-pipeline`
- `UI / 流程 / Presenter / Prefab`
  - `unity-hotupdate-boundary + unity-ugui-flow-integration`
- `测试 / 审查`
  - 默认 `unity-hotupdate-boundary`
  - 再按被测对象叠加 `findcat-config-pipeline` 或 `unity-ugui-flow-integration`

## 模板使用规则

- 先从 [Agent 启动口令清单](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动口令清单.md) 取最短可复制口令。
- 如果任务需要更明确的 skill、约束、交付格式，再补本页模板。
- 这页的模板只补“怎么描述 skill 和任务结构”，不替代 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md) 中的边界和验收。
- 模板中的 `目标 / 约束 / 交付` 都要按当前任务裁剪，不要求每次整段照抄。

## 通用执行模板

```text
你负责本项目的……实现。请遵循 $unity-hotupdate-boundary。
如果本轮涉及配置、生成、奖励、权重或任务栏规则，请额外遵循 $findcat-config-pipeline。
如果本轮涉及 UGUI、Prefab、Presenter、Controller 或页面流转，请额外遵循 $unity-ugui-flow-integration。

目标：
1. ……
2. ……
3. ……

约束：
- 以长期主文档里的 Agent 启动与验收规范为准
- 不要越过当前 Agent 的允许写入边界
- 如需新增跨层接口或 DTO，先保持最小化

交付：
- 列出你修改的文件
- 说明输入、输出和依赖接口
- 标出未完成项、风险和阻塞
- 完成后按文档维护流程收尾
```

## 按职责划分的模板

### Agent 1：边界与骨架

适合：

- 冻结 `App.Shared`
- 搭 `App.HotUpdate` 骨架和组合层
- 处理跨层接口、入口、模块根目录

```text
你负责本项目的边界与骨架实现。请遵循 $unity-hotupdate-boundary。

目标：
1. 冻结本轮需要的最小 DTO、接口和事件
2. 建立稳定的 HotUpdate 模块骨架和组合层
3. 给后续执行线提供可依赖的入口和边界

约束：
- 以 Agent 1 的长期边界和验收规则为准
- 不要展开到地图、任务、UI 或长期业务细节

交付：
- 修改文件
- DTO / 接口冻结结果
- 其他 Agent 可依赖的入口和说明
```

### Agent 2：地图与棋盘

适合：

- `MinesweeperTerrainData`
- `BoardTemplate`
- `LevelSnapshot`
- 棋盘生成、揭示、扩散、通关判定

```text
你负责本项目的地图与棋盘实现。请遵循 $unity-hotupdate-boundary 和 $findcat-config-pipeline。

目标：
1. 接入 terrain 作为地图模板输入
2. 实现棋盘纯逻辑和运行时状态
3. 输出稳定的关卡与格子状态接口

约束：
- 以 Agent 2 的长期边界和验收规则为准
- 不要越到 Shared、UI 或任务服务

交付：
- 修改文件
- 地图输入输出说明
- UI / 任务线可消费的状态接口
```

### Agent 3：任务与长期进度

适合：

- 任务栏
- 奖励公式
- 任务推进
- 长期成长服务

```text
你负责本项目的任务与长期进度实现。请遵循 $unity-hotupdate-boundary 和 $findcat-config-pipeline。

目标：
1. 实现任务栏规则、抽取、去重和补位
2. 实现奖励计算和任务推进
3. 输出长期进度或元进度相关服务接口

约束：
- 以 Agent 3 的长期边界和验收规则为准
- 不要越到 Shared、入口或 UI 表现层

交付：
- 修改文件
- 任务与奖励接口
- 长期进度状态说明
```

### Agent 4：UI 与流程

适合：

- UGUI 页面
- Prefab
- Presenter / Controller
- 场景绑定
- 冒烟联调

```text
你负责本项目的 UI 与流程接线。请遵循 $unity-hotupdate-boundary 和 $unity-ugui-flow-integration。

目标：
1. 接入当前阶段需要的页面、交互和流程
2. 保持 UI 只做表现和交互编排
3. 输出可复现的联调或冒烟结论

约束：
- 以 Agent 4 的长期边界和验收规则为准
- 不要把核心规则、奖励公式或生成逻辑塞进 UI

交付：
- 修改文件
- 页面状态来源和动作出口
- 冒烟路径与联调说明
```

### Agent 5：测试与质量保障

适合：

- 单元测试
- 集成测试
- 验证脚本
- 回归检查

```text
你负责本项目的测试与质量保障实现。请遵循 $unity-hotupdate-boundary。
如果测试对象涉及地图、任务、配置，请额外遵循 $findcat-config-pipeline。
如果测试对象涉及 UI 流程，请额外遵循 $unity-ugui-flow-integration。

目标：
1. 为当前阶段补关键测试或验证脚本
2. 校验边界、输入输出和关键规则
3. 输出通过项、失败项、风险和回归建议

约束：
- 以 Agent 5 的长期边界和验收规则为准
- 不要把发现的问题直接改成新的业务实现

交付：
- 修改文件
- 覆盖面说明
- 失败项、风险和建议归属
```

### Agent 6：挑刺与问题审查

适合：

- 阶段里程碑审查
- 复审
- blocking / non-blocking 结论裁定

```text
你负责本项目的挑刺与问题审查。请遵循 $unity-hotupdate-boundary。
如果审查对象涉及地图、任务、配置，请额外遵循 $findcat-config-pipeline。
如果审查对象涉及 UI 流程，请额外遵循 $unity-ugui-flow-integration。

目标：
1. 独立审查当前交付是否存在 bug、回归、越界、误解需求或缺关键验证
2. 给出通过 / 通过，但有非阻塞建议 / 未通过，退回修复
3. 明确问题归属和复审条件

约束：
- 以 Agent 6 的长期边界和验收规则为准
- 不要接管实现，只负责裁定和退回

交付：
- 审查对象和范围
- 审查结论
- 问题列表、严重级别、归属和复审条件
```

## 主控集成时只保留的动作提醒

- 先按 [Codex新会话必读](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Codex新会话必读.md) 判断当前还在 `briefing` 还是已经进入 `execution dispatch`。
- 再按 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md) 决定由主线程直做、复用 helper，还是启动真实 subagent。
- 需要 helper 时，helper 的角色、注册表、复用和压缩规则统一看 [线程级辅助 subagent 角色](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/线程级辅助 subagent 角色.md)。
- 任务结束后，固定按 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md) 收尾。
