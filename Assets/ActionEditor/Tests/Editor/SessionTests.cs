using System;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public class SessionTests
    {
        private sealed class Store : IDocumentStore
        {
            public bool FailLoad;
            public bool FailSave;
            public ActionDocument Load(string path)
            {
                if (FailLoad) throw new InvalidOperationException("load failure");
                var document = DocumentModelTests.Sample();
                document.Title = path;
                return document;
            }
            public void Save(string path, ActionDocument document, byte[] expectedCurrent)
            {
                if (FailSave) throw new InvalidOperationException("save failure");
            }
            public byte[] ReadRevision(string path) => new byte[] { 1 };
        }

        [Test]
        public void FailedOpenAndSaveKeepOriginalTargetAndDirtyState()
        {
            var store = new Store();
            using var session = new DocumentSession(store);
            session.Open("A");
            var original = session.Document;
            store.FailLoad = true;
            Assert.Throws<InvalidOperationException>(() => session.Open("B"));
            Assert.That(session.Document, Is.SameAs(original));
            Assert.That(session.Path, Is.EqualTo("A"));
            session.Document.Title = "edited";
            store.FailSave = true;
            Assert.Throws<InvalidOperationException>(() => session.Save("B"));
            Assert.That(session.Path, Is.EqualTo("A"));
            Assert.That(session.IsDirty, Is.True);
        }

        [Test]
        public void WorkspaceChangesAreNotContentAndDirtySwitchRequiresExplicitDiscard()
        {
            using var session = new DocumentSession(new Store());
            session.Open("A");
            session.Workspace.PixelsPerSecond = 300;
            session.Workspace.CollapsedGroups.Add("group");
            Assert.That(session.IsDirty, Is.False);
            session.Document.Title = "dirty";
            Assert.Throws<InvalidOperationException>(() => session.Open("B"));
            session.DiscardAndClose();
            session.Open("B");
            Assert.That(session.Document.Title, Is.EqualTo("B"));
            Assert.That(session.IsDirty, Is.False);
            session.DiscardAndClose();
            session.DiscardAndClose();
        }

        [Test]
        public void SessionsAndRepeatedDisposalDoNotShareState()
        {
            var a = new DocumentSession(new Store());
            using var b = new DocumentSession(new Store());
            a.Open("A"); b.Open("B");
            a.Document.Title = "change";
            a.Selection.Add("item");
            Assert.That(b.IsDirty, Is.False);
            Assert.That(b.Selection, Is.Empty);
            a.Dispose(); a.Dispose();
            Assert.Throws<ObjectDisposedException>(() => a.New());
        }

        [Test]
        public void AutoSaveFailureRetainsDirtyContentAndLastSuccessTime()
        {
            var store = new Store();
            using var session = new DocumentSession(store);
            session.Open("A");
            var last = session.LastSaveTime;
            session.Document.Title = "unsaved";
            Assert.That(session.TryAutoSave(last + 119, 120), Is.False);
            store.FailSave = true;
            Assert.Throws<InvalidOperationException>(() => session.TryAutoSave(last + 120, 120));
            Assert.That(session.LastSaveTime, Is.EqualTo(last));
            Assert.That(session.IsDirty, Is.True);
            store.FailSave = false;
            Assert.That(session.TryAutoSave(last + 121, 120), Is.True);
            Assert.That(session.LastSaveTime, Is.EqualTo(last + 121));
            Assert.That(session.IsDirty, Is.False);
        }
    }
}
