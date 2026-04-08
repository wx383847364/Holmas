using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI
{
    public static class HolmasUiBootstrap
    {
        private const string RootObjectName = "HolmasUiRoot";

        private static UiRoot _root;

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
                _root = Object.FindObjectOfType<UiRoot>();
            }

            if (_root == null)
            {
                GameObject rootObject = FindOrCreateRootObject();
                _root = rootObject.GetComponent<UiRoot>();
                if (_root == null)
                {
                    _root = rootObject.AddComponent<UiRoot>();
                }
            }

            _root.Initialize(context, levelLaunchGateway);

            HolmasUiRoot legacyShell = _root.GetComponent<HolmasUiRoot>();
            if (legacyShell == null)
            {
                legacyShell = _root.gameObject.AddComponent<HolmasUiRoot>();
            }

            legacyShell.InitializeCompatibility(context, levelLaunchGateway, _root);
        }

        private static GameObject FindOrCreateRootObject()
        {
            HolmasUiRoot legacyRoot = Object.FindObjectOfType<HolmasUiRoot>();
            if (legacyRoot != null)
            {
                Object.DontDestroyOnLoad(legacyRoot.gameObject);
                return legacyRoot.gameObject;
            }

            UiRoot existingRoot = Object.FindObjectOfType<UiRoot>();
            if (existingRoot != null)
            {
                Object.DontDestroyOnLoad(existingRoot.gameObject);
                return existingRoot.gameObject;
            }

            var rootObject = new GameObject(RootObjectName);
            Object.DontDestroyOnLoad(rootObject);
            return rootObject;
        }
    }
}
