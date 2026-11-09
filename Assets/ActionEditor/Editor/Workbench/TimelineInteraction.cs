using System;
using System.Linq;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    public sealed class TimelineInteraction : IDisposable
    {
        private enum Gesture { None, Move, ResizeStart, ResizeEnd, Marquee, Pan, Seek }
        private readonly TimelineCanvas canvas;
        private readonly DocumentSession session;
        private readonly Action<string> error;
        private Gesture gesture;
        private DocumentTransaction edit;
        private Vector2 origin;
        private double scroll;
        private float scrollY;
        private string activeId;
        private string[] selectionBefore;
        private int pointerId = -1;
        private readonly IVisualElementScheduledItem captureWatch;
        private double playhead;
        public bool IsActive => gesture != Gesture.None;

        public TimelineInteraction(TimelineCanvas canvas, Action<string> error)
        {
            this.canvas = canvas; session = canvas.Session; this.error = error;
            canvas.RegisterCallback<PointerDownEvent>(OnDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnMove);
            canvas.RegisterCallback<PointerUpEvent>(OnUp);
            canvas.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            canvas.RegisterCallback<KeyDownEvent>(OnKey);
            canvas.RegisterCallback<FocusOutEvent>(OnFocusOut);
            canvas.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            canvas.RegisterCallback<WheelEvent>(OnWheel);
            captureWatch = canvas.schedule.Execute(() =>
            {
                if (pointerId >= 0 && !canvas.HasPointerCapture(pointerId)) Cancel();
            }).Every(16);
            captureWatch.Pause();
        }

        public void Begin(Vector2 position, int button, bool additive)
        {
            Cancel();
            if (session.Document == null) return;
            origin = position; scroll = session.Workspace.ScrollTime; scrollY = session.Workspace.ScrollY;
            playhead = session.Workspace.Playhead;
            selectionBefore = session.Selection.ToArray();
            if (button == 2) { gesture = Gesture.Pan; return; }
            if (button != 0) return;
            if (position.y < TimelineTransform.RulerHeight && position.x >= TimelineTransform.HeaderWidth)
            {
                gesture = Gesture.Seek; Move(position); return;
            }
            if (position.x < TimelineTransform.HeaderWidth) return;
            var item = canvas.Hit(position);
            if (item == null)
            {
                gesture = Gesture.Marquee;
                if (!additive) session.Selection.Clear();
                canvas.Marquee = new Rect(position, Vector2.zero);
                return;
            }
            if (additive && session.Selection.Contains(item.Id)) { session.Selection.Remove(item.Id); session.NotifyChanged(); return; }
            if (!session.Selection.Contains(item.Id))
            {
                if (!additive) session.Selection.Clear();
                session.Selection.Add(item.Id);
            }
            activeId = item.Id;
            if (session.Document.Groups.Any(g => g.Tracks.Any(t => t.Events.Any(e => session.Selection.Contains(e.Id))
                && (g.Locked || t.Locked || t.Events.Any(e => session.Selection.Contains(e.Id) && e.Locked)))))
            {
                error?.Invoke("Selection includes locked content."); session.NotifyChanged(); return;
            }
            var row = canvas.Rows().FindIndex(r => r.Track.Events.Exists(e => e.Id == item.Id));
            var rect = canvas.Transform.EventRect(item, row);
            gesture = item.Kind == EventKind.Interval && Math.Abs(position.x - rect.xMin) <= 6 ? Gesture.ResizeStart
                : item.Kind == EventKind.Interval && Math.Abs(position.x - rect.xMax) <= 6 ? Gesture.ResizeEnd : Gesture.Move;
            edit = new DocumentTransaction(session, "Timeline gesture");
            edit.Selection.Clear(); edit.Selection.AddRange(session.Selection);
            canvas.DisplayDocument = edit.Candidate;
            session.NotifyChanged();
        }

        public void Move(Vector2 position)
        {
            if (!IsActive) return;
            var delta = position - origin;
            if (gesture == Gesture.Pan)
            {
                session.Workspace.ScrollTime = scroll - delta.x / session.Workspace.PixelsPerSecond;
                session.Workspace.ScrollY = Math.Max(0, scrollY - delta.y);
            }
            else if (gesture == Gesture.Seek) session.Workspace.Playhead = Math.Clamp(canvas.Transform.ToTime(position.x), 0, session.Document.Duration);
            else if (gesture == Gesture.Marquee) canvas.Marquee = TimelineTransform.SelectionRect(origin, position);
            else if (edit != null)
            {
                EditorUtility.CopySerialized(session.Document, edit.Candidate);
                if (delta.sqrMagnitude < 9) { canvas.MarkDirtyRepaint(); return; }
                var source = DocumentCommands.Event(session.Document, activeId);
                var change = delta.x / session.Workspace.PixelsPerSecond;
                var snap = session.Workspace.SnapSeconds;
                double Snap(double value) => snap > 0 ? Math.Round(value / snap, MidpointRounding.AwayFromZero) * snap : value;
                if (gesture == Gesture.Move)
                {
                    var move = Snap(source.Start + change) - source.Start;
                    DocumentCommands.Move(edit, edit.Selection, move);
                }
                else if (gesture == Gesture.ResizeStart)
                {
                    var start = Snap(source.Start + change);
                    DocumentCommands.Resize(edit, activeId, start, source.Start + source.Duration - start);
                }
                else DocumentCommands.Resize(edit, activeId, source.Start, Snap(source.Start + source.Duration + change) - source.Start);
            }
            canvas.MarkDirtyRepaint();
        }

        public void End(Vector2 position)
        {
            if (!IsActive) return;
            if (!canvas.contentRect.Contains(position)) { Cancel(); return; }
            Move(position);
            var completed = gesture;
            gesture = Gesture.None;
            var transaction = edit; edit = null;
            canvas.DisplayDocument = null;
            if (completed == Gesture.Marquee && canvas.Marquee.HasValue)
            {
                var rows = canvas.Rows();
                for (var row = 0; row < rows.Count; row++)
                    foreach (var item in rows[row].Track.Events)
                        if (canvas.Marquee.Value.Overlaps(canvas.Transform.EventRect(item, row)) && !session.Selection.Contains(item.Id)) session.Selection.Add(item.Id);
            }
            canvas.Marquee = null;
            try
            {
                if (transaction != null)
                {
                    session.Selection.Clear(); session.Selection.AddRange(selectionBefore);
                    transaction.Commit();
                }
            }
            catch (Exception exception) { error?.Invoke(exception.Message); }
            finally { transaction?.Dispose(); ReleasePointer(); }
            session.NotifyChanged();
        }

        public void Cancel()
        {
            if (!IsActive) return;
            gesture = Gesture.None;
            edit?.Dispose(); edit = null;
            canvas.DisplayDocument = null; canvas.Marquee = null;
            session.Workspace.ScrollTime = scroll; session.Workspace.ScrollY = scrollY;
            session.Workspace.Playhead = playhead;
            session.Selection.Clear(); session.Selection.AddRange(selectionBefore ?? Array.Empty<string>());
            ReleasePointer(); canvas.MarkDirtyRepaint();
            session.NotifyChanged();
        }

        public void Zoom(float x, float delta)
        {
            Cancel();
            var time = canvas.Transform.ToTime(x);
            session.Workspace.PixelsPerSecond = Math.Clamp(session.Workspace.PixelsPerSecond * Math.Pow(1.12, -delta), 10, 5000);
            session.Workspace.ScrollTime = time - (x - TimelineTransform.HeaderWidth) / session.Workspace.PixelsPerSecond;
            canvas.MarkDirtyRepaint();
        }

        private void OnDown(PointerDownEvent evt)
        {
            canvas.Focus(); Begin(evt.localPosition, evt.button, evt.ctrlKey || evt.shiftKey);
            if (IsActive) { pointerId = evt.pointerId; canvas.CapturePointer(pointerId); captureWatch.Resume(); evt.StopPropagation(); }
        }
        private void OnMove(PointerMoveEvent evt) { if (evt.pointerId == pointerId) Move(evt.localPosition); }
        private void OnUp(PointerUpEvent evt) { if (evt.pointerId == pointerId) { End(evt.localPosition); evt.StopPropagation(); } }
        private void OnCaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == pointerId) Cancel(); }
        private void OnFocusOut(FocusOutEvent evt) => Cancel();
        private void OnDetach(DetachFromPanelEvent evt) => Cancel();
        private void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape) { Cancel(); evt.StopPropagation(); }
        }
        private void OnWheel(WheelEvent evt)
        {
            if (evt.ctrlKey) Zoom(evt.localMousePosition.x, evt.delta.y);
            else { Cancel(); session.Workspace.ScrollY = Math.Max(0, session.Workspace.ScrollY + evt.delta.y * 20); canvas.MarkDirtyRepaint(); }
            evt.StopPropagation();
        }
        private void ReleasePointer()
        {
            captureWatch.Pause();
            var id = pointerId; pointerId = -1;
            if (id >= 0 && canvas.HasPointerCapture(id)) canvas.ReleasePointer(id);
        }
        public void Dispose()
        {
            Cancel();
            captureWatch.Pause();
            canvas.UnregisterCallback<PointerDownEvent>(OnDown); canvas.UnregisterCallback<PointerMoveEvent>(OnMove);
            canvas.UnregisterCallback<PointerUpEvent>(OnUp); canvas.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            canvas.UnregisterCallback<KeyDownEvent>(OnKey); canvas.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            canvas.UnregisterCallback<DetachFromPanelEvent>(OnDetach); canvas.UnregisterCallback<WheelEvent>(OnWheel);
        }
    }
}
