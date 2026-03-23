using System;
using System.Collections.Generic;
using App.Shared.Contracts;

namespace App.AOT.Infrastructure.EventBus
{
    /// <summary>
    /// 事件总线：支持强类型事件
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new Dictionary<Type, List<object>>();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : class
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
            {
                _handlers[type] = new List<object>();
            }

            _handlers[type].Add(handler);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlers.Remove(type);
                }
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        public void Publish<T>(T eventData) where T : class
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    if (handler is Action<T> action)
                    {
                        try
                        {
                            action(eventData);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"EventBus: Error handling event {type.Name}: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有订阅
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
