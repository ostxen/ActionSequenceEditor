using NBC.ActionEditor.Views.IMGUI;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitSettings = NBC.ActionEditor.Views.UIToolkit.SettingsWindow;
using NBC.ActionEditor.Infrastructure.Sequences;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Model;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class ImguiTimelineTests : SharedContextFixture
    {
        [UnityTest] public IEnumerator ImguiMultiMoveClampsOneDeltaAndKeepsSpacingAtZero()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600); window.Focus();
                yield return null; yield return null;
                var first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                var second = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips[0];
                first.StartTime = 1; second.StartTime = 3;
                EditorFixture.Shared.Selection.Set(new IDirectable[] { first, second }, false); window.Presenter.Refresh();
                var down = ImguiPoint(window, first);
                var scale = (window.position.width - window.Timeline.State.ContentLeft) / window.Workspace.ViewTime;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = down, shift = true });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Move));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down - new Vector2(scale * 2, 0), shift = true });
                Assert.That(first.StartTime, Is.Zero.Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(2).Within(.0001f), "Both items must use the same zero-clamped delta.");
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down - new Vector2(400, 0), shift = true });
                Assert.That(first.StartTime, Is.Zero.Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(2).Within(.0001f));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down + new Vector2(scale, 0), shift = true });
                Assert.That(first.StartTime, Is.EqualTo(2).Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(4).Within(.0001f));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(1).Within(.0001f));
                first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; second = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips[0];
                EditorFixture.Shared.Selection.Set(new IDirectable[] { first, second }, false); window.Presenter.Refresh(); down = ImguiPoint(window, first);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = down, shift = true });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down + new Vector2(scale * .5f, 0), shift = true });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = down + new Vector2(scale * .5f, 0), shift = true });
                Assert.That(first.StartTime, Is.EqualTo(1.5f).Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(3.5f).Within(.0001f));
                Undo.PerformUndo(); yield return null;
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(1).Within(.0001f));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips[0].StartTime, Is.EqualTo(3).Within(.0001f));
            }
            finally { window.Presenter?.Cancel(); window.DiscardChanges(); window.Close(); }
        }

        [Test] public void CollapsedClipsCannotBeHitThroughRetainedDrawGeometry()
        {
            var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); var track = group.Tracks[0]; var clip = track.Clips[0];
            var layout = new TimelineLayout(); var size = new TimelineSize(1000, 500);
            var state = layout.Build(EditorFixture.Shared.Sequence, EditorFixture.Shared.Selection, Workspace, size, 0, null);
            var rect = state.Clips.Single(c => c.Key == layout.KeyFor(clip)).Bounds;
            var x = rect.X + rect.Width * .5f; var y = rect.Y + 8;
            Assert.That(layout.HitClip(x, y), Is.SameAs(clip));
            Workspace.SetCollapsed(group, true);
            state = layout.Build(EditorFixture.Shared.Sequence, EditorFixture.Shared.Selection, Workspace, size, 0, null);
            Assert.That(layout.HitClip(x, y), Is.Not.SameAs(clip));
            Assert.That(state.Clips.Any(c => c.Key == layout.KeyFor(clip)), Is.False);
            CollectionAssert.DoesNotContain(layout.In(rect), clip);
        }

        [Test] public void StretchRefreshesMagnetReferencesWithoutPreviousMoveGesture()
        {
            var gesture = new ClipGesture(EditorFixture.Shared.Session, EditorFixture.Shared.Clips);
            var settings = new NBC.ActionEditor.Application.Settings.EditorSettings(NBC.ActionEditor.Application.Settings.TimeStepMode.Seconds, .01f, 30, true, true, 0, "Assets/");
            var transform = new TimeTransform(0, 7, 700);
            try
            {
                var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
                Assert.That(gesture.Begin(this, clip, new[] { clip }, 1, 175, 12345), Is.True);
                gesture.Cancel();
                clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                Assert.That(gesture.Begin(this, clip, new[] { clip }, 1, 175, 0), Is.True);
                gesture.Move(247, false, transform, settings);
                Assert.That(clip.EndTime, Is.EqualTo(2.5f), "First resize must use the current unselected clip references.");
                Assert.That(gesture.Guides.Any(g => g.Time == 2.5f && g.Solid), Is.True);
                Assert.That(gesture.Guides.Any(g => g.Time == 12345), Is.False);
            }
            finally { gesture.Cancel(); }
        }

        [Test] public void ImguiGestureReadsShiftFromReceivedPointerWithoutGlobalGuiEvent()
        {
            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
                var presenter = window.Presenter; presenter.Resize(new TimelineSize(1000, 500));
                var rect = presenter.State.Clips.Single(c => c.Key == presenter.KeyFor(clip)).Bounds;
                var x = rect.X + rect.Width * .5f; var y = rect.Y + 8;
                var delta = (1000 - presenter.State.ContentLeft) / 7 * .25f;
                presenter.Pointer(new PointerInput(PointerAction.Down, x, y, shift: true));
                presenter.Pointer(new PointerInput(PointerAction.Move, x + delta, y, shift: true));
                presenter.Pointer(new PointerInput(PointerAction.Up, x + delta, y, shift: true));
                Assert.That(clip.StartTime, Is.EqualTo(.5f).Within(.00001f));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                Assert.That(new PointerInput(PointerAction.Move, x, y, shift: false).Shift, Is.False);
            }
            finally { window.Presenter?.Cancel(); window.DiscardChanges(); UnityEngine.Object.DestroyImmediate(window); }
        }

        [UnityTest] public IEnumerator ImguiMiddlePanReleaseOutsideDoesNotConsumeLaterLeftDrag()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600); window.Focus();
                yield return null; yield return null;
                window.Workspace.ViewTimeMax = 8; window.Workspace.ViewTimeMin = 1;
                var start = new Vector2(Styles.TimelineLeftTotalWidth + 100, 100);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 2, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 2, mousePosition = new Vector2(10, 100) });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + new Vector2(40, 0) });
                Assert.That(window.Workspace.ViewTimeMin, Is.EqualTo(1).Within(.001f));
                Assert.That(window.Workspace.ViewTimeMax, Is.EqualTo(8).Within(.001f));
                window.SendEvent(new Event { type = EventType.MouseDown, button = 2, mousePosition = start });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 2, mousePosition = start + new Vector2(40, 0) });
                Assert.That(window.Workspace.ViewTimeMin, Is.EqualTo(1).Within(.001f), "Escape must cancel navigation even when the window consumes the key first.");
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [Test] public void ImguiMarqueeCancelCannotApplyThePreviousRectangleOnRelease()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
            try
            {
                var presenter = window.Presenter; presenter.Resize(new TimelineSize(1000, 500));
                presenter.Pointer(new PointerInput(PointerAction.Down, 900, 400));
                presenter.Pointer(new PointerInput(PointerAction.Move, 300, 30));
                Assert.That(presenter.State.Marquee.HasValue, Is.True);
                presenter.Key(new KeyInput(TimelineKey.Escape));
                presenter.Pointer(new PointerInput(PointerAction.Up, 300, 30));
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(clip));
                Assert.That(presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(presenter.State.Marquee.HasValue, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test] public void ImguiWindowsOwnIndependentGestureSnapshotsAndReferences()
        {

            var first = ScriptableObject.CreateInstance<ImguiWindow>();
            var second = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                Assert.That(first.Presenter, Is.Not.SameAs(second.Presenter));
                Assert.That(first.Workspace, Is.Not.SameAs(second.Workspace));
                first.Workspace.SetRange(-1, 12); first.Workspace.ScrollY = 55; first.Workspace.LeftWidth = 310;
                first.Workspace.SetCollapsed(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().First(), true);
                Assert.That(second.Workspace.ViewTimeMin, Is.Zero); Assert.That(second.Workspace.ViewTimeMax, Is.EqualTo(7));
                Assert.That(second.Workspace.ScrollY, Is.Zero); Assert.That(second.Workspace.LeftWidth, Is.Not.EqualTo(310));
                Assert.That(second.Workspace.IsCollapsed(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().First()), Is.False);
                first.Workspace.SetCollapsed(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().First(), false);
                var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
                first.Presenter.Resize(new TimelineSize(1000, 500));
                var rect = first.Timeline.State.Clips.Single(c => c.Key == first.Presenter.KeyFor(clip)).Bounds;
                first.Presenter.Pointer(new PointerInput(PointerAction.Down, rect.X + rect.Width * .5f, rect.Y + 8));
                second.Presenter.Cancel();
                Assert.That(first.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Move));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True);
                Assert.That(second.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                first.Presenter.Cancel();
                Assert.That(first.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(first.Timeline.State.Guides, Is.Empty);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            }
            finally { first.Presenter?.Cancel(); second.Presenter?.Cancel(); first.DiscardChanges(); second.DiscardChanges(); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
        }

        [Test] public void ZoomAndFoldDoNotDirtyAuthoringData()
        {
            Workspace.ViewTimeMin = 1; Workspace.ViewTimeMax = 10;
            Workspace.SetCollapsed(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0), true);
            Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
            Assert.That(Workspace.ViewTimeMin, Is.EqualTo(1)); Assert.That(Workspace.ViewTimeMax, Is.EqualTo(10));
        }

        [Test] public void ImguiInvalidStretchRestoresLengthAndTrackReorderIsUndoable()
        {
            var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); var track = group.Tracks[0]; var clip = track.Clips[0];
            var gesture = new ClipGesture(EditorFixture.Shared.Session, EditorFixture.Shared.Clips);
            Assert.That(gesture.Begin(this, clip, new[] { clip }, 1, 175, 0), Is.True);
            gesture.Move(1025, true, new TimeTransform(0, 7, 700), new NBC.ActionEditor.Application.Settings.EditorSettings(NBC.ActionEditor.Application.Settings.TimeStepMode.Seconds, .01f, 30, false, true, 0, "Assets/"));
            gesture.Finish();
            group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); track = group.Tracks[0];
            Assert.That(track.Clips[0].Length, Is.EqualTo(1.5f));
            EditorFixture.Shared.Structure.MoveTrack(track, group, 3);
            Assert.That(group.Tracks[2], Is.SameAs(track));
            Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(track.Name));
        }

        [UnityTest] public IEnumerator ImguiInputNeedsPressAndEndsOutside()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus();
                yield return null; yield return null;
                var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
                var start = offset + new Vector2(90, 53);
                window.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = start, button = 0 });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = start, button = 0 });
                window.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = start + Vector2.down * 3, button = 0 });
                Assert.That(window.Timeline.State.Insertion.Visible, Is.False, "Exactly three pixels is not yet a structure drag.");
                window.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = start + Vector2.down * 4, button = 0 });
                Assert.That(window.Timeline.State.Insertion.Visible, Is.True);
                window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = start, button = 1 });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = offset + new Vector2(-10, 53), button = 0 });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(window.Timeline.State.Insertion.Visible, Is.False);
                Assert.That(EditorFixture.Shared.Session.IsDirty || EditorFixture.Shared.Session.IsEditing, Is.False);
                window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = start, button = 0 });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                window.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = start + Vector2.down * 4, button = 0 });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
    }
}
