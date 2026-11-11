using System;
using System.Collections.Generic;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    public readonly struct TimelineTransform
    {
        public const float HeaderWidth = 249;
        public const float RulerHeight = 20;
        public const float RowHeight = 28;
        public readonly float LeftWidth;
        public readonly double Scale;
        public readonly double Scroll;
        public readonly float ScrollY;
        public TimelineTransform(double scale, double scroll, float scrollY, float leftWidth = HeaderWidth)
        {
            if (!TimelineTime.IsFinite(scale) || scale <= 0 || !TimelineTime.IsFinite(scroll) || !TimelineTime.IsFinite(scrollY)) throw new ArgumentOutOfRangeException();
            Scale = scale; Scroll = scroll; ScrollY = scrollY; LeftWidth = leftWidth;
        }
        public float ToX(double time) => LeftWidth + (float)((time - Scroll) * Scale);
        public double ToTime(float x) => Scroll + (x - LeftWidth) / Scale;
        public float ToY(int row) => RulerHeight + row * RowHeight - ScrollY;
        public Rect EventRect(TimelineEvent item, int row)
        {
            var width = item.Kind == EventKind.Point ? 10 : Math.Max(6, (float)(item.Duration * Scale));
            return new Rect(ToX(item.Start) - (item.Kind == EventKind.Point ? 5 : 0), ToY(row) + 2, width, RowHeight - 4);
        }
        public static Rect SelectionRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Max(a.x, b.x), Math.Max(a.y, b.y));
    }

    public readonly struct TimelineRow
    {
        public readonly DocumentGroup Group;
        public readonly DocumentTrack Track;
        public TimelineRow(DocumentGroup group, DocumentTrack track) { Group = group; Track = track; }
    }

    public sealed class TimelineCanvas : VisualElement
    {
        public readonly DocumentSession Session;
        public ActionDocument DisplayDocument;
        public Rect? Marquee;
        public event Action ViewChanged;
        public void NotifyViewChanged() { MarkDirtyRepaint(); ViewChanged?.Invoke(); }
        public float LeftWidth => Session.Workspace.TrackHeaderWidth + 9;
        public TimelineTransform Transform => new(Session.Workspace.PixelsPerSecond, Session.Workspace.ScrollTime, Session.Workspace.ScrollY, LeftWidth);
        public TimelineCanvas(DocumentSession session)
        {
            Session = session;
            name = "timeline-canvas";
            focusable = true;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
        }

        public List<TimelineRow> Rows()
        {
            var rows = new List<TimelineRow>();
            var document = DisplayDocument != null ? DisplayDocument : Session.Document;
            if (document == null) return rows;
            foreach (var group in document.Groups)
            {
                rows.Add(new TimelineRow(group, null));
                if (Session.Workspace.CollapsedGroups.Contains(group.Id)) continue;
                foreach (var track in group.Tracks) rows.Add(new TimelineRow(group, track));
            }
            return rows;
        }

        public TimelineEvent Hit(Vector2 position)
        {
            if (position.x < LeftWidth || position.y < TimelineTransform.RulerHeight || !contentRect.Contains(position)) return null;
            var transform = Transform;
            var rows = Rows();
            for (var row = 0; row < rows.Count; row++)
            {
                if (position.y < transform.ToY(row) || position.y >= transform.ToY(row) + TimelineTransform.RowHeight) continue;
                if (rows[row].Track == null) return null;
                for (var i = rows[row].Track.Events.Count - 1; i >= 0; i--)
                    if (transform.EventRect(rows[row].Track.Events[i], row).Contains(position)) return rows[row].Track.Events[i];
            }
            return null;
        }

        public int RowAt(Vector2 position)
        {
            if (!contentRect.Contains(position) || position.y < TimelineTransform.RulerHeight) return -1;
            var row = (int)Math.Floor((position.y - TimelineTransform.RulerHeight + Session.Workspace.ScrollY) / TimelineTransform.RowHeight);
            return row >= 0 && row < Rows().Count ? row : -1;
        }

        public void SelectTrack(string id, bool reveal = false)
        {
            var rows = Rows();
            var row = rows.FindIndex(r => r.Track?.Id == id);
            if (row < 0) return;
            Session.Workspace.SelectedTrackId = id;
            Session.Workspace.SelectedGroupId = rows[row].Group.Id;
            if (reveal)
            {
                var top = row * TimelineTransform.RowHeight;
                var height = Math.Max(TimelineTransform.RowHeight, contentRect.height - TimelineTransform.RulerHeight);
                if (top < Session.Workspace.ScrollY) Session.Workspace.ScrollY = top;
                else if (top + TimelineTransform.RowHeight > Session.Workspace.ScrollY + height)
                    Session.Workspace.ScrollY = (float)(top + TimelineTransform.RowHeight - height);
            }
            NotifyViewChanged();
        }

        public void ToggleGroup(string id)
        {
            if (!Session.Workspace.CollapsedGroups.Remove(id)) Session.Workspace.CollapsedGroups.Add(id);
            NotifyViewChanged();
        }

        private void Draw(MeshGenerationContext context)
        {
            var document = DisplayDocument != null ? DisplayDocument : Session.Document;
            var painter = context.painter2D;
            Fill(painter, contentRect, new Color(0.12f, 0.13f, 0.15f));
            if (document == null)
            {
                context.DrawText("新建或打开文档后开始编辑时间轴", new Vector2(24, 45), 14, new Color(0.8f, 0.8f, 0.8f));
                return;
            }
            if (contentRect.width <= LeftWidth) return;
            var transform = Transform;
            var rows = Rows();
            for (var row = 0; row < rows.Count; row++)
            {
                var y = Math.Max(TimelineTransform.RulerHeight, transform.ToY(row));
                var bottom = Math.Min(contentRect.height, transform.ToY(row) + TimelineTransform.RowHeight);
                if (bottom <= y) continue;
                var selected = rows[row].Track == null ? rows[row].Group.Id == Session.Workspace.SelectedGroupId && Session.Workspace.SelectedTrackId == null
                    : rows[row].Track.Id == Session.Workspace.SelectedTrackId;
                Fill(painter, new Rect(0, y, contentRect.width, bottom - y), selected ? new Color(0.19f, 0.26f, 0.34f)
                    : rows[row].Track == null ? new Color(0.24f, 0.24f, 0.24f) : new Color(0.19f, 0.19f, 0.19f));
            }
            var step = Math.Pow(10, Math.Floor(Math.Log10(80 / transform.Scale)));
            if (step * transform.Scale < 40) step *= 5;
            var start = Math.Max(0, Math.Floor(transform.Scroll / step) * step);
            var end = Math.Min(document.Duration, transform.ToTime(contentRect.width));
            for (var time = start; time <= end; time += step)
            {
                var x = transform.ToX(time);
                if (x < LeftWidth) continue;
                Line(painter, new Vector2(x, 0), new Vector2(x, contentRect.height), new Color(0.24f, 0.25f, 0.28f));
            }
            for (var row = 0; row < rows.Count; row++)
            {
                var y = transform.ToY(row);
                if (y + TimelineTransform.RowHeight < TimelineTransform.RulerHeight || y >= contentRect.height) continue;
                Line(painter, new Vector2(0, y + TimelineTransform.RowHeight), new Vector2(contentRect.width, y + TimelineTransform.RowHeight), new Color(0.2f, 0.21f, 0.23f));
                if (rows[row].Track == null) continue;
                foreach (var item in rows[row].Track.Events)
                {
                    var rect = transform.EventRect(item, row);
                    rect.xMin = Math.Max(rect.xMin, LeftWidth);
                    rect.xMax = Math.Min(rect.xMax, contentRect.width);
                    rect.yMin = Math.Max(rect.yMin, TimelineTransform.RulerHeight);
                    if (rect.width <= 0 || rect.height <= 0) continue;
                    var color = Session.Selection.Contains(item.Id) ? new Color(0.55f, 0.65f, 0.78f) : item.Kind == EventKind.Point ? new Color(0.4f, 0.8f, 0.72f) : new Color(0.35f, 0.55f, 0.85f);
                    Fill(painter, rect, color);
                    if (rect.width > 40)
                    {
                        var text = Session.Registry.Find(item)?.Summary(item) ?? item.Name ?? item.TypeId;
                        var limit = Math.Max(1, (int)((rect.width - 8) / 8));
                        if (text.Length > limit) text = text.Substring(0, limit) + "…";
                        context.DrawText(text, new Vector2(rect.x + 4, rect.y + 6), 11, Color.black);
                    }
                }
            }
            Fill(painter, new Rect(0, 0, LeftWidth, contentRect.height), new Color(0.22f, 0.22f, 0.22f));
            for (var row = 0; row < rows.Count; row++)
            {
                var y = transform.ToY(row);
                if (y >= TimelineTransform.RulerHeight && y + TimelineTransform.RowHeight <= contentRect.height)
                {
                    var groupRow = rows[row].Track == null;
                    var selected = groupRow ? rows[row].Group.Id == Session.Workspace.SelectedGroupId && Session.Workspace.SelectedTrackId == null
                        : rows[row].Track.Id == Session.Workspace.SelectedTrackId;
                    if (selected) Fill(painter, new Rect(0, y, LeftWidth - 9, TimelineTransform.RowHeight - 2), new Color(0.23f, 0.36f, 0.50f));
                    if (!groupRow) Fill(painter, new Rect(20, y + 3, 4, 20), new Color(0.5f, 0.65f, 0.8f));
                    var label = groupRow ? (Session.Workspace.CollapsedGroups.Contains(rows[row].Group.Id) ? "▶  " : "▼  ") + rows[row].Group.Name : rows[row].Track.Name ?? "Track";
                    var limit = Math.Max(4, (int)((LeftWidth - 75) / 7));
                    if (label.Length > limit) label = label.Substring(0, limit) + "…";
                    context.DrawText(label, new Vector2(groupRow ? 4 : 34, y + 5), 12, Color.white);
                    context.DrawText(rows[row].Group.Locked || rows[row].Track?.Locked == true ? "L  ⋮" : "⋮", new Vector2(LeftWidth - 40, y + 5), 12, Color.gray);
                }
                if (y >= TimelineTransform.RulerHeight && y < contentRect.height)
                    Line(painter, new Vector2(0, y + TimelineTransform.RowHeight), new Vector2(LeftWidth, y + TimelineTransform.RowHeight), new Color(0.25f, 0.26f, 0.28f));
            }
            // Paint the fixed ruler last so scrolling rows and event labels cannot cover it.
            Fill(painter, new Rect(0, 0, contentRect.width, TimelineTransform.RulerHeight), new Color(0.23f, 0.24f, 0.26f));
            for (var time = start; time <= end; time += step)
            {
                var x = transform.ToX(time);
                if (x < LeftWidth) continue;
                Line(painter, new Vector2(x, 19), new Vector2(x, TimelineTransform.RulerHeight), Color.gray);
                context.DrawText(time.ToString("0.00"), new Vector2(x + 4, 1), 11, new Color(0.85f, 0.85f, 0.85f));
            }
            Line(painter, new Vector2(LeftWidth - 7, 0), new Vector2(LeftWidth - 7, contentRect.height), new Color(0.4f, 0.4f, 0.4f));
            var endpoint = transform.ToX(document.Duration);
            if (endpoint >= LeftWidth && endpoint <= contentRect.width)
                Line(painter, new Vector2(endpoint, 0), new Vector2(endpoint, contentRect.height), new Color(0.22f, 0.48f, 0.92f));
            var playhead = transform.ToX(Session.Workspace.Playhead);
            if (playhead >= LeftWidth && playhead <= contentRect.width)
                Line(painter, new Vector2(playhead, 0), new Vector2(playhead, contentRect.height), Color.red);
            if (Marquee.HasValue) Fill(painter, Marquee.Value, new Color(0.3f, 0.6f, 1, 0.25f));
        }

        private static void Fill(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color; painter.BeginPath(); painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin)); painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax)); painter.ClosePath(); painter.Fill();
        }
        private static void Line(Painter2D painter, Vector2 start, Vector2 end, Color color)
        {
            painter.strokeColor = color; painter.lineWidth = 1; painter.BeginPath(); painter.MoveTo(start); painter.LineTo(end); painter.Stroke();
        }
    }
}
