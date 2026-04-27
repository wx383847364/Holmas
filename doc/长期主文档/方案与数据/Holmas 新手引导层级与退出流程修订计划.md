# Holmas 新手引导层级与退出流程修订计划

## Summary

采用 Main 内双棋盘容器方案：正式棋盘在 `BoardContainer`，教程棋盘在更高层的 `TutorialBoardContainer`。教程 Overlay 只做提示和高亮，GM Popup 继续高于 Overlay。教程完成或跳过时，统一清掉教程局并直接进入正式棋盘，但不打开 Loading。

## Key Changes

- Main 内棋盘层级：
  - `MinesGroup/BoardContainer` 渲染正式棋盘。
  - `MinesGroup/TutorialBoardContainer` 渲染教程棋盘，并作为更高 sibling。
  - 当前 `LevelSnapshot` 是教程 map 时，只显示教程棋盘；非教程 map 时，只显示正式棋盘。
  - `ResolveTutorialTarget("BoardCell:*")` 在教程步骤中优先取 active tutorial board，失败时降级到棋盘整体。

- Overlay 与输入：
  - `TutorialOverlay` 继续走 Overlay 层，但不做全屏输入阻断。
  - 高亮、手指、背景装饰不吃射线；只有教程按钮吃输入。
  - GM 按钮在调试态保持可打开；GM Popup 走 Popup 层，高于 Tutorial Overlay。
  - 若 Loading Overlay 会关闭教程 Overlay，则在 Loading 结束后由教程协调器恢复后续步骤，或把教程提示改为非互斥教程通道；实现时优先选择“恢复后续步骤”，改动更小。

- 教程退出流程：
  - 点“完成”或“跳过”都写入教程完成状态。
  - 如果当前仍是教程棋盘，调用现有 runtime/session 清理路径结束教程局。
  - 随后通过无 Loading 的正式棋盘启动入口刷新 Main，显示 `BoardContainer`。
  - 不把“教程完成”写进玩法 runtime；教程状态仍只走 `CoreFindCatTutorialProgressService`。

- 服务层修正：
  - 教程猫池只取当前任务栏未完成、未领奖、有效 `catId` 的任务，按槽位顺序。
  - 普通棋盘 `NormalBoardHint` 只写 `dismissedNormalBoardHint`，绝不写 `completed=true`。
  - 进度保存继续单调合并：已有 `completed=true` 不能被旧步骤保存覆盖。

## Test Plan

- 必跑验证：
  - `git diff --check`
  - `bash tools/validation/check_boundary.sh`
  - `python3 -m unittest discover -s tools/tests -p 'test_*.py'`
  - `bash tools/validation/run_holmas_validation.sh`

- 补/调测试：
  - Main bindings 能解析 `TutorialBoardContainer`。
  - 教程局显示教程棋盘并隐藏正式棋盘。
  - 正式局显示正式棋盘并隐藏教程棋盘。
  - 教程完成/跳过后清理教程局并启动正式局，但不显示 Loading。
  - Loading Overlay 不会永久丢失教程后续步骤。
  - Overlay 展示时棋盘、GM 入口、主界面关键按钮仍可响应。
  - 教程猫池排除已完成/已领奖任务。
  - `completed=true` 不会被旧保存降级。

## Assumptions

- 教程棋盘不新建第二套 gameplay runtime，只新建第二套 UI board view；玩法权威仍是当前 `HolmasGameplayRuntime`。
- 完成或跳过教程会直接切正式棋盘，但不会走 Loading；这是本轮修正后的默认行为。
- 当前工作区已有中断前的部分补丁；实施前先审 diff，把不符合本计划的内容修正或移除。
