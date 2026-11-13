using System;
using System.Collections.Generic;
using System.IO;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests.Application
{
    public sealed class DocumentSessionTests
    {
        private sealed class Document : ActionDocument { public int Value; }
        private sealed class Snapshots : IDocumentSnapshot
        {
            public bool FailValidation;
            public string Capture(ActionDocument document) => ((Document)document).Value.ToString();
            public ActionDocument Restore(string snapshot) => new Document { Value = int.Parse(snapshot) };
            public string Content(ActionDocument document) => Capture(document);
            public void ValidateAndInitialize(ActionDocument document)
            { if (FailValidation) throw new InvalidDataException("validation"); document.Init(); }
        }
        private sealed class History : IUndoHistory
        {
            public event Action<string> Restored;
            public int Records, Disposals;
            public bool FailReset, FailRecord;
            public string Before, After;
            public void Reset(string snapshot) { if (FailReset) throw new IOException("reset"); }
            public void Record(string before, string after, string label)
            { if (FailRecord) throw new IOException("history"); Before = before; After = after; Records++; }
            public void Restore(string value) => Restored?.Invoke(value);
            public void Dispose() { Disposals++; }
        }
        private sealed class Repository : IDocumentRepository
        {
            public bool FailRead, FailCommit, FailImport;
            public int Commits, Imports;
            public byte[] Bytes = { 1 };
            public DocumentOpenData Read(string path)
            { if (FailRead) throw new IOException("read"); return new DocumentOpenData(new Document { Value = 1 }, path, Bytes); }
            public string ValidatePath(string path) => string.IsNullOrEmpty(path) ? throw new ArgumentException("path") : path;
            public DocumentCommit Commit(string path, ActionDocument document, byte[] revision)
            {
                if (FailCommit) throw new IOException("commit");
                if (revision != null && (revision.Length != 1 || revision[0] != Bytes[0])) throw new IOException("conflict");
                Commits++; Bytes = new[] { (byte)(Bytes[0] + 1) };
                var text = ((Document)document).Value.ToString();
                return new DocumentCommit(text, text, Bytes);
            }
            public void Import(string path) { Imports++; if (FailImport) throw new IOException("import"); }
        }
        private sealed class Recovery : IRecoveryCodec
        {
            private readonly Dictionary<string, DocumentRecovery> values = new Dictionary<string, DocumentRecovery>();
            public string Encode(DocumentRecovery value) { var key = Guid.NewGuid().ToString(); values[key] = value; return key; }
            public DocumentRecovery Decode(string text) => values[text];
        }
        private sealed class Exporter : IDocumentExporter
        {
            public int Calls;
            public bool Fail;
            public void Export(ActionDocument document, string source, string target, string format)
            { if (Fail) throw new IOException("export"); Calls++; }
        }
        private sealed class Factory : IDocumentFactory
        {
            public ActionDocument Create(Type type) => type == typeof(Document) ? new Document() : throw new ArgumentException("type");
        }
        private sealed class Harness : IDisposable
        {
            public readonly Snapshots Snapshots = new Snapshots();
            public readonly Repository Repository = new Repository();
            public readonly History History = new History();
            public readonly Recovery Recovery = new Recovery();
            public readonly Exporter Exporter = new Exporter();
            public readonly DocumentSession Session;
            public readonly DocumentLifecycle Lifecycle;
            public Document Current => (Document)Session.Document;
            public Harness()
            {
                Session = new DocumentSession(Snapshots, Repository, History, Recovery);
                Lifecycle = new DocumentLifecycle(Session, Repository, Exporter, new Factory());
                Session.Open(Repository.Read("A"));
            }
            public void Dispose() => Session.Dispose();
        }
        [Test] public void OwnerAndStaleTokensCannotCompleteAnotherEdit()
        {
            using var h = new Harness(); var owner = new object();
            var token = h.Session.BeginEdit(owner);
            Assert.That(h.Session.BeginEdit(owner), Is.SameAs(token));
            Assert.That(h.Session.BeginEdit(new object()), Is.Null);
            token.Update(() => h.Current.Value = 2);
            h.Session.EndEdit(); h.Session.CancelEdit();
            Assert.That(token.IsActive, Is.True);
            token.Commit(); token.Cancel(); token.Commit();
            Assert.That(h.History.Records, Is.EqualTo(1));
            var next = h.Session.BeginEdit(new object());
            Assert.That(token.Update(() => Assert.Fail("stale")), Is.False);
            token.Cancel(); Assert.That(next.IsActive, Is.True); next.Cancel();
        }
        [Test] public void NoOpAndNestedFailureDoNotCreateUndoOrDirtyContent()
        {
            using var h = new Harness(); var version = h.Session.Version;
            h.Session.Edit("empty", () => { });
            Assert.That(h.Session.Version, Is.EqualTo(version)); Assert.That(h.History.Records, Is.Zero);
            using var token = h.Session.BeginEdit(this);
            token.Update(() => h.Current.Value = 5);
            Assert.Throws<InvalidOperationException>(() => h.Session.Edit("nested", () => throw new InvalidOperationException("nested")));
            Assert.That(token.IsActive, Is.False); Assert.That(h.Current.Value, Is.EqualTo(1));
            Assert.That(h.Session.IsDirty, Is.False); Assert.That(h.History.Records, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)] public void ValidationOrHistoryFailureRollsBackAndAllowsNextEdit(bool history)
        {
            using var h = new Harness(); h.Snapshots.FailValidation = !history; h.History.FailRecord = history;
            Assert.Catch(() => h.Session.Edit("bad", () => h.Current.Value = 4));
            Assert.That(h.Current.Value, Is.EqualTo(1)); Assert.That(h.Session.IsEditing, Is.False);
            h.Snapshots.FailValidation = false; h.History.FailRecord = false;
            h.Session.Edit("good", () => h.Current.Value = 3); Assert.That(h.History.Records, Is.EqualTo(1));
        }
        [Test] public void FailedReadAndHistoryResetPreserveCurrentGraphAndActiveToken()
        {
            using var h = new Harness(); var graph = h.Current; using var token = h.Session.BeginEdit(this);
            token.Update(() => h.Current.Value = 4);
            h.Repository.FailRead = true; Assert.That(h.Lifecycle.Open("B").Succeeded, Is.False);
            h.Repository.FailRead = false; h.History.FailReset = true;
            Assert.That(h.Lifecycle.Open("B").Succeeded, Is.False);
            Assert.That(h.Session.Document, Is.SameAs(graph)); Assert.That(token.IsActive, Is.True);
            Assert.That(h.Session.Path, Is.EqualTo("A")); Assert.That(h.Current.Value, Is.EqualTo(4));
        }
        [Test] public void CommitFailureAndExternalConflictRetainDirtyGraphSourceAndRevision()
        {
            using var h = new Harness(); h.Session.Edit("edit", () => h.Current.Value = 5);
            var graph = h.Current; h.Repository.FailCommit = true;
            var failed = h.Lifecycle.SaveAs("B"); Assert.That(failed.Succeeded || failed.BytesCommitted, Is.False);
            Assert.That(h.Session.Path, Is.EqualTo("A")); Assert.That(h.Session.Document, Is.SameAs(graph));
            h.Repository.FailCommit = false; h.Repository.Bytes[0] = 9;
            failed = h.Lifecycle.Save(); Assert.That(failed.Succeeded || failed.BytesCommitted, Is.False);
            Assert.That(h.Repository.Imports, Is.Zero); Assert.That(h.Session.IsDirty, Is.True);
        }
        [Test] public void ImportFailureReportsCommittedBytesAndRecoveryKeepsExactRevision()
        {
            using var h = new Harness(); h.Session.Edit("edit", () => h.Current.Value = 7);
            h.Repository.FailImport = true;
            var result = h.Lifecycle.SaveAs("B");
            Assert.That(result.Succeeded, Is.False); Assert.That(result.BytesCommitted, Is.True);
            Assert.That(h.Session.Path, Is.EqualTo("B")); Assert.That(h.Session.IsDirty, Is.False);
            var saved = h.Session.CaptureRecovery(); h.Session.Open(null); h.Session.RestoreRecovery(saved);
            h.Session.Edit("edit", () => h.Current.Value = 8); h.Repository.Bytes[0] = 20;
            Assert.That(h.Lifecycle.Save().BytesCommitted, Is.False);
            Assert.That(h.Repository.Commits, Is.EqualTo(1)); Assert.That(h.Repository.Imports, Is.EqualTo(1));
        }
        [Test] public void SaveAndExportDoNotEmitAuthorChangesOrInvalidateGraphAndToken()
        {
            using var h = new Harness(); var content = 0; h.Session.ContentChanged += () => content++;
            var graph = h.Current; Assert.That(h.Lifecycle.Save().Succeeded, Is.True);
            Assert.That(h.Lifecycle.Export("E", "format").Succeeded, Is.True);
            Assert.That(content, Is.Zero); Assert.That(h.Session.Document, Is.SameAs(graph));
            using var token = h.Session.BeginEdit(this);
            Assert.That(h.Lifecycle.Save().Succeeded, Is.False); Assert.That(h.Lifecycle.Export("F", "format").Succeeded, Is.False);
            Assert.That(token.IsActive, Is.True); Assert.That(h.Exporter.Calls, Is.EqualTo(1));
        }
        [Test] public void CreateCancelFailureAndImportFailureNeverSwitchCurrentSource()
        {
            using var h = new Harness(); var graph = h.Current;
            Assert.That(h.Lifecycle.Create(typeof(Document), null).Cancelled, Is.True);
            Assert.That(h.Lifecycle.SaveAs(null).Cancelled, Is.True);
            Assert.That(h.Lifecycle.Export(null, "format").Cancelled, Is.True);
            Assert.That(h.Lifecycle.Create(typeof(string), "B").Succeeded, Is.False);
            h.Repository.FailImport = true; var result = h.Lifecycle.Create(typeof(Document), "B");
            Assert.That(result.BytesCommitted, Is.True); Assert.That(result.Succeeded, Is.False);
            Assert.That(h.Session.Document, Is.SameAs(graph)); Assert.That(h.Session.Path, Is.EqualTo("A"));
        }
        [Test] public void UndoCancelsTokensAndDisposalUnsubscribesExactlyOnce()
        {
            var h = new Harness(); var token = h.Session.BeginEdit(this); token.Update(() => h.Current.Value = 4);
            h.History.Restore("2"); Assert.That(token.IsActive, Is.False); Assert.That(h.Current.Value, Is.EqualTo(2));
            h.Session.Dispose(); h.Session.Dispose(); h.History.Restore("3");
            Assert.That(h.History.Disposals, Is.EqualTo(1)); Assert.That(h.Session.Document, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => h.Session.BeginEdit(this));
        }
        [Test] public void BrokenViewNotificationCannotMisreportCommittedSaveOrBlockOtherViews()
        {
            using var h = new Harness(); var received = 0;
            h.Session.Changed += () => throw new InvalidOperationException("view"); h.Session.Changed += () => received++;
            Assert.That(h.Lifecycle.Save().Succeeded, Is.True);
            Assert.That(received, Is.EqualTo(1)); Assert.That(h.Session.NotificationWarning, Does.Contain("view"));
        }
    }
}
