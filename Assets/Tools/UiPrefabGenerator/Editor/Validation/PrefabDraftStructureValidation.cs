using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Editor.Validation
{
    public interface IPrefabDraftStructureValidator
    {
        UiPrefabValidationResult Validate(string prefabDraftPath, UiPrefabSpec spec);
    }

    public sealed class DefaultPrefabDraftStructureValidator : IPrefabDraftStructureValidator
    {
        public UiPrefabValidationResult Validate(string prefabDraftPath, UiPrefabSpec spec)
        {
            var result = new UiPrefabValidationResult();
            if (string.IsNullOrWhiteSpace(prefabDraftPath))
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "prefab_draft_path", "PrefabDraftPath 不能为空。");
                return result;
            }

            if (spec == null)
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Schema, "spec", "UiPrefabSpec 不能为空。");
                return result;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabDraftPath);
            if (prefabRoot == null)
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Generator, "prefab_draft_path", "无法加载 prefab 草稿。");
                return result;
            }

            try
            {
                Dictionary<string, string> expectedPaths = BuildExpectedNodePaths(spec, result);
                HashSet<string> actualPaths = CollectActualPaths(prefabRoot.transform);

                foreach (KeyValuePair<string, string> pair in expectedPaths)
                {
                    if (!actualPaths.Contains(pair.Value))
                    {
                        AddIssue(
                            result,
                            UiPrefabValidationIssueSeverity.Error,
                            UiPrefabValidationIssueCategory.Generator,
                            pair.Key,
                            "prefab 草稿缺少节点: " + pair.Value);
                    }
                }

                foreach (string actualPath in actualPaths)
                {
                    if (!ContainsExpectedPath(expectedPaths, actualPath))
                    {
                        AddIssue(
                            result,
                            UiPrefabValidationIssueSeverity.Warning,
                            UiPrefabValidationIssueCategory.Generator,
                            actualPath,
                            "prefab 草稿包含未声明节点: " + actualPath);
                    }
                }

                ValidateNodeComponents(prefabRoot.transform, spec, expectedPaths, result);
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Dictionary<string, string> BuildExpectedNodePaths(UiPrefabSpec spec, UiPrefabValidationResult result)
        {
            var nodeById = new Dictionary<string, UiNodeSpec>(StringComparer.Ordinal);
            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                nodeById[node.NodeId] = node;
            }

            var pathCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var expectedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, UiNodeSpec> pair in nodeById)
            {
                expectedPaths[pair.Key] = BuildNodePath(pair.Key, nodeById, pathCache, visiting, result);
            }

            return expectedPaths;
        }

        private static string BuildNodePath(
            string nodeId,
            Dictionary<string, UiNodeSpec> nodeById,
            Dictionary<string, string> pathCache,
            HashSet<string> visiting,
            UiPrefabValidationResult result)
        {
            string cached;
            if (pathCache.TryGetValue(nodeId, out cached))
            {
                return cached;
            }

            if (!visiting.Add(nodeId))
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Schema, nodeId, "节点父子关系存在环。");
                return string.Empty;
            }

            UiNodeSpec node;
            if (!nodeById.TryGetValue(nodeId, out node) || node == null)
            {
                AddIssue(result, UiPrefabValidationIssueSeverity.Error, UiPrefabValidationIssueCategory.Schema, nodeId, "找不到节点定义。");
                visiting.Remove(nodeId);
                return string.Empty;
            }

            string path;
            if (string.IsNullOrWhiteSpace(node.ParentNodeId))
            {
                path = node.NodeName ?? string.Empty;
            }
            else
            {
                string parentPath = BuildNodePath(node.ParentNodeId, nodeById, pathCache, visiting, result);
                path = string.IsNullOrWhiteSpace(parentPath)
                    ? node.NodeName ?? string.Empty
                    : parentPath + "/" + (node.NodeName ?? string.Empty);
            }

            visiting.Remove(nodeId);
            pathCache[nodeId] = path;
            return path;
        }

        private static HashSet<string> CollectActualPaths(Transform root)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (root == null)
            {
                return paths;
            }

            CollectActualPaths(root, root.name ?? string.Empty, paths);
            return paths;
        }

        private static void CollectActualPaths(Transform current, string currentPath, HashSet<string> paths)
        {
            if (current == null)
            {
                return;
            }

            paths.Add(currentPath ?? string.Empty);
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                CollectActualPaths(child, currentPath + "/" + child.name, paths);
            }
        }

        private static void ValidateNodeComponents(
            Transform prefabRoot,
            UiPrefabSpec spec,
            Dictionary<string, string> expectedPaths,
            UiPrefabValidationResult result)
        {
            if (prefabRoot == null || spec == null || spec.Nodes == null)
            {
                return;
            }

            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                string nodePath;
                if (!expectedPaths.TryGetValue(node.NodeId, out nodePath) || string.IsNullOrWhiteSpace(nodePath))
                {
                    continue;
                }

                Transform actualTransform = FindTransformByPath(prefabRoot, nodePath);
                if (actualTransform == null)
                {
                    continue;
                }

                string[] actualComponentTypes = GetComponentTypes(actualTransform.gameObject);
                string[] expectedComponentTypes = GetExpectedComponentTypes(node);
                int actualIndex = 0;
                for (int componentIndex = 0; componentIndex < expectedComponentTypes.Length; componentIndex++)
                {
                    string expectedComponentType = expectedComponentTypes[componentIndex];
                    int foundIndex = IndexOf(actualComponentTypes, expectedComponentType, actualIndex);
                    if (foundIndex < 0)
                    {
                        AddIssue(
                            result,
                            UiPrefabValidationIssueSeverity.Error,
                            UiPrefabValidationIssueCategory.Generator,
                            nodePath + "." + expectedComponentType,
                            "prefab 草稿缺少组件: " + expectedComponentType);
                        continue;
                    }

                    actualIndex = foundIndex + 1;
                }
            }
        }

        private static string[] GetExpectedComponentTypes(UiNodeSpec node)
        {
            if (node == null || node.Components == null)
            {
                return new string[0];
            }

            var expected = new List<string>();
            for (int i = 0; i < node.Components.Count; i++)
            {
                UiComponentSpec component = node.Components[i];
                if (component != null && !string.IsNullOrWhiteSpace(component.ComponentType))
                {
                    expected.Add(component.ComponentType);
                }
            }

            return expected.ToArray();
        }

        private static string[] GetComponentTypes(GameObject nodeObject)
        {
            Component[] components = nodeObject.GetComponents<Component>();
            var types = new List<string>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;
                if (string.Equals(typeName, "CanvasRenderer", StringComparison.Ordinal))
                {
                    continue;
                }

                types.Add(typeName);
            }

            return types.ToArray();
        }

        private static int IndexOf(string[] values, string expected, int startIndex)
        {
            if (values == null || values.Length == 0 || string.IsNullOrWhiteSpace(expected))
            {
                return -1;
            }

            for (int i = Math.Max(0, startIndex); i < values.Length; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Transform FindTransformByPath(Transform root, string expectedPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(expectedPath))
            {
                return null;
            }

            if (string.Equals(root.name, expectedPath, StringComparison.Ordinal))
            {
                return root;
            }

            string prefix = root.name + "/";
            if (!expectedPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            string relativePath = expectedPath.Substring(prefix.Length);
            return root.Find(relativePath);
        }

        private static bool ContainsExpectedPath(Dictionary<string, string> expectedPaths, string actualPath)
        {
            if (expectedPaths == null || expectedPaths.Count == 0 || string.IsNullOrWhiteSpace(actualPath))
            {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in expectedPaths)
            {
                if (string.Equals(pair.Value, actualPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddIssue(
            UiPrefabValidationResult result,
            UiPrefabValidationIssueSeverity severity,
            UiPrefabValidationIssueCategory category,
            string fieldPath,
            string message)
        {
            result.Issues.Add(new UiPrefabValidationIssue
            {
                Severity = severity,
                Category = category,
                FieldPath = fieldPath ?? string.Empty,
                Message = message ?? string.Empty,
            });
        }
    }
}
