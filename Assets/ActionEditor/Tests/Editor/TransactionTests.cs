using System;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEditor;

namespace NBC.ActionEditor.Tests
{
    public class TransactionTests
    {
        [Test]
        public void RowUnlockAllowsOnlyLockFlagChangeAndRetainsUndo()
        {
            using var session = new DocumentSession(); session.New();
            var group = new DocumentGroup { Locked = true };
            var track = new DocumentTrack { Locked = true }; group.Tracks.Add(track); session.Document.Groups.Add(group);
            using (var edit = new DocumentTransaction(session, "bad unlock"))
            {
                edit.Candidate.Groups[0].Locked = false; edit.Candidate.Groups[0].Name = "bad";
                Assert.Throws<InvalidOperationException>(() => edit.Commit());
            }
            using (var edit = new DocumentTransaction(session, "unlock group")) { edit.Candidate.Groups[0].Locked = false; edit.Commit(); }
            using (var edit = new DocumentTransaction(session, "unlock track")) { edit.Candidate.Groups[0].Tracks[0].Locked = false; edit.Commit(); }
            Assert.That(session.Document.Groups[0].Tracks[0].Locked, Is.False);
            Undo.PerformUndo(); Assert.That(session.Document.Groups[0].Tracks[0].Locked, Is.True);
            Undo.PerformUndo(); Assert.That(session.Document.Groups[0].Locked, Is.True);
        }

        [Test]
        public void CancelAndInvalidCommitNeverMutateDocument()
        {
            using var session = new DocumentSession();
            session.New();
            using (var edit = new DocumentTransaction(session, "cancel")) edit.Candidate.Title = "candidate";
            Assert.That(session.Document.Title, Is.EqualTo("Untitled"));
            using (var edit = new DocumentTransaction(session, "invalid"))
            {
                edit.Candidate.Duration = -1;
                Assert.Throws<InvalidOperationException>(() => edit.Commit());
            }
            Assert.That(session.Document.Duration, Is.EqualTo(5));
        }

        [Test]
        public void CommandsUndoRedoRestoreDataIdsSelectionAndIndependentCollections()
        {
            using var session = new DocumentSession(); session.New();
            var group = new DocumentGroup(); var track = new DocumentTrack();
            group.Tracks.Add(track); session.Document.Groups.Add(group);
            using (var edit = new DocumentTransaction(session, "add"))
            {
                DocumentCommands.Add(edit, track.Id, new SignalEvent { Start = 0.25, Message = "sample" });
                edit.Commit();
            }
            var id = session.Selection[0];
            Undo.PerformUndo();
            Assert.That(DocumentCommands.Track(session.Document, track.Id).Events, Is.Empty);
            Assert.That(session.Selection, Is.Empty);
            Undo.PerformRedo();
            Assert.That(session.Selection[0], Is.EqualTo(id));
            using (var edit = new DocumentTransaction(session, "copy and edit"))
            {
                DocumentCommands.Duplicate(edit, new[] { id });
                var copy = (SignalEvent)DocumentCommands.Event(edit.Candidate, edit.Selection[0]);
                Assert.That(copy.Id, Is.Not.EqualTo(id));
                Assert.That(copy.Start, Is.EqualTo(0.25));
                Assert.That(copy.Message, Is.EqualTo("sample"));
                copy.Tags.Add("independent");
                DocumentCommands.Move(edit, edit.Selection, 0.5);
                DocumentCommands.Reorder(edit, copy.Id, 3);
                edit.Commit();
            }
            Assert.That(((SignalEvent)DocumentCommands.Event(session.Document, id)).Tags, Is.Empty);
            Undo.PerformUndo(); Undo.PerformRedo();
            Assert.That(DocumentCommands.Track(session.Document, track.Id).Events.Count, Is.EqualTo(2));
        }

        [Test]
        public void LocksForeignIdsAndOverlappingTransactionsCannotLeakPartialEdits()
        {
            using var session = new DocumentSession(); session.New();
            var group = new DocumentGroup(); var track = new DocumentTrack(); var item = new IntervalEvent { Duration = 1, Locked = true };
            track.Events.Add(item); group.Tracks.Add(track); session.Document.Groups.Add(group);
            using (var edit = new DocumentTransaction(session, "locked"))
            {
                Assert.Throws<InvalidOperationException>(() => new DocumentTransaction(session, "overlap"));
                DocumentCommands.Move(edit, new[] { item.Id }, 1);
                Assert.Throws<InvalidOperationException>(() => edit.Commit());
            }
            Assert.That(DocumentCommands.Event(session.Document, item.Id).Start, Is.Zero);
            using (var edit = new DocumentTransaction(session, "foreign"))
                Assert.Throws<InvalidOperationException>(() => DocumentCommands.Delete(edit, new[] { item.Id, StableId.New() }));
            Assert.That(DocumentCommands.Track(session.Document, track.Id).Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResizeBatchChangeAndDeleteUndoRestoreWholeTransaction()
        {
            using var session = new DocumentSession(); session.New();
            var group = new DocumentGroup(); var track = new DocumentTrack();
            var first = new IntervalEvent { Start = 0.25, Duration = 0.5 };
            var second = new IntervalEvent { Start = 2, Duration = 0.5 };
            track.Events.Add(first); track.Events.Add(second); group.Tracks.Add(track); session.Document.Groups.Add(group);
            using (var edit = new DocumentTransaction(session, "batch"))
            {
                DocumentCommands.Resize(edit, first.Id, 0.5, 0.75);
                DocumentCommands.Change(edit, new[] { first.Id, second.Id }, item => item.Name = "changed");
                edit.Selection.Add(first.Id);
                edit.Commit();
            }
            Undo.PerformUndo();
            Assert.That(DocumentCommands.Event(session.Document, first.Id).Start, Is.EqualTo(0.25));
            Assert.That(DocumentCommands.Event(session.Document, second.Id).Name, Is.EqualTo("Event"));
            Undo.PerformRedo();
            using (var edit = new DocumentTransaction(session, "delete"))
            {
                Assert.Throws<InvalidOperationException>(() => session.Save("ignored"));
                Assert.Throws<InvalidOperationException>(() => session.DiscardAndClose());
                DocumentCommands.Delete(edit, new[] { first.Id, second.Id });
                edit.Commit();
            }
            Assert.That(DocumentCommands.Track(session.Document, track.Id).Events, Is.Empty);
            Assert.That(session.Selection, Is.Empty);
            Undo.PerformUndo();
            Assert.That(DocumentCommands.Track(session.Document, track.Id).Events.Count, Is.EqualTo(2));
            Assert.That(session.Selection, Does.Contain(first.Id));
        }
    }
}
