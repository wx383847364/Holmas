using App.HotUpdate.Holmas.Application;
using App.HotUpdate.Holmas.UI.Core;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI
{
    /// <summary>
    /// Holmas UI 启动入口。
    /// 负责确保运行时只有一个常驻 UiRoot，并把业务上下文接进去。
    /// </summary>
    public static class HolmasUiBootstrap
    {
        private const string RootObjectName = "HolmasUiRoot";

        private static UiRoot _root;

        public static void EnsureCreated(HolmasApplicationContext context, IHolmasLevelLaunchGateway levelLaunchGateway)
        {
            // UI Root 必须依赖已建立好的业务上下文和关卡启动门面。
            if (context == null || levelLaunchGateway == null)
            {
                return;
            }

            // 编辑器非运行态或 batchmode 下不创建真实运行时 UI。
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
                // 场景里没有预摆真正的 UiRoot 时，会在运行时动态创建。
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
            // 兼容旧入口：如果场景里还残留旧 HolmasUiRoot，就复用那个对象并挂上新 UiRoot。
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

            // 最后兜底：动态新建一个常驻 UI 根物体。
            var rootObject = new GameObject(RootObjectName);
            Object.DontDestroyOnLoad(rootObject);
            return rootObject;
        }
    }
}
