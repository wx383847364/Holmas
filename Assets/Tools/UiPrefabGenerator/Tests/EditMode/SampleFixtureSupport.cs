using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UiPrefabGenerator.Core.Intake;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.HolmasAdapter;
using UnityEngine;

namespace UiPrefabGenerator.Tests.EditMode
{
    internal static class SampleFixtureLoader
    {
        private const string SampleRoot = "Assets/Tools/UiPrefabGenerator/Samples~/Holmas";

        public static DesignPacket LoadDesignPacket()
        {
            DesignPacketJson json = LoadJson<DesignPacketJson>("sample_design_packet.json");
            var packet = new DesignPacket
            {
                PageId = json.page_id ?? string.Empty,
                PageTitle = json.page_title ?? string.Empty,
                PrefabName = json.prefab_name ?? string.Empty,
                Notes = json.notes ?? string.Empty,
            };

            if (json.design_images != null)
            {
                for (int i = 0; i < json.design_images.Count; i++)
                {
                    DesignImageReferenceJson image = json.design_images[i] ?? new DesignImageReferenceJson();
                    packet.DesignImages.Add(new DesignImageReference
                    {
                        ImageId = image.image_id ?? string.Empty,
                        ImagePath = image.image_path ?? string.Empty,
                        StateId = image.state_id ?? string.Empty,
                    });
                }
            }

            if (json.states != null)
            {
                for (int i = 0; i < json.states.Count; i++)
                {
                    DesignStateDefinitionJson state = json.states[i] ?? new DesignStateDefinitionJson();
                    packet.States.Add(new DesignStateDefinition
                    {
                        StateId = state.state_id ?? string.Empty,
                        Description = state.description ?? string.Empty,
                    });
                }
            }

            if (json.rules != null)
            {
                for (int i = 0; i < json.rules.Count; i++)
                {
                    DesignRuleDefinitionJson rule = json.rules[i] ?? new DesignRuleDefinitionJson();
                    packet.Rules.Add(new DesignRuleDefinition
                    {
                        RuleId = rule.rule_id ?? string.Empty,
                        Description = rule.description ?? string.Empty,
                    });
                }
            }

            if (json.asset_slot_hints != null)
            {
                for (int i = 0; i < json.asset_slot_hints.Count; i++)
                {
                    DesignAssetSlotHintJson hint = json.asset_slot_hints[i] ?? new DesignAssetSlotHintJson();
                    packet.AssetSlotHints.Add(new DesignAssetSlotHint
                    {
                        SlotId = hint.slot_id ?? string.Empty,
                        Usage = hint.usage ?? string.Empty,
                    });
                }
            }

            return packet;
        }

        public static UiPrefabSpec LoadUiPrefabSpec()
        {
            UiPrefabSpecJson json = LoadJson<UiPrefabSpecJson>("sample_ui_prefab_spec.json");
            var spec = new UiPrefabSpec
            {
                PageId = json.page_id ?? string.Empty,
                PrefabName = json.prefab_name ?? string.Empty,
                RootNodeId = json.root_node_id ?? string.Empty,
                GenerationProfileId = json.generation_profile_id ?? string.Empty,
            };

            if (json.nodes != null)
            {
                for (int i = 0; i < json.nodes.Count; i++)
                {
                    UiNodeSpecJson nodeJson = json.nodes[i] ?? new UiNodeSpecJson();
                    var node = new UiNodeSpec
                    {
                        NodeId = nodeJson.node_id ?? string.Empty,
                        NodeName = nodeJson.node_name ?? string.Empty,
                        ParentNodeId = nodeJson.parent_node_id ?? string.Empty,
                        Layout = new UiLayoutSpec
                        {
                            LayoutType = nodeJson.layout != null ? nodeJson.layout.layout_type ?? string.Empty : string.Empty,
                            LayoutSlot = nodeJson.layout != null ? nodeJson.layout.layout_slot ?? string.Empty : string.Empty,
                        },
                    };

                    if (nodeJson.components != null)
                    {
                        for (int componentIndex = 0; componentIndex < nodeJson.components.Count; componentIndex++)
                        {
                            UiComponentSpecJson componentJson = nodeJson.components[componentIndex] ?? new UiComponentSpecJson();
                            node.Components.Add(new UiComponentSpec
                            {
                                ComponentType = componentJson.component_type ?? string.Empty,
                                BindingKey = componentJson.binding_key ?? string.Empty,
                                AssetSlot = componentJson.asset_slot ?? string.Empty,
                            });
                        }
                    }

                    spec.Nodes.Add(node);
                }
            }

            if (json.bindings != null)
            {
                for (int i = 0; i < json.bindings.Count; i++)
                {
                    UiBindingSpecJson bindingJson = json.bindings[i] ?? new UiBindingSpecJson();
                    spec.Bindings.Add(new UiBindingSpec
                    {
                        NodeId = bindingJson.node_id ?? string.Empty,
                        BindingKey = bindingJson.binding_key ?? string.Empty,
                    });
                }
            }

            if (json.interactions != null)
            {
                for (int i = 0; i < json.interactions.Count; i++)
                {
                    UiInteractionSpecJson interactionJson = json.interactions[i] ?? new UiInteractionSpecJson();
                    spec.Interactions.Add(new UiInteractionSpec
                    {
                        NodeId = interactionJson.node_id ?? string.Empty,
                        EventName = interactionJson.event_name ?? string.Empty,
                        HandlerKey = interactionJson.handler_key ?? string.Empty,
                    });
                }
            }

            return spec;
        }

        public static PrefabBindingManifest LoadPrefabBindingManifest()
        {
            PrefabBindingManifestJson json = LoadJson<PrefabBindingManifestJson>("sample_prefab_binding_manifest.json");
            var manifest = new PrefabBindingManifest
            {
                PrefabName = json.prefab_name ?? string.Empty,
                PrefabDraftPath = json.prefab_draft_path ?? string.Empty,
            };

            if (json.entries != null)
            {
                for (int i = 0; i < json.entries.Count; i++)
                {
                    PrefabBindingEntryJson entryJson = json.entries[i] ?? new PrefabBindingEntryJson();
                    manifest.Entries.Add(new PrefabBindingEntry
                    {
                        NodePath = entryJson.node_path ?? string.Empty,
                        ComponentType = entryJson.component_type ?? string.Empty,
                        BindingKey = entryJson.binding_key ?? string.Empty,
                        AssetSlot = entryJson.asset_slot ?? string.Empty,
                        EventName = entryJson.event_name ?? string.Empty,
                        RequiresManualWiring = entryJson.requires_manual_wiring,
                        Notes = entryJson.notes ?? string.Empty,
                    });
                }
            }

            return manifest;
        }

        public static HolmasGeneratedResultPlan LoadHolmasGeneratedResultPlan()
        {
            HolmasGeneratedResultPlanJson json = LoadJson<HolmasGeneratedResultPlanJson>("sample_holmas_generated_result_plan.json");
            var plan = new HolmasGeneratedResultPlan
            {
                ProfileId = json.profile_id ?? string.Empty,
                PrefabName = json.prefab_name ?? string.Empty,
                PrefabDraftPath = json.prefab_draft_path ?? string.Empty,
                RuntimeBindingNamespace = json.runtime_binding_namespace ?? string.Empty,
            };

            if (json.entries != null)
            {
                for (int i = 0; i < json.entries.Count; i++)
                {
                    HolmasGeneratedBindingViewJson entryJson = json.entries[i] ?? new HolmasGeneratedBindingViewJson();
                    plan.Entries.Add(new HolmasGeneratedBindingView
                    {
                        NodePath = entryJson.node_path ?? string.Empty,
                        ComponentType = entryJson.component_type ?? string.Empty,
                        BindingKey = entryJson.binding_key ?? string.Empty,
                        AssetSlot = entryJson.asset_slot ?? string.Empty,
                        EventName = entryJson.event_name ?? string.Empty,
                        RequiresManualWiring = entryJson.requires_manual_wiring,
                        Notes = entryJson.notes ?? string.Empty,
                    });
                }
            }

            if (json.manual_wiring_node_paths != null)
            {
                plan.ManualWiringNodePaths.AddRange(json.manual_wiring_node_paths);
            }

            return plan;
        }

        public static DesignPacketIntakeAssessment LoadDesignPacketIntakeAssessment()
        {
            DesignPacketIntakeAssessmentJson json = LoadJson<DesignPacketIntakeAssessmentJson>("sample_design_packet_intake_assessment.json");
            var assessment = new DesignPacketIntakeAssessment
            {
                IntakeProfileId = json.intake_profile_id ?? string.Empty,
            };

            if (json.unresolved_items != null)
            {
                for (int i = 0; i < json.unresolved_items.Count; i++)
                {
                    DesignPacketIntakeIssueJson issueJson = json.unresolved_items[i] ?? new DesignPacketIntakeIssueJson();
                    assessment.UnresolvedItems.Add(new DesignPacketIntakeIssue
                    {
                        IssueId = issueJson.issue_id ?? string.Empty,
                        Severity = ParseEnum(issueJson.severity, DesignPacketIntakeIssueSeverity.Info),
                        Kind = ParseEnum(issueJson.kind, DesignPacketIntakeIssueKind.HumanDecisionRequired),
                        FieldPath = issueJson.field_path ?? string.Empty,
                        Summary = issueJson.summary ?? string.Empty,
                        Details = issueJson.details ?? string.Empty,
                        SuggestedResolution = issueJson.suggested_resolution ?? string.Empty,
                        RequiresHumanDecision = issueJson.requires_human_decision,
                    });
                }
            }

            if (json.notes != null)
            {
                assessment.Notes.AddRange(json.notes);
            }

            return assessment;
        }

        private static T LoadJson<T>(string fileName)
        {
            string text = File.ReadAllText(Path.Combine(SampleRoot, fileName));
            T json = JsonUtility.FromJson<T>(text);
            if (json == null)
            {
                throw new InvalidOperationException("无法读取 sample fixture: " + fileName);
            }

            return json;
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
            where TEnum : struct
        {
            TEnum parsed;
            return Enum.TryParse(value, out parsed) ? parsed : fallback;
        }

        [Serializable]
        private sealed class DesignPacketJson
        {
            public string page_id = string.Empty;
            public string page_title = string.Empty;
            public string prefab_name = string.Empty;
            public List<DesignImageReferenceJson> design_images = new List<DesignImageReferenceJson>();
            public List<DesignStateDefinitionJson> states = new List<DesignStateDefinitionJson>();
            public List<DesignRuleDefinitionJson> rules = new List<DesignRuleDefinitionJson>();
            public List<DesignAssetSlotHintJson> asset_slot_hints = new List<DesignAssetSlotHintJson>();
            public string notes = string.Empty;
        }

        [Serializable]
        private sealed class DesignImageReferenceJson
        {
            public string image_id = string.Empty;
            public string image_path = string.Empty;
            public string state_id = string.Empty;
        }

        [Serializable]
        private sealed class DesignStateDefinitionJson
        {
            public string state_id = string.Empty;
            public string description = string.Empty;
        }

        [Serializable]
        private sealed class DesignRuleDefinitionJson
        {
            public string rule_id = string.Empty;
            public string description = string.Empty;
        }

        [Serializable]
        private sealed class DesignAssetSlotHintJson
        {
            public string slot_id = string.Empty;
            public string usage = string.Empty;
        }

        [Serializable]
        private sealed class UiPrefabSpecJson
        {
            public string page_id = string.Empty;
            public string prefab_name = string.Empty;
            public string root_node_id = string.Empty;
            public string generation_profile_id = string.Empty;
            public List<UiNodeSpecJson> nodes = new List<UiNodeSpecJson>();
            public List<UiBindingSpecJson> bindings = new List<UiBindingSpecJson>();
            public List<UiInteractionSpecJson> interactions = new List<UiInteractionSpecJson>();
        }

        [Serializable]
        private sealed class UiNodeSpecJson
        {
            public string node_id = string.Empty;
            public string node_name = string.Empty;
            public string parent_node_id = string.Empty;
            public List<UiComponentSpecJson> components = new List<UiComponentSpecJson>();
            public UiLayoutSpecJson layout = new UiLayoutSpecJson();
        }

        [Serializable]
        private sealed class UiComponentSpecJson
        {
            public string component_type = string.Empty;
            public string binding_key = string.Empty;
            public string asset_slot = string.Empty;
        }

        [Serializable]
        private sealed class UiLayoutSpecJson
        {
            public string layout_type = string.Empty;
            public string layout_slot = string.Empty;
        }

        [Serializable]
        private sealed class UiBindingSpecJson
        {
            public string node_id = string.Empty;
            public string binding_key = string.Empty;
        }

        [Serializable]
        private sealed class UiInteractionSpecJson
        {
            public string node_id = string.Empty;
            public string event_name = string.Empty;
            public string handler_key = string.Empty;
        }

        [Serializable]
        private sealed class PrefabBindingManifestJson
        {
            public string prefab_name = string.Empty;
            public string prefab_draft_path = string.Empty;
            public List<PrefabBindingEntryJson> entries = new List<PrefabBindingEntryJson>();
        }

        [Serializable]
        private sealed class PrefabBindingEntryJson
        {
            public string node_path = string.Empty;
            public string component_type = string.Empty;
            public string binding_key = string.Empty;
            public string asset_slot = string.Empty;
            public string event_name = string.Empty;
            public bool requires_manual_wiring;
            public string notes = string.Empty;
        }

        [Serializable]
        private sealed class HolmasGeneratedResultPlanJson
        {
            public string profile_id = string.Empty;
            public string prefab_name = string.Empty;
            public string prefab_draft_path = string.Empty;
            public string runtime_binding_namespace = string.Empty;
            public List<HolmasGeneratedBindingViewJson> entries = new List<HolmasGeneratedBindingViewJson>();
            public List<string> manual_wiring_node_paths = new List<string>();
        }

        [Serializable]
        private sealed class HolmasGeneratedBindingViewJson
        {
            public string node_path = string.Empty;
            public string component_type = string.Empty;
            public string binding_key = string.Empty;
            public string asset_slot = string.Empty;
            public string event_name = string.Empty;
            public bool requires_manual_wiring;
            public string notes = string.Empty;
        }

        [Serializable]
        private sealed class DesignPacketIntakeAssessmentJson
        {
            public string intake_profile_id = string.Empty;
            public bool has_blocking_issues;
            public List<DesignPacketIntakeIssueJson> unresolved_items = new List<DesignPacketIntakeIssueJson>();
            public List<string> notes = new List<string>();
        }

        [Serializable]
        private sealed class DesignPacketIntakeIssueJson
        {
            public string issue_id = string.Empty;
            public string severity = string.Empty;
            public string kind = string.Empty;
            public string field_path = string.Empty;
            public string summary = string.Empty;
            public string details = string.Empty;
            public string suggested_resolution = string.Empty;
            public bool requires_human_decision;
        }
    }

    internal static class SampleFixtureAssert
    {
        public static void AreEqual(DesignPacketIntakeAssessment actual, DesignPacketIntakeAssessment expected)
        {
            Assert.That(actual.IntakeProfileId, Is.EqualTo(expected.IntakeProfileId));
            Assert.That(actual.HasBlockingIssues, Is.EqualTo(expected.HasBlockingIssues));
            Assert.That(actual.UnresolvedItems.Count, Is.EqualTo(expected.UnresolvedItems.Count));
            Assert.That(actual.Notes, Is.EqualTo(expected.Notes));

            for (int i = 0; i < expected.UnresolvedItems.Count; i++)
            {
                DesignPacketIntakeIssue actualIssue = actual.UnresolvedItems[i];
                DesignPacketIntakeIssue expectedIssue = expected.UnresolvedItems[i];
                Assert.That(actualIssue.IssueId, Is.EqualTo(expectedIssue.IssueId));
                Assert.That(actualIssue.Severity, Is.EqualTo(expectedIssue.Severity));
                Assert.That(actualIssue.Kind, Is.EqualTo(expectedIssue.Kind));
                Assert.That(actualIssue.FieldPath, Is.EqualTo(expectedIssue.FieldPath));
                Assert.That(actualIssue.Summary, Is.EqualTo(expectedIssue.Summary));
                Assert.That(actualIssue.Details, Is.EqualTo(expectedIssue.Details));
                Assert.That(actualIssue.SuggestedResolution, Is.EqualTo(expectedIssue.SuggestedResolution));
                Assert.That(actualIssue.RequiresHumanDecision, Is.EqualTo(expectedIssue.RequiresHumanDecision));
            }
        }

        public static void AreEqual(UiPrefabSpec actual, UiPrefabSpec expected)
        {
            Assert.That(actual.PageId, Is.EqualTo(expected.PageId));
            Assert.That(actual.PrefabName, Is.EqualTo(expected.PrefabName));
            Assert.That(actual.RootNodeId, Is.EqualTo(expected.RootNodeId));
            Assert.That(actual.GenerationProfileId, Is.EqualTo(expected.GenerationProfileId));
            Assert.That(actual.Nodes.Count, Is.EqualTo(expected.Nodes.Count));
            Assert.That(actual.Bindings.Count, Is.EqualTo(expected.Bindings.Count));
            Assert.That(actual.Interactions.Count, Is.EqualTo(expected.Interactions.Count));

            for (int i = 0; i < expected.Nodes.Count; i++)
            {
                UiNodeSpec actualNode = actual.Nodes[i];
                UiNodeSpec expectedNode = expected.Nodes[i];
                Assert.That(actualNode.NodeId, Is.EqualTo(expectedNode.NodeId));
                Assert.That(actualNode.NodeName, Is.EqualTo(expectedNode.NodeName));
                Assert.That(actualNode.ParentNodeId, Is.EqualTo(expectedNode.ParentNodeId));
                Assert.That(actualNode.Layout.LayoutType, Is.EqualTo(expectedNode.Layout.LayoutType));
                Assert.That(actualNode.Layout.LayoutSlot, Is.EqualTo(expectedNode.Layout.LayoutSlot));
                Assert.That(actualNode.Components.Count, Is.EqualTo(expectedNode.Components.Count));

                for (int componentIndex = 0; componentIndex < expectedNode.Components.Count; componentIndex++)
                {
                    UiComponentSpec actualComponent = actualNode.Components[componentIndex];
                    UiComponentSpec expectedComponent = expectedNode.Components[componentIndex];
                    Assert.That(actualComponent.ComponentType, Is.EqualTo(expectedComponent.ComponentType));
                    Assert.That(actualComponent.BindingKey, Is.EqualTo(expectedComponent.BindingKey));
                    Assert.That(actualComponent.AssetSlot, Is.EqualTo(expectedComponent.AssetSlot));
                }
            }

            for (int i = 0; i < expected.Bindings.Count; i++)
            {
                UiBindingSpec actualBinding = actual.Bindings[i];
                UiBindingSpec expectedBinding = expected.Bindings[i];
                Assert.That(actualBinding.NodeId, Is.EqualTo(expectedBinding.NodeId));
                Assert.That(actualBinding.BindingKey, Is.EqualTo(expectedBinding.BindingKey));
            }

            for (int i = 0; i < expected.Interactions.Count; i++)
            {
                UiInteractionSpec actualInteraction = actual.Interactions[i];
                UiInteractionSpec expectedInteraction = expected.Interactions[i];
                Assert.That(actualInteraction.NodeId, Is.EqualTo(expectedInteraction.NodeId));
                Assert.That(actualInteraction.EventName, Is.EqualTo(expectedInteraction.EventName));
                Assert.That(actualInteraction.HandlerKey, Is.EqualTo(expectedInteraction.HandlerKey));
            }
        }

        public static void AreEqual(PrefabBindingManifest actual, PrefabBindingManifest expected)
        {
            Assert.That(actual.PrefabName, Is.EqualTo(expected.PrefabName));
            Assert.That(actual.PrefabDraftPath, Is.EqualTo(expected.PrefabDraftPath));
            Assert.That(actual.Entries.Count, Is.EqualTo(expected.Entries.Count));

            for (int i = 0; i < expected.Entries.Count; i++)
            {
                PrefabBindingEntry actualEntry = actual.Entries[i];
                PrefabBindingEntry expectedEntry = expected.Entries[i];
                Assert.That(actualEntry.NodePath, Is.EqualTo(expectedEntry.NodePath));
                Assert.That(actualEntry.ComponentType, Is.EqualTo(expectedEntry.ComponentType));
                Assert.That(actualEntry.BindingKey, Is.EqualTo(expectedEntry.BindingKey));
                Assert.That(actualEntry.AssetSlot, Is.EqualTo(expectedEntry.AssetSlot));
                Assert.That(actualEntry.EventName, Is.EqualTo(expectedEntry.EventName));
                Assert.That(actualEntry.RequiresManualWiring, Is.EqualTo(expectedEntry.RequiresManualWiring));
                Assert.That(actualEntry.Notes, Is.EqualTo(expectedEntry.Notes));
            }
        }

        public static void AreEqual(HolmasGeneratedResultPlan actual, HolmasGeneratedResultPlan expected)
        {
            Assert.That(actual.ProfileId, Is.EqualTo(expected.ProfileId));
            Assert.That(actual.PrefabName, Is.EqualTo(expected.PrefabName));
            Assert.That(actual.PrefabDraftPath, Is.EqualTo(expected.PrefabDraftPath));
            Assert.That(actual.RuntimeBindingNamespace, Is.EqualTo(expected.RuntimeBindingNamespace));
            Assert.That(actual.ManualWiringNodePaths, Is.EqualTo(expected.ManualWiringNodePaths));
            Assert.That(actual.Entries.Count, Is.EqualTo(expected.Entries.Count));

            for (int i = 0; i < expected.Entries.Count; i++)
            {
                HolmasGeneratedBindingView actualEntry = actual.Entries[i];
                HolmasGeneratedBindingView expectedEntry = expected.Entries[i];
                Assert.That(actualEntry.NodePath, Is.EqualTo(expectedEntry.NodePath));
                Assert.That(actualEntry.ComponentType, Is.EqualTo(expectedEntry.ComponentType));
                Assert.That(actualEntry.BindingKey, Is.EqualTo(expectedEntry.BindingKey));
                Assert.That(actualEntry.AssetSlot, Is.EqualTo(expectedEntry.AssetSlot));
                Assert.That(actualEntry.EventName, Is.EqualTo(expectedEntry.EventName));
                Assert.That(actualEntry.RequiresManualWiring, Is.EqualTo(expectedEntry.RequiresManualWiring));
                Assert.That(actualEntry.Notes, Is.EqualTo(expectedEntry.Notes));
            }
        }
    }
}
