using System;
using UiPrefabGenerator.Core.Interpretation;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Intake
{
    public sealed class DefaultDesignPacketToUiPrefabSpecInterpreter : IUiSpecInterpreter
    {
        private const string DefaultGenerationProfileId = "holmas_ugui";
        private const string PanelBackgroundRuleId = "panel_background";
        private const string TitleTextRuleId = "title_text";
        private const string PrimaryButtonRuleId = "primary_button";
        private const string NumericValueDisplayRuleId = "numeric_value_display";
        private const string TaskListScrollableRuleId = "task_list_scrollable";
        private const string ClaimButtonClickableRuleId = "claim_button_clickable";
        private const string ClaimButtonVisualizedRuleId = "claim_button_visualized";

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

            DesignElementHint titleHint = FindBestElementHint(designPacket, "title_text");
            if (HasRule(designPacket, TitleTextRuleId) || titleHint != null)
            {
                spec.Nodes.Add(BuildTitleTextNode(titleHint));
                spec.Bindings.Add(new UiBindingSpec
                {
                    NodeId = titleHint != null && !string.IsNullOrWhiteSpace(titleHint.SuggestedNodeId)
                        ? titleHint.SuggestedNodeId
                        : "title_text",
                    BindingKey = "title_text",
                });
            }

            DesignElementHint primaryButtonHint = FindBestElementHint(designPacket, "primary_button");
            if (HasRule(designPacket, PrimaryButtonRuleId) || primaryButtonHint != null)
            {
                spec.Nodes.Add(BuildPrimaryButtonNode(primaryButtonHint));
                spec.Interactions.Add(new UiInteractionSpec
                {
                    NodeId = primaryButtonHint != null && !string.IsNullOrWhiteSpace(primaryButtonHint.SuggestedNodeId)
                        ? primaryButtonHint.SuggestedNodeId
                        : "primary_button",
                    EventName = "on_click",
                    HandlerKey = "primary_action",
                });
            }

            DesignElementHint numericHint = FindBestElementHint(designPacket, "numeric_value_display");
            if (HasRule(designPacket, NumericValueDisplayRuleId) || numericHint != null)
            {
                spec.Nodes.Add(BuildNumericValueNode(numericHint));
                spec.Bindings.Add(new UiBindingSpec
                {
                    NodeId = numericHint != null && !string.IsNullOrWhiteSpace(numericHint.SuggestedNodeId)
                        ? numericHint.SuggestedNodeId
                        : "numeric_value_display",
                    BindingKey = "numeric_value",
                });
            }

            if (HasRule(designPacket, TaskListScrollableRuleId))
            {
                spec.Nodes.Add(BuildScrollableTaskListNode());
            }

            bool hasClickableClaimButton = HasRule(designPacket, ClaimButtonClickableRuleId);
            bool hasVisualizedClaimButton = HasRule(designPacket, ClaimButtonVisualizedRuleId);
            if (hasClickableClaimButton || hasVisualizedClaimButton)
            {
                spec.Nodes.Add(BuildClaimButtonNode(hasVisualizedClaimButton));
                if (hasClickableClaimButton)
                {
                    spec.Interactions.Add(BuildClaimButtonInteraction());
                }
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

        private static UiNodeSpec BuildTitleTextNode(DesignElementHint hint)
        {
            var node = new UiNodeSpec
            {
                NodeId = hint != null && !string.IsNullOrWhiteSpace(hint.SuggestedNodeId)
                    ? hint.SuggestedNodeId
                    : "title_text",
                NodeName = "TitleText",
                ParentNodeId = "root",
                Layout = new UiLayoutSpec
                {
                    LayoutType = "Anchored",
                    LayoutSlot = hint != null && !string.IsNullOrWhiteSpace(hint.LayoutSlot)
                        ? hint.LayoutSlot
                        : "title_text",
                },
            };
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "Text",
                BindingKey = "title_text",
            });
            return node;
        }

        private static UiNodeSpec BuildPrimaryButtonNode(DesignElementHint hint)
        {
            var node = new UiNodeSpec
            {
                NodeId = hint != null && !string.IsNullOrWhiteSpace(hint.SuggestedNodeId)
                    ? hint.SuggestedNodeId
                    : "primary_button",
                NodeName = "PrimaryButton",
                ParentNodeId = "root",
                Layout = new UiLayoutSpec
                {
                    LayoutType = "Anchored",
                    LayoutSlot = hint != null && !string.IsNullOrWhiteSpace(hint.LayoutSlot)
                        ? hint.LayoutSlot
                        : "primary_button",
                },
            };
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "Image",
                AssetSlot = hint != null && !string.IsNullOrWhiteSpace(hint.AssetSlot)
                    ? hint.AssetSlot
                    : "primary_button_bg",
            });
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "Button",
                BindingKey = "primary_button",
            });
            return node;
        }

        private static UiNodeSpec BuildNumericValueNode(DesignElementHint hint)
        {
            var node = new UiNodeSpec
            {
                NodeId = hint != null && !string.IsNullOrWhiteSpace(hint.SuggestedNodeId)
                    ? hint.SuggestedNodeId
                    : "numeric_value_display",
                NodeName = "NumericValueDisplay",
                ParentNodeId = "root",
                Layout = new UiLayoutSpec
                {
                    LayoutType = "Anchored",
                    LayoutSlot = hint != null && !string.IsNullOrWhiteSpace(hint.LayoutSlot)
                        ? hint.LayoutSlot
                        : "numeric_value_display",
                },
            };
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });
            node.Components.Add(new UiComponentSpec
            {
                ComponentType = "Text",
                BindingKey = "numeric_value",
            });
            return node;
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

        private static UiNodeSpec BuildClaimButtonNode(bool includeVisualComponent)
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
            if (includeVisualComponent)
            {
                claimButtonNode.Components.Add(new UiComponentSpec
                {
                    ComponentType = "Image",
                    AssetSlot = "claim_button_bg",
                });
            }
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
            DesignElementHint panelHint = FindBestElementHint(designPacket, "panel_background");
            if (panelHint != null && !string.IsNullOrWhiteSpace(panelHint.AssetSlot))
            {
                return panelHint.AssetSlot;
            }

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

        private static DesignElementHint FindBestElementHint(DesignPacket designPacket, string semanticRole)
        {
            if (designPacket == null ||
                designPacket.ElementHints == null ||
                string.IsNullOrWhiteSpace(semanticRole))
            {
                return null;
            }

            DesignElementHint bestHint = null;
            for (int i = 0; i < designPacket.ElementHints.Count; i++)
            {
                DesignElementHint hint = designPacket.ElementHints[i];
                if (hint == null ||
                    !string.Equals(hint.SemanticRole, semanticRole, StringComparison.Ordinal))
                {
                    continue;
                }

                if (bestHint == null || hint.Confidence > bestHint.Confidence)
                {
                    bestHint = hint;
                }
            }

            return bestHint;
        }
    }
}
