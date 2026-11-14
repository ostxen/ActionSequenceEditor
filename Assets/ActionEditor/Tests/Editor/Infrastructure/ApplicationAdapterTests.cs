using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public sealed class ApplicationAdapterTests
    {
        [Test] public void RowUnlockChangesOnlyItsFlagAndUndoRestoresInheritedLocks()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            var document = new FeatureSampleDocument();
            var group = document.RootItems.OfType<NBC.ActionEditor.Domain.TimelineGroup>().First();
            var track = group.Tracks[0];
            var groupName = group.Name; var trackName = track.Name;
            track.IsLocked = true; group.IsLocked = true;
            context.Session.Open(new DocumentOpenData(document));
            var before = JsonDocumentCodec.Encode(context.Document);
            Assert.Throws<InvalidOperationException>(() => context.Structure.Rename(group, "must not change"));
            Assert.Throws<InvalidOperationException>(() => context.Structure.ToggleLock(track));
            Assert.That(JsonDocumentCodec.Encode(context.Document), Is.EqualTo(before));
            context.Structure.ToggleLock(group);
            Assert.That(group.IsLocked, Is.False);
            Assert.That(track.IsLocked, Is.True);
            context.Structure.ToggleLock(track);
            Assert.That(track.IsLocked, Is.False);
            Assert.That(group.Name, Is.EqualTo(groupName));
            Assert.That(track.Name, Is.EqualTo(trackName));
            UnityEditor.Undo.PerformUndo();
            group = context.Document.RootItems.OfType<NBC.ActionEditor.Domain.TimelineGroup>().First();
            Assert.That(group.IsLocked, Is.False);
            Assert.That(group.Tracks[0].IsLocked, Is.True);
            UnityEditor.Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(context.Document), Is.EqualTo(before));
            UnityEditor.Undo.PerformRedo(); UnityEditor.Undo.PerformRedo();
            group = context.Document.RootItems.OfType<NBC.ActionEditor.Domain.TimelineGroup>().First();
            Assert.That(group.IsLocked || group.Tracks[0].IsLocked, Is.False);
            Assert.That(context.Session.IsEditing, Is.False);
        }

        [Test] public void ResizeAndBatchRenameShareOneUndoAndDeletionRestoresBothClips()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            context.Session.Open(new DocumentOpenData(new FeatureSampleDocument()));
            var track = context.Document.AllTracks.First();
            var first = track.Clips[0]; var second = track.Clips[1];
            var before = JsonDocumentCodec.Encode(context.Document);
            using (var token = context.Session.BeginEdit(this))
            {
                context.Clips.Resize(token, first, .5f, .75f);
                context.Structure.Rename(first, "changed"); context.Structure.Rename(second, "changed");
                token.Commit();
            }
            Assert.That(first.StartTime, Is.EqualTo(.5f)); Assert.That(first.Length, Is.EqualTo(.75f));
            Assert.That(first.Name, Is.EqualTo("changed")); Assert.That(second.Name, Is.EqualTo("changed"));
            var changed = JsonDocumentCodec.Encode(context.Document);
            UnityEditor.Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(context.Document), Is.EqualTo(before));
            UnityEditor.Undo.PerformRedo();
            Assert.That(JsonDocumentCodec.Encode(context.Document), Is.EqualTo(changed));
            track = context.Document.AllTracks.First(); first = track.Clips[0]; second = track.Clips[1];
            using (var token = context.Session.BeginEdit(this))
            {
                Assert.That(context.Lifecycle.Save().Succeeded, Is.False);
                Assert.Throws<InvalidOperationException>(() => Composition.DocumentPresenterComposition.ConfirmSwitch(context));
                context.Structure.Deletion(first, second).Execute();
                token.Commit();
            }
            Assert.That(track.Clips.Count, Is.EqualTo(1));
            UnityEditor.Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(context.Document), Is.EqualTo(changed));
            Assert.That(context.Session.IsEditing, Is.False);
        }

        [Test] public void DocumentDialogsKeepExplicitContextAndRejectInvalidCreationNames()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            var fixture = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument())) { name = "Isolated source" };
            var existing = Resources.FindObjectsOfTypeAll<DocumentExportWindow>();
            DocumentExportWindow window = null;
            var global = ActionEditorHost.Context.Document;
            try
            {
                context.Open(fixture);
                var binding = new Composition.DocumentDialogComposition(context);
                Assert.That(binding.DocumentName, Is.EqualTo("Isolated source"));
                Assert.That(binding.ValidateName("  Example  "), Is.EqualTo("Example"));
                foreach (var name in new[] { "", "..", "a/b", "a\\b" })
                    Assert.Throws<ArgumentException>(() => binding.ValidateName(name));
                Assert.That(binding.CreationTypes(), Does.Contain(typeof(FeatureSampleDocument)));
                DocumentExportWindow.ShowFor(context);
                window = Resources.FindObjectsOfTypeAll<DocumentExportWindow>().Except(existing).Single();
                Assert.That(window.ExportTo("actioneditor.json", null), Is.False);
                using var token = context.Session.BeginEdit(this);
                Assert.Throws<InvalidOperationException>(() => window.ExportTo("actioneditor.json", "Assets/ActionEditor/ActiveExportMustNotWrite.json"));
                Assert.That(token.IsActive, Is.True);
                Assert.That(ActionEditorHost.Context.Document, Is.SameAs(global));
            }
            finally
            {
                if (window != null) window.Close();
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        [Test] public void DocumentPresenterClosesOnlyItsInjectedContextAndProtectsActiveEdit()
        {
            var global = ActionEditorHost.Context.Document;
            var globalSource = ActionEditorHost.Context.Source;
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            var fixture = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                context.Open(fixture);
                var presenter = Composition.DocumentPresenterComposition.Create(context);
                using (var token = context.Session.BeginEdit(this))
                {
                    Assert.Throws<InvalidOperationException>(() => presenter.Invoke(DocumentIntent.Close));
                    Assert.Throws<InvalidOperationException>(() => presenter.Invoke(DocumentIntent.Open));
                    Assert.Throws<InvalidOperationException>(() => presenter.Invoke(DocumentIntent.Create));
                    Assert.Throws<InvalidOperationException>(() => Composition.DocumentPresenterComposition.ConfirmSwitch(context));
                    var creation = context.Lifecycle.Create(typeof(FeatureSampleDocument), "Assets/ActionEditor/ActiveEditMustNotCreate.json");
                    Assert.That(creation.Succeeded, Is.False);
                    Assert.That(creation.Exception, Is.TypeOf<InvalidOperationException>());
                    Assert.That(creation.BytesCommitted, Is.False);
                    Assert.That(token.IsActive, Is.True);
                    token.Commit();
                }
                presenter.Invoke(DocumentIntent.Close);
                Assert.That(context.Document, Is.Null);
                Assert.That(context.Source, Is.Null);
                Assert.That(ActionEditorHost.Context.Document, Is.SameAs(global));
                Assert.That(ActionEditorHost.Context.Source, Is.SameAs(globalSource));
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }

        [Test] public void PreferencesAdapterUsesOnlyItsExplicitPathAndFailedWriteKeepsSharedValue()
        {
            var directory = System.IO.Path.Combine("Temp", "Preferences-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "preferences.json");
            try
            {
                var store = new Infrastructure.PreferencesStore(path);
                var session = new NBC.ActionEditor.Application.Settings.SettingsSession(store);
                var baseline = session.Current;
                var candidate = new NBC.ActionEditor.Application.Settings.EditorSettings(NBC.ActionEditor.Application.Settings.TimeStepMode.Frames,
                    .025f, 40, false, false, 15, "Assets/ActionEditor/");
                Assert.That(session.Commit(candidate).Succeeded, Is.True);
                Assert.That(new Infrastructure.PreferencesStore(path).Read(), Is.EqualTo(candidate));
                var failing = new NBC.ActionEditor.Application.Settings.SettingsSession(new Infrastructure.PreferencesStore(System.IO.Path.Combine(directory, "missing", "preferences.json")));
                Assert.That(failing.Commit(candidate).Succeeded, Is.False);
                Assert.That(failing.Current, Is.EqualTo(baseline));
            }
            finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(directory); }
        }
        private sealed class Files : ICommitFiles
        {
            public readonly Dictionary<string, byte[]> Data = new Dictionary<string, byte[]>();
            public bool Exists(string path) => Data.ContainsKey(path);
            public byte[] Read(string path) => Data[path].ToArray();
            public void Write(string path, byte[] bytes) => Data.Add(path, bytes.ToArray());
            public void Replace(string staged, string target) { Data[target] = Data[staged]; Data.Remove(staged); }
            public void Move(string staged, string target) { Data.Add(target, Data[staged]); Data.Remove(staged); }
            public void Delete(string path) => Data.Remove(path);
        }
        [Test] public void OpeningUsesOneByteRevisionAndNotStaleUnityResourceContents()
        {
            const string path = "Assets/ActionEditor/LifecycleFixture.json";
            var files = new Files(); var source = new FeatureSampleDocument { Version = 7 };
            files.Data[path] = System.Text.Encoding.UTF8.GetBytes(JsonDocumentCodec.Encode(source));
            var repository = new JsonDocumentRepository(files, _ => { });
            var opened = repository.Read(path);
            Assert.That(((FeatureSampleDocument)opened.Document).Version, Is.EqualTo(7));
            var bytes = opened.Revision; bytes[0] ^= 1;
            Assert.That(opened.Revision, Is.EqualTo(files.Data[path]));
            files.Data[path] = System.Text.Encoding.UTF8.GetBytes(JsonDocumentCodec.Encode(new FeatureSampleDocument { Version = 9 }));
            Assert.Throws<IOException>(() => repository.Commit(path, opened.Document, opened.Revision));
            Assert.That(((FeatureSampleDocument)JsonDocumentCodec.Decode(System.Text.Encoding.UTF8.GetString(files.Data[path]))).Version, Is.EqualTo(9));
        }
        [Test] public void SaveExportAndFailedSavePreserveRealContextSelectionPlayheadAndPreview()
        {
            const string path = "Assets/ActionEditor/LifecycleFixture.json";
            var files = new Files();
            var repository = new JsonDocumentRepository(files, _ => throw new IOException("import"));
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create(repository), repository);
            var fixture = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            try
            {
                context.Open(fixture);
                var clip = context.Document.AllTracks.First().Clips.First();
                context.Selection.Set(new[] { clip }, false);
                context.Player.Sample(.5f); context.Playback.Pause(true);
                var graph = context.Document; var initialized = context.Player.IsInitialized;
                var lifecycle = new DocumentLifecycle(context.Session, repository, new DocumentExportService(files), new DocumentFactory());
                var result = lifecycle.Save(path);
                Assert.That(result.Succeeded, Is.False); Assert.That(result.BytesCommitted, Is.True);
                Check();
                Assert.That(lifecycle.Export("Assets/ActionEditor/LifecycleExport.json", "actioneditor.json").Succeeded, Is.True);
                Check();
                context.Session.Edit("dirty", () => ((FeatureSampleDocument)context.Document).Version = 4);
                context.Player.Sample(.5f); context.Playback.Pause(true);
                files.Data[path] = new byte[] { 42 };
                Assert.That(lifecycle.Save().BytesCommitted, Is.False);
                Assert.That(context.Session.IsDirty, Is.True); Check();
                using var token = context.Session.BeginEdit(this);
                Assert.That(lifecycle.Save().Succeeded, Is.False); Assert.That(lifecycle.Export("Assets/ActionEditor/LifecycleOther.json", "actioneditor.json").Succeeded, Is.False);
                Assert.That(token.IsActive, Is.True); Check();
                void Check()
                {
                    Assert.That(context.Document, Is.SameAs(graph)); Assert.That(context.Selection.First, Is.SameAs(clip));
                    Assert.That(context.Player.CurrentTime, Is.EqualTo(.5f)); Assert.That(context.Player.IsInitialized, Is.EqualTo(initialized));
                    Assert.That(context.Playback.IsPaused, Is.True);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }
    }
}
