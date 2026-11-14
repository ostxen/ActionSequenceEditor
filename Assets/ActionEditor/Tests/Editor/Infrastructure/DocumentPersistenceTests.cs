using NBC.ActionEditor.Views.IMGUI;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitPreferences = NBC.ActionEditor.Views.UIToolkit.PreferencesWindow;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class DocumentPersistenceTests : SharedContextFixture
    {
        [Test] public void SampleRoundtripContainsAllGroupsTracksClipsAndFields()
        {
            var sample = (FeatureSampleDocument)ActionEditorHost.Context.Document;
            Assert.That(sample.RootItems.OfType<TimelineGroup>().Count(), Is.EqualTo(3));
            Assert.That(sample.RootItems.OfType<TimelineGroup>().Sum(g => g.Tracks.Count), Is.EqualTo(7));
            Assert.That(sample.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).Sum(t => t.Clips.Count), Is.EqualTo(13));
            Assert.That(sample.Length, Is.EqualTo(6));
            var interval = (FeatureSampleInterval)sample.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[1];
            Assert.That(interval.Offset, Is.EqualTo(new Vector3(2, 1, 0)));
            Assert.That(interval.Parameters.Count, Is.EqualTo(2)); Assert.That(interval.CanScale(), Is.True);
            Assert.That(sample.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[2].Clips[0].CanScale(), Is.False);
            Assert.That(sample.RootItems.OfType<TimelineGroup>().ElementAt(2).Tracks[0].IsLocked, Is.True);
            Assert.That(sample.RootItems.OfType<TimelineGroup>().ElementAt(2).Tracks[1].IsActive, Is.False);
        }

        [Test] public void SaveSnapshotRefreshesDerivedDurationWithoutDroppingEditedPayload()
        {
            var clip = (FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
            clip.StartTime = 8; clip.Length = 3; clip.Count = 27;
            Assert.That(ActionEditorHost.Context.Document.Length, Is.EqualTo(6), "The public graph can contain a stale derived duration before commit.");
            Assert.That(JsonDocumentCodec.InspectReadbackDifferences(ActionEditorHost.Context.Document), Is.Empty, "Readback validates an isolated derived snapshot.");
            var json = JsonDocumentCodec.CreateSaveSnapshot(ActionEditorHost.Context.Document);
            var loaded = NBC.ActionEditor.Storage.JsonDocumentCodec.Decode(json);
            Assert.That(ActionEditorHost.Context.Document.Length, Is.EqualTo(6), "Save snapshot must not mutate source duration.");
            Assert.That(loaded.Length, Is.EqualTo(11));
            Assert.That(((FeatureSampleInterval)loaded.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(27));
            Assert.That(JsonDocumentCodec.InspectReadbackDifferences(ActionEditorHost.Context.Document), Is.Empty);
        }

        [Test] public void JsonCodecPreservesFormatAndRejectsPayloadDifferences()
        {
            var document = ActionEditorHost.Context.Document;
            var json = NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(document);
            Assert.That(JsonDocumentCodec.Encode(document), Is.EqualTo(json));
            var decoded = JsonDocumentCodec.Decode(json);
            Assert.That(JsonDocumentCodec.ReadbackDifferences(json, decoded), Is.Empty);
            Workspace.SetRange(1, 9);
            Workspace.SetCollapsed(decoded.RootItems.OfType<TimelineGroup>().ElementAt(0), true);
            Assert.That(JsonDocumentCodec.Content(decoded), Is.EqualTo(JsonDocumentCodec.Content(document)), "Workspace is excluded only from author content.");
            Assert.That(JsonDocumentCodec.ReadbackDifferences(json, decoded), Is.Empty, "Workspace does not enter author snapshots.");
            decoded = JsonDocumentCodec.Decode(json);
            ((FeatureSampleInterval)decoded.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count = 91;
            var differences = JsonDocumentCodec.ReadbackDifferences(json, decoded);
            Assert.That(differences, Has.Length.EqualTo(1));
            Assert.That(differences[0], Does.Contain("Count"));
            Assert.That(differences[0], Does.Not.Contain("91"), "Diagnostics must contain paths, not user values.");
            decoded.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Add(null);
            Assert.Throws<InvalidDataException>(() => JsonDocumentCodec.CreateSaveSnapshot(decoded));
            Assert.That(ActionEditorHost.Context.Document, Is.SameAs(document));
            Assert.That(ActionEditorHost.Context.Session.IsDirty, Is.False);
        }

        [Test] public void FractionalTimelineEditsKeepSaveReadbackStable()
        {
            var random = new System.Random(1937);
            for (var iteration = 0; iteration < 80; iteration++)
            {
                ActionEditorHost.Context.Session.Edit("fractional edit", () =>
                {
                    foreach (var clip in ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                    {
                        clip.StartTime = (float)(random.NextDouble() * 17);
                        if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                    }
                });
                var json = JsonDocumentCodec.CreateSaveSnapshot(ActionEditorHost.Context.Document);
                var differences = JsonDocumentCodec.InspectReadbackDifferences(ActionEditorHost.Context.Document);
                Assert.That(differences, Is.Empty, "Iteration " + iteration + ": " + string.Join(", ", differences));
                Assert.That(NBC.ActionEditor.Storage.JsonDocumentCodec.Decode(json).Length, Is.EqualTo(ActionEditorHost.Context.Document.Length));
            }
        }

        [Test] public void FloatSerializationPreservesAuthoredBitsAndStandardJsonNumbers()
        {
            var values = new System.Collections.Generic.List<float> { float.Epsilon, -float.Epsilon, float.MaxValue, float.MinValue, .1f, 16.3388195f };
            var random = new System.Random(1937);
            for (var i = 0; i < 512; i++)
            {
                var bytes = new byte[4]; random.NextBytes(bytes);
                var value = BitConverter.ToSingle(bytes, 0);
                if (!float.IsNaN(value) && !float.IsInfinity(value)) values.Add(value);
            }
            foreach (var value in values)
            {
                foreach (var compressed in new[] { false, true })
                {
                    var context = new NBC.ActionEditor.Serialization.DocumentFormatContext(Array.Empty<Type>());
                    var tree = NBC.ActionEditor.Serialization.DocumentValueConverter.Capture(value, typeof(float), context);
                    var text = NBC.ActionEditor.Serialization.DocumentJson.Print(tree, compressed);
                    var loaded = (float)NBC.ActionEditor.Serialization.DocumentValueConverter.Restore(NBC.ActionEditor.Serialization.DocumentJson.Parse(text), typeof(float), context);
                    Assert.That(BitConverter.GetBytes(loaded), Is.EqualTo(BitConverter.GetBytes(value)));
                }
            }
            Assert.That(NBC.ActionEditor.Serialization.DocumentValueConverter.Restore(NBC.ActionEditor.Serialization.DocumentJson.Parse("0.1"), typeof(float), new NBC.ActionEditor.Serialization.DocumentFormatContext(Array.Empty<Type>())), Is.EqualTo(.1f));
            Assert.That(NBC.ActionEditor.Serialization.DocumentValueConverter.Restore(NBC.ActionEditor.Serialization.DocumentJson.Parse("3"), typeof(float), new NBC.ActionEditor.Serialization.DocumentFormatContext(Array.Empty<Type>())), Is.EqualTo(3f));
        }

        [Test] public void RecoveryRejectsOldContractWithoutReplacingCurrentDocument()
        {
            var before = ActionEditorHost.Context.Session.Document; var recovery = ActionEditorHost.Context.Session.CaptureRecovery();
            Assert.That(recovery, Does.Contain("actioneditor.recovery.v2"));
            var old = recovery.Replace("\"Version\":2", "\"Version\":1").Replace("actioneditor.recovery.v2", "actioneditor.json");
            Assert.Throws<InvalidDataException>(() => ActionEditorHost.Context.Session.RestoreRecovery(old));
            Assert.That(ActionEditorHost.Context.Session.Document, Is.SameAs(before));
            Assert.That(ActionEditorHost.Context.Session.CaptureRecovery(), Is.EqualTo(recovery));
        }

        [Test] public void RecoveryPreservesUnsavedChangesAndBaseline()
        {
            ActionEditorHost.Context.Session.Edit("change", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 9);
            var recovery = ActionEditorHost.Context.Session.CaptureRecovery();
            using var restored = NBC.ActionEditor.Composition.DocumentComposition.Create(); restored.RestoreRecovery(recovery);
            Assert.That(((FeatureSampleDocument)restored.Document).Version, Is.EqualTo(9));
            Assert.That(restored.IsDirty, Is.True);
        }

        [Test] public void RecoveryKeepsDiscardBaselineIncludingViewStateAndRepeatedDiscard()
        {
            Workspace.ViewTimeMin = 1; Workspace.ViewTimeMax = 10;
            ActionEditorHost.Context.Session.Edit("change", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 9);
            using var restored = NBC.ActionEditor.Composition.DocumentComposition.Create();
            restored.RestoreRecovery(ActionEditorHost.Context.Session.CaptureRecovery());
            restored.DiscardChanges();
            Assert.That(((FeatureSampleDocument)restored.Document).Version, Is.EqualTo(1));
            Assert.That(Workspace.ViewTimeMin, Is.EqualTo(1));
            Assert.That(Workspace.ViewTimeMax, Is.EqualTo(10));
            Assert.That(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(restored.Document), Does.Not.Contain("isCollapsed"));
            Assert.That(restored.IsDirty, Is.False);
            restored.DiscardChanges();
            Assert.That(restored.IsDirty || restored.IsEditing, Is.False);
        }

        [Test] public void SnapshotReadbackAndReliableCommitPreserveExistingFileOnFailure()
        {
            var directory = System.IO.Path.Combine("Temp", "ActionEditor-R-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); var path = System.IO.Path.Combine(directory, "Document.json");
            try
            {
                var original = System.Text.Encoding.UTF8.GetBytes(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(ActionEditorHost.Context.Document));
                new AtomicFileCommit().Save(path, () => original, bytes => NBC.ActionEditor.Storage.JsonDocumentCodec.Decode(System.Text.Encoding.UTF8.GetString(bytes)));
                Assert.That(NBC.ActionEditor.Storage.JsonDocumentCodec.Decode(File.ReadAllText(path)).RootItems.OfType<TimelineGroup>().Count(), Is.EqualTo(3));
                Assert.Throws<IOException>(() => new AtomicFileCommit().Save(path, () => original, _ => { }, new byte[] { 1 }));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
