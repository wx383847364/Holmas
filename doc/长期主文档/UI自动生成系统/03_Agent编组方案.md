# Agent 编组方案

## 编组目标

这套系统按“系统孵化职责”拆 subagent，而不是按 Holmas gameplay 功能拆。  
流水线阶段仍然存在，但执行组织固定为 `6 个 subagent + 2 个 skill`。

## Skill 组合

- `Subagent 1`：必带 `ui-prefab-governance`
- `Subagent 2`：必带 `ui-prefab-pipeline`
- `Subagent 3`：必带 `ui-prefab-pipeline`
- `Subagent 4`：必带 `ui-prefab-governance + ui-prefab-pipeline`
  - 只有触碰 Holmas 接入代码时才叠加 `unity-hotupdate-boundary`
- `Subagent 5`：必带 `ui-prefab-pipeline`
- `Subagent 6`：必带 `ui-prefab-governance`
  - 按审查对象叠加 `ui-prefab-pipeline`

## 角色

### 1. Subagent 1 / Foundation-Contracts

职责：

- 冻结专区正文位置
- 冻结 `asmdef` 分层和依赖方向
- 冻结 `DesignPacket / UiPrefabSpec / PrefabBindingManifest / ProjectUiProfile` 的不可变契约
- 冻结 Holmas adapter 接口边界，但不实现 Holmas 细节

### 2. Subagent 2 / Design-Intake-Spec

职责：

- 把设计图 + 标注包解释成 `UiPrefabSpec`
- 输出人工审阅稿
- 标记不确定项与人工补位项
- 冻结输入校验口径

### 3. Subagent 3 / Generator-Manifest

职责：

- 根据批准后的 spec 生成 prefab 草稿
- 生成 `PrefabBindingManifest`
- 冻结组件白名单、节点命名和资源槽位规则
- 保证输出稳定，不随机漂移

### 4. Subagent 4 / Holmas-Adapter-Profile

职责：

- 只实现 Holmas 侧 `ProjectUiProfile`
- 定义 Holmas 输出目录约定
- 定义 Holmas 对 manifest 的消费约束
- 不反向污染生成器核心

### 5. Subagent 5 / Validation-Regression

职责：

- 跑 spec 校验
- 跑 prefab / manifest 结果校验
- 跑 deterministic 回归
- 产出 `validation baseline`

### 6. Subagent 6 / Review-Acceptance

职责：

- 独立审 schema、spec、prefab 草稿和回归覆盖
- 给阶段里程碑出 `通过 / 通过，但有非阻塞建议 / 未通过，退回修复`
- 审查历史只落迭代记录，不回写长期执行稿

## 启动顺序

1. 先启动 `Subagent 1 / Foundation-Contracts`
2. 契约冻结后，并行启动：
   - `Subagent 2 / Design-Intake-Spec`
   - `Subagent 3 / Generator-Manifest`
   - `Subagent 4 / Holmas-Adapter-Profile`
3. `Subagent 3` 至少交付 1 份 `sample manifest`
4. 至少有 1 份 approved sample spec 和 1 份 sample manifest 后，`Subagent 5 / Validation-Regression` 全面介入
5. 阶段性交付后启动 `Subagent 6 / Review-Acceptance`

## 与 Holmas 现有 Agent 的关系

- 这套编组不替代 Holmas 现有 gameplay Agent
- Holmas `Agent 4` 只在需要接业务 UI 流程时消费生成结果
- Holmas `Agent 2 / Agent 3` 不负责 UI 生成系统本体
