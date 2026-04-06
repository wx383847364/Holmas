using System;
using System.Collections.Generic;
using System.Linq;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Core.Schema;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UiPrefabGenerator.Editor.ResourceMatch
{
    public sealed class UiPrefabResourceBinder
    {
        public void Apply(
            string prefabPath,
            PrefabBindingManifest manifest,
            UiResourceMatchReport report,
            UiGenerationExecutionResult executionResult)
        {
            if (string.IsNullOrWhiteSpace(prefabPath) || manifest == null || executionResult == null)
            {
                return;
            }

            List<PrefabBindingEntry> entriesWithSlots = manifest.Entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.AssetSlot))
                .ToList();
            if (entriesWithSlots.Count == 0)
            {
                return;
            }

            var matchBySlot = new Dictionary<string, UiAssetSlotMatch>(StringComparer.Ordinal);
            if (report != null && report.Matches != null)
            {
                for (int i = 0; i < report.Matches.Count; i++)
                {
                    UiAssetSlotMatch match = report.Matches[i];
                    if (match != null && !string.IsNullOrWhiteSpace(match.AssetSlot) && !matchBySlot.ContainsKey(match.AssetSlot))
                    {
                        matchBySlot[match.AssetSlot] = match;
                    }
                }
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            try
            {
                for (int i = 0; i < entriesWithSlots.Count; i++)
                {
                    PrefabBindingEntry entry = entriesWithSlots[i];
                    UiAssetSlotMatch match;
                    if (!matchBySlot.TryGetValue(entry.AssetSlot, out match) || string.IsNullOrWhiteSpace(match.SelectedAssetPath))
                    {
                        AddUnique(executionResult.UnmatchedAssetSlots, entry.AssetSlot);
                        continue;
                    }

                    Transform target = FindNode(prefabRoot.transform, entry.NodePath);
                    if (target == null)
                    {
                        executionResult.Warnings.Add("资源绑定找不到节点路径: " + entry.NodePath);
                        AddUnique(executionResult.UnmatchedAssetSlots, entry.AssetSlot);
                        continue;
                    }

                    if (TryApplyAsset(target.gameObject, entry.ComponentType, match.SelectedAssetPath, executionResult))
                    {
                        changed = true;
                        AddUnique(executionResult.AutoBoundAssets, entry.AssetSlot + " -> " + match.SelectedAssetPath);
                    }
                    else
                    {
                        AddUnique(executionResult.UnmatchedAssetSlots, entry.AssetSlot);
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool TryApplyAsset(GameObject target, string componentType, string assetPath, UiGenerationExecutionResult result)
        {
            if (string.Equals(componentType, "Image", StringComparison.Ordinal))
            {
                var image = target.GetComponent<Image>();
                if (image == null)
                {
                    result.Warnings.Add("节点缺少 Image 组件，无法绑定资源: " + target.name);
                    return false;
                }

                Sprite sprite = LoadSprite(assetPath);
                if (sprite == null)
                {
                    result.Warnings.Add("无法加载 Sprite 资源: " + assetPath);
                    return false;
                }

                image.sprite = sprite;
                return true;
            }

            if (string.Equals(componentType, "RawImage", StringComparison.Ordinal))
            {
                var rawImage = target.GetComponent<RawImage>();
                if (rawImage == null)
                {
                    result.Warnings.Add("节点缺少 RawImage 组件，无法绑定资源: " + target.name);
                    return false;
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                {
                    result.Warnings.Add("无法加载 Texture2D 资源: " + assetPath);
                    return false;
                }

                rawImage.texture = texture;
                return true;
            }

            result.Warnings.Add("当前资源绑定暂不支持组件类型: " + componentType);
            return false;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                return sprite;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                sprite = assets[i] as Sprite;
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static Transform FindNode(Transform root, string nodePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(nodePath))
            {
                return null;
            }

            string[] segments = nodePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            Transform current = root;
            int startIndex = string.Equals(root.name, segments[0], StringComparison.Ordinal) ? 1 : 0;
            for (int i = startIndex; i < segments.Length; i++)
            {
                current = current.Find(segments[i]);
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static void AddUnique(List<string> values, string item)
        {
            if (values == null || string.IsNullOrWhiteSpace(item) || values.Contains(item))
            {
                return;
            }

            values.Add(item);
        }
    }
}
