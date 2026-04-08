using NUnit.Framework;
using UiPrefabGenerator.Core.Intake;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class DesignPacketIntakeAnalyzerTests
    {
        [Test]
        public void Analyze_SampleFixtureMatchesGoldenAssessment()
        {
            DesignPacket packet = SampleFixtureLoader.LoadDesignPacket();
            DesignPacketIntakeAssessment expected = SampleFixtureLoader.LoadDesignPacketIntakeAssessment();

            DesignPacketIntakeAssessment assessment = new DefaultDesignPacketIntakeAnalyzer().Analyze(packet);

            SampleFixtureAssert.AreEqual(assessment, expected);
        }

        [Test]
        public void Analyze_FailsWhenImageStateDoesNotExist()
        {
            DesignPacket packet = SampleFixtureLoader.LoadDesignPacket();
            packet.DesignImages[0].StateId = "missing";

            DesignPacketIntakeAssessment assessment = new DefaultDesignPacketIntakeAnalyzer().Analyze(packet);

            Assert.That(assessment.HasBlockingIssues, Is.True);
            Assert.That(
                assessment.UnresolvedItems,
                Has.Some.Matches<DesignPacketIntakeIssue>(
                    issue => issue.Kind == DesignPacketIntakeIssueKind.AmbiguousImageStateMapping));
        }

        [Test]
        public void Analyze_SupportedScrollableRule_DoesNotReportUnsupportedRule()
        {
            DesignPacket packet = SampleFixtureLoader.LoadDesignPacket();

            DesignPacketIntakeAssessment assessment = new DefaultDesignPacketIntakeAnalyzer().Analyze(packet);

            Assert.That(
                assessment.UnresolvedItems,
                Has.None.Matches<DesignPacketIntakeIssue>(
                    issue => issue.Kind == DesignPacketIntakeIssueKind.UnsupportedRule));
        }

        [Test]
        public void Analyze_SupportedVisualizedRule_DoesNotReportUnsupportedRule()
        {
            DesignPacket packet = SampleFixtureLoader.LoadDesignPacket();
            packet.Rules.Add(new DesignRuleDefinition
            {
                RuleId = "claim_button_visualized",
                Description = "claim button should have a visible background",
            });

            DesignPacketIntakeAssessment assessment = new DefaultDesignPacketIntakeAnalyzer().Analyze(packet);

            Assert.That(
                assessment.UnresolvedItems,
                Has.None.Matches<DesignPacketIntakeIssue>(
                    issue => issue.Kind == DesignPacketIntakeIssueKind.UnsupportedRule &&
                             issue.FieldPath == "rules[3].rule_id"));
        }

        [Test]
        public void Analyze_ElementHintsProduceStructuredIssues()
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
                ExpectedSemanticRoles =
                {
                    "title_text",
                    "primary_button",
                },
                ElementHints =
                {
                    new DesignElementHint
                    {
                        HintId = "title",
                        SemanticRole = "title_text",
                        DisplayText = "Agency Main",
                        Confidence = 0.62f,
                        Bounds = new UiRect
                        {
                            X = 0.1f,
                            Y = 0.1f,
                            Width = 0.8f,
                            Height = 0.08f,
                        },
                    },
                    new DesignElementHint
                    {
                        HintId = "duplicate_title",
                        SemanticRole = "title_text",
                        DisplayText = "Agency Main Duplicate",
                        Confidence = 0.85f,
                        Bounds = new UiRect
                        {
                            X = 0.1f,
                            Y = 0.22f,
                            Width = 0.8f,
                            Height = 0.08f,
                        },
                    },
                },
            };

            DesignPacketIntakeAssessment assessment = new DefaultDesignPacketIntakeAnalyzer().Analyze(packet);

            Assert.That(
                assessment.UnresolvedItems,
                Has.Some.Matches<DesignPacketIntakeIssue>(issue => issue.Kind == DesignPacketIntakeIssueKind.LowConfidenceEvidence));
            Assert.That(
                assessment.UnresolvedItems,
                Has.Some.Matches<DesignPacketIntakeIssue>(issue => issue.Kind == DesignPacketIntakeIssueKind.MissingCriticalControl));
            Assert.That(
                assessment.UnresolvedItems,
                Has.Some.Matches<DesignPacketIntakeIssue>(issue => issue.Kind == DesignPacketIntakeIssueKind.SemanticConflict));
        }
    }
}
