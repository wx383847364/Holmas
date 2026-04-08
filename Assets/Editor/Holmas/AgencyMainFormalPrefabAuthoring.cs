using System.IO;
using App.HotUpdate.Holmas.UI.Binding;
using App.HotUpdate.Holmas.UI.Core;
using App.HotUpdate.Holmas.UI.Generated;
using App.HotUpdate.Holmas.UI.Screens.AgencyMain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Holmas.Editor
{
    public static class AgencyMainFormalPrefabAuthoring
    {
        [MenuItem("Holmas/UI/Regenerate AgencyMain Formal Prefab")]
        public static void Generate()
        {
            string assetPath = AgencyMainGeneratedBindings.PrefabAssetPath;
            EnsureAssetFolders(assetPath);

            GameObject root = BuildPrefabRoot();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("AgencyMain formal prefab regenerated: " + assetPath);
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        public static void GenerateForBatchMode()
        {
            Generate();
        }

        public static void GenerateAndValidateForBatchMode()
        {
            Generate();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AgencyMainGeneratedBindings.PrefabAssetPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("AgencyMain 正式 prefab 资源缺失。");
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                UiReferenceCollector collector = instance.GetComponent<UiReferenceCollector>();
                if (collector == null)
                {
                    throw new System.InvalidOperationException("AgencyMain prefab 缺少 UiReferenceCollector。");
                }

                var resolver = new UiBindingResolver(collector, AgencyMainGeneratedBindings.Manifest);
                AgencyMainBindings bindings = AgencyMainBindings.Resolve(resolver);
                if (!bindings.HasRequiredBindings)
                {
                    throw new System.InvalidOperationException("AgencyMainBindings 未能完整解析正式 binding。");
                }

                UiSafeAreaFitter safeAreaFitter = instance.transform.Find(AgencyMainBindings.ContentNodeName)?.GetComponent<UiSafeAreaFitter>();
                if (safeAreaFitter == null)
                {
                    throw new System.InvalidOperationException("AgencyMain prefab 缺少 UiSafeAreaFitter。");
                }
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }

            Debug.Log("AgencyMain formal prefab validation passed.");
        }

        private static GameObject BuildPrefabRoot()
        {
            var root = new GameObject(AgencyMainGeneratedBindings.PrefabName, typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            var background = root.AddComponent<Image>();
            background.color = new Color(0.09f, 0.12f, 0.16f, 0.95f);

            GameObject contentRoot = new GameObject(AgencyMainBindings.ContentNodeName, typeof(RectTransform));
            contentRoot.transform.SetParent(root.transform, false);

            RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
            Stretch(contentRect);
            contentRoot.AddComponent<UiSafeAreaFitter>();

            var layout = contentRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 96, 48);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            root.AddComponent<AgencyMainView>();
            UiReferenceCollector collector = root.AddComponent<UiReferenceCollector>();
            collector.RegisterOrReplace(AgencyMainBindings.RootPanelKey, rootRect, nodePath: AgencyMainBindings.RootNodePath);

            Text titleText = CreateText(contentRoot.transform, "TitleText", "AgencyMain", 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            collector.RegisterOrReplace(AgencyMainBindings.TitleTextKey, titleText, nodePath: AgencyMainBindings.TitleTextNodePath);

            Text summaryText = CreateText(contentRoot.transform, "SummaryText", "AgencyMain summary", 28, FontStyle.Normal, TextAnchor.UpperLeft);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.SummaryTextKey, summaryText, nodePath: AgencyMainBindings.SummaryTextNodePath);

            Text taskSummaryText = CreateText(contentRoot.transform, "TaskSummaryText", "Task summary", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            taskSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.TaskSummaryTextKey, taskSummaryText, nodePath: AgencyMainBindings.TaskSummaryTextNodePath);

            Text boardSummaryText = CreateText(contentRoot.transform, "BoardSummaryText", "Board summary", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            boardSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            boardSummaryText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.BoardSummaryTextKey, boardSummaryText, nodePath: AgencyMainBindings.BoardSummaryTextNodePath);

            Text statusText = CreateText(contentRoot.transform, "StatusText", "Status", 24, FontStyle.Italic, TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            collector.RegisterOrReplace(AgencyMainBindings.StatusTextKey, statusText, nodePath: AgencyMainBindings.StatusTextNodePath);

            Button primaryActionButton = CreateButton(contentRoot.transform, "PrimaryActionButton", "Open Level");
            collector.RegisterOrReplace(
                AgencyMainBindings.PrimaryActionButtonKey,
                primaryActionButton,
                AgencyMainBindings.PrimaryActionButtonClickEvent,
                AgencyMainBindings.PrimaryActionButtonNodePath);

            return root;
        }

        private static Text CreateText(Transform parent, string objectName, string textValue, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, fontSize * 1.8f);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = textValue;
            return text;
        }

        private static Button CreateButton(Transform parent, string objectName, string textValue)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 96f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.23f, 0.45f, 0.75f, 1f);

            var button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.28f, 0.50f, 0.82f, 1f);
            colors.pressedColor = new Color(0.15f, 0.31f, 0.58f, 1f);
            button.colors = colors;

            Text label = CreateText(buttonObject.transform, objectName + "_Label", textValue, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        private static void EnsureAssetFolders(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
    }
}
