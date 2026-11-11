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
            public OriginalTimelineGesture Gesture;
            private readonly FieldInspector inspector = new FieldInspector();
            private void OnGUI()
            {
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
        private static Clip Last(int track) => App.AssetData.groups[0].Tracks[track].Clips.Last();
        private static void OriginalCreate(Track track, Type type)
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
        private static void NativeCreate(JsonActionWorkbench window, int track, Type type)
        {
            var canvas = window.Canvas; var rows = canvas.Rows(); var target = App.AssetData.groups[0].Tracks[track];
            var row = rows.IndexOf(target);
            var position = canvas.LocalToWorld(new Vector2(canvas.X(6.5f), row * 28 + 13 - window.ScrollY));
            var menu = new DropdownMenu();
            using (var trigger = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = position, button = 1 }))
            using (var e = ContextualMenuPopulateEvent.GetPooled(trigger, menu, canvas, null))
                typeof(JsonTimelineCanvas).GetMethod("Menu", Instance).Invoke(canvas, new object[] { e });
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
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
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
                        LogAssert.Expect(LogType.Log, "type=" + type + " newAction=" + type);
                        if (ui == 0) OriginalCreate(App.AssetData.groups[0].Tracks[track], type);
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
                            var root = new VisualElement(); window.rootVisualElement.Add(root); JsonPropertyView.DrawFields(root, clip, () => { }, 0);
                            var text = root.Query<TextField>().ToList().Single(f => f.label == (track == 0 ? "标题" : "消息"));
                            var input = text.Q("unity-text-input").Q<TextElement>(); input.Focus(); yield return null;
                            Key(input, KeyCode.A, control: true); Key(input, KeyCode.Alpha7, '7'); Key(input, KeyCode.Return, '\n');
                            window.Canvas.Focus(); yield return null; root.RemoveFromHierarchy();
                        }
                        Assert.That(field.GetValue(clip), Is.EqualTo("7"), "Real property control must change the authored field: UI=" + ui + ", track=" + track);
                        token.Cancel(); clip = Last(track); Assert.That(field.GetValue(clip), Is.EqualTo(initial));
                        token = App.Session.BeginEdit(this); new JsonPropertyPath(clip).Field(field).Write("7"); token.Commit();
                        Undo.PerformUndo(); Assert.That(field.GetValue(Last(track)), Is.EqualTo(initial));
                        Undo.PerformRedo(); Assert.That(field.GetValue(Last(track)), Is.EqualTo("7"));
                    }
                    var encoded = JsonDocumentCodec.CreateSaveSnapshot(App.AssetData);
                    var reopened = JsonDocumentCodec.Decode(encoded);
                    Assert.That(reopened.groups[0].Tracks[0].Clips.Last(), Is.TypeOf<LegacyExperienceInterval>());
                    Assert.That(reopened.groups[0].Tracks[2].Clips.Last().Length, Is.Zero);
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
            var native = ScriptableObject.CreateInstance<JsonActionWorkbench>();
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
                    LogAssert.Expect(LogType.Log, "type=" + type + " newAction=" + type);
                    if (ui == 0) OriginalCreate(App.AssetData.groups[0].Tracks[0], type);
                    else NativeCreate(native, 0, type);
                    var created = Last(0); JsonEditCommands.Copy(created);
                    var copy = (Clip)JsonEditCommands.Paste(created.Parent, 9);
                    Assert.That(copy, Is.Not.SameAs(created));
                    App.AssetData.ViewTimeMax = 12; native.SyncView();
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
                    Undo.PerformUndo(); created = App.AssetData.groups[0].Tracks[0].Clips.Last(c => c.StartTime < 8);
                    Assert.That(created.StartTime, Is.EqualTo(start).Within(.001));
                    Undo.PerformRedo(); created = App.AssetData.groups[0].Tracks[0].Clips.Last(c => c.StartTime < 8);
                    native.Select(created); var length = created.Length;
                    if (ui == 0)
                    {
                        events.Gesture = original.Gesture; events.Focus(); events.Repaint(); yield return null;
                        var down = new Vector2(Styles.TimelineLeftTotalWidth + App.AssetData.TimeToPos(created.EndTime, ActionEditorHost.Width), 30) + events.ContentOffset;
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
                    var token = App.Session.BeginEdit(this); new JsonPropertyPath(created).Field(field).Write("cancelled"); token.Cancel();
                    created = App.AssetData.groups[0].Tracks[0].Clips.Last(c => c.StartTime < 8);
                    Assert.That(field.GetValue(created), Is.EqualTo(initial));
                    token = App.Session.BeginEdit(this); new JsonPropertyPath(created).Field(field).Write("RF15"); token.Commit();
                    Undo.PerformUndo(); Undo.PerformRedo();
                    var encoded = JsonDocumentCodec.CreateSaveSnapshot(App.AssetData);
                    var expected = JsonDocumentCodec.Content(JsonDocumentCodec.Decode(encoded));
                    var invalid = new TextAsset("not-json");
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Error while parsing: Expected u"));
                    try { Assert.Throws<System.IO.InvalidDataException>(() => App.Context.Open(invalid)); }
                    finally { UnityEngine.Object.DestroyImmediate(invalid); }
                    Assert.That(JsonDocumentCodec.Content(App.AssetData), Is.EqualTo(expected));
                    App.Session.DiscardChanges(); original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
                    App.TextAsset = null; reopened = new TextAsset(encoded); App.TextAsset = reopened;
                    original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                    native = ScriptableObject.CreateInstance<JsonActionWorkbench>(); native.Show(); yield return null;
                    Assert.That(original.Context.Document, Is.SameAs(native.Document));
                    Assert.That(JsonDocumentCodec.Content(native.Document), Is.EqualTo(expected));
                    Assert.That(((LegacyExperienceInterval)native.Document.groups[0].Tracks[0].Clips.Last(c => c.StartTime < 8)).Caption, Is.EqualTo("RF15"));
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
