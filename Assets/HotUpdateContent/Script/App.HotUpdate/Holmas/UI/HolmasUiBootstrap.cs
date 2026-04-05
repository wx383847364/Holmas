using App.HotUpdate.Holmas.Application;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI
{
    public static class HolmasUiBootstrap
    {
        private static HolmasUiRoot _root;

        public static void EnsureCreated(HolmasApplicationContext context, IHolmasLevelLaunchGateway levelLaunchGateway)
        {
            if (context == null || levelLaunchGateway == null)
            {
                return;
            }

            if (!UnityEngine.Application.isPlaying || UnityEngine.Application.isBatchMode)
            {
                return;
            }

            if (_root == null)
            {
                _root = Object.FindObjectOfType<HolmasUiRoot>();
            }

            if (_root == null)
            {
                var rootObject = new GameObject("HolmasUiRoot");
                Object.DontDestroyOnLoad(rootObject);
                _root = rootObject.AddComponent<HolmasUiRoot>();
            }

            _root.Initialize(context, levelLaunchGateway);
        }
    }
}
