# UI Prefab 自动生成系统 Agent 规划 v1

## Summary

目标是先在 Holmas 内做一套 **可拆出的 UI 自动生成系统**，长期再抽成独立小项目。  
这一版不按你当前的游戏功能型 `Agent 1~6` 直接复用，而是改成 **工具链型 5-Agent 编组**，专门服务这条流水线：

- 输入：设计图 + 标注包 + 规则
- 中间层：结构化 `UI Spec`
- 输出：UGUI prefab 草稿 + 绑定清单
- 流程：`agent 产 spec -> 人审 -> 生成 prefab -> 自动校验`

## Agent 编组

### 1. Foundation / Schema Agent
建议沿用 `Agent 1` 的边界职责，但改成工具链方向。

职责：
- 冻结模块边界与目录结构
- 定义 `DesignPacket`、`UiPrefabSpec`、`PrefabBindingManifest`
- 定义项目级 `UiProfile`：命名、目录、默认组件、资源槽位规则
- 约束“哪些内容能自动生成，哪些必须人工确认”

允许写入：
- 工具链核心 schema
- 生成器公共接口
- 项目适配层入口

禁止写入：
- Unity prefab 生成细节
- 业务 UI 联调逻辑
- Holmas gameplay 代码

交付物：
- schema 定稿
- 目录结构
- 规则清单
- 其他 agent 可依赖的接口

skill：
- `unity-hotupdate-boundary`

### 2. Design Intake / Spec Agent
这是新角色，不建议硬套现有地图/任务 agent。

职责：
- 接收设计图、标注包、规则文本
- 转成结构化 `UiPrefabSpec`
- 输出可审阅的页面树、组件树、状态树、资源槽位和交互出口
- 标记“不确定项”和“需人工补位项”

允许写入：
- spec 解释器
- spec 草稿生成器
- 设计输入校验器

禁止写入：
- Unity prefab 正式生成逻辑
- 业务 Presenter
- gameplay 绑定实现

交付物：
- `UiPrefabSpec`
- spec 预览
- 不确定项列表

skill：
- `unity-hotupdate-boundary`
- 后续建议单独做一个 `ui-spec-authoring` skill

### 3. Unity UGUI Generator Agent
这是第一版的主实现 agent，最接近你原来 `Agent 4`，但范围更窄更稳。

职责：
- 把已批准 spec 生成为 UGUI prefab 草稿
- 创建节点层级、组件、布局、占位资源位
- 生成绑定清单，不直接写业务逻辑
- 保证重复生成稳定，不随机漂移

允许写入：
- Unity Editor 生成器
- prefab 草稿目录
- manifest / binding 输出

禁止写入：
- schema 定义
- gameplay 逻辑
- `App.Shared`
- HotUpdate 业务入口

交付物：
- prefab 草稿
- 绑定清单
- 生成日志

skill：
- `unity-hotupdate-boundary`
- `unity-ugui-flow-integration`

### 4. Validation / Regression Agent
对应你原体系里的 `Agent 5`。

职责：
- 校验 spec 完整性
- 校验 prefab 生成结果
- 校验命名、目录、组件白名单、资源槽位
- 做 deterministic 回归测试
- 比较同一 spec 多次生成是否一致

允许写入：
- 测试代码
- 校验脚本
- golden 回归用例

禁止写入：
- schema 主定义
- prefab 正式实现
- gameplay 业务代码

交付物：
- 校验报告
- 回归报告
- 失败项与风险清单

skill：
- `unity-hotupdate-boundary`
- `unity-ugui-flow-integration`

### 5. Review / Acceptance Agent
对应你原体系里的 `Agent 6`。

职责：
- 审 schema 是否稳定
- 审 spec 是否可生成
- 审 prefab 草稿是否满足规则
- 审校验覆盖是否足够
- 给出 `通过 / 通过但有建议 / 未通过` 结论

允许写入：
- review 结论
- 问题清单
- 验收意见

禁止写入：
- 直接改 schema
- 直接改生成器
- 直接改 prefab

交付物：
- 阶段审查结论
- findings 归属
- 复审条件

skill：
- `unity-hotupdate-boundary`
- 审 UGUI 时叠加 `unity-ugui-flow-integration`

## 启动顺序

固定这样开：

1. 先启动 `Foundation / Schema Agent`
先冻结 schema、目录、命名、输入输出边界。

2. Foundation 冻结后，并行启动：
- `Design Intake / Spec Agent`
- `Unity UGUI Generator Agent`

3. 两边第一轮打通后，再启动：
- `Validation / Regression Agent`

4. 里程碑交付后，再启动：
- `Review / Acceptance Agent`

不建议一开始就开：
- 现有 `Agent 2`
- 现有 `Agent 3`

因为这两个角色在你当前项目里偏地图/任务/长期进度，会把工具链项目带偏成 Holmas 功能联调。

## 第一阶段里程碑

### M1. 基础契约冻结
产出：
- `DesignPacket`
- `UiPrefabSpec`
- `PrefabBindingManifest`
- 项目级 `UiProfile`

### M2. 单页面跑通
选一个最小页面做试点：
- 一个主面板
- 1 个列表区
- 2~3 个按钮
- 资源槽位
- 状态位

产出：
- spec 草稿
- prefab 草稿
- binding 清单

### M3. 回归稳定
要求：
- 同一 spec 重复生成稳定
- 改一个节点只影响局部
- validator 能拦住非法输入

### M4. Holmas 接入试点
把生成出来的 prefab 草稿接进 Holmas 的正式 UI 工作流，但仍保持：
- prefab 生成是工具链职责
- 业务 Presenter/交互接线是项目 UI 实现职责

## 和你之前规划的区别

你之前的规划本质上是 **游戏研发型 6-Agent**：
- Agent 1：边界
- Agent 2：地图
- Agent 3：任务/长期进度
- Agent 4：UI 联调
- Agent 5：测试
- Agent 6：审查

它适合“做 Holmas 这个游戏”。

这次我给你的规划是 **工具链型 5-Agent**：
- Foundation / Schema
- Design Intake / Spec
- UGUI Generator
- Validation / Regression
- Review / Acceptance

它适合“做 UI 自动生成平台”。

核心区别有 3 个：
- 从“业务功能拆分”改成“流水线阶段拆分”
- 从“直接做 UI 页面”改成“先产 spec，再生成 prefab”
- 从“服务 Holmas 一次开发”改成“服务长期可复用工具链”

## 优势

### 1. 更容易拆项目
因为边界天然是：
- 输入层
- spec 层
- 生成层
- 校验层

以后从 Holmas 拆出去时，不需要把地图/任务/业务代码一起搬走。

### 2. 更适合长期迭代
你后面要优化的重点一定是：
- 图片理解准确率
- spec 稳定性
- 生成质量
- 回归一致性

这套 agent 编排正好对应这 4 个演进点。

### 3. 更不容易把系统做歪
如果沿用旧 `Agent 2/3/4` 思路，团队很容易一路变成：
“为了 Holmas 当前页面方便，直接把业务逻辑、UI 接线、生成脚本混在一起。”

这套新编排会强制把：
- 设计理解
- 结构表达
- prefab 生成
- 业务接线
拆开。

### 4. 更适合人审后生成
你已经明确希望先人审再落地，这种流程天然要求中间有一层稳定 spec。  
旧规划里 `Agent 4` 更偏“直接联调 UI”，中间审阅面不够清晰；新规划里 `Spec Agent` 和 `Generator Agent` 分开后，人工确认点就非常明确。

## Assumptions

- 第一版只支持 Unity UGUI
- 第一版只生成 prefab 草稿和绑定清单
- 设计侧未来会提供“设计图 + 标注包”，不是只有效果图
- Holmas 是首个试点项目，但不是最终边界
- 业务 Presenter/玩法接线不纳入第一版自动生成范围
