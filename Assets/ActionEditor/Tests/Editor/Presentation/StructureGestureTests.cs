using NBC.ActionEditor.Infrastructure.Serialization;
using System;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEditor;
using EditorSettings = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Tests.Presentation
{
    public sealed class StructureGestureTests
    {
        [Serializable, Unique, Attachable(typeof(FeatureSampleGroup))] private sealed class UniqueTrack : TimelineTrack { }
        private sealed class View : ITimelineView
        {
            public bool Captured;
            public string Error;
            public void Render(TimelineRenderState state) { }
            public void SetCapture(bool value) => Captured = value;
            public void ShowError(string message) => Error = message;
        }
        private sealed class Playhead : ITimelinePlayhead
        { public float Time => 0; public void Seek(float value) { } }
        private sealed class Harness : IDisposable
        {
            public readonly DocumentSession Session = Composition.DocumentComposition.Create();
            public readonly SelectionState Selection = new SelectionState();
            public readonly StructureEditing Editing;
            public readonly View View = new View();
            public readonly TimelinePresenter Presenter;
            public ActionDocument Document => Session.Document;
            public Harness(int height = 500)
            {
                Session.Open(new DocumentOpenData(new FeatureSampleDocument()));
                Editing = new StructureEditing(Session, () => Selection.Items);
                var clipboard = new ClipboardEditing(Session, Editing, new ClipboardState(), new FragmentCodec(), item => Selection.Set(new[] { item }, false));
                Presenter = new TimelinePresenter(Session, Selection, Editing, new NodeEditing(Session), clipboard, new ClipEditing(Session), new TimelineWorkspace(),
                    new Playhead(), () => new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/"), View);
                Presenter.Resize(new TimelineSize(1000, height));
            }
            public TimelineGroup Group(int index) => Document.RootItems.OfType<TimelineGroup>().ElementAt(index);
            public void Pointer(PointerAction action, float x, float y, PointerButton button = PointerButton.Primary) => Presenter.Pointer(new PointerInput(action, x, y, button));
            public float Y(IDirectable item) => Presenter.State.Rows.Single(row => row.Key == Key(item)).Bounds.Y + 14;
            private int Key(IDirectable item) => Presenter.State.Rows.Single(row => row.Title == item.Name).Key;
            public string Content => NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Content(Document);
            public void Dispose() { Presenter.Dispose(); Session.Dispose(); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void GroupMoveUsesPreRemovalBoundaryAndOneUndo(int boundary)
        {
            using var h = new Harness(); var before = h.Content; var version = h.Session.Version;
            var groups = h.Document.RootItems.ToArray(); var first = groups[0]; var children = ((TimelineGroup)first).Tracks.ToArray();
            var changed = h.Editing.Move(first, null, boundary, version);
            if (boundary < 2)
            { Assert.That(changed, Is.False); Assert.That(h.Session.Version, Is.EqualTo(version)); Assert.That(h.Session.IsDirty, Is.False); return; }
            var expected = boundary == 2 ? new[] { groups[1], groups[0], groups[2] } : new[] { groups[1], groups[2], groups[0] };
            Assert.That(h.Document.RootItems, Is.EqualTo(expected));
            Assert.That(((TimelineGroup)first).Tracks, Is.EqualTo(children));
            var after = h.Content; Undo.PerformUndo(); Assert.That(h.Content, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(h.Content, Is.EqualTo(after));
        }
        [Test] public void TrackCrossGroupDetachReattachAndMixedRootsRoundTripWithoutParameterLoss()
        {
            using var h = new Harness(); var original = h.Content;
            var first = h.Group(0); var target = h.Group(1); var track = first.Tracks[0]; var clips = track.Clips.ToArray();
            h.Editing.Move(track, target, 1, h.Session.Version);
            Assert.That(target.Tracks[1], Is.SameAs(track)); Assert.That(track.Parent, Is.SameAs(target));
            h.Editing.Move(track, null, 1, h.Session.Version);
            Assert.That(h.Document.RootItems[1], Is.SameAs(track)); Assert.That(track.Parent, Is.Null);
            var encoded = NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Encode(h.Document);
            var loaded = NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Decode(encoded);
            Assert.That(loaded.RootItems.Select(item => ((IDirectable)item).Name), Is.EqualTo(h.Document.RootItems.Select(item => ((IDirectable)item).Name)));
            Assert.That(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Content(loaded), Is.EqualTo(h.Content));
            h.Editing.Move(track, first, 0, h.Session.Version);
            Assert.That(h.Content, Is.EqualTo(original)); Assert.That(track.Clips, Is.EqualTo(clips));
            Undo.PerformUndo(); Assert.That(h.Document.RootItems[1], Is.TypeOf<FeatureSampleIntervalTrack>());
            Undo.PerformUndo(); Assert.That(h.Group(1).Tracks[1], Is.TypeOf<FeatureSampleIntervalTrack>());
            Undo.PerformUndo(); Assert.That(h.Content, Is.EqualTo(original));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InGroupMoveCorrectsDownwardIndexAndAdjacentNoOp(int boundary)
        {
            using var h = new Harness(); var group = h.Group(0); var tracks = group.Tracks.ToArray(); var version = h.Session.Version;
            var moved = h.Editing.Move(tracks[0], group, boundary, version);
            Assert.That(moved, Is.EqualTo(boundary > 1));
            var expected = boundary < 2 ? tracks : boundary == 2 ? new[] { tracks[1], tracks[0], tracks[2] } : new[] { tracks[1], tracks[2], tracks[0] };
            Assert.That(group.Tracks, Is.EqualTo(expected));
        }
        [Test] public void LockedSourceTargetAndStaleVersionAreRecheckedAtRelease()
        {
            using var h = new Harness(); var source = h.Group(0).Tracks[0]; var target = h.Group(1);
            var version = h.Session.Version; var operation = h.Editing.Reorder(source, target, 0);
            target.IsLocked = true; var before = h.Content;
            Assert.That(operation.CanExecute(out _), Is.False); Assert.Throws<InvalidOperationException>(operation.Execute);
            Assert.That(h.Content, Is.EqualTo(before)); target.IsLocked = false;
            h.Session.Edit("rename", () => target.Name = "changed");
            Assert.Throws<InvalidOperationException>(() => h.Editing.Move(source, target, 0, version));
            h.Group(0).IsLocked = true;
            Assert.That(h.Editing.MoveError(source, null, 0, h.Session.Version), Is.Not.Null);
        }
        [Test] public void UniqueAttachableNestedGroupAndInvalidBoundariesRejectWithoutMutation()
        {
            var document = new FeatureSampleDocument(); var first = (TimelineGroup)document.RootItems[0]; var second = (TimelineGroup)document.RootItems[1];
            var unique = new UniqueTrack(); first.Tracks.Add(unique); second.Tracks.Add(new UniqueTrack()); document.Validate();
            Assert.That(StructureRules.Error(document, unique, second, 0), Is.Not.Null);
            Assert.That(StructureRules.Error(document, unique, first, 0), Is.Null);
            var other = new TimelineGroup(); document.AddRoot(other); document.Validate();
            Assert.That(StructureRules.Error(document, unique, other, 0), Is.Not.Null);
            Assert.That(StructureRules.Error(document, first, second, 0), Is.Not.Null);
            Assert.That(StructureRules.Error(document, first, null, -1), Is.Not.Null);
            Assert.That(StructureRules.Error(document, first, null, 99), Is.Not.Null);
        }
        [Test] public void UnlockedGroupMovesWholeSubtreeEvenWhenChildLockedOrInactive()
        {
            using var h = new Harness(); var group = h.Group(2); var tracks = group.Tracks.ToArray();
            Assert.That(tracks[0].IsLocked, Is.True);
            h.Editing.Move(group, null, 0, h.Session.Version);
            Assert.That(h.Document.RootItems[0], Is.SameAs(group)); Assert.That(group.Tracks, Is.EqualTo(tracks));
        }
        [Test] public void ThresholdWrongButtonCancelAndOutsideReleaseNeverMutateGraph()
        {
            using var h = new Harness(); var before = h.Content;
            h.Pointer(PointerAction.Down, 70, 14); h.Pointer(PointerAction.Move, 73, 14);
            Assert.That(h.Presenter.State.Insertion.Visible, Is.False);
            h.Pointer(PointerAction.Up, 73, 14); Assert.That(h.Session.IsDirty, Is.False);
            h.Pointer(PointerAction.Down, 70, 14); h.Pointer(PointerAction.Move, 70, 200);
            Assert.That(h.Presenter.State.Insertion.Visible, Is.True); Assert.That(h.Session.IsEditing, Is.False);
            h.Pointer(PointerAction.Up, 70, 200, PointerButton.Secondary); Assert.That(h.View.Captured, Is.True);
            h.Presenter.Key(new KeyInput(TimelineKey.Escape)); Assert.That(h.View.Captured, Is.False);
            h.Pointer(PointerAction.Down, 70, 14); h.Pointer(PointerAction.Move, 70, 200); h.Pointer(PointerAction.Up, -1, 200);
            Assert.That(h.Content, Is.EqualTo(before)); Assert.That(h.Presenter.State.Insertion.Visible || h.View.Captured, Is.False);
        }
        [Test] public void GroupDropOnExpandedChildUsesWholeBlockMidpointAndFullWidthIndicator()
        {
            using var h = new Harness(); var first = h.Group(0); var second = h.Group(1); var third = h.Group(2);
            h.Pointer(PointerAction.Down, 70, h.Y(third));
            h.Pointer(PointerAction.Move, 800, h.Y(first.Tracks[0]));
            var line = h.Presenter.State.Insertion;
            Assert.That(line.Visible && line.Valid, Is.True); Assert.That(line.Width, Is.EqualTo(1000)); Assert.That(line.Y, Is.Zero);
            h.Pointer(PointerAction.Move, 800, h.Y(first.Tracks[2]));
            Assert.That(h.Presenter.State.Insertion.Y, Is.EqualTo(112));
            h.Pointer(PointerAction.Up, 800, h.Y(first.Tracks[2]));
            Assert.That(h.Document.RootItems, Is.EqualTo(new[] { first, third, second }));
        }
        [Test] public void RootTwentyPixelZoneIsDisjointFromGroupContentAndCollapsedReceiverExpands()
        {
            using var h = new Harness(); var first = h.Group(0); var target = h.Group(1); var track = first.Tracks[0];
            var targetRow = h.Presenter.State.Rows.Single(row => row.Title == target.Name);
            h.Presenter.ToggleFold(targetRow.Key);
            h.Pointer(PointerAction.Down, 80, h.Y(track)); h.Pointer(PointerAction.Move, 19, h.Y(target));
            Assert.That(h.Presenter.State.Insertion.Y, Is.EqualTo(140));
            h.Pointer(PointerAction.Move, 20, h.Y(target));
            h.Pointer(PointerAction.Up, 20, h.Y(target));
            Assert.That(target.Tracks[0], Is.SameAs(track)); Assert.That(h.Presenter.Workspace.IsCollapsed(target), Is.False);
        }
        [Test] public void InvalidReceiverPaintsRedAndReleaseKeepsOriginalTree()
        {
            using var h = new Harness(); var first = h.Group(0); var target = h.Group(1); target.IsLocked = true;
            var before = h.Content;
            h.Pointer(PointerAction.Down, 70, h.Y(first.Tracks[0])); h.Pointer(PointerAction.Move, 70, h.Y(target));
            var line = h.Presenter.State.Insertion;
            Assert.That(line.Visible, Is.True); Assert.That(line.Valid, Is.False); Assert.That(line.Red, Is.GreaterThan(line.Blue));
            h.Pointer(PointerAction.Up, 70, h.Y(target));
            Assert.That(h.View.Error, Is.Not.Null); Assert.That(h.Content, Is.EqualTo(before)); Assert.That(h.View.Captured, Is.False);
        }
        [TestCase(0f, -300f)] [TestCase(12f, -150f)] [TestCase(24f, 0f)]
        [TestCase(76f, 0f)] [TestCase(88f, 150f)] [TestCase(99f, 287.5f)]
        public void ClockAutoScrollUsesExactTwentyFourPixelRamp(float y, float velocity)
        {
            using var h = new Harness(100); h.Presenter.Workspace.ScrollY = 100; h.Presenter.Refresh();
            h.Pointer(PointerAction.Down, 70, 26); h.Pointer(PointerAction.Move, 70, y);
            h.Presenter.Tick(.1);
            Assert.That(h.Presenter.Workspace.ScrollY, Is.EqualTo(100 + velocity * .1f).Within(.0001f));
            Assert.That(h.Session.IsEditing, Is.False);
            h.Presenter.Tick(10);
            Assert.That(h.Presenter.Workspace.ScrollY, Is.InRange(0, 212));
            h.Presenter.Cancel(); Assert.That(h.Session.IsDirty, Is.False);
        }
        [Test] public void ChangedDocumentAndOtherWindowEditCancelWithoutOwningTransaction()
        {
            using var h = new Harness(); h.Pointer(PointerAction.Down, 70, 14); h.Pointer(PointerAction.Move, 70, 200);
            h.Session.Edit("other", () => h.Group(1).Name = "other window");
            Assert.That(h.View.Captured || h.Presenter.State.Insertion.Visible, Is.False);
            h.Pointer(PointerAction.Up, 70, 200); Assert.That(h.Group(1).Name, Is.EqualTo("other window"));
            h.Pointer(PointerAction.Down, 70, 14); h.Pointer(PointerAction.Move, 70, 200);
            using var token = h.Session.BeginEdit(new object());
            h.Presenter.Cancel(); Assert.That(token.IsActive, Is.True);
        }
        [Test] public void EmptyGroupReceivesFirstTrackAndUndoRestoresEmptyFoldState()
        {
            using var h = new Harness();
            h.Session.Edit("empty", () => h.Group(1).Tracks.Clear());
            var target = h.Group(1); var track = h.Group(0).Tracks[0];
            Assert.That(h.Presenter.State.Rows.Single(row => row.Title == target.Name).CanFold, Is.False);
            var before = h.Content;
            h.Pointer(PointerAction.Down, 70, h.Y(track)); h.Pointer(PointerAction.Move, 70, h.Y(target)); h.Pointer(PointerAction.Up, 70, h.Y(target));
            Assert.That(target.Tracks.Single(), Is.SameAs(track));
            Assert.That(h.Presenter.State.Rows.Single(row => row.Title == target.Name).CanFold, Is.True);
            Undo.PerformUndo(); Assert.That(h.Content, Is.EqualTo(before));
            Assert.That(h.Presenter.State.Rows.Single(row => row.Title == target.Name).CanFold, Is.False);
            Undo.PerformRedo(); Assert.That(h.Presenter.State.Rows.Single(row => row.Title == target.Name).CanFold, Is.True);
        }
        [Test] public void BodyBlankAppendsRootAndOutOfBoundsHidesIndicator()
        {
            using var h = new Harness(); var track = h.Group(0).Tracks[0];
            h.Pointer(PointerAction.Down, 70, h.Y(track)); h.Pointer(PointerAction.Move, 1200, 400);
            Assert.That(h.Presenter.State.Insertion.Visible, Is.False);
            h.Pointer(PointerAction.Move, 700, 450); h.Pointer(PointerAction.Up, 700, 450);
            Assert.That(h.Document.RootItems.Last(), Is.SameAs(track)); Assert.That(track.Parent, Is.Null);
        }
        [Test] public void RepeatedCancelAndNoOpLeavePriorUndoEntryIntact()
        {
            using var h = new Harness(); var original = h.Content;
            h.Session.Edit("sentinel", () => h.Group(1).Name = "sentinel");
            var version = h.Session.Version;
            h.Editing.MoveTrack(h.Group(0).Tracks[0], h.Group(0), 1);
            for (var i = 0; i < 3; i++)
            { h.Pointer(PointerAction.Down, 70, 14); h.Pointer(PointerAction.Move, 70, 200); h.Presenter.Cancel(); }
            Assert.That(h.Session.Version, Is.EqualTo(version));
            Undo.PerformUndo(); Assert.That(h.Content, Is.EqualTo(original));
        }
    }
}
