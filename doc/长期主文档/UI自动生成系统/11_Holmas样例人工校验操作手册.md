# Holmas 样例人工校验操作手册

这份文档给“我要亲自走一遍 UI 自动生成流程”的场景使用。  
目标不是解释系统愿景，而是给出一条当前仓库里可直接复跑的 Holmas sample 实操路径。

## 先记住这条推荐路线

推荐按下面顺序执行：

1. 先看 sample 输入和 golden 基线，知道这次应该生成什么
2. 先跑 layout 检查，确认专区目录、asmdef 和 sample 文件没有断
3. 再跑 `UiPrefabGenerator` 的 EditMode 测试，确认核心生成/校验逻辑没挂
4. 再跑 Holmas sample pipeline，拿到本次 report
5. 最后打开保留下来的临时工程，亲自检查生成 prefab 和 report 内容

这样做的原因是：

- layout 检查负责确认“系统骨架还在不在”
- EditMode 测试负责确认“生成器逻辑和 golden fixtures 还对不对”
- sample pipeline 负责确认“从 DesignPacket 到 prefab 草稿、manifest、adapter plan 的整条链路还能不能走通”
- 人工打开 prefab 负责确认“机器通过了，但结果是不是仍符合你的直觉”

## 你要看的输入和期望输出

本次样例输入和基线固定在 `Assets/Tools/UiPrefabGenerator/Samples~/Holmas`：

- `sample_design_packet.json`
- `sample_design_packet_intake_assessment.json`
- `sample_ui_prefab_spec.json`
- `sample_prefab_binding_manifest.json`
- `sample_holmas_generated_result_plan.json`
- `validation_baseline.json`

第一次手动走流程时，建议先快速读这 4 份核心文件：

1. `sample_design_packet.json`
2. `sample_ui_prefab_spec.json`
3. `sample_prefab_binding_manifest.json`
4. `sample_holmas_generated_result_plan.json`

你应该先在脑子里建立这几个预期：

- 页面 ID 是 `agency_main`
- prefab 名字是 `AgencyMainPanel`
- spec 里应该只有 3 个节点：`AgencyMainPanel`、`TaskList`、`ClaimButton`
- `TaskList` 应该带 `ScrollRect`
- `ClaimButton` 应该同时带 `Image` 和 `Button`
- `ClaimButton` 的 `Button` 需要暴露 `on_click`，并在 manifest / adapter plan 中保留人工接线痕迹

## 实操前准备

### 编辑器

默认脚本会从 `ProjectSettings/ProjectVersion.txt` 读取编辑器版本，并自动尝试：

- `/Applications/Tuanjie/Hub/Editor/<version>/Tuanjie.app/Contents/MacOS/Tuanjie`
- `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`

如果自动探测失败，再用 `--editor /path/to/Tuanjie` 显式指定。

### 为什么推荐临时工程

推荐优先用脚本跑，而不是直接在主工程菜单里点，是因为脚本会把项目复制到临时目录再执行 batchmode：

- 不会和你当前打开的主工程抢项目锁
- 不会把 sample 生成产物直接写进主仓库工作区
- 你仍然可以在成功后保留临时工程，再用编辑器打开它做肉眼检查

如果你直接在主工程菜单里跑 `Run Holmas Sample Pipeline`，生成的 prefab 草稿会写进仓库当前工作区，导致 worktree 变脏。只有在你明确就是想在主工程里观察落地结果时才建议这么做。

## 完整操作步骤

### 1. 先做 layout 验证

在仓库根目录执行：

```bash
python3 scripts/ui_prefab_generator/check_ui_generator_layout.py
```

通过标准：

- 输出 `[ok] ui prefab generator isolated layout is present`

这一步主要确认：

- 长期主文档专区还在
- `Assets/Tools/UiPrefabGenerator` 的核心目录和 asmdef 还在
- Holmas sample fixtures 还在
- `.meta` GUID 没坏

### 2. 跑 UiPrefabGenerator EditMode 测试

在仓库根目录执行：

```bash
scripts/ui_prefab_generator/run_ui_prefab_generator_editmode_tests.sh
```

通过标准：

- 日志里出现 `UiPrefabGenerator EditMode tests finished.`
- 脚本最后输出 `UiPrefabGenerator EditMode 验证通过`

这一步主要覆盖：

- sample pipeline runner
- prefab draft writer
- prefab structure validation
- manifest validation
- deterministic regression

如果这一步不过，不要继续怀疑 sample 输入本身，先把生成器或基线回归问题查清楚。

### 3. 跑 Holmas sample pipeline

在仓库根目录执行：

```bash
scripts/ui_prefab_generator/run_sample_ui_pipeline.sh --keep-temp-on-success --log-prefix manual_walkthrough
```

推荐带上 `--keep-temp-on-success`，因为你后面要亲自打开生成出的 prefab。  
这条命令会做几件事：

1. 复制当前项目到临时目录
2. 读取 `Samples~/Holmas/sample_design_packet.json`
3. 运行 `DesignPacket -> UiPrefabSpec -> prefab 草稿 + manifest -> adapter`
4. 写出 report
5. 成功后保留临时工程，供你人工检查

通过标准：

- 日志里出现 `UiPrefabGenerator sample pipeline finished.`
- 日志里出现 `Exiting batchmode successfully now!`
- report 文件里 `Success` 为 `true`

### 4. 看 report，不要直接跳过

默认推荐命令会生成：

- 日志：`/tmp/manual_walkthrough.log`
- report：`/tmp/manual_walkthrough_report.json`

先确认 report 顶层这几项：

- `Success == true`
- `ProfileId == holmas_ugui`
- `PageId == agency_main`
- `PrefabName == AgencyMainPanel`
- `PrefabDraftPath == Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab`

再确认 `Stages` 里这些阶段全部成功：

- `intake`
- `spec`
- `preview_generation`
- `draft_write`
- `manifest_validation`
- `prefab_structure_validation`
- `adapter_consumption`

如果某一段失败，就按阶段定位，不要一上来去翻 prefab：

- `intake` 失败：先看 `DesignPacket` 是否有 blocking issue
- `spec` 失败：先看解释器是否没有产出合法 `UiPrefabSpec`
- `preview_generation` / `draft_write` 失败：先看生成器和 prefab 写盘
- `manifest_validation` 失败：先看 manifest 结构、组件类型、资源位、命名冲突
- `prefab_structure_validation` 失败：先看真正写出的 prefab 结构是否和 spec 漂了
- `adapter_consumption` 失败：先看 Holmas profile / adapter 映射

### 5. 打开保留下来的临时工程

跑完上一步后，脚本会打印一个临时工程路径，形如：

```text
/private/tmp/ui_prefab_sample_pipeline_xxxxxx
```

然后用团结编辑器打开这个临时工程，而不是主工程。  
打开后重点看：

- 生成 prefab 是否存在于 `Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab`
- Project 面板里是否真的能选中这个 prefab
- 选中 prefab 后 Hierarchy / Inspector 是否和 sample spec 一致

### 6. 亲自检查 prefab 结构

打开 `AgencyMainPanel.prefab` 后，至少检查下面这些点：

#### 根节点

- 根节点名称是 `AgencyMainPanel`
- 根节点存在 `RectTransform`
- 根节点存在 `Image`
- 根节点上的图片资源位语义对应 `panel_bg`

#### TaskList 子节点

- 存在名为 `TaskList` 的子节点
- 节点上有 `RectTransform`
- 节点上有 `ScrollRect`
- 这个 `ScrollRect` 对应的绑定语义是 `task_list`

#### ClaimButton 子节点

- 存在名为 `ClaimButton` 的子节点
- 节点上有 `RectTransform`
- 节点上有 `Image`
- 节点上有 `Button`
- `Image` 对应的资源位语义是 `claim_button_bg`
- `Button` 对应的绑定语义是 `claim_button`

### 7. 检查人工接线缺口是否被正确暴露

这一步很关键。第一版系统的目标不是替你写完 Presenter 或业务 wiring，而是把“哪里还要人工接”准确暴露出来。

所以你要对照 `sample_prefab_binding_manifest.json` 和 report 里的 `AdapterPlanText` 确认：

- `AgencyMainPanel/ClaimButton` 的 `Button` entry 带 `event_name = on_click`
- 这条 entry 的 `requires_manual_wiring = true`
- notes 里还能看到 `handler_key = claim_task`
- adapter plan 里的 `ManualWiringNodePaths` 包含 `AgencyMainPanel/ClaimButton`

如果按钮已经被生成出来，但 manifest 没把人工接线缺口标出来，这也算流程不通过。

### 8. 对照 golden fixtures，确认没有无关漂移

人工检查最后一步，不是只看“能不能生成出来”，而是看“是不是还是这份 sample 预期的结果”。

最少对照这三份：

- `sample_ui_prefab_spec.json`
- `sample_prefab_binding_manifest.json`
- `sample_holmas_generated_result_plan.json`

重点判断：

- 节点树有没有平白多出无关节点
- 组件类型有没有发生无关漂移
- `manifest` 里的 entry 顺序和关键信息是否稳定
- adapter plan 是否仍指向 `App.HotUpdate.Holmas.UI.Generated`
- 人工接线节点是否仍只有 `AgencyMainPanel/ClaimButton`

## 如果你想在编辑器菜单里手点一遍

也可以，但只建议在你清楚会污染当前工作区时再这么做。

菜单入口有两个：

- `UiPrefabGenerator/Validation/Run UiPrefabGenerator EditMode Tests`
- `UiPrefabGenerator/Validation/Run Holmas Sample Pipeline`

建议顺序仍然是先点测试，再点 sample pipeline。  
如果你用的是主工程而不是临时工程，跑完后记得检查工作区里是否新增了：

- `Assets/Res/Perfabs/Generated/Holmas/AgencyMainPanel.prefab`

## 建议的人工验收口径

你亲自校验一次时，可以按下面的口径判定是否通过：

1. layout 检查通过
2. EditMode 测试通过
3. sample pipeline report 全阶段通过
4. 生成 prefab 能在编辑器里打开
5. prefab 节点树和 sample spec 对得上
6. manifest 和 adapter plan 对人工接线缺口的暴露是准确的
7. 没有出现无关漂移

满足这 7 条，才算“亲自走通了一次 UI 自动生成流程”。

## 常见坑

### 坑 1：只看 prefab，不看 report

这样容易漏掉 intake warning、manifest validation 或 adapter consumption 的失败。

### 坑 2：直接在主工程里点菜单

这样会把 sample 生成结果写进当前仓库，容易和真实开发改动混在一起。

### 坑 3：只看生成成功，不看 manual wiring 暴露

第一版系统本来就不会自动写业务接线，所以“缺口有没有被正确标出来”是验收的一部分。

### 坑 4：把图片当作机器权威输入

当前机器权威中间层始终是 `UiPrefabSpec`，图片只作为设计参考。  
人工审阅时要重点看 spec 是否正确，不要跳过 spec 直接按图片印象判断。
