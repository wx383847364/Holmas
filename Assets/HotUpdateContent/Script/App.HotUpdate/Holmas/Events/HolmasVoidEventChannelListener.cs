using System;
using UnityEngine;
using UnityEngine.Events;

namespace App.HotUpdate.Holmas.Events
{
    /// <summary>
    /// 无参数事件通道监听组件。
    /// </summary>
    /// <remarks>
    /// 把这个组件挂到 GameObject 上，并在 Inspector 里指定 HolmasVoidEventChannel，
    /// 就可以在对应事件 Raise 时触发 UnityEvent response。
    ///
    /// 生命周期约定：
    /// - OnEnable 时开始监听，OnDisable 时停止监听。
    /// - StartListening / StopListening 也公开给测试或代码手动控制。
    /// - _subscribed 用于防止重复启停时重复注册。
    ///
    /// 注意：
    /// Configure 会清空 response 并换成代码传入的 listener，主要服务测试和程序化配置；
    /// 如果未来要大量使用 Inspector 持久监听，不应在运行中随意调用 Configure。
    /// </remarks>
    public sealed class HolmasVoidEventChannelListener : MonoBehaviour
    {
        /// <summary>
        /// 要监听的 SO 事件资产。
        /// </summary>
        [SerializeField] private HolmasVoidEventChannel channel;

        /// <summary>
        /// 事件触发后的 UnityEvent 响应，可在 Inspector 里绑定音效、动画或简单 UI 行为。
        /// </summary>
        [SerializeField] private UnityEvent response = new UnityEvent();
        private bool _subscribed;

        private void OnEnable()
        {
            StartListening();
        }

        private void OnDisable()
        {
            StopListening();
        }

        public void Configure(HolmasVoidEventChannel eventChannel, UnityAction listener)
        {
            StopListening();
            channel = eventChannel;
            response.RemoveAllListeners();
            if (listener != null)
            {
                response.AddListener(listener);
            }

            if (isActiveAndEnabled)
            {
                StartListening();
            }
        }

        /// <summary>
        /// 开始监听当前 channel。
        /// 已监听时重复调用不会重复注册。
        /// </summary>
        public void StartListening()
        {
            if (_subscribed || channel == null)
            {
                return;
            }

            channel.AddListener(OnRaised);
            _subscribed = true;
        }

        /// <summary>
        /// 停止监听当前 channel。
        /// 未监听时重复调用也安全。
        /// </summary>
        public void StopListening()
        {
            if (!_subscribed || channel == null)
            {
                _subscribed = false;
                return;
            }

            channel.RemoveListener(OnRaised);
            _subscribed = false;
        }

        /// <summary>
        /// SO 事件被 Raise 后进入这里，再转发给 UnityEvent。
        /// </summary>
        private void OnRaised()
        {
            response?.Invoke();
        }
    }
}
