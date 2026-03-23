# 项目结构（抽离版）

```text
UnityProject_CoreExtract
├── Assets
│   ├── Scripts
│   │   ├── App.Shared
│   │   │   ├── App.Shared.asmdef
│   │   │   └── Contracts
│   │   │       ├── IService.cs
│   │   │       └── ITransport.cs
│   │   └── App.AOT
│   │       ├── App.AOT.asmdef
│   │       ├── Bootstrap
│   │       │   ├── GameBootstrap.cs
│   │       │   └── AppLifetime.cs
│   │       ├── Infrastructure
│   │       │   ├── DI/ServiceContainer.cs
│   │       │   ├── EventBus/EventBus.cs
│   │       │   ├── Logger/UnityLogger.cs
│   │       │   ├── Persistence/FilePersistenceProvider.cs
│   │       │   └── Tick/TickManager.cs
│   │       ├── Networking
│   │       │   ├── NetClient.cs
│   │       │   ├── Auth/AuthContext.cs
│   │       │   └── Transport/HttpTransport.cs
│   │       ├── Platform
│   │       │   └── WeChat/WeChatBridge.cs
│   │       ├── YooRuntimeAssets
│   │       │   ├── YooAssetsRuntime.cs
│   │       │   └── PatchFlow/PatchOperationHandler.cs
│   │       └── HotUpdate
│   │           └── HybridClrLoader.cs
│   └── HotUpdateContent
│       └── Script
│           └── App.HotUpdate
│               ├── App.HotUpdate.asmdef
│               └── Entry/HotUpdateEntry.cs
├── README.md
├── PROJECT_STRUCTURE.md
└── UPDATE_LOGIC_FLOW.md
```

## 分层说明
- `App.Shared`：跨层协议与接口定义（AOT/HotUpdate共用）。
- `App.AOT`：宿主层，负责启动、基础设施、更新与热更加载。
- `App.HotUpdate`：热更层入口（当前为无业务占位实现）。
