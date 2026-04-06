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
    }
}
