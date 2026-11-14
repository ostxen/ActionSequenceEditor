using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Presentation.Timeline;
using System.Linq;
using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using NBC.ActionEditor.Samples.FeatureSample;
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
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create(); var memory = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                session.Open(new JsonDocumentRepository().ReadSource(memory)); var files = new Files();
                var registry = new DocumentFormatRegistry(new IActionDocumentFormat[] { new WriteOnly() });
                Assert.That(registry.Editable, Is.Empty);
                Assert.That(new DocumentLifecycle(session, new JsonDocumentRepository(files), new DocumentExportService(files, registry), new DocumentFactory()).Export("Assets/ActionEditor/ExportFixture.test", "tests.export-only").Succeeded, Is.True);
                Assert.That(files.Data["Assets/ActionEditor/ExportFixture.test"], Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(DocumentFormats.Registry.Exportable.Count, Is.EqualTo(1));
                Assert.That(DocumentFormats.Registry.Exportable[0].Id, Is.EqualTo("actioneditor.json"));
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void CanonicalCaseAndDotSourceAliasesAreRejectedBeforeWriting()
        {
            var files = new Files(); using var session = NBC.ActionEditor.Composition.DocumentComposition.Create(new JsonDocumentRepository(files, _ => throw new IOException("Injected import failure")));
            var memory = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                session.Open(new JsonDocumentRepository().ReadSource(memory));
                Assert.Throws<IOException>(() => session.Save("Assets/ActionEditor/ExportFixture.json"));
                var before = session.CaptureRecovery();
                foreach (var alias in new[] { "Assets/actioneditor/./exportfixture.JSON", "Assets/ActionEditor/./ExportFixture.json" })
                    Assert.Throws<IOException>(() => new DocumentExportService(files).Export(session.Document, session.Path, alias, "actioneditor.json"));
                Assert.That(files.Data.Count, Is.EqualTo(1)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [TestCase(false)] [TestCase(true)]
        public void CleanAndDirtyExportPreserveGraphBaselineUndoAndWorkspace(bool dirty)
        {
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create();
            var memory = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                session.Open(new JsonDocumentRepository().ReadSource(memory));
                var workspace = new TimelineWorkspace(); workspace.Bind(session.Document); workspace.ViewTimeMax = 12; workspace.SetCollapsed(session.Document.RootItems.OfType<TimelineGroup>().ElementAt(0), true);
                if (dirty) session.Edit("business", () => ((FeatureSampleDocument)session.Document).Version = 9);
                var source = session.Document; var before = session.CaptureRecovery();
                var files = new Files(); var service = new DocumentLifecycle(session, new JsonDocumentRepository(files), new DocumentExportService(files), new DocumentFactory());
                const string path = "Assets/ActionEditor/ExportFixture.json";
                Assert.That(service.Export(null, "actioneditor.json").Cancelled, Is.True);
                Assert.That(service.Export(path, "actioneditor.json").Succeeded, Is.True);
                Assert.That(session.Document, Is.SameAs(source)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                Assert.That(session.IsDirty, Is.EqualTo(dirty));
                var loaded = JsonDocumentCodec.Decode(System.Text.Encoding.UTF8.GetString(files.Data[path]));
                Assert.That(workspace.ViewTimeMax, Is.EqualTo(12)); Assert.That(workspace.IsCollapsed(session.Document.RootItems.OfType<TimelineGroup>().ElementAt(0)), Is.True);
                Assert.That(System.Text.Encoding.UTF8.GetString(files.Data[path]), Does.Not.Contain("viewTime").And.Not.Contain("isCollapsed"));
                Assert.That(((FeatureSampleDocument)loaded).Version, Is.EqualTo(dirty ? 9 : 1));
                Assert.That(service.Export(path, "actioneditor.json").Succeeded, Is.False);
                Assert.That(files.Data.Count, Is.EqualTo(1)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                if (dirty)
                {
                    Undo.PerformUndo(); Assert.That(((FeatureSampleDocument)session.Document).Version, Is.EqualTo(1));
                    Undo.PerformRedo(); Assert.That(((FeatureSampleDocument)session.Document).Version, Is.EqualTo(9));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void ActiveGestureAndWriteFailuresDoNotChangeSessionOrLeaveFiles()
        {
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create(); var memory = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                session.Open(new JsonDocumentRepository().ReadSource(memory)); var files = new Files { FailWrite = true }; var service = new DocumentLifecycle(session, new JsonDocumentRepository(files), new DocumentExportService(files), new DocumentFactory());
                const string path = "Assets/ActionEditor/ExportFixture.json";
                var before = session.CaptureRecovery(); var source = session.Document;
                var failed = service.Export(path, "actioneditor.json");
                Assert.That(failed.Succeeded || failed.BytesCommitted, Is.False);
                Assert.That(failed.Exception, Is.TypeOf<IOException>());
                Assert.That(files.Data, Is.Empty); Assert.That(session.Document, Is.SameAs(source)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                using var token = session.BeginEdit(this); ((FeatureSampleDocument)session.Document).Version = 7;
                Assert.That(service.Export(path, "actioneditor.json").Succeeded, Is.False);
                Assert.That(token.IsActive, Is.True); Assert.That(((FeatureSampleDocument)session.Document).Version, Is.EqualTo(7)); Assert.That(files.Data, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
    }
}
