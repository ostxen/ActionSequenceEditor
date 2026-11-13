using NBC.ActionEditor.Presentation.Timeline;
using System.Linq;
using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using NBC.ActionEditor.Samples.LegacyExperience;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public sealed class DocumentExportTests
    {
        private sealed class Files : ICommitFiles
        {
            public readonly Dictionary<string, byte[]> Data = new Dictionary<string, byte[]>();
            public bool FailWrite;
            public bool Exists(string path) => Data.ContainsKey(path);
            public void Write(string path, byte[] bytes) { if (FailWrite) throw new IOException("Injected write failure"); Data.Add(path, bytes); }
            public byte[] Read(string path) => Data[path];
            public void Replace(string staged, string target) => throw new AssertionException("Export must never replace.");
            public void Move(string staged, string target) { Data.Add(target, Data[staged]); Data.Remove(staged); }
            public void Delete(string path) => Data.Remove(path);
        }
        private sealed class WriteOnly : IActionDocumentFormat
        {
            public string Id => "tests.export-only";
            public string DisplayName => "Test export";
            public string Extension => ".test";
            public bool CanRead => false; public bool CanWrite => true; public bool CanExport => true;
            public byte[] Serialize(ActionDocument asset, DocumentFormatOptions options, DocumentFormatContext context) => new byte[] { 1, 2, 3 };
            public ActionDocument Deserialize(byte[] bytes, DocumentFormatContext context) => throw new AssertionException("Write-only export must not deserialize.");
        }
        [Test] public void WriteOnlyFormatDoesNotNeedReadbackOrEnterProductionRegistry()
        {
            using var session = new JsonDocumentSession(); var memory = new TextAsset(JsonDocumentCodec.Encode(new LegacyExperienceAsset()));
            try
            {
                session.Open(memory); var files = new Files();
                var registry = new DocumentFormatRegistry(new IActionDocumentFormat[] { new WriteOnly() });
                Assert.That(registry.Editable, Is.Empty);
                Assert.That(new DocumentExportService(files, registry).Export(session, "tests.export-only", "Assets/ActionEditor/RF20-Fake.test"), Is.True);
                Assert.That(files.Data["Assets/ActionEditor/RF20-Fake.test"], Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(DocumentFormats.Registry.Exportable.Count, Is.EqualTo(1));
                Assert.That(DocumentFormats.Registry.Exportable[0].Id, Is.EqualTo("actioneditor.json"));
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void CanonicalCaseAndDotSourceAliasesAreRejectedBeforeWriting()
        {
            var files = new Files(); using var session = new JsonDocumentSession(new JsonDocumentStore(files, _ => throw new IOException("Injected import failure")));
            var memory = new TextAsset(JsonDocumentCodec.Encode(new LegacyExperienceAsset()));
            try
            {
                session.Open(memory);
                Assert.Throws<IOException>(() => session.Save("Assets/ActionEditor/RF20-Fake.json"));
                var before = session.CaptureRecovery();
                foreach (var alias in new[] { "Assets/actioneditor/./RF20-fake.JSON", "Assets/ActionEditor/./RF20-Fake.json" })
                    Assert.Throws<IOException>(() => new DocumentExportService(files).Export(session, "actioneditor.json", alias));
                Assert.That(files.Data.Count, Is.EqualTo(1)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [TestCase(false)] [TestCase(true)]
        public void CleanAndDirtyExportPreserveGraphBaselineUndoAndWorkspace(bool dirty)
        {
            using var session = new JsonDocumentSession();
            var memory = new TextAsset(JsonDocumentCodec.Encode(new LegacyExperienceAsset()));
            try
            {
                session.Open(memory);
                var workspace = new TimelineWorkspace(); workspace.Bind(session.Document); workspace.ViewTimeMax = 12; workspace.SetCollapsed(session.Document.RootItems.OfType<TimelineGroup>().ElementAt(0), true);
                if (dirty) session.Edit("business", () => ((LegacyExperienceAsset)session.Document).Version = 9);
                var source = session.Document; var before = session.CaptureRecovery();
                var files = new Files(); var service = new DocumentExportService(files);
                const string path = "Assets/ActionEditor/RF20-Fake.json";
                Assert.That(service.Export(session, "actioneditor.json", null), Is.False);
                Assert.That(service.Export(session, "actioneditor.json", path), Is.True);
                Assert.That(session.Document, Is.SameAs(source)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                Assert.That(session.IsDirty, Is.EqualTo(dirty));
                var loaded = JsonDocumentCodec.Decode(System.Text.Encoding.UTF8.GetString(files.Data[path]));
                Assert.That(workspace.ViewTimeMax, Is.EqualTo(12)); Assert.That(workspace.IsCollapsed(session.Document.RootItems.OfType<TimelineGroup>().ElementAt(0)), Is.True);
                Assert.That(System.Text.Encoding.UTF8.GetString(files.Data[path]), Does.Not.Contain("viewTime").And.Not.Contain("isCollapsed"));
                Assert.That(((LegacyExperienceAsset)loaded).Version, Is.EqualTo(dirty ? 9 : 1));
                Assert.Throws<IOException>(() => service.Export(session, "actioneditor.json", path));
                Assert.That(files.Data.Count, Is.EqualTo(1)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                if (dirty)
                {
                    Undo.PerformUndo(); Assert.That(((LegacyExperienceAsset)session.Document).Version, Is.EqualTo(1));
                    Undo.PerformRedo(); Assert.That(((LegacyExperienceAsset)session.Document).Version, Is.EqualTo(9));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void ActiveGestureAndWriteFailuresDoNotChangeSessionOrLeaveFiles()
        {
            using var session = new JsonDocumentSession(); var memory = new TextAsset(JsonDocumentCodec.Encode(new LegacyExperienceAsset()));
            try
            {
                session.Open(memory); var files = new Files { FailWrite = true }; var service = new DocumentExportService(files);
                const string path = "Assets/ActionEditor/RF20-Fake.json";
                var before = session.CaptureRecovery(); var source = session.Document;
                Assert.Throws<IOException>(() => service.Export(session, "actioneditor.json", path));
                Assert.That(files.Data, Is.Empty); Assert.That(session.Document, Is.SameAs(source)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                using var token = session.BeginEdit(this); ((LegacyExperienceAsset)session.Document).Version = 7;
                Assert.Throws<InvalidOperationException>(() => service.Export(session, "actioneditor.json", path));
                Assert.That(token.IsActive, Is.True); Assert.That(((LegacyExperienceAsset)session.Document).Version, Is.EqualTo(7)); Assert.That(files.Data, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
    }
}
