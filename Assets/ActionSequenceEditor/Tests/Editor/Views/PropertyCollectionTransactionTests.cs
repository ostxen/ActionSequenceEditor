using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Application.Editing;
using ActionSequenceEditor.Application.Settings;
using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Presentation.Presenters;
using ActionSequenceEditor.Samples.FeatureSample;
using ActionSequenceEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using EditorSettings = ActionSequenceEditor.Application.Settings.EditorSettings;

namespace ActionSequenceEditor.Tests
{
    // 集合候选必须独立于作者图，预检失败和Undo都保留完整批次。
    public sealed class PropertyCollectionTransactionTests
    {
        [TestCase(true)] [TestCase(false)]
        public void BatchListChangesRecordOneUndoAndRestoreBothOriginalLists(bool append)
        {
            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            context.Session.Open(new SequenceOpenData(new FeatureSampleSequence()));
            var clips = context.Sequence.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
            context.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            var presenter = new PropertyPresenter(context.Properties, context.Nodes, context.Clips,
                () => new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/"), context.Selection);
            var field = presenter.CaptureSelectionInputs().Single(input => input.Name == "Parameters").Id;
            var baseline = SequenceJsonCodec.GetContentKey(context.Sequence);
            var version = context.Session.Version;
            if (append) presenter.Append(field);
            else presenter.Remove(field, 0);
            Assert.That(context.Session.Version, Is.EqualTo(version + 1), "集合变更必须在事务快照之后发生。");
            Assert.That(clips.Select(c => c.Parameters.Count), Is.EqualTo(new[] { append ? 3 : 1, append ? 3 : 1 }));
            var changed = SequenceJsonCodec.GetContentKey(context.Sequence);
            Undo.PerformUndo();
            Assert.That(SequenceJsonCodec.GetContentKey(context.Sequence), Is.EqualTo(baseline));
            Undo.PerformRedo();
            Assert.That(SequenceJsonCodec.GetContentKey(context.Sequence), Is.EqualTo(changed));
        }

        [Test] public void RejectedBatchRemovalDoesNotMutateAnyEarlierList()
        {
            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            var document = new FeatureSampleSequence();
            var clips = document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
            clips[1].Parameters.Clear();
            context.Session.Open(new SequenceOpenData(document));
            context.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            var presenter = new PropertyPresenter(context.Properties, context.Nodes, context.Clips,
                () => new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/"), context.Selection);
            var field = presenter.CaptureSelectionInputs().Single(input => input.Name == "Parameters").Id;
            var baseline = SequenceJsonCodec.GetContentKey(context.Sequence);
            var version = context.Session.Version;
            Assert.Throws<ArgumentOutOfRangeException>(() => presenter.Remove(field, 0));
            Assert.That(SequenceJsonCodec.GetContentKey(context.Sequence), Is.EqualTo(baseline));
            Assert.That(context.Session.Version, Is.EqualTo(version));
            Assert.That(context.Session.IsEditing || context.Session.IsDirty, Is.False);
        }
    }
}
