using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Editor.Generation
{
    public interface IUnityPrefabDraftWriter
    {
        UiPrefabDraftWriteResult WriteDraft(UiPrefabDraftWriteRequest request);
    }

    [Serializable]
    public sealed class UiPrefabDraftWriteRequest
    {
        public UiPrefabSpec Spec;
        public IProjectUiProfile Profile;
        public string PrefabDraftPath = string.Empty;
    }

    [Serializable]
    public sealed class UiPrefabDraftWriteResult
    {
        public bool Success;
        public string PrefabDraftPath = string.Empty;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    public sealed class DefaultUnityPrefabDraftWriter : IUnityPrefabDraftWriter
    {
        public UiPrefabDraftWriteResult WriteDraft(UiPrefabDraftWriteRequest request)
        {
            var result = new UiPrefabDraftWriteResult();
            if (request == null)
            {
                result.Errors.Add("UiPrefabDraftWriteRequest 不能为空。");
                return result;
            }

            if (request.Spec == null)
            {
                result.Errors.Add("UiPrefabDraftWriteRequest.Spec 不能为空。");
                return result;
            }

            if (request.Profile == null)
            {
                result.Errors.Add("UiPrefabDraftWriteRequest.Profile 不能为空。");
                return result;
            }

            string prefabDraftPath = request.PrefabDraftPath;
            if (string.IsNullOrWhiteSpace(prefabDraftPath))
            {
                prefabDraftPath = NormalizeAssetPath(request.Profile.DraftPrefabRoot) + "/" + request.Spec.PrefabName + ".prefab";
            }

            if (string.IsNullOrWhiteSpace(prefabDraftPath))
            {
                result.Errors.Add("PrefabDraftPath 不能为空。");
                return result;
            }

            if (!IsPathWithinProfileRoot(prefabDraftPath, request.Profile.DraftPrefabRoot))
            {
                result.Errors.Add(string.Format("PrefabDraftPath 不在 profile 允许目录内: {0}。", prefabDraftPath));
                return result;
            }

            GameObject rootObject = BuildPrefabHierarchy(request.Spec, result);
            if (rootObject == null)
            {
                if (result.Errors.Count == 0)
                {
                    result.Errors.Add("无法构建 prefab 草稿根节点。");
                }

                return result;
            }

            if (result.Errors.Count > 0)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                return result;
            }

            try
            {
                EnsureAssetFolderExists(prefabDraftPath);
                DeleteExistingPrefabAsset(prefabDraftPath);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootObject, prefabDraftPath);
                if (savedPrefab == null)
                {
                    result.Errors.Add("PrefabUtility.SaveAsPrefabAsset 未能写出 prefab 草稿。");
                    return result;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                result.PrefabDraftPath = prefabDraftPath;
                result.Success = true;
                return result;
            }
            finally
            {
                if (rootObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(rootObject);
                }
            }
        }

        private static GameObject BuildPrefabHierarchy(UiPrefabSpec spec, UiPrefabDraftWriteResult result)
        {
            if (spec.Nodes == null || spec.Nodes.Count == 0)
            {
                result.Errors.Add("UiPrefabSpec.Nodes 不能为空。");
                return null;
            }

            var nodeById = new Dictionary<string, UiNodeSpec>(StringComparer.Ordinal);
            var objectByNodeId = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    result.Errors.Add(string.Format("UiPrefabSpec.Nodes[{0}] 无效。", i));
                    continue;
                }

                if (!nodeById.ContainsKey(node.NodeId))
                {
                    nodeById[node.NodeId] = node;
                }
            }

            foreach (KeyValuePair<string, UiNodeSpec> pair in nodeById)
            {
                GameObject nodeObject = new GameObject(pair.Value.NodeName ?? string.Empty, typeof(RectTransform));

                objectByNodeId[pair.Key] = nodeObject;
            }

            GameObject rootObject = null;
            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                GameObject nodeObject;
                if (!objectByNodeId.TryGetValue(node.NodeId, out nodeObject))
                {
                    continue;
                }

                if (string.Equals(node.NodeId, spec.RootNodeId, StringComparison.Ordinal))
                {
                    rootObject = nodeObject;
                    continue;
                }

                GameObject parentObject;
                if (!objectByNodeId.TryGetValue(node.ParentNodeId ?? string.Empty, out parentObject))
                {
                    result.Errors.Add(string.Format("节点 {0} 找不到父节点 {1}。", node.NodeId, node.ParentNodeId ?? string.Empty));
                    UnityEngine.Object.DestroyImmediate(nodeObject);
                    continue;
                }

                nodeObject.transform.SetParent(parentObject.transform, false);
            }

            if (rootObject == null && spec.Nodes.Count > 0)
            {
                UiNodeSpec rootNode = spec.Nodes[0];
                if (rootNode != null && objectByNodeId.TryGetValue(rootNode.NodeId, out rootObject))
                {
                    result.Warnings.Add("RootNodeId 未匹配，已回退使用首个节点作为根节点。");
                }
            }

            if (rootObject == null)
            {
                return null;
            }

            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                GameObject nodeObject;
                if (!objectByNodeId.TryGetValue(node.NodeId, out nodeObject))
                {
                    continue;
                }

                AttachComponents(nodeObject, node, result);
            }

            return rootObject;
        }

        private static void AttachComponents(GameObject nodeObject, UiNodeSpec node, UiPrefabDraftWriteResult result)
        {
            if (nodeObject == null || node == null || node.Components == null)
            {
                return;
            }

            var addedComponents = new List<Component>();
            for (int i = 0; i < node.Components.Count; i++)
            {
                UiComponentSpec component = node.Components[i];
                if (component == null || string.IsNullOrWhiteSpace(component.ComponentType))
                {
                    continue;
                }

                Component created = AddComponent(nodeObject, component.ComponentType);
                if (created == null)
                {
                    result.Errors.Add(string.Format("节点 {0} 无法创建组件 {1}。", node.NodeId, component.ComponentType));
                    continue;
                }

                addedComponents.Add(created);
                if (created is Image image && !string.IsNullOrWhiteSpace(component.AssetSlot))
                {
                    image.raycastTarget = true;
                }
            }

            Image firstImage = nodeObject.GetComponent<Image>();
            Button button = nodeObject.GetComponent<Button>();
            if (button != null && button.targetGraphic == null && firstImage != null)
            {
                button.targetGraphic = firstImage;
            }

            Toggle toggle = nodeObject.GetComponent<Toggle>();
            if (toggle != null && toggle.graphic == null && firstImage != null)
            {
                toggle.graphic = firstImage;
            }

            if (addedComponents.Count == 0)
            {
                result.Warnings.Add(string.Format("节点 {0} 没有可写入的组件。", node.NodeId));
            }
        }

        private static Component AddComponent(GameObject nodeObject, string componentType)
        {
            if (string.Equals(componentType, "RectTransform", StringComparison.Ordinal))
            {
                return nodeObject.GetComponent<RectTransform>() ?? nodeObject.AddComponent<RectTransform>();
            }

            if (string.Equals(componentType, "CanvasGroup", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<CanvasGroup>();
            }

            if (string.Equals(componentType, "Image", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Image>();
            }

            if (string.Equals(componentType, "RawImage", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<RawImage>();
            }

            if (string.Equals(componentType, "Text", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Text>();
            }

            if (string.Equals(componentType, "Button", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Button>();
            }

            if (string.Equals(componentType, "Toggle", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Toggle>();
            }

            if (string.Equals(componentType, "Slider", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Slider>();
            }

            if (string.Equals(componentType, "Scrollbar", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Scrollbar>();
            }

            if (string.Equals(componentType, "ScrollRect", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<ScrollRect>();
            }

            if (string.Equals(componentType, "InputField", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<InputField>();
            }

            if (string.Equals(componentType, "Mask", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<Mask>();
            }

            if (string.Equals(componentType, "RectMask2D", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<RectMask2D>();
            }

            if (string.Equals(componentType, "HorizontalLayoutGroup", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<HorizontalLayoutGroup>();
            }

            if (string.Equals(componentType, "VerticalLayoutGroup", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<VerticalLayoutGroup>();
            }

            if (string.Equals(componentType, "GridLayoutGroup", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<GridLayoutGroup>();
            }

            if (string.Equals(componentType, "LayoutElement", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<LayoutElement>();
            }

            if (string.Equals(componentType, "ContentSizeFitter", StringComparison.Ordinal))
            {
                return nodeObject.AddComponent<ContentSizeFitter>();
            }

            return null;
        }

        private static void EnsureAssetFolderExists(string prefabDraftPath)
        {
            string directory = Path.GetDirectoryName(prefabDraftPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(directory))
            {
                string[] segments = directory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string currentPath = segments.Length > 0 && string.Equals(segments[0], "Assets", StringComparison.Ordinal)
                    ? "Assets"
                    : string.Empty;

                int startIndex = string.Equals(currentPath, "Assets", StringComparison.Ordinal) ? 1 : 0;
                for (int i = startIndex; i < segments.Length; i++)
                {
                    string nextPath = string.IsNullOrEmpty(currentPath) ? segments[i] : currentPath + "/" + segments[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        string parent = string.IsNullOrEmpty(currentPath) ? "Assets" : currentPath;
                        AssetDatabase.CreateFolder(parent, segments[i]);
                    }

                    currentPath = nextPath;
                }
            }
        }

        private static void DeleteExistingPrefabAsset(string prefabDraftPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabDraftPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabDraftPath);
            }
        }

        private static bool IsPathWithinProfileRoot(string prefabDraftPath, string profileRoot)
        {
            string normalizedDraftPath = NormalizeAssetPath(prefabDraftPath);
            string normalizedRoot = NormalizeAssetPath(profileRoot);
            if (string.IsNullOrWhiteSpace(normalizedDraftPath) || string.IsNullOrWhiteSpace(normalizedRoot))
            {
                return false;
            }

            if (!normalizedDraftPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                return false;
            }

            if (normalizedDraftPath.Length == normalizedRoot.Length)
            {
                return true;
            }

            return normalizedDraftPath[normalizedRoot.Length] == '/';
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
