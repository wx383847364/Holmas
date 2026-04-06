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
    }
}
