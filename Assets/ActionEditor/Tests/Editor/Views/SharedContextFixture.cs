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
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    // 仅测试使用反射观察组合根私有状态，不为断言恢复生产服务查询入口。
    public sealed class EditorFixture : IDisposable
    {
        public readonly Composition.EditorComposition Composition;
        public EditorFixture(NBC.ActionEditor.Application.Documents.DocumentSession session, JsonDocumentRepository repository = null)
            => Composition = new Composition.EditorComposition(session, repository);
        private EditorFixture(Composition.EditorComposition composition) => Composition = composition;
        public static EditorFixture Shared => new EditorFixture((Composition.EditorComposition)typeof(ActionEditorHost)
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
    public abstract class SharedContextFixture
    {
        protected static readonly TimelineWorkspace Workspace = new TimelineWorkspace();

        protected static Vector2 ImguiPoint(ImguiWindow window, TimelineClip clip, bool right = false)
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
            fixture = new TextAsset(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            EditorFixture.Shared.Open(fixture);
            Workspace.Bind(null); Workspace.Bind(EditorFixture.Shared.Document); Workspace.SetRange(0, 7);
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
