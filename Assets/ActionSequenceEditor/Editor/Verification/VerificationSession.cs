using System;
using ActionSequenceEditor.Model;
using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Infrastructure.Serialization;
using UnityEngine;

namespace ActionSequenceEditor.Verification
{
    // 仅验证程序集使用反射观察组合根私有状态，不为断言恢复生产服务查询入口。
    public sealed class VerificationSession : IDisposable
    {
        public readonly Composition.EditorCompositionRoot Composition;
        public VerificationSession(ActionSequenceEditor.Application.Sequences.SequenceSession session, SequenceFileRepository repository = null)
            => Composition = new Composition.EditorCompositionRoot(session, repository);
        private VerificationSession(Composition.EditorCompositionRoot composition) => Composition = composition;
        public static VerificationSession Shared => new VerificationSession((Composition.EditorCompositionRoot)typeof(ActionSequenceEditorHost)
            .GetField("composition", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
        public static EditorViewRegistry SharedViews => (EditorViewRegistry)typeof(ActionSequenceEditorHost)
            .GetField("views", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        public static ActionSequenceEditor.Application.Settings.SettingsSession SharedSettings => (ActionSequenceEditor.Application.Settings.SettingsSession)typeof(ActionSequenceEditorHost)
            .GetField("settings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        private T Read<T>(string name) => (T)typeof(Composition.EditorCompositionRoot)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Composition);
        public ActionSequenceEditor.Application.Sequences.SequenceSession Session => Read<ActionSequenceEditor.Application.Sequences.SequenceSession>("session");
        public ActionSequence Sequence => Session.Sequence;
        public SequenceNavigation Navigation => Read<SequenceNavigation>("navigation");
        public TextAsset Source => Navigation.Source;
        public ActionSequenceEditor.Application.Sequences.SequenceLifecycle Lifecycle => Read<ActionSequenceEditor.Application.Sequences.SequenceLifecycle>("lifecycle");
        public ActionSequenceEditor.Application.Sequences.SequenceAutoSave AutoSave => Read<ActionSequenceEditor.Application.Sequences.SequenceAutoSave>("autoSave");
        public ActionSequenceEditor.Application.Editing.SelectionState Selection => Read<ActionSequenceEditor.Application.Editing.SelectionState>("selection");
        public ActionSequenceEditor.Application.Editing.ClipboardState Clipboard => Read<ActionSequenceEditor.Application.Editing.ClipboardState>("clipboard");
        public ActionSequenceEditor.Application.Editing.StructureEditing Structure => Read<ActionSequenceEditor.Application.Editing.StructureEditing>("structure");
        public ActionSequenceEditor.Application.Editing.NodeEditing Nodes => Read<ActionSequenceEditor.Application.Editing.NodeEditing>("nodes");
        public ActionSequenceEditor.Application.Editing.ClipboardEditing ClipboardEditing => Read<ActionSequenceEditor.Application.Editing.ClipboardEditing>("clipboardEditing");
        public ActionSequenceEditor.Application.Editing.PropertyEditing Properties => Read<ActionSequenceEditor.Application.Editing.PropertyEditing>("properties");
        public ActionSequenceEditor.Application.Editing.ClipEditing Clips => Read<ActionSequenceEditor.Application.Editing.ClipEditing>("clips");
        public ActionSequenceEditor.Application.Ports.IEditorClock Clock => Read<ActionSequenceEditor.Application.Ports.IEditorClock>("clock");
        public ActionSequenceEditor.Application.Playback.SequencePlayback Playback => Read<ActionSequenceEditor.Application.Playback.SequencePlayback>("playback");
        public ActionSequenceEditor.Infrastructure.Preview.SequenceSampler Sampler => Read<ActionSequenceEditor.Infrastructure.Preview.SequenceSampler>("sampler");
        public void Open(TextAsset value) => Navigation.Open(value);
        public ActionSequenceEditor.Presentation.Presenters.SequencePresenter CreateSequencePresenter(Func<bool, string> choose = null) => Composition.CreateSequencePresenter(choose);
        public ActionSequenceEditor.Presentation.Presenters.PropertyPresenter CreateProperties(ActionSequenceEditor.Application.Settings.SettingsSession settings) => Composition.CreateProperties(settings);
        public Composition.WindowBinding Bind(object owner, Action repaint, Action changed, EditorViewRegistry views,
            ActionSequenceEditor.Application.Settings.SettingsSession settings, Func<bool, string> choose = null)
            => Composition.CreateBinding(owner, repaint, changed, views, settings, choose);
        public void Dispose() => Composition.Dispose();
    }
}
