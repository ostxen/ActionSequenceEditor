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
    public sealed class SessionEditingTests : SharedContextFixture
    {
        [Test] public void SelectionCandidatesKeepExistingAddAndRetainScopesWithoutEditing()
        {
            var selection = EditorFixture.Shared.Selection;
            var first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]; var second = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips[0];
            selection.Set(new IDirectable[] { first }, false);
            Assert.That(selection.ClickCandidate(second, true, true), Is.EqualTo(new IDirectable[] { first, second }));
            Assert.That(selection.Items, Is.EqualTo(new IDirectable[] { first }));
            selection.Set(new IDirectable[] { first, second }, false);
            Assert.That(selection.ClickCandidate(first, false, true), Is.EqualTo(new IDirectable[] { first, second }));
            Assert.That(selection.ClickCandidate(second, false, false), Is.EqualTo(new IDirectable[] { second }));
            Assert.That(selection.ClickCandidate(null, false, false), Is.Empty);
            Assert.That(EditorFixture.Shared.Session.IsDirty || EditorFixture.Shared.Session.IsEditing, Is.False);
        }

        [Test] public void SharedEditsUndoRedoAndCancelKeepWholeJsonGraph()
        {
            EditorFixture.Shared.Session.Edit("Change payload", () => ((FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count = 12);
            Assert.That(EditorFixture.Shared.Session.IsDirty, Is.True);
            Undo.PerformUndo(); Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
            Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
            Undo.PerformRedo(); Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(12));
            EditorFixture.Shared.Session.BeginEdit(); EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = 1.25f; EditorFixture.Shared.Session.CancelEdit();
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
        }

        [Test] public void CopyPasteTrackAndCutClipPreservePayloadAndRespectLocks()
        {
            var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); var track = group.Tracks[0];
            EditorFixture.Shared.ClipboardEditing.Copy(track); var pasted = (ActionTrack)EditorFixture.Shared.ClipboardEditing.Paste(group);
            Assert.That(pasted, Is.Not.SameAs(track)); Assert.That(pasted.Clips.Count, Is.EqualTo(3));
            Assert.That(((FeatureSampleInterval)pasted.Clips[1]).Offset, Is.EqualTo(new Vector3(2, 1, 0)));
            var clip = track.Clips[0]; EditorFixture.Shared.ClipboardEditing.Copy(clip, true); EditorFixture.Shared.ClipboardEditing.Paste(pasted, 6);
            Assert.That(track.Clips.Count, Is.EqualTo(2)); Assert.That(pasted.Clips.Last().StartTime, Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => EditorFixture.Shared.Structure.Delete(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(2).Tracks[0]));
        }

        private sealed class FailingCreationClip : ActionClip
        {
            public FailingCreationClip() { throw new InvalidOperationException("expected constructor failure"); }
        }

        [Test] public void StructuralCommandsRevalidateAndRollbackFailuresWithoutEmptyUndo()
        {
            var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); var track = group.Tracks[0];
            var command = EditorFixture.Shared.Structure.CreateOperation(track, typeof(FeatureSampleInterval), 8);
            Assert.That(command.CanExecute(out var reason), Is.True, reason);
            track.IsLocked = true;
            Assert.That(command.CanExecute(out reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => command.Execute());
            Assert.That(track.Clips.Count, Is.EqualTo(3)); track.IsLocked = false;
            var before = ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(EditorFixture.Shared.Sequence);
            Assert.Catch(() => EditorFixture.Shared.Structure.AddClip(track, typeof(FailingCreationClip), 8));
            Assert.That(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(EditorFixture.Shared.Sequence), Is.EqualTo(before));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); track = group.Tracks[0];
            var created = EditorFixture.Shared.Structure.AddClip(track, typeof(FeatureSampleInterval), 8);
            Assert.That(created.StartTime, Is.EqualTo(8)); Assert.That(created.Parent, Is.SameAs(track));
            Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(3));
            Undo.PerformRedo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(4));
            Assert.That(command.CanExecute(out reason), Is.False, "Undo/Redo invalidates captured graph targets.");
        }

        [Test] public void BatchDeletionAndLockedCutDoNotPartiallyModifyGraphOrClipboard()
        {
            var track = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            var locked = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(2).Tracks[0];
            var batch = EditorFixture.Shared.Structure.DeleteOperation(clip, locked);
            Assert.That(batch.CanExecute(out var reason), Is.False, reason);
            Assert.Throws<InvalidOperationException>(() => batch.Execute());
            Assert.That(track.Clips.Count, Is.EqualTo(3));
            EditorFixture.Shared.ClipboardEditing.Copy(clip, true); track.IsLocked = true;
            var target = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1];
            Assert.That(EditorFixture.Shared.ClipboardEditing.CanPaste(target), Is.False);
            Assert.Throws<InvalidOperationException>(() => EditorFixture.Shared.ClipboardEditing.Paste(target, 7));
            Assert.That(EditorFixture.Shared.Clipboard.IsCut, Is.True); Assert.That(track.Clips.Count, Is.EqualTo(3));
            track.IsLocked = false;
            EditorFixture.Shared.ClipboardEditing.Paste(target, 7);
            Assert.That(track.Clips.Count, Is.EqualTo(2)); Assert.That(target.Clips.Count, Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(3));
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Clips.Count, Is.EqualTo(2));
            track = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0];
            EditorFixture.Shared.Structure.DeleteOperation(track.Clips[0], track.Clips[1]).Execute();
            Assert.That(track.Clips.Count, Is.EqualTo(1));
            Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(3));
        }

        [Test] public void MultipleEditsHaveIndependentUndoStepsAndFailureRollsBack()
        {
            EditorFixture.Shared.Session.Edit("one", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 2);
            EditorFixture.Shared.Session.Edit("two", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 3);
            Undo.PerformUndo(); Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(2));
            Undo.PerformUndo(); Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => EditorFixture.Shared.Session.Edit("failed", () => { ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 100; throw new InvalidOperationException(); }));
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
        }

        [Test] public void InvalidTimeRollsBackAndProtectedPathsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => EditorFixture.Shared.Session.Edit("invalid", () => EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = float.NaN));
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.Throws<ArgumentException>(() => SequenceFileRepository.ValidatePath("Assets/CombatEditor/Imgui.json"));
            Assert.Throws<ArgumentException>(() => SequenceFileRepository.ValidatePath("Assets/../Imgui.json"));
        }

        [Test] public void NestedCommandsStayInOneUndoAndRangeCanBeShorterThanDocument()
        {
            EditorFixture.Shared.Session.BeginEdit();
            EditorFixture.Shared.Nodes.Rename(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0], "nested");
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.True);
            EditorFixture.Shared.Sequence.RangeMin = 1; EditorFixture.Shared.Sequence.RangeMax = 2;
            Assert.That(EditorFixture.Shared.Sequence.RangeMax, Is.EqualTo(2));
            EditorFixture.Shared.Session.EndEdit("outer"); Undo.PerformUndo();
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Name, Is.Not.EqualTo("nested"));
            Assert.That(EditorFixture.Shared.Sequence.RangeMin, Is.Zero);
        }

        [Test] public void TransactionTokensRejectOtherOwnersAndStaleTokensAndCommitOneUndo()
        {
            var session = EditorFixture.Shared.Session; var first = new object(); var other = new object();
            var token = session.BeginEdit(first);
            Assert.That(token, Is.Not.Null);
            Assert.That(session.BeginEdit(first), Is.SameAs(token));
            Assert.That(session.BeginEdit(other), Is.Null);
            ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 5;
            session.EndEdit(); session.CancelEdit();
            Assert.That(session.IsEditing, Is.True, "Ownerless callers cannot finish another owner's transaction.");
            EditorFixture.Shared.Nodes.Rename(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0], "nested-owned");
            Assert.That(token.IsActive, Is.True);
            token.Commit("Owned gesture"); token.Commit(); token.Cancel();
            Assert.That(session.IsEditing, Is.False);
            Undo.PerformUndo();
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Name, Is.Not.EqualTo("nested-owned"));
            Undo.PerformRedo();
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(5));
            var next = session.BeginEdit(other);
            ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 6;
            token.Cancel(); token.Commit();
            Assert.That(next.IsActive, Is.True);
            next.Cancel(); next.Cancel();
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(5));
            Assert.That(next.IsActive, Is.False);
            Assert.That(next.Update(() => Assert.Fail("A stale token must not execute a mutation.")), Is.False);
        }

        [Test] public void OwnedTransactionsRollbackNestedFailuresAndDoNotAddEmptyUndoSteps()
        {
            var token = EditorFixture.Shared.Session.BeginEdit(this);
            ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 5;
            Assert.Throws<InvalidOperationException>(() => EditorFixture.Shared.Session.Edit("Nested failure", () =>
            {
                ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 9;
                throw new InvalidOperationException("expected failure");
            }));
            Assert.That(token.IsActive, Is.False);
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
            EditorFixture.Shared.Session.Edit("Real change", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 2);
            EditorFixture.Shared.Session.BeginEdit(this).Commit("No change");
            Undo.PerformUndo();
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
            var stale = EditorFixture.Shared.Session.BeginEdit(this);
            EditorFixture.Shared.Session.Open(new SequenceFileRepository().ReadSource(fixture));
            Assert.That(stale.IsActive, Is.False);
            var active = EditorFixture.Shared.Session.BeginEdit(this);
            stale.Cancel(); Assert.That(active.IsActive, Is.True);
            EditorFixture.Shared.Session.DiscardChanges();
            Assert.That(active.IsActive, Is.False);
            var partial = EditorFixture.Shared.Session.BeginEdit(this);
            Assert.Throws<InvalidOperationException>(() => partial.Update(() =>
            {
                ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 99;
                throw new InvalidOperationException("partial update");
            }));
            Assert.That(partial.IsActive, Is.False);
            Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
            var invalid = EditorFixture.Shared.Session.BeginEdit(this);
            EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Add(null);
            Assert.Throws<System.IO.InvalidDataException>(() => invalid.Commit());
            Assert.That(invalid.IsActive, Is.False);
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
        }
    }
}
