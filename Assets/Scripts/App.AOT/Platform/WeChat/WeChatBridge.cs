using System;
using App.Shared.Contracts;

namespace App.AOT.Platform.WeChat
{
    /// <summary>
    /// 微信平台桥接：JSBridge封装（登录、广告、支付唤起、网络状态）
    /// </summary>
    public class WeChatBridge : IWeChatBridge
    {
        private IAppLogger _logger;

        public WeChatBridge()
        {
        }

        /// <summary>
        /// 设置日志器（通过DI注入）
        /// </summary>
        public void SetLogger(IAppLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _logger?.LogInfo("WeChatBridge: 初始化微信桥接...");
            // TODO: 初始化微信JSBridge
        }

        public void Update(float deltaTime)
        {
            // 不需要每帧更新
        }

        public void Shutdown()
        {
            _logger?.LogInfo("WeChatBridge: 已关闭");
        }

        /// <summary>
        /// 微信登录
        /// </summary>
        public async System.Threading.Tasks.Task<string> LoginAsync()
        {
            _logger?.LogInfo("WeChatBridge: 开始微信登录...");
            // TODO: 调用微信登录API
            await System.Threading.Tasks.Task.Delay(100);
            return "mock_code"; // 返回临时code
        }

        /// <summary>
        /// 显示激励视频广告
        /// </summary>
        public async System.Threading.Tasks.Task<bool> ShowRewardedAdAsync(string adUnitId)
        {
            _logger?.LogInfo("WeChatBridge: 显示激励视频广告: {0}", adUnitId);
            // TODO: 调用微信广告API
            await System.Threading.Tasks.Task.Delay(100);
            return true;
        }

        /// <summary>
        /// 发起支付
        /// </summary>
        public async System.Threading.Tasks.Task<bool> RequestPaymentAsync(string orderId, string paymentParams)
        {
            _logger?.LogInfo("WeChatBridge: 发起支付: {0}", orderId);
            // TODO: 调用微信支付API
            await System.Threading.Tasks.Task.Delay(100);
            return true;
        }

        /// <summary>
        /// 获取网络状态
        /// </summary>
        public NetworkState GetNetworkState()
        {
            // TODO: 从微信API获取网络状态
            return NetworkState.Unknown;
        }
    }

    public enum NetworkState
    {
        Unknown,
        None,
        Wifi,
        Mobile
    }
}
