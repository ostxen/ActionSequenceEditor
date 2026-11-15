using System;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Infrastructure.Documents;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitCanvas = NBC.ActionEditor.Views.UIToolkit.TimelineCanvas;
using System.Linq;
using NBC.ActionEditor.Infrastructure.Serialization;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Verification
{
    // 仅验证程序集使用反射观察组合根私有状态，不为断言恢复生产服务查询入口。
    public sealed class VerificationSession : IDisposable
    {
        public readonly Composition.EditorComposition Composition;
        public VerificationSession(NBC.ActionEditor.Application.Documents.DocumentSession session, JsonDocumentRepository repository = null)
            => Composition = new Composition.EditorComposition(session, repository);
        private VerificationSession(Composition.EditorComposition composition) => Composition = composition;
        public static VerificationSession Shared => new VerificationSession((Composition.EditorComposition)typeof(ActionEditorHost)
            .GetField("composition", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null));
        public static ActionEditorViews SharedViews => (ActionEditorViews)typeof(ActionEditorHost)
            .GetField("views", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        public static NBC.ActionEditor.Application.Settings.SettingsSession SharedSettings => (NBC.ActionEditor.Application.Settings.SettingsSession)typeof(ActionEditorHost)
            .GetField("settings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        private T Read<T>(string name) => (T)typeof(Composition.EditorComposition)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Composition);
        public NBC.ActionEditor.Application.Documents.DocumentSession Session => Read<NBC.ActionEditor.Application.Documents.DocumentSession>("session");
        public ActionDocument Document => Session.Document;
        public DocumentNavigation Navigation => Read<DocumentNavigation>("navigation");
        public TextAsset Source => Navigation.Source;
        public NBC.ActionEditor.Application.Documents.DocumentLifecycle Lifecycle => Read<NBC.ActionEditor.Application.Documents.DocumentLifecycle>("lifecycle");
        public NBC.ActionEditor.Application.Documents.DocumentAutoSave AutoSave => Read<NBC.ActionEditor.Application.Documents.DocumentAutoSave>("autoSave");
        public NBC.ActionEditor.Application.Editing.SelectionState Selection => Read<NBC.ActionEditor.Application.Editing.SelectionState>("selection");
        public NBC.ActionEditor.Application.Editing.ClipboardState Clipboard => Read<NBC.ActionEditor.Application.Editing.ClipboardState>("clipboard");
        public NBC.ActionEditor.Application.Editing.StructureEditing Structure => Read<NBC.ActionEditor.Application.Editing.StructureEditing>("structure");
        public NBC.ActionEditor.Application.Editing.NodeEditing Nodes => Read<NBC.ActionEditor.Application.Editing.NodeEditing>("nodes");
        public NBC.ActionEditor.Application.Editing.ClipboardEditing ClipboardEditing => Read<NBC.ActionEditor.Application.Editing.ClipboardEditing>("clipboardEditing");
        public NBC.ActionEditor.Application.Editing.PropertyEditing Properties => Read<NBC.ActionEditor.Application.Editing.PropertyEditing>("properties");
        public NBC.ActionEditor.Application.Editing.ClipEditing Clips => Read<NBC.ActionEditor.Application.Editing.ClipEditing>("clips");
        public NBC.ActionEditor.Application.Ports.IEditorClock Clock => Read<NBC.ActionEditor.Application.Ports.IEditorClock>("clock");
        public NBC.ActionEditor.Application.Playback.DocumentPlayback Playback => Read<NBC.ActionEditor.Application.Playback.DocumentPlayback>("playback");
        public NBC.ActionEditor.Infrastructure.Preview.DocumentSampler Player => Read<NBC.ActionEditor.Infrastructure.Preview.DocumentSampler>("player");
        public void Open(TextAsset value) => Navigation.Open(value);
        public NBC.ActionEditor.Presentation.Presenters.DocumentPresenter CreateDocuments(Func<bool, string> choose = null) => Composition.CreateDocuments(choose);
        public NBC.ActionEditor.Presentation.Presenters.PropertyPresenter CreateProperties(NBC.ActionEditor.Application.Settings.SettingsSession settings) => Composition.CreateProperties(settings);
        public Composition.WindowBinding Bind(object owner, Action repaint, Action changed, ActionEditorViews views,
            NBC.ActionEditor.Application.Settings.SettingsSession settings, Func<bool, string> choose = null)
            => Composition.CreateBinding(owner, repaint, changed, views, settings, choose);
        public void Dispose() => Composition.Dispose();
    }

    // 只读诊断当前产品窗口和往返差异，不执行菜单或输出作者字段值。
    public static class WindowDiagnostics
    {
        [CliGroupCommand("actioneditor.verification", "Read-only current JSON roundtrip field paths only. Never returns field values or writes assets.")]
        public static object JsonStatus() => new { differences = JsonDocumentCodec.InspectReadbackDifferences(VerificationSession.Shared.Document) };

        [CliGroupCommand("actioneditor.verification", "Read-only product window types, focus and loaded menu bindings. No asset contents or UI actions.")]
        public static object WindowStatus()
        {
            var focused = EditorWindow.focusedWindow;
            return new
            {
                focusedType = focused == null ? null : focused.GetType().FullName,
                windows = Resources.FindObjectsOfTypeAll<EditorWindow>()
                    .Where(window => window is ImguiWindow || window is ToolkitWindow)
                    .Select(window => new
                    {
                        type = window.GetType().FullName, title = window.titleContent.text, focused = window == focused,
                        width = window.position.width, height = window.position.height,
                        nativeTimeline = window.rootVisualElement.Q<ToolkitCanvas>() != null,
                        imguiView = window is ImguiWindow imgui ? (!imgui.Documents.HasDocument ? "welcome" : "timeline") : null
                    }).ToArray(),
                menuBindings = TypeCache.GetMethodsWithAttribute<MenuItem>()
                    .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>()
                        .Where(item => item.menuItem == "NBC/Action Editor/IMGUI" || item.menuItem == "NBC/Action Editor/UI Toolkit")
                        .Select(item => new { menu = item.menuItem, validation = item.validate, type = method.DeclaringType.FullName, method = method.Name }))
                    .ToArray()
            };
        }
    }
}
