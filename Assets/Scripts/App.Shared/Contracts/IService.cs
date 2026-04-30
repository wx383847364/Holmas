namespace App.Shared.Contracts
{
    public sealed class UiSafeAreaInfo
    {
        public int Left { get; set; }
        public int Right { get; set; }
        public int Top { get; set; }
        public int Bottom { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public UiSafeAreaInfo Clone()
        {
            return new UiSafeAreaInfo
            {
                Left = Left,
                Right = Right,
                Top = Top,
                Bottom = Bottom,
                Width = Width,
                Height = Height,
            };
        }
    }

    public sealed class WeChatWindowInfo
    {
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public float PixelRatio { get; set; } = 1f;
        public int StatusBarHeight { get; set; }
        public bool IsFallback { get; set; }
        public bool IsAvailable { get; set; }
        public UiSafeAreaInfo SafeArea { get; set; } = new UiSafeAreaInfo();

        public WeChatWindowInfo Clone()
        {
            return new WeChatWindowInfo
            {
                ScreenWidth = ScreenWidth,
                ScreenHeight = ScreenHeight,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                PixelRatio = PixelRatio,
                StatusBarHeight = StatusBarHeight,
                IsFallback = IsFallback,
                IsAvailable = IsAvailable,
                SafeArea = SafeArea != null ? SafeArea.Clone() : new UiSafeAreaInfo(),
            };
        }
    }

    /// <summary>
    /// 服务接口基类，所有服务都应实现此接口
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// 初始化服务
        /// </summary>
        void Initialize();

        /// <summary>
        /// 更新服务（每帧调用）
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// 销毁服务
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 日志接口（Shared层，供AOT和HotUpdate共用）
    /// </summary>
    public interface IAppLogger
    {
        void Log(LogLevel level, string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogInfo(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, params object[] args);
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 服务容器接口（Shared层，供AOT和HotUpdate共用）
    /// </summary>
    public interface IServiceContainer
    {
        void RegisterSingleton<T>(T instance) where T : class;
        T Get<T>() where T : class;
        bool IsRegistered<T>();
    }

    /// <summary>
    /// 网络客户端接口（Shared层）
    /// </summary>
    public interface INetClient : IService
    {
        bool IsConnected { get; }
        System.Threading.Tasks.Task<TransportResponse> SendRequestAsync(string url, string method = "GET", byte[] body = null, System.Collections.Generic.Dictionary<string, string> headers = null);
    }

    /// <summary>
    /// 持久化接口（Shared层）
    /// </summary>
    public interface IPersistence
    {
        System.Threading.Tasks.Task<bool> SaveAsync(string key, byte[] data);
        System.Threading.Tasks.Task<byte[]> LoadAsync(string key);
        System.Threading.Tasks.Task<bool> DeleteAsync(string key);
        bool Exists(string key);
    }

    /// <summary>
    /// 微信平台桥接接口（Shared层）
    /// </summary>
    public interface IWeChatBridge : IService
    {
        System.Threading.Tasks.Task<string> LoginAsync();
        System.Threading.Tasks.Task<bool> ShowRewardedAdAsync(string adUnitId);
        System.Threading.Tasks.Task<bool> RequestPaymentAsync(string orderId, string paymentParams);
        bool TryGetWindowInfo(out WeChatWindowInfo windowInfo);
    }

    /// <summary>
    /// 资源句柄（Shared层，用于跨层传递加载结果）
    /// </summary>
    public interface IAssetHandle
    {
        UnityEngine.Object AssetObject { get; }
        void Release();
    }

    /// <summary>
    /// 资源服务接口（Shared层）
    /// </summary>
    public interface IAssetsRuntime
    {
        System.Threading.Tasks.Task InitializeAsync();
        System.Threading.Tasks.Task<bool> RunPatchFlowAsync(string packageVersion = null);
        System.Threading.Tasks.Task<IAssetHandle> LoadAssetAsync(string location);
        void Shutdown();
    }

    /// <summary>
    /// Tick接口（Shared层）
    /// </summary>
    public interface ITickable
    {
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Tick管理器接口（Shared层）
    /// </summary>
    public interface ITickManager
    {
        void Register(ITickable tickable);
        void Unregister(ITickable tickable);
    }

    /// <summary>
    /// 请求进入战斗的事件（Shared层，供AOT和HotUpdate层跨层通信）
    /// </summary>
    public class RequestEnterBattleEvent
    {
        public string SceneName { get; set; } = "Battle";
    }

    /// <summary>
    /// 事件总线接口（Shared层，供AOT和HotUpdate层跨层通信）
    /// </summary>
    public interface IEventSubscription : System.IDisposable
    {
    }

    public interface IEventBus
    {
        void Subscribe<T>(System.Action<T> handler) where T : class;
        void Unsubscribe<T>(System.Action<T> handler) where T : class;
        IEventSubscription SubscribeScoped<T>(
            System.Action<T> handler,
            int priority = 0,
            System.Predicate<T> condition = null) where T : class;
        void Publish<T>(T eventData) where T : class;
    }
}
