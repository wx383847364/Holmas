using System;
using UiPrefabGenerator.Core.Interpretation;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Intake
{
    public sealed class DefaultDesignPacketToUiPrefabSpecInterpreter : IUiSpecInterpreter
    {
        private const string DefaultGenerationProfileId = "holmas_ugui";
        private const string TaskListScrollableRuleId = "task_list_scrollable";
        private const string ClaimButtonClickableRuleId = "claim_button_clickable";

        private readonly IDesignPacketIntakeAnalyzer _analyzer;
        private readonly string _generationProfileId;

        public DefaultDesignPacketToUiPrefabSpecInterpreter(
            IDesignPacketIntakeAnalyzer analyzer = null,
            string generationProfileId = DefaultGenerationProfileId)
        {
            _analyzer = analyzer ?? new DefaultDesignPacketIntakeAnalyzer();
            _generationProfileId = string.IsNullOrWhiteSpace(generationProfileId) ? DefaultGenerationProfileId : generationProfileId;
        }

        public UiPrefabSpec Interpret(DesignPacket designPacket)
        {
            DesignPacketIntakeAssessment assessment = _analyzer.Analyze(designPacket);
            if (assessment.HasBlockingIssues)
            {
                throw new InvalidOperationException("DesignPacket 存在 blocking intake issues，不能直接转换为 UiPrefabSpec。");
            }

            var spec = new UiPrefabSpec
            {
                PageId = designPacket.PageId ?? string.Empty,
                PrefabName = designPacket.PrefabName ?? string.Empty,
                RootNodeId = "root",
                GenerationProfileId = _generationProfileId,
            };

            spec.Nodes.Add(BuildRootNode(designPacket));

            if (HasRule(designPacket, TaskListScrollableRuleId))
            {
                spec.Nodes.Add(BuildScrollableTaskListNode());
            }

            if (HasRule(designPacket, ClaimButtonClickableRuleId))
            {
                spec.Nodes.Add(BuildClaimButtonNode());
                spec.Interactions.Add(BuildClaimButtonInteraction());
            }

            return spec;
        }

        private static UiNodeSpec BuildRootNode(DesignPacket designPacket)
        {
            var rootNode = new UiNodeSpec
            {
                NodeId = "root",
                NodeName = designPacket.PrefabName ?? string.Empty,
                ParentNodeId = string.Empty,
                Layout = new UiLayoutSpec
                {
                    LayoutType = "FullScreen",
                    LayoutSlot = "root",
                },
            };
            rootNode.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });

            string primaryAssetSlot = ResolvePrimaryAssetSlot(designPacket);
            if (!string.IsNullOrWhiteSpace(primaryAssetSlot))
            {
                rootNode.Components.Add(new UiComponentSpec
                {
                    ComponentType = "Image",
                    AssetSlot = primaryAssetSlot,
                });
            }

            return rootNode;
        }

        private static UiNodeSpec BuildScrollableTaskListNode()
        {
            var taskListNode = new UiNodeSpec
            {
                NodeId = "task_list",
                NodeName = "TaskList",
                ParentNodeId = "root",
                Layout = new UiLayoutSpec
                {
                    LayoutType = "VerticalLayout",
                    LayoutSlot = "task_list",
                },
            };
            taskListNode.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });
            taskListNode.Components.Add(new UiComponentSpec
            {
                ComponentType = "ScrollRect",
                BindingKey = "task_list",
            });

            return taskListNode;
        }

        private static UiNodeSpec BuildClaimButtonNode()
        {
            var claimButtonNode = new UiNodeSpec
            {
                NodeId = "claim_button",
                NodeName = "ClaimButton",
                ParentNodeId = "root",
                Layout = new UiLayoutSpec
                {
                    LayoutType = "Anchored",
                    LayoutSlot = "claim_button",
                },
            };
            claimButtonNode.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });
            claimButtonNode.Components.Add(new UiComponentSpec
            {
                ComponentType = "Button",
                BindingKey = "claim_button",
            });

            return claimButtonNode;
        }

        private static UiInteractionSpec BuildClaimButtonInteraction()
        {
            return new UiInteractionSpec
            {
                NodeId = "claim_button",
                EventName = "on_click",
                HandlerKey = "claim_task",
            };
        }

        private static bool HasRule(DesignPacket designPacket, string ruleId)
        {
            if (designPacket == null || designPacket.Rules == null || string.IsNullOrWhiteSpace(ruleId))
            {
                return false;
            }

            for (int i = 0; i < designPacket.Rules.Count; i++)
            {
                DesignRuleDefinition rule = designPacket.Rules[i];
                if (rule != null && string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolvePrimaryAssetSlot(DesignPacket designPacket)
        {
            if (designPacket == null || designPacket.AssetSlotHints == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < designPacket.AssetSlotHints.Count; i++)
            {
                DesignAssetSlotHint hint = designPacket.AssetSlotHints[i];
                if (hint != null && !string.IsNullOrWhiteSpace(hint.SlotId))
                {
                    return hint.SlotId;
                }
            }

            return string.Empty;
        }
    }
}
