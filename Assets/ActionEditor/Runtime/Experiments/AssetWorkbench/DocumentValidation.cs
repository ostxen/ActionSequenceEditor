using System;
using System.Collections.Generic;

namespace NBC.ActionEditor.Model
{
    public readonly struct DocumentIssue
    {
        public readonly string TargetId;
        public readonly string Message;
        public DocumentIssue(string targetId, string message) { TargetId = targetId; Message = message; }
        public override string ToString() => Message;
    }

    public static class DocumentValidation
    {
        public static List<DocumentIssue> Inspect(ActionDocument document)
        {
            var issues = new List<DocumentIssue>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (document == null) { issues.Add(new DocumentIssue(null, "No document.")); return issues; }
            void Issue(string id, string message) => issues.Add(new DocumentIssue(id, message));
            void Id(string id)
            {
                if (!StableId.IsValid(id)) Issue(id, "Invalid persistent ID.");
                else if (!ids.Add(id)) Issue(id, "Duplicate persistent ID: " + id);
            }
            Id(document.Id);
            if (document.FormatVersion != ActionDocument.CurrentVersion) Issue(document.Id, "Unsupported document version.");
            if (!TimelineTime.IsFinite(document.Duration) || document.Duration <= 0) Issue(document.Id, "Duration must be finite and positive.");
            if (document.Groups == null) { Issue(document.Id, "Missing groups."); return issues; }
            foreach (var group in document.Groups)
            {
                if (group == null) { Issue(document.Id, "Missing group."); continue; }
                Id(group.Id);
                if (group.Tracks == null) { Issue(group.Id, "Missing tracks."); continue; }
                foreach (var track in group.Tracks)
                {
                    if (track == null) { Issue(group.Id, "Missing track."); continue; }
                    Id(track.Id);
                    if (!Enum.IsDefined(typeof(OverlapPolicy), track.Overlap)) Issue(track.Id, "Invalid overlap policy.");
                    if (track.Events == null) { Issue(track.Id, "Missing events."); continue; }
                    foreach (var item in track.Events)
                    {
                        if (item == null) { Issue(track.Id, "Missing or unknown event type; document cannot be saved."); continue; }
                        Id(item.Id);
                        if (string.IsNullOrWhiteSpace(item.TypeId)) Issue(item.Id, "Missing event type ID.");
                        if (!Enum.IsDefined(typeof(EventRole), item.Role) || !Enum.IsDefined(typeof(EventKind), item.Kind)) Issue(item.Id, "Invalid event classification.");
                        if (!ValidTime(item, document.Duration)) Issue(item.Id, "Event time is invalid or outside document.");
                    }
                    if (track.Overlap == OverlapPolicy.Reject)
                        for (var i = 0; i < track.Events.Count; i++)
                            for (var j = i + 1; j < track.Events.Count; j++)
                            {
                                var a = track.Events[i];
                                var b = track.Events[j];
                                if (a != null && b != null && ValidTime(a, document.Duration) && ValidTime(b, document.Duration)
                                    && TimelineTime.Overlaps(a.Start, a.Duration, b.Start, b.Duration))
                                    Issue(b.Id, "Intervals overlap on a track that rejects overlap.");
                            }
                }
            }
            return issues;
        }

        public static void RequireValid(ActionDocument document)
        {
            var issues = Inspect(document);
            if (issues.Count != 0) throw new InvalidOperationException(issues[0].Message);
        }

        private static bool ValidTime(TimelineEvent item, double documentDuration)
        {
            return TimelineTime.IsFinite(item.Start) && item.Start >= 0 && TimelineTime.IsFinite(item.Duration)
                && item.Duration >= 0 && TimelineTime.IsFinite(item.Start + item.Duration)
                && item.Start + item.Duration <= documentDuration
                && (item.Kind == EventKind.Point ? item.Duration == 0 : item.Duration > 0);
        }
    }
}
