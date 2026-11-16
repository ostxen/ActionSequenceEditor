using NBC.ActionEditor.Infrastructure.Sequences;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Presentation.Timeline;
using System.Linq;
using NBC.ActionEditor.Model;
using System;
using System.Collections.Generic;
using System.IO;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
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
        private sealed class WriteOnly : ISequenceFormat
        {
            public string Id => "tests.export-only";
            public string DisplayName => "Test export";
            public string Extension => ".test";
            public bool CanRead => false; public bool CanWrite => true; public bool CanExport => true;
            public byte[] Serialize(ActionSequence asset, SequenceFormatOptions options, SequenceFormatContext context) => new byte[] { 1, 2, 3 };
            public ActionSequence Deserialize(byte[] bytes, SequenceFormatContext context) => throw new AssertionException("Write-only export must not deserialize.");
        }
        [Test] public void WriteOnlyFormatDoesNotNeedReadbackOrEnterProductionRegistry()
        {
            using var session = NBC.ActionEditor.Composition.SequenceComposition.Create(); var memory = new TextAsset(SequenceJsonCodec.Encode(new FeatureSampleSequence()));
            try
            {
                session.Open(new SequenceFileRepository().ReadSource(memory)); var files = new Files();
                var registry = new SequenceFormatRegistry(new ISequenceFormat[] { new WriteOnly() });
                Assert.That(registry.Editable, Is.Empty);
                Assert.That(new SequenceLifecycle(session, new SequenceFileRepository(files), new SequenceExportService(files, registry), new SequenceFactory()).Export("Assets/ActionEditor/ExportFixture.test", "tests.export-only").Succeeded, Is.True);
                Assert.That(files.Data["Assets/ActionEditor/ExportFixture.test"], Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(SequenceFormats.Registry.Exportable.Count, Is.EqualTo(1));
                Assert.That(SequenceFormats.Registry.Exportable[0].Id, Is.EqualTo("actioneditor.json"));
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void CanonicalCaseAndDotSourceAliasesAreRejectedBeforeWriting()
        {
            var files = new Files(); using var session = NBC.ActionEditor.Composition.SequenceComposition.Create(new SequenceFileRepository(files, _ => throw new IOException("Injected import failure")));
            var memory = new TextAsset(SequenceJsonCodec.Encode(new FeatureSampleSequence()));
            try
            {
                session.Open(new SequenceFileRepository().ReadSource(memory));
                Assert.Throws<IOException>(() => session.Save("Assets/ActionEditor/ExportFixture.json"));
                var before = session.CaptureRecovery();
                foreach (var alias in new[] { "Assets/actioneditor/./exportfixture.JSON", "Assets/ActionEditor/./ExportFixture.json" })
                    Assert.Throws<IOException>(() => new SequenceExportService(files).Export(session.Sequence, session.Path, alias, "actioneditor.json"));
                Assert.That(files.Data.Count, Is.EqualTo(1)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [TestCase(false)] [TestCase(true)]
        public void CleanAndDirtyExportPreserveGraphBaselineUndoAndWorkspace(bool dirty)
        {
            using var session = NBC.ActionEditor.Composition.SequenceComposition.Create();
            var memory = new TextAsset(SequenceJsonCodec.Encode(new FeatureSampleSequence()));
            try
            {
                session.Open(new SequenceFileRepository().ReadSource(memory));
                var workspace = new TimelineWorkspace(); workspace.Bind(session.Sequence); workspace.ViewTimeMax = 12; workspace.SetCollapsed(session.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0), true);
                if (dirty) session.Edit("business", () => ((FeatureSampleSequence)session.Sequence).Version = 9);
                var source = session.Sequence; var before = session.CaptureRecovery();
                var files = new Files(); var service = new SequenceLifecycle(session, new SequenceFileRepository(files), new SequenceExportService(files), new SequenceFactory());
                const string path = "Assets/ActionEditor/ExportFixture.json";
                Assert.That(service.Export(null, "actioneditor.json").Cancelled, Is.True);
                Assert.That(service.Export(path, "actioneditor.json").Succeeded, Is.True);
                Assert.That(session.Sequence, Is.SameAs(source)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                Assert.That(session.IsDirty, Is.EqualTo(dirty));
                var loaded = SequenceJsonCodec.Decode(System.Text.Encoding.UTF8.GetString(files.Data[path]));
                Assert.That(workspace.ViewTimeMax, Is.EqualTo(12)); Assert.That(workspace.IsCollapsed(session.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0)), Is.True);
                Assert.That(System.Text.Encoding.UTF8.GetString(files.Data[path]), Does.Not.Contain("viewTime").And.Not.Contain("isCollapsed"));
                Assert.That(((FeatureSampleSequence)loaded).Version, Is.EqualTo(dirty ? 9 : 1));
                Assert.That(service.Export(path, "actioneditor.json").Succeeded, Is.False);
                Assert.That(files.Data.Count, Is.EqualTo(1)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                if (dirty)
                {
                    Undo.PerformUndo(); Assert.That(((FeatureSampleSequence)session.Sequence).Version, Is.EqualTo(1));
                    Undo.PerformRedo(); Assert.That(((FeatureSampleSequence)session.Sequence).Version, Is.EqualTo(9));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
        [Test] public void ActiveGestureAndWriteFailuresDoNotChangeSessionOrLeaveFiles()
        {
            using var session = NBC.ActionEditor.Composition.SequenceComposition.Create(); var memory = new TextAsset(SequenceJsonCodec.Encode(new FeatureSampleSequence()));
            try
            {
                session.Open(new SequenceFileRepository().ReadSource(memory)); var files = new Files { FailWrite = true }; var service = new SequenceLifecycle(session, new SequenceFileRepository(files), new SequenceExportService(files), new SequenceFactory());
                const string path = "Assets/ActionEditor/ExportFixture.json";
                var before = session.CaptureRecovery(); var source = session.Sequence;
                var failed = service.Export(path, "actioneditor.json");
                Assert.That(failed.Succeeded || failed.BytesCommitted, Is.False);
                Assert.That(failed.Exception, Is.TypeOf<IOException>());
                Assert.That(files.Data, Is.Empty); Assert.That(session.Sequence, Is.SameAs(source)); Assert.That(session.CaptureRecovery(), Is.EqualTo(before));
                using var token = session.BeginEdit(this); ((FeatureSampleSequence)session.Sequence).Version = 7;
                Assert.That(service.Export(path, "actioneditor.json").Succeeded, Is.False);
                Assert.That(token.IsActive, Is.True); Assert.That(((FeatureSampleSequence)session.Sequence).Version, Is.EqualTo(7)); Assert.That(files.Data, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(memory); }
        }
    }
}
