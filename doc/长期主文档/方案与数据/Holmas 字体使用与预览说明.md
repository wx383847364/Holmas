# Holmas 字体使用与预览说明

## 当前字体目录

字体源文件放在：

- `Assets/Res/Font/NotoSansSC.ttf`
- `Assets/Res/Font/LXGWMarkerGothic-Regular.ttf`
- `Assets/Res/Font/ResourceHanRoundedCN-Regular.ttf`
- `Assets/Res/Font/SmileySans-Oblique.ttf`
- `Assets/Res/Font/ZCOOLKuaiLe-Regular.ttf`

本地整体风格预览页：

- `tools/font_preview/HolmasFontPreview.html`

用浏览器打开这个 HTML，可以同时看到同一套 Holmas UI 样例套用不同字体后的观感。

## Unity / TextMeshPro 使用方式

正式 UI 文本建议走 TextMeshPro，不直接把 `.ttf` 挂到界面上。

首选使用项目内置 Editor 工具：

1. 打开 Unity。
2. 选择菜单 `Holmas > UI > Font Role Tool`。
3. 检查 `FormalBody / FormalTitle / ActivityTitle / Numeric / Fallback` 五个角色的字体。
4. 点击 `Generate / Refresh TMP Assets`，生成或刷新 `Assets/Res/Font/TMP/` 下的 TMP 字体资产。
5. 点击 `Scan Formal Prefabs`，查看每个正式 prefab 里的文本将匹配到哪个字体角色。
6. 如需单独调整某条文本，在扫描报告里改角色，工具会保存为手动覆盖。
7. 点击 `Apply To Formal Prefabs`，批量替换正式 UI prefab 字体。
8. 点击 `Create / Refresh Runtime Settings`，同步运行时创建文本使用的正式字体与兜底字体。

工具按钮说明：

工具窗口里默认显示每个按钮的一句简述；点击按钮右侧的 `Details` 可以展开完整说明，避免工具窗口过高。

- `Scan Fonts Folder`：从 `Assets/Res/Font` 重新查找可用的 `.ttf / .otf` 字体，并给空角色补上推荐字体；只更新 `HolmasFontRoleProfile.asset`，不生成 TMP，也不修改 prefab。新增、删除、替换字体文件后先点它。
- `Generate / Refresh TMP Assets`：把已启用角色的源字体生成或复用为 TMP Font Asset；会创建或刷新 `Assets/Res/Font/TMP` 下的 TMP 资源，并回填到角色的 `TMP Font`。`TMP Font` 显示 `None`，或更换角色字体后点它。
- `Create / Refresh Runtime Settings`：同步运行时新建文本使用的正式字体和中文兜底字体；会创建或刷新 `Assets/Res/Font/HolmasFontRuntimeSettings.asset`，不修改 prefab。调整 `FormalBody` 或 `Fallback` 后点它。
- `Scan Formal Prefabs`：预览正式 prefab 里的每个 TMP / Text 会匹配到哪个字体角色；范围是 `MainPanel`、`BattlePanel`、`LoadingPanel`、`LeadbroadPanel`、`AgencyMainPanel`。普通扫描不写 prefab；如果在报告里手动改角色，会保存覆盖规则到 Profile。执行 Apply 前先点它。
- `Apply To Formal Prefabs`：把字体角色真正写入正式 prefab；`TextMeshProUGUI` 使用角色的 TMP Font，legacy `Text` 使用角色的 Source Font。不改字号、颜色、对齐、文本内容、布局和材质颜色。确认 Scan 结果没问题，并且 TMP Font 都已生成后再点它。

也可以手动创建 TMP 字体资产：

1. 打开 Unity。
2. 选择菜单 `Window > TextMeshPro > Font Asset Creator`。
3. `Source Font File` 选择一个字体源文件，例如 `LXGWMarkerGothic-Regular.ttf`。
4. `Character Set` 建议选择 `Custom Characters`。
5. `Custom Character List` 填入当前版本 UI 实际会出现的汉字、英文、数字和标点。
6. 点击 `Generate Font Atlas`。
7. 点击 `Save`，建议保存到 `Assets/Res/Font/TMP/`。
8. 在 prefab 或运行时创建的 `TextMeshProUGUI` 上，把 `Font Asset` 换成生成出来的 TMP Font Asset。

## 推荐接入策略

- 主 UI 字体：优先试 `LXGWMarkerGothic-Regular.ttf`。
- 稳妥圆润字体：试 `ResourceHanRoundedCN-Regular.ttf`，但它原始体积很大，必须子集化。
- 标题点缀：`SmileySans-Oblique.ttf` 或 `ZCOOLKuaiLe-Regular.ttf`，只用于短标题、活动字、奖励弹窗。
- 正文兜底：`NotoSansSC.ttf` 只做 fallback 或子集正文，不建议直接进微信小游戏首包。

## 微信小游戏包体规则

不要直接把完整中文字体打进首包。当前全量字体目录大约 34M，其中：

- `NotoSansSC.ttf` 约 13M。
- `ResourceHanRoundedCN-Regular.ttf` 约 14M。

正式发布前要做：

1. 从配置表、UI 文案、按钮文案、排行榜、收藏品页面里抽取实际字符集。
2. 用字体子集化工具生成小字体。
3. 再由 TextMeshPro 生成小型 SDF Font Asset。
4. 首包只放主流程必需字符，活动、图鉴、排行榜可走分包或远程资源。

## 快速取舍

当前 Holmas 最建议先试：

- `LXGWMarkerGothic-Regular.ttf`：标题、按钮、任务卡、常规 UI。
- `NotoSansSC.ttf`：长正文 fallback。

如果看完预览觉得主 UI 需要更规整，再把主字体换成 `ResourceHanRoundedCN-Regular.ttf`。
