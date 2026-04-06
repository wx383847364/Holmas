using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Validation
{
    public interface IUiSpecValidator
    {
        UiPrefabSpecValidationResult Validate(UiPrefabSpec spec);
    }

    [Serializable]
    public sealed class UiPrefabSpecValidationResult
    {
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();

        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }

    public sealed class DefaultUiSpecValidator : IUiSpecValidator
    {
        public UiPrefabSpecValidationResult Validate(UiPrefabSpec spec)
        {
            var result = new UiPrefabSpecValidationResult();
            if (spec == null)
            {
                result.Errors.Add("UiPrefabSpec 不能为空。");
                return result;
            }

            if (string.IsNullOrWhiteSpace(spec.PageId))
            {
                result.Errors.Add("UiPrefabSpec.PageId 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(spec.PrefabName))
            {
                result.Errors.Add("UiPrefabSpec.PrefabName 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(spec.RootNodeId))
            {
                result.Errors.Add("UiPrefabSpec.RootNodeId 不能为空。");
            }

            if (spec.Nodes == null || spec.Nodes.Count == 0)
            {
                result.Errors.Add("UiPrefabSpec.Nodes 不能为空。");
                return result;
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            bool foundRoot = false;
            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null)
                {
                    result.Errors.Add(string.Format("UiPrefabSpec.Nodes[{0}] 不能为空。", i));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.NodeId))
                {
                    result.Errors.Add(string.Format("UiPrefabSpec.Nodes[{0}].NodeId 不能为空。", i));
                    continue;
                }

                if (!nodeIds.Add(node.NodeId))
                {
                    result.Errors.Add(string.Format("UiPrefabSpec.Nodes 存在重复 NodeId: {0}。", node.NodeId));
                }

                if (string.IsNullOrWhiteSpace(node.NodeName))
                {
                    result.Errors.Add(string.Format("UiPrefabSpec.Nodes[{0}].NodeName 不能为空。", i));
                }

                if (string.Equals(node.NodeId, spec.RootNodeId, StringComparison.Ordinal))
                {
                    foundRoot = true;
                }

                if (node.Components == null)
                {
                    result.Warnings.Add(string.Format("节点 {0} 没有组件列表。", node.NodeId));
                    continue;
                }

                for (int componentIndex = 0; componentIndex < node.Components.Count; componentIndex++)
                {
                    UiComponentSpec component = node.Components[componentIndex];
                    if (component == null || string.IsNullOrWhiteSpace(component.ComponentType))
                    {
                        result.Errors.Add(string.Format("节点 {0} 存在空组件类型。", node.NodeId));
                    }
                }
            }

            if (!foundRoot)
            {
                result.Errors.Add(string.Format("找不到 RootNodeId 对应节点: {0}。", spec.RootNodeId));
            }

            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                UiNodeSpec node = spec.Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                bool isRoot = string.Equals(node.NodeId, spec.RootNodeId, StringComparison.Ordinal);
                if (isRoot)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.ParentNodeId))
                {
                    result.Errors.Add(string.Format("非根节点 {0} 缺少 ParentNodeId。", node.NodeId));
                    continue;
                }

                if (string.Equals(node.ParentNodeId, node.NodeId, StringComparison.Ordinal))
                {
                    result.Errors.Add(string.Format("节点 {0} 不能引用自己作为父节点。", node.NodeId));
                    continue;
                }

                if (!nodeIds.Contains(node.ParentNodeId))
                {
                    result.Errors.Add(string.Format("节点 {0} 的父节点不存在: {1}。", node.NodeId, node.ParentNodeId));
                }
            }

            ValidateParentCycles(spec.Nodes, result);

            ValidateBindingTargets(spec.Bindings, nodeIds, "Binding", result);
            ValidateInteractionTargets(spec.Interactions, nodeIds, result);
            return result;
        }

        private static void ValidateParentCycles(
            List<UiNodeSpec> nodes,
            UiPrefabSpecValidationResult result)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var parentByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                UiNodeSpec node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                parentByNodeId[node.NodeId] = node.ParentNodeId ?? string.Empty;
            }

            var processed = new HashSet<string>(StringComparer.Ordinal);
            var reportedCycleNodes = new HashSet<string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> pair in parentByNodeId)
            {
                if (processed.Contains(pair.Key))
                {
                    continue;
                }

                var traversal = new List<string>();
                var traversalIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                string currentNodeId = pair.Key;

                while (!string.IsNullOrWhiteSpace(currentNodeId) && parentByNodeId.ContainsKey(currentNodeId))
                {
                    if (processed.Contains(currentNodeId))
                    {
                        break;
                    }

                    int existingIndex;
                    if (traversalIndex.TryGetValue(currentNodeId, out existingIndex))
                    {
                        var cycleNodes = traversal.GetRange(existingIndex, traversal.Count - existingIndex);
                        if (cycleNodes.Count > 0)
                        {
                            bool shouldReport = false;
                            for (int cycleIndex = 0; cycleIndex < cycleNodes.Count; cycleIndex++)
                            {
                                if (reportedCycleNodes.Add(cycleNodes[cycleIndex]))
                                {
                                    shouldReport = true;
                                }
                            }

                            if (shouldReport)
                            {
                                result.Errors.Add(string.Format(
                                    "检测到节点父子环: {0} -> {1}。",
                                    string.Join(" -> ", cycleNodes.ToArray()),
                                    currentNodeId));
                            }
                        }

                        break;
                    }

                    traversalIndex[currentNodeId] = traversal.Count;
                    traversal.Add(currentNodeId);

                    string parentNodeId = parentByNodeId[currentNodeId];
                    if (string.IsNullOrWhiteSpace(parentNodeId))
                    {
                        break;
                    }

                    currentNodeId = parentNodeId;
                }

                for (int traversalItemIndex = 0; traversalItemIndex < traversal.Count; traversalItemIndex++)
                {
                    processed.Add(traversal[traversalItemIndex]);
                }
            }
        }

        private static void ValidateBindingTargets(
            List<UiBindingSpec> bindings,
            HashSet<string> nodeIds,
            string label,
            UiPrefabSpecValidationResult result)
        {
            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                UiBindingSpec binding = bindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.NodeId))
                {
                    result.Errors.Add(string.Format("{0}[{1}] 缺少 NodeId。", label, i));
                    continue;
                }

                if (!nodeIds.Contains(binding.NodeId))
                {
                    result.Errors.Add(string.Format("{0}[{1}] 引用了不存在的节点: {2}。", label, i, binding.NodeId));
                }
            }
        }

        private static void ValidateInteractionTargets(
            List<UiInteractionSpec> interactions,
            HashSet<string> nodeIds,
            UiPrefabSpecValidationResult result)
        {
            if (interactions == null)
            {
                return;
            }

            for (int i = 0; i < interactions.Count; i++)
            {
                UiInteractionSpec interaction = interactions[i];
                if (interaction == null || string.IsNullOrWhiteSpace(interaction.NodeId))
                {
                    result.Errors.Add(string.Format("Interaction[{0}] 缺少 NodeId。", i));
                    continue;
                }

                if (!nodeIds.Contains(interaction.NodeId))
                {
                    result.Errors.Add(string.Format("Interaction[{0}] 引用了不存在的节点: {1}。", i, interaction.NodeId));
                }

                if (string.IsNullOrWhiteSpace(interaction.EventName))
                {
                    result.Errors.Add(string.Format("Interaction[{0}] 缺少 EventName。", i));
                }
            }
        }
    }
}
