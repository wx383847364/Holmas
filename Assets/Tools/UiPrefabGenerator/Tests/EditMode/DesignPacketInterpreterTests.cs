using NUnit.Framework;
using UiPrefabGenerator.Core.Intake;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class DesignPacketInterpreterTests
    {
        [Test]
        public void Interpret_SampleFixtureMatchesGoldenSpec()
        {
            DesignPacket packet = SampleFixtureLoader.LoadDesignPacket();
            UiPrefabSpec expected = SampleFixtureLoader.LoadUiPrefabSpec();

            UiPrefabSpec spec = new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet);

            SampleFixtureAssert.AreEqual(spec, expected);
        }

        [Test]
        public void Interpret_ThrowsWhenBlockingIssuesRemain()
        {
            var packet = new DesignPacket
            {
                PrefabName = "AgencyMainPanel",
            };

            Assert.That(
                () => new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet),
                Throws.InvalidOperationException);
        }

        [Test]
        public void Interpret_TaskListScrollableRule_AddsScrollableTaskListNode()
        {
            var packet = new DesignPacket
            {
                PageId = "agency_main",
                PageTitle = "Agency Main",
                PrefabName = "AgencyMainPanel",
                DesignImages =
                {
                    new DesignImageReference
                    {
                        ImageId = "default",
                        ImagePath = "Design/AgencyMain/default.png",
                        StateId = "default",
                    },
                },
                States =
                {
                    new DesignStateDefinition
                    {
                        StateId = "default",
                        Description = "default state",
                    },
                },
                Rules =
                {
                    new DesignRuleDefinition
                    {
                        RuleId = "task_list_scrollable",
                        Description = "task list should be scrollable",
                    },
                },
            };

            UiPrefabSpec spec = new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet);

            Assert.That(
                spec.Nodes,
                Has.Some.Matches<UiNodeSpec>(
                    node => node.NodeId == "task_list" &&
                            node.ParentNodeId == "root" &&
                            node.Layout != null &&
                            node.Layout.LayoutType == "VerticalLayout" &&
                            node.Components.Exists(
                                component => component != null &&
                                             component.ComponentType == "ScrollRect" &&
                                             component.BindingKey == "task_list")));
        }

        [Test]
        public void Interpret_ClaimButtonClickableRule_AddsButtonNodeAndInteraction()
        {
            var packet = new DesignPacket
            {
                PageId = "agency_main",
                PageTitle = "Agency Main",
                PrefabName = "AgencyMainPanel",
                DesignImages =
                {
                    new DesignImageReference
                    {
                        ImageId = "default",
                        ImagePath = "Design/AgencyMain/default.png",
                        StateId = "default",
                    },
                },
                States =
                {
                    new DesignStateDefinition
                    {
                        StateId = "default",
                        Description = "default state",
                    },
                },
                Rules =
                {
                    new DesignRuleDefinition
                    {
                        RuleId = "claim_button_clickable",
                        Description = "claim button should expose click event",
                    },
                },
            };

            UiPrefabSpec spec = new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet);

            Assert.That(
                spec.Nodes,
                Has.Some.Matches<UiNodeSpec>(
                    node => node.NodeId == "claim_button" &&
                            node.ParentNodeId == "root" &&
                            node.Components.Exists(
                                component => component != null &&
                                             component.ComponentType == "Button" &&
                                             component.BindingKey == "claim_button")));
            Assert.That(
                spec.Interactions,
                Has.Some.Matches<UiInteractionSpec>(
                    interaction => interaction.NodeId == "claim_button" &&
                                   interaction.EventName == "on_click" &&
                                   interaction.HandlerKey == "claim_task"));
        }

        [Test]
        public void Interpret_ClaimButtonVisualizedRule_AddsImageComponentToClaimButton()
        {
            var packet = new DesignPacket
            {
                PageId = "agency_main",
                PageTitle = "Agency Main",
                PrefabName = "AgencyMainPanel",
                DesignImages =
                {
                    new DesignImageReference
                    {
                        ImageId = "default",
                        ImagePath = "Design/AgencyMain/default.png",
                        StateId = "default",
                    },
                },
                States =
                {
                    new DesignStateDefinition
                    {
                        StateId = "default",
                        Description = "default state",
                    },
                },
                Rules =
                {
                    new DesignRuleDefinition
                    {
                        RuleId = "claim_button_visualized",
                        Description = "claim button should have a visible background",
                    },
                },
            };

            UiPrefabSpec spec = new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet);

            Assert.That(
                spec.Nodes,
                Has.Some.Matches<UiNodeSpec>(
                    node => node.NodeId == "claim_button" &&
                            node.Components.Count == 3 &&
                            node.Components[1].ComponentType == "Image" &&
                            node.Components[1].AssetSlot == "claim_button_bg" &&
                            node.Components[2].ComponentType == "Button" &&
                            node.Components[2].BindingKey == "claim_button"));
            Assert.That(spec.Interactions, Is.Empty);
        }

        [Test]
        public void Interpret_HighValueRules_AddsTitlePrimaryButtonAndNumericDisplay()
        {
            var packet = new DesignPacket
            {
                PageId = "agency_main",
                PageTitle = "Agency Main",
                PrefabName = "AgencyMainPanel",
                DesignImages =
                {
                    new DesignImageReference
                    {
                        ImageId = "default",
                        ImagePath = "Design/AgencyMain/default.png",
                        StateId = "default",
                    },
                },
                States =
                {
                    new DesignStateDefinition
                    {
                        StateId = "default",
                        Description = "default state",
                    },
                },
                Rules =
                {
                    new DesignRuleDefinition { RuleId = "panel_background", Description = "panel background" },
                    new DesignRuleDefinition { RuleId = "title_text", Description = "title text" },
                    new DesignRuleDefinition { RuleId = "primary_button", Description = "primary button" },
                    new DesignRuleDefinition { RuleId = "numeric_value_display", Description = "numeric value" },
                },
                AssetSlotHints =
                {
                    new DesignAssetSlotHint
                    {
                        SlotId = "panel_bg",
                        Usage = "background",
                    },
                },
                ElementHints =
                {
                    new DesignElementHint
                    {
                        HintId = "title",
                        SemanticRole = "title_text",
                        SuggestedNodeId = "title_text",
                        LayoutSlot = "title_text",
                        Confidence = 0.8f,
                    },
                    new DesignElementHint
                    {
                        HintId = "primary",
                        SemanticRole = "primary_button",
                        SuggestedNodeId = "primary_button",
                        LayoutSlot = "primary_button",
                        AssetSlot = "primary_button_bg",
                        Confidence = 0.85f,
                    },
                    new DesignElementHint
                    {
                        HintId = "numeric",
                        SemanticRole = "numeric_value_display",
                        SuggestedNodeId = "numeric_value_display",
                        LayoutSlot = "numeric_value_display",
                        Confidence = 0.76f,
                    },
                },
            };

            UiPrefabSpec spec = new DefaultDesignPacketToUiPrefabSpecInterpreter().Interpret(packet);

            Assert.That(spec.Nodes, Has.Some.Matches<UiNodeSpec>(node => node.NodeId == "title_text"));
            Assert.That(spec.Nodes, Has.Some.Matches<UiNodeSpec>(node => node.NodeId == "primary_button"));
            Assert.That(spec.Nodes, Has.Some.Matches<UiNodeSpec>(node => node.NodeId == "numeric_value_display"));
            Assert.That(spec.Bindings, Has.Some.Matches<UiBindingSpec>(binding => binding.NodeId == "title_text" && binding.BindingKey == "title_text"));
            Assert.That(spec.Bindings, Has.Some.Matches<UiBindingSpec>(binding => binding.NodeId == "numeric_value_display" && binding.BindingKey == "numeric_value"));
            Assert.That(spec.Interactions, Has.Some.Matches<UiInteractionSpec>(interaction => interaction.NodeId == "primary_button" && interaction.HandlerKey == "primary_action"));
        }
    }
}
