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
    public sealed class SessionEditingTests : SharedContextFixture
    {
        [Test] public void SelectionCandidatesKeepExistingAddAndRetainScopesWithoutEditing()
        {
            var selection = ActionEditorHost.Context.Selection;
            var first = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]; var second = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1].Clips[0];
            selection.Set(new IDirectable[] { first }, false);
            Assert.That(selection.ClickCandidate(second, true, true), Is.EqualTo(new IDirectable[] { first, second }));
            Assert.That(selection.Items, Is.EqualTo(new IDirectable[] { first }));
            selection.Set(new IDirectable[] { first, second }, false);
            Assert.That(selection.ClickCandidate(first, false, true), Is.EqualTo(new IDirectable[] { first, second }));
            Assert.That(selection.ClickCandidate(second, false, false), Is.EqualTo(new IDirectable[] { second }));
            Assert.That(selection.ClickCandidate(null, false, false), Is.Empty);
            Assert.That(ActionEditorHost.Context.Session.IsDirty || ActionEditorHost.Context.Session.IsEditing, Is.False);
        }

        [Test] public void SharedEditsUndoRedoAndCancelKeepWholeJsonGraph()
        {
            ActionEditorHost.Context.Session.Edit("Change payload", () => ((FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count = 12);
            Assert.That(ActionEditorHost.Context.Session.IsDirty, Is.True);
            Undo.PerformUndo(); Assert.That(((FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
            Assert.That(ActionEditorHost.Context.Session.IsDirty, Is.False);
            Undo.PerformRedo(); Assert.That(((FeatureSampleInterval)ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(12));
            ActionEditorHost.Context.Session.BeginEdit(); ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = 1.25f; ActionEditorHost.Context.Session.CancelEdit();
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
        }

        [Test] public void CopyPasteTrackAndCutClipPreservePayloadAndRespectLocks()
        {
            var group = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0); var track = group.Tracks[0];
            ActionEditorHost.Context.ClipboardEditing.Copy(track); var pasted = (TimelineTrack)ActionEditorHost.Context.ClipboardEditing.Paste(group);
            Assert.That(pasted, Is.Not.SameAs(track)); Assert.That(pasted.Clips.Count, Is.EqualTo(3));
            Assert.That(((FeatureSampleInterval)pasted.Clips[1]).Offset, Is.EqualTo(new Vector3(2, 1, 0)));
            var clip = track.Clips[0]; ActionEditorHost.Context.ClipboardEditing.Copy(clip, true); ActionEditorHost.Context.ClipboardEditing.Paste(pasted, 6);
            Assert.That(track.Clips.Count, Is.EqualTo(2)); Assert.That(pasted.Clips.Last().StartTime, Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => ActionEditorHost.Context.Structure.Delete(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(2).Tracks[0]));
        }

        private sealed class FailingCreationClip : TimelineClip
        {
            public FailingCreationClip() { throw new InvalidOperationException("expected constructor failure"); }
        }

        [Test] public void StructuralCommandsRevalidateAndRollbackFailuresWithoutEmptyUndo()
        {
            var group = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0); var track = group.Tracks[0];
            var command = ActionEditorHost.Context.Structure.Creation(track, typeof(FeatureSampleInterval), 8);
            Assert.That(command.CanExecute(out var reason), Is.True, reason);
            track.IsLocked = true;
            Assert.That(command.CanExecute(out reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => command.Execute());
            Assert.That(track.Clips.Count, Is.EqualTo(3)); track.IsLocked = false;
            var before = NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(ActionEditorHost.Context.Document);
            Assert.Catch(() => ActionEditorHost.Context.Structure.AddClip(track, typeof(FailingCreationClip), 8));
            Assert.That(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(ActionEditorHost.Context.Document), Is.EqualTo(before));
            Assert.That(ActionEditorHost.Context.Session.IsEditing, Is.False);
            group = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0); track = group.Tracks[0];
            var created = ActionEditorHost.Context.Structure.AddClip(track, typeof(FeatureSampleInterval), 8);
            Assert.That(created.StartTime, Is.EqualTo(8)); Assert.That(created.Parent, Is.SameAs(track));
            Undo.PerformUndo(); Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(3));
            Undo.PerformRedo(); Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(4));
            Assert.That(command.CanExecute(out reason), Is.False, "Undo/Redo invalidates captured graph targets.");
        }

        [Test] public void BatchDeletionAndLockedCutDoNotPartiallyModifyGraphOrClipboard()
        {
            var track = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            var locked = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(2).Tracks[0];
            var batch = ActionEditorHost.Context.Structure.Deletion(clip, locked);
            Assert.That(batch.CanExecute(out var reason), Is.False, reason);
            Assert.Throws<InvalidOperationException>(() => batch.Execute());
            Assert.That(track.Clips.Count, Is.EqualTo(3));
            ActionEditorHost.Context.ClipboardEditing.Copy(clip, true); track.IsLocked = true;
            var target = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1];
            Assert.That(ActionEditorHost.Context.ClipboardEditing.CanPaste(target), Is.False);
            Assert.Throws<InvalidOperationException>(() => ActionEditorHost.Context.ClipboardEditing.Paste(target, 7));
            Assert.That(ActionEditorHost.Context.Clipboard.IsCut, Is.True); Assert.That(track.Clips.Count, Is.EqualTo(3));
            track.IsLocked = false;
            ActionEditorHost.Context.ClipboardEditing.Paste(target, 7);
            Assert.That(track.Clips.Count, Is.EqualTo(2)); Assert.That(target.Clips.Count, Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(3));
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1].Clips.Count, Is.EqualTo(2));
            track = ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0];
            ActionEditorHost.Context.Structure.Deletion(track.Clips[0], track.Clips[1]).Execute();
            Assert.That(track.Clips.Count, Is.EqualTo(1));
            Undo.PerformUndo(); Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Count, Is.EqualTo(3));
        }

        [Test] public void MultipleEditsHaveIndependentUndoStepsAndFailureRollsBack()
        {
            ActionEditorHost.Context.Session.Edit("one", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 2);
            ActionEditorHost.Context.Session.Edit("two", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 3);
            Undo.PerformUndo(); Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(2));
            Undo.PerformUndo(); Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => ActionEditorHost.Context.Session.Edit("failed", () => { ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 100; throw new InvalidOperationException(); }));
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
        }

        [Test] public void InvalidTimeRollsBackAndProtectedPathsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => ActionEditorHost.Context.Session.Edit("invalid", () => ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = float.NaN));
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.Throws<ArgumentException>(() => JsonDocumentRepository.ValidatePath("Assets/CombatEditor/Imgui.json"));
            Assert.Throws<ArgumentException>(() => JsonDocumentRepository.ValidatePath("Assets/../Imgui.json"));
        }

        [Test] public void NestedCommandsStayInOneUndoAndRangeCanBeShorterThanDocument()
        {
            ActionEditorHost.Context.Session.BeginEdit();
            ActionEditorHost.Context.Structure.Rename(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0], "nested");
            Assert.That(ActionEditorHost.Context.Session.IsEditing, Is.True);
            ActionEditorHost.Context.Document.RangeMin = 1; ActionEditorHost.Context.Document.RangeMax = 2;
            Assert.That(ActionEditorHost.Context.Document.RangeMax, Is.EqualTo(2));
            ActionEditorHost.Context.Session.EndEdit("outer"); Undo.PerformUndo();
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.Not.EqualTo("nested"));
            Assert.That(ActionEditorHost.Context.Document.RangeMin, Is.Zero);
        }

        [Test] public void TransactionTokensRejectOtherOwnersAndStaleTokensAndCommitOneUndo()
        {
            var session = ActionEditorHost.Context.Session; var first = new object(); var other = new object();
            var token = session.BeginEdit(first);
            Assert.That(token, Is.Not.Null);
            Assert.That(session.BeginEdit(first), Is.SameAs(token));
            Assert.That(session.BeginEdit(other), Is.Null);
            ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 5;
            session.EndEdit(); session.CancelEdit();
            Assert.That(session.IsEditing, Is.True, "Ownerless callers cannot finish another owner's transaction.");
            ActionEditorHost.Context.Structure.Rename(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0], "nested-owned");
            Assert.That(token.IsActive, Is.True);
            token.Commit("Owned gesture"); token.Commit(); token.Cancel();
            Assert.That(session.IsEditing, Is.False);
            Undo.PerformUndo();
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.Not.EqualTo("nested-owned"));
            Undo.PerformRedo();
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(5));
            var next = session.BeginEdit(other);
            ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 6;
            token.Cancel(); token.Commit();
            Assert.That(next.IsActive, Is.True);
            next.Cancel(); next.Cancel();
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(5));
            Assert.That(next.IsActive, Is.False);
            Assert.That(next.Update(() => Assert.Fail("A stale token must not execute a mutation.")), Is.False);
        }

        [Test] public void OwnedTransactionsRollbackNestedFailuresAndDoNotAddEmptyUndoSteps()
        {
            var token = ActionEditorHost.Context.Session.BeginEdit(this);
            ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 5;
            Assert.Throws<InvalidOperationException>(() => ActionEditorHost.Context.Session.Edit("Nested failure", () =>
            {
                ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 9;
                throw new InvalidOperationException("expected failure");
            }));
            Assert.That(token.IsActive, Is.False);
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
            ActionEditorHost.Context.Session.Edit("Real change", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 2);
            ActionEditorHost.Context.Session.BeginEdit(this).Commit("No change");
            Undo.PerformUndo();
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
            var stale = ActionEditorHost.Context.Session.BeginEdit(this);
            ActionEditorHost.Context.Open(fixture);
            Assert.That(stale.IsActive, Is.False);
            var active = ActionEditorHost.Context.Session.BeginEdit(this);
            stale.Cancel(); Assert.That(active.IsActive, Is.True);
            ActionEditorHost.Context.Session.DiscardChanges();
            Assert.That(active.IsActive, Is.False);
            var partial = ActionEditorHost.Context.Session.BeginEdit(this);
            Assert.Throws<InvalidOperationException>(() => partial.Update(() =>
            {
                ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 99;
                throw new InvalidOperationException("partial update");
            }));
            Assert.That(partial.IsActive, Is.False);
            Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
            var invalid = ActionEditorHost.Context.Session.BeginEdit(this);
            ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Add(null);
            Assert.Throws<System.IO.InvalidDataException>(() => invalid.Commit());
            Assert.That(invalid.IsActive, Is.False);
            Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.That(ActionEditorHost.Context.Session.IsDirty, Is.False);
        }
    }
}
