using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEditor;
using EditorSettings = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Tests
{
    // 集合候选必须独立于作者图，预检失败和Undo都保留完整批次。
    public sealed class PropertyCollectionTransactionTests
    {
        [TestCase(true)] [TestCase(false)]
        public void BatchListChangesRecordOneUndoAndRestoreBothOriginalLists(bool append)
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            context.Session.Open(new DocumentOpenData(new FeatureSampleDocument()));
            var clips = context.Document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
            context.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            var presenter = new PropertyPresenter(context.Properties, context.Structure, context.Clips,
                () => new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/"), context.Selection);
            var path = presenter.Field(clips[0], typeof(FeatureSampleInterval).GetField("Parameters")).Path;
            var baseline = JsonDocumentCodec.Content(context.Document);
            var version = context.Session.Version;
            if (append) presenter.Append(path, typeof(List<FeatureSampleParameter>));
            else presenter.Remove(path, typeof(List<FeatureSampleParameter>), 0);
            Assert.That(context.Session.Version, Is.EqualTo(version + 1), "集合变更必须在事务快照之后发生。");
            Assert.That(clips.Select(c => c.Parameters.Count), Is.EqualTo(new[] { append ? 3 : 1, append ? 3 : 1 }));
            var changed = JsonDocumentCodec.Content(context.Document);
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Content(context.Document), Is.EqualTo(baseline));
            Undo.PerformRedo();
            Assert.That(JsonDocumentCodec.Content(context.Document), Is.EqualTo(changed));
        }

        [Test] public void RejectedBatchRemovalDoesNotMutateAnyEarlierList()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            var document = new FeatureSampleDocument();
            var clips = document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
            clips[1].Parameters.Clear();
            context.Session.Open(new DocumentOpenData(document));
            context.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            var presenter = new PropertyPresenter(context.Properties, context.Structure, context.Clips,
                () => new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/"), context.Selection);
            var path = presenter.Field(clips[0], typeof(FeatureSampleInterval).GetField("Parameters")).Path;
            var baseline = JsonDocumentCodec.Content(context.Document);
            var version = context.Session.Version;
            Assert.Throws<ArgumentOutOfRangeException>(() => presenter.Remove(path, typeof(List<FeatureSampleParameter>), 0));
            Assert.That(JsonDocumentCodec.Content(context.Document), Is.EqualTo(baseline));
            Assert.That(context.Session.Version, Is.EqualTo(version));
            Assert.That(context.Session.IsEditing || context.Session.IsDirty, Is.False);
        }
    }
}
