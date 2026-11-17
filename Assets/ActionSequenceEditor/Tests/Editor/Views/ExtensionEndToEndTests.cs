using ActionSequenceEditor.Infrastructure.Discovery;
using ActionSequenceEditor.Views.IMGUI.Properties;
using ActionSequenceEditor.Views.IMGUI.Properties.Standard;
using ImguiWindow = ActionSequenceEditor.Views.IMGUI.ActionSequenceEditorWindow;
using ToolkitWindow = ActionSequenceEditor.Views.UIToolkit.ActionSequenceEditorWindow;
using ToolkitCanvas = ActionSequenceEditor.Views.UIToolkit.TimelineCanvas;
using ToolkitProperties = ActionSequenceEditor.Views.UIToolkit.PropertyFieldsView;
using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Presentation.Timeline;
using ActionSequenceEditor.Model;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ActionSequenceEditor.Samples.FeatureSample;
using ActionSequenceEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Tests
{
    public sealed class ExtensionEndToEndTests
    {
        private TextAsset source, fixture;
        private string recovery;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private sealed class FieldInspector : PropertyInspectorBase
        {
            public void Draw(object value, string field)
            { if (Presenter == null) Bind(EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings)); FieldDefaultInspector(Presenter.CaptureInputs(value).Single(f => f.Name == field)); }
        }
        private sealed class FieldWindow : EditorWindow
        {
            public object Value;
            public string Field;
            public Rect Input;
            public Vector2 ContentOffset;
            public string InputTrace;
            private readonly FieldInspector inspector = new FieldInspector();
            private void OnGUI()
            {
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.KeyDown) InputTrace += Event.current.type + ":" + Event.current.keyCode + ":" + Event.current.mousePosition + ";";
                if (Value == null) return;
                ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                inspector.Draw(Value, Field);
                if (Event.current.type == EventType.Repaint) Input = GUILayoutUtility.GetLastRect();
            }
        }
        [SetUp] public void SetUp()
        {
            source = EditorFixture.Shared.Source; recovery = EditorFixture.Shared.Session.CaptureRecovery();
            fixture = new TextAsset(SequenceJsonCodec.Encode(new FeatureSampleSequence())); EditorFixture.Shared.Open(fixture);
        }
        [TearDown] public void TearDown()
        {
            EditorFixture.Shared.Session.CancelEdit(); EditorFixture.Shared.Playback.Stop(); EditorFixture.Shared.Open(source); EditorFixture.Shared.Session.RestoreRecovery(recovery);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
        private static ActionClip Last(int track) => EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[track].Clips.Last();
        private static int RowIndex(ToolkitCanvas canvas, IDirectable item) => canvas.State.Rows.ToList().FindIndex(row => row.Key == canvas.Presenter.KeyFor(item));
        private static void ImguiCreate(ActionTrack track, Type type)
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
            var state = window.Presenter.State;
            var row = state.Rows.Single(r => r.Key == window.Presenter.KeyFor(track));
            var x = state.ContentLeft + (6.5f - state.Minimum) * (state.Size.WindowWidth - state.ContentLeft) / (state.Maximum - state.Minimum);
            var menu = window.Timeline.ContextMenu(x, row.Bounds.Y + 13);
            var field = typeof(GenericMenu).GetField("m_MenuItems", Instance);
            var items = (IEnumerable)(field != null ? field.GetValue(menu) : typeof(GenericMenu).GetProperty("menuItems", Instance).GetValue(menu));
            var name = DirectableTypeInfo.For(type).MenuName;
            var item = items.Cast<object>().Single(value => ((GUIContent)value.GetType().GetField("content", Instance).GetValue(value)).text.EndsWith("/" + name));
            var action = (GenericMenu.MenuFunction)item.GetType().GetField("func", Instance).GetValue(item);
            Assert.That(action, Is.Not.Null); action();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
        private static IEnumerator NativeCreate(ToolkitWindow window, int track, Type type)
        {
            var canvas = window.Canvas; var target = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[track];
            var row = RowIndex(canvas, target);
            var name = DirectableTypeInfo.For(type).MenuName;
            yield return NativeMenuInput.Choose(window, new Vector2(canvas.X(6.5f), row * 28 + 13 - window.ScrollY), name);
            Assert.That(window.LastError, Is.Null);
        }
        private static void Key(VisualElement input, KeyCode code, char character = '\0', bool control = false)
        {
            using var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = code, character = character, control = control });
            e.target = input; input.SendEvent(e);
        }
        [UnityTest] public IEnumerator BothMenusAndPropertyControlsProduceEquivalentPointAndIntervalGraphs()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            var original = ScriptableObject.CreateInstance<FieldWindow>();
            string expected = null;
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show();
                original.position = new Rect(120, 120, 450, 200); original.Show();
                yield return null; yield return null;
                for (var ui = 0; ui < 2; ui++)
                {
                    EditorFixture.Shared.Open(fixture);
                    foreach (var track in new[] { 0, 2 })
                    {
                        var type = track == 0 ? typeof(FeatureSampleInterval) : typeof(FeatureSampleMarker);
                        if (ui == 0) ImguiCreate(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[track], type);
                        else yield return NativeCreate(window, track, type);
                        var clip = Last(track);
                        Assert.That(clip.GetType(), Is.EqualTo(type)); Assert.That(clip.StartTime, Is.EqualTo(6.5f).Within(.0001f));
                        var fieldName = track == 0 ? "Caption" : "Message";
                        var field = type.GetField(fieldName); var initial = (string)field.GetValue(clip);
                        if (ui == 0)
                        {
                            original.Value = clip; original.Field = fieldName; original.Focus(); original.Repaint(); yield return null; yield return null;
                            var point = original.ContentOffset + new Vector2(original.Input.xMax - 50, original.Input.center.y);
                            original.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                            original.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                            original.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, control = true });
                            original.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha7, character = '7' });
                            original.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\n' });
                            yield return null;
                        }
                        else
                        {
                            window.Focus(); window.Select(clip);
                            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                            var root = new VisualElement(); window.rootVisualElement.Add(root); new ToolkitProperties(presenter).DrawFields(root, presenter.CaptureInputs(clip), () => { }, 0);
                            var text = root.Query<TextField>().ToList().Single(f => f.label == (track == 0 ? "标题" : "消息"));
                            var input = text.Q("unity-text-input").Q<TextElement>(); input.Focus(); yield return null;
                            Key(input, KeyCode.A, control: true); Key(input, KeyCode.Alpha7, '7'); Key(input, KeyCode.Return, '\n');
                            window.Canvas.Focus(); yield return null; root.RemoveFromHierarchy();
                        }
                        Assert.That(field.GetValue(clip), Is.EqualTo("7"), "Real property control must change the authored field: UI=" + ui + ", track=" + track + ", input=" + original.Input + ", offset=" + original.ContentOffset + ", focus=" + EditorWindow.focusedWindow?.GetType().Name + ", hot=" + GUIUtility.hotControl + ", trace=" + original.InputTrace);
                        original.Value = null;
                        // 真实控件拥有自己的提交；宿主测试不再替它预开另一所有者的事务。
                        Undo.PerformUndo(); clip = Last(track); Assert.That(field.GetValue(clip), Is.EqualTo(initial));
                        Undo.PerformRedo(); Assert.That(field.GetValue(Last(track)), Is.EqualTo("7"));
                    }
                    var encoded = SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence);
                    var reopened = SequenceJsonCodec.Decode(encoded);
                    Assert.That(reopened.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Last(), Is.TypeOf<FeatureSampleInterval>());
                    Assert.That(reopened.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[2].Clips.Last().Length, Is.Zero);
                    if (ui == 0) expected = SequenceJsonCodec.GetContentKey(reopened);
                    else Assert.That(SequenceJsonCodec.GetContentKey(reopened), Is.EqualTo(expected));
                    original.Value = null;
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { original.Value = null; original.Close(); window.DiscardChanges(); window.Close(); }
        }
        private static void Pointer(VisualElement target, EventType type, Vector2 point)
        {
            var source = new Event { type = type, button = 0, shift = true, mousePosition = target.LocalToWorld(point) };
            if (type == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else if (type == EventType.MouseDrag) { using var e = PointerMoveEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else { using var e = PointerUpEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
        }
        [UnityTest] public IEnumerator BothUiSequencesKeepOneGraphThroughCopyDragResizeCancelReopenAndPlayback()
        {

            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            TextAsset reopened = null;
            try
            {
                original.position = new Rect(60, 60, 1000, 600); original.Show();
                native.Show(); native.position = new Rect(50, 50, 1000, 600);
                yield return null; yield return null;
                for (var ui = 0; ui < 2; ui++)
                {
                    EditorFixture.Shared.Open(fixture);
                    var type = typeof(FeatureSampleInterval);
                    if (ui == 0) ImguiCreate(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0], type);
                    else NativeCreate(native, 0, type);
                    var created = Last(0); EditorFixture.Shared.ClipboardEditing.Copy(created);
                    var copy = (ActionClip)EditorFixture.Shared.ClipboardEditing.Paste(created.Parent, 9);
                    Assert.That(copy, Is.Not.SameAs(created));
                    native.Workspace.ViewTimeMax = 12; original.Workspace.ViewTimeMax = 12; native.SyncView();
                    native.Select(created, copy); yield return null;
                    var start = created.StartTime; var spacing = copy.StartTime - start;
                    if (ui == 0)
                    {
                        original.Focus(); original.Repaint(); yield return null; yield return null;
                        var rect = original.Timeline.State.Clips.Single(c => c.Key == original.Presenter.KeyFor(created)).Bounds;
                        var down = new Vector2(rect.X + rect.Width * .5f, rect.Y + rect.Height * .5f + 40) + (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(original);
                        var scale = (original.position.width - original.Timeline.State.ContentLeft) / original.Workspace.ViewTime;
                        foreach (var kind in new[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp })
                            original.SendEvent(new Event { type = kind, button = 0, shift = true, mousePosition = down + (kind == EventType.MouseDown ? Vector2.zero : new Vector2(scale * .5f, 0)) });
                        Assert.That(original.Timeline.Error, Is.Null);
                    }
                    else
                    {
                        var canvas = native.Canvas; var down = canvas.ClipRect(created, 1).center;
                        Pointer(canvas, EventType.MouseDown, down);
                        Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .5f, 0));
                        Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * .5f, 0));
                    }
                    Assert.That(created.StartTime, Is.EqualTo(start + .5f).Within(.001));
                    Assert.That(copy.StartTime - created.StartTime, Is.EqualTo(spacing).Within(.001));
                    Undo.PerformUndo(); created = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8);
                    Assert.That(created.StartTime, Is.EqualTo(start).Within(.001));
                    Undo.PerformRedo(); created = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8);
                    native.Select(created); var length = created.Length;
                    if (ui == 0)
                    {
                        original.Focus(); original.Repaint(); yield return null; yield return null;
                        var rect = original.Timeline.State.Clips.Single(c => c.Key == original.Presenter.KeyFor(created)).Bounds;
                        var down = new Vector2(rect.Right - 2, rect.Y + rect.Height * .5f + 40) + (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(original);
                        var delta = new Vector2((original.position.width - original.Timeline.State.ContentLeft) / original.Workspace.ViewTime * .25f, 0);
                        original.SendEvent(new Event { type = EventType.MouseDown, button = 0, shift = true, mousePosition = down });
                        Assert.That(original.Presenter.Mode, Is.EqualTo(TimelineGestureMode.ResizeRight));
                        original.SendEvent(new Event { type = EventType.MouseDrag, button = 0, shift = true, mousePosition = down + delta });
                        original.SendEvent(new Event { type = EventType.MouseUp, button = 0, shift = true, mousePosition = down + delta });
                        Assert.That(original.Timeline.Error, Is.Null);
                    }
                    else
                    {
                        var canvas = native.Canvas; var rect = canvas.ClipRect(created, 1); var down = new Vector2(rect.xMax - 2, rect.center.y);
                        Pointer(canvas, EventType.MouseDown, down); Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .25f, 0));
                        Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * .25f, 0));
                    }
                    Assert.That(created.Length, Is.EqualTo(length + .25f).Within(.001));
                    var field = type.GetField("Caption"); var initial = field.GetValue(created);
                    var token = EditorFixture.Shared.Session.BeginEdit(this); new ActionSequenceEditor.Application.Editing.PropertyPath(EditorFixture.Shared.Properties, created).Field(field).Write("cancelled"); token.Cancel();
                    created = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8);
                    Assert.That(field.GetValue(created), Is.EqualTo(initial));
                    token = EditorFixture.Shared.Session.BeginEdit(this); new ActionSequenceEditor.Application.Editing.PropertyPath(EditorFixture.Shared.Properties, created).Field(field).Write("Edited caption"); token.Commit();
                    Undo.PerformUndo(); Undo.PerformRedo();
                    var encoded = SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence);
                    var expected = SequenceJsonCodec.GetContentKey(SequenceJsonCodec.Decode(encoded));
                    var invalid = new TextAsset("not-json");
                    try { Assert.Throws<System.IO.InvalidDataException>(() => EditorFixture.Shared.Open(invalid)); }
                    finally { UnityEngine.Object.DestroyImmediate(invalid); }
                    Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(expected));
                    EditorFixture.Shared.Session.DiscardChanges(); original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
                    EditorFixture.Shared.Open(null); reopened = new TextAsset(encoded); EditorFixture.Shared.Open(reopened);
                    original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                    native = ScriptableObject.CreateInstance<ToolkitWindow>(); native.Show(); yield return null;
                    Assert.That(original.Presenter.KeyFor(EditorFixture.Shared.Sequence.AllTracks.First().Clips.First()), Is.GreaterThan(0));
                    Assert.That(native.Canvas.Presenter.KeyFor(EditorFixture.Shared.Sequence.AllTracks.First().Clips.First()), Is.GreaterThan(0));
                    Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(expected));
                    Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8)).Caption, Is.EqualTo("Edited caption"));
                    var now = (float)EditorApplication.timeSinceStartup; EditorFixture.Shared.Playback.Play(now); EditorFixture.Shared.Playback.Update(now + .1f, 1);
                    Assert.That(EditorFixture.Shared.Playback.IsPlaying, Is.True); Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.GreaterThan(0));
                    EditorFixture.Shared.Playback.Stop(); Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.False); Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.Zero);
                    EditorFixture.Shared.Open(fixture); UnityEngine.Object.DestroyImmediate(reopened); reopened = null;
                }
            }
            finally
            {
                original.Presenter?.Cancel(); EditorFixture.Shared.Session.CancelEdit();
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();

                if (reopened != null) UnityEngine.Object.DestroyImmediate(reopened);
            }
        }

        [UnityTest] public IEnumerator StructureHeadersExposeCurrentInputAndControlBehavior()
        {

            ToolkitWindow native = null;
            ImguiWindow imgui = null;
            try
            {
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0);
                group.Tracks.Clear(); EditorFixture.Shared.Sequence.Init();
                native = ScriptableObject.CreateInstance<ToolkitWindow>();
                native.position = new Rect(60, 60, 1000, 600); native.Show(); native.Focus();
                yield return null; yield return null;
                var canvas = native.Canvas;
                var headers = canvas.Q("timeline-row-headers");
                var emptyHeader = headers.Children().First();
                Assert.That(emptyHeader.Query<Button>().ToList().Count, Is.EqualTo(1), "Empty group exposes only the menu button.");
                Assert.That(emptyHeader.Q("timeline-fold"), Is.Null);
                Assert.That(emptyHeader.Q("timeline-fold-placeholder").pickingMode, Is.EqualTo(PickingMode.Ignore));
                Pointer(canvas, EventType.MouseDown, new Vector2(90, 13));
                Pointer(canvas, EventType.MouseDrag, new Vector2(90, 90));
                Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                Pointer(canvas, EventType.MouseUp, new Vector2(90, 90));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1), Is.SameAs(group));
                native.DiscardChanges(); native.Close(); native = null;

                group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0);
                group.Tracks.Clear(); EditorFixture.Shared.Sequence.Init();
                imgui = ScriptableObject.CreateInstance<ImguiWindow>();
                imgui.position = new Rect(60, 60, 1000, 600); imgui.Show(); imgui.Focus(); imgui.Repaint();
                yield return null; yield return null; yield return null;
                var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(imgui);
                var start = offset + new Vector2(90, Styles.PlayControlHeight + 13);
                var end = start + new Vector2(0, 70);
                imgui.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(group));
                imgui.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(imgui.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                Assert.That(imgui.Timeline.State.Insertion.Visible, Is.True);
                imgui.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                imgui.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0);
                var wasCollapsed = imgui.Workspace.IsCollapsed(group);
                var fold = offset + new Vector2(12, Styles.PlayControlHeight + 12);
                imgui.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = fold });
                imgui.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = fold });
                Assert.That(imgui.Workspace.IsCollapsed(group), Is.EqualTo(wasCollapsed), "Empty fold placeholder must stay passive.");
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                if (imgui != null) { imgui.Presenter.Cancel(); imgui.DiscardChanges(); imgui.Close(); }
            }
        }

        private static Texture2D ReadWindowPixels(EditorWindow window)
        {
            var host = typeof(EditorWindow).GetField("m_Parent", Instance).GetValue(window);
            var scale = EditorGUIUtility.pixelsPerPoint;
            var raw = RenderTexture.GetTemporary(Mathf.RoundToInt(window.position.width * scale), Mathf.RoundToInt(window.position.height * scale), 0);
            var upright = RenderTexture.GetTemporary(raw.descriptor);
            var previous = RenderTexture.active;
            var image = new Texture2D(raw.width, raw.height, TextureFormat.RGB24, false);
            try
            {
                host.GetType().GetMethod("GrabPixels", Instance).Invoke(host, new object[] { raw, new Rect(0, 0, raw.width, raw.height) });
                Graphics.Blit(raw, upright, EditorGUIUtility.Load("SceneView/BlitSceneViewCapture.mat") as Material);
                RenderTexture.active = upright;
                image.ReadPixels(new Rect(0, 0, upright.width, upright.height), 0, 0); image.Apply();
                return image;
            }
            catch { UnityEngine.Object.DestroyImmediate(image); throw; }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(upright); RenderTexture.ReleaseTemporary(raw);
            }
        }

        [UnityTest] public IEnumerator ToolkitStructureInsertionPaintCoversFullBodyWithoutText()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            Texture2D image = null;
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus();
                yield return null; yield return null;
                var canvas = window.Canvas;
                Pointer(canvas, EventType.MouseDown, new Vector2(90, 69));
                Pointer(canvas, EventType.MouseDrag, new Vector2(90, 31));
                var drop = canvas.State.Insertion;
                Assert.That(drop.Visible && drop.Valid, Is.True);
                Assert.That(canvas.Q("timeline-insertion-overlay").Query<Label>().ToList(), Is.Empty);
                window.Repaint(); yield return null; yield return null;
                image = ReadWindowPixels(window);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/structure-insertion-baseline.png"), image.EncodeToPNG());
                Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                var scale = EditorGUIUtility.pixelsPerPoint;
                bool BlueAt(float x)
                {
                    var px = Mathf.Clamp(Mathf.RoundToInt(x * scale), 0, image.width - 1);
                    var y = Mathf.RoundToInt((canvas.worldBound.yMin - window.rootVisualElement.worldBound.yMin + drop.Y) * scale);
                    for (var delta = -3; delta <= 3; delta++)
                    {
                        var c = image.GetPixel(px, Mathf.Clamp(image.height - 1 - y + delta, 0, image.height - 1));
                        if (c.b - c.r > .2f && c.g - c.r > .1f) return true;
                    }
                    return false;
                }
                Assert.That(BlueAt(60), Is.True, "Actual paint must contain the insertion line in the title area: bounds=" + canvas.worldBound + ", root=" + window.rootVisualElement.worldBound + ", dropY=" + drop.Y + ", scale=" + scale);
                Assert.That(BlueAt(canvas.Left + 200), Is.True, "Actual overlay must extend through the time area.");
                Assert.That(BlueAt(canvas.contentRect.width - 4), Is.True, "Actual overlay must reach the drawable right edge.");
                Pointer(canvas, EventType.MouseUp, new Vector2(-10, 31));
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                window.Canvas?.Cancel(); window.DiscardChanges(); window.Close();
            }
        }

        [UnityTest] public IEnumerator ImguiStructureInsertionPaintCoversFullBodyWithoutText()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            Texture2D image = null;
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null; yield return null;
                var offset = ((Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(window)).y;
                var pitch = Styles.LineHeight + Styles.Space;
                var start = new Vector2(90, offset + Styles.PlayControlHeight + 2 * pitch + 13);
                var end = new Vector2(90, offset + Styles.PlayControlHeight + pitch + 3);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + Vector2.down * 4 });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                Assert.That(window.Timeline.State.Insertion.Visible, Is.True);
                Assert.That(window.Timeline.State.Insertion.Valid, Is.True);
                window.Repaint(); yield return null; yield return null;
                image = ReadWindowPixels(window);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/structure-insertion-imgui.png"), image.EncodeToPNG());
                var scale = EditorGUIUtility.pixelsPerPoint;
                var y = Mathf.RoundToInt((Styles.PlayControlHeight + window.Timeline.State.Insertion.Y) * scale);
                bool BlueAt(float x)
                {
                    var px = Mathf.Clamp(Mathf.RoundToInt(x * scale), 0, image.width - 1);
                    for (var delta = -4; delta <= 4; delta++)
                    {
                        var c = image.GetPixel(px, Mathf.Clamp(image.height - 1 - y + delta, 0, image.height - 1));
                        if (c.b - c.r > .2f && c.g - c.r > .1f) return true;
                    }
                    return false;
                }
                Assert.That(BlueAt(60), Is.True, "Actual IMGUI title paint must contain the insertion line.");
                Assert.That(BlueAt(window.Timeline.State.ContentLeft + 200), Is.True);
                Assert.That(BlueAt(window.position.width - 4), Is.True);
                Assert.That(window.Timeline.State.Insertion.Width, Is.EqualTo(window.position.width));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                window.Presenter.Cancel(); window.DiscardChanges(); window.Close();
            }
        }

        [UnityTest] public IEnumerator ToolkitTrackHeaderCanMoveUpAndDetachWithoutLosingClips()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas;
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0); var moving = group.Tracks[1]; var first = group.Tracks[0];
                var start = new Vector2(90, RowIndex(canvas, moving) * 28 + 13);
                var end = new Vector2(90, RowIndex(canvas, first) * 28 + 3);
                Pointer(canvas, EventType.MouseDown, start);
                Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure), "Header press must start structure capture.");
                Pointer(canvas, EventType.MouseDrag, end);
                var drop = canvas.State.Insertion;
                Assert.That(drop.Visible && drop.Valid, Is.True, "Drop target missing: start=" + start + ", end=" + end + ", bounds=" + canvas.contentRect);
                Assert.That(drop.Y, Is.EqualTo(28));
                Assert.That(group.Tracks[1], Is.SameAs(moving), "Dragging must not commit before release.");
                Pointer(canvas, EventType.MouseUp, end);
                Assert.That(window.LastError, Is.Null);
                Assert.That(group.Tracks[0], Is.SameAs(moving), "Dropping on the upper half must insert before the target.");
                Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(first.Name));
                Undo.PerformRedo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(moving.Name));
                moving = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0];
                start = new Vector2(90, RowIndex(canvas, moving) * 28 + 13);
                end = new Vector2(5, start.y + 10);
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                Pointer(canvas, EventType.MouseUp, end);
                Assert.That(window.LastError, Is.Null); Assert.That(moving.Group, Is.Null);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Single(), Is.SameAs(moving));
                Assert.That(moving.Root, Is.SameAs(EditorFixture.Shared.Sequence)); Assert.That(moving.Clips, Is.Not.Empty);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers[1], Is.SameAs(moving), "Root zone inserts after the whole first group block.");
                start = new Vector2(90, RowIndex(canvas, moving) * 28 + 13);
                end = new Vector2(90, RowIndex(canvas, EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1)) * 28 + 13);
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                Key(canvas, KeyCode.Escape); Pointer(canvas, EventType.MouseUp, end);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Single(), Is.SameAs(moving));
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                var outside = new Vector2(-10, end.y);
                Pointer(canvas, EventType.MouseUp, outside);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Single(), Is.SameAs(moving));
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                Pointer(canvas, EventType.MouseUp, end);
                Assert.That(window.LastError, Is.Null); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>(), Is.Empty);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1).Tracks[0], Is.SameAs(moving));
                var encoded = SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence);
                Assert.That(SequenceJsonCodec.GetContentKey(SequenceJsonCodec.Decode(encoded)), Is.EqualTo(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence)));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiTrackHeadersReorderDetachRejoinAndCancelThroughWindowInput()
        {
            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.Workspace.ScrollY = 0;
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null; yield return null;
                var moving = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1];
                var name = moving.Name;
                var pitch = Styles.LineHeight + Styles.Space;
                var offset = Styles.PlayControlHeight + ((Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(window)).y;
                var start = new Vector2(90, offset + 2 * pitch + 13);
                var end = new Vector2(90, offset + pitch + 3);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(moving), "Header event must reach the intended row; offset=" + offset);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + Vector2.down * 4 });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Timeline.State.Insertion.Visible && window.Timeline.State.Insertion.Valid, Is.True);
                Assert.That(window.Timeline.State.Insertion.Y, Is.EqualTo(pitch));
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle), "Release must end the captured gesture.");
                Assert.That(window.Timeline.State.Insertion.Visible, Is.False);
                Assert.That(window.Timeline.Error, Is.Null);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(name));
                Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1].Name, Is.EqualTo(name));
                Undo.PerformRedo();
                window.Repaint(); yield return null; yield return null;
                start = new Vector2(90, offset + pitch + 13); end = new Vector2(5, start.y + 10);
                foreach (var type in new[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp })
                    window.SendEvent(new Event { type = type, button = 0, mousePosition = type == EventType.MouseDown ? start : end });
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Single().Name, Is.EqualTo(name));
                window.Presenter.Scroll(0);
                window.Repaint(); yield return null; yield return null;
                var rootRow = window.Presenter.State.Rows.Single(row => row.Key == window.Presenter.KeyFor(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().First()));
                var groupRow = window.Presenter.State.Rows.Single(row => row.Key == window.Presenter.KeyFor(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1)));
                start = new Vector2(90, offset + rootRow.Bounds.Y + 13);
                end = new Vector2(90, offset + groupRow.Bounds.Y + 13);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().ElementAt(0)), "Root row start=" + start + ", scroll=" + window.Workspace.ScrollY);
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Single().Name, Is.EqualTo(name));
                window.Repaint(); yield return null; yield return null;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().ElementAt(0)), "Root row after cancel start=" + start);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>(), Is.Empty);
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1).Tracks[0].Name, Is.EqualTo(name));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [Test] public void RootTracksPersistUndoPreviewAndRejectDuplicateOwnership()
        {
            var track = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            EditorFixture.Shared.Structure.MoveTrack(track, null, 0);
            Assert.That(track.Parent, Is.Null); Assert.That(track.Root, Is.SameAs(EditorFixture.Shared.Sequence));
            Assert.That(track.IsActive, Is.True); Assert.That(clip.Parent, Is.SameAs(track));
            var layout = new TimelineLayout();
            layout.Build(EditorFixture.Shared.Sequence, new ActionSequenceEditor.Application.Editing.SelectionState(), new TimelineWorkspace(), new TimelineSize(1000, 600), 0, null);
            Assert.That(layout.KeyFor(clip), Is.Not.Zero);
            var encoded = SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence);
            var decoded = SequenceJsonCodec.Decode(encoded);
            Assert.That(decoded.RootContainers.OfType<ActionTrack>().Single().Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.That(decoded.RootContainers.OfType<ActionTrack>().ElementAt(0).Root, Is.SameAs(decoded));
            EditorFixture.Shared.Sampler.Sample(.75f); Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.True); EditorFixture.Shared.Playback.Stop();
            Undo.PerformUndo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>(), Is.Empty);
            Undo.PerformRedo(); Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Count(), Is.EqualTo(1));
            var moved = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().ElementAt(0); moved.IsLocked = true;
            Assert.That(EditorFixture.Shared.Structure.MoveOperation(moved, EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0), 0).CanExecute(out _), Is.False);
            moved.IsLocked = false;
            EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks.Add(moved);
            Assert.Throws<System.IO.InvalidDataException>(() => SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence));
            EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks.Remove(moved);
        }

        [Test] public void RootTrackOrderingCopyCutLocksAndRecoveryKeepOneOwner()
        {
            var first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]; var second = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1];
            EditorFixture.Shared.Structure.MoveTrack(first, null, 0); EditorFixture.Shared.Structure.MoveTrack(second, null, 1);
            EditorFixture.Shared.Structure.MoveTrack(second, null, 0);
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>(), Is.EqualTo(new[] { second, first }));
            EditorFixture.Shared.Structure.MoveTrack(second, null, 2);
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>(), Is.EqualTo(new[] { first, second }));
            EditorFixture.Shared.ClipboardEditing.Copy(first, true); first.IsLocked = true;
            Assert.That(EditorFixture.Shared.ClipboardEditing.CanPaste(null), Is.False);
            Assert.Throws<InvalidOperationException>(() => EditorFixture.Shared.ClipboardEditing.Paste(null));
            first.IsLocked = false; EditorFixture.Shared.ClipboardEditing.Duplicate(first);
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Count(), Is.EqualTo(3));
            Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Last().Clips[0], Is.Not.SameAs(first.Clips[0]));
            var restored = EditorFixture.Shared.Session.CaptureRecovery(); var expected = SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence);
            EditorFixture.Shared.Structure.Delete(second); EditorFixture.Shared.Session.RestoreRecovery(restored);
            Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(expected));
            var lockedGroup = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1); lockedGroup.IsLocked = true;
            Assert.That(EditorFixture.Shared.Structure.MoveOperation(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().ElementAt(0), lockedGroup, 0).CanExecute(out _), Is.False);
            lockedGroup.IsLocked = false;
            EditorFixture.Shared.Structure.MoveTrack(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().ElementAt(0), lockedGroup, 1);
            Assert.That(lockedGroup.Tracks[1].Group, Is.SameAs(lockedGroup));
        }

        [Test] public void RootTrackDiskSnapshotReopensWithClipsAndExactOrder()
        {
            var first = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]; var second = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[1];
            EditorFixture.Shared.Structure.MoveTrack(first, null, 0); EditorFixture.Shared.Structure.MoveTrack(second, null, 0);
            var expected = SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence);
            var directory = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/RootTrack-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "Document.json"); TextAsset reopened = null;
            try
            {
                new AtomicFileCommit().Save(path, () => System.Text.Encoding.UTF8.GetBytes(SequenceJsonCodec.CreateSaveSnapshot(EditorFixture.Shared.Sequence)),
                    bytes => Assert.That(SequenceJsonCodec.GetContentKey(SequenceJsonCodec.Decode(System.Text.Encoding.UTF8.GetString(bytes))), Is.EqualTo(expected)), createOnly: true);
                reopened = new TextAsset(System.IO.File.ReadAllText(path)); EditorFixture.Shared.Open(reopened);
                Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(expected));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().Select(t => t.Name), Is.EqualTo(new[] { second.Name, first.Name }));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().ElementAt(1).Clips[0].StartTime, Is.EqualTo(.25f));
                Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionTrack>().All(t => t.Parent == null && t.Root == EditorFixture.Shared.Sequence), Is.True);
            }
            finally
            {
                EditorFixture.Shared.Open(fixture);
                if (reopened != null) UnityEngine.Object.DestroyImmediate(reopened);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                System.IO.Directory.Delete(directory);
            }
        }

        [UnityTest] public IEnumerator BothRulersZoomAroundHoveredTimeWithoutAuthorChanges()
        {

            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            ToolkitWindow native = null;
            try
            {
                original.position = new Rect(60, 60, 1000, 600); original.Show(); original.Focus(); original.Repaint();
                yield return null; yield return null; yield return null;
                original.Workspace.SetRange(1, 8);
                var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(original);
                var fraction = 100 / (original.position.width - Styles.TimelineLeftTotalWidth);
                var anchor = 1 + 7 * fraction;
                original.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = new Vector2(Styles.TimelineLeftTotalWidth + 100, 30) + offset, delta = Vector2.up });
                Assert.That(original.Workspace.ViewTime, Is.EqualTo(EditorFixture.SharedSettings.Current.ScrollWheelZooms ? 8.575f : 7).Within(.001f));
                Assert.That(original.Workspace.ViewTimeMin + fraction * original.Workspace.ViewTime, Is.EqualTo(anchor).Within(.0001f));
                native = ScriptableObject.CreateInstance<ToolkitWindow>();
                native.position = new Rect(60, 60, 1000, 600); native.Show(); native.Focus();
                yield return null; yield return null;
                native.Workspace.SetRange(1, 8);
                var root = native.rootVisualElement; fraction = 100 / (root.contentRect.width - native.Canvas.Left); anchor = 1 + 7 * fraction;
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, mousePosition = root.LocalToWorld(new Vector2(native.Canvas.Left + 100, 30)), delta = Vector2.up }))
                { wheel.target = root; root.SendEvent(wheel); }
                Assert.That(native.Workspace.ViewTime, Is.EqualTo(EditorFixture.SharedSettings.Current.ScrollWheelZooms ? 8.575f : 7).Within(.001f));
                Assert.That(native.Workspace.ViewTimeMin + fraction * native.Workspace.ViewTime, Is.EqualTo(anchor).Within(.0001f));
                Assert.That(EditorFixture.Shared.Session.IsDirty || EditorFixture.Shared.Session.IsEditing, Is.False);
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                original.DiscardChanges(); original.Close();
            }
        }

        [UnityTest] public IEnumerator ToolkitEmptyFoldPlaceholderStaysPassiveAndTracksUndoRedoFirstChild()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().First(); group.Tracks.Clear(); EditorFixture.Shared.Sequence.Validate();
                window.position = new Rect(60, 60, 1000, 600); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas; var header = canvas.Q("timeline-row-headers").Children().First();
                Assert.That(header.Q<Button>("timeline-fold"), Is.Null);
                Pointer(canvas, EventType.MouseDown, new Vector2(12, 13)); Pointer(canvas, EventType.MouseUp, new Vector2(12, 13));
                Assert.That(window.Workspace.IsCollapsed(group), Is.False);
                EditorFixture.Shared.Structure.AddTrack(group, typeof(FeatureSampleIntervalTrack)); yield return null; yield return null;
                header = canvas.Q("timeline-row-headers").Children().First();
                var fold = header.Q<Button>("timeline-fold"); Assert.That(fold, Is.Not.Null);
                var point = fold.contentRect.center;
                using (var down = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = fold.LocalToWorld(point) }))
                { down.target = fold; fold.SendEvent(down); }
                using (var up = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = fold.LocalToWorld(point) }))
                { up.target = fold; fold.SendEvent(up); }
                yield return null;
                Assert.That(window.Workspace.IsCollapsed(group), Is.True); Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Undo.PerformUndo(); yield return null;
                Assert.That(canvas.Q("timeline-row-headers").Children().First().Q<Button>("timeline-fold"), Is.Null);
                Undo.PerformRedo(); yield return null;
                Assert.That(canvas.Q("timeline-row-headers").Children().First().Q<Button>("timeline-fold"), Is.Not.Null);
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ToolkitScrolledGroupDragRecalculatesOverlayAtViewportEdgesAndCancelsOnUndo()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.position = new Rect(60, 60, 800, 250); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas; canvas.Presenter.Scroll(56); yield return null;
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1);
                var row = canvas.State.Rows.Single(value => value.Key == canvas.Presenter.KeyFor(group));
                var before = SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence);
                Pointer(canvas, EventType.MouseDown, new Vector2(80, row.Bounds.Y + 13));
                Pointer(canvas, EventType.MouseDrag, new Vector2(80, 12));
                var scroll = window.ScrollY; canvas.Tick(.1);
                Assert.That(window.ScrollY, Is.EqualTo(Mathf.Max(0, scroll - 15)).Within(.001f));
                Assert.That(canvas.State.Insertion.Width, Is.EqualTo(canvas.contentRect.width));
                Pointer(canvas, EventType.MouseDrag, new Vector2(-1, 12));
                Assert.That(canvas.State.Insertion.Visible, Is.False);
                Key(canvas, KeyCode.Escape);
                Assert.That(canvas.PointerId, Is.EqualTo(-1)); Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(before));
                EditorFixture.Shared.Session.Edit("sentinel", () => group.Name = "sentinel");
                row = canvas.State.Rows.Single(value => value.Key == canvas.Presenter.KeyFor(group));
                Pointer(canvas, EventType.MouseDown, new Vector2(80, row.Bounds.Y + 13)); Pointer(canvas, EventType.MouseDrag, new Vector2(80, 70));
                Key(canvas, KeyCode.Z, control: true);
                Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle)); Assert.That(canvas.State.Insertion.Visible, Is.False);
                Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(before));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiEmptyFoldPlaceholderTracksFirstChildUndoRedoAndDoesNotCaptureButtons()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().First(); group.Tracks.Clear(); EditorFixture.Shared.Sequence.Validate();
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null;
                var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(window);
                var fold = offset + new Vector2(12, 53);
                foreach (var kind in new[] { EventType.MouseDown, EventType.MouseUp })
                    window.SendEvent(new Event { type = kind, button = 0, mousePosition = fold });
                Assert.That(window.Workspace.IsCollapsed(group), Is.False);
                Assert.That(window.Timeline.State.Rows.First().CanFold, Is.False);
                EditorFixture.Shared.Structure.AddTrack(group, typeof(FeatureSampleIntervalTrack)); window.Repaint();
                yield return null; yield return null;
                foreach (var kind in new[] { EventType.MouseDown, EventType.MouseUp })
                    window.SendEvent(new Event { type = kind, button = 0, mousePosition = fold });
                Assert.That(window.Workspace.IsCollapsed(group), Is.True);
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Undo.PerformUndo(); yield return null;
                Assert.That(window.Timeline.State.Rows.First().CanFold, Is.False);
                Undo.PerformRedo(); yield return null;
                Assert.That(window.Timeline.State.Rows.First().CanFold, Is.True);
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiScrolledGroupDragUpdatesEdgesAndCancelsOnUndo()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.position = new Rect(60, 60, 800, 250); window.Show(); window.Focus();
                yield return null; yield return null;
                window.Presenter.Scroll(56); window.Repaint(); yield return null; yield return null;
                var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", Instance).GetValue(window);
                var group = EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(1);
                var before = SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence);
                var row = window.Timeline.State.Rows.Single(r => r.Key == window.Presenter.KeyFor(group));
                var start = offset + new Vector2(80, row.Bounds.Y + 53);
                var end = offset + new Vector2(80, 52);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                var scroll = window.Workspace.ScrollY; window.Presenter.Tick(.1);
                Assert.That(window.Workspace.ScrollY, Is.EqualTo(Mathf.Max(0, scroll - 15)).Within(.001f));
                Assert.That(window.Timeline.State.Insertion.Width, Is.EqualTo(window.position.width));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = offset + new Vector2(-1, 52) });
                Assert.That(window.Timeline.State.Insertion.Visible, Is.False);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(before));
                EditorFixture.Shared.Session.Edit("sentinel", () => group.Name = "sentinel");
                row = window.Timeline.State.Rows.Single(r => r.Key == window.Presenter.KeyFor(group));
                start = offset + new Vector2(80, row.Bounds.Y + 53);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Z, control = true });
                Assert.That(window.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(window.Timeline.State.Insertion.Visible, Is.False);
                Assert.That(SequenceJsonCodec.GetContentKey(EditorFixture.Shared.Sequence), Is.EqualTo(before));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [Test] public void ExistingSampleUsesMetadataAndOptionalAdaptersWithoutBusinessBranches()
        {
            Assert.That(DirectableTypeInfo.For(typeof(FeatureSampleMarker)).IsSignal, Is.True);
            Assert.That(EditorExtensionFactory.CreateHeader(EditorFixture.Shared.Sequence.GetType()), Is.TypeOf<Samples.FeatureSampleHeader>());
            Assert.That(EditorExtensionFactory.CreateToolkitHeader(EditorFixture.Shared.Sequence.GetType()).CreateGUI(
                new EditorExtensionContext(new ActionSequenceEditor.Presentation.Presenters.ExtensionSequenceProjection(() => EditorFixture.Shared.Sequence, () => 0).Capture)), Is.Not.Null);
            Assert.That(EditorExtensionFactory.ResolveInspectorType(typeof(FeatureSampleMarker)), Is.EqualTo(typeof(ClipInspector)), "The shared clip inspector inherits generic authored fields.");
            Assert.That(ActionSequenceEditorTypeCatalog.CreatePreview(typeof(FeatureSampleMarker)), Is.Null, "The sample state view has no scene side effects.");
        }
    }
}
