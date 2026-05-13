using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.Main;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Holmas.Editor
{
    public static class MainPanelStaticTaskTextAuthoring
    {
        [MenuItem("Holmas/UI/Refresh MainPanel Static Task Texts")]
        public static void Refresh()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MainGeneratedBindings.PrefabAssetPath);
            if (root == null)
            {
                throw new System.InvalidOperationException("MainPanel prefab 缺失：" + MainGeneratedBindings.PrefabAssetPath);
            }

            try
            {
                UiReferenceCollector collector = root.GetComponent<UiReferenceCollector>() ?? root.AddComponent<UiReferenceCollector>();
                for (int i = 0; i < MainBindings.TaskSlotCount; i++)
                {
                    string slotPath = $"BackgroundImage/TaskGroup/Task{i + 1}";
                    Transform slot = root.transform.Find(slotPath);
                    if (slot == null)
                    {
                        throw new System.InvalidOperationException("MainPanel task slot 缺失：" + slotPath);
                    }

                    RemoveLegacyRuntimeText(slot, "RuntimeTaskTitle");
                    RemoveLegacyRuntimeText(slot, "RuntimeTaskReward");

                    TextMeshProUGUI title = EnsureTaskText(
                        slot,
                        "TaskTitle",
                        new Vector2(0.08f, 1f),
                        new Vector2(0.92f, 1f),
                        new Vector2(0.5f, 1f),
                        new Vector2(0f, -10f),
                        new Vector2(0f, 34f),
                        18f,
                        "任务");
                    TextMeshProUGUI reward = EnsureTaskText(
                        slot,
                        "TaskReward",
                        new Vector2(0.08f, 0f),
                        new Vector2(0.92f, 0f),
                        new Vector2(0.5f, 0f),
                        new Vector2(0f, 14f),
                        new Vector2(0f, 34f),
                        16f,
                        "奖励");

                    collector.RegisterOrReplace(
                        MainBindings.TaskTitleTextKeys[i],
                        title,
                        nodePath: MainBindings.TaskTitleTextNodePaths[i]);
                    collector.RegisterOrReplace(
                        MainBindings.TaskRewardTextKeys[i],
                        reward,
                        nodePath: MainBindings.TaskRewardTextNodePaths[i]);
                }

                PrefabUtility.SaveAsPrefabAsset(root, MainGeneratedBindings.PrefabAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("MainPanel static task texts refreshed: " + MainGeneratedBindings.PrefabAssetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void RefreshForBatchMode()
        {
            Refresh();
        }

        private static TextMeshProUGUI EnsureTaskText(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            string previewText)
        {
            Transform existing = parent.Find(objectName);
            GameObject textObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (existing == null)
            {
                textObject.transform.SetParent(parent, false);
            }

            RectTransform rect = textObject.GetComponent<RectTransform>() ?? textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>() ?? textObject.AddComponent<TextMeshProUGUI>();
            text.text = previewText;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static void RemoveLegacyRuntimeText(Transform parent, string objectName)
        {
            Transform legacy = parent.Find(objectName);
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy.gameObject);
            }
        }
    }
}
