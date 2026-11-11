using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Model;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Storage
{
    public sealed class DocumentTransaction : IDisposable
    {
        private readonly DocumentSession session;
        private readonly ActionDocument original;
        private readonly string initialJson;
        private readonly string label;
        private bool finished;
        public ActionDocument Candidate { get; private set; }
        public List<string> Selection { get; }

        public DocumentTransaction(DocumentSession session, string label)
        {
            if (session.Document == null) throw new InvalidOperationException("No open document.");
            if (session.TransactionActive) throw new InvalidOperationException("Another transaction is active.");
            this.session = session;
            this.label = label;
            original = session.Document;
            initialJson = EditorJsonUtility.ToJson(original);
            Candidate = UnityEngine.Object.Instantiate(original);
            Candidate.hideFlags = HideFlags.HideAndDontSave;
            Selection = new List<string>(session.Selection);
            session.TransactionActive = true;
        }

        public void Commit()
        {
            if (finished) throw new ObjectDisposedException(nameof(DocumentTransaction));
            if (original == null || session.Document != original || EditorJsonUtility.ToJson(original) != initialJson)
                throw new InvalidOperationException("Document changed during candidate editing.");
            session.Registry.RequireValid(Candidate);
            ValidateLocks(original, Candidate);
            var known = new HashSet<string>(Candidate.Groups.SelectMany(g => g.Tracks).SelectMany(t => t.Events).Select(e => e.Id));
            Selection.RemoveAll(id => !known.Contains(id));
            if (EditorJsonUtility.ToJson(Candidate) != initialJson || !Selection.SequenceEqual(session.Selection))
            {
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(label);
                Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { original, session.SelectionObject }, label);
                EditorUtility.CopySerialized(Candidate, original);
                original.hideFlags = HideFlags.HideAndDontSave;
                session.Selection.Clear();
                session.Selection.AddRange(Selection);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
            }
            Dispose();
            session.NotifyChanged();
        }

        private static void ValidateLocks(ActionDocument before, ActionDocument after)
        {
            foreach (var group in before.Groups)
            {
                var nextGroup = after.Groups.Find(g => g.Id == group.Id);
                if (group.Locked && !OnlyUnlock(group, nextGroup)) throw new InvalidOperationException("Group is locked.");
                foreach (var track in group.Tracks)
                {
                    var nextTrack = nextGroup?.Tracks.Find(t => t.Id == track.Id);
                    if (track.Locked && !OnlyUnlock(track, nextTrack)) throw new InvalidOperationException("Track is locked.");
                    foreach (var item in track.Events.Where(e => e.Locked))
                    {
                        var next = nextTrack?.Events.Find(e => e.Id == item.Id);
                        if (JsonUtility.ToJson(item) != JsonUtility.ToJson(next)) throw new InvalidOperationException("Event is locked.");
                    }
                }
            }
        }

        private static bool OnlyUnlock(DocumentGroup before, DocumentGroup after)
        {
            if (after == null) return false;
            var locked = after.Locked;
            try { after.Locked = before.Locked; return JsonUtility.ToJson(before) == JsonUtility.ToJson(after); }
            finally { after.Locked = locked; }
        }

        private static bool OnlyUnlock(DocumentTrack before, DocumentTrack after)
        {
            if (after == null) return false;
            var locked = after.Locked;
            try { after.Locked = before.Locked; return JsonUtility.ToJson(before) == JsonUtility.ToJson(after); }
            finally { after.Locked = locked; }
        }

        public void Dispose()
        {
            if (finished) return;
            if (Candidate != null) UnityEngine.Object.DestroyImmediate(Candidate);
            Candidate = null;
            session.TransactionActive = false;
            finished = true;
        }
    }

    public static class DocumentCommands
    {
        public static DocumentTrack Track(ActionDocument document, string id)
        {
            return document.Groups.SelectMany(g => g.Tracks).FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException("Track does not belong to this document.");
        }

        public static TimelineEvent Event(ActionDocument document, string id)
        {
            return document.Groups.SelectMany(g => g.Tracks).SelectMany(t => t.Events).FirstOrDefault(e => e.Id == id)
                ?? throw new InvalidOperationException("Event does not belong to this document.");
        }

        public static void Add(DocumentTransaction edit, string trackId, TimelineEvent item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var copy = (TimelineEvent)JsonUtility.FromJson(JsonUtility.ToJson(item), item.GetType());
            copy.Id = StableId.New();
            Track(edit.Candidate, trackId).Events.Add(copy);
            edit.Selection.Clear(); edit.Selection.Add(copy.Id);
        }

        public static void Delete(DocumentTransaction edit, IEnumerable<string> ids)
        {
            var targets = ids.Distinct().ToArray();
            foreach (var id in targets) Event(edit.Candidate, id);
            foreach (var track in edit.Candidate.Groups.SelectMany(g => g.Tracks)) track.Events.RemoveAll(e => targets.Contains(e.Id));
            edit.Selection.RemoveAll(targets.Contains);
        }

        public static void Move(DocumentTransaction edit, IEnumerable<string> ids, double delta)
        {
            foreach (var id in ids.Distinct()) Event(edit.Candidate, id).Start += delta;
        }

        public static void Resize(DocumentTransaction edit, string id, double start, double duration)
        {
            var item = Event(edit.Candidate, id);
            item.Start = start; item.Duration = duration;
        }

        public static void Duplicate(DocumentTransaction edit, IEnumerable<string> ids)
        {
            var copies = new List<string>();
            foreach (var id in ids.Distinct().ToArray())
            {
                var track = edit.Candidate.Groups.SelectMany(g => g.Tracks).FirstOrDefault(t => t.Events.Exists(e => e.Id == id))
                    ?? throw new InvalidOperationException("Foreign event.");
                var item = Event(edit.Candidate, id);
                Add(edit, track.Id, item);
                copies.Add(edit.Selection[0]);
            }
            edit.Selection.Clear(); edit.Selection.AddRange(copies);
        }

        public static void Reorder(DocumentTransaction edit, string id, int order) => Event(edit.Candidate, id).Order = order;

        public static void Change(DocumentTransaction edit, IEnumerable<string> ids, Action<TimelineEvent> change)
        {
            foreach (var id in ids.Distinct()) change(Event(edit.Candidate, id));
        }
    }
}
