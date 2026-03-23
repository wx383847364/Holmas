using UnityEngine;
using App.Shared.Contracts;
using App.AOT.Infrastructure.DI;
using App.AOT.Infrastructure.Logger;
using App.AOT.Infrastructure.Tick;
using App.AOT.Infrastructure.EventBus;
using App.AOT.Infrastructure.Persistence;
using App.AOT.YooRuntimeAssets;
using App.AOT.HotUpdate;
using App.AOT.Networking;
using App.AOT.Platform.WeChat;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// 游戏启动器：AOT层入口，挂载在首场景
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private ServiceContainer _serviceContainer;
        private UnityLogger _logger;
        private TickManager _tickManager;
        private EventBus _eventBus;
        private FilePersistenceProvider _persistence;
        private YooAssetsRuntime _yooAssets;
        private HybridClrLoader _hybridClrLoader;
        private NetClient _netClient;
        private WeChatBridge _weChatBridge;

        private async void Start()
        {
            DontDestroyOnLoad(gameObject);
            await InitializeAsync();
        }

        /// <summary>
        /// 获取服务容器（供AOT层其他组件使用）
        /// </summary>
        public IServiceContainer GetServiceContainer()
        {
            return _serviceContainer;
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            // 1. 初始化基础设施（Logger需要最先初始化）
            InitializeInfrastructure();

            _logger.LogInfo("GameBootstrap: 开始初始化...");

            // 2. 初始化平台桥接（微信）
            InitializePlatform();

            // 3. 初始化网络底座
            InitializeNetworking();

            // 4. 初始化YooAssets
            await InitializeYooAssetsAsync();

            // 5. 加载HybridCLR热更代码
            await InitializeHybridClrAsync();

            _logger.LogInfo("GameBootstrap: 初始化完成，进入热更层");
        }

        private void InitializeInfrastructure()
        {
            _serviceContainer = new ServiceContainer();

            _logger = new UnityLogger();
            _logger.Initialize();
            // 同时注册为接口和具体类型，方便AOT和HotUpdate层都能获取
            _serviceContainer.RegisterSingleton<IAppLogger>(_logger);

            _tickManager = new TickManager();
            _tickManager.Initialize();
            _serviceContainer.RegisterSingleton<TickManager>(_tickManager);
            _serviceContainer.RegisterSingleton<ITickManager>(_tickManager);

            _eventBus = new EventBus();
            _serviceContainer.RegisterSingleton<EventBus>(_eventBus);
            _serviceContainer.RegisterSingleton<IEventBus>(_eventBus);

            _persistence = new FilePersistenceProvider();
            _serviceContainer.RegisterSingleton<IPersistence>(_persistence);

            // 注册服务容器自身（供HotUpdate层使用）
            _serviceContainer.RegisterSingleton<IServiceContainer>(_serviceContainer);

            _logger.LogInfo("基础设施初始化完成");
        }

        private void InitializePlatform()
        {
            _weChatBridge = new WeChatBridge();
            _weChatBridge.SetLogger(_logger);
            _weChatBridge.Initialize();
            _serviceContainer.RegisterSingleton<IWeChatBridge>(_weChatBridge);
            _logger.LogInfo("平台桥接初始化完成");
        }

        private void InitializeNetworking()
        {
            _netClient = new NetClient();
            _netClient.SetLogger(_logger);
            _netClient.Initialize();
            _serviceContainer.RegisterSingleton<INetClient>(_netClient);
            _logger.LogInfo("网络客户端初始化完成");
        }

        private async System.Threading.Tasks.Task InitializeYooAssetsAsync()
        {
            _yooAssets = new YooAssetsRuntime(_logger);
            await _yooAssets.InitializeAsync();
            _serviceContainer.RegisterSingleton<IAssetsRuntime>(_yooAssets);
            _serviceContainer.RegisterSingleton<YooAssetsRuntime>(_yooAssets);
            _logger.LogInfo("YooAssets初始化完成");
        }

        private async System.Threading.Tasks.Task InitializeHybridClrAsync()
        {
            _hybridClrLoader = new HybridClrLoader(_logger, _yooAssets, _serviceContainer);
            await _hybridClrLoader.LoadAsync();
            _logger.LogInfo("HybridCLR热更代码加载完成");
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            _tickManager?.Update(deltaTime);
            _netClient?.Update(deltaTime);
        }

        private void OnDestroy()
        {
            _hybridClrLoader?.Shutdown();
            _yooAssets?.Shutdown();
            _netClient?.Shutdown();
            _weChatBridge?.Shutdown();
            _tickManager?.Shutdown();
            _logger?.Shutdown();
        }
    }
}
