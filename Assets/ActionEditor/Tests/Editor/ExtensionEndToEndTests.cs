using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NBC.ActionEditor.Samples.LegacyExperience;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class ExtensionEndToEndTests
    {
        private TextAsset source, fixture;
        private string recovery;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private sealed class FieldInspector : InspectorsBase
        {
            public void Draw(object value, string field) => FieldDefaultInspector(value.GetType().GetField(field), value);
        }
        private sealed class FieldWindow : EditorWindow
        {
            public object Value;
            public string Field;
            public Rect Input;
            public Vector2 ContentOffset;
            public string InputTrace;
            public OriginalTimelineGesture Gesture;
            private readonly FieldInspector inspector = new FieldInspector();
            private void OnGUI()
            {
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.KeyDown) InputTrace += Event.current.type + ":" + Event.current.keyCode + ":" + Event.current.mousePosition + ";";
                if (Gesture != null)
                {
                    ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                    var pointer = new NBC.ActionEditor.Events.PointerEventData(Event.current);
                    if (Event.current.type == EventType.MouseDown) Gesture.OnBeginDrag(pointer);
                    else if (Event.current.type == EventType.MouseDrag) Gesture.OnDrag(pointer);
                    else if (Event.current.type == EventType.MouseUp) Gesture.OnEndDrag(pointer);
                }
                if (Value == null) return;
                ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                inspector.Draw(Value, Field);
                if (Event.current.type == EventType.Repaint) Input = GUILayoutUtility.GetLastRect();
            }
        }
        [SetUp] public void SetUp()
        {
            source = App.TextAsset; recovery = App.Session.CaptureRecovery();
            fixture = new TextAsset(JsonDocumentCodec.Encode(new LegacyExperienceAsset())); App.TextAsset = fixture;
        }
        [TearDown] public void TearDown()
        {
            App.Session.CancelEdit(); App.Stop(); App.TextAsset = source; App.Session.RestoreRecovery(recovery);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
        private static TimelineClip Last(int track) => App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[track].Clips.Last();
        private static int RowIndex(UIToolkitTimelineCanvas canvas, IDirectable item) => canvas.State.Rows.ToList().FindIndex(row => row.Key == canvas.Presenter.KeyFor(item));
        private static void OriginalCreate(TimelineTrack track, Type type)
        {
            var view = new TimelineTrackItemRightView();
            var menu = (GenericMenu)typeof(TimelineTrackItemRightView).GetMethod("BuildTrackContextMenu", Instance).Invoke(view, new object[] { track, track.Root.SnapTime(6.5f) });
            var field = typeof(GenericMenu).GetField("m_MenuItems", Instance);
            var items = (IEnumerable)(field != null ? field.GetValue(menu) : typeof(GenericMenu).GetProperty("menuItems", Instance).GetValue(menu));
            var name = DirectableTypeInfo.For(type).MenuName;
            var item = items.Cast<object>().Single(value => ((GUIContent)value.GetType().GetField("content", Instance).GetValue(value)).text.EndsWith("/" + name));
            var action = (GenericMenu.MenuFunction)item.GetType().GetField("func", Instance).GetValue(item);
            Assert.That(action, Is.Not.Null); action();
        }
        private static void NativeCreate(UIToolkitActionEditorWindow window, int track, Type type)
        {
            var canvas = window.Canvas; var target = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[track];
            var row = RowIndex(canvas, target);
            var position = canvas.LocalToWorld(new Vector2(canvas.X(6.5f), row * 28 + 13 - window.ScrollY));
            var menu = new DropdownMenu();
            using (var trigger = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = position, button = 1 }))
            using (var e = ContextualMenuPopulateEvent.GetPooled(trigger, menu, canvas, null))
                typeof(UIToolkitTimelineCanvas).GetMethod("Menu", Instance).Invoke(canvas, new object[] { e });
            var name = DirectableTypeInfo.For(type).MenuName;
            Assert.That(menu.MenuItems().OfType<DropdownMenuAction>().Any(item => item.name.EndsWith("/" + name)), Is.True,
                "track=" + track + ", row=" + row + ", scroll=" + window.ScrollY + ", position=" + position + ", canvas=" + canvas.worldBound + ", actions=" + string.Join(";", menu.MenuItems().OfType<DropdownMenuAction>().Select(item => item.name)));
            var action = menu.MenuItems().OfType<DropdownMenuAction>().Single(item => item.name.EndsWith("/" + name));
            action.Execute(); Assert.That(window.LastError, Is.Null);
        }
        private static void Key(VisualElement input, KeyCode code, char character = '\0', bool control = false)
        {
            using var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = code, character = character, control = control });
            e.target = input; input.SendEvent(e);
        }
        [UnityTest] public IEnumerator BothMenusAndPropertyControlsProduceEquivalentPointAndIntervalGraphs()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            var original = ScriptableObject.CreateInstance<FieldWindow>();
            string expected = null;
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show();
                original.position = new Rect(120, 120, 450, 200); original.Show();
                yield return null; yield return null;
                for (var ui = 0; ui < 2; ui++)
                {
                    App.TextAsset = fixture;
                    foreach (var track in new[] { 0, 2 })
                    {
                        var type = track == 0 ? typeof(LegacyExperienceInterval) : typeof(LegacyExperienceMarker);
                        if (ui == 0) OriginalCreate(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[track], type);
                        else NativeCreate(window, track, type);
                        var clip = Last(track);
                        Assert.That(clip.GetType(), Is.EqualTo(type)); Assert.That(clip.StartTime, Is.EqualTo(6.5f).Within(.0001f));
                        var fieldName = track == 0 ? "Caption" : "Message";
                        var field = type.GetField(fieldName); var initial = (string)field.GetValue(clip);
                        var token = App.Session.BeginEdit(this);
                        if (ui == 0)
                        {
                            original.Value = clip; original.Field = fieldName; original.Focus(); original.Repaint(); yield return null; yield return null;
                            var point = original.ContentOffset + new Vector2(original.Input.xMax - 50, original.Input.center.y);
                            original.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                            original.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                            original.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, control = true });
                            original.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha7, character = '7' });
                            yield return null;
                        }
                        else
                        {
                            window.Focus(); window.Select(clip);
                            var root = new VisualElement(); window.rootVisualElement.Add(root); UIToolkitPropertyView.DrawFields(root, clip, () => { }, 0);
                            var text = root.Query<TextField>().ToList().Single(f => f.label == (track == 0 ? "标题" : "消息"));
                            var input = text.Q("unity-text-input").Q<TextElement>(); input.Focus(); yield return null;
                            Key(input, KeyCode.A, control: true); Key(input, KeyCode.Alpha7, '7'); Key(input, KeyCode.Return, '\n');
                            window.Canvas.Focus(); yield return null; root.RemoveFromHierarchy();
                        }
                        Assert.That(field.GetValue(clip), Is.EqualTo("7"), "Real property control must change the authored field: UI=" + ui + ", track=" + track + ", input=" + original.Input + ", offset=" + original.ContentOffset + ", focus=" + EditorWindow.focusedWindow?.GetType().Name + ", hot=" + GUIUtility.hotControl + ", trace=" + original.InputTrace);
                        token.Cancel(); clip = Last(track); Assert.That(field.GetValue(clip), Is.EqualTo(initial));
                        token = App.Session.BeginEdit(this); new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, clip).Field(field).Write("7"); token.Commit();
                        Undo.PerformUndo(); Assert.That(field.GetValue(Last(track)), Is.EqualTo(initial));
                        Undo.PerformRedo(); Assert.That(field.GetValue(Last(track)), Is.EqualTo("7"));
                    }
                    var encoded = JsonDocumentCodec.CreateSaveSnapshot(App.AssetData);
                    var reopened = JsonDocumentCodec.Decode(encoded);
                    Assert.That(reopened.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Last(), Is.TypeOf<LegacyExperienceInterval>());
                    Assert.That(reopened.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[2].Clips.Last().Length, Is.Zero);
                    if (ui == 0) expected = JsonDocumentCodec.Content(reopened);
                    else Assert.That(JsonDocumentCodec.Content(reopened), Is.EqualTo(expected));
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
            var previousWindow = ActionEditorHost.Window; var previousWidth = ActionEditorHost.Width;
            var native = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            var events = ScriptableObject.CreateInstance<FieldWindow>();
            TextAsset reopened = null;
            try
            {
                original.Show(); native.Show(); native.position = new Rect(50, 50, 1000, 600);
                events.Show(); events.position = new Rect(100, 100, 1000, 200);
                yield return null; yield return null;
                for (var ui = 0; ui < 2; ui++)
                {
                    App.TextAsset = fixture;
                    var type = typeof(LegacyExperienceInterval);
                    if (ui == 0) OriginalCreate(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0], type);
                    else NativeCreate(native, 0, type);
                    var created = Last(0); App.Context.ClipboardEditing.Copy(created);
                    var copy = (TimelineClip)App.Context.ClipboardEditing.Paste(created.Parent, 9);
                    Assert.That(copy, Is.Not.SameAs(created));
                    native.Workspace.ViewTimeMax = 12; original.Workspace.ViewTimeMax = 12; native.SyncView();
                    native.Select(created, copy); yield return null;
                    var start = created.StartTime; var spacing = copy.StartTime - start;
                    if (ui == 0)
                    {
                        ActionEditorHost.Width = 700; events.Gesture = original.Gesture; events.Focus(); events.Repaint(); yield return null;
                        ClipDrawer.Reset(); ClipDrawer.GetDraw(created).ClipRealRect = new Rect(100, 20, 150, 26);
                        var down = new Vector2(Styles.TimelineLeftTotalWidth + 120, 30) + events.ContentOffset;
                        foreach (var kind in new[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp })
                            events.SendEvent(new Event { type = kind, button = 0, shift = true, mousePosition = down + (kind == EventType.MouseDown ? Vector2.zero : new Vector2(ActionEditorHost.Width * .5f / 12, 0)) });
                        events.Gesture = null;
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
                    Undo.PerformUndo(); created = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8);
                    Assert.That(created.StartTime, Is.EqualTo(start).Within(.001));
                    Undo.PerformRedo(); created = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8);
                    native.Select(created); var length = created.Length;
                    if (ui == 0)
                    {
                        events.Gesture = original.Gesture; events.Focus(); events.Repaint(); yield return null;
                        var down = new Vector2(Styles.TimelineLeftTotalWidth + original.Workspace.TimeToPos(created.EndTime, ActionEditorHost.Width), 30) + events.ContentOffset;
                        events.SendEvent(new Event { type = EventType.MouseDown, button = 0, shift = true, mousePosition = down });
                        Assert.That(original.Gesture.DragType, Is.EqualTo(ItemDragType.StretchEnd));
                        events.SendEvent(new Event { type = EventType.MouseDrag, button = 0, shift = true, mousePosition = down + new Vector2(ActionEditorHost.Width * .25f / 12, 0) });
                        events.SendEvent(new Event { type = EventType.MouseUp, button = 0, shift = true, mousePosition = down + new Vector2(ActionEditorHost.Width * .25f / 12, 0) });
                        events.Gesture = null;
                    }
                    else
                    {
                        var canvas = native.Canvas; var rect = canvas.ClipRect(created, 1); var down = new Vector2(rect.xMax - 2, rect.center.y);
                        Pointer(canvas, EventType.MouseDown, down); Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .25f, 0));
                        Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * .25f, 0));
                    }
                    Assert.That(created.Length, Is.EqualTo(length + .25f).Within(.001));
                    var field = type.GetField("Caption"); var initial = field.GetValue(created);
                    var token = App.Session.BeginEdit(this); new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, created).Field(field).Write("cancelled"); token.Cancel();
                    created = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8);
                    Assert.That(field.GetValue(created), Is.EqualTo(initial));
                    token = App.Session.BeginEdit(this); new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, created).Field(field).Write("RF15"); token.Commit();
                    Undo.PerformUndo(); Undo.PerformRedo();
                    var encoded = JsonDocumentCodec.CreateSaveSnapshot(App.AssetData);
                    var expected = JsonDocumentCodec.Content(JsonDocumentCodec.Decode(encoded));
                    var invalid = new TextAsset("not-json");
                    try { Assert.Throws<System.IO.InvalidDataException>(() => App.Context.Open(invalid)); }
                    finally { UnityEngine.Object.DestroyImmediate(invalid); }
                    Assert.That(JsonDocumentCodec.Content(App.AssetData), Is.EqualTo(expected));
                    App.Session.DiscardChanges(); original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
                    App.TextAsset = null; reopened = new TextAsset(encoded); App.TextAsset = reopened;
                    original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                    native = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>(); native.Show(); yield return null;
                    Assert.That(original.Context.Document, Is.SameAs(native.Document));
                    Assert.That(JsonDocumentCodec.Content(native.Document), Is.EqualTo(expected));
                    Assert.That(((LegacyExperienceInterval)native.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.Last(c => c.StartTime < 8)).Caption, Is.EqualTo("RF15"));
                    var now = (float)EditorApplication.timeSinceStartup; App.Context.Playback.Play(now); App.Context.Playback.Update(now + .1f, 1);
                    Assert.That(App.Context.Playback.IsPlaying, Is.True); Assert.That(App.Context.Player.CurrentTime, Is.GreaterThan(0));
                    App.Stop(); Assert.That(App.Context.Player.IsInitialized, Is.False); Assert.That(App.Context.Player.CurrentTime, Is.Zero);
                    App.TextAsset = fixture; UnityEngine.Object.DestroyImmediate(reopened); reopened = null;
                }
            }
            finally
            {
                events.Gesture = null; original.Gesture.Cancel(); App.Session.CancelEdit();
                events.Close(); original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
                ActionEditorHost.Window = previousWindow; ActionEditorHost.Width = previousWidth;
                if (reopened != null) UnityEngine.Object.DestroyImmediate(reopened);
            }
        }

        [UnityTest] public IEnumerator StructureHeadersExposeCurrentInputAndControlBehavior()
        {
            var previousWindow = ActionEditorHost.Window;
            var previousScroll = Styles.TimelineScrollPos;
            UIToolkitActionEditorWindow native = null;
            ActionEditorWindow imgui = null;
            try
            {
                var group = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0);
                group.Tracks.Clear(); App.AssetData.Init();
                native = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
                native.position = new Rect(60, 60, 1000, 600); native.Show(); native.Focus();
                yield return null; yield return null;
                var canvas = native.Canvas;
                var headers = canvas.Q("json-row-headers");
                var emptyHeader = headers.Children().First();
                Assert.That(emptyHeader.Query<Button>().ToList().Count, Is.EqualTo(1), "Empty group exposes only the menu button.");
                Assert.That(emptyHeader.Q("timeline-fold"), Is.Null);
                Assert.That(emptyHeader.Q("timeline-fold-placeholder").pickingMode, Is.EqualTo(PickingMode.Ignore));
                Pointer(canvas, EventType.MouseDown, new Vector2(90, 13));
                Pointer(canvas, EventType.MouseDrag, new Vector2(90, 90));
                Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                Pointer(canvas, EventType.MouseUp, new Vector2(90, 90));
                Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1), Is.SameAs(group));
                native.DiscardChanges(); native.Close(); native = null;

                group = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0);
                group.Tracks.Clear(); App.AssetData.Init();
                Styles.TimelineScrollPos = Vector2.zero;
                imgui = ScriptableObject.CreateInstance<ActionEditorWindow>();
                imgui.position = new Rect(60, 60, 1000, 600); imgui.Show(); imgui.Focus(); imgui.Repaint();
                yield return null; yield return null; yield return null;
                var offset = (Vector2)typeof(ActionEditorWindow).GetProperty("ContentOffset", Instance).GetValue(imgui);
                var start = offset + new Vector2(90, Styles.PlayControlHeight + 13);
                var end = start + new Vector2(0, 70);
                imgui.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(App.FistSelect, Is.SameAs(group));
                imgui.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(imgui.Gesture.DragType, Is.Not.EqualTo(ItemDragType.Track), "Group currently has no structure gesture.");
                imgui.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                imgui.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                group = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0);
                var wasCollapsed = imgui.Workspace.IsCollapsed(group);
                var fold = offset + new Vector2(12, Styles.PlayControlHeight + 12);
                imgui.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = fold });
                imgui.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = fold });
                Assert.That(imgui.Workspace.IsCollapsed(group), Is.Not.EqualTo(wasCollapsed), "Empty group currently exposes a clickable collapse icon.");
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                if (imgui != null) { imgui.Gesture.Cancel(); imgui.DiscardChanges(); imgui.Close(); }
                ActionEditorHost.Window = previousWindow; Styles.TimelineScrollPos = previousScroll;
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
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
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

        [UnityTest] public IEnumerator ImguiStructureInsertionPaintCurrentlyStopsAtHeaderBoundary()
        {
            var previousWindow = ActionEditorHost.Window;
            var previousScroll = Styles.TimelineScrollPos;
            var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
            Texture2D image = null;
            try
            {
                Styles.TimelineScrollPos = Vector2.zero;
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null; yield return null;
                var offset = ((Vector2)typeof(ActionEditorWindow).GetProperty("ContentOffset", Instance).GetValue(window)).y;
                var pitch = Styles.LineHeight + Styles.Space;
                var start = new Vector2(90, offset + Styles.PlayControlHeight + 2 * pitch + 13);
                var end = new Vector2(90, offset + Styles.PlayControlHeight + pitch + 3);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + Vector2.down * 4 });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Gesture.DragType, Is.EqualTo(ItemDragType.Track));
                Assert.That(window.Gesture.TrackDrop.IsValid, Is.True);
                window.Repaint(); yield return null; yield return null;
                image = ReadWindowPixels(window);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/structure-insertion-imgui-baseline.png"), image.EncodeToPNG());
                var scale = EditorGUIUtility.pixelsPerPoint;
                var y = Mathf.RoundToInt((Styles.PlayControlHeight + window.Gesture.TrackDrop.Y) * scale);
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
                Assert.That(BlueAt(Styles.TimelineLeftTotalWidth + 200), Is.False, "Current IMGUI insertion paint does not reach the time area.");
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                window.Gesture.Cancel(); window.DiscardChanges(); window.Close();
                ActionEditorHost.Window = previousWindow; Styles.TimelineScrollPos = previousScroll;
            }
        }

        [UnityTest] public IEnumerator ToolkitTrackHeaderCanMoveUpAndDetachWithoutLosingClips()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.position = new Rect(60, 60, 1000, 600); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas;
                var group = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0); var moving = group.Tracks[1]; var first = group.Tracks[0];
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
                Undo.PerformUndo(); Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(first.Name));
                Undo.PerformRedo(); Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(moving.Name));
                moving = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0];
                start = new Vector2(90, RowIndex(canvas, moving) * 28 + 13);
                end = new Vector2(5, start.y + 10);
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                Pointer(canvas, EventType.MouseUp, end);
                Assert.That(window.LastError, Is.Null); Assert.That(moving.Group, Is.Null);
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Single(), Is.SameAs(moving));
                Assert.That(moving.Root, Is.SameAs(App.AssetData)); Assert.That(moving.Clips, Is.Not.Empty);
                Assert.That(App.AssetData.RootItems[1], Is.SameAs(moving), "Root zone inserts after the whole first group block.");
                start = new Vector2(90, RowIndex(canvas, moving) * 28 + 13);
                end = new Vector2(90, RowIndex(canvas, App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1)) * 28 + 13);
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                Key(canvas, KeyCode.Escape); Pointer(canvas, EventType.MouseUp, end);
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Single(), Is.SameAs(moving));
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                var outside = new Vector2(-10, end.y);
                Pointer(canvas, EventType.MouseUp, outside);
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Single(), Is.SameAs(moving));
                Pointer(canvas, EventType.MouseDown, start); Pointer(canvas, EventType.MouseDrag, end);
                Pointer(canvas, EventType.MouseUp, end);
                Assert.That(window.LastError, Is.Null); Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>(), Is.Empty);
                Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1).Tracks[0], Is.SameAs(moving));
                var encoded = JsonDocumentCodec.CreateSaveSnapshot(App.AssetData);
                Assert.That(JsonDocumentCodec.Content(JsonDocumentCodec.Decode(encoded)), Is.EqualTo(JsonDocumentCodec.Content(App.AssetData)));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator OriginalTrackHeadersReorderDetachRejoinAndCancelThroughWindowInput()
        {
            var previousWindow = ActionEditorHost.Window; var previousScroll = Styles.TimelineScrollPos;
            var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
            try
            {
                window.Workspace.ScrollY = 0;
                window.position = new Rect(60, 60, 1000, 600); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null; yield return null;
                var moving = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1];
                var name = moving.Name;
                var pitch = Styles.LineHeight + Styles.Space;
                var offset = Styles.PlayControlHeight + ((Vector2)typeof(ActionEditorWindow).GetProperty("ContentOffset", Instance).GetValue(window)).y;
                var start = new Vector2(90, offset + 2 * pitch + 13);
                var end = new Vector2(90, offset + pitch + 3);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(App.FistSelect, Is.SameAs(moving), "Header event must reach the intended row; offset=" + offset);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + Vector2.down * 4 });
                Assert.That(window.Gesture.DragType, Is.EqualTo(ItemDragType.Track));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Gesture.TrackDrop.IsValid, Is.True);
                Assert.That(window.Gesture.TrackDrop.Index, Is.Zero, "drop=" + window.Gesture.TrackDrop.Row?.Name + ", y=" + window.Gesture.TrackDrop.Y + ", before=" + window.Gesture.TrackDrop.Before);
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                Assert.That(window.Gesture.DragType, Is.EqualTo(ItemDragType.None), "Release must end the captured gesture.");
                Assert.That(window.Gesture.TrackDrop.IsValid, Is.True, "Release must remain inside the viewport.");
                Assert.That(window.Gesture.TrackDrop.Index, Is.Zero, "Release drop=" + window.Gesture.TrackDrop.Row?.Name + ", y=" + window.Gesture.TrackDrop.Y);
                Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo(name));
                Undo.PerformUndo(); Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1].Name, Is.EqualTo(name));
                Undo.PerformRedo();
                window.Repaint(); yield return null; yield return null;
                start = new Vector2(90, offset + pitch + 13); end = new Vector2(5, start.y + 10);
                foreach (var type in new[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp })
                    window.SendEvent(new Event { type = type, button = 0, mousePosition = type == EventType.MouseDown ? start : end });
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Single().Name, Is.EqualTo(name));
                window.Workspace.ScrollY = pitch * 3;
                window.Repaint(); yield return null; yield return null;
                var rows = TimelineGeometry.VisibleRows(App.AssetData, window.Workspace);
                start = new Vector2(90, offset + rows.IndexOf(App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(0)) * pitch + 13 - window.Workspace.ScrollY);
                end = new Vector2(90, offset + rows.IndexOf(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1)) * pitch + 13 - window.Workspace.ScrollY);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(App.FistSelect, Is.SameAs(App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(0)), "Root row start=" + start + ", scroll=" + window.Workspace.ScrollY);
                var timeline = typeof(ActionEditorWindow).GetField("_timelineView", Instance).GetValue(window);
                var middle = (TimelineMiddleView)typeof(TimelineView).GetField("_middleView", Instance).GetValue(timeline);
                Assert.That(typeof(ViewBase).GetField("_havePointerDown", Instance).GetValue(middle), Is.True,
                    "Root press must reach body: start=" + start + ", bounds=" + middle.Position + ", offset=" + offset + ", rows=" + rows.Count + ", scroll=" + window.Workspace.ScrollY);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Gesture.DragType, Is.EqualTo(ItemDragType.Track));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Single().Name, Is.EqualTo(name));
                window.Repaint(); yield return null; yield return null;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = start });
                Assert.That(App.FistSelect, Is.SameAs(App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(0)), "Root row after cancel start=" + start);
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = end });
                Assert.That(window.Gesture.DragType, Is.EqualTo(ItemDragType.Track));
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = end });
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>(), Is.Empty);
                Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1).Tracks[0].Name, Is.EqualTo(name));
            }
            finally { window.DiscardChanges(); window.Close(); ActionEditorHost.Window = previousWindow; Styles.TimelineScrollPos = previousScroll; }
        }

        [Test] public void RootTracksPersistUndoPreviewAndRejectDuplicateOwnership()
        {
            var track = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            App.Context.Structure.MoveTrack(track, null, 0);
            Assert.That(track.Parent, Is.Null); Assert.That(track.Root, Is.SameAs(App.AssetData));
            Assert.That(track.IsActive, Is.True); Assert.That(clip.Parent, Is.SameAs(track));
            Assert.That(TimelineGeometry.IsVisible(clip, new TimelineWorkspace()), Is.True);
            var encoded = JsonDocumentCodec.CreateSaveSnapshot(App.AssetData);
            var decoded = JsonDocumentCodec.Decode(encoded);
            Assert.That(decoded.RootItems.OfType<TimelineTrack>().Single().Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.That(decoded.RootItems.OfType<TimelineTrack>().ElementAt(0).Root, Is.SameAs(decoded));
            App.Context.Player.Sample(.75f); Assert.That(App.Context.Player.IsInitialized, Is.True); App.Stop();
            Undo.PerformUndo(); Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>(), Is.Empty);
            Undo.PerformRedo(); Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Count(), Is.EqualTo(1));
            var moved = App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(0); moved.IsLocked = true;
            Assert.That(App.Context.Structure.Reorder(moved, App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0), 0).CanExecute(out _), Is.False);
            moved.IsLocked = false;
            App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks.Add(moved);
            Assert.Throws<System.IO.InvalidDataException>(() => JsonDocumentCodec.CreateSaveSnapshot(App.AssetData));
            App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks.Remove(moved);
        }

        [Test] public void RootTrackOrderingCopyCutLocksAndRecoveryKeepOneOwner()
        {
            var first = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0]; var second = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1];
            App.Context.Structure.MoveTrack(first, null, 0); App.Context.Structure.MoveTrack(second, null, 1);
            App.Context.Structure.MoveTrack(second, null, 0);
            Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>(), Is.EqualTo(new[] { second, first }));
            App.Context.Structure.MoveTrack(second, null, 2);
            Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>(), Is.EqualTo(new[] { first, second }));
            App.Context.ClipboardEditing.Copy(first, true); first.IsLocked = true;
            Assert.That(App.Context.ClipboardEditing.CanPaste(null), Is.False);
            Assert.Throws<InvalidOperationException>(() => App.Context.ClipboardEditing.Paste(null));
            first.IsLocked = false; App.Context.ClipboardEditing.Duplicate(first);
            Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Count(), Is.EqualTo(3));
            Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Last().Clips[0], Is.Not.SameAs(first.Clips[0]));
            var restored = App.Session.CaptureRecovery(); var expected = JsonDocumentCodec.Content(App.AssetData);
            App.Context.Structure.Delete(second); App.Session.RestoreRecovery(restored);
            Assert.That(JsonDocumentCodec.Content(App.AssetData), Is.EqualTo(expected));
            var lockedGroup = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1); lockedGroup.IsLocked = true;
            Assert.That(App.Context.Structure.Reorder(App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(0), lockedGroup, 0).CanExecute(out _), Is.False);
            lockedGroup.IsLocked = false;
            App.Context.Structure.MoveTrack(App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(0), lockedGroup, 1);
            Assert.That(lockedGroup.Tracks[1].Group, Is.SameAs(lockedGroup));
        }

        [Test] public void RootTrackDiskSnapshotReopensWithClipsAndExactOrder()
        {
            var first = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0]; var second = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[1];
            App.Context.Structure.MoveTrack(first, null, 0); App.Context.Structure.MoveTrack(second, null, 0);
            var expected = JsonDocumentCodec.Content(App.AssetData);
            var directory = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/RootTrack-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "Document.json"); TextAsset reopened = null;
            try
            {
                new AtomicFileCommit().Save(path, () => System.Text.Encoding.UTF8.GetBytes(JsonDocumentCodec.CreateSaveSnapshot(App.AssetData)),
                    bytes => Assert.That(JsonDocumentCodec.Content(JsonDocumentCodec.Decode(System.Text.Encoding.UTF8.GetString(bytes))), Is.EqualTo(expected)), createOnly: true);
                reopened = new TextAsset(System.IO.File.ReadAllText(path)); App.TextAsset = reopened;
                Assert.That(JsonDocumentCodec.Content(App.AssetData), Is.EqualTo(expected));
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().Select(t => t.Name), Is.EqualTo(new[] { second.Name, first.Name }));
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().ElementAt(1).Clips[0].StartTime, Is.EqualTo(.25f));
                Assert.That(App.AssetData.RootItems.OfType<TimelineTrack>().All(t => t.Parent == null && t.Root == App.AssetData), Is.True);
            }
            finally
            {
                App.TextAsset = fixture;
                if (reopened != null) UnityEngine.Object.DestroyImmediate(reopened);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                System.IO.Directory.Delete(directory);
            }
        }

        [UnityTest] public IEnumerator BothRulersZoomAroundHoveredTimeWithoutAuthorChanges()
        {
            var previous = ActionEditorHost.Window;
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            UIToolkitActionEditorWindow native = null;
            try
            {
                original.position = new Rect(60, 60, 1000, 600); original.Show(); original.Focus(); original.Repaint();
                yield return null; yield return null; yield return null;
                TimelineNavigation.SetRange(original.Workspace, new Vector2(1, 8));
                var offset = (Vector2)typeof(ActionEditorWindow).GetProperty("ContentOffset", Instance).GetValue(original);
                var fraction = 100 / (original.position.width - Styles.TimelineLeftTotalWidth);
                var anchor = 1 + 7 * fraction;
                original.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = new Vector2(Styles.TimelineLeftTotalWidth + 100, 30) + offset, delta = Vector2.up });
                Assert.That(original.Workspace.ViewTime, Is.GreaterThan(7));
                Assert.That(original.Workspace.ViewTimeMin + fraction * original.Workspace.ViewTime, Is.EqualTo(anchor).Within(.0001f));
                native = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
                native.position = new Rect(60, 60, 1000, 600); native.Show(); native.Focus();
                yield return null; yield return null;
                TimelineNavigation.SetRange(native.Workspace, new Vector2(1, 8));
                var root = native.rootVisualElement; fraction = 100 / (root.contentRect.width - native.Canvas.Left); anchor = 1 + 7 * fraction;
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, mousePosition = root.LocalToWorld(new Vector2(native.Canvas.Left + 100, 30)), delta = Vector2.up }))
                { wheel.target = root; root.SendEvent(wheel); }
                Assert.That(native.Workspace.ViewTime, Is.EqualTo(Prefs.scrollWheelZooms ? 8.575f : 7).Within(.001f));
                Assert.That(native.Workspace.ViewTimeMin + fraction * native.Workspace.ViewTime, Is.EqualTo(anchor).Within(.0001f));
                Assert.That(App.Session.IsDirty || App.Session.IsEditing, Is.False);
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                original.DiscardChanges(); original.Close(); ActionEditorHost.Window = previous;
            }
        }

        [UnityTest] public IEnumerator ToolkitEmptyFoldPlaceholderStaysPassiveAndTracksUndoRedoFirstChild()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                var group = App.AssetData.RootItems.OfType<TimelineGroup>().First(); group.Tracks.Clear(); App.AssetData.Validate();
                window.position = new Rect(60, 60, 1000, 600); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas; var header = canvas.Q("json-row-headers").Children().First();
                Assert.That(header.Q<Button>("timeline-fold"), Is.Null);
                Pointer(canvas, EventType.MouseDown, new Vector2(12, 13)); Pointer(canvas, EventType.MouseUp, new Vector2(12, 13));
                Assert.That(window.Workspace.IsCollapsed(group), Is.False);
                App.Context.Structure.AddTrack(group, typeof(LegacyIntervalTrack)); yield return null; yield return null;
                header = canvas.Q("json-row-headers").Children().First();
                var fold = header.Q<Button>("timeline-fold"); Assert.That(fold, Is.Not.Null);
                var point = fold.contentRect.center;
                using (var down = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = fold.LocalToWorld(point) }))
                { down.target = fold; fold.SendEvent(down); }
                using (var up = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = fold.LocalToWorld(point) }))
                { up.target = fold; fold.SendEvent(up); }
                yield return null;
                Assert.That(window.Workspace.IsCollapsed(group), Is.True); Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Undo.PerformUndo(); yield return null;
                Assert.That(canvas.Q("json-row-headers").Children().First().Q<Button>("timeline-fold"), Is.Null);
                Undo.PerformRedo(); yield return null;
                Assert.That(canvas.Q("json-row-headers").Children().First().Q<Button>("timeline-fold"), Is.Not.Null);
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ToolkitScrolledGroupDragRecalculatesOverlayAtViewportEdgesAndCancelsOnUndo()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.position = new Rect(60, 60, 800, 250); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas; canvas.Presenter.Scroll(56); yield return null;
                var group = App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(1);
                var row = canvas.State.Rows.Single(value => value.Key == canvas.Presenter.KeyFor(group));
                var before = JsonDocumentCodec.Content(App.AssetData);
                Pointer(canvas, EventType.MouseDown, new Vector2(80, row.Bounds.Y + 13));
                Pointer(canvas, EventType.MouseDrag, new Vector2(80, 12));
                var scroll = window.ScrollY; canvas.Tick(.1);
                Assert.That(window.ScrollY, Is.EqualTo(Mathf.Max(0, scroll - 15)).Within(.001f));
                Assert.That(canvas.State.Insertion.Width, Is.EqualTo(canvas.contentRect.width));
                Pointer(canvas, EventType.MouseDrag, new Vector2(-1, 12));
                Assert.That(canvas.State.Insertion.Visible, Is.False);
                Key(canvas, KeyCode.Escape);
                Assert.That(canvas.PointerId, Is.EqualTo(-1)); Assert.That(JsonDocumentCodec.Content(App.AssetData), Is.EqualTo(before));
                App.Session.Edit("sentinel", () => group.Name = "sentinel");
                row = canvas.State.Rows.Single(value => value.Key == canvas.Presenter.KeyFor(group));
                Pointer(canvas, EventType.MouseDown, new Vector2(80, row.Bounds.Y + 13)); Pointer(canvas, EventType.MouseDrag, new Vector2(80, 70));
                Key(canvas, KeyCode.Z, control: true);
                Assert.That(canvas.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle)); Assert.That(canvas.State.Insertion.Visible, Is.False);
                Assert.That(JsonDocumentCodec.Content(App.AssetData), Is.EqualTo(before));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [Test] public void ExistingSampleUsesMetadataAndOptionalAdaptersWithoutBusinessBranches()
        {
            Assert.That(DirectableTypeInfo.For(typeof(LegacyExperienceMarker)).IsSignal, Is.True);
            Assert.That(EditorCustomFactory.GetHeader(App.AssetData), Is.TypeOf<Samples.LegacyExperienceHeader>());
            Assert.That(EditorCustomFactory.GetHeader(App.AssetData).CreateGUI(), Is.Not.Null);
            Assert.That(EditorCustomFactory.GetInspectorType(typeof(LegacyExperienceMarker)), Is.EqualTo(typeof(ActionClipInspector)), "The shared clip inspector inherits generic authored fields.");
            Assert.That(ActionEditorTypeCatalog.CreatePreview(typeof(LegacyExperienceMarker)), Is.Null, "The sample state view has no scene side effects.");
        }
    }
}
