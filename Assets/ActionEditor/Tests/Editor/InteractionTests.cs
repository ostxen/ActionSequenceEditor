using System.Collections;
using System.Linq;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public class InteractionTests
    {
        [UnityTest]
        public IEnumerator PlainWheelZoomPanAndContextMenusMatchOriginalEntryPoints()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1200, 700); window.Show(); yield return null;
                window.NewDocument();
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                var before = EditorJsonUtility.ToJson(window.Session.Document);
                window.Session.Workspace.ScrollTime = 0.5;
                var scale = window.Session.Workspace.PixelsPerSecond;
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = new Vector2(0, 1), mousePosition = canvas.LocalToWorld(new Vector2(400, 100)) }))
                { wheel.target = canvas; canvas.SendEvent(wheel); }
                Assert.That(window.Session.Workspace.PixelsPerSecond, Is.LessThan(scale));
                Assert.That(window.Session.Workspace.ScrollTime, Is.EqualTo(0.5));
                window.Interaction.Begin(new Vector2(400, 100), 2, false);
                window.Interaction.End(new Vector2(800, 180));
                Assert.That(window.Session.Workspace.ScrollTime, Is.Zero);
                Assert.That(window.Session.Workspace.ScrollY, Is.Zero);
                window.Interaction.Begin(new Vector2(canvas.Transform.ToX(1.26), 10), 0, false);
                window.Interaction.End(new Vector2(canvas.Transform.ToX(1.26), 10));
                Assert.That(window.Session.Workspace.Playhead, Is.EqualTo(1.3).Within(0.001));
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                var menu = new DropdownMenu();
                var position = new Vector2(canvas.Transform.ToX(1.26), canvas.Transform.ToY(1) + 10);
                using (var trigger = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 1, mousePosition = canvas.LocalToWorld(position) }))
                using (var context = ContextualMenuPopulateEvent.GetPooled(trigger, menu, canvas, null))
                { context.target = canvas; canvas.SendEvent(context); }
                var add = menu.MenuItems().OfType<DropdownMenuAction>().Single(a => a.name == "Add Event/actioneditor.signal");
                add.Execute();
                var item = window.Session.Document.Groups[0].Tracks[0].Events.Single();
                Assert.That(item.Start, Is.EqualTo(1.3).Within(0.001));
                menu = new DropdownMenu();
                using (var trigger = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 1,
                    mousePosition = canvas.LocalToWorld(new Vector2(canvas.Transform.ToX(item.Start), canvas.Transform.ToY(1) + 10)) }))
                using (var context = ContextualMenuPopulateEvent.GetPooled(trigger, menu, canvas, null))
                { context.target = canvas; canvas.SendEvent(context); }
                Assert.That(menu.MenuItems().OfType<DropdownMenuAction>().Select(a => a.name), Is.EquivalentTo(new[] { "Duplicate", "Delete" }));
                menu.MenuItems().OfType<DropdownMenuAction>().Single(a => a.name == "Duplicate").Execute();
                Assert.That(window.Session.Document.Groups[0].Tracks[0].Events.Count, Is.EqualTo(2));
                Undo.PerformUndo();
                Assert.That(window.Session.Document.Groups[0].Tracks[0].Events.Count, Is.EqualTo(1));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) { window.Session?.DiscardAndClose(); window.Close(); } }
        }

        [UnityTest]
        public IEnumerator DragCandidateCancelCommitUndoAndNoPressAreConsistent()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1200, 700); window.Show(); yield return null;
                window.NewDocument();
                var track = window.Session.Document.Groups[0].Tracks[0];
                var item = new IntervalEvent { Start = 1, Duration = 1 };
                track.Events.Add(item);
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                // Use the same gesture entry points as Pointer callbacks, with real laid-out coordinates.
                var controller = window.Interaction;
                var start = new Vector2(canvas.Transform.ToX(1.5), canvas.Transform.ToY(1) + 10);
                controller.Move(start + new Vector2(100, 0));
                Assert.That(DocumentCommands.Event(window.Session.Document, item.Id).Start, Is.EqualTo(1));
                controller.Begin(start, 0, false); controller.Move(start + new Vector2(100, 0));
                Assert.That(DocumentCommands.Event(window.Session.Document, item.Id).Start, Is.EqualTo(1));
                controller.Cancel();
                Assert.That(controller.IsActive, Is.False);
                controller.Begin(start, 0, false); controller.End(start + new Vector2(100, 0)); controller.End(start + new Vector2(200, 0));
                Assert.That(DocumentCommands.Event(window.Session.Document, item.Id).Start, Is.EqualTo(2));
                Undo.PerformUndo();
                Assert.That(DocumentCommands.Event(window.Session.Document, item.Id).Start, Is.EqualTo(1));
                controller.Begin(start, 0, false);
                canvas.Focus();
                using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                {
                    escape.target = canvas;
                    canvas.SendEvent(escape);
                }
                Assert.That(controller.IsActive, Is.False);
                // Actual Pointer callbacks acquire and release capture on the real panel.
                var capturedId = -1;
                using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = canvas.LocalToWorld(start) }))
                { capturedId = down.pointerId; down.target = canvas; canvas.SendEvent(down); }
                Assert.That(controller.IsActive, Is.True);
                yield return null;
                Assert.That(canvas.HasPointerCapture(capturedId), Is.True, "Pointer callback must capture its actual pointer ID.");
                canvas.ReleasePointer(capturedId);
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(controller.IsActive, Is.False);
                controller.Begin(start, 0, false);
                using (var focusOut = FocusOutEvent.GetPooled()) { focusOut.target = canvas; canvas.SendEvent(focusOut); }
                Assert.That(controller.IsActive, Is.False);
                controller.Begin(start, 0, false);
                canvas.RemoveFromHierarchy();
                Assert.That(controller.IsActive, Is.False);
                controller.Zoom(400, -2);
                Assert.That(window.Session.Workspace.ScrollTime, Is.EqualTo(0));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) window.Close(); }
        }

        [UnityTest]
        public IEnumerator ResizeBoxSelectPanAndLocksUseRealCanvasCoordinates()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1200, 700); window.Show(); yield return null;
                window.NewDocument();
                var track = window.Session.Document.Groups[0].Tracks[0];
                var a = new IntervalEvent { Start = 1, Duration = 1 };
                var b = new SignalEvent { Start = 3 };
                track.Events.Add(a); track.Events.Add(b);
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                var controller = window.Interaction;
                var y = canvas.Transform.ToY(1) + 10;
                controller.Begin(new Vector2(canvas.Transform.ToX(2) - 1, y), 0, false);
                controller.End(new Vector2(canvas.Transform.ToX(2.5) - 1, y));
                Assert.That(DocumentCommands.Event(window.Session.Document, a.Id).Duration, Is.EqualTo(1.5).Within(0.001));
                Undo.PerformUndo();
                controller.Begin(new Vector2(canvas.Transform.ToX(3.5), y + 18), 0, false);
                controller.End(new Vector2(canvas.Transform.ToX(0.5), y - 5));
                Assert.That(window.Session.Selection.Count, Is.EqualTo(2));
                var oldScroll = window.Session.Workspace.ScrollTime;
                controller.Begin(new Vector2(300, 100), 2, false); controller.End(new Vector2(200, 130));
                Assert.That(window.Session.Workspace.ScrollTime, Is.GreaterThan(oldScroll));
                DocumentCommands.Event(window.Session.Document, a.Id).Locked = true;
                controller.Begin(new Vector2(canvas.Transform.ToX(1.5), y), 0, false);
                Assert.That(controller.IsActive, Is.False);
                Assert.That(DocumentCommands.Event(window.Session.Document, a.Id).Start, Is.EqualTo(1));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) window.Close(); }
        }

        [UnityTest]
        public IEnumerator SnapMultiMoveInvalidOverlapAndOutsideReleaseAreAtomic()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1400, 700); window.Show(); yield return null;
                window.NewDocument();
                var track = window.Session.Document.Groups[0].Tracks[0];
                track.Overlap = OverlapPolicy.Reject;
                var a = new IntervalEvent { Start = 1, Duration = 1 };
                var b = new IntervalEvent { Start = 3, Duration = 1 };
                track.Events.Add(a); track.Events.Add(b);
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                var controller = window.Interaction;
                var y = canvas.Transform.ToY(1) + 10;
                Vector2 At(double time) => new(canvas.Transform.ToX(time), y);
                var before = EditorJsonUtility.ToJson(window.Session.Document);
                controller.Begin(At(1.5), 0, false); controller.End(At(3.5));
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                Assert.That(window.Session.Selection, Is.Empty);
                Assert.That(window.rootVisualElement.Q<Label>().panel, Is.Not.Null);
                // Start edge snaps without changing the end boundary.
                window.Session.Workspace.SnapSeconds = 0.25;
                controller.Begin(At(1), 0, false); controller.End(At(1.38));
                Assert.That(DocumentCommands.Event(window.Session.Document, a.Id).Start, Is.EqualTo(1.5));
                Assert.That(DocumentCommands.Event(window.Session.Document, a.Id).Duration, Is.EqualTo(0.5));
                Undo.PerformUndo();
                Assert.That(window.Session.Selection, Is.Empty);
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                // Multi-selection moves keep relative offsets and are a single Undo operation.
                window.Session.Selection.Add(a.Id); window.Session.Selection.Add(b.Id);
                controller.Begin(At(1.5), 0, false);
                controller.Move(At(1.7)); controller.Move(At(1.8)); controller.End(At(2.0));
                Assert.That(DocumentCommands.Event(window.Session.Document, a.Id).Start, Is.EqualTo(1.5));
                Assert.That(DocumentCommands.Event(window.Session.Document, b.Id).Start, Is.EqualTo(3.5));
                Undo.PerformUndo();
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                Assert.That(window.Session.Selection, Is.EquivalentTo(new[] { a.Id, b.Id }));
                controller.Begin(At(1.5), 0, false); controller.Move(At(2));
                controller.End(new Vector2(canvas.contentRect.xMax + 20, y));
                Assert.That(controller.IsActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                // Cancellation restores view state as well as the definition.
                var scroll = window.Session.Workspace.ScrollTime;
                controller.Begin(At(1.5), 2, false); controller.Move(At(2)); controller.Cancel();
                Assert.That(window.Session.Workspace.ScrollTime, Is.EqualTo(scroll));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) window.Close(); }
        }
    }
}
