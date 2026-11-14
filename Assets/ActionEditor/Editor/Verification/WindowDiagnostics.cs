using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitCanvas = NBC.ActionEditor.Views.UIToolkit.TimelineCanvas;
using System.Linq;
using NBC.ActionEditor.Storage;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Verification
{
    // 只读诊断当前产品窗口和往返差异，不执行菜单或输出作者字段值。
    public static class WindowDiagnostics
    {
        [CliGroupCommand("actioneditor.verification", "Read-only current JSON roundtrip field paths only. Never returns field values or writes assets.")]
        public static object JsonStatus() => new { differences = JsonDocumentCodec.InspectReadbackDifferences(ActionEditorHost.Context.Document) };

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
                        imguiView = window is ImguiWindow imgui ? (imgui.Context.Document == null ? "welcome" : "timeline") : null
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
