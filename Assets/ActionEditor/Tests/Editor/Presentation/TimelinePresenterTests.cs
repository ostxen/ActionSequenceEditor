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
    public sealed class TimelinePresenterTests
    {
        private sealed class View : ITimelineView
        {
            public TimelineRenderState State;
            public bool Captured;
            public string Error;
            public int Renders;
            public void Render(TimelineRenderState state) { State = state; Renders++; }
            public void SetCapture(bool captured) => Captured = captured;
            public void ShowError(string message) => Error = message;
        }
        private sealed class Playhead : ITimelinePlayhead
        {
            public float Time { get; private set; }
            public bool Fail;
            public void Seek(float time) { if (Fail) throw new InvalidOperationException("seek"); Time = time; }
        }
        private sealed class Harness : IDisposable
        {
            public readonly DocumentSession Session = Composition.DocumentComposition.Create();
            public readonly SelectionState Selection = new SelectionState();
            public readonly View View = new View();
            public readonly Playhead Playhead = new Playhead();
            public readonly TimelinePresenter Presenter;
            public readonly StructureEditing Structure;
            public readonly ClipboardEditing Clipboard;
            public EditorSettings Settings = new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/");
            public FeatureSampleDocument Document => (FeatureSampleDocument)Session.Document;
            public TimelineClip Clip => Document.AllTracks.First().Clips.First();
            public Harness()
            {
                Session.Open(new DocumentOpenData(new FeatureSampleDocument()));
                Structure = new StructureEditing(Session, () => Selection.Items);
                Clipboard = new ClipboardEditing(Session, Structure, new ClipboardState(), new FragmentCodec(), clip => Selection.Set(new[] { clip }, false));
                Presenter = Create(View, new TimelineWorkspace());
                Presenter.Resize(new TimelineSize(1000, 500));
            }
            public TimelinePresenter Create(View view, TimelineWorkspace workspace) => new TimelinePresenter(Session, Selection,
                Structure, new NodeEditing(Session), Clipboard, new ClipEditing(Session), workspace, Playhead, () => Settings, view);
            public TimelineClipState First => Presenter.State.Clips.First();
            public void Pointer(PointerAction action, float x, float y, PointerButton button = PointerButton.Primary,
                bool control = false, bool shift = false, TimelineRegion region = TimelineRegion.Body) =>
                Presenter.Pointer(new PointerInput(action, x, y, button, control, shift, region));
            public void Dispose() { Presenter.Dispose(); Session.Dispose(); }
        }
        [Test] public void RevealClipExpandsItsGroupAndLeavesOtherWorkspaceUntouched()
        {
            using var h = new Harness();
            using var other = h.Create(new View(), new TimelineWorkspace());
            other.Resize(new TimelineSize(800, 90));
            h.Presenter.Resize(new TimelineSize(800, 90));
            var clip = h.Document.AllTracks.Skip(4).First().Clips.First();
            var group = (TimelineGroup)clip.Parent.Parent;
            h.Presenter.Workspace.SetCollapsed(group, true);
            other.Workspace.SetCollapsed(group, true);
            h.Presenter.Refresh(); other.Refresh();
            h.Presenter.Reveal(clip);
            Assert.That(h.Presenter.Workspace.IsCollapsed(group), Is.False);
            var row = h.Presenter.State.Rows.Single(value => value.Key == h.Presenter.KeyFor(clip.Parent));
            Assert.That(row.Bounds.Y, Is.GreaterThanOrEqualTo(0));
            Assert.That(row.Bounds.Y + row.Bounds.Height, Is.LessThanOrEqualTo(90));
            Assert.That(other.Workspace.IsCollapsed(group), Is.True);
            Assert.That(other.Workspace.ScrollY, Is.Zero);
            Assert.That(h.Session.IsDirty, Is.False);
        }

        [Test] public void CompleteMoveCommitUndoRedoNeedsNoWindowOrGlobalEvent()
        {
            using var h = new Harness(); var rect = h.First.Bounds; var x = rect.X + rect.Width / 2; var y = rect.Y + 8;
            var start = h.Clip.StartTime;
            h.Pointer(PointerAction.Down, x, y); Assert.That(h.View.Captured, Is.True);
            h.Pointer(PointerAction.Move, x + 60, y); Assert.That(h.Clip.StartTime, Is.GreaterThan(start));
            h.Pointer(PointerAction.Up, x + 60, y); Assert.That(h.View.Captured || h.Session.IsEditing, Is.False);
            var moved = h.Clip.StartTime; Undo.PerformUndo(); Assert.That(h.Clip.StartTime, Is.EqualTo(start));
            Undo.PerformRedo(); Assert.That(h.Clip.StartTime, Is.EqualTo(moved));
        }
        [Test] public void WrongButtonOutsideReleaseAndEscapeKeepOwnerSafe()
        {
            using var h = new Harness(); var rect = h.First.Bounds; var x = rect.X + rect.Width / 2; var y = rect.Y + 8; var start = h.Clip.StartTime;
            h.Pointer(PointerAction.Down, x, y); h.Pointer(PointerAction.Move, x + 30, y);
            h.Pointer(PointerAction.Up, x + 30, y, PointerButton.Secondary);
            Assert.That(h.View.Captured && h.Session.IsEditing, Is.True);
            h.Pointer(PointerAction.Up, -1, y);
            Assert.That(h.Clip.StartTime, Is.EqualTo(start)); Assert.That(h.Session.IsEditing || h.View.Captured, Is.False);
            rect = h.First.Bounds; x = rect.X + rect.Width / 2;
            h.Pointer(PointerAction.Down, x, y); h.Pointer(PointerAction.Move, x + 20, y);
            h.Presenter.Key(new KeyInput(TimelineKey.Escape));
            Assert.That(h.Clip.StartTime, Is.EqualTo(start)); Assert.That(h.View.Captured, Is.False);
        }
        [Test] public void ResizeUsesSharedMinimumAndLeftHandlePriority()
        {
            using var h = new Harness(); var rect = h.First.Bounds; var end = h.Clip.EndTime;
            h.Pointer(PointerAction.Down, rect.X + 1, rect.Y + 5);
            Assert.That(h.Presenter.Mode, Is.EqualTo(TimelineGestureMode.ResizeLeft));
            h.Pointer(PointerAction.Move, rect.Right - 2, rect.Y + 5);
            h.Pointer(PointerAction.Up, rect.Right - 2, rect.Y + 5);
            Assert.That(h.Clip.EndTime, Is.EqualTo(end).Within(.00001f));
            Assert.That(h.Clip.Length, Is.GreaterThanOrEqualTo(.01f));
            Assert.That(h.Clip.Length, Is.LessThan(.1f));
            Assert.That(h.Clip.BlendIn, Is.InRange(0, h.Clip.Length));
            Assert.That(h.Clip.BlendOut, Is.InRange(0, h.Clip.Length - h.Clip.BlendIn));
            var shortened = JsonDocumentCodec.Encode(h.Document);
            Undo.PerformUndo();
            Assert.That(h.Clip.Length, Is.EqualTo(1.5f));
            Assert.That(h.Clip.BlendIn, Is.EqualTo(.25f));
            Assert.That(h.Clip.BlendOut, Is.EqualTo(.25f));
            Undo.PerformRedo();
            Assert.That(JsonDocumentCodec.Encode(h.Document), Is.EqualTo(shortened));
        }
        [Test] public void TwoWindowsShareAuthorsButNotRangeScrollCaptureOrDisposal()
        {
            using var h = new Harness(); var otherView = new View(); using var other = h.Create(otherView, new TimelineWorkspace());
            other.Resize(new TimelineSize(800, 300)); other.Wheel(new WheelInput(600, 100, 1));
            Assert.That(other.State.Maximum, Is.Not.EqualTo(h.Presenter.State.Maximum));
            var rect = h.First.Bounds; var x = rect.X + rect.Width / 2; var y = rect.Y + 5;
            h.Pointer(PointerAction.Down, x, y); h.Pointer(PointerAction.Move, x + 30, y);
            other.Cancel(); other.Dispose();
            Assert.That(h.View.Captured && h.Session.IsEditing, Is.True);
            h.Pointer(PointerAction.Up, x + 30, y); Assert.That(h.Session.IsEditing, Is.False);
        }
        [Test] public void DocumentReplacementAndUndoDropStaleKeysAndCancelCapture()
        {
            using var h = new Harness(); var key = h.First.Key; var rect = h.First.Bounds;
            h.Pointer(PointerAction.Down, rect.X + rect.Width / 2, rect.Y + 4);
            h.Session.Open(new DocumentOpenData(new FeatureSampleDocument()));
            Assert.That(h.View.Captured || h.Session.IsEditing, Is.False);
            Assert.That(h.Presenter.State.Clips.Any(clip => clip.Key == key), Is.False);
            var previous = h.Selection.Count; h.Presenter.Select(key);
            Assert.That(h.Selection.Count, Is.EqualTo(previous)); Assert.That(h.View.Error, Is.Not.Null);
        }
        [TestCase(0f)] [TestCase(.25f)] [TestCase(.75f)] [TestCase(1f)]
        public void ZoomAnchorsAndPanPreserveSpanAndDoNotDirtyDocument(float fraction)
        {
            using var h = new Harness(); var workspace = h.Presenter.Workspace;
            workspace.SetRange(1, 8); h.Presenter.Refresh();
            var x = workspace.LeftWidth + (1000 - workspace.LeftWidth) * fraction; var before = 1 + 7 * fraction;
            h.Presenter.Wheel(new WheelInput(x, 100, -1));
            Assert.That(workspace.ViewTimeMin + workspace.ViewTime * fraction, Is.EqualTo(before).Within(.00001f));
            var span = workspace.ViewTime;
            h.Pointer(PointerAction.Down, 400, 200, PointerButton.Middle);
            h.Pointer(PointerAction.Move, 990, 200, PointerButton.Middle);
            h.Pointer(PointerAction.Up, 990, 200, PointerButton.Middle);
            Assert.That(workspace.ViewTime, Is.EqualTo(span).Within(.00001f)); Assert.That(workspace.ViewTimeMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(h.Session.IsDirty, Is.False);
        }
        [Test] public void FoldMarqueeAndTextConsumedShortcutUseSharedSelection()
        {
            using var h = new Harness(); var group = h.Presenter.State.Rows.First();
            h.Presenter.ToggleFold(group.Key); Assert.That(h.Presenter.State.Rows.First().Collapsed, Is.True);
            Assert.That(h.Session.IsDirty, Is.False); h.Presenter.ToggleFold(group.Key);
            var rect = h.First.Bounds;
            h.Pointer(PointerAction.Down, rect.X - 8, rect.Y - 1);
            h.Pointer(PointerAction.Move, rect.Right + 8, rect.Bottom + 1);
            h.Pointer(PointerAction.Up, rect.Right + 8, rect.Bottom + 1);
            Assert.That(h.Selection.Contains(h.Clip), Is.True);
            var count = h.Document.AllTracks.Sum(track => track.Clips.Count);
            h.Presenter.Key(new KeyInput(TimelineKey.Delete, true));
            Assert.That(h.Document.AllTracks.Sum(track => track.Clips.Count), Is.EqualTo(count));
        }
        [Test] public void SeekCancelAndFaultCleanCaptureWithoutTouchingAuthors()
        {
            using var h = new Harness(); h.Playhead.Seek(.5f);
            h.Pointer(PointerAction.Down, 500, 10, region: TimelineRegion.Ruler);
            Assert.That(h.Playhead.Time, Is.Not.EqualTo(.5f));
            h.Presenter.Cancel(); Assert.That(h.Playhead.Time, Is.EqualTo(.5f)); Assert.That(h.View.Captured, Is.False);
            h.Playhead.Fail = true; h.Pointer(PointerAction.Down, 500, 10, region: TimelineRegion.Ruler);
            Assert.That(h.View.Captured || h.Session.IsEditing, Is.False); Assert.That(h.View.Error, Does.Contain("seek"));
        }
        [Test] public void DisposedPresenterUnsubscribesAndCannotCancelAnotherOwner()
        {
            using var h = new Harness(); h.Presenter.Dispose(); var renders = h.View.Renders;
            using var token = h.Session.BeginEdit(this); token.Update(() => h.Document.Version = 4);
            h.Selection.Set(new[] { h.Clip }, false); h.Presenter.Dispose();
            Assert.That(token.IsActive, Is.True); Assert.That(h.View.Renders, Is.EqualTo(renders));
            Assert.Throws<ObjectDisposedException>(() => h.Presenter.Pointer(new PointerInput(PointerAction.Down, 500, 50)));
        }
        [Test] public void GridFollowsMagnetSettingAndShiftOnlyBypassesReferenceSnapping()
        {
            using var h = new Harness();
            h.Settings = new EditorSettings(TimeStepMode.Seconds, .1f, 30, true, true, 0, "Assets/");
            var bounds = h.First.Bounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + 5;
            h.Pointer(PointerAction.Down, x, y);
            h.Pointer(PointerAction.Move, x + 20, y, shift: true);
            Assert.That(h.Clip.StartTime / .1f, Is.EqualTo(Math.Round(h.Clip.StartTime / .1f)).Within(.00001f));
            h.Presenter.Cancel();
            h.Settings = new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/");
            bounds = h.First.Bounds; x = bounds.X + bounds.Width / 2;
            var start = h.Clip.StartTime;
            h.Pointer(PointerAction.Down, x, y); h.Pointer(PointerAction.Move, x + 13, y);
            Assert.That(h.Clip.StartTime, Is.EqualTo(start + 13f / 760 * 7).Within(.00001f));
            h.Presenter.Cancel();
        }
        [Test] public void MultiselectMoveUsesOneDeltaAndCancelMarqueeRestoresSelection()
        {
            using var h = new Harness();
            var first = h.Clip; var second = h.Document.AllTracks.First().Clips.Skip(1).First();
            var spacing = second.StartTime - first.StartTime;
            h.Selection.Set(new[] { first, second }, true);
            var rect = h.First.Bounds; var x = rect.X + rect.Width / 2; var y = rect.Y + 5;
            h.Pointer(PointerAction.Down, x, y); h.Pointer(PointerAction.Move, 241, y);
            Assert.That(first.StartTime, Is.GreaterThanOrEqualTo(0));
            Assert.That(second.StartTime - first.StartTime, Is.EqualTo(spacing).Within(.00001f));
            h.Presenter.Cancel();
            h.Selection.Set(new[] { h.Clip }, false);
            h.Pointer(PointerAction.Down, 900, 450); h.Pointer(PointerAction.Move, 950, 480);
            Assert.That(h.Selection.Count, Is.Zero); h.Presenter.Cancel();
            Assert.That(h.Selection.First, Is.SameAs(h.Clip)); Assert.That(h.View.State.Marquee, Is.Null);
        }
        [Test] public void EmptyFoldAndLockedSourceCannotStartAuthoringOrLeaveCapture()
        {
            using var h = new Harness();
            h.Session.Edit("empty", () => h.Document.RootItems.OfType<TimelineGroup>().Last().Tracks.Clear());
            var empty = h.Presenter.State.Rows.Last(row => row.IsGroup);
            Assert.That(empty.CanFold, Is.False); h.Presenter.ToggleFold(empty.Key);
            Assert.That(h.Presenter.State.Rows.Last(row => row.IsGroup).Collapsed, Is.False);
            h.Session.Edit("lock", () => h.Document.AllTracks.First().IsLocked = true);
            var rect = h.First.Bounds;
            h.Pointer(PointerAction.Down, rect.X + rect.Width / 2, rect.Y + 5);
            Assert.That(h.Session.IsEditing || h.View.Captured, Is.False);
        }
        [Test] public void SameNeutralInputThroughTwoPresenterInstancesProducesIdenticalAuthors()
        {
            using var first = new Harness(); using var second = new Harness();
            var a = first.First.Bounds; var b = second.First.Bounds;
            var actions = new[] { PointerAction.Down, PointerAction.Move, PointerAction.Up };
            foreach (var action in actions)
            {
                var offset = action == PointerAction.Down ? 0 : 35;
                first.Pointer(action, a.X + a.Width / 2 + offset, a.Y + 5);
                second.Pointer(action, b.X + b.Width / 2 + offset, b.Y + 5);
            }
            Assert.That(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Content(first.Document), Is.EqualTo(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Content(second.Document)));
            Assert.That(first.View.State.Clips.Select(clip => clip.Bounds.X), Is.EqualTo(second.View.State.Clips.Select(clip => clip.Bounds.X)));
        }
        [Test] public void MinimumResizeBeyondEndRemainsFiniteAndAtLeastOneCentisecond()
        {
            using var h = new Harness(); var bounds = h.First.Bounds;
            h.Pointer(PointerAction.Down, bounds.X + 1, bounds.Y + 5);
            h.Pointer(PointerAction.Move, 990, bounds.Y + 5);
            Assert.That(h.Clip.Length, Is.GreaterThanOrEqualTo(.01f));
            Assert.That(h.View.Error, Is.Null);
            h.Presenter.Cancel();
        }
    }
}
