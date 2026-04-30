using System;
using UnityEngine;

namespace App.HotUpdate.Holmas.Events
{
    [CreateAssetMenu(menuName = "Holmas/Events/Void Event Channel", fileName = "HolmasVoidEventChannel")]
    public sealed class HolmasVoidEventChannel : ScriptableObject
    {
        private event Action Raised;

        public void Raise()
        {
            Raised?.Invoke();
        }

        public void AddListener(Action listener)
        {
            if (listener == null)
            {
                return;
            }

            Raised -= listener;
            Raised += listener;
        }

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
