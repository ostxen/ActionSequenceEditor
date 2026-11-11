using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    public sealed class SharedJsonTests
    {
        private sealed class CollectionFixture
        {
            public System.Collections.Generic.List<string> Values = new System.Collections.Generic.List<string> { "one", "two" };
            public int[] Numbers = { 1, 2 };
            public string[] Empty;
            public System.Collections.Generic.List<int> Missing;
        }
        private sealed class CollectionInspector : InspectorsBase
        {
            public Rect RemoveRect, AddRect;
            protected override bool DrawAddButton()
            {
                var clicked = base.DrawAddButton();
                if (Event.current.type == EventType.Repaint) AddRect = GUILayoutUtility.GetLastRect();
                return clicked;
            }
            protected override bool DrawRemoveButton()
            {
                var clicked = base.DrawRemoveButton();
                if (Event.current.type == EventType.Repaint && RemoveRect.width == 0) RemoveRect = GUILayoutUtility.GetLastRect();
                return clicked;
            }
            public void Expand(object value) => typeof(InspectorsBase).GetMethod("SetFoldout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new[] { value, (object)true });
            public void DrawField(CollectionFixture value, string name) => FieldDefaultInspector(typeof(CollectionFixture).GetField(name), value);
        }
        private sealed class CollectionWindow : EditorWindow
        {
            public CollectionFixture Value;
            public CollectionInspector Inspector;
            public string Field;
            public Vector2 ContentOffset;
            private void OnGUI()
            {
                ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                if (Value != null) Inspector.DrawField(Value, Field);
            }
        }
        private sealed class PropertyEvidenceWindow : EditorWindow
        {
            public bool Original;
            private Editor inspector;
            private InspectorPreviewAsset preview;
            public void Initialize(JsonActionWorkbench owner)
            {
                if (!Original) rootVisualElement.Add(owner.CreatePropertyView());
                else { preview = CreateInstance<InspectorPreviewAsset>(); inspector = Editor.CreateEditor(preview); }
            }
            private void OnGUI() { if (Original && inspector != null) inspector.OnInspectorGUI(); }
            private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); if (preview != null) DestroyImmediate(preview); }
        }
        private sealed class DragEventWindow : EditorWindow
        {
            public Vector2 ContentOffset;
            private void OnGUI()
            {
                ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                var ev = Event.current;
                var pointer = new NBC.ActionEditor.Events.PointerEventData(ev);
                if (ev.type == EventType.MouseDown) ItemDragger.OnBeginDrag(pointer);
                else if (ev.type == EventType.MouseDrag) ItemDragger.OnDrag(pointer);
                else if (ev.type == EventType.MouseUp) ItemDragger.OnEndDrag(pointer);
            }
        }
        [UnityTest] public IEnumerator OriginalCollectionDeleteClicksKeepGUILayoutBalancedAndNullArrayUnchanged()
        {
            var window = ScriptableObject.CreateInstance<CollectionWindow>();
            try
            {
                window.Value = new CollectionFixture(); window.Inspector = new CollectionInspector();
                window.Show(); window.position = new Rect(80, 80, 400, 400);
                foreach (var name in new[] { "Values", "Numbers" })
                {
                    window.Field = name; var value = typeof(CollectionFixture).GetField(name).GetValue(window.Value);
                    window.Inspector.Expand(value); window.Inspector.RemoveRect = default;
                    window.Focus(); window.Repaint(); yield return null; yield return null; yield return null;
                    var button = window.Inspector.RemoveRect; Assert.That(button.width, Is.GreaterThan(0));
                    var point = button.center + window.ContentOffset;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                    yield return null;
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                    window.Repaint(); yield return null; yield return null;
                    Assert.That(((System.Collections.IList)typeof(CollectionFixture).GetField(name).GetValue(window.Value)).Count, Is.EqualTo(1), name + " button=" + button);
                }
                window.Field = "Empty"; window.Repaint(); yield return null; yield return null;
                Assert.That(window.Value.Empty, Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [UnityTest] public IEnumerator NullCollectionFoldoutsPersistAndFirstItemCanBeCreated()
        {
            var window = ScriptableObject.CreateInstance<CollectionWindow>();
            try
            {
                window.Value = new CollectionFixture(); window.Inspector = new CollectionInspector();
                window.Show(); window.position = new Rect(80, 80, 400, 400);
                foreach (var field in new[] { "Empty", "Missing" })
                {
                    window.Field = field; window.Inspector.AddRect = default;
                    window.Focus(); window.Repaint(); yield return null; yield return null;
                    Assert.That(window.Inspector.AddRect.width, Is.Zero, "Expanding another null field must not expand this field.");
                    var foldout = new Vector2(8, 8) + window.ContentOffset;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = foldout });
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = foldout });
                    window.Repaint(); yield return null; yield return null;
                    Assert.That(window.Inspector.AddRect.width, Is.GreaterThan(0), field + " plus button must remain visible after repaint.");
                    var add = window.Inspector.AddRect.center + window.ContentOffset;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = add });
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = add });
                    window.Repaint(); yield return null; yield return null;
                    var value = typeof(CollectionFixture).GetField(field).GetValue(window.Value) as System.Collections.IList;
                    Assert.That(value, Is.Not.Null, field + " must retain expansion until the plus button can be clicked.");
                    Assert.That(value.Count, Is.EqualTo(1));
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [Test] public void JsonPickerRejectsNonJsonBeforeDecodingAndKeepsCurrentDocument()
        {
            var before = App.AssetData;
            var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionEditor/Runtime/Interfaces/IDirectable.cs");
            Assert.That(script, Is.Not.Null);
            LogAssert.Expect(LogType.Warning, "请选择 .json 格式的 ActionEditor 文档。");
            App.OnObjectPickerConfig(script);
            Assert.That(App.AssetData, Is.SameAs(before));
        }

        [UnityTest] public IEnumerator OriginalMultiMoveClampsOneDeltaAndKeepsSpacingAtZero()
        {
            var previousWidth = App.Width;
            var window = ScriptableObject.CreateInstance<DragEventWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600); window.Focus();
                yield return null; yield return null;
                App.Width = 700;
                var first = App.AssetData.groups[0].Tracks[0].Clips[0];
                var second = App.AssetData.groups[0].Tracks[1].Clips[0];
                first.StartTime = 1; second.StartTime = 3;
                App.SelectWithoutInspector(first, second); ClipDrawer.Reset();
                ClipDrawer.GetDraw(first).ClipRealRect = new Rect(100, 20, 150, 26);
                var down = new Vector2(Styles.TimelineLeftTotalWidth + 120, 30) + window.ContentOffset;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = down, shift = true });
                Assert.That(ItemDragger.DragType, Is.EqualTo(ItemDragType.Pos));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down - new Vector2(App.Width * 2 / 7, 0), shift = true });
                Assert.That(first.StartTime, Is.Zero.Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(2).Within(.0001f), "Both items must use the same zero-clamped delta.");
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down - new Vector2(400, 0), shift = true });
                Assert.That(first.StartTime, Is.Zero.Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(2).Within(.0001f));
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = down + new Vector2(100, 0), shift = true });
                Assert.That(first.StartTime, Is.EqualTo(2).Within(.0001f));
                Assert.That(second.StartTime, Is.EqualTo(4).Within(.0001f));
                ItemDragger.Cancel();
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(1).Within(.0001f));
            }
            finally { ItemDragger.Cancel(); window.Close(); App.Width = previousWidth; }
        }

        [Test] public void DocumentPickerFiltersBothDiscoveryPathsWithoutRestrictingGenericResources()
        {
            var selector = ScriptableObject.CreateInstance<ObjectSelectorWindow>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(ObjectSelectorWindow);
            var filter = type.GetField("_assetPathFilter", flags);
            var init = type.GetMethod("InitBuiltinList", flags);
            var list = type.GetField("_currentBuiltinResources", flags);
            try
            {
                var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionEditor/Runtime/Interfaces/IDirectable.cs");
                type.GetField("_folderPath", flags).SetValue(selector, "Assets/ActionEditor");
                filter.SetValue(selector, (Predicate<string>)App.IsJsonDocumentPath);
                init.Invoke(selector, new object[] { "TextAsset", null });
                foreach (var entry in (Array)list.GetValue(selector))
                    Assert.That(App.IsJsonDocumentPath((string)entry.GetType().GetField("path").GetValue(entry)), Is.True);
                var allowed = new System.Collections.Generic.List<int> { script.GetInstanceID() };
                init.Invoke(selector, new object[] { "TextAsset", allowed });
                Assert.That(((Array)list.GetValue(selector)).Length, Is.Zero);
                filter.SetValue(selector, null);
                init.Invoke(selector, new object[] { "TextAsset", allowed });
                Assert.That(((Array)list.GetValue(selector)).Length, Is.EqualTo(1), "Generic resource pickers still accept non-JSON TextAssets.");
                Assert.That(App.IsJsonDocumentPath("Assets/Sample.JSON"), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(selector); }
        }

        [Test] public void CollapsedClipsCannotBeHitThroughRetainedDrawGeometry()
        {
            var group = App.AssetData.groups[0]; var track = group.Tracks[0]; var clip = track.Clips[0];
            ClipDrawer.Reset();
            var draw = ClipDrawer.GetDraw(clip); draw.ClipRect = draw.ClipRealRect = new Rect(10, 10, 20, 20);
            Assert.That(ClipDrawer.GetClipByTrackPosition(track, new Vector2(15, 15)), Is.SameAs(clip));
            group.IsCollapsed = true; ClipDrawer.Reset();
            Assert.That(ClipDrawer.GetClipByTrackPosition(track, new Vector2(15, 15)), Is.Null);
            Assert.That(ClipDrawer.ClipContainsByRealRect(new Vector2(15, 15)), Is.False);
            CollectionAssert.DoesNotContain(ClipDrawer.GetClips(new Rect(10, 10, 20, 20)), clip);
        }

        [Test] public void StretchRefreshesMagnetReferencesWithoutPreviousMoveGesture()
        {
            var oldEvent = Event.current; var oldWidth = App.Width;
            try
            {
                App.Width = 700;
                var clip = App.AssetData.groups[0].Tracks[0].Clips[0]; App.SelectWithoutInspector(clip);
                typeof(ItemDragger).GetField("magnetSnapTimesCache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, new[] { 12345f });
                var source = new Event { type = EventType.MouseDrag, button = 0,
                    mousePosition = new Vector2(Styles.TimelineLeftTotalWidth + App.AssetData.TimeToPos(clip.EndTime, App.Width) - 2, 30) };
                Event.current = source;
                var pointer = new NBC.ActionEditor.Events.PointerEventData(); pointer.SetEvent(source);
                ItemDragger.OnBeginDrag(pointer);
                Assert.That(ItemDragger.DragType, Is.EqualTo(ItemDragType.StretchEnd));
                Assert.That(ItemDragger.HasMagnetSnapTime(2.5f), Is.True);
                Assert.That(ItemDragger.HasMagnetSnapTime(12345), Is.False);
                Assert.That(ItemDragger.HasMagnetSnapTime(clip.StartTime), Is.False, "The selected item is not a snap reference.");
            }
            finally { ItemDragger.Cancel(); Event.current = oldEvent; App.Width = oldWidth; }
        }

        [UnityTest] public IEnumerator OriginalMiddlePanReleaseOutsideDoesNotConsumeLaterLeftDrag()
        {
            var previousWindow = App.Window;
            var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600); window.Focus();
                yield return null; yield return null;
                App.AssetData.ViewTimeMax = 8; App.AssetData.ViewTimeMin = 1;
                var start = new Vector2(Styles.TimelineLeftTotalWidth + 100, 100);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 2, mousePosition = start });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 2, mousePosition = new Vector2(10, 100) });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + new Vector2(40, 0) });
                Assert.That(App.AssetData.ViewTimeMin, Is.EqualTo(1).Within(.001f));
                Assert.That(App.AssetData.ViewTimeMax, Is.EqualTo(8).Within(.001f));
                window.SendEvent(new Event { type = EventType.MouseDown, button = 2, mousePosition = start });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 2, mousePosition = start + new Vector2(40, 0) });
                Assert.That(App.AssetData.ViewTimeMin, Is.EqualTo(1).Within(.001f), "Escape must cancel navigation even when the window consumes the key first.");
            }
            finally { window.DiscardChanges(); window.Close(); App.Window = previousWindow; }
        }
        private TextAsset previous, fixture;
        private string recovery;
        [SetUp] public void Setup()
        {
            previous = App.TextAsset;
            recovery = App.Session.CaptureRecovery();
            fixture = new TextAsset(JsonDocumentSession.Encode(new LegacyExperienceAsset()));
            App.TextAsset = fixture;
        }
        [TearDown] public void Teardown()
        {
            App.Session.CancelEdit(); App.Stop(); App.TextAsset = previous;
            App.Session.RestoreRecovery(recovery);
            UnityEngine.Object.DestroyImmediate(fixture);
        }

        [Test] public void SharedContextOwnsStateAndServicesWithoutAnEditorWindow()
        {
            using (var context = new ActionEditorContext(new JsonDocumentSession()))
            {
                context.Open(fixture);
                var clip = context.Document.groups[0].Tracks[0].Clips[0];
                context.Selection.Set(new IDirectable[] { clip }, false);
                context.Clipboard.Item = clip;
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Document, Is.Not.SameAs(App.AssetData));
                Assert.That(context.Selection.First, Is.SameAs(clip));
                Assert.That(context.Clipboard.Item, Is.SameAs(clip));
                context.Playback.Play(10);
                Assert.That(context.Playback.Update(10.25f, 1), Is.True);
                Assert.That(context.Player.CurrentTime, Is.EqualTo(.25f).Within(.0001f));
                context.Playback.Pause(true); context.Playback.Update(11, 1);
                Assert.That(context.Player.CurrentTime, Is.EqualTo(.25f).Within(.0001f));
                context.Playback.Pause(false); context.Playback.Update(11.25f, 1);
                Assert.That(context.Player.CurrentTime, Is.EqualTo(.5f).Within(.0001f));
                context.Open(null);
                Assert.That(context.Selection.Count, Is.Zero);
                Assert.That(context.Playback.IsPlaying || context.Playback.IsPaused || context.Player.IsInitialized, Is.False);
                Assert.That(context.Player.CurrentTime, Is.Zero);
            }
        }

        [Test] public void FailedOpenKeepsSharedContextStateAndFacadeHasNoSecondPlayer()
        {
            var context = App.Context; var document = context.Document;
            var clip = document.groups[0].Tracks[0].Clips[0];
            App.SelectWithoutInspector(clip);
            var invalid = new TextAsset("{}");
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("Cannot create an instance of an interface or abstract type"));
                LogAssert.Expect(LogType.Error, new Regex("ActionEditor 打开失败"));
                App.TextAsset = invalid;
                Assert.That(context.Document, Is.SameAs(document));
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Selection.First, Is.SameAs(clip));
                Assert.That(AssetPlayer.Inst, Is.SameAs(context.Player));
                JsonEditCommands.Copy(clip);
                Assert.That(context.Clipboard.Item, Is.SameAs(App.CopyAsset));
                Assert.That(context.Clipboard.Type, Is.EqualTo(clip.GetType()));
                App.Pause(); Assert.That(context.Playback.IsPaused, Is.True);
                context.Playback.Pause(false); Assert.That(App.IsPause, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(invalid); }
        }

        [Test] public void ViewRegistrationIsIdempotentAndTransfersUpdateOwnership()
        {
            var views = new ActionEditorViews(); var first = new object(); var second = new object();
            var paints = 0; var changes = 0;
            views.Attach(first, () => paints++, () => changes++);
            views.Attach(first, () => paints++, () => changes++);
            views.Attach(second, () => paints++, () => changes++);
            Assert.That(views.Count, Is.EqualTo(2));
            Assert.That(views.OwnsUpdate(first), Is.True);
            Assert.That(views.OwnsUpdate(second), Is.False);
            views.Repaint(); views.NotifyChanged();
            Assert.That(paints, Is.EqualTo(2)); Assert.That(changes, Is.EqualTo(2));
            views.Detach(first); views.Detach(first);
            Assert.That(views.OwnsUpdate(second), Is.True);
            views.Detach(second);
            Assert.That(views.Count, Is.Zero);
            Assert.That(views.OwnsUpdate(first) || views.OwnsUpdate(second), Is.False);
        }

        [UnityTest] public IEnumerator BothWindowOpenOrdersShareContextAndRebuildWithoutRegistrationLeaks()
        {
            var count = App.Views.Count; var previousWindow = App.Window;
            ActionEditorWindow original = null; JsonActionWorkbench native = null;
            var alternate = new TextAsset(JsonDocumentSession.Encode(new LegacyExperienceAsset { Version = 7 }));
            try
            {
                foreach (var nativeFirst in new[] { false, true })
                {
                    if (nativeFirst)
                    {
                        native = ScriptableObject.CreateInstance<JsonActionWorkbench>(); native.Show();
                        original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                    }
                    else
                    {
                        original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                        native = ScriptableObject.CreateInstance<JsonActionWorkbench>(); native.Show();
                    }
                    yield return null; yield return null;
                    Assert.That(App.Views.Count, Is.EqualTo(count + 2));
                    Assert.That(original.Context, Is.SameAs(native.Context));
                    Assert.That(original.Context.Session, Is.SameAs(App.Session));
                    native.CreateGUI(); native.CreateGUI();
                    Assert.That(App.Views.Count, Is.EqualTo(count + 2));
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(JsonActionWorkbench).GetMethod("OnEnable", flags).Invoke(native, null);
                    typeof(ActionEditorWindow).GetMethod("OnEnable", flags).Invoke(original, null);
                    Assert.That(App.Views.Count, Is.EqualTo(count + 2), "Repeated enable must replace registrations rather than duplicate them.");
                    native.Select(App.AssetData.groups[0].Tracks[0].Clips[0]);
                    Assert.That(original.Context.Selection.First, Is.SameAs(App.FistSelect));
                    App.TextAsset = alternate;
                    Assert.That(original.Context.Document, Is.SameAs(native.Document));
                    Assert.That(((LegacyExperienceAsset)native.Document).Version, Is.EqualTo(7));
                    Assert.That(App.SelectCount, Is.Zero);
                    App.TextAsset = fixture;
                    if (nativeFirst) { native.Close(); native = null; original.Close(); original = null; }
                    else { original.Close(); original = null; native.Close(); native = null; }
                    Assert.That(App.Views.Count, Is.EqualTo(count));
                }
            }
            finally
            {
                if (original != null) { original.DiscardChanges(); original.Close(); }
                if (native != null) { native.DiscardChanges(); native.Close(); }
                App.Window = previousWindow; UnityEngine.Object.DestroyImmediate(alternate);
            }
        }

        [UnityTest] public IEnumerator DiscardFromEitherWindowRestoresSharedBaselineAndClearsPendingUndo()
        {
            var previousWindow = App.Window;
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            var native = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                foreach (var window in new EditorWindow[] { original, native })
                {
                    App.Session.Edit("discard payload", () => ((LegacyExperienceAsset)App.AssetData).Version = 19);
                    App.Session.BeginEdit();
                    App.AssetData.groups[0].Tracks[0].Clips[0].StartTime = 1;
                    window.DiscardChanges();
                    Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
                    Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                    Assert.That(App.Session.IsDirty, Is.False);
                    Assert.That(App.Session.IsEditing, Is.False);
                    Assert.That(native.Document, Is.SameAs(App.AssetData));
                    Undo.PerformUndo(); Undo.PerformRedo();
                    Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1), "Discarded changes must not return through Undo/Redo.");
                    yield return null; yield return null;
                    Assert.That(original.hasUnsavedChanges, Is.False);
                    Assert.That(native.hasUnsavedChanges, Is.False);
                }
                App.Session.Edit("new edit", () => ((LegacyExperienceAsset)App.AssetData).Version = 7);
                Undo.PerformUndo(); Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
                Undo.PerformRedo(); Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(7));
            }
            finally
            {
                App.TextAsset = fixture;
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close(); App.Window = previousWindow;
            }
        }

        [UnityTest] public IEnumerator ClosingOriginalStopsSharedPlaybackAndReopeningDoesNotResume()
        {
            var previousWindow = App.Window;
            var native = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            var stops = 0;
            CallbackFunction stopped = () => stops++;
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                App.OnStop += stopped;
                App.Play(); AssetPlayer.Inst.Sample(1); App.Pause();
                Assert.That(App.IsPlay && App.IsPause, Is.True);
                original.Close(); original = null;
                Assert.That(App.IsPlay, Is.False);
                Assert.That(App.IsPause, Is.False);
                Assert.That(AssetPlayer.Inst.CurrentTime, Is.Zero);
                Assert.That(AssetPlayer.Inst.IsInitialized, Is.False);
                Assert.That(stops, Is.EqualTo(1));
                yield return null; yield return null;
                Assert.That(App.IsPlay, Is.False, "The remaining native window must not resume playback.");
                original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                yield return null; yield return null;
                Assert.That(App.IsPlay, Is.False);
                Assert.That(AssetPlayer.Inst.CurrentTime, Is.Zero);
                native.Close(); native = null;
                App.Play(); AssetPlayer.Inst.Sample(1);
                original.Close(); original = null;
                Assert.That(App.IsPlay || App.IsPause || AssetPlayer.Inst.IsInitialized, Is.False);
                Assert.That(AssetPlayer.Inst.CurrentTime, Is.Zero);
            }
            finally
            {
                App.OnStop -= stopped;
                if (original != null) { original.DiscardChanges(); original.Close(); }
                if (native != null) { native.DiscardChanges(); native.Close(); }
                App.Window = previousWindow;
            }
        }

        [Test] public void SampleRoundtripContainsAllGroupsTracksClipsAndFields()
        {
            var sample = (LegacyExperienceAsset)App.AssetData;
            Assert.That(sample.groups.Count, Is.EqualTo(3));
            Assert.That(sample.groups.Sum(g => g.Tracks.Count), Is.EqualTo(7));
            Assert.That(sample.groups.SelectMany(g => g.Tracks).Sum(t => t.Clips.Count), Is.EqualTo(13));
            Assert.That(sample.Length, Is.EqualTo(6));
            var interval = (LegacyExperienceInterval)sample.groups[0].Tracks[0].Clips[1];
            Assert.That(interval.Offset, Is.EqualTo(new Vector3(2, 1, 0)));
            Assert.That(interval.Parameters.Count, Is.EqualTo(2)); Assert.That(interval.CanScale(), Is.True);
            Assert.That(sample.groups[0].Tracks[2].Clips[0].CanScale(), Is.False);
            Assert.That(sample.groups[2].Tracks[0].IsLocked, Is.True);
            Assert.That(sample.groups[2].Tracks[1].IsActive, Is.False);
        }
        [Test] public void SaveSnapshotRefreshesDerivedDurationWithoutDroppingEditedPayload()
        {
            var clip = (LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0];
            clip.StartTime = 8; clip.Length = 3; clip.Count = 27;
            Assert.That(App.AssetData.Length, Is.EqualTo(6), "The public graph can contain a stale derived duration before commit.");
            Assert.That(App.Session.InspectReadbackDifferences(), Does.Contain("$.length (value)"));
            var json = App.Session.CreateSaveSnapshot();
            var loaded = JsonDocumentSession.Decode(json);
            Assert.That(App.AssetData.Length, Is.EqualTo(11));
            Assert.That(loaded.Length, Is.EqualTo(11));
            Assert.That(((LegacyExperienceInterval)loaded.groups[0].Tracks[0].Clips[0]).Count, Is.EqualTo(27));
            Assert.That(App.Session.InspectReadbackDifferences(), Is.Empty);
        }
        [Test] public void ExtractedJsonCodecPreservesFormatAndRejectsPayloadDifferences()
        {
            var document = App.AssetData;
            var json = JsonDocumentSession.Encode(document);
            Assert.That(JsonDocumentCodec.Encode(document), Is.EqualTo(json));
            var decoded = JsonDocumentCodec.Decode(json);
            Assert.That(JsonDocumentCodec.ReadbackDifferences(json, decoded), Is.Empty);
            decoded.ViewTimeMin = 1; decoded.ViewTimeMax = 9;
            decoded.groups[0].IsCollapsed = !decoded.groups[0].IsCollapsed;
            Assert.That(JsonDocumentCodec.ReadbackDifferences(json, decoded), Is.Empty, "Workspace-only changes remain excluded.");
            ((LegacyExperienceInterval)decoded.groups[0].Tracks[0].Clips[0]).Count = 91;
            var differences = JsonDocumentCodec.ReadbackDifferences(json, decoded);
            Assert.That(differences, Has.Length.EqualTo(1));
            Assert.That(differences[0], Does.Contain("Count"));
            Assert.That(differences[0], Does.Not.Contain("91"), "Diagnostics must contain paths, not user values.");
            decoded.groups[0].Tracks[0].Clips.Add(null);
            Assert.Throws<InvalidDataException>(() => JsonDocumentCodec.CreateSaveSnapshot(decoded));
            Assert.That(App.AssetData, Is.SameAs(document));
            Assert.That(App.Session.IsDirty, Is.False);
        }
        [Test] public void FractionalTimelineEditsKeepSaveReadbackStable()
        {
            var random = new System.Random(1937);
            for (var iteration = 0; iteration < 80; iteration++)
            {
                App.Session.Edit("fractional edit", () =>
                {
                    foreach (var clip in App.AssetData.groups.SelectMany(g => g.Tracks).SelectMany(t => t.Clips))
                    {
                        clip.StartTime = (float)(random.NextDouble() * 17);
                        if (clip.CanScale()) clip.Length = (float)(random.NextDouble() * 4 + .01);
                    }
                });
                var json = App.Session.CreateSaveSnapshot();
                var differences = App.Session.InspectReadbackDifferences();
                Assert.That(differences, Is.Empty, "Iteration " + iteration + ": " + string.Join(", ", differences));
                Assert.That(JsonDocumentSession.Decode(json).Length, Is.EqualTo(App.AssetData.Length));
            }
        }
        [Test] public void FloatSerializationPreservesAuthoredBitsAndOldNumericJson()
        {
            var values = new System.Collections.Generic.List<float> { float.Epsilon, -float.Epsilon, float.MaxValue, float.MinValue, .1f, 16.3388195f };
            var random = new System.Random(1937);
            for (var i = 0; i < 512; i++)
            {
                var bytes = new byte[4]; random.NextBytes(bytes);
                var value = BitConverter.ToSingle(bytes, 0);
                if (!float.IsNaN(value) && !float.IsInfinity(value)) values.Add(value);
            }
            foreach (var value in values)
            {
                foreach (var compressed in new[] { false, true })
                {
                    var loaded = (float)Json.Deserialize(typeof(float), Json.Serialize(value, compressed));
                    Assert.That(BitConverter.GetBytes(loaded), Is.EqualTo(BitConverter.GetBytes(value)));
                }
            }
            Assert.That(Json.Deserialize(typeof(float), "0.1"), Is.EqualTo(.1f));
            Assert.That(Json.Deserialize(typeof(float), "3"), Is.EqualTo(3f));
        }
        [Test] public void SharedEditsUndoRedoAndCancelKeepWholeJsonGraph()
        {
            App.Session.Edit("Change payload", () => ((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0]).Count = 12);
            Assert.That(App.Session.IsDirty, Is.True);
            Undo.PerformUndo(); Assert.That(((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0]).Count, Is.EqualTo(3));
            Assert.That(App.Session.IsDirty, Is.False);
            Undo.PerformRedo(); Assert.That(((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0]).Count, Is.EqualTo(12));
            App.Session.BeginEdit(); App.AssetData.groups[0].Tracks[0].Clips[0].StartTime = 1.25f; App.Session.CancelEdit();
            Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
        }
        [Test] public void CopyPasteTrackAndCutClipPreservePayloadAndRespectLocks()
        {
            var group = App.AssetData.groups[0]; var track = group.Tracks[0];
            JsonEditCommands.Copy(track); var pasted = (Track)JsonEditCommands.Paste(group);
            Assert.That(pasted, Is.Not.SameAs(track)); Assert.That(pasted.Clips.Count, Is.EqualTo(3));
            Assert.That(((LegacyExperienceInterval)pasted.Clips[1]).Offset, Is.EqualTo(new Vector3(2, 1, 0)));
            var clip = track.Clips[0]; JsonEditCommands.Copy(clip, true); JsonEditCommands.Paste(pasted, 6);
            Assert.That(track.Clips.Count, Is.EqualTo(2)); Assert.That(pasted.Clips.Last().StartTime, Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => JsonEditCommands.Delete(App.AssetData.groups[2].Tracks[0]));
        }
        private sealed class FailingCreationClip : Clip
        {
            public FailingCreationClip() { throw new InvalidOperationException("expected constructor failure"); }
        }
        [Test] public void StructuralCommandsRevalidateAndRollbackFailuresWithoutEmptyUndo()
        {
            var group = App.AssetData.groups[0]; var track = group.Tracks[0];
            var command = JsonEditCommands.Creation(track, typeof(LegacyExperienceInterval), 8);
            Assert.That(command.CanExecute(out var reason), Is.True, reason);
            track.IsLocked = true;
            Assert.That(command.CanExecute(out reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => command.Execute());
            Assert.That(track.Clips.Count, Is.EqualTo(3)); track.IsLocked = false;
            var before = JsonDocumentSession.Encode(App.AssetData);
            Assert.Catch(() => JsonEditCommands.AddClip(track, typeof(FailingCreationClip), 8));
            Assert.That(JsonDocumentSession.Encode(App.AssetData), Is.EqualTo(before));
            Assert.That(App.Session.IsEditing, Is.False);
            group = App.AssetData.groups[0]; track = group.Tracks[0];
            var created = JsonEditCommands.AddClip(track, typeof(LegacyExperienceInterval), 8);
            Assert.That(created.StartTime, Is.EqualTo(8)); Assert.That(created.Parent, Is.SameAs(track));
            Undo.PerformUndo(); Assert.That(App.AssetData.groups[0].Tracks[0].Clips.Count, Is.EqualTo(3));
            Undo.PerformRedo(); Assert.That(App.AssetData.groups[0].Tracks[0].Clips.Count, Is.EqualTo(4));
            Assert.That(command.CanExecute(out reason), Is.False, "Undo/Redo invalidates captured graph targets.");
        }
        [Test] public void BatchDeletionAndLockedCutDoNotPartiallyModifyGraphOrClipboard()
        {
            var track = App.AssetData.groups[0].Tracks[0]; var clip = track.Clips[0];
            var locked = App.AssetData.groups[2].Tracks[0];
            var batch = JsonEditCommands.Deletion(clip, locked);
            Assert.That(batch.CanExecute(out var reason), Is.False, reason);
            Assert.Throws<InvalidOperationException>(() => batch.Execute());
            Assert.That(track.Clips.Count, Is.EqualTo(3));
            JsonEditCommands.Copy(clip, true); track.IsLocked = true;
            var target = App.AssetData.groups[0].Tracks[1];
            Assert.That(JsonEditCommands.CanPaste(target), Is.False);
            Assert.Throws<InvalidOperationException>(() => JsonEditCommands.Paste(target, 7));
            Assert.That(App.IsCut, Is.True); Assert.That(track.Clips.Count, Is.EqualTo(3));
            track.IsLocked = false;
            JsonEditCommands.Paste(target, 7);
            Assert.That(track.Clips.Count, Is.EqualTo(2)); Assert.That(target.Clips.Count, Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(App.AssetData.groups[0].Tracks[0].Clips.Count, Is.EqualTo(3));
            Assert.That(App.AssetData.groups[0].Tracks[1].Clips.Count, Is.EqualTo(2));
            track = App.AssetData.groups[0].Tracks[0];
            JsonEditCommands.Deletion(track.Clips[0], track.Clips[1]).Execute();
            Assert.That(track.Clips.Count, Is.EqualTo(1));
            Undo.PerformUndo(); Assert.That(App.AssetData.groups[0].Tracks[0].Clips.Count, Is.EqualTo(3));
        }
        [Test] public void ZoomAndFoldDoNotDirtyAuthoringData()
        {
            App.AssetData.ViewTimeMin = 1; App.AssetData.ViewTimeMax = 10;
            App.AssetData.groups[0].IsCollapsed = true;
            Assert.That(App.Session.IsDirty, Is.False);
            Assert.That(App.AssetData.ViewTimeMin, Is.EqualTo(1)); Assert.That(App.AssetData.ViewTimeMax, Is.EqualTo(10));
        }
        [Test] public void RecoveryPreservesUnsavedChangesAndBaseline()
        {
            App.Session.Edit("change", () => ((LegacyExperienceAsset)App.AssetData).Version = 9);
            var recovery = App.Session.CaptureRecovery();
            using var restored = new JsonDocumentSession(); restored.RestoreRecovery(recovery);
            Assert.That(((LegacyExperienceAsset)restored.Document).Version, Is.EqualTo(9));
            Assert.That(restored.IsDirty, Is.True);
        }
        [Test] public void RecoveryKeepsDiscardBaselineIncludingViewStateAndRepeatedDiscard()
        {
            App.AssetData.ViewTimeMin = 1; App.AssetData.ViewTimeMax = 10;
            App.Session.Edit("change", () => ((LegacyExperienceAsset)App.AssetData).Version = 9);
            using var restored = new JsonDocumentSession();
            restored.RestoreRecovery(App.Session.CaptureRecovery());
            restored.DiscardChanges();
            Assert.That(((LegacyExperienceAsset)restored.Document).Version, Is.EqualTo(1));
            Assert.That(restored.Document.ViewTimeMin, Is.Zero);
            Assert.That(restored.Document.ViewTimeMax, Is.EqualTo(7));
            Assert.That(restored.Document.groups[2].IsCollapsed, Is.True);
            Assert.That(restored.IsDirty, Is.False);
            restored.DiscardChanges();
            Assert.That(restored.IsDirty || restored.IsEditing, Is.False);
        }
        [Test] public void MultipleEditsHaveIndependentUndoStepsAndFailureRollsBack()
        {
            App.Session.Edit("one", () => ((LegacyExperienceAsset)App.AssetData).Version = 2);
            App.Session.Edit("two", () => ((LegacyExperienceAsset)App.AssetData).Version = 3);
            Undo.PerformUndo(); Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(2));
            Undo.PerformUndo(); Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => App.Session.Edit("failed", () => { ((LegacyExperienceAsset)App.AssetData).Version = 100; throw new InvalidOperationException(); }));
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
        }
        [Test] public void InvalidTimeRollsBackAndProtectedPathsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => App.Session.Edit("invalid", () => App.AssetData.groups[0].Tracks[0].Clips[0].StartTime = float.NaN));
            Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.Throws<ArgumentException>(() => JsonDocumentSession.ValidatePath("Assets/CombatEditor/Original.json"));
            Assert.Throws<ArgumentException>(() => JsonDocumentSession.ValidatePath("Assets/../Original.json"));
        }
        [Test] public void NestedCommandsStayInOneUndoAndRangeCanBeShorterThanDocument()
        {
            App.Session.BeginEdit();
            JsonEditCommands.Rename(App.AssetData.groups[0].Tracks[0], "nested");
            Assert.That(App.Session.IsEditing, Is.True);
            App.AssetData.RangeMin = 1; App.AssetData.RangeMax = 2;
            Assert.That(App.AssetData.RangeMax, Is.EqualTo(2));
            App.Session.EndEdit("outer"); Undo.PerformUndo();
            Assert.That(App.AssetData.groups[0].Tracks[0].Name, Is.Not.EqualTo("nested"));
            Assert.That(App.AssetData.RangeMin, Is.Zero);
        }
        [Test] public void OriginalInvalidStretchRestoresLengthAndTrackReorderIsUndoable()
        {
            var group = App.AssetData.groups[0]; var track = group.Tracks[0]; var clip = track.Clips[0];
            App.Select(clip); ItemDragger.DragType = ItemDragType.StretchEnd;
            clip.Length = 10; ItemDragger.DragType = ItemDragType.None;
            Assert.That(clip.Length, Is.EqualTo(1.5f));
            JsonEditCommands.MoveTrack(track, group, 3);
            Assert.That(group.Tracks[2], Is.SameAs(track));
            Undo.PerformUndo(); Assert.That(App.AssetData.groups[0].Tracks[0].Name, Is.EqualTo(track.Name));
        }
        [Test] public void PreviewResetDisposesOwnedCallbacksAndAllowsRebuild()
        {
            AssetPlayer.Inst.Reset(); AssetPlayer.Inst.Sample(0);
            Assert.That(AssetPlayer.Inst.IsInitialized, Is.True, "First sample at zero must initialize preview.");
            AssetPlayer.Inst.Sample(1); Assert.That(AssetPlayer.Inst.IsInitialized, Is.True);
            AssetPlayer.Inst.Reset(); Assert.That(AssetPlayer.Inst.IsInitialized, Is.False);
            AssetPlayer.Inst.Sample(2); Assert.That(AssetPlayer.Inst.IsInitialized, Is.True);
            App.Session.Edit("remove", () => App.AssetData.groups[0].Tracks[0].Clips.RemoveAt(0));
            Assert.That(AssetPlayer.Inst.IsInitialized, Is.False);
        }
        [Test] public void SnapshotReadbackAndReliableCommitPreserveExistingFileOnFailure()
        {
            var directory = System.IO.Path.Combine("Temp", "ActionEditor-R-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); var path = System.IO.Path.Combine(directory, "Document.json");
            try
            {
                var original = System.Text.Encoding.UTF8.GetBytes(JsonDocumentSession.Encode(App.AssetData));
                new ReliableFileCommit().Save(path, () => original, bytes => JsonDocumentSession.Decode(System.Text.Encoding.UTF8.GetString(bytes)));
                Assert.That(JsonDocumentSession.Decode(File.ReadAllText(path)).groups.Count, Is.EqualTo(3));
                Assert.Throws<IOException>(() => new ReliableFileCommit().Save(path, () => original, _ => { }, new byte[] { 1 }));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            }
            finally { Directory.Delete(directory, true); }
        }
        [TestCase(typeof(string), "")]
        [TestCase(typeof(int), 0)]
        public void CollectionDefaultCreationSupportsPrimitives(Type type, object expected) => Assert.That(InspectorsBase.CreateElement(type), Is.EqualTo(expected));

        [UnityTest] public IEnumerator ToolkitUsesSameGraphNativeInspectorAndOriginalGeometry()
        {
            var oldWindow = App.Window;
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1000, 600); window.Show();
                yield return null; yield return null;
                Assert.That(window.Document, Is.SameAs(App.AssetData));
                Assert.That(window.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(window.rootVisualElement.Q("json-header").resolvedStyle.height, Is.EqualTo(40));
                Assert.That(window.rootVisualElement.Q("json-footer").resolvedStyle.height, Is.EqualTo(16).Within(.001f));
                Assert.That(window.rootVisualElement.Q("json-ruler").worldBound.yMin - window.rootVisualElement.worldBound.yMin, Is.EqualTo(20).Within(1));
                Assert.That(window.Canvas.Rows().Count, Is.EqualTo(8)); // 3 groups, first two groups' 5 tracks; third collapsed
                var clip = App.AssetData.groups[0].Tracks[0].Clips[0]; window.Select(clip);
                var view = window.CreatePropertyView(); window.rootVisualElement.Add(view); yield return null;
                var count = view.Query<IntegerField>().ToList().First(f => f.label == "次数");
                count.value = 7; yield return null;
                Assert.That(((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0]).Count, Is.EqualTo(7));
                Undo.PerformUndo(); yield return null;
                Assert.That(((LegacyExperienceInterval)App.AssetData.groups[0].Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                window.rootVisualElement.Remove(view);
                window.DiscardChanges();
            }
            finally { window.DiscardChanges(); window.Close(); App.Window = oldWindow; }
        }

        private static void Pointer(VisualElement target, EventType type, Vector2 local, int button = 0, bool shift = false)
        {
            var point = target.LocalToWorld(local);
            var source = new Event { type = type, mousePosition = point, button = button, shift = shift };
            if (type == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else if (type == EventType.MouseDrag) { using var e = PointerMoveEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
            else { using var e = PointerUpEvent.GetPooled(source); e.target = target; target.SendEvent(e); }
        }

        [Serializable] private sealed class InteractionBaseline
        {
            public string CapturedUtc, UnityVersion;
            public float PixelsPerPoint, SnapInterval, LeftWidth;
            public int FrameRate, TimeStepMode, AutoSaveSeconds, Groups, Tracks, Clips;
            public bool MagnetSnapping, ScrollWheelZooms;
            public Vector2 OriginalMinSize, NativeMinSize;
            public float OriginalWheelMax, NativeWheelMax, OriginalPanMin, NativePanMin;
            public float NativeScale, NativeHeaderHeight, NativeFooterHeight;
        }

        [UnityTest] public IEnumerator BaselineWindowNavigationRecordsCurrentPreferencesAndWheelPanResults()
        {
            var previousWindow = App.Window;
            var previousScroll = Styles.TimelineScrollPos;
            var originalWindow = ScriptableObject.CreateInstance<ActionEditorWindow>();
            JsonActionWorkbench nativeWindow = null;
            var baseline = new InteractionBaseline
            {
                CapturedUtc = DateTime.UtcNow.ToString("O"), UnityVersion = Application.unityVersion,
                PixelsPerPoint = EditorGUIUtility.pixelsPerPoint, SnapInterval = Prefs.SnapInterval,
                FrameRate = Prefs.FrameRate, TimeStepMode = (int)Prefs.timeStepMode, AutoSaveSeconds = Prefs.autoSaveSeconds,
                MagnetSnapping = Prefs.MagnetSnapping, ScrollWheelZooms = Prefs.scrollWheelZooms,
                Groups = App.AssetData.groups.Count, Tracks = App.AssetData.groups.Sum(g => g.Tracks.Count),
                Clips = App.AssetData.groups.SelectMany(g => g.Tracks).Sum(t => t.Clips.Count)
            };
            try
            {
                originalWindow.Show(); originalWindow.position = new Rect(60, 60, 1000, 600);
                originalWindow.Focus(); originalWindow.Repaint(); yield return null; yield return null; yield return null;
                baseline.OriginalMinSize = originalWindow.minSize;
                baseline.LeftWidth = Styles.TimelineLeftWidth;
                App.AssetData.ViewTimeMax = 8; App.AssetData.ViewTimeMin = 1;
                var point = new Vector2(Styles.TimelineLeftTotalWidth + 100, 100);
                originalWindow.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = point, delta = new Vector2(0, 120) });
                baseline.OriginalWheelMax = App.AssetData.ViewTimeMax;
                Assert.That(baseline.OriginalWheelMax, Is.EqualTo(8.525f).Within(.001f), "Original wheel keeps the left boundary and normalizes wheel magnitude.");
                Assert.That(App.AssetData.ViewTimeMin, Is.EqualTo(1));
                App.AssetData.ViewTimeMax = 8; App.AssetData.ViewTimeMin = 1;
                originalWindow.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 2 });
                originalWindow.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = point + new Vector2(20, 0), button = 2 });
                originalWindow.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point + new Vector2(20, 0), button = 2 });
                baseline.OriginalPanMin = App.AssetData.ViewTimeMin;
                Assert.That(baseline.OriginalPanMin, Is.EqualTo(1 - 20 * 7 / (1000 - Styles.TimelineLeftTotalWidth)).Within(.002f));
                Assert.That(App.AssetData.ViewTime, Is.EqualTo(7).Within(.001f));

                nativeWindow = ScriptableObject.CreateInstance<JsonActionWorkbench>();
                nativeWindow.Show(); nativeWindow.position = new Rect(60, 60, 1000, 600);
                nativeWindow.Focus(); yield return null; yield return null;
                baseline.NativeMinSize = nativeWindow.minSize;
                App.AssetData.ViewTimeMax = 8; App.AssetData.ViewTimeMin = 1;
                var canvas = nativeWindow.Canvas;
                point = new Vector2(canvas.Left + 100, 100);
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel,
                    mousePosition = canvas.LocalToWorld(point), delta = new Vector2(0, 120) }))
                { wheel.target = canvas; canvas.SendEvent(wheel); }
                baseline.NativeWheelMax = App.AssetData.ViewTimeMax;
                Assert.That(baseline.NativeWheelMax, Is.EqualTo(Prefs.scrollWheelZooms ? 9.575f : 8).Within(.001f), "Native wheel currently uses three times the original zoom increment.");
                Assert.That(App.AssetData.ViewTimeMin, Is.EqualTo(1));
                App.AssetData.ViewTimeMax = 8; App.AssetData.ViewTimeMin = 1;
                baseline.NativeScale = canvas.Scale;
                Pointer(canvas, EventType.MouseDown, point, 2);
                Pointer(canvas, EventType.MouseDrag, point + new Vector2(20, 0), 2);
                Pointer(canvas, EventType.MouseUp, point + new Vector2(20, 0), 2);
                baseline.NativePanMin = App.AssetData.ViewTimeMin;
                Assert.That(baseline.NativePanMin, Is.EqualTo(1 - 20 / baseline.NativeScale).Within(.001f));
                Assert.That(App.AssetData.ViewTime, Is.EqualTo(7).Within(.001f));
                baseline.NativeHeaderHeight = nativeWindow.rootVisualElement.Q("json-header").resolvedStyle.height;
                baseline.NativeFooterHeight = nativeWindow.rootVisualElement.Q("json-footer").resolvedStyle.height;
                foreach (var size in new[] { new Vector2(500, 250), new Vector2(1000, 600) })
                {
                    nativeWindow.position = new Rect(new Vector2(60, 60), size); yield return null; yield return null;
                    Assert.That(canvas.Scale, Is.EqualTo((size.x - canvas.Left) / App.AssetData.ViewTime).Within(.01f));
                    Assert.That(canvas.contentRect.height, Is.EqualTo(size.y - 56).Within(1));
                    Assert.That(nativeWindow.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                }
                Assert.That(App.Session.IsDirty || App.Session.IsEditing, Is.False);
                File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/rf01-interaction-baseline.json"), JsonUtility.ToJson(baseline, true));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (nativeWindow != null) { nativeWindow.DiscardChanges(); nativeWindow.Close(); }
                if (originalWindow != null) { originalWindow.DiscardChanges(); originalWindow.Close(); }
                App.Window = previousWindow; Styles.TimelineScrollPos = previousScroll;
            }
        }

        [UnityTest] public IEnumerator NativeDragResizeCancelAndCaptureLossUseRealPointerEvents()
        {
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1000, 600); window.Show(); yield return null; yield return null;
                var canvas = window.Canvas;
                var item = App.AssetData.groups[0].Tracks[0].Clips[0];
                var rect = canvas.ClipRect(item, 1); var center = rect.center;
                Pointer(canvas, EventType.MouseDrag, center + new Vector2(30, 0));
                Assert.That(item.StartTime, Is.EqualTo(.25f));
                Pointer(canvas, EventType.MouseDown, center);
                Pointer(canvas, EventType.MouseDrag, center + new Vector2(canvas.Scale, 0));
                Pointer(canvas, EventType.MouseUp, center + new Vector2(canvas.Scale, 0));
                Assert.That(item.StartTime, Is.EqualTo(1.3f).Within(.051f)); // configured 0.1-second snapping
                Undo.PerformUndo(); item = App.AssetData.groups[0].Tracks[0].Clips[0];
                Assert.That(item.StartTime, Is.EqualTo(.25f));
                rect = canvas.ClipRect(item, 1); center = rect.center;
                Pointer(canvas, EventType.MouseDown, center); Pointer(canvas, EventType.MouseDrag, center + new Vector2(30, 0));
                yield return null;
                var captured = (int)typeof(JsonTimelineCanvas).GetField("pointer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(canvas);
                Assert.That(captured, Is.GreaterThanOrEqualTo(0));
                Assert.That(App.Session.IsEditing, Is.True, "An open original window must not commit the native drag during repaint.");
                canvas.ReleasePointer(captured);
                Pointer(canvas, EventType.MouseDrag, center + new Vector2(30, 0));
                yield return null;
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                item = App.AssetData.groups[0].Tracks[0].Clips[0]; rect = canvas.ClipRect(item, 1);
                var edge = new Vector2(rect.xMax - 2, rect.center.y);
                Pointer(canvas, EventType.MouseDown, edge); Pointer(canvas, EventType.MouseDrag, edge + new Vector2(canvas.Scale, 0));
                Pointer(canvas, EventType.MouseUp, edge + new Vector2(canvas.Scale, 0));
                Assert.That(item.Length, Is.GreaterThan(2.4f));
                window.DiscardChanges();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator InactiveOriginalLifecycleDoesNotCancelNativeOwnedDrag()
        {
            var previousWindow = App.Window;
            ActionEditorWindow original = null; JsonActionWorkbench native = null;
            try
            {
                original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                native = ScriptableObject.CreateInstance<JsonActionWorkbench>(); native.Show();
                native.position = new Rect(50, 50, 1000, 600); native.Focus();
                yield return null; yield return null;
                var item = App.AssetData.groups[0].Tracks[0].Clips[0];
                var canvas = native.Canvas; var down = canvas.ClipRect(item, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                var moved = item.StartTime;
                Assert.That(moved, Is.GreaterThan(.25f));
                Assert.That(App.Session.IsEditing, Is.True);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(ActionEditorWindow).GetMethod("OnLostFocus", flags).Invoke(original, null);
                typeof(ActionEditorWindow).GetMethod("OnDisable", flags).Invoke(original, null);
                Assert.That(App.Session.IsEditing, Is.True, "An inactive view's disable handler must not roll back the native gesture.");
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(moved));
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale, 0));
                Assert.That(App.Session.IsEditing, Is.False);
                Undo.PerformUndo();
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                if (original != null) { original.DiscardChanges(); original.Close(); }
                App.Window = previousWindow;
            }
        }

        [Test] public void TransactionTokensRejectOtherOwnersAndStaleTokensAndCommitOneUndo()
        {
            var session = App.Session; var first = new object(); var other = new object();
            var token = session.BeginEdit(first);
            Assert.That(token, Is.Not.Null);
            Assert.That(session.BeginEdit(first), Is.SameAs(token));
            Assert.That(session.BeginEdit(other), Is.Null);
            ((LegacyExperienceAsset)App.AssetData).Version = 5;
            session.EndEdit(); session.CancelEdit();
            Assert.That(session.IsEditing, Is.True, "Legacy callers cannot finish another owner's transaction.");
            JsonEditCommands.Rename(App.AssetData.groups[0].Tracks[0], "nested-owned");
            Assert.That(token.IsActive, Is.True);
            token.Commit("Owned gesture"); token.Commit(); token.Cancel();
            Assert.That(session.IsEditing, Is.False);
            Undo.PerformUndo();
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
            Assert.That(App.AssetData.groups[0].Tracks[0].Name, Is.Not.EqualTo("nested-owned"));
            Undo.PerformRedo();
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(5));
            var next = session.BeginEdit(other);
            ((LegacyExperienceAsset)App.AssetData).Version = 6;
            token.Cancel(); token.Commit();
            Assert.That(next.IsActive, Is.True);
            next.Cancel(); next.Cancel();
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(5));
            Assert.That(next.IsActive, Is.False);
            Assert.That(next.Update(() => Assert.Fail("A stale token must not execute a mutation.")), Is.False);
        }

        [UnityTest] public IEnumerator NativeWrongButtonReleaseAndLostFocusRespectGestureOwnership()
        {
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600);
                yield return null; yield return null;
                var canvas = window.Canvas; var item = App.AssetData.groups[0].Tracks[0].Clips[0];
                var down = canvas.ClipRect(item, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                Assert.That(App.Session.IsEditing, Is.True, "The left drag must be active before the unrelated release.");
                Pointer(canvas, EventType.MouseUp, down, button: 1);
                Assert.That(App.Session.IsEditing, Is.True, "A right-button release cannot commit a left-button drag.");
                typeof(JsonActionWorkbench).GetMethod("OnLostFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(window, null);
                Assert.That(App.Session.IsEditing, Is.False);
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                item = App.AssetData.groups[0].Tracks[0].Clips[0]; down = canvas.ClipRect(item, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale, 0));
                Assert.That(App.Session.IsDirty, Is.True);
                Undo.PerformUndo(); Assert.That(App.Session.IsDirty, Is.False);
                item = App.AssetData.groups[0].Tracks[0].Clips[0]; down = canvas.ClipRect(item, 1).center;
                window.Select(item);
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale, 0));
                App.Session.DiscardChanges();
                Assert.That(App.Session.IsEditing, Is.False);
                Assert.That(App.SelectItems, Is.Empty, "Rollback must not retain references into the discarded graph.");
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * 2, 0));
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * 2, 0));
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                Assert.That(App.Session.IsDirty, Is.False);
                yield return null; yield return null;
                Assert.That(window.rootVisualElement.Q("json-drag-guides").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator InactiveNativeCancellationDoesNotTouchOriginalWindowToken()
        {
            var previousWindow = App.Window;
            ActionEditorWindow original = null; JsonActionWorkbench native = null;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                original = ScriptableObject.CreateInstance<ActionEditorWindow>(); original.Show();
                native = ScriptableObject.CreateInstance<JsonActionWorkbench>(); native.Show();
                yield return null; yield return null;
                var token = App.Session.BeginEdit(original);
                typeof(ActionEditorWindow).GetField("guiEdit", flags).SetValue(original, token);
                ((LegacyExperienceAsset)App.AssetData).Version = 17;
                native.Canvas.Cancel();
                typeof(JsonActionWorkbench).GetMethod("OnLostFocus", flags).Invoke(native, null);
                Assert.That(token.IsActive, Is.True);
                Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(17));
                typeof(ActionEditorWindow).GetMethod("OnLostFocus", flags).Invoke(original, null);
                Assert.That(token.IsActive, Is.False);
                Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
            }
            finally
            {
                if (native != null) { native.DiscardChanges(); native.Close(); }
                if (original != null) { original.DiscardChanges(); original.Close(); }
                App.Window = previousWindow;
            }
        }

        [Test] public void OwnedTransactionsRollbackNestedFailuresAndDoNotAddEmptyUndoSteps()
        {
            var token = App.Session.BeginEdit(this);
            ((LegacyExperienceAsset)App.AssetData).Version = 5;
            Assert.Throws<InvalidOperationException>(() => App.Session.Edit("Nested failure", () =>
            {
                ((LegacyExperienceAsset)App.AssetData).Version = 9;
                throw new InvalidOperationException("expected failure");
            }));
            Assert.That(token.IsActive, Is.False);
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
            App.Session.Edit("Real change", () => ((LegacyExperienceAsset)App.AssetData).Version = 2);
            App.Session.BeginEdit(this).Commit("No change");
            Undo.PerformUndo();
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
            var stale = App.Session.BeginEdit(this);
            App.TextAsset = fixture;
            Assert.That(stale.IsActive, Is.False);
            var active = App.Session.BeginEdit(this);
            stale.Cancel(); Assert.That(active.IsActive, Is.True);
            App.Session.DiscardChanges();
            Assert.That(active.IsActive, Is.False);
            var partial = App.Session.BeginEdit(this);
            Assert.Throws<InvalidOperationException>(() => partial.Update(() =>
            {
                ((LegacyExperienceAsset)App.AssetData).Version = 99;
                throw new InvalidOperationException("partial update");
            }));
            Assert.That(partial.IsActive, Is.False);
            Assert.That(((LegacyExperienceAsset)App.AssetData).Version, Is.EqualTo(1));
            var invalid = App.Session.BeginEdit(this);
            App.AssetData.groups[0].Tracks[0].Clips.Add(null);
            Assert.Throws<System.IO.InvalidDataException>(() => invalid.Commit());
            Assert.That(invalid.IsActive, Is.False);
            Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
            Assert.That(App.Session.IsDirty, Is.False);
        }

        [UnityTest] public IEnumerator NativeMultiClipDragGuidesMatchBoundsSnappingAndGestureLifetime()
        {
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600); yield return null; yield return null;
                var canvas = window.Canvas; var root = window.rootVisualElement;
                var first = App.AssetData.groups[0].Tracks[0].Clips[0]; var second = App.AssetData.groups[0].Tracks[1].Clips[0];
                window.Select(first, second); yield return null;
                var overlay = root.Q("json-drag-guides");
                var startGuide = overlay.Q("json-drag-guide-0"); var endGuide = overlay.Q("json-drag-guide-1");
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                var down = canvas.ClipRect(first, 1).center;
                Pointer(canvas, EventType.MouseDown, down);
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .12f, 0), shift: true);
                window.SyncView(); yield return null; yield return null;
                Assert.That(App.SelectItems.Count(), Is.EqualTo(2));
                Assert.That(second.StartTime - first.StartTime, Is.EqualTo(.75f).Within(.0001f));
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(startGuide.Q<Label>().text, Is.EqualTo(first.StartTime.ToString("0.00")));
                Assert.That(endGuide.Q<Label>().text, Is.EqualTo(second.EndTime.ToString("0.00")));
                Assert.That(startGuide.ClassListContains("json-guide-solid"), Is.False);
                Assert.That(endGuide.ClassListContains("json-guide-solid"), Is.False);
                Assert.That(startGuide.worldBound.xMin - root.worldBound.xMin, Is.EqualTo(canvas.X(first.StartTime)).Within(1));
                Assert.That(overlay.worldBound.yMin - root.worldBound.yMin, Is.EqualTo(20).Within(1));
                Assert.That(overlay.worldBound.yMax - root.worldBound.yMin, Is.EqualTo(root.contentRect.height - 16).Within(1));
                window.Repaint(); yield return null; Capture(window, "shared-json-native-multi-drag-dashed.png");
                Pointer(canvas, EventType.MouseDrag, down + new Vector2(canvas.Scale * .75f, 0));
                window.SyncView(); yield return null; yield return null;
                Assert.That(first.StartTime, Is.EqualTo(1).Within(.0001f));
                Assert.That(startGuide.ClassListContains("json-guide-solid"), Is.True);
                window.Repaint(); yield return null; Capture(window, "shared-json-native-multi-drag-snapped.png");
                Pointer(canvas, EventType.MouseUp, down + new Vector2(canvas.Scale * .75f, 0)); yield return null;
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Undo.PerformUndo(); yield return null;
                first = App.AssetData.groups[0].Tracks[0].Clips[0];
                Assert.That(first.StartTime, Is.EqualTo(.25f));
                Assert.That(App.AssetData.groups[0].Tracks[1].Clips[0].StartTime, Is.EqualTo(1));
                window.Select(first); yield return null;
                foreach (var left in new[] { true, false })
                {
                    first = App.AssetData.groups[0].Tracks[0].Clips[0]; var rect = canvas.ClipRect(first, 1);
                    var edge = new Vector2(left ? rect.xMin + 2 : rect.xMax - 2, rect.center.y);
                    Pointer(canvas, EventType.MouseDown, edge); Pointer(canvas, EventType.MouseDrag, edge + new Vector2(canvas.Scale * .12f, 0), shift: true);
                    yield return null; yield return null;
                    Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(startGuide.resolvedStyle.display, Is.EqualTo(left ? DisplayStyle.Flex : DisplayStyle.None));
                    Assert.That(endGuide.resolvedStyle.display, Is.EqualTo(left ? DisplayStyle.None : DisplayStyle.Flex));
                    if (left)
                    {
                        using var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                        escape.target = canvas; canvas.SendEvent(escape);
                    }
                    else
                    {
                        var captured = (int)typeof(JsonTimelineCanvas).GetField("pointer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(canvas);
                        canvas.ReleasePointer(captured); Pointer(canvas, EventType.MouseDrag, edge); yield return null;
                    }
                    yield return null;
                    Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                    Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].Length, Is.EqualTo(1.5f));
                }
                canvas.Dispose(); Assert.That(root.Q("json-drag-guides"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator NativeClipEdgesShowResizeCursorAndClearItOutsideEditableEdges()
        {
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                window.Show(); window.position = new Rect(50, 50, 1000, 600); yield return null; yield return null;
                var canvas = window.Canvas; var clip = App.AssetData.groups[0].Tracks[0].Clips[0]; window.Select(clip);
                yield return null;
                var rect = canvas.ClipRect(clip, 1);
                Pointer(canvas, EventType.MouseDrag, rect.center); yield return null;
                var normal = ComputedCursor(canvas);
                foreach (var x in new[] { rect.xMin + 2, rect.xMax - 2 })
                {
                    Pointer(canvas, EventType.MouseDrag, new Vector2(x, rect.center.y)); yield return null;
                    Assert.That(canvas.ClassListContains("json-resize-horizontal"), Is.True);
                    Assert.That(ComputedCursor(canvas), Is.Not.EqualTo(normal), "USS must apply a real editor resize cursor, not just a class name.");
                }
                Pointer(canvas, EventType.MouseDrag, new Vector2(rect.xMin + Styles.ClipScaleRectWidth + 1, rect.center.y)); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                var marker = App.AssetData.groups[0].Tracks[2].Clips[0];
                Pointer(canvas, EventType.MouseDrag, canvas.ClipRect(marker, 3).center); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                var edge = new Vector2(rect.xMax - 2, rect.center.y);
                Pointer(canvas, EventType.MouseDown, edge); Pointer(canvas, EventType.MouseDrag, new Vector2(canvas.contentRect.width + 20, rect.center.y)); yield return null;
                Assert.That(canvas.ClassListContains("json-resize-horizontal"), Is.True, "Resizing retains its cursor outside the clip.");
                canvas.Cancel(); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                clip = App.AssetData.groups[0].Tracks[0].Clips[0]; rect = canvas.ClipRect(clip, 1);
                Pointer(canvas, EventType.MouseDrag, new Vector2(rect.xMin + 2, rect.center.y)); yield return null;
                canvas.Cancel();
                Assert.That(canvas.ClassListContains("json-resize-horizontal"), Is.False);
                JsonEditCommands.ToggleLock(App.AssetData.groups[0].Tracks[0]); yield return null;
                Pointer(canvas, EventType.MouseDrag, new Vector2(rect.xMin + 2, rect.center.y)); yield return null;
                Assert.That(ComputedCursor(canvas), Is.EqualTo(normal));
                Assert.That(App.Session.IsEditing, Is.False); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        private static object ComputedCursor(VisualElement element)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            // Unity 2022 does not expose cursor through IResolvedStyle.
            var field = typeof(VisualElement).GetFields(flags).FirstOrDefault(f => f.FieldType.Name == "ComputedStyle");
            Assert.That(field, Is.Not.Null, "Computed style storage must be available for cursor verification.");
            var computed = field.GetValue(element);
            return computed.GetType().GetProperty("cursor", flags).GetValue(computed);
        }

        [UnityTest] public IEnumerator RulerContinuousSeekAndEscapeRestoreTime()
        {
            var window = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1000, 600); window.Show(); yield return null; yield return null;
                var root = window.rootVisualElement; var canvas = window.Canvas;
                window.Seek(.5f);
                Pointer(root, EventType.MouseDown, new Vector2(canvas.X(1), 30));
                Assert.That((int)typeof(JsonTimelineCanvas).GetField("mode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(canvas), Is.EqualTo(7), "ruler down mode");
                Pointer(root, EventType.MouseDrag, new Vector2(canvas.X(2), 30));
                Assert.That((int)typeof(JsonTimelineCanvas).GetField("mode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(canvas), Is.EqualTo(7), "ruler move mode");
                Assert.That((float)typeof(JsonTimelineCanvas).GetField("seekBefore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(canvas), Is.EqualTo(.5f), "ruler snapshot");
                Assert.That(AssetPlayer.Inst.CurrentTime, Is.EqualTo(2).Within(.051));
                var receivedKey = false;
                root.RegisterCallback<KeyDownEvent>(_ => receivedKey = true, TrickleDown.TrickleDown);
                using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                { escape.target = canvas; canvas.SendEvent(escape); }
                yield return null;
                Assert.That(receivedKey, Is.True, "Escape must reach the real window panel");
                Assert.That(AssetPlayer.Inst.CurrentTime, Is.EqualTo(.5f));
                Assert.That(App.Session.IsDirty, Is.False);
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator SharedLayoutEvidenceCapturesBothOwnedWindows()
        {
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            var native = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            var previousWindow = App.Window;
            var previousLeft = Styles.TimelineLeftWidth; var previousScroll = Styles.TimelineScrollPos;
            try
            {
                Styles.TimelineLeftWidth = 320; Styles.TimelineScrollPos = Vector2.zero;
                App.AssetData.ViewTimeMin = 0; App.AssetData.ViewTimeMax = 7; App.Stop(); App.IsRange = false; App.SelectWithoutInspector();
                original.Show(); original.position = new Rect(60, 60, 1000, 600);
                yield return null; yield return null; yield return null; original.Repaint(); yield return null;
                Capture(original, "shared-json-original.png");
                native.Show(); native.position = new Rect(60, 60, 1000, 600);
                native.LeftWidth = 320; native.ScrollY = 0; native.Refresh();
                yield return null; yield return null; yield return null; native.Repaint(); yield return null;
                Capture(native, "shared-json-native.png");
                Assert.That(native.rootVisualElement.Q("json-transport").resolvedStyle.width, Is.EqualTo(320).Within(1));
                Assert.That(native.rootVisualElement.Q("json-document-toolbar").worldBound.xMin - native.rootVisualElement.worldBound.xMin, Is.EqualTo(325).Within(1));
                Assert.That(native.Canvas.Scale, Is.EqualTo((1000 - 329) / 7f).Within(1));
                Assert.That(native.rootVisualElement.Q("json-loop-start"), Is.Null);
                Assert.That(native.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(native.rootVisualElement.Q("json-custom-header").Q<Button>().text, Is.EqualTo("样例状态预览"));
                foreach (var width in new[] { 850, 1024, 1400 })
                {
                    native.position = new Rect(60, 60, width, 600); yield return null; yield return null;
                    Assert.That(native.Canvas.contentRect.width, Is.GreaterThan(width - 25));
                    Assert.That(native.rootVisualElement.Q("json-ruler").worldBound.xMin - native.rootVisualElement.worldBound.xMin, Is.EqualTo(native.Canvas.Left).Within(1));
                }
                original.position = native.position = new Rect(60, 60, 1000, 600);
                var selected = App.AssetData.groups[0].Tracks[0].Clips[0]; App.SelectWithoutInspector(selected);
                App.Play(); App.Pause(true); AssetPlayer.Inst.Sample(1); App.IsRange = true;
                original.Focus(); original.Repaint(); yield return null; yield return null;
                Capture(original, "shared-json-original-selected.png");
                native.Refresh(); native.Focus(); native.Repaint(); yield return null; yield return null;
                Capture(native, "shared-json-native-selected.png");
                App.Stop(); App.IsRange = false;
                App.AssetData.groups[2].IsCollapsed = false; App.Refresh();
                original.position = native.position = new Rect(60, 60, 1000, 240);
                Styles.TimelineScrollPos = new Vector2(0, 60); native.ScrollY = 60;
                original.Focus(); original.Repaint(); yield return null; yield return null;
                Styles.TimelineScrollPos = new Vector2(0, 60); original.Repaint(); yield return null; yield return null;
                Capture(original, "shared-json-original-scrolled.png");
                native.Refresh(); native.Focus(); native.Repaint(); yield return null; yield return null;
                native.ScrollY = 60; native.SyncView(); native.Repaint(); yield return null; yield return null;
                Capture(native, "shared-json-native-scrolled.png");
                Assert.That(native.ScrollY, Is.EqualTo(60).Within(.01f));
                Assert.That(native.rootVisualElement.Q<Scroller>().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(native.Canvas.Scale, Is.EqualTo((1000 - 329) / App.AssetData.ViewTime).Within(1));
            }
            finally { original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close(); App.Window = previousWindow; Styles.TimelineLeftWidth = previousLeft; Styles.TimelineScrollPos = previousScroll; }
        }

        [UnityTest] public IEnumerator OriginalInspectorWelcomeAndSettingsHaveNativeCounterparts()
        {
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>(); var native = ScriptableObject.CreateInstance<JsonActionWorkbench>();
            var originalProperty = ScriptableObject.CreateInstance<PropertyEvidenceWindow>(); originalProperty.Original = true;
            var nativeProperty = ScriptableObject.CreateInstance<PropertyEvidenceWindow>(); var preferences = ScriptableObject.CreateInstance<JsonPreferencesWindow>();
            var previousWindow = App.Window;
            try
            {
                native.Show(); native.position = new Rect(60, 60, 1000, 600); yield return null; yield return null;
                native.Select(App.AssetData.groups[0].Tracks[0].Clips[0]);
                originalProperty.Show(); originalProperty.position = new Rect(60, 60, 640, 850); originalProperty.Initialize(native);
                originalProperty.Repaint(); yield return null; yield return null;
                Capture(originalProperty, "shared-json-original-inspector.png");
                nativeProperty.Show(); nativeProperty.position = new Rect(60, 60, 640, 850); nativeProperty.Initialize(native);
                yield return null; yield return null; Capture(nativeProperty, "shared-json-native-inspector.png");
                Assert.That(nativeProperty.rootVisualElement.Q("json-clip-timing").resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
                Assert.That(nativeProperty.rootVisualElement.Q("json-clip-blending").resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
                var frames = Prefs.timeStepMode == Prefs.TimeStepMode.Frames;
                if (frames) nativeProperty.rootVisualElement.Q<IntegerField>("json-clip-length").value = Mathf.RoundToInt(1.2f * Prefs.FrameRate);
                else nativeProperty.rootVisualElement.Q<FloatField>("json-clip-length").value = 1.2f;
                yield return null;
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].Length, Is.EqualTo(1.2f).Within(Prefs.SnapInterval));
                Undo.PerformUndo(); yield return null;
                Assert.That(App.AssetData.groups[0].Tracks[0].Clips[0].Length, Is.EqualTo(1.5f));
                preferences.Show(); preferences.position = new Rect(60, 60, 400, 310); yield return null; yield return null;
                Assert.That(preferences.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(preferences.rootVisualElement.Query<Toggle>().ToList().Count, Is.EqualTo(2));
                Capture(preferences, "shared-json-native-settings.png"); preferences.Close();
                App.TextAsset = null; original.Show(); original.position = new Rect(60, 60, 1000, 600);
                original.Repaint(); yield return null; yield return null; Capture(original, "shared-json-original-welcome.png");
                native.Refresh(); native.Focus(); yield return null; yield return null;
                Assert.That(native.rootVisualElement.Q("json-header").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                var welcome = native.rootVisualElement.Q("json-welcome");
                Assert.That(welcome.resolvedStyle.alignItems, Is.EqualTo(Align.Center));
                Assert.That(welcome.Query<Button>().ToList().Count, Is.EqualTo(3));
                Capture(native, "shared-json-native-welcome.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                originalProperty.Close(); nativeProperty.Close(); if (preferences != null) preferences.Close();
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close(); App.Window = previousWindow;
            }
        }
        private static void Capture(EditorWindow window, string name)
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
                File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Temp/" + name), snapshot.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(snapshot); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(upright); RenderTexture.ReleaseTemporary(raw); }
        }

        [UnityTest] public IEnumerator OriginalRebuildDoesNotLeakRegisteredViews()
        {
            var previousWindow = App.Window;
            var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
            try
            {
                window.Show(); window.Focus(); App.Refresh(); window.Repaint(); yield return null; yield return null;
                window.Repaint(); yield return null; yield return null;
                Assert.That(window.RegisteredViewCount(), Is.EqualTo(7 + App.AssetData.groups.Sum(g => 1 + (g.IsCollapsed ? 0 : g.Tracks.Count)) * 2));
                var baseline = window.RegisteredViewCount();
                for (var i = 0; i < 5; i++) { App.Refresh(); window.Repaint(); yield return null; yield return null; }
                Assert.That(window.RegisteredViewCount(), Is.EqualTo(baseline));
                window.DiscardChanges();
            }
            finally { window.DiscardChanges(); window.Close(); App.Window = previousWindow; }
            Assert.That(window.RegisteredViewCount(), Is.Zero);
        }

        private sealed class PointerView : ViewBase, NBC.ActionEditor.Events.IDragBeginHandler, NBC.ActionEditor.Events.IDragEndHandler
        {
            public int Started, Ended;
            public override void OnDraw() { }
            public void OnDragBegin(NBC.ActionEditor.Events.PointerEventData e) => Started++;
            public void OnDragEnd(NBC.ActionEditor.Events.PointerEventData e) => Ended++;
            public void Send(Event e) { Position = new Rect(0, 0, 100, 100); CheckPointerEvent(e); }
        }
        [Test] public void OriginalInputNeedsPressAndEndsOutside()
        {
            var old = Event.current; var view = new PointerView();
            try
            {
                view.Send(new Event { type = EventType.MouseDrag, mousePosition = new Vector2(20, 20), button = 0 }); Assert.That(view.Started, Is.Zero);
                view.Send(new Event { type = EventType.MouseDown, mousePosition = new Vector2(20, 20), button = 0 });
                view.Send(new Event { type = EventType.MouseDrag, mousePosition = new Vector2(21, 20), button = 0 }); Assert.That(view.Started, Is.Zero, "One pixel is not yet a drag.");
                view.Send(new Event { type = EventType.MouseDrag, mousePosition = new Vector2(150, 20), button = 0 }); Assert.That(view.Started, Is.EqualTo(1));
                view.Send(new Event { type = EventType.MouseUp, mousePosition = new Vector2(150, 20), button = 1 }); Assert.That(view.Ended, Is.Zero);
                view.Send(new Event { type = EventType.MouseUp, mousePosition = new Vector2(150, 20), button = 0 }); Assert.That(view.Ended, Is.EqualTo(1));
                view.Send(new Event { type = EventType.MouseDown, mousePosition = new Vector2(20, 20), button = 0 });
                typeof(ViewBase).GetMethod("ResetPointerState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                view.Send(new Event { type = EventType.MouseDrag, mousePosition = new Vector2(150, 20), button = 0 }); Assert.That(view.Started, Is.EqualTo(1));
            }
            finally { Event.current = old; }
        }
    }
}
