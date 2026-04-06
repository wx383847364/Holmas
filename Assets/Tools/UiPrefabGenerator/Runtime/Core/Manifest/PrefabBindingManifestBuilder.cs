using System;
using System.Collections.Generic;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Manifest
{
    public interface IPrefabBindingManifestBuilder
    {
        PrefabBindingManifestBuildResult Build(PrefabBindingManifestBuildRequest request);
    }

    [Serializable]
    public sealed class PrefabBindingManifestBuildRequest
    {
        public UiPrefabSpec Spec;
        public string PrefabDraftPath = string.Empty;
    }

    [Serializable]
    public sealed class PrefabBindingManifestBuildResult
    {
        public bool Success;
        public PrefabBindingManifest Manifest = new PrefabBindingManifest();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    public sealed class DefaultPrefabBindingManifestBuilder : IPrefabBindingManifestBuilder
    {
        public PrefabBindingManifestBuildResult Build(PrefabBindingManifestBuildRequest request)
        {
            var result = new PrefabBindingManifestBuildResult();
            if (request == null)
            {
                result.Errors.Add("PrefabBindingManifestBuildRequest 不能为空。");
                return result;
            }

            if (request.Spec == null)
            {
                result.Errors.Add("PrefabBindingManifestBuildRequest.Spec 不能为空。");
                return result;
            }

            if (string.IsNullOrWhiteSpace(request.PrefabDraftPath))
            {
                result.Errors.Add("PrefabBindingManifestBuildRequest.PrefabDraftPath 不能为空。");
                return result;
            }

            if (request.Spec.Nodes == null)
            {
                result.Errors.Add("PrefabBindingManifestBuildRequest.Spec.Nodes 不能为空。");
                return result;
            }

            var nodesById = new Dictionary<string, UiNodeSpec>(StringComparer.Ordinal);
            for (int i = 0; i < request.Spec.Nodes.Count; i++)
            {
                UiNodeSpec node = request.Spec.Nodes[i];
                if (node != null && !string.IsNullOrWhiteSpace(node.NodeId))
                {
                    nodesById[node.NodeId] = node;
                }
            }

            var bindingsByNode = new Dictionary<string, List<UiBindingSpec>>(StringComparer.Ordinal);
            CollectBindingsByNode(request.Spec.Bindings, bindingsByNode);

            var interactionsByNode = new Dictionary<string, List<UiInteractionSpec>>(StringComparer.Ordinal);
            CollectInteractionsByNode(request.Spec.Interactions, interactionsByNode);

            var manifest = new PrefabBindingManifest
            {
                PrefabName = request.Spec.PrefabName ?? string.Empty,
                PrefabDraftPath = request.PrefabDraftPath ?? string.Empty,
            };

            for (int i = 0; i < request.Spec.Nodes.Count; i++)
            {
                UiNodeSpec node = request.Spec.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                string nodePath;
                string nodePathError;
                if (!TryBuildNodePath(node, nodesById, out nodePath, out nodePathError))
                {
                    result.Errors.Add(nodePathError);
                    continue;
                }

                if (node.Components == null)
                {
                    continue;
                }

                List<UiInteractionSpec> nodeInteractions;
                int interactionCarrierComponentIndex = -1;
                if (interactionsByNode.TryGetValue(node.NodeId, out nodeInteractions) &&
                    nodeInteractions.Count > 0)
                {
                    interactionCarrierComponentIndex = FindInteractionCarrierComponentIndex(node.Components);
                    if (nodeInteractions.Count > 1)
                    {
                        result.Warnings.Add(string.Format("节点 {0} 存在多个交互候选，已使用第一个。", node.NodeId));
                    }

                    if (interactionCarrierComponentIndex < 0)
                    {
                        result.Warnings.Add(string.Format("节点 {0} 定义了交互，但未找到可承载交互的组件。", node.NodeId));
                    }
                }

                for (int componentIndex = 0; componentIndex < node.Components.Count; componentIndex++)
                {
                    UiComponentSpec component = node.Components[componentIndex];
                    if (component == null || string.IsNullOrWhiteSpace(component.ComponentType))
                    {
                        continue;
                    }

                    string bindingKey = component.BindingKey ?? string.Empty;
                    bool bindingInferredFromNode = false;
                    List<UiBindingSpec> nodeBindings;
                    if (string.IsNullOrWhiteSpace(bindingKey) &&
                        bindingsByNode.TryGetValue(node.NodeId, out nodeBindings) &&
                        nodeBindings.Count > 0)
                    {
                        bindingKey = nodeBindings[0].BindingKey ?? string.Empty;
                        bindingInferredFromNode = true;
                        if (nodeBindings.Count > 1)
                        {
                            result.Warnings.Add(string.Format("节点 {0} 存在多个绑定候选，已使用第一个。", node.NodeId));
                        }
                    }

                    string eventName = string.Empty;
                    string handlerKey = string.Empty;
                    bool requiresManualWiring = false;
                    if (nodeInteractions != null &&
                        nodeInteractions.Count > 0 &&
                        componentIndex == interactionCarrierComponentIndex)
                    {
                        UiInteractionSpec interaction = nodeInteractions[0];
                        eventName = interaction.EventName ?? string.Empty;
                        handlerKey = interaction.HandlerKey ?? string.Empty;
                        requiresManualWiring = true;
                    }

                    var entry = new PrefabBindingEntry
                    {
                        NodePath = nodePath,
                        ComponentType = component.ComponentType,
                        BindingKey = bindingKey,
                        AssetSlot = component.AssetSlot ?? string.Empty,
                        EventName = eventName,
                        RequiresManualWiring = requiresManualWiring,
                        Notes = BuildEntryNotes(
                            node,
                            component,
                            bindingKey,
                            bindingInferredFromNode,
                            handlerKey,
                            requiresManualWiring)
                    };

                    manifest.Entries.Add(entry);
                }
            }

            result.Manifest = manifest;
            result.Success = result.Errors.Count == 0;
            return result;
        }

        private static void CollectBindingsByNode(
            List<UiBindingSpec> bindings,
            Dictionary<string, List<UiBindingSpec>> bindingsByNode)
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
                    continue;
                }

                List<UiBindingSpec> list;
                if (!bindingsByNode.TryGetValue(binding.NodeId, out list))
                {
                    list = new List<UiBindingSpec>();
                    bindingsByNode[binding.NodeId] = list;
                }

                list.Add(binding);
            }
        }

        private static int FindInteractionCarrierComponentIndex(List<UiComponentSpec> components)
        {
            if (components == null)
            {
                return -1;
            }

            for (int i = 0; i < components.Count; i++)
            {
                UiComponentSpec component = components[i];
                if (component != null && IsInteractionCarrierComponentType(component.ComponentType))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsInteractionCarrierComponentType(string componentType)
        {
            return string.Equals(componentType, "Button", StringComparison.Ordinal) ||
                   string.Equals(componentType, "Toggle", StringComparison.Ordinal) ||
                   string.Equals(componentType, "Slider", StringComparison.Ordinal) ||
                   string.Equals(componentType, "Scrollbar", StringComparison.Ordinal) ||
                   string.Equals(componentType, "ScrollRect", StringComparison.Ordinal) ||
                   string.Equals(componentType, "InputField", StringComparison.Ordinal);
        }

        private static void CollectInteractionsByNode(
            List<UiInteractionSpec> interactions,
            Dictionary<string, List<UiInteractionSpec>> interactionsByNode)
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
                    continue;
                }

                List<UiInteractionSpec> list;
                if (!interactionsByNode.TryGetValue(interaction.NodeId, out list))
                {
                    list = new List<UiInteractionSpec>();
                    interactionsByNode[interaction.NodeId] = list;
                }

                list.Add(interaction);
            }
        }

        private static string BuildEntryNotes(
            UiNodeSpec node,
            UiComponentSpec component,
            string bindingKey,
            bool bindingInferredFromNode,
            string handlerKey,
            bool requiresManualWiring)
        {
            var notes = new List<string>
            {
                "node_id=" + (node.NodeId ?? string.Empty),
                "component=" + (component.ComponentType ?? string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(bindingKey))
            {
                notes.Add("binding_key=" + bindingKey);
            }
            else if (bindingInferredFromNode)
            {
                notes.Add("binding_key=inferred_from_node");
            }

            if (!string.IsNullOrWhiteSpace(component.AssetSlot))
            {
                notes.Add("asset_slot=" + component.AssetSlot);
            }

            if (node.Layout != null && !string.IsNullOrWhiteSpace(node.Layout.LayoutType))
            {
                notes.Add("layout=" + node.Layout.LayoutType);
            }

            if (node.Layout != null && !string.IsNullOrWhiteSpace(node.Layout.LayoutSlot))
            {
                notes.Add("layout_slot=" + node.Layout.LayoutSlot);
            }

            if (!string.IsNullOrWhiteSpace(handlerKey))
            {
                notes.Add("handler_key=" + handlerKey);
            }

            notes.Add("manual_wiring=" + (requiresManualWiring ? "true" : "false"));
            return string.Join("; ", notes.ToArray());
        }

        private static bool TryBuildNodePath(
            UiNodeSpec node,
            Dictionary<string, UiNodeSpec> nodesById,
            out string nodePath,
            out string error)
        {
            return TryBuildNodePath(
                node,
                nodesById,
                new List<string>(),
                new Dictionary<string, int>(StringComparer.Ordinal),
                out nodePath,
                out error);
        }

        private static bool TryBuildNodePath(
            UiNodeSpec node,
            Dictionary<string, UiNodeSpec> nodesById,
            List<string> traversal,
            Dictionary<string, int> traversalIndex,
            out string nodePath,
            out string error)
        {
            nodePath = string.Empty;
            error = string.Empty;
            if (node == null)
            {
                error = "构建 NodePath 失败: 节点不能为空。";
                return false;
            }

            string currentNodeId = node.NodeId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentNodeId))
            {
                int existingIndex;
                if (traversalIndex.TryGetValue(currentNodeId, out existingIndex))
                {
                    var cycleNodes = traversal.GetRange(existingIndex, traversal.Count - existingIndex);
                    cycleNodes.Add(currentNodeId);
                    error = string.Format(
                        "构建 NodePath 失败，检测到节点父子环: {0}。",
                        string.Join(" -> ", cycleNodes.ToArray()));
                    return false;
                }

                traversalIndex[currentNodeId] = traversal.Count;
                traversal.Add(currentNodeId);
            }

            if (string.IsNullOrWhiteSpace(node.ParentNodeId))
            {
                nodePath = node.NodeName ?? string.Empty;
                return true;
            }

            UiNodeSpec parent;
            if (!nodesById.TryGetValue(node.ParentNodeId, out parent) || parent == null)
            {
                nodePath = node.NodeName ?? string.Empty;
                return true;
            }

            string parentPath;
            if (!TryBuildNodePath(parent, nodesById, traversal, traversalIndex, out parentPath, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                nodePath = node.NodeName ?? string.Empty;
                return true;
            }

            nodePath = parentPath + "/" + (node.NodeName ?? string.Empty);
            return true;
        }
    }
}
