using App.Shared.Contracts;

namespace App.HotUpdate.Entry
{
    /// <summary>
    /// 热更入口（去业务版）：仅保留跨层启动与基础Tick示例。
    /// </summary>
    public static class HotUpdateEntry
    {
        private static IServiceContainer _serviceContainer;
        private static IAppLogger _logger;
        private static ITickManager _tickManager;
        private static HotUpdateHeartbeat _heartbeat;

        public static void Start(IServiceContainer serviceContainer)
        {
            _serviceContainer = serviceContainer;
            _logger = _serviceContainer.Get<IAppLogger>();
            _tickManager = _serviceContainer.Get<ITickManager>();

            _logger?.LogInfo("HotUpdateEntry(Core): 热更层启动");

            // 示例：注册一个轻量Tick对象，验证AOT <-> HotUpdate跨层可用。
            _heartbeat = new HotUpdateHeartbeat(_logger);
            _tickManager?.Register(_heartbeat);

            _logger?.LogInfo("HotUpdateEntry(Core): 初始化完成");
        }

        private sealed class HotUpdateHeartbeat : ITickable
        {
            private readonly IAppLogger _logger;
            private float _elapsed;

            public HotUpdateHeartbeat(IAppLogger logger)
            {
                _logger = logger;
            }

            public void Tick(float deltaTime)
            {
                _elapsed += deltaTime;
                if (_elapsed >= 5f)
                {
                    _elapsed = 0f;
                    _logger?.LogDebug("HotUpdateHeartbeat: tick");
                }
            }
        }
    }
}
