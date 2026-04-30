using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using App.AOT.Infrastructure.EventBus;
using App.HotUpdate.Holmas.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Holmas.Tests
{
    public sealed class HolmasEventSystemTests
    {
        [Test]
        public void EventBus_PublishesByPriorityThenSubscribeOrder()
        {
            var eventBus = new EventBus();
            var calls = new List<string>();

            eventBus.SubscribeScoped<TestEvent>(_ => calls.Add("normal-a"));
            eventBus.SubscribeScoped<TestEvent>(_ => calls.Add("high"), priority: 10);
            eventBus.SubscribeScoped<TestEvent>(_ => calls.Add("normal-b"));

            eventBus.Publish(new TestEvent());

            CollectionAssert.AreEqual(new[] { "high", "normal-a", "normal-b" }, calls);
        }

        [Test]
        public void EventBus_ConditionFalseSkipsHandler()
        {
            var eventBus = new EventBus();
            int calls = 0;

            eventBus.SubscribeScoped<TestEvent>(_ => calls++, condition: _ => false);

            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void EventBus_ConditionExceptionIsIsolated()
        {
            var eventBus = new EventBus();
            int calls = 0;
            LogAssert.Expect(LogType.Error, new Regex("EventBus: Error evaluating condition for event TestEvent"));

            eventBus.SubscribeScoped<TestEvent>(_ => calls++, condition: _ => throw new InvalidOperationException("condition failed"));

            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void EventBus_HandlerExceptionDoesNotBlockLaterHandlers()
        {
            var eventBus = new EventBus();
            int calls = 0;
            LogAssert.Expect(LogType.Error, new Regex("EventBus: Error handling event TestEvent"));

            eventBus.SubscribeScoped<TestEvent>(_ => throw new InvalidOperationException("handler failed"));
            eventBus.SubscribeScoped<TestEvent>(_ => calls++);

            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void EventBus_DisposeIsIdempotentAndStopsFuturePublishes()
        {
            var eventBus = new EventBus();
            int calls = 0;

            var subscription = eventBus.SubscribeScoped<TestEvent>(_ => calls++);
            subscription.Dispose();
            subscription.Dispose();

            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void EventBus_LegacyUnsubscribeRemovesOneDuplicateHandler()
        {
            var eventBus = new EventBus();
            int calls = 0;
            Action<TestEvent> handler = _ => calls++;

            eventBus.Subscribe(handler);
            eventBus.Subscribe(handler);
            eventBus.Unsubscribe(handler);

            eventBus.Publish(new TestEvent());

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void EventBus_HandlerCanUnsubscribeAnotherHandlerDuringPublish()
        {
            var eventBus = new EventBus();
            var calls = new List<string>();
            App.Shared.Contracts.IEventSubscription second = null;

            eventBus.SubscribeScoped<TestEvent>(
                _ =>
                {
                    calls.Add("first");
                    second.Dispose();
                },
                priority: 10);
            second = eventBus.SubscribeScoped<TestEvent>(_ => calls.Add("second"));

            eventBus.Publish(new TestEvent());

            CollectionAssert.AreEqual(new[] { "first" }, calls);
        }

        [Test]
        public void VoidEventChannelListener_SubscribeLifecycleDoesNotDuplicate()
        {
            var channel = ScriptableObject.CreateInstance<HolmasVoidEventChannel>();
            var go = new GameObject("VoidEventChannelListenerTest");
            int calls = 0;

            try
            {
                var listener = go.AddComponent<HolmasVoidEventChannelListener>();
                listener.Configure(channel, () => calls++);

                channel.Raise();
                listener.StopListening();
                channel.Raise();
                listener.StartListening();
                listener.StopListening();
                listener.StartListening();
                channel.Raise();

                Assert.That(calls, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(channel);
            }
        }

        [Test]
        public void StringEventChannelListener_SubscribeLifecycleDoesNotDuplicate()
        {
            var channel = ScriptableObject.CreateInstance<HolmasStringEventChannel>();
            var go = new GameObject("StringEventChannelListenerTest");
            var values = new List<string>();

            try
            {
                var listener = go.AddComponent<HolmasStringEventChannelListener>();
                listener.Configure(channel, value => values.Add(value));

                channel.Raise("first");
                listener.StopListening();
                channel.Raise("disabled");
                listener.StartListening();
                listener.StopListening();
                listener.StartListening();
                channel.Raise("second");

                CollectionAssert.AreEqual(new[] { "first", "second" }, values);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(channel);
            }
        }

        private sealed class TestEvent
        {
        }
    }
}
