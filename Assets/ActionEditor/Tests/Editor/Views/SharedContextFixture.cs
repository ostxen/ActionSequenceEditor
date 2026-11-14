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
            previous = ActionEditorHost.Context.Source;
            recovery = ActionEditorHost.Context.Session.CaptureRecovery();
            fixture = new TextAsset(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            ActionEditorHost.Context.Open(fixture);
            Workspace.Bind(null); Workspace.Bind(ActionEditorHost.Context.Document); Workspace.SetRange(0, 7);
        }

        [TearDown] public void Teardown()
        {
            ActionEditorHost.Context.Session.CancelEdit(); ActionEditorHost.Context.Playback.Stop(); ActionEditorHost.Context.Open(previous);
            ActionEditorHost.Context.Session.RestoreRecovery(recovery);
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
