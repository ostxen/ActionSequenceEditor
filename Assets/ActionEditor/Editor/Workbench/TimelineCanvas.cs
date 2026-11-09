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
        public const float HeaderWidth = 150;
        public const float RulerHeight = 28;
        public const float RowHeight = 32;
        public readonly double Scale;
        public readonly double Scroll;
        public readonly float ScrollY;
        public TimelineTransform(double scale, double scroll, float scrollY)
        {
            if (!TimelineTime.IsFinite(scale) || scale <= 0 || !TimelineTime.IsFinite(scroll) || !TimelineTime.IsFinite(scrollY)) throw new ArgumentOutOfRangeException();
            Scale = scale; Scroll = scroll; ScrollY = scrollY;
        }
        public float ToX(double time) => HeaderWidth + (float)((time - Scroll) * Scale);
        public double ToTime(float x) => Scroll + (x - HeaderWidth) / Scale;
        public float ToY(int row) => RulerHeight + row * RowHeight - ScrollY;
        public Rect EventRect(TimelineEvent item, int row)
        {
            var width = item.Kind == EventKind.Point ? 10 : Math.Max(6, (float)(item.Duration * Scale));
            return new Rect(ToX(item.Start) - (item.Kind == EventKind.Point ? 5 : 0), ToY(row) + 5, width, RowHeight - 10);
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
        public TimelineTransform Transform => new(Session.Workspace.PixelsPerSecond, Session.Workspace.ScrollTime, Session.Workspace.ScrollY);
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
                if (Session.Workspace.CollapsedGroups.Contains(group.Id)) continue;
                foreach (var track in group.Tracks) rows.Add(new TimelineRow(group, track));
            }
            return rows;
        }

        public TimelineEvent Hit(Vector2 position)
        {
            if (position.x < TimelineTransform.HeaderWidth || position.y < TimelineTransform.RulerHeight || !contentRect.Contains(position)) return null;
            var transform = Transform;
            var rows = Rows();
            for (var row = 0; row < rows.Count; row++)
            {
                if (position.y < transform.ToY(row) || position.y >= transform.ToY(row) + TimelineTransform.RowHeight) continue;
                for (var i = rows[row].Track.Events.Count - 1; i >= 0; i--)
                    if (transform.EventRect(rows[row].Track.Events[i], row).Contains(position)) return rows[row].Track.Events[i];
            }
            return null;
        }

        private void Draw(MeshGenerationContext context)
        {
            var document = DisplayDocument != null ? DisplayDocument : Session.Document;
            var painter = context.painter2D;
            Fill(painter, contentRect, new Color(0.12f, 0.13f, 0.15f));
            if (document == null || contentRect.width <= TimelineTransform.HeaderWidth) return;
            var transform = Transform;
            var rows = Rows();
            var step = Math.Pow(10, Math.Floor(Math.Log10(80 / transform.Scale)));
            if (step * transform.Scale < 40) step *= 5;
            var start = Math.Max(0, Math.Floor(transform.Scroll / step) * step);
            var end = Math.Min(document.Duration, transform.ToTime(contentRect.width));
            for (var time = start; time <= end; time += step)
            {
                var x = transform.ToX(time);
                if (x < TimelineTransform.HeaderWidth) continue;
                Line(painter, new Vector2(x, 0), new Vector2(x, contentRect.height), new Color(0.24f, 0.25f, 0.28f));
                context.DrawText(time.ToString("0.###"), new Vector2(x + 3, 4), 11, Color.gray);
            }
            for (var row = 0; row < rows.Count; row++)
            {
                var y = transform.ToY(row);
                if (y + TimelineTransform.RowHeight < TimelineTransform.RulerHeight || y >= contentRect.height) continue;
                Line(painter, new Vector2(0, y + TimelineTransform.RowHeight), new Vector2(contentRect.width, y + TimelineTransform.RowHeight), new Color(0.2f, 0.21f, 0.23f));
                foreach (var item in rows[row].Track.Events)
                {
                    var rect = transform.EventRect(item, row);
                    rect.xMin = Math.Max(rect.xMin, TimelineTransform.HeaderWidth);
                    rect.xMax = Math.Min(rect.xMax, contentRect.width);
                    rect.yMin = Math.Max(rect.yMin, TimelineTransform.RulerHeight);
                    if (rect.width <= 0 || rect.height <= 0) continue;
                    var color = Session.Selection.Contains(item.Id) ? new Color(0.95f, 0.67f, 0.25f) : item.Kind == EventKind.Point ? new Color(0.4f, 0.8f, 0.72f) : new Color(0.35f, 0.55f, 0.85f);
                    Fill(painter, rect, color);
                    if (rect.width > 40) context.DrawText(Session.Registry.Find(item)?.Summary(item) ?? item.Name ?? item.TypeId,
                        new Vector2(rect.x + 4, rect.y + 3), 11, Color.black);
                }
            }
            Fill(painter, new Rect(0, 0, TimelineTransform.HeaderWidth, contentRect.height), new Color(0.16f, 0.17f, 0.19f));
            context.DrawText("TRACKS / seconds", new Vector2(8, 5), 12, Color.gray);
            for (var row = 0; row < rows.Count; row++)
            {
                var y = transform.ToY(row);
                if (y >= TimelineTransform.RulerHeight && y < contentRect.height)
                    context.DrawText(rows[row].Track.Name + (rows[row].Track.Locked ? " [locked]" : ""), new Vector2(8, y + 8), 12, Color.white);
            }
            var playhead = transform.ToX(Session.Workspace.Playhead);
            if (playhead >= TimelineTransform.HeaderWidth && playhead <= contentRect.width)
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
