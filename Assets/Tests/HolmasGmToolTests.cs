using System.Collections.Generic;
using App.HotUpdate.Holmas.UI.Screens.GmTool;
using NUnit.Framework;
using UnityEngine;

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
            }
            finally
            {
                Object.DestroyImmediate(root);
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
    }
}
