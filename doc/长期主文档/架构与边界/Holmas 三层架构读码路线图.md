# Holmas 三层架构读码路线图

## 这份文档解决什么问题

这份路线图不是用来一次性解释所有细节，而是给第一次接手 Holmas 的开发者一条稳定的读码主线。

目标只有三个：

- 先分清 `App.AOT / App.Shared / App.HotUpdate` 到底各管什么
- 再顺着真实调用链读完“启动 -> 配置 -> 玩法 -> UI -> 平台适配”
- 最后让你能自己回答“这段代码为什么放在这里，而不是别的层”

## 先建立三层脑图

先把项目想成三层：

- `App.AOT`
  - 宿主层
  - 负责启动、平台桥接、资源系统、网络、持久化、HybridCLR
- `App.Shared`
  - 协议层
  - 负责跨层共享的接口、DTO、事件、基础契约
- `App.HotUpdate`
  - 业务层
  - 负责正式玩法、运行时流程、UI 页面、任务与成长

如果你读到一段代码时拿不准放哪层，先问三个问题：

1. 它是不是宿主基础设施？
2. 它是不是跨层都要认识的稳定契约？
3. 它是不是具体业务和玩法逻辑？

这三个问题通常就够把文件放回正确位置。

## 读码方法

推荐你每读完一个文件，只写下三句结论：

- 这个文件的职责是什么？
- 它依赖上游谁？
- 它把什么结果交给下游？

只要持续这样记，项目会从“很多文件”变成“一条主链”。

## 按文件名顺序的一条主线

### 第 1 站：宿主启动入口

文件：
- [GameBootstrap.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.AOT/Bootstrap/GameBootstrap.cs)

这一站看什么：
- Unity 首场景启动后，第一个真正负责“把系统跑起来”的地方是谁
- 它先后初始化了哪些基础设施
- 为什么日志、平台、网络、资源、HybridCLR 都在这里起

看完你应该能回答：
- 为什么 `GameBootstrap` 属于 `App.AOT`
- 它为什么不该直接写玩法规则

### 第 2 站：热更新装载入口

文件：
- [HybridClrLoader.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.AOT/HotUpdate/HybridClrLoader.cs)

这一站看什么：
- AOT 层是怎么把热更新程序集加载起来的
- 宿主层和热更层的真正交接点在哪里

看完你应该能回答：
- `GameBootstrap` 和 `HybridClrLoader` 的边界分别是什么
- 为什么热更入口不应该直接写在 Unity 场景脚本里

### 第 3 站：共享契约总入口

文件：
- [IService.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.Shared/Contracts/IService.cs)

这一站看什么：
- `App.Shared` 里到底放什么
- 哪些接口和 DTO 是跨层通信的基础语言

重点留意：
- `IServiceContainer`
- `IAppLogger`
- `IAssetsRuntime`
- `IWeChatBridge`
- `IEventBus`
- `WeChatWindowInfo`
- `UiSafeAreaInfo`

看完你应该能回答：
- 为什么这些类型适合放 `App.Shared`
- 为什么这里不放具体玩法实现

### 第 4 站：Holmas 业务总启动入口

文件：
- [HolmasGameBootstrap.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Bootstrap/HolmasGameBootstrap.cs)

这一站看什么：
- HotUpdate 层接住 AOT 提供的服务后，先组装了什么
- Holmas 业务骨架是怎么立起来的

看完你应该能回答：
- AOT 到 HotUpdate 的接力点在哪里
- 为什么这里是业务组合根，而不是具体玩法执行点

### 第 5 站：业务上下文容器

文件：
- [HolmasApplicationContext.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Application/HolmasApplicationContext.cs)

这一站看什么：
- Holmas 当前运行时到底需要持有哪些核心对象
- 为什么很多业务流程都会先拿到 `HolmasApplicationContext`

看完你应该能回答：
- 这个类聚合了哪些长期会被复用的业务能力
- 它为什么不是 UI 层对象

### 第 6 站：配置加载入口

文件：
- [HolmasConfigRuntimeLoader.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Levels/HolmasConfigRuntimeLoader.cs)

这一站看什么：
- 配置资源是怎么在运行时被加载和转换的
- 正式配置为什么走运行时加载链，而不是写死在 UI 里

看完你应该能回答：
- Holmas 的配置读取入口在哪里
- 配置加载和玩法执行为什么要分开

### 第 7 站：关卡请求生成

文件：
- [HolmasLevelRequestGenerator.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Levels/HolmasLevelRequestGenerator.cs)

这一站看什么：
- 玩家当前状态是怎么被翻译成一次关卡生成请求的
- 等级、配置、任务需求是怎么进入关卡请求的

看完你应该能回答：
- `LevelGenerationRequest` 代表什么
- 为什么“生成关卡请求”不应该写在 `View` 或 `Controller` 里

### 第 8 站：关卡启动门面

文件：
- [HolmasLevelLaunchGateway.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Application/HolmasLevelLaunchGateway.cs)

这一站看什么：
- UI 点击“开始”之后，真正进入关卡之前会经过哪个业务门面
- 门面层如何把 UI 请求转成玩法启动动作

看完你应该能回答：
- 为什么要有 `Gateway`
- 它和真正的玩法运行时是什么关系

### 第 9 站：玩法运行时主脑

文件：
- [HolmasGameplayRuntime.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Application/HolmasGameplayRuntime.cs)

这一站看什么：
- 开局、推进、结算这些核心动作最终在哪个对象里发生
- 地图模板、运行时状态、任务推进、成长推进如何在这一层串起来

看完你应该能回答：
- 哪些逻辑一定应该留在 `HolmasGameplayRuntime`
- 为什么它是业务主脑，而不是某个 `MonoBehaviour`

### 第 10 站：UI 启动入口

文件：
- [HolmasUiBootstrap.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Bootstrap/HolmasUiBootstrap.cs)

这一站看什么：
- UI 根节点是怎么被动态创建出来的
- 为什么 `BootstrapScene` 里看不到完整静态 UI

看完你应该能回答：
- UI 为什么不是预摆在场景里的固定结构
- HotUpdate UI 是怎么接到业务上下文的

### 第 11 站：UI 根节点与层级

文件：
- [UiRoot.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/UiRoot.cs)

这一站看什么：
- `Canvas`、页面层、弹窗层、覆盖层是谁创建的
- `UiRoot` 为什么是所有页面的宿主

重点留意：
- `PageLayer`
- `PopupLayer`
- `OverlayLayer`
- Safe Area 相关初始化

看完你应该能回答：
- 为什么 `UiRoot` 是 UI 世界的根
- 层级为什么集中在这里维护

### 第 12 站：页面注册与打开服务

文件：
- [UiScreenService.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/UiScreenService.cs)

这一站看什么：
- 页面 prefab 是如何注册、预加载、打开、关闭的
- `Controller` 是如何被创建出来的

看完你应该能回答：
- `UiScreenService` 管的到底是“资源”还是“页面状态”
- 为什么页面切换不能散落在各个 View 里随便写

### 第 13 站：页面流程调度器

文件：
- [HolmasFlowCoordinator.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/HolmasFlowCoordinator.cs)

这一站看什么：
- `Loading -> AgencyMain -> Battle` 的流程由谁统一调度
- 页面切换和玩法启动怎么衔接

看完你应该能回答：
- 为什么 `FlowCoordinator` 是“流程编排者”
- 它为什么不承担具体表现和玩法细节

### 第 14 站：侦探社首页 MVC 读法

文件：
- [AgencyMainPageController.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/AgencyMain/AgencyMainPageController.cs)
- [AgencyMainPresenter.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/AgencyMain/AgencyMainPresenter.cs)
- [AgencyMainView.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/AgencyMain/AgencyMainView.cs)

这一站看什么：
- `Controller / Presenter / View` 在这个项目里的分工方式
- 侦探社首页上的按钮、文案、任务栏和运行时兼容布局分别由谁负责

看完你应该能回答：
- `Controller` 为什么负责接输入和编排
- `Presenter` 为什么负责把业务状态翻译成可显示数据
- `View` 为什么只负责节点和表现

### 第 15 站：Loading 和 Battle 页面

文件：
- [LoadingView.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/Loading/LoadingView.cs)
- [BattlePageController.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/Battle/BattlePageController.cs)
- [BattleView.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/Battle/BattleView.cs)

这一站看什么：
- 加载页怎么负责启动态反馈
- 战斗页怎么把关卡运行时状态渲染成棋盘表现

看完你应该能回答：
- 为什么 `BattleView` 只做显示和交互，不做生成逻辑
- 为什么 `LoadingView` 的作用是阶段反馈，而不是业务决策

## 建议最后再读的支线

### 支线 1：平台桥接与安全区

文件：
- [WeChatBridge.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.AOT/Platform/WeChat/WeChatBridge.cs)
- [UiSafeAreaRuntime.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/UiSafeAreaRuntime.cs)
- [UiSafeAreaFitter.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/UiSafeAreaFitter.cs)

为什么放在后面读：
- 这是“平台适配细节”
- 它会影响 UI 布局结果，但不是理解主业务链的第一入口

### 支线 2：运行时字体与动态补节点

文件：
- [RuntimeTmpFontResolver.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/RuntimeTmpFontResolver.cs)

为什么放在后面读：
- 这是运行时显示层的兜底能力
- 它能帮助你理解“为什么有些文本不是 prefab 里原生带好的”

## 一小时速读版

如果你只有一小时，按下面顺序读：

1. [GameBootstrap.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.AOT/Bootstrap/GameBootstrap.cs)
2. [HybridClrLoader.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.AOT/HotUpdate/HybridClrLoader.cs)
3. [IService.cs](/Users/bruce/work/Holmas/Assets/Scripts/App.Shared/Contracts/IService.cs)
4. [HolmasGameBootstrap.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Bootstrap/HolmasGameBootstrap.cs)
5. [HolmasApplicationContext.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Application/HolmasApplicationContext.cs)
6. [HolmasGameplayRuntime.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/Application/HolmasGameplayRuntime.cs)
7. [HolmasUiBootstrap.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Bootstrap/HolmasUiBootstrap.cs)
8. [UiRoot.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/UiRoot.cs)
9. [UiScreenService.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/UiScreenService.cs)
10. [HolmasFlowCoordinator.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Core/HolmasFlowCoordinator.cs)
11. [AgencyMainPageController.cs](/Users/bruce/work/Holmas/Assets/HotUpdateContent/Script/App.HotUpdate/Holmas/UI/Screens/AgencyMain/AgencyMainPageController.cs)

这 11 个文件看通以后，项目的主脉络基本就立住了。

## 第二轮深入建议

主链看完后，再按兴趣进入二轮：

- 如果你想理解玩法数据怎么来：继续看配置表、运行时状态、任务推进相关文件
- 如果你想理解 UI 怎么接 prefab：继续看各个 `ScreenRegistration`、`Controller`、`Presenter`、`View`
- 如果你想理解平台差异：继续看 `WeChatBridge`、安全区、窗口信息刷新链
- 如果你想理解为什么很多东西不直接写进 `MonoBehaviour`：回头对照 `App.Shared` 和 `HolmasGameplayRuntime`
- 如果你想补看旧版首页链路：再回头看 `MainPageController / MainPresenter / MainView`

## 关于 MainScreen

当前运行时主链的首页是 `AgencyMain`，不是早期的 `MainScreen`。

`MainScreen` 仍然值得读，但更适合当成“旧首页 / 补充页面实现”来理解：

- 它能帮助你看懂早一轮 UI 接线方式
- 它仍然保留了一些运行时补节点、TMP 文本兜底的实现思路
- 但如果你的目标是先看懂当前真实用户路径，应优先读 `AgencyMain`

## 最后用一句话记住

Holmas 这套项目最重要的不是“某个脚本怎么写”，而是下面这条分工一直不乱：

- `App.AOT` 把系统能力准备好
- `App.Shared` 定义双方怎么说话
- `App.HotUpdate` 用这些能力去跑真正的游戏业务
