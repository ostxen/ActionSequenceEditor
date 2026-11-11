using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public sealed class TimelineGeometryTests
    {
        [TestCase(500, 232, 0, 7)]
        [TestCase(1000, 320, 1, 8)]
        [TestCase(1400, 220, 12, 240)]
        public void TransformMatchesExistingFormulaAndRoundTrips(float width, float left, float minimum, float maximum)
        {
            var transform = new JsonTimelineTransform(minimum, maximum - minimum, width - left, left);
            foreach (var time in new[] { 0f, .25f, minimum, maximum, maximum + 3 })
            {
                var expected = left + (time - minimum) / (maximum - minimum) * (width - left);
                Assert.That(transform.X(time), Is.EqualTo(expected).Within(.0001f));
                Assert.That(transform.Time(expected), Is.EqualTo(time).Within(.0001f));
            }
            Assert.That(transform.Duration(width - left), Is.EqualTo(maximum - minimum));
        }
        [Test] public void VisibleRowsHideCollapsedTracksWithoutChangingDocument()
        {
            var asset = new LegacyExperienceAsset();
            var json = Storage.JsonDocumentSession.Encode(asset);
            var rows = TimelineGeometry.VisibleRows(asset);
            Assert.That(rows.Count, Is.EqualTo(8));
            Assert.That(rows.Take(4), Is.EqualTo(new IDirectable[] { asset.groups[0], asset.groups[0].Tracks[0], asset.groups[0].Tracks[1], asset.groups[0].Tracks[2] }));
            Assert.That(TimelineGeometry.IsVisible(asset.groups[2].Tracks[0].Clips[0]), Is.False);
            Assert.That(Storage.JsonDocumentSession.Encode(asset), Is.EqualTo(json));
            asset.groups[0].IsCollapsed = true;
            Assert.That(TimelineGeometry.VisibleRows(asset).Count, Is.EqualTo(5));
        }
        [Test] public void ShortClipsPointsScrollAndHandlePriorityAreExplicit()
        {
            var transform = new JsonTimelineTransform(0, 7, 700, 200);
            var clip = new LegacyExperienceInterval { StartTime = 1, Length = .01f };
            var rect = transform.ClipRect(clip, 28 - 14, 26, 8, 4);
            Assert.That(rect, Is.EqualTo(new Rect(300, 14, 4, 26)));
            Assert.That(TimelineGeometry.ResizeEdge(rect, rect.center, 6), Is.EqualTo(-1));
            Assert.That(TimelineGeometry.ResizeEdge(rect, new Vector2(310, 20), 6), Is.Zero);
            var marker = transform.ClipRect(new LegacyExperienceMarker { StartTime = 1 }, 14, 26, 8, 4);
            Assert.That(marker, Is.EqualTo(new Rect(296, 14, 8, 26)));
            Assert.That(TimelineGeometry.RowAt(14, 14, 28), Is.EqualTo(1));
            Assert.That(TimelineGeometry.Overlaps(rect, new Rect(310, 45, -15, -35)), Is.True);
        }
    }
}
