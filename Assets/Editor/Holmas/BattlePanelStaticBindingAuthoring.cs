using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.Battle;
using UnityEditor;
using UnityEngine;

namespace Holmas.Editor
{
    public static class BattlePanelStaticBindingAuthoring
    {
        [MenuItem("Holmas/UI/Refresh BattlePanel Static Bindings")]
        public static void Refresh()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleGeneratedBindings.PrefabAssetPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("BattlePanel prefab 缺失：" + BattleGeneratedBindings.PrefabAssetPath);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new System.InvalidOperationException("BattlePanel prefab 实例化失败。");
            }

            try
            {
                BattleView view = instance.GetComponent<BattleView>();
                if (view == null)
                {
                    view = instance.AddComponent<BattleView>();
                }

                view.EnsureBindingSurface();

                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                if (collector == null)
                {
                    throw new System.InvalidOperationException("BattlePanel 静态 binding surface 生成失败：缺少 UiReferenceCollector。");
                }

                var resolver = new UiBindingResolver(collector, BattleGeneratedBindings.Manifest);
                BattleBindings bindings = BattleBindings.Resolve(resolver);
                if (!bindings.HasRequiredBindings)
                {
                    throw new System.InvalidOperationException("BattlePanel 静态 binding surface 生成失败：BattleBindings 无法完整解析。");
                }

                PrefabUtility.SaveAsPrefabAsset(instance, BattleGeneratedBindings.PrefabAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("BattlePanel static bindings refreshed: " + BattleGeneratedBindings.PrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static void RefreshForBatchMode()
        {
            Refresh();
        }
    }
}
