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
            private readonly FieldInspector inspector = new FieldInspector();
            private void OnGUI()
            {
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
