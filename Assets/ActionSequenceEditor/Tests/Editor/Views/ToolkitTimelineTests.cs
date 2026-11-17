using ActionSequenceEditor.Views.IMGUI;
using ImguiWindow = ActionSequenceEditor.Views.IMGUI.ActionSequenceEditorWindow;
using ToolkitWindow = ActionSequenceEditor.Views.UIToolkit.ActionSequenceEditorWindow;
using ToolkitSettings = ActionSequenceEditor.Views.UIToolkit.SettingsWindow;
using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Presentation.Timeline;
using ActionSequenceEditor.Model;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ActionSequenceEditor.Samples.FeatureSample;
using ActionSequenceEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Tests
{
    public sealed class ToolkitTimelineTests : SharedContextFixture
    {
        [UnityTest] public IEnumerator ToolkitTextFocusDoesNotRunTimelineShortcutsAndWheelCancelsOwnedDrag()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600); yield return null; yield return null;
                var canvas = window.Canvas;
                var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; window.Select(clip);
                var input = new TextField { value = "editable text" }; canvas.Add(input); input.Focus(); yield return null;
                var before = ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(EditorFixture.Shared.Sequence);
                var escapeReachedInput = false;
                input.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) escapeReachedInput = true; });
                foreach (var key in new[] { KeyCode.Delete, KeyCode.C, KeyCode.X, KeyCode.V, KeyCode.D, KeyCode.Z, KeyCode.Escape })
                {
                    using var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, control = key != KeyCode.Delete });
                    e.target = input.Q("unity-text-input"); canvas.SendEvent(e);
                }
                Assert.That(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(EditorFixture.Shared.Sequence), Is.EqualTo(before));
                Assert.That(escapeReachedInput, Is.True, "The root timeline handler must not steal text-editor Escape.");
                Assert.That(EditorFixture.Shared.Session.IsDirty || EditorFixture.Shared.Session.IsEditing, Is.False);
                input.RemoveFromHierarchy(); canvas.Focus();
                var center = canvas.ClipRect(clip, 1).center;
                Pointer(canvas, EventType.MouseDown, center); Pointer(canvas, EventType.MouseDrag, center + new Vector2(canvas.Scale * .5f, 0), shift: true);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True);
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, mousePosition = canvas.LocalToWorld(center), delta = Vector2.up }))
                { wheel.target = canvas; canvas.SendEvent(wheel); }
                Assert.That(EditorFixture.Shared.Session.IsEditing || EditorFixture.Shared.Session.IsDirty, Is.False);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                Pointer(canvas, EventType.MouseUp, center);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ToolkitUsesSameGraphToolkitInspectorAndImguiGeometry()
        {

            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.position = new Rect(50, 50, 1000, 600); window.Show();
                yield return null; yield return null;
                Assert.That(window.Canvas.Presenter.KeyFor(EditorFixture.Shared.Sequence.AllTracks.First().Clips.First()), Is.GreaterThan(0));
                Assert.That(window.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(window.rootVisualElement.Q("timeline-header").resolvedStyle.height, Is.EqualTo(40));
                Assert.That(window.rootVisualElement.Q("timeline-footer").resolvedStyle.height, Is.EqualTo(16).Within(.001f));
                Assert.That(window.rootVisualElement.Q("timeline-ruler").worldBound.yMin - window.rootVisualElement.worldBound.yMin, Is.EqualTo(20).Within(1));
                Assert.That(window.Canvas.State.Rows.Count, Is.EqualTo(10));
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().First();
                var groupKey = window.Canvas.Presenter.KeyFor(group);
                window.Canvas.Presenter.ToggleLock(groupKey); yield return null;
                var stamp = window.Canvas.Q<Label>("timeline-row-stamp-" + groupKey);
                Assert.That(stamp, Is.Not.Null);
                Assert.That(stamp.resolvedStyle.fontSize, Is.EqualTo(12));
                Assert.That(stamp.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.MiddleCenter));
                window.Canvas.Presenter.ToggleLock(groupKey); yield return null;
                Assert.That(window.Canvas.Q<Label>("timeline-row-stamp-" + groupKey), Is.Null);
                var row = window.Canvas.State.Rows.First();
                var renameEntry = window.Canvas.Presenter.Menu(30, row.Bounds.Y + row.Bounds.Height * .5f).Single(entry => entry.Text == EditorText.Rename);
                var previousRenames = Resources.FindObjectsOfTypeAll<Views.UIToolkit.RenameWindow>();
                window.Canvas.Presenter.Invoke(renameEntry);
                var rename = Resources.FindObjectsOfTypeAll<Views.UIToolkit.RenameWindow>().Except(previousRenames).Single();
                try
                {
                    yield return null;
                    rename.rootVisualElement.Q<TextField>("rename-name").value = "Native menu rename";
                    var confirmRename = rename.rootVisualElement.Q<Button>("dialog-submit");
                    using (var submitRename = NavigationSubmitEvent.GetPooled()) { submitRename.target = confirmRename; confirmRename.SendEvent(submitRename); }
                    Assert.That(group.Name, Is.EqualTo("Native menu rename"));
                    Undo.PerformUndo(); yield return null;
                }
                finally { if (rename != null) rename.Close(); }
                var previousExports = Resources.FindObjectsOfTypeAll<Views.UIToolkit.SequenceExportWindow>();
                window.Sequence.Invoke(ActionSequenceEditor.Presentation.Contracts.SequenceIntent.Export);
                var export = Resources.FindObjectsOfTypeAll<Views.UIToolkit.SequenceExportWindow>().Except(previousExports).Single();
                try
                {
                    yield return null;
                    Assert.That(export.rootVisualElement.Q<DropdownField>("export-format"), Is.Not.Null);
                    Assert.That(export.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                    Assert.That(export.ExportTo("actioneditor.json", null), Is.False);
                }
                finally { export.Close(); }
                window.Canvas.ShowGroupMenu(); yield return null;
                Assert.That(window.rootVisualElement.Q(className: "unity-base-dropdown"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                using (var escapeMenu = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                { escapeMenu.target = window.rootVisualElement.Q(className: "unity-base-dropdown"); window.rootVisualElement.SendEvent(escapeMenu); }
                window.Canvas.Presenter.ToggleFold(window.Canvas.Presenter.KeyFor(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(2)));
                Assert.That(window.Canvas.State.Rows.Count, Is.EqualTo(8));
                var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; window.Select(clip);
                var view = window.CreatePropertyView(); window.rootVisualElement.Add(view); yield return null;
                var count = view.Query<IntegerField>().ToList().First(f => f.label == "次数");
                count.value = 7; yield return null;
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(7));
                Undo.PerformUndo(); yield return null;
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                window.rootVisualElement.Remove(view);
                window.DiscardChanges();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ToolkitDragResizeCancelAndCaptureLossUseRealPointerEvents()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600); window.Focus(); yield return null; yield return null;
                var canvas = window.Canvas;
                var item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                var rect = canvas.ClipRect(item, 1); var center = rect.center;
                Pointer(canvas, EventType.MouseDrag, center + new Vector2(30, 0));
                Assert.That(item.StartTime, Is.EqualTo(.25f));
                Pointer(canvas, EventType.MouseDown, center);
                Pointer(canvas, EventType.MouseDrag, center + new Vector2(canvas.Scale, 0));
                Pointer(canvas, EventType.MouseUp, center + new Vector2(canvas.Scale, 0));
                Assert.That(item.StartTime, Is.EqualTo(1.3f).Within(.051f)); // configured 0.1-second snapping
                Undo.PerformUndo(); item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                Assert.That(item.StartTime, Is.EqualTo(.25f));
                // Undo重建Inspector可延迟转移原生焦点；下一次拖动必须从已聚焦窗口开始。
                yield return null; yield return null;
                window.Focus(); canvas.Focus(); yield return null; yield return null;
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(window));
                rect = canvas.ClipRect(item, 1); center = rect.center;
                Pointer(canvas, EventType.MouseDown, center); Pointer(canvas, EventType.MouseDrag, center + new Vector2(30, 0));
                yield return null;
                var captured = canvas.PointerId;
                Assert.That(captured, Is.GreaterThanOrEqualTo(0));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True, "An open original window must not commit the native drag during repaint.");
                canvas.ReleasePointer(captured);
                Pointer(canvas, EventType.MouseDrag, center + new Vector2(30, 0));
                yield return null;
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; rect = canvas.ClipRect(item, 1);
                var edge = new Vector2(rect.xMax - 2, rect.center.y);
                Pointer(canvas, EventType.MouseDown, edge); Pointer(canvas, EventType.MouseDrag, edge + new Vector2(canvas.Scale, 0));
                Pointer(canvas, EventType.MouseUp, edge + new Vector2(canvas.Scale, 0));
                Assert.That(item.Length, Is.GreaterThan(2.4f));
                window.DiscardChanges();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator InactiveImguiLifecycleDoesNotCancelToolkitOwnedDrag()
        {

            ImguiWindow original = null; ToolkitWindow native = null;
            try
            {
                original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                native = ScriptableObject.CreateInstance<ToolkitWindow>(); native.Show();
                native.position = new Rect(50, 50, 1000, 600); native.Focus();
                yield return null; yield return null;
                var item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                var canvas = native.Canvas; var down = canvas.ClipRect(item, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                var moved = item.StartTime;
                Assert.That(moved, Is.GreaterThan(.25f));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(ImguiWindow).GetMethod("OnLostFocus", flags).Invoke(original, null);
                typeof(ImguiWindow).GetMethod("OnDisable", flags).Invoke(original, null);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True, "An inactive view's disable handler must not roll back the native gesture.");
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(moved));
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale, 0));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                Undo.PerformUndo();
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                if (original != null) { original.DiscardChanges(); original.Close(); }

            }
        }

        [UnityTest] public IEnumerator ToolkitWrongButtonReleaseAndLostFocusRespectGestureOwnership()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600);
                yield return null; yield return null;
                var canvas = window.Canvas; var item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                var down = canvas.ClipRect(item, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True, "The left drag must be active before the unrelated release.");
                Pointer(canvas, EventType.MouseUp, down, button: 1);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True, "A right-button release cannot commit a left-button drag.");
                typeof(ToolkitWindow).GetMethod("OnLostFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(window, null);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; down = canvas.ClipRect(item, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale, 0));
                Assert.That(EditorFixture.Shared.Session.IsDirty, Is.True);
                Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
                item = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; down = canvas.ClipRect(item, 1).center;
                window.Select(item);
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                EditorFixture.Shared.Session.DiscardChanges();
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                Assert.That(EditorFixture.Shared.Selection.Items, Is.Empty, "Rollback must not retain references into the discarded graph.");
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * 2, 0));
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * 2, 0));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
                yield return null; yield return null;
                Assert.That(window.rootVisualElement.Q("timeline-drag-guides").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator InactiveToolkitCancellationDoesNotTouchImguiWindowToken()
        {

            ImguiWindow original = null; ToolkitWindow native = null;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                original = ScriptableObject.CreateInstance<ImguiWindow>(); original.position = new Rect(60, 60, 1000, 600); original.Show();
                native = ScriptableObject.CreateInstance<ToolkitWindow>(); native.Show();
                yield return null; yield return null;
                original.Focus(); original.Repaint(); yield return null; yield return null;
                var clip = EditorFixture.Shared.Sequence.AllTracks.First().Clips[0]; var down = ImguiPoint(original, clip);
                original.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = down });
                original.SendEvent(new Event { type = EventType.MouseDrag, button = 0, shift = true, mousePosition = down + Vector2.right * 30 });
                var moved = clip.StartTime;
                Assert.That(moved, Is.GreaterThan(.25f));
                native.Canvas.Cancel();
                typeof(ToolkitWindow).GetMethod("OnLostFocus", flags).Invoke(native, null);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True);
                Assert.That(original.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Move));
                Assert.That(clip.StartTime, Is.EqualTo(moved));
                typeof(ImguiWindow).GetMethod("OnLostFocus", flags).Invoke(original, null);
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                Assert.That(original.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(EditorFixture.Shared.Sequence.AllTracks.First().Clips[0].StartTime, Is.EqualTo(.25f));
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                if (original != null) { original.DiscardChanges(); original.Close(); }

            }
        }

        [UnityTest] public IEnumerator ToolkitMultiClipDragGuidesMatchBoundsSnappingAndGestureLifetime()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600); yield return null; yield return null;
                var canvas = window.Canvas; var root = window.rootVisualElement;
                var first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; var second = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips[0];
                window.Select(first, second); yield return null;
                var overlay = root.Q("timeline-drag-guides");
                var startGuide = overlay.Q("timeline-drag-guide-0"); var endGuide = overlay.Q("timeline-drag-guide-1");
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                var down = canvas.ClipRect(first, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .12f, 0), shift: true);
                window.SyncView(); yield return null; yield return null;
                Assert.That(EditorFixture.Shared.Selection.Items.Count(), Is.EqualTo(2));
                Assert.That(second.StartTime - first.StartTime, Is.EqualTo(.75f).Within(.0001f));
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(startGuide.Q<Label>().text, Is.EqualTo(first.StartTime.ToString("0.00")));
                Assert.That(endGuide.Q<Label>().text, Is.EqualTo(second.EndTime.ToString("0.00")));
                Assert.That(startGuide.ClassListContains("timeline-guide-solid"), Is.False);
                Assert.That(endGuide.ClassListContains("timeline-guide-solid"), Is.False);
                Assert.That(startGuide.worldBound.xMin - root.worldBound.xMin, Is.EqualTo(canvas.X(first.StartTime)).Within(1));
                Assert.That(overlay.worldBound.yMin - root.worldBound.yMin, Is.EqualTo(20).Within(1));
                Assert.That(overlay.worldBound.yMax - root.worldBound.yMin, Is.EqualTo(root.contentRect.height - 16).Within(1));
                window.Repaint(); yield return null; Capture(window, "shared-json-native-multi-drag-dashed.png");
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .75f, 0));
                window.SyncView(); yield return null; yield return null;
                Assert.That(first.StartTime, Is.EqualTo(1).Within(.0001f));
                Assert.That(startGuide.ClassListContains("timeline-guide-solid"), Is.True);
                window.Repaint(); yield return null; Capture(window, "shared-json-native-multi-drag-snapped.png");
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * .75f, 0)); yield return null;
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Undo.PerformUndo(); yield return null;
                first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                Assert.That(first.StartTime, Is.EqualTo(.25f));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips[0].StartTime, Is.EqualTo(1));
                window.Select(first); yield return null;
                foreach (var left in new[] { true, false })
                {
                    first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; var rect = canvas.ClipRect(first, 1);
                    var edge = new Vector2(left ? rect.xMin + 2 : rect.xMax - 2, rect.center.y);
                    Pointer(canvas, EventType.MouseDown, edge); Pointer(canvas, EventType.MouseDrag, edge + new Vector2(canvas.Scale * .12f, 0), shift: true);
                    yield return null; yield return null;
                    Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(startGuide.resolvedStyle.display, Is.EqualTo(left ? DisplayStyle.Flex : DisplayStyle.None));
                    Assert.That(endGuide.resolvedStyle.display, Is.EqualTo(left ? DisplayStyle.None : DisplayStyle.Flex));
                    if (left)
                    {
                        using var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                        escape.target = canvas; canvas.SendEvent(escape);
                    }
                    else
                    {
                        var captured = canvas.PointerId;
                        canvas.ReleasePointer(captured); Pointer(canvas, EventType.MouseDrag, edge); yield return null;
                    }
                    yield return null;
                    Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                    Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].Length, Is.EqualTo(1.5f));
                }
                canvas.Dispose(); Assert.That(root.Q("timeline-drag-guides"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ToolkitClipEdgesShowResizeCursorAndClearItOutsideEditableEdges()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600); yield return null; yield return null;
                var canvas = window.Canvas; var clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; window.Select(clip);
                yield return null;
                var rect = canvas.ClipRect(clip, 1);
                Pointer(canvas, EventType.MouseDrag, rect.center); yield return null;
                var normal = ComputedCursor(canvas);
                foreach (var x in new[] { rect.xMin + 2, rect.xMax - 2 })
                {
                    Pointer(canvas, EventType.MouseDrag, new Vector2(x, rect.center.y)); yield return null;
                    Assert.That(canvas.ClassListContains("timeline-resize-horizontal"), Is.True);
                    Assert.That(ComputedCursor(canvas), Is.Not.EqualTo(normal), "USS must apply a real editor resize cursor, not just a class name.");
                }
                Pointer(canvas, EventType.MouseDrag, new Vector2(rect.xMin + Styles.ClipScaleRectWidth + 1, rect.center.y)); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                var marker = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[2].Clips[0];
                Pointer(canvas, EventType.MouseDrag, canvas.ClipRect(marker, 3).center); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                var edge = new Vector2(rect.xMax - 2, rect.center.y);
                Pointer(canvas, EventType.MouseDown, edge); Pointer(canvas, EventType.MouseDrag, new Vector2(canvas.contentRect.width + 20, rect.center.y)); yield return null;
                Assert.That(canvas.ClassListContains("timeline-resize-horizontal"), Is.True, "Resizing retains its cursor outside the clip.");
                canvas.Cancel(); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                clip = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; rect = canvas.ClipRect(clip, 1);
                Pointer(canvas, EventType.MouseDrag, new Vector2(rect.xMin + 2, rect.center.y)); yield return null;
                canvas.Cancel();
                Assert.That(canvas.ClassListContains("timeline-resize-horizontal"), Is.False);
                EditorFixture.Shared.Nodes.ToggleLock(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]); yield return null;
                Pointer(canvas, EventType.MouseDrag, new Vector2(rect.xMin + 2, rect.center.y)); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        private static object ComputedCursor(VisualElement element)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            // Unity 2022 does not expose cursor through IResolvedStyle.
            var field = typeof(VisualElement).GetFields(flags).FirstOrDefault(f => f.FieldType.Name == "ComputedStyle");
            Assert.That(field, Is.Not.Null, "Computed style storage must be available for cursor verification.");
            var computed = field.GetValue(element);
            return computed.GetType().GetProperty("cursor", flags).GetValue(computed);
        }

        [UnityTest] public IEnumerator RulerContinuousSeekAndEscapeRestoreTime()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.position = new Rect(50, 50, 1000, 600); window.Show(); yield return null; yield return null;
                var root = window.rootVisualElement; var canvas = window.Canvas;
                window.Seek(.5f);
                Pointer(root, EventType.MouseDown, new Vector2(canvas.X(1), 30));
                Assert.That(canvas.Presenter.Mode.ToString(), Is.EqualTo("Seek"), "ruler down mode");
                Pointer(root, EventType.MouseDrag, new Vector2(canvas.X(2), 30));
                Assert.That(canvas.Presenter.Mode.ToString(), Is.EqualTo("Seek"), "ruler move mode");
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False, "Seeking is not authoring.");
                Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.EqualTo(2).Within(.051));
                var receivedKey = false;
                root.RegisterCallback<KeyDownEvent>(_ => receivedKey = true, TrickleDown.TrickleDown);
                using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                { escape.target = canvas; canvas.SendEvent(escape); }
                yield return null;
                Assert.That(receivedKey, Is.True, "Escape must reach the real window panel");
                Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.EqualTo(.5f));
                Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
    }
}
