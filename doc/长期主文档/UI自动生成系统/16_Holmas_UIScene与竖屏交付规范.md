# Holmas UIScene 与竖屏交付规范

## 文档定位

这页固定 Holmas 竖屏小游戏 UI 的制作工作台、正式交付物、机型验收口径、字体策略和场景职责。  
它是美术、UI 程序、生成链接入同一套基线的权威说明。

## 一句话结论

- `UIScene` 是工作台，不是正式交付物
- 最终交付物是 `page prefab + binding/manifest`
- 正式运行入口是 `BootstrapScene`
- 竖屏 UI 基线固定为 `1080 x 1920 + Match Height`

## 场景职责

### `BootstrapScene`

- 生产入口场景
- 固定挂 `GameBootstrap` 与 `AppLifetime`
- 进入 BuildSettings
- 不承载美术拼装

### `UIScene`

- 美术拼装台
- 适配验收台
- 不进入生产 BuildSettings
- 不承载正式业务引用

### `SampleScene` 与 `MinesweeperScene`

- 样例/验证场景
- 不作为正式入口

## UIScene 固定层级

```text
UIScene
├── EventSystem
├── PreviewCamera
└── UIRootPreview
    ├── UnsafeBackgroundLayer
    ├── SafeAreaRoot
    │   ├── PageLayer
    │   ├── PopupLayer
    │   ├── SheetLayer
    │   └── OverlayLayer
    └── DebugLayer
```

## Canvas 固定参数

- `Render Mode = Screen Space - Overlay`
- `CanvasScaler = Scale With Screen Size`
- `Reference Resolution = 1080 x 1920`
- `Screen Match Mode = Match Width Or Height`
- `Match Width Or Height = 1.0`

## 美术拼装规则

- 所有核心交互内容必须放在 `SafeAreaRoot` 或页面内部的 `SafeAreaContent`
- 背景、边缘装饰、氛围图可以放在 `UnsafeBackgroundLayer`
- `DebugLayer` 只放设备框、安全区框、比例辅助线，不放业务控件
- 页面长期维护在 prefab，不长期维护在 `UIScene`
- 一个页面一个 prefab 根，不跨页面拖内部节点

## 正式交付物

- `XxxPage.prefab`
- `XxxPopup.prefab`
- `XxxSheet.prefab`
- `XxxOverlay.prefab`
- 对应 `binding/manifest`

页面 prefab 推荐固定结构：

```text
XxxPage
├── BackgroundRoot
├── SafeAreaContent
├── DecorationRoot
└── InteractionRoot
```

## 竖屏机型比例验收

Holmas 不按“每种机型一套设计稿”维护，而按比例桶验收：

- `16:9`：短屏兼容下限
- `19.5:9`：iPhone 主流全面屏
- `20:9`：Android 主流长屏
- `21:9`：超长屏 guardrail，只做溢出保护

建议在编辑器里至少建立以下预览尺寸：

- `750 x 1334`
- `1179 x 2556`
- `1080 x 2340`
- `1080 x 2400`
- `1080 x 2560`

验收要求：

- 顶栏标题、货币区、返回按钮不被裁切
- 底部 CTA 不被底部安全区遮挡
- 弹窗主按钮和关闭按钮始终可见
- 滚动区边缘与遮罩在长屏上不漂移

## 微信安全区链路

- AOT 层 `WeChatBridge` 负责提供窗口尺寸与安全区信息
- HotUpdate/UI 层优先消费桥接数据
- Unity `Screen.safeArea` 作为回退兜底
- 编辑器通过 `UIScene` 的尺寸预设与 `DebugLayer` 做人工验收

当前冻结口径：

- 运行时优先相信桥接数据
- 没有桥接数据时退回 `Screen.safeArea`
- 安全区逻辑统一收口到 `UiSafeAreaFitter`

## 字体规范

这里的 `Tmp` 指临时目录 `UI/Tool/Tmp`，不是 TextMeshPro。两者必须分开理解。

冻结策略：

- 新页面允许使用 `TextMeshProUGUI`
- 旧 `Text` 页面继续兼容
- 不要求一次性全量迁移
- 生成链与人工拼装链都要遵循同一套字体口径

最小组件支持口径：

- legacy：`Text`、`InputField`
- TMP：`TextMeshProUGUI`、`TMP_InputField`、`TMP_Dropdown`

## BuildSettings 约定

- 生产 BuildSettings 只放正式入口场景
- 当前固定为：
  1. `Assets/Scenes/BootstrapScene.scene`
- `UIScene`、`SampleScene`、`MinesweeperScene` 不进生产入口链

## 备注

- `UI/Tool/Tmp` 作为临时遗留目录处理，不参与字体决策
- 如果后续删除该目录，必须同步检查是否仍有引用或历史说明依赖它
