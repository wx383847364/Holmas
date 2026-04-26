using System.Collections.Generic;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Screens.GmTool
{
    public struct GmGestureSample
    {
        public Vector2 Position;
        public float Time;
    }

    public sealed class GmGestureRecognizer
    {
        public const float DoubleStarWindowSeconds = 2.5f;

        private const float SampleDistance = 14f;
        private const float MinimumStrokeExtent = 80f;
        private const float MinimumStrokeLongestSide = 120f;

        private readonly List<GmGestureSample> _stroke = new List<GmGestureSample>();
        private int _recognizedStarCount;
        private float _lastRecognizedStarTime = -999f;
        private bool _isDrawing;

        public void BeginStroke(float time, Vector2 position)
        {
            ExpireSequenceIfNeeded(time);
            _stroke.Clear();
            _stroke.Add(new GmGestureSample
            {
                Position = position,
                Time = time,
            });
            _isDrawing = true;
        }

        public void AppendPoint(float time, Vector2 position)
        {
            if (!_isDrawing)
            {
                return;
            }

            if (_stroke.Count > 0 &&
                Vector2.Distance(_stroke[_stroke.Count - 1].Position, position) < SampleDistance)
            {
                return;
            }

            _stroke.Add(new GmGestureSample
            {
                Position = position,
                Time = time,
            });
        }

        public bool EndStroke(float time, Vector2 position)
        {
            if (!_isDrawing)
            {
                return false;
            }

            AppendPoint(time, position);
            _isDrawing = false;

            bool isStar = IsStarStroke(_stroke);
            _stroke.Clear();
            if (!isStar)
            {
                ExpireSequenceIfNeeded(time, forceReset: true);
                return false;
            }

            if (_recognizedStarCount > 0 && time - _lastRecognizedStarTime <= DoubleStarWindowSeconds)
            {
                _recognizedStarCount = 0;
                _lastRecognizedStarTime = -999f;
                return true;
            }

            _recognizedStarCount = 1;
            _lastRecognizedStarTime = time;
            return false;
        }

        private void ExpireSequenceIfNeeded(float time, bool forceReset = false)
        {
            if (forceReset || (_recognizedStarCount > 0 && time - _lastRecognizedStarTime > DoubleStarWindowSeconds))
            {
                _recognizedStarCount = 0;
                _lastRecognizedStarTime = -999f;
            }
        }

        private static bool IsStarStroke(List<GmGestureSample> samples)
        {
            if (samples == null || samples.Count < 10)
            {
                return false;
            }

            Rect bounds = BuildBounds(samples);
            float minExtent = Mathf.Min(bounds.width, bounds.height);
            float maxExtent = Mathf.Max(bounds.width, bounds.height);
            if (minExtent < MinimumStrokeExtent || maxExtent < MinimumStrokeLongestSide)
            {
                return false;
            }

            Vector2 start = samples[0].Position;
            Vector2 end = samples[samples.Count - 1].Position;
            if (Vector2.Distance(start, end) > maxExtent * 0.35f)
            {
                return false;
            }

            List<Vector2> simplified = Simplify(samples, Mathf.Max(18f, maxExtent * 0.08f));
            if (simplified.Count < 5)
            {
                return false;
            }

            if (Vector2.Distance(simplified[0], simplified[simplified.Count - 1]) <= maxExtent * 0.2f)
            {
                simplified.RemoveAt(simplified.Count - 1);
            }

            if (simplified.Count < 5 || simplified.Count > 6)
            {
                return false;
            }

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < simplified.Count; i++)
            {
                centroid += simplified[i];
            }

            centroid /= simplified.Count;
            if (!HasConsistentOuterRadius(simplified, centroid))
            {
                return false;
            }

            return CountSelfIntersections(simplified) >= 4;
        }

        private static Rect BuildBounds(List<GmGestureSample> samples)
        {
            Vector2 min = samples[0].Position;
            Vector2 max = min;
            for (int i = 1; i < samples.Count; i++)
            {
                min = Vector2.Min(min, samples[i].Position);
                max = Vector2.Max(max, samples[i].Position);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static List<Vector2> Simplify(List<GmGestureSample> samples, float epsilon)
        {
            var source = new List<Vector2>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                source.Add(samples[i].Position);
            }

            return SimplifyRdp(source, epsilon);
        }

        private static List<Vector2> SimplifyRdp(List<Vector2> points, float epsilon)
        {
            if (points == null || points.Count < 3)
            {
                return points != null ? new List<Vector2>(points) : new List<Vector2>();
            }

            int index = -1;
            float distanceMax = 0f;
            Vector2 start = points[0];
            Vector2 end = points[points.Count - 1];
            for (int i = 1; i < points.Count - 1; i++)
            {
                float distance = DistanceToSegment(points[i], start, end);
                if (distance > distanceMax)
                {
                    distanceMax = distance;
                    index = i;
                }
            }

            if (distanceMax <= epsilon || index < 0)
            {
                return new List<Vector2> { start, end };
            }

            List<Vector2> firstHalf = SimplifyRdp(points.GetRange(0, index + 1), epsilon);
            List<Vector2> secondHalf = SimplifyRdp(points.GetRange(index, points.Count - index), epsilon);
            firstHalf.RemoveAt(firstHalf.Count - 1);
            firstHalf.AddRange(secondHalf);
            return firstHalf;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float t = Vector2.Dot(point - start, segment) / segment.sqrMagnitude;
            t = Mathf.Clamp01(t);
            Vector2 projection = start + segment * t;
            return Vector2.Distance(point, projection);
        }

        private static bool HasConsistentOuterRadius(List<Vector2> points, Vector2 centroid)
        {
            float minRadius = float.PositiveInfinity;
            float maxRadius = 0f;
            float totalRadius = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float radius = Vector2.Distance(points[i], centroid);
                minRadius = Mathf.Min(minRadius, radius);
                maxRadius = Mathf.Max(maxRadius, radius);
                totalRadius += radius;
            }

            if (minRadius <= Mathf.Epsilon)
            {
                return false;
            }

            float averageRadius = totalRadius / points.Count;
            return averageRadius >= 40f && maxRadius / minRadius <= 1.65f;
        }

        private static int CountSelfIntersections(List<Vector2> points)
        {
            int count = 0;
            int segmentCount = points.Count;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 a1 = points[i];
                Vector2 a2 = points[(i + 1) % segmentCount];
                for (int j = i + 1; j < segmentCount; j++)
                {
                    if (Mathf.Abs(i - j) <= 1 || (i == 0 && j == segmentCount - 1))
                    {
                        continue;
                    }

                    Vector2 b1 = points[j];
                    Vector2 b2 = points[(j + 1) % segmentCount];
                    if (SegmentsIntersect(a1, a2, b1, b2))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float d1 = Cross(a2 - a1, b1 - a1);
            float d2 = Cross(a2 - a1, b2 - a1);
            float d3 = Cross(b2 - b1, a1 - b1);
            float d4 = Cross(b2 - b1, a2 - b1);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                   ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross(Vector2 lhs, Vector2 rhs)
        {
            return lhs.x * rhs.y - lhs.y * rhs.x;
        }
    }
}
