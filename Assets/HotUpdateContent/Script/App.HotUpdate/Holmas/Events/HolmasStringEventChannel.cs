using System;
using UnityEngine;

namespace App.HotUpdate.Holmas.Events
{
    /// <summary>
    /// 字符串参数 ScriptableObject 事件通道。
    /// </summary>
    /// <remarks>
    /// 和 HolmasVoidEventChannel 类似，但 Raise 时可以携带一段字符串。
    /// 适合调试文案、简单状态提示、非核心 UI 消息等外围场景。
    ///
    /// 这个类刻意放在 HotUpdate/Holmas 侧，不放 Shared，也不被 Runtime 依赖；
    /// 这样它只是一个资源驱动事件工具，不会污染核心玩法链路。
    /// </remarks>
    [CreateAssetMenu(menuName = "Holmas/Events/String Event Channel", fileName = "HolmasStringEventChannel")]
    public sealed class HolmasStringEventChannel : ScriptableObject
    {
        private event Action<string> Raised;

        /// <summary>
        /// 发布字符串事件。
        /// </summary>
        public void Raise(string value)
        {
            Raised?.Invoke(value);
        }

        /// <summary>
        /// 添加监听。
        /// 先 -= 再 +=，确保同一个 listener 最多只注册一次。
        /// </summary>
        public void AddListener(Action<string> listener)
        {
            if (listener == null)
            {
                return;
            }

            Raised -= listener;
            Raised += listener;
        }

        /// <summary>
        /// 移除监听。
        /// </summary>
        public void RemoveListener(Action<string> listener)
        {
            if (listener == null)
            {
                return;
            }

            Raised -= listener;
        }
    }
}
