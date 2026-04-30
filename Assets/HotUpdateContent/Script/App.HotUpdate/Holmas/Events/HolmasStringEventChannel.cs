using System;
using UnityEngine;

namespace App.HotUpdate.Holmas.Events
{
    [CreateAssetMenu(menuName = "Holmas/Events/String Event Channel", fileName = "HolmasStringEventChannel")]
    public sealed class HolmasStringEventChannel : ScriptableObject
    {
        private event Action<string> Raised;

        public void Raise(string value)
        {
            Raised?.Invoke(value);
        }

        public void AddListener(Action<string> listener)
        {
            if (listener == null)
            {
                return;
            }

            Raised -= listener;
            Raised += listener;
        }

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
