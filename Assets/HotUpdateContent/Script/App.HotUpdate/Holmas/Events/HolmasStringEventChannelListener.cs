using System;
using UnityEngine;
using UnityEngine.Events;

namespace App.HotUpdate.Holmas.Events
{
    /// <summary>
    /// 字符串事件通道监听组件。
    /// </summary>
    /// <remarks>
    /// 这个组件监听 HolmasStringEventChannel，并把字符串参数继续传给 UnityEvent&lt;string&gt;。
    /// 它适合非核心 UI 文案、调试提示、简单状态广播，不用于核心玩法数据同步。
    ///
    /// 生命周期与 HolmasVoidEventChannelListener 一致：
    /// OnEnable 开始监听，OnDisable 停止监听，StartListening / StopListening 可被测试或代码显式调用。
    /// </remarks>
    public sealed class HolmasStringEventChannelListener : MonoBehaviour
    {
        /// <summary>
        /// UnityEvent 的可序列化字符串版本，让 Inspector 可以绑定带 string 参数的响应。
        /// </summary>
        [Serializable]
        public sealed class StringResponse : UnityEvent<string>
        {
        }

        /// <summary>
        /// 要监听的字符串事件资产。
        /// </summary>
        [SerializeField] private HolmasStringEventChannel channel;

        /// <summary>
        /// 事件触发后的响应，参数就是 Raise(value) 传入的 value。
        /// </summary>
        [SerializeField] private StringResponse response = new StringResponse();
        private bool _subscribed;

        private void OnEnable()
        {
            StartListening();
        }

        private void OnDisable()
        {
            StopListening();
        }

        public void Configure(HolmasStringEventChannel eventChannel, UnityAction<string> listener)
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
        /// SO 事件被 Raise 后进入这里，再把字符串参数转发给 UnityEvent。
        /// </summary>
        private void OnRaised(string value)
        {
            response?.Invoke(value);
        }
    }
}
