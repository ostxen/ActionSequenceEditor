using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Domain;
using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public sealed class TimelineGeometryTests
    {
        private readonly TimelineWorkspace workspace = new TimelineWorkspace();
        [SetUp] public void ResetWorkspace() { workspace.SetRange(0, 7); }
        [TestCase(0f)] [TestCase(.25f)] [TestCase(.75f)] [TestCase(1f)]
        public void ZoomKeepsHoveredTimeFixedAtEveryAnchorAndMinimumSpan(float fraction)
        {
            foreach (var wheel in new[] { -120f, 120f })
            {
                var range = TimelineNavigation.ZoomAt(1, 8, wheel, 1000, fraction, true);
                Assert.That(Mathf.Lerp(range.x, range.y, fraction), Is.EqualTo(1 + 7 * fraction).Within(.00001f));
                Assert.That(range.y - range.x, Is.EqualTo(wheel > 0 ? 8.575f : 5.425f).Within(.00001f));
            }
            var min = TimelineNavigation.ZoomAt(0, .25f, -1, 500, fraction, true);
            Assert.That(min.y - min.x, Is.EqualTo(.25f));
            Assert.That(TimelineNavigation.ZoomAt(1, 8, 1, 1000, fraction, false), Is.EqualTo(new Vector2(1, 8)));
            var asset = new LegacyExperienceAsset();
            TimelineNavigation.SetRange(workspace, new Vector2(-1, 9));
            Assert.That(workspace.ViewTimeMin, Is.EqualTo(-1)); Assert.That(workspace.ViewTimeMax, Is.EqualTo(9));
        }

        [Test] public void TrackDropResolvesBeforeAfterGroupAndRootFromScrolledCoordinates()
        {
            var asset = new LegacyExperienceAsset(); var moving = asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1];
            var before = TimelineGeometry.TrackDrop(asset, moving, new Vector2(90, 31), 0, 1000, 600, 28, workspace);
            var after = TimelineGeometry.TrackDrop(asset, moving, new Vector2(90, 53), 0, 1000, 600, 28, workspace);
            Assert.That(before.Group, Is.SameAs(asset.RootItems.OfType<TimelineGroup>().ElementAt(0))); Assert.That(before.Index, Is.Zero);
            Assert.That(after.Index, Is.EqualTo(1));
            var scrolled = TimelineGeometry.TrackDrop(asset, moving, new Vector2(90, 3), 28, 1000, 600, 28, workspace);
            Assert.That(scrolled.Index, Is.Zero); Assert.That(scrolled.Y, Is.Zero);
            var root = TimelineGeometry.TrackDrop(asset, moving, new Vector2(5, 31), 0, 1000, 600, 28, workspace);
            Assert.That(root.IsValid, Is.True); Assert.That(root.Group, Is.Null);
            Assert.That(root.Y, Is.EqualTo(TimelineGeometry.VisibleRows(asset, workspace).Count * 28));
            Assert.That(TimelineGeometry.TrackDrop(asset, moving, new Vector2(90, 601), 0, 1000, 600, 28, workspace).IsValid, Is.False);
            Assert.That(TimelineGeometry.TrackDrop(asset, moving, new Vector2(-1, 31), 0, 1000, 600, 28, workspace).IsValid, Is.False);
        }
        [TestCase(120, 9.575f)]
        [TestCase(1, 9.575f)]
        [TestCase(-120, 6.425f)]
        [TestCase(0, 8)]
        public void NavigationUsesChosen225StepAndRetainsRangeAndPreferenceRules(float wheel, float expected)
        {
            Assert.That(TimelineNavigation.ZoomMaximum(1, 8, wheel, 1000, true), Is.EqualTo(expected).Within(.00001f));
            Assert.That(TimelineNavigation.ZoomMaximum(1, 8, wheel, 1000, false), Is.EqualTo(8));
            Assert.That(TimelineNavigation.ZoomMaximum(1, 242, 1, 1000, true), Is.EqualTo(242));
            Assert.That(TimelineNavigation.Pan(1, 8, 200, 700, true), Is.EqualTo(new Vector2(0, 7)));
            Assert.That(TimelineNavigation.Pan(1, 8, 200, 700, false), Is.EqualTo(new Vector2(0, 6)));
            Assert.That(TimelineNavigation.Pan(0, 7, 200, 700, false), Is.EqualTo(new Vector2(0, 7)));
        }
        [Test] public void GestureCandidatesPreserveSnapOrderingThresholdsAndSnapshotBounds()
        {
            Assert.That(TimelineCandidates.Magnet(1, 0, new[] { 1.25f }, .25f, true), Is.EqualTo(1.25f));
            Assert.That(TimelineCandidates.Magnet(1, 0, new[] { 1.25f }, .25f, false), Is.EqualTo(1));
            // Equal start/end distances: original favors the start, native keeps the first reference.
            Assert.That(TimelineCandidates.Magnet(1, 2, new[] { 3.25f, .75f }, .5f, true), Is.EqualTo(.75f));
            Assert.That(TimelineCandidates.Magnet(1, 2, new[] { 3.25f, .75f }, .5f, false), Is.EqualTo(1.25f));
            Assert.That(TimelineCandidates.ClampMoveDelta(-4, new[] { 1f, 3f }), Is.EqualTo(-1));
            var initial = new Vector2(1, 2);
            Assert.That(TimelineCandidates.Resize(initial, true, -5, .1f, .5f, 2.9f, value => value), Is.EqualTo(new Vector2(.5f, 2.5f)));
            Assert.That(TimelineCandidates.Resize(initial, false, 8, .1f, 1.1f, 4, value => value), Is.EqualTo(new Vector2(1, 3)));
            Assert.That(TimelineCandidates.Resize(initial, true, 4, .01f, 0, float.PositiveInfinity).y, Is.EqualTo(.01f).Within(.00001f));
            // A short original clip has inverted limits; keep the original upper-bound-first rule.
            Assert.That(TimelineCandidates.Resize(new Vector2(0, .01f), true, 0, .1f, 0, -.09f, value => value).x, Is.EqualTo(-.09f));
        }
        [Test] public void SnapReferencesExcludeSelectedAndRebuildWithoutChangingInput()
        {
            var first = new LegacyExperienceInterval { StartTime = 1, Length = 2 };
            var second = new LegacyExperienceMarker { StartTime = 4 };
            Assert.That(TimelineCandidates.References(new TimelineClip[] { first, second }, new[] { first }, 7, 2, true), Is.EqualTo(new[] { 0f, 7, 2, 4 }));
            second.StartTime = 5;
            Assert.That(TimelineCandidates.References(new TimelineClip[] { first, second }, new[] { first }, 7, 2, false), Is.EqualTo(new[] { 5f, 0, 7, 2 }));
            Assert.That(first.StartTime, Is.EqualTo(1));
        }
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
            workspace.Bind(asset); workspace.SetCollapsed(asset.RootItems.OfType<TimelineGroup>().ElementAt(2), true);
            var json = Storage.JsonDocumentSession.Encode(asset);
            var rows = TimelineGeometry.VisibleRows(asset, workspace);
            Assert.That(rows.Count, Is.EqualTo(8));
            Assert.That(rows.Take(4), Is.EqualTo(new IDirectable[] { asset.RootItems.OfType<TimelineGroup>().ElementAt(0), asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0], asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1], asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[2] }));
            Assert.That(TimelineGeometry.IsVisible(asset.RootItems.OfType<TimelineGroup>().ElementAt(2).Tracks[0].Clips[0], workspace), Is.False);
            Assert.That(Storage.JsonDocumentSession.Encode(asset), Is.EqualTo(json));
            workspace.SetCollapsed(asset.RootItems.OfType<TimelineGroup>().ElementAt(0), true);
            Assert.That(TimelineGeometry.VisibleRows(asset, workspace).Count, Is.EqualTo(5));
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
