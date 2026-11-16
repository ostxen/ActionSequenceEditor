using System;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Infrastructure.Sequences;
using NBC.ActionEditor.Infrastructure.Serialization;
using UnityEngine;

namespace NBC.ActionEditor.Verification
{
    // 仅验证程序集使用反射观察组合根私有状态，不为断言恢复生产服务查询入口。
    public sealed class VerificationSession : IDisposable
    {
        public readonly Composition.EditorCompositionRoot Composition;
        public VerificationSession(NBC.ActionEditor.Application.Sequences.SequenceSession session, SequenceFileRepository repository = null)
            => Composition = new Composition.EditorCompositionRoot(session, repository);
        private VerificationSession(Composition.EditorCompositionRoot composition) => Composition = composition;
        public static VerificationSession Shared => new VerificationSession((Composition.EditorCompositionRoot)typeof(ActionEditorHost)
            .GetField("composition", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
        public static EditorViewRegistry SharedViews => (EditorViewRegistry)typeof(ActionEditorHost)
            .GetField("views", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        public static NBC.ActionEditor.Application.Settings.SettingsSession SharedSettings => (NBC.ActionEditor.Application.Settings.SettingsSession)typeof(ActionEditorHost)
            .GetField("settings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        private T Read<T>(string name) => (T)typeof(Composition.EditorCompositionRoot)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Composition);
        public NBC.ActionEditor.Application.Sequences.SequenceSession Session => Read<NBC.ActionEditor.Application.Sequences.SequenceSession>("session");
        public ActionSequence Sequence => Session.Sequence;
        public SequenceNavigation Navigation => Read<SequenceNavigation>("navigation");
        public TextAsset Source => Navigation.Source;
        public NBC.ActionEditor.Application.Sequences.SequenceLifecycle Lifecycle => Read<NBC.ActionEditor.Application.Sequences.SequenceLifecycle>("lifecycle");
        public NBC.ActionEditor.Application.Sequences.SequenceAutoSave AutoSave => Read<NBC.ActionEditor.Application.Sequences.SequenceAutoSave>("autoSave");
        public NBC.ActionEditor.Application.Editing.SelectionState Selection => Read<NBC.ActionEditor.Application.Editing.SelectionState>("selection");
        public NBC.ActionEditor.Application.Editing.ClipboardState Clipboard => Read<NBC.ActionEditor.Application.Editing.ClipboardState>("clipboard");
        public NBC.ActionEditor.Application.Editing.StructureEditing Structure => Read<NBC.ActionEditor.Application.Editing.StructureEditing>("structure");
        public NBC.ActionEditor.Application.Editing.NodeEditing Nodes => Read<NBC.ActionEditor.Application.Editing.NodeEditing>("nodes");
        public NBC.ActionEditor.Application.Editing.ClipboardEditing ClipboardEditing => Read<NBC.ActionEditor.Application.Editing.ClipboardEditing>("clipboardEditing");
        public NBC.ActionEditor.Application.Editing.PropertyEditing Properties => Read<NBC.ActionEditor.Application.Editing.PropertyEditing>("properties");
        public NBC.ActionEditor.Application.Editing.ClipEditing Clips => Read<NBC.ActionEditor.Application.Editing.ClipEditing>("clips");
        public NBC.ActionEditor.Application.Ports.IEditorClock Clock => Read<NBC.ActionEditor.Application.Ports.IEditorClock>("clock");
        public NBC.ActionEditor.Application.Playback.SequencePlayback Playback => Read<NBC.ActionEditor.Application.Playback.SequencePlayback>("playback");
        public NBC.ActionEditor.Infrastructure.Preview.SequenceSampler Sampler => Read<NBC.ActionEditor.Infrastructure.Preview.SequenceSampler>("sampler");
        public void Open(TextAsset value) => Navigation.Open(value);
        public NBC.ActionEditor.Presentation.Presenters.SequencePresenter CreateSequencePresenter(Func<bool, string> choose = null) => Composition.CreateSequencePresenter(choose);
        public NBC.ActionEditor.Presentation.Presenters.PropertyPresenter CreateProperties(NBC.ActionEditor.Application.Settings.SettingsSession settings) => Composition.CreateProperties(settings);
        public Composition.WindowBinding Bind(object owner, Action repaint, Action changed, EditorViewRegistry views,
            NBC.ActionEditor.Application.Settings.SettingsSession settings, Func<bool, string> choose = null)
            => Composition.CreateBinding(owner, repaint, changed, views, settings, choose);
        public void Dispose() => Composition.Dispose();
    }
}
