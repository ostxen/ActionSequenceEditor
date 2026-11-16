using System.Linq;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests
{
    public sealed class TimelineGeometryTests
    {
        [TestCase(0f)] [TestCase(.25f)] [TestCase(.75f)] [TestCase(1f)]
        public void ZoomKeepsHoveredTimeFixedAtEveryAnchorAndMinimumSpan(float fraction)
        {
            var workspace = new TimelineWorkspace();
            var size = new TimelineSize(1000, 600);
            var x = workspace.LeftWidth + (1000 - workspace.LeftWidth) * fraction;
            foreach (var wheel in new[] { -120f, 120f })
            {
                workspace.SetRange(1, 8);
                ViewportNavigation.Zoom(workspace, size, x, wheel, true);
                Assert.That(workspace.ViewTimeMin + workspace.ViewTime * fraction, Is.EqualTo(1 + 7 * fraction).Within(.00001f));
                Assert.That(workspace.ViewTime, Is.EqualTo(wheel > 0 ? 8.575f : 5.425f).Within(.00001f));
            }
            workspace.SetRange(0, .25f);
            ViewportNavigation.Zoom(workspace, size, x, -1, true);
            Assert.That(workspace.ViewTime, Is.EqualTo(.25f));
            workspace.SetRange(-1, 9);
            ViewportNavigation.Zoom(workspace, size, x, 1, false);
            Assert.That(workspace.ViewTimeMin, Is.EqualTo(-1)); Assert.That(workspace.ViewTimeMax, Is.EqualTo(9));
        }

        [Test] public void TrackDropResolvesBeforeAfterGroupAndRootFromScrolledCoordinates()
        {
            var document = new FeatureSampleSequence();
            var workspace = new TimelineWorkspace();
            var layout = new TimelineLayout();
            var size = new TimelineSize(1000, 600);
            layout.Build(document, new SelectionState(), workspace, size, 0, null);
            var group = (ActionGroup)document.RootContainers[0]; var moving = group.Tracks[1];
            var before = StructureDropGeometry.Hit(document, moving, layout, workspace, size, 90, 31);
            var after = StructureDropGeometry.Hit(document, moving, layout, workspace, size, 90, 53);
            Assert.That(before.Owner, Is.SameAs(group)); Assert.That(before.Boundary, Is.Zero);
            Assert.That(after.Boundary, Is.EqualTo(1));
            workspace.ScrollY = 28;
            var scrolled = StructureDropGeometry.Hit(document, moving, layout, workspace, size, 90, 3);
            Assert.That(scrolled.Boundary, Is.Zero); Assert.That(scrolled.Y, Is.Zero);
            workspace.ScrollY = 0;
            var root = StructureDropGeometry.Hit(document, moving, layout, workspace, size, 5, 31);
            Assert.That(root.Hit, Is.True); Assert.That(root.Owner, Is.Null);
            Assert.That(root.Boundary, Is.Zero); Assert.That(root.Y, Is.Zero);
            Assert.That(StructureDropGeometry.Hit(document, moving, layout, workspace, size, 90, 601).Hit, Is.False);
            Assert.That(StructureDropGeometry.Hit(document, moving, layout, workspace, size, -1, 31).Hit, Is.False);
        }

        [TestCase(120, 8.575f)] [TestCase(1, 8.575f)] [TestCase(-120, 5.425f)] [TestCase(0, 7)]
        public void NavigationUses225StepAndPreservesSpanWhenPanReachesZero(float wheel, float expectedSpan)
        {
            var workspace = new TimelineWorkspace(); var size = new TimelineSize(1000, 600);
            workspace.SetRange(1, 8);
            ViewportNavigation.Zoom(workspace, size, 500, wheel, true);
            Assert.That(workspace.ViewTime, Is.EqualTo(expectedSpan).Within(.00001f));
            workspace.SetRange(1, 8);
            ViewportNavigation.Zoom(workspace, size, 500, wheel, false);
            Assert.That(workspace.ViewTimeMin, Is.EqualTo(1)); Assert.That(workspace.ViewTimeMax, Is.EqualTo(8));
            workspace.SetRange(1, 242);
            ViewportNavigation.Zoom(workspace, size, 500, 1, true);
            Assert.That(workspace.ViewTimeMax, Is.EqualTo(242));
            workspace.SetRange(1, 8);
            ViewportNavigation.Pan(workspace, size, 200);
            Assert.That(workspace.ViewTimeMin, Is.Zero); Assert.That(workspace.ViewTimeMax, Is.EqualTo(7));
        }

        [TestCase(false, 1f)] [TestCase(true, 1.25f)]
        public void ReferenceSnappingExcludesExactThresholdAndKeepsFirstEqualDistance(bool tied, float expected)
        {
            var document = new FeatureSampleSequence();
            foreach (var track in document.AllTracks) track.Clips.Clear();
            var interval = new FeatureSampleInterval { StartTime = 1, Length = 2 };
            document.AllTracks.First().Clips.Add(interval);
            var markers = document.AllTracks.OfType<FeatureSampleMarkerTrack>().First();
            markers.Clips.Add(new FeatureSampleMarker { StartTime = tied ? 3.25f : 1.25f });
            if (tied) markers.Clips.Add(new FeatureSampleMarker { StartTime = .75f });
            document.Init();
            using var session = Composition.SequenceComposition.Create();
            session.Open(new NBC.ActionEditor.Application.Sequences.SequenceOpenData(document));
            var gesture = new ClipGesture(session, new ClipEditing(session));
            var baseline = SequenceJsonCodec.GetContentKey(document);
            try
            {
                Assert.That(gesture.Begin(this, interval, new[] { interval }, 0, 0, 6), Is.True);
                gesture.Move(0, false, new TimeTransform(0, tied ? 50 : 25, 100), new EditorSettings(TimeStepMode.Seconds, .01f, 30, true, true, 0, "Assets/"));
                Assert.That(interval.StartTime, Is.EqualTo(expected).Within(.00001f));
            }
            finally { gesture.Cancel(); }
            Assert.That(SequenceJsonCodec.GetContentKey(session.Sequence), Is.EqualTo(baseline));
            Assert.That(session.IsEditing || session.IsDirty, Is.False);
        }

        [TestCase(500, 232, 0, 7)] [TestCase(1000, 320, 1, 8)] [TestCase(1400, 220, 12, 240)]
        public void TimePixelTransformMatchesFormulaAndRoundTrips(float width, float left, float minimum, float maximum)
        {
            var transform = new TimeTransform(minimum, maximum - minimum, width - left, left);
            foreach (var time in new[] { 0f, .25f, minimum, maximum, maximum + 3 })
            {
                var expected = left + ((double)time - minimum) / (maximum - minimum) * (width - left);
                Assert.That(transform.X(time), Is.EqualTo(expected).Within(.0001f));
                Assert.That(transform.Time((float)expected), Is.EqualTo(time).Within(.0001f));
            }
            Assert.That(transform.Duration(width - left), Is.EqualTo(maximum - minimum).Within(.00001f));
        }

        [Test] public void VisibleRowsHideCollapsedTracksWithoutChangingDocument()
        {
            var document = new FeatureSampleSequence(); var workspace = new TimelineWorkspace();
            var layout = new TimelineLayout(); var selection = new SelectionState(); var size = new TimelineSize(1000, 600);
            workspace.Bind(document); workspace.SetCollapsed((ActionGroup)document.RootContainers[2], true);
            var json = SequenceJsonCodec.Encode(document);
            layout.Build(document, selection, workspace, size, 0, null);
            var group = (ActionGroup)document.RootContainers[0];
            Assert.That(layout.Rows.Count, Is.EqualTo(8));
            Assert.That(layout.Rows.Take(4), Is.EqualTo(new IDirectable[] { group, group.Tracks[0], group.Tracks[1], group.Tracks[2] }));
            Assert.That(layout.HitClip(400, 280), Is.Null);
            Assert.That(SequenceJsonCodec.Encode(document), Is.EqualTo(json));
            workspace.SetCollapsed(group, true);
            layout.Build(document, selection, workspace, size, 0, null);
            Assert.That(layout.Rows.Count, Is.EqualTo(5));
            Assert.That(SequenceJsonCodec.Encode(document), Is.EqualTo(json));
        }

        [Test] public void ShortIntervalsPointsAndReverseMarqueeUseActualLayoutBounds()
        {
            var document = new FeatureSampleSequence(); var workspace = new TimelineWorkspace();
            workspace.LeftWidth = 200; workspace.ScrollY = 14;
            var first = document.AllTracks.First().Clips.First(); first.StartTime = 1; first.Length = .01f;
            var marker = document.AllTracks.OfType<FeatureSampleMarkerTrack>().First().Clips.First(); marker.StartTime = 1;
            var layout = new TimelineLayout();
            layout.Build(document, new SelectionState(), workspace, new TimelineSize(900, 100), 0, null);
            var bounds = layout.Bounds(first);
            Assert.That(bounds.X, Is.EqualTo(300)); Assert.That(bounds.Y, Is.EqualTo(17));
            Assert.That(bounds.Width, Is.EqualTo(5)); Assert.That(bounds.Height, Is.EqualTo(22));
            var point = layout.Bounds(marker);
            Assert.That(point.X, Is.EqualTo(294)); Assert.That(point.Width, Is.EqualTo(12));
            Assert.That(layout.RowAt(14, workspace, new TimelineSize(900, 100)), Is.SameAs(first.Parent));
            Assert.That(layout.In(TimelineRect.Between(310, 45, 295, 10)), Does.Contain(first));
            Assert.That(layout.HitClip(bounds.X + 2, bounds.Y + 2), Is.SameAs(first));
            Assert.That(layout.HitClip(310, bounds.Y + 2), Is.Null);
        }
    }
}
