using System;
using UnityEngine;
using UnityEngine.Events;

namespace App.HotUpdate.Holmas.Events
{
    public sealed class HolmasVoidEventChannelListener : MonoBehaviour
    {
        [SerializeField] private HolmasVoidEventChannel channel;
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

        public void StartListening()
        {
            if (_subscribed || channel == null)
            {
                return;
            }

            channel.AddListener(OnRaised);
            _subscribed = true;
        }

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

        private void OnRaised()
        {
            response?.Invoke();
        }
    }
}
