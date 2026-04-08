using NUnit.Framework;
using UiPrefabGenerator.Core.ResourceMatch;
using UiPrefabGenerator.Core.Result;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Editor.Window;
using UnityEditor;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiGenerationAnalysisStatusSummarizerTests
    {
        [Test]
        public void Build_ReturnsFailed_WhenAnalysisMarkedFailed()
        {
            UiGenerationAnalysisResult result = CreateBaseResult();
            result.Success = false;
            result.Errors.Add("analysis failed");

            UiGenerationAnalysisStatusSummary summary = UiGenerationAnalysisStatusSummarizer.Build(result);

            Assert.That(summary.StateKind, Is.EqualTo(UiGenerationAnalysisStateKind.Failed));
            Assert.That(summary.StatusLabel, Is.EqualTo("失败"));
            Assert.That(summary.StatusMessageType, Is.EqualTo(MessageType.Error));
            Assert.That(summary.ErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void Build_ReturnsSuccessWithUnresolvedItems_WhenSuccessContainsUnresolved()
        {
            UiGenerationAnalysisResult result = CreateBaseResult();
            result.UnresolvedItems.Add("resource_slot:panel_bg");
            result.ResourceMatchReport.UnresolvedSlots.Add("panel_bg");

            UiGenerationAnalysisStatusSummary summary = UiGenerationAnalysisStatusSummarizer.Build(result);

            Assert.That(summary.StateKind, Is.EqualTo(UiGenerationAnalysisStateKind.SuccessWithUnresolvedItems));
            Assert.That(summary.StatusLabel, Is.EqualTo("成功但有未解决项"));
            Assert.That(summary.UnresolvedItemCount, Is.EqualTo(1));
            Assert.That(summary.UnresolvedSlotCount, Is.EqualTo(1));
            Assert.That(summary.UnresolvedSummaries, Has.Member("resource_slot:panel_bg"));
            Assert.That(summary.UnresolvedSummaries, Has.Member("未匹配资源槽：panel_bg"));
        }

        [Test]
        public void Build_MarksWeakResult_WhenCoverageAndSelectionAreThin()
        {
            UiGenerationAnalysisResult result = CreateBaseResult();
            result.UiPrefabSpec.Nodes.Clear();
            result.UiPrefabSpec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "root",
                NodeName = "Root"
            });
            result.ResourceMatchReport.Matches.Clear();
            result.ResourceMatchReport.Matches.Add(new UiAssetSlotMatch
            {
                AssetSlot = "panel_bg",
                ComponentType = "Image",
                SelectedAssetPath = string.Empty,
                Confidence = 0.1f
            });
            result.ResourceMatchReport.UnresolvedSlots.Add("panel_bg");

            UiGenerationAnalysisStatusSummary summary = UiGenerationAnalysisStatusSummarizer.Build(result);

            Assert.That(summary.IsWeakResult, Is.True);
            Assert.That(summary.WeakResultSummary, Does.Contain("仅识别到 1 个节点"));
            Assert.That(summary.WeakResultSummary, Does.Contain("0/1 个资源槽完成自动选中"));
            Assert.That(summary.WeakResultSummary, Does.Contain("存在 1 个未匹配资源槽"));
        }

        [Test]
        public void Build_ReturnsCleanSuccess_WhenAnalysisIsComplete()
        {
            UiGenerationAnalysisResult result = CreateBaseResult();
            result.UiPrefabSpec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "title",
                NodeName = "Title",
                ParentNodeId = "root"
            });
            result.UiPrefabSpec.Bindings.Add(new UiBindingSpec
            {
                NodeId = "title",
                BindingKey = "title_text"
            });
            result.ResourceMatchReport.Matches.Add(new UiAssetSlotMatch
            {
                AssetSlot = "panel_bg",
                ComponentType = "Image",
                SelectedAssetPath = "Assets/Res/Textures/panel_bg.png",
                Confidence = 0.92f
            });

            UiGenerationAnalysisStatusSummary summary = UiGenerationAnalysisStatusSummarizer.Build(result);

            Assert.That(summary.StateKind, Is.EqualTo(UiGenerationAnalysisStateKind.Success));
            Assert.That(summary.StatusLabel, Is.EqualTo("成功"));
            Assert.That(summary.IsWeakResult, Is.False);
            Assert.That(summary.StatusMessageType, Is.EqualTo(MessageType.Info));
            Assert.That(summary.SelectedMatchCount, Is.EqualTo(1));
        }

        [Test]
        public void Build_IncludesReviewIssuesAndPreviewCounts()
        {
            UiGenerationAnalysisResult result = CreateBaseResult();
            result.VisualUnderstanding = new VisualUnderstandingBundle
            {
                ConfidenceSummary = new VisualConfidenceSummary
                {
                    LowConfidenceElementCount = 2,
                },
            };
            result.VisualReviewReport = new VisualReviewReport
            {
                Issues =
                {
                    new VisualReviewIssue
                    {
                        IssueId = "blocking_role",
                        Severity = "blocking",
                        Summary = "missing primary button",
                    },
                    new VisualReviewIssue
                    {
                        IssueId = "warning_text",
                        Severity = "warning",
                        Summary = "title text needs review",
                    },
                },
            };
            result.PreviewRenderPlan = new PreviewRenderPlan
            {
                Nodes =
                {
                    new PreviewRenderNode { NodeId = "title" },
                    new PreviewRenderNode { NodeId = "button" },
                },
            };
            result.PreviewDiffReport = new PreviewDiffReport
            {
                Regions =
                {
                    new PreviewDiffRegion { RegionId = "diff1" },
                },
            };

            UiGenerationAnalysisStatusSummary summary = UiGenerationAnalysisStatusSummarizer.Build(result);

            Assert.That(summary.BlockingReviewIssueCount, Is.EqualTo(1));
            Assert.That(summary.ReviewIssueCount, Is.EqualTo(2));
            Assert.That(summary.LowConfidenceElementCount, Is.EqualTo(2));
            Assert.That(summary.PreviewNodeCount, Is.EqualTo(2));
            Assert.That(summary.PreviewDiffRegionCount, Is.EqualTo(1));
            Assert.That(summary.UnresolvedSummaries, Has.Member("blocking：missing primary button"));
            Assert.That(summary.WarningSummaries, Has.Member("review：title text needs review"));
        }

        private static UiGenerationAnalysisResult CreateBaseResult()
        {
            var result = new UiGenerationAnalysisResult
            {
                TaskId = "analysis_status_test",
                Success = true,
                UiPrefabSpec = new UiPrefabSpec
                {
                    PageId = "test_page",
                    PrefabName = "TestPanel",
                    RootNodeId = "root"
                },
                ResourceMatchReport = new UiResourceMatchReport()
            };

            result.UiPrefabSpec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "root",
                NodeName = "Root"
            });
            return result;
        }
    }
}
