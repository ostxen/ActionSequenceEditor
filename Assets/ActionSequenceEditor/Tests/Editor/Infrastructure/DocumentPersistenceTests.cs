using ActionSequenceEditor.Views.IMGUI;
using ImguiWindow = ActionSequenceEditor.Views.IMGUI.ActionSequenceEditorWindow;
using ToolkitWindow = ActionSequenceEditor.Views.UIToolkit.ActionSequenceEditorWindow;
using ToolkitSettings = ActionSequenceEditor.Views.UIToolkit.SettingsWindow;
using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Presentation.Timeline;
using ActionSequenceEditor.Model;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ActionSequenceEditor.Samples.FeatureSample;
using ActionSequenceEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Tests
{
    public sealed class DocumentPersistenceTests : SharedContextFixture
    {
        [Test] public void SampleRoundtripContainsAllGroupsTracksClipsAndFields()
        {
            var sample = (FeatureSampleSequence)EditorFixture.Shared.Sequence;
            Assert.That(sample.RootContainers.OfType<ActionGroup>().Count(), Is.EqualTo(3));
            Assert.That(sample.RootContainers.OfType<ActionGroup>().Sum(g => g.Tracks.Count), Is.EqualTo(7));
            Assert.That(sample.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks).Sum(t => t.Clips.Count), Is.EqualTo(13));
            Assert.That(sample.Length, Is.EqualTo(6));
            var interval = (FeatureSampleInterval)sample.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[1];
            Assert.That(interval.Offset, Is.EqualTo(new Vector3(2, 1, 0)));
            Assert.That(interval.Parameters.Count, Is.EqualTo(2)); Assert.That(interval.CanScale(), Is.True);
            Assert.That(sample.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[2].Clips[0].CanScale(), Is.False);
            Assert.That(sample.RootContainers.OfType<ActionGroup>().ElementAt(2).Tracks[0].IsLocked, Is.True);
            Assert.That(sample.RootContainers.OfType<ActionGroup>().ElementAt(2).Tracks[1].IsActive, Is.False);
        }

        [Test] public void SaveSnapshotRefreshesDerivedDurationWithoutDroppingEditedPayload()
        {
            var clip = (FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
            clip.StartTime = 8; clip.Length = 3; clip.Count = 27;
            Assert.That(EditorFixture.Shared.Sequence.Length, Is.EqualTo(6), "The public graph can contain a stale derived duration before commit.");
            Assert.That(SequenceJsonCodec.InspectReadbackDifferences(EditorFixture.Shared.Sequence), Is.Empty, "Readback validates an isolated derived snapshot.");
            var json = SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence);
            var loaded = ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Decode(json);
            Assert.That(EditorFixture.Shared.Sequence.Length, Is.EqualTo(6), "Save snapshot must not mutate source duration.");
            Assert.That(loaded.Length, Is.EqualTo(11));
            Assert.That(((FeatureSampleInterval)loaded.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(27));
            Assert.That(SequenceJsonCodec.InspectReadbackDifferences(EditorFixture.Shared.Sequence), Is.Empty);
        }

        [Test] public void JsonCodecPreservesFormatAndRejectsPayloadDifferences()
        {
            var document = EditorFixture.Shared.Sequence;
            var json = ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(document);
            Assert.That(SequenceJsonCodec.Encode(document), Is.EqualTo(json));
            var decoded = SequenceJsonCodec.Decode(json);
            Assert.That(SequenceJsonCodec.ReadbackDifferences(json, decoded), Is.Empty);
            Workspace.SetRange(1, 9);
            Workspace.SetCollapsed(decoded.RootContainers.OfType<ActionGroup>().ElementAt(0), true);
            Assert.That(SequenceJsonCodec.GetContentKey(decoded), Is.EqualTo(SequenceJsonCodec.GetContentKey(document)), "Workspace is excluded only from author content.");
            Assert.That(SequenceJsonCodec.ReadbackDifferences(json, decoded), Is.Empty, "Workspace does not enter author snapshots.");
            decoded = SequenceJsonCodec.Decode(json);
            ((FeatureSampleInterval)decoded.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count = 91;
            var differences = SequenceJsonCodec.ReadbackDifferences(json, decoded);
            Assert.That(differences, Has.Length.EqualTo(1));
            Assert.That(differences[0], Does.Contain("Count"));
            Assert.That(differences[0], Does.Not.Contain("91"), "Diagnostics must contain paths, not user values.");
            decoded.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Add(null);
            Assert.Throws<InvalidDataException>(() => SequenceJsonCodec.CreateSaveSnapshot(decoded));
            Assert.That(EditorFixture.Shared.Sequence, Is.SameAs(document));
            Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
        }

        [Test] public void FractionalTimelineEditsKeepSaveReadbackStable()
        {
            var random = new System.Random(1937);
            for (var iteration = 0; iteration < 80; iteration++)
            {
                EditorFixture.Shared.Session.Edit("fractional edit", () =>
                {
                    foreach (var clip in EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                    {
                        clip.StartTime = (float)(random.NextDouble() * 17);
                        if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                    }
                });
                var json = SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence);
                var differences = SequenceJsonCodec.InspectReadbackDifferences(EditorFixture.Shared.Sequence);
                Assert.That(differences, Is.Empty, "Iteration " + iteration + ": " + string.Join(", ", differences));
                Assert.That(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Decode(json).Length, Is.EqualTo(EditorFixture.Shared.Sequence.Length));
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
                    var context = new ActionSequenceEditor.Serialization.SequenceFormatContext(Array.Empty<Type>());
                    var tree = ActionSequenceEditor.Serialization.JsonValueConverter.Capture(value, typeof(float), context);
                    var text = ActionSequenceEditor.Serialization.JsonText.Print(tree, compressed);
                    var loaded = (float)ActionSequenceEditor.Serialization.JsonValueConverter.Restore(ActionSequenceEditor.Serialization.JsonText.Parse(text), typeof(float), context);
                    Assert.That(BitConverter.GetBytes(loaded), Is.EqualTo(BitConverter.GetBytes(value)));
                }
            }
            Assert.That(ActionSequenceEditor.Serialization.JsonValueConverter.Restore(ActionSequenceEditor.Serialization.JsonText.Parse("0.1"), typeof(float), new ActionSequenceEditor.Serialization.SequenceFormatContext(Array.Empty<Type>())), Is.EqualTo(.1f));
            Assert.That(ActionSequenceEditor.Serialization.JsonValueConverter.Restore(ActionSequenceEditor.Serialization.JsonText.Parse("3"), typeof(float), new ActionSequenceEditor.Serialization.SequenceFormatContext(Array.Empty<Type>())), Is.EqualTo(3f));
        }

        [Test] public void RecoveryRejectsOldContractWithoutReplacingCurrentDocument()
        {
            var before = EditorFixture.Shared.Session.Sequence; var recovery = EditorFixture.Shared.Session.CaptureRecovery();
            Assert.That(recovery, Does.Contain("actioneditor.recovery.v2"));
            var old = recovery.Replace("\"Version\":2", "\"Version\":1").Replace("actioneditor.recovery.v2", "actioneditor.json");
            Assert.Throws<InvalidDataException>(() => EditorFixture.Shared.Session.RestoreRecovery(old));
            Assert.That(EditorFixture.Shared.Session.Sequence, Is.SameAs(before));
            Assert.That(EditorFixture.Shared.Session.CaptureRecovery(), Is.EqualTo(recovery));
        }

        [Test] public void RecoveryPreservesUnsavedChangesAndBaseline()
        {
            EditorFixture.Shared.Session.Edit("change", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 9);
            var recovery = EditorFixture.Shared.Session.CaptureRecovery();
            using var restored = ActionSequenceEditor.Composition.SequenceComposition.Create(); restored.RestoreRecovery(recovery);
            Assert.That(((FeatureSampleSequence)restored.Sequence).Version, Is.EqualTo(9));
            Assert.That(restored.IsDirty, Is.True);
        }

        [Test] public void RecoveryKeepsDiscardBaselineIncludingViewStateAndRepeatedDiscard()
        {
            Workspace.ViewTimeMin = 1; Workspace.ViewTimeMax = 10;
            EditorFixture.Shared.Session.Edit("change", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 9);
            using var restored = ActionSequenceEditor.Composition.SequenceComposition.Create();
            restored.RestoreRecovery(EditorFixture.Shared.Session.CaptureRecovery());
            restored.DiscardChanges();
            Assert.That(((FeatureSampleSequence)restored.Sequence).Version, Is.EqualTo(1));
            Assert.That(Workspace.ViewTimeMin, Is.EqualTo(1));
            Assert.That(Workspace.ViewTimeMax, Is.EqualTo(10));
            Assert.That(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(restored.Sequence), Does.Not.Contain("isCollapsed"));
            Assert.That(restored.IsDirty, Is.False);
            restored.DiscardChanges();
            Assert.That(restored.IsDirty || restored.IsEditing, Is.False);
        }

        [Test] public void SnapshotReadbackAndReliableCommitPreserveExistingFileOnFailure()
        {
            var directory = System.IO.Path.Combine("Temp", "ActionSequenceEditor-R-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); var path = System.IO.Path.Combine(directory, "Document.json");
            try
            {
                var original = System.Text.Encoding.UTF8.GetBytes(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(EditorFixture.Shared.Sequence));
                new AtomicFileCommit().Save(path, () => original, bytes => ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Decode(System.Text.Encoding.UTF8.GetString(bytes)));
                Assert.That(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Decode(File.ReadAllText(path)).RootContainers.OfType<ActionGroup>().Count(), Is.EqualTo(3));
                Assert.Throws<IOException>(() => new AtomicFileCommit().Save(path, () => original, _ => { }, new byte[] { 1 }));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
