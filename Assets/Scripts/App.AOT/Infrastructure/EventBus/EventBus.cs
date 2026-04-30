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
        private readonly Dictionary<Type, List<HandlerEntry>> _handlers = new Dictionary<Type, List<HandlerEntry>>();
        private long _nextOrder;

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : class
        {
            SubscribeScoped(handler);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    if (handlers[i].Matches(handler))
                    {
                        handlers[i].IsDisposed = true;
                        handlers.RemoveAt(i);
                        break;
                    }
                }

                if (handlers.Count == 0)
                {
                    _handlers.Remove(type);
                }
            }
        }

        public IEventSubscription SubscribeScoped<T>(
            Action<T> handler,
            int priority = 0,
            Predicate<T> condition = null) where T : class
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handlers))
            {
                handlers = new List<HandlerEntry>();
                _handlers[type] = handlers;
            }

            var entry = new HandlerEntry(
                handler,
                condition,
                priority,
                _nextOrder++);
            handlers.Add(entry);
            return new EventSubscription(this, type, entry);
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        public void Publish<T>(T eventData) where T : class
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                HandlerEntry[] snapshot = handlers.ToArray();
                Array.Sort(snapshot, CompareHandlers);
                foreach (var handler in snapshot)
                {
                    if (handler.IsDisposed || !ContainsHandler(type, handler))
                    {
                        continue;
                    }

                    if (handler.Condition is Predicate<T> condition)
                    {
                        bool shouldHandle;
                        try
                        {
                            shouldHandle = condition(eventData);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"EventBus: Error evaluating condition for event {type.Name}: {ex}");
                            continue;
                        }

                        if (!shouldHandle)
                        {
                            continue;
                        }
                    }

                    if (handler.Handler is Action<T> action)
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
            foreach (var pair in _handlers)
            {
                foreach (var handler in pair.Value)
                {
                    handler.IsDisposed = true;
                }
            }

            _handlers.Clear();
        }

        private static int CompareHandlers(HandlerEntry left, HandlerEntry right)
        {
            int priorityComparison = right.Priority.CompareTo(left.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : left.Order.CompareTo(right.Order);
        }

        private bool ContainsHandler(Type type, HandlerEntry entry)
        {
            return _handlers.TryGetValue(type, out var handlers) && handlers.Contains(entry);
        }

        private void Remove(Type type, HandlerEntry entry)
        {
            if (entry == null || entry.IsDisposed)
            {
                return;
            }

            entry.IsDisposed = true;
            if (_handlers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(entry);
                if (handlers.Count == 0)
                {
                    _handlers.Remove(type);
                }
            }
        }

        private sealed class HandlerEntry
        {
            public HandlerEntry(object handler, object condition, int priority, long order)
            {
                Handler = handler;
                Condition = condition;
                Priority = priority;
                Order = order;
            }

            public object Handler { get; }

            public object Condition { get; }

            public int Priority { get; }

            public long Order { get; }

            public bool IsDisposed { get; set; }

            public bool Matches(object handler)
            {
                return ReferenceEquals(Handler, handler) || Equals(Handler, handler);
            }
        }

        private sealed class EventSubscription : IEventSubscription
        {
            private readonly EventBus _eventBus;
            private readonly Type _type;
            private readonly HandlerEntry _entry;
            private bool _disposed;

            public EventSubscription(EventBus eventBus, Type type, HandlerEntry entry)
            {
                _eventBus = eventBus;
                _type = type;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _eventBus.Remove(_type, _entry);
            }
        }
    }
}
