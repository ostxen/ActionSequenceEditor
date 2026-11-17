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
    internal static class NativeMenuInput
    {
        public static IEnumerator Choose(ToolkitWindow window, Vector2 local, string suffix)
        {
            window.Focus(); yield return null; yield return null;
            var canvas = window.Canvas;
            var path = canvas.Presenter.Menu(local.x, local.y).Single(entry => entry.Text.EndsWith("/" + suffix) || entry.Text == suffix).Text;
            using (var open = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 1, mousePosition = canvas.LocalToWorld(local) }))
            { open.target = canvas; canvas.SendEvent(open); }
            yield return null; yield return null;
            var parts = path.Split('/');
            for (var depth = 0; depth < parts.Length; depth++)
            {
                var dropdown = window.rootVisualElement.Q(className: GenericDropdownMenu.ussClassName);
                Assert.That(dropdown, Is.Not.Null, "native menu depth=" + depth + ", path=" + path);
                var label = dropdown.Query<Label>().ToList().Single(item => item.text == parts[depth] + (depth < parts.Length - 1 ? " ›" : ""));
                var row = label.parent;
                for (var frame = 0; frame < 30 && (float.IsNaN(row.worldBound.width) || row.worldBound.width <= 0); frame++)
                { window.Repaint(); yield return null; }
                Assert.That(row.worldBound.width, Is.GreaterThan(0), "menu row must complete layout");
                var point = row.worldBound.center;
                using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = point })) { move.target = row; row.SendEvent(move); }
                using (var choose = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = point })) { choose.target = row; row.SendEvent(choose); }
                yield return null; yield return null;
            }
        }
    }
    // 仅测试使用反射观察组合根私有状态，不为断言恢复生产服务查询入口。
    public sealed class EditorFixture : IDisposable
    {
        public readonly Composition.EditorCompositionRoot Composition;
        public EditorFixture(ActionSequenceEditor.Application.Sequences.SequenceSession session, SequenceFileRepository repository = null)
            => Composition = new Composition.EditorCompositionRoot(session, repository);
        private EditorFixture(Composition.EditorCompositionRoot composition) => Composition = composition;
        public static EditorFixture Shared => new EditorFixture((Composition.EditorCompositionRoot)typeof(ActionSequenceEditorHost)
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
    public abstract class SharedContextFixture
    {
        protected static readonly TimelineWorkspace Workspace = new TimelineWorkspace();

        protected static Vector2 ImguiPoint(ImguiWindow window, ActionClip clip, bool right = false)
        {
            var rect = window.Timeline.State.Clips.Single(c => c.Key == window.Presenter.KeyFor(clip)).Bounds;
            var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            return new Vector2(right ? rect.Right - 2 : rect.X + rect.Width * .5f, rect.Y + rect.Height * .5f + 40) + offset;
        }

        protected TextAsset previous, fixture;

        protected string recovery;

        [SetUp] public void Setup()
        {
            previous = EditorFixture.Shared.Source;
            recovery = EditorFixture.Shared.Session.CaptureRecovery();
            fixture = new TextAsset(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(new FeatureSampleSequence()));
            EditorFixture.Shared.Open(fixture);
            Workspace.Bind(null); Workspace.Bind(EditorFixture.Shared.Sequence); Workspace.SetRange(0, 7);
        }

        [TearDown] public void Teardown()
        {
            EditorFixture.Shared.Session.CancelEdit(); EditorFixture.Shared.Playback.Stop(); EditorFixture.Shared.Open(previous);
            EditorFixture.Shared.Session.RestoreRecovery(recovery);
            UnityEngine.Object.DestroyImmediate(fixture);
        }

        protected static void Pointer(VisualElement target, EventType type, Vector2 local, int button = 0, bool shift = false)
        {
            var point = target.LocalToWorld(local);
            var source = new Event { type = type, mousePosition = point, button = button, shift = shift };
            if (type == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else if (type == EventType.MouseDrag) { using var e = PointerMoveEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else { using var e = PointerUpEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
        }

        protected static void Capture(EditorWindow window, string name)
        {
            var host = typeof(EditorWindow).GetField("m_Parent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            var scale = EditorGUIUtility.pixelsPerPoint;
            var raw = RenderTexture.GetTemporary(Mathf.RoundToInt(window.position.width * scale), Mathf.RoundToInt(window.position.height * scale), 0);
            var upright = RenderTexture.GetTemporary(raw.descriptor); var previous = RenderTexture.active;
            var snapshot = new Texture2D(raw.width, raw.height, TextureFormat.RGB24, false);
            try
            {
                host.GetType().GetMethod("GrabPixels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(host, new object[] { raw, new Rect(0, 0, raw.width, raw.height) });
                Graphics.Blit(raw, upright, EditorGUIUtility.Load("SceneView/BlitSceneViewCapture.mat") as Material);
                RenderTexture.active = upright; snapshot.ReadPixels(new Rect(0, 0, upright.width, upright.height), 0, 0); snapshot.Apply();
                File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/" + name), snapshot.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(snapshot); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(upright); RenderTexture.ReleaseTemporary(raw); }
        }
    }
}
