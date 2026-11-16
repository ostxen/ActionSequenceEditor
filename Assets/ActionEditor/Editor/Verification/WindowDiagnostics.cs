using System;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Infrastructure.Sequences;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitCanvas = NBC.ActionEditor.Views.UIToolkit.TimelineCanvas;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBC.ActionEditor.Infrastructure.Serialization;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Verification
{

    // 只读诊断当前产品窗口和往返差异，不执行菜单或输出作者字段值。
    public static class WindowDiagnostics
    {
        [CliGroupCommand("actioneditor.verification", "Fixed existing Toolkit window menu font/layout probe. Opens and cancels menus only; restores font/focus; no author edits, files or assets.")]
        public static async Task<object> MenuLayoutProbe([CliArg("confirm", "Apply transient menu probe.")] bool confirm = false,
            [CliArg("dry-run", "Preview fixed transient UI actions.")] bool dryRun = false)
        {
            VerificationGuard.RequireReady();
            var window = Resources.FindObjectsOfTypeAll<ToolkitWindow>().SingleOrDefault();
            if (dryRun) return new { target = typeof(ToolkitWindow).FullName, operations = new[] { "open Toolkit window if absent and leave open", "focus window", "open/cancel track and save menus at 16/20/24 point owner fonts", "measure actual label/row/menu bounds", "restore owner fonts and previous focus" }, writes = false };
            if (!confirm) throw new InvalidOperationException("Preview and approval required.");
            if (window == null) { ToolkitWindow.Open(); window = Resources.FindObjectsOfTypeAll<ToolkitWindow>().Single(); await Frames(window); }
            var canvas = window.Canvas;
            if (canvas?.State == null || canvas.Presenter.Mode != Presentation.Timeline.TimelineGestureMode.Idle)
                throw new InvalidOperationException("Requires an existing idle Toolkit timeline.");
            if (window.rootVisualElement.Q(className: GenericDropdownMenu.ussClassName) != null)
                throw new InvalidOperationException("Close the existing menu before this probe.");
            var previousFocus = EditorWindow.focusedWindow;
            var save = window.rootVisualElement.Q("timeline-save");
            var previousCanvasFont = canvas.style.fontSize; var previousSaveFont = save.style.fontSize;
            var measurements = new List<object>();
            var row = canvas.State.Rows.First(item => !item.IsGroup);
            try
            {
                window.Focus();
                foreach (var font in new[] { 16, 20, 24 })
                {
                    canvas.style.fontSize = save.style.fontSize = font;
                    await Frames(window);
                    foreach (var isSave in new[] { false, true })
                    {
                        var target = isSave ? save : canvas;
                        var point = isSave ? save.worldBound.center : canvas.LocalToWorld(new Vector2(30, row.Bounds.Y + row.Bounds.Height * .5f));
                        using (var e = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 1, mousePosition = point }))
                        { e.target = target; target.SendEvent(e); }
                        await Frames(window);
                        var menu = window.rootVisualElement.Q(className: GenericDropdownMenu.ussClassName);
                        if (menu == null) throw new InvalidOperationException("Menu did not open.");
                        var labels = menu.Query<Label>(className: GenericDropdownMenu.labelUssClassName).ToList();
                        if (labels.Count == 0) throw new InvalidOperationException("Menu has no rows.");
                        foreach (var label in labels)
                        {
                            var measured = label.MeasureTextSize(label.text, label.contentRect.width, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);
                            if (Mathf.Abs(label.resolvedStyle.fontSize - font) > .1f || label.contentRect.height + 1 < measured.y
                                || label.contentRect.width + 1 < measured.x || label.parent.worldBound.height < font * 1.8f - 1)
                                throw new InvalidOperationException($"Menu clipped: font={font}/{label.resolvedStyle.fontSize}, text={measured.x}x{measured.y}, content={label.contentRect.width}x{label.contentRect.height}, row={label.parent.worldBound.width}x{label.parent.worldBound.height}.");
                        }
                        measurements.Add(new { menu = isSave ? "save" : "track", font, rows = labels.Count,
                            width = labels[0].parent.worldBound.width, rowHeight = labels[0].parent.worldBound.height });
                        CloseMenu(window);
                        await Frames(window);
                    }
                }
                return new { status = "passed", measurements };
            }
            finally
            {
                CloseMenu(window); canvas.style.fontSize = previousCanvasFont; save.style.fontSize = previousSaveFont;
                if (previousFocus != null) previousFocus.Focus();
            }
        }
        private static void CloseMenu(ToolkitWindow window)
        {
            var menu = window.rootVisualElement.Q(className: GenericDropdownMenu.ussClassName);
            if (menu == null) return;
            using var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            var input = menu.Q<ScrollView>().contentContainer;
            e.target = input; input.SendEvent(e);
        }
        private static Task Frames(ToolkitWindow window)
        {
            var completion = new TaskCompletionSource<bool>();
            var frames = 0; var deadline = EditorApplication.timeSinceStartup + 5;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (window == null || EditorApplication.timeSinceStartup > deadline)
                { EditorApplication.update -= tick; completion.TrySetException(new TimeoutException("Menu layout interrupted.")); return; }
                window.Repaint();
                if (++frames < 8) return;
                EditorApplication.update -= tick; completion.TrySetResult(true);
            };
            EditorApplication.update += tick;
            return completion.Task;
        }
        [CliGroupCommand("actioneditor.verification", "Read-only current JSON roundtrip field paths only. Never returns field values or writes assets.")]
        public static object JsonStatus() => new { differences = SequenceJsonCodec.InspectReadbackDifferences(VerificationSession.Shared.Sequence) };

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
                        imguiView = window is ImguiWindow imgui ? (!imgui.Sequence.HasSequence ? "welcome" : "timeline") : null
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
