using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Screens.GmTool;
using App.HotUpdate.Holmas.UI.Screens.Tutorial;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holmas.Tests
{
    public sealed class HolmasGmToolTests
    {
        [Test]
        public void GmGestureRecognizer_DoublePentagram_TogglesOnSecondStroke()
        {
            var recognizer = new GmGestureRecognizer();
            List<Vector2> stroke = BuildPentagramStroke(new Vector2(320f, 320f), 180f, 10);

            Assert.That(FeedStroke(recognizer, stroke, 0f), Is.False);
            Assert.That(FeedStroke(recognizer, stroke, 1.5f), Is.True);
        }

        [Test]
        public void GmGestureRecognizer_InvalidStroke_DoesNotToggle()
        {
            var recognizer = new GmGestureRecognizer();
            var stroke = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(80f, 10f),
                new Vector2(160f, 20f),
                new Vector2(240f, 30f),
                new Vector2(320f, 40f),
            };

            Assert.That(FeedStroke(recognizer, stroke, 0f), Is.False);
            Assert.That(FeedStroke(recognizer, stroke, 1.5f), Is.False);
        }

        [Test]
        public void GmToolView_EnsureSurface_CreatesRectTransformUiTree()
        {
            var root = new GameObject("GmToolViewTestRoot", typeof(RectTransform), typeof(GmToolView));
            try
            {
                GmToolView view = root.GetComponent<GmToolView>();

                Assert.DoesNotThrow(() => view.EnsureSurface());
                Assert.That(root.transform.Find("Panel"), Is.TypeOf<RectTransform>());
                Assert.That(root.transform.Find("Panel/Header"), Is.TypeOf<RectTransform>());
                Assert.That(root.transform.Find("Panel/ScrollView/Viewport/Content"), Is.TypeOf<RectTransform>());
                Assert.That(root.transform.Find("Panel/ScrollView/Viewport/Content/QuickActionsCard/QuickActionsRow/StartTutorialButton"), Is.Null);
                Assert.That(root.transform.Find("Panel/ScrollView/Viewport/Content/TutorialCard/StepInputRow/StartAtStepButton"), Is.TypeOf<RectTransform>());
                Assert.That(root.transform.Find("Panel/ScrollView/Viewport/Content/RuntimeCard/RuntimeSummaryText"), Is.TypeOf<RectTransform>());
                Assert.That(root.transform.Find("Panel/ScrollView/Viewport/Content/RuntimeCard/StatusText"), Is.TypeOf<RectTransform>());
                Assert.That(root.transform.Find("Panel/StatusText"), Is.Null, "StatusText 应归入 RuntimeCard，不应残留在 Panel 根级。");
                Assert.That(root.transform.Find("Label"), Is.Null, "GM 工具不应创建根级游离 Label。");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GmToolView_Render_UpdatesRuntimeSummaryAndStatusInsideRuntimeCard()
        {
            var root = new GameObject("GmToolViewRenderTestRoot", typeof(RectTransform), typeof(GmToolView));
            try
            {
                GmToolView view = root.GetComponent<GmToolView>();

                view.Render(new GmToolVm
                {
                    RuntimeSummary = "runtime summary ok",
                    Status = "status ok",
                    TutorialProgressSummary = "tutorial ok",
                    TutorialActionHint = "hint ok",
                });

                TextMeshProUGUI runtimeSummary = root.transform
                    .Find("Panel/ScrollView/Viewport/Content/RuntimeCard/RuntimeSummaryText")
                    ?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI status = root.transform
                    .Find("Panel/ScrollView/Viewport/Content/RuntimeCard/StatusText")
                    ?.GetComponent<TextMeshProUGUI>();

                Assert.That(runtimeSummary, Is.Not.Null);
                Assert.That(status, Is.Not.Null);
                Assert.That(runtimeSummary.text, Is.EqualTo("runtime summary ok"));
                Assert.That(status.text, Is.EqualTo("status ok"));
                Assert.That(root.transform.Find("Panel/StatusText"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialOverlayView_EnsureSurface_DoesNotCreateRootLevelButtonLabels()
        {
            var root = new GameObject("TutorialOverlayViewTestRoot", typeof(RectTransform), typeof(TutorialOverlayView));
            try
            {
                TutorialOverlayView view = root.GetComponent<TutorialOverlayView>();

                Assert.DoesNotThrow(() => view.EnsureSurface());
                Assert.That(root.transform.Find("Label"), Is.Null, "教程 Overlay 不应创建根级游离 Label。");
                Assert.That(root.transform.Find("TutorialCard/NextButton/Label"), Is.TypeOf<RectTransform>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialOverlayView_EnsureSurface_CreatesPassthroughDimMaskBehindGuidance()
        {
            var root = new GameObject("TutorialOverlayViewMaskTestRoot", typeof(RectTransform), typeof(TutorialOverlayView));
            try
            {
                TutorialOverlayView view = root.GetComponent<TutorialOverlayView>();

                Assert.DoesNotThrow(() => view.EnsureSurface());
                Transform dimMask = root.transform.Find("DimMask");
                Transform highlight = root.transform.Find("Highlight");
                Transform card = root.transform.Find("TutorialCard");
                Image dimMaskImage = dimMask != null ? dimMask.GetComponent<Image>() : null;
                Image nextButtonImage = root.transform.Find("TutorialCard/NextButton")?.GetComponent<Image>();

                Assert.That(dimMask, Is.TypeOf<RectTransform>());
                Assert.That(dimMaskImage, Is.Not.Null);
                Assert.That(dimMaskImage.color.a, Is.GreaterThanOrEqualTo(0.45f));
                Assert.That(dimMaskImage.raycastTarget, Is.False, "教程蒙板必须点击穿透，避免手机/编辑器输入被卡死。");
                Assert.That(nextButtonImage, Is.Not.Null);
                Assert.That(nextButtonImage.raycastTarget, Is.True, "教程按钮仍需可点击。");
                Assert.That(dimMask.GetSiblingIndex(), Is.LessThan(highlight.GetSiblingIndex()));
                Assert.That(dimMask.GetSiblingIndex(), Is.LessThan(card.GetSiblingIndex()));
                Assert.That(root.transform.Find("TutorialCard/NextButton/Label"), Is.TypeOf<RectTransform>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialOverlayView_Render_PlacesCardAboveBottomTarget()
        {
            var fixture = CreateTutorialOverlayFixture("TutorialOverlayBottomTargetTestRoot");
            try
            {
                RectTransform target = CreateTarget(fixture.Root.transform, "BottomTarget", new Vector2(-360f, -390f));
                TutorialOverlayView view = fixture.Root.GetComponent<TutorialOverlayView>();

                view.Render(BuildTutorialVm(target));

                RectTransform card = fixture.Root.transform.Find("TutorialCard") as RectTransform;
                Rect targetBounds = GetLocalBounds(fixture.RootRect, target);
                Rect cardBounds = GetLocalBounds(fixture.RootRect, card);
                Assert.That(cardBounds.yMin, Is.GreaterThan(targetBounds.yMax), "目标靠底部时 TutorialCard 应放到目标上方。");
                Assert.That(cardBounds.width, Is.GreaterThan(fixture.RootRect.rect.width * 0.8f), "TutorialCard 应尽量保持默认宽度。");
            }
            finally
            {
                Object.DestroyImmediate(fixture.Canvas);
            }
        }

        [Test]
        public void TutorialOverlayView_Render_KeepsDefaultWidthForNearCenterBottomTarget()
        {
            var fixture = CreateTutorialOverlayFixture("TutorialOverlayNearCenterBottomTargetTestRoot");
            try
            {
                RectTransform target = CreateTarget(fixture.Root.transform, "NearCenterBottomTarget", new Vector2(-90f, -390f));
                TutorialOverlayView view = fixture.Root.GetComponent<TutorialOverlayView>();

                view.Render(BuildTutorialVm(target));

                RectTransform card = fixture.Root.transform.Find("TutorialCard") as RectTransform;
                Rect targetBounds = GetLocalBounds(fixture.RootRect, target);
                Rect cardBounds = GetLocalBounds(fixture.RootRect, card);
                Assert.That(cardBounds.yMin, Is.GreaterThan(targetBounds.yMax), "目标靠底部时 TutorialCard 应放到目标上方。");
                Assert.That(cardBounds.width, Is.GreaterThan(fixture.RootRect.rect.width * 0.8f), "目标接近中心时 TutorialCard 不应缩成半宽。");
            }
            finally
            {
                Object.DestroyImmediate(fixture.Canvas);
            }
        }

        [Test]
        public void TutorialOverlayView_Render_UsesVisibleChildBoundsForZeroSizeBottomTarget()
        {
            var fixture = CreateTutorialOverlayFixture("TutorialOverlayZeroSizeBottomTargetTestRoot");
            try
            {
                RectTransform target = CreateZeroSizeTargetWithVisualChild(
                    fixture.Root.transform,
                    "ZeroSizeWalkToggle",
                    new Vector2(-84f, -390f));
                TutorialOverlayView view = fixture.Root.GetComponent<TutorialOverlayView>();

                view.Render(BuildTutorialVm(target));

                RectTransform card = fixture.Root.transform.Find("TutorialCard") as RectTransform;
                Rect targetBounds = GetLocalBounds(fixture.RootRect, target.Find("Background") as RectTransform);
                Rect cardBounds = GetLocalBounds(fixture.RootRect, card);
                Assert.That(cardBounds.yMin, Is.GreaterThan(targetBounds.yMax), "零尺寸父节点应使用可见子节点 bounds 做上下避让。");
                Assert.That(cardBounds.width, Is.GreaterThan(fixture.RootRect.rect.width * 0.8f), "零尺寸父节点不应导致 TutorialCard 缩成半宽。");
            }
            finally
            {
                Object.DestroyImmediate(fixture.Canvas);
            }
        }

        [Test]
        public void TutorialOverlayView_Render_PlacesCardBelowTopTarget()
        {
            var fixture = CreateTutorialOverlayFixture("TutorialOverlayTopTargetTestRoot");
            try
            {
                RectTransform target = CreateTarget(fixture.Root.transform, "TopTarget", new Vector2(0f, 390f));
                TutorialOverlayView view = fixture.Root.GetComponent<TutorialOverlayView>();

                view.Render(BuildTutorialVm(target));

                RectTransform card = fixture.Root.transform.Find("TutorialCard") as RectTransform;
                Rect targetBounds = GetLocalBounds(fixture.RootRect, target);
                Rect cardBounds = GetLocalBounds(fixture.RootRect, card);
                Assert.That(cardBounds.yMax, Is.LessThan(targetBounds.yMin), "目标靠顶部时 TutorialCard 应放到目标下方。");
            }
            finally
            {
                Object.DestroyImmediate(fixture.Canvas);
            }
        }

        private static bool FeedStroke(GmGestureRecognizer recognizer, List<Vector2> stroke, float startTime)
        {
            recognizer.BeginStroke(startTime, stroke[0]);
            for (int i = 1; i < stroke.Count - 1; i++)
            {
                recognizer.AppendPoint(startTime + i * 0.02f, stroke[i]);
            }

            return recognizer.EndStroke(startTime + stroke.Count * 0.02f, stroke[stroke.Count - 1]);
        }

        private static List<Vector2> BuildPentagramStroke(Vector2 center, float radius, int samplesPerSegment)
        {
            Vector2[] outer = new Vector2[5];
            for (int i = 0; i < outer.Length; i++)
            {
                float angle = Mathf.Deg2Rad * (90f - i * 72f);
                outer[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            int[] starOrder = { 0, 2, 4, 1, 3, 0 };
            var points = new List<Vector2>();
            for (int segmentIndex = 0; segmentIndex < starOrder.Length - 1; segmentIndex++)
            {
                Vector2 from = outer[starOrder[segmentIndex]];
                Vector2 to = outer[starOrder[segmentIndex + 1]];
                for (int i = 0; i < samplesPerSegment; i++)
                {
                    float t = i / (float)samplesPerSegment;
                    points.Add(Vector2.Lerp(from, to, t));
                }
            }

            points.Add(outer[0]);
            return points;
        }

        private static TutorialOverlayVm BuildTutorialVm(RectTransform target)
        {
            return new TutorialOverlayVm
            {
                Title = "引导标题",
                Body = "引导正文",
                NextButtonText = "下一步",
                SkipButtonText = "跳过",
                CollapseButtonText = "收起",
                CanSkip = true,
                TargetRect = target,
            };
        }

        private static TutorialOverlayFixture CreateTutorialOverlayFixture(string rootName)
        {
            var canvas = new GameObject(rootName + "Canvas", typeof(RectTransform));
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 1000f);

            var root = new GameObject(rootName, typeof(RectTransform), typeof(TutorialOverlayView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            root.transform.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            return new TutorialOverlayFixture
            {
                Canvas = canvas,
                Root = root,
                RootRect = rootRect,
            };
        }

        private static RectTransform CreateTarget(Transform parent, string objectName, Vector2 anchoredPosition)
        {
            var targetObject = new GameObject(objectName, typeof(RectTransform));
            targetObject.transform.SetParent(parent, false);
            RectTransform target = targetObject.GetComponent<RectTransform>();
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = new Vector2(160f, 80f);
            return target;
        }

        private static RectTransform CreateZeroSizeTargetWithVisualChild(Transform parent, string objectName, Vector2 anchoredPosition)
        {
            var targetObject = new GameObject(objectName, typeof(RectTransform));
            targetObject.transform.SetParent(parent, false);
            RectTransform target = targetObject.GetComponent<RectTransform>();
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = Vector2.zero;

            var backgroundObject = new GameObject("Background", typeof(RectTransform));
            backgroundObject.transform.SetParent(target, false);
            RectTransform background = backgroundObject.GetComponent<RectTransform>();
            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(168f, 84f);
            return target;
        }

        private static Rect GetLocalBounds(RectTransform root, RectTransform target)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 localPoint = root.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private struct TutorialOverlayFixture
        {
            public GameObject Canvas;
            public GameObject Root;
            public RectTransform RootRect;
        }
    }
}
