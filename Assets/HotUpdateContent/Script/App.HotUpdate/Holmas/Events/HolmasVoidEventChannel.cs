using System;
using UnityEngine;

namespace App.HotUpdate.Holmas.Events
{
    /// <summary>
    /// 无参数 ScriptableObject 事件通道。
    /// </summary>
    /// <remarks>
    /// 这是首轮非核心 SO EventChannel，用来验证“资源驱动事件”的工作流：
    /// 一个资源资产代表一个事件，发布者调用 Raise，监听者通过 listener 组件响应。
    ///
    /// 使用边界：
    /// - 适合调试按钮、简单 UI 反馈、音效/动效触发等外围场景。
    /// - 不接入 HolmasGameplayRuntime、存档同步或教程主流程，核心玩法仍使用强类型 EventBus。
    /// - Unity 的 .meta GUID 会保证资产移动或重命名时引用不丢，但前提是 .meta 文件不被删除。
    /// </remarks>
    [CreateAssetMenu(menuName = "Holmas/Events/Void Event Channel", fileName = "HolmasVoidEventChannel")]
    public sealed class HolmasVoidEventChannel : ScriptableObject
    {
        private event Action Raised;

        /// <summary>
        /// 发布无参数事件。
        /// </summary>
        public void Raise()
        {
            Raised?.Invoke();
        }

        /// <summary>
        /// 添加监听。
        /// 先 -= 再 +=，可以避免同一个 listener 被重复注册后一次 Raise 响应多次。
        /// </summary>
        public void AddListener(Action listener)
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
        public void RemoveListener(Action listener)
        {
            if (listener == null)
            {
                return;
            }

            Raised -= listener;
        }
    }
}
