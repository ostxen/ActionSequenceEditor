using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Composition;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using EditorSettings = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Tests
{
    // 两个真实窗口分别执行同一矩阵，独立作者会话不接管用户文档和个人偏好。
    public sealed class StructureInputMatrixTests
    {
        private sealed class Preferences : IPreferencesStore
        {
            public EditorSettings Read() => new EditorSettings(TimeStepMode.Seconds, .1f, 30, true, true, 0, "Assets/");
            public void Write(EditorSettings value) => throw new InvalidOperationException("测试不得写个人偏好。");
        }

        [Serializable, Unique, Attachable(typeof(FeatureSampleGroup))]
        private sealed class UniqueTrack : TimelineTrack { }

        // 仅显式注入测试类型目录，不能把测试业务加入生产注册表。
        private sealed class TestSnapshots : IDocumentSnapshot
        {
            private readonly DocumentFormatContext context = DocumentFormats.CreateContext(typeof(UniqueTrack));
            private readonly ActionDocumentJsonFormat format = new ActionDocumentJsonFormat();
            public string Capture(ActionDocument document) => System.Text.Encoding.UTF8.GetString(format.Serialize(
                DocumentSnapshots.Capture(document, false, context), new DocumentFormatOptions(false), context));
            public ActionDocument Restore(string snapshot) => format.Deserialize(System.Text.Encoding.UTF8.GetBytes(snapshot), context);
            public string Content(ActionDocument document) => DocumentSnapshots.Content(document, context);
            public void ValidateAndInitialize(ActionDocument document) => ActionDocumentGraph.Initialize(document, context);
        }

        internal sealed class WindowInput : IDisposable
        {
            public readonly ActionEditorContext Context;
            public readonly ActionEditorViews Views = new ActionEditorViews();
            public readonly SettingsSession Settings = new SettingsSession(new Preferences());
            public readonly ImguiWindow Imgui;
            public readonly ToolkitWindow Toolkit;
            private readonly int globalViews;
            private readonly IDocumentSnapshot snapshots;
            private bool closed;
            public EditorWindow Window => (EditorWindow)Imgui ?? Toolkit;
            public TimelinePresenter Presenter => Imgui != null ? Imgui.Presenter : Toolkit.Canvas.Presenter;
            public TimelineWorkspace Workspace => Presenter.Workspace;
            public string Content => snapshots.Content(Context.Document);
            public string Error => Imgui != null ? Imgui.Timeline.Error : Toolkit.LastError;
            public TimelineGroup Group(string name) => Context.Document.RootItems.OfType<TimelineGroup>().Single(g => g.Name == name);
            public TimelineTrack Track(string name) => Context.Document.AllTracks.Single(t => t.Name == name);
            public float Y(IDirectable item, float fraction = .5f) => Presenter.State.Rows.Single(r => r.Key == Presenter.KeyFor(item)).Bounds.Y + fraction * 28;

            public WindowInput(bool toolkit, int height = 650, bool unique = false)
            {
                globalViews = ActionEditorHost.Views.Count;
                snapshots = unique ? (IDocumentSnapshot)new TestSnapshots() : new DocumentSnapshot();
                Context = new ActionEditorContext(new DocumentSession(snapshots, new JsonDocumentRepository(), new UnityUndoHistory(), new RecoveryCodec()));
                var document = new FeatureSampleDocument();
                for (var i = 0; i < 3; i++)
                {
                    var group = (TimelineGroup)document.RootItems[i]; group.Name = ((char)('A' + i)).ToString();
                    for (var j = 0; j < group.Tracks.Count; j++) group.Tracks[j].Name = group.Name + j;
                }
                document.AddRoot(new FeatureSampleIntervalTrack { Name = "R" });
                document.AddRoot(new FeatureSampleGroup { Name = "E" });
                if (unique)
                {
                    ((TimelineGroup)document.RootItems[0]).Tracks.Add(new UniqueTrack { Name = "U0" });
                    ((TimelineGroup)document.RootItems[1]).Tracks.Add(new UniqueTrack { Name = "U1" });
                    document.AddRoot(new TimelineGroup { Name = "N" });
                }
                document.Validate();
                Context.Session.Open(new DocumentOpenData(document));
                if (toolkit)
                {
                    Toolkit = ScriptableObject.CreateInstance<ToolkitWindow>();
                    Toolkit.BindContext(Context, Views, Settings);
                }
                else
                {
                    Imgui = ScriptableObject.CreateInstance<ImguiWindow>();
                    Imgui.BindContext(Context, Views, Settings);
                }
                // Show可能恢复同类型上次窗口尺寸，测试尺寸必须在显示后设定。
                Window.Show(); Window.position = new Rect(60, 60, 1000, height); Window.Focus(); Window.Repaint();
            }

            // 只转换真实主体坐标，不直接调用Presenter.Pointer或结构编辑用例。
            public void Pointer(EventType type, Vector2 local, int button = 0)
            {
                if (Imgui != null)
                {
                    var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Imgui);
                    Imgui.SendEvent(new Event { type = type, button = button, mousePosition = local + offset + Vector2.up * 40 });
                    return;
                }
                var canvas = Toolkit.Canvas;
                var source = new Event { type = type, button = button, mousePosition = canvas.LocalToWorld(local) };
                if (type == EventType.MouseDown) { using var e = PointerDownEvent.GetPooled(source); e.target = canvas; canvas.SendEvent(e); }
                else if (type == EventType.MouseDrag) { using var e = PointerMoveEvent.GetPooled(source); e.target = canvas; canvas.SendEvent(e); }
                else { using var e = PointerUpEvent.GetPooled(source); e.target = canvas; canvas.SendEvent(e); }
            }
            public void Key(KeyCode key, bool control = false, bool shift = false)
            {
                var source = new Event { type = EventType.KeyDown, keyCode = key, control = control, shift = shift };
                if (Imgui != null) { Imgui.SendEvent(source); return; }
                using var e = KeyDownEvent.GetPooled(source); e.target = Toolkit.Canvas; Toolkit.Canvas.SendEvent(e);
            }
            public void Begin(IDirectable source, Vector2 end)
            {
                var start = new Vector2(90, Y(source));
                Pointer(EventType.MouseDown, start);
                Assert.That(Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure), "标题按下必须进入本窗口结构候选。");
                Pointer(EventType.MouseDrag, start + Vector2.right * 4);
                Pointer(EventType.MouseDrag, end);
            }
            public void Drag(IDirectable source, Vector2 end)
            {
                var before = Content;
                Begin(source, end);
                Assert.That(Content, Is.EqualTo(before), "松开前不能改作者图。");
                Assert.That(Presenter.State.Insertion.Visible && Presenter.State.Insertion.Valid, Is.True,
                    $"source={source.Name}, end={end}, size={Presenter.State.Size.Width}x{Presenter.State.Size.Height}, window={Window.position}, scroll={Workspace.ScrollY}, mode={Presenter.Mode}, line={Presenter.State.Insertion.Y}, valid={Presenter.State.Insertion.Valid}");
                Assert.That(Presenter.State.Insertion.Width, Is.EqualTo(Presenter.State.Size.Width));
                Pointer(EventType.MouseUp, end);
                Assert.That(Error, Is.Null); AssertReleased();
            }
            public void AssertReleased()
            {
                Assert.That(Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
                Assert.That(Presenter.State.Insertion.Visible || Context.Session.IsEditing, Is.False);
                if (Toolkit != null) Assert.That(Toolkit.Canvas.PointerId, Is.EqualTo(-1));
                else Assert.That(GUIUtility.hotControl, Is.Zero);
            }
            public void Roots(params string[] names) => Assert.That(Context.Document.RootItems.Cast<IDirectable>().Select(i => i.Name), Is.EqualTo(names));
            public void Tracks(string group, params string[] names) => Assert.That(Group(group).Tracks.Select(t => t.Name), Is.EqualTo(names));
            public void Close()
            {
                if (closed) return; closed = true;
                // 只放弃隔离会话，永不调用全局作者会话的Discard。
                if (Imgui != null) Imgui.DiscardChanges(); else Toolkit.DiscardChanges();
                Window.Close(); Assert.That(Views.Count, Is.Zero);
            }
            public void Dispose()
            {
                try { Close(); }
                finally { Context.Dispose(); }
                Assert.That(ActionEditorHost.Views.Count, Is.EqualTo(globalViews));
            }
        }

        [UnityTest] public IEnumerator ImguiGroupPositionsWholeBlocksAndNoOps() => GroupMatrix(false);
        [UnityTest] public IEnumerator ToolkitGroupPositionsWholeBlocksAndNoOps() => GroupMatrix(true);

        private static IEnumerator GroupMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            yield return null; yield return null; yield return null;
            var baseline = h.Content;
            var cases = new[]
            {
                ("A", "B1", .9f, new[] { "B", "A", "C", "R", "E" }),
                ("E", "A", .1f, new[] { "E", "A", "B", "C", "R" }),
                ("B", "C1", .9f, new[] { "A", "C", "B", "R", "E" }),
                ("B", "A0", .1f, new[] { "B", "A", "C", "R", "E" }),
                ("A", "C0", .1f, new[] { "B", "A", "C", "R", "E" }),
                ("C", "A2", .9f, new[] { "A", "C", "B", "R", "E" }),
                ("E", "R", .1f, new[] { "A", "B", "C", "E", "R" }),
                ("A", "E", .9f, new[] { "B", "C", "R", "E", "A" })
            };
            foreach (var c in cases)
            {
                var target = c.Item2.Length == 2 ? (IDirectable)h.Track(c.Item2) : c.Item2 == "R" ? h.Track("R") : h.Group(c.Item2);
                var children = h.Group(c.Item1).Tracks.ToArray();
                h.Drag(h.Group(c.Item1), new Vector2(90, h.Y(target, c.Item3)));
                h.Roots(c.Item4); Assert.That(h.Group(c.Item1).Tracks, Is.EqualTo(children));
                var after = h.Content;
                h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(baseline), "一次Undo恢复整块与所有参数。");
                h.Key(KeyCode.Z, control: true, shift: true); Assert.That(h.Content, Is.EqualTo(after));
                h.Key(KeyCode.Z, control: true); Assert.That(h.Context.Session.IsDirty, Is.False);
                h.Window.Repaint(); yield return null;
            }
            h.Workspace.SetCollapsed(h.Group("B"), true); h.Presenter.Refresh();
            h.Drag(h.Group("B"), new Vector2(90, h.Y(h.Group("A"), .1f)));
            h.Roots("B", "A", "C", "R", "E");
            Assert.That(h.Workspace.IsCollapsed(h.Group("B")), Is.True);
            h.Key(KeyCode.Z, control: true); h.Window.Repaint(); yield return null;
            foreach (var fraction in new[] { .1f, .9f })
            {
                var version = h.Context.Session.Version;
                h.Drag(h.Group("E"), new Vector2(90, h.Y(h.Group("E"), fraction)));
                Assert.That(h.Content, Is.EqualTo(baseline));
                Assert.That(h.Context.Session.Version, Is.EqualTo(version));
                Assert.That(h.Context.Session.IsDirty, Is.False);
            }
        }

        [UnityTest] public IEnumerator ImguiTrackBoundariesMixedRootsAndContinuousUndo() => TrackMatrix(false);
        [UnityTest] public IEnumerator ToolkitTrackBoundariesMixedRootsAndContinuousUndo() => TrackMatrix(true);

        private static IEnumerator TrackMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            yield return null; yield return null; yield return null;
            var initial = h.Content;
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("A2"), .9f)));
            h.Tracks("A", "A1", "A2", "A0"); var down = h.Content;
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("A1"), .1f)));
            h.Tracks("A", "A0", "A1", "A2"); Assert.That(h.Content, Is.EqualTo(initial));
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(down));
            h.Key(KeyCode.Z, control: true, shift: true); Assert.That(h.Content, Is.EqualTo(initial));
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Group("B"))));
            h.Tracks("B", "A0", "B0", "B1"); var crossFirst = h.Content;
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("B1"), .9f)));
            h.Tracks("B", "B0", "B1", "A0"); var crossLast = h.Content;
            h.Drag(h.Track("A0"), new Vector2(19, h.Y(h.Group("B"), .9f)));
            h.Roots("A", "B", "A0", "C", "R", "E"); Assert.That(h.Track("A0").Parent, Is.Null);
            var detached = h.Content;
            h.Workspace.SetCollapsed(h.Group("B"), true); h.Presenter.Refresh();
            h.Drag(h.Track("A0"), new Vector2(20, h.Y(h.Group("B"))));
            h.Tracks("B", "A0", "B0", "B1"); Assert.That(h.Workspace.IsCollapsed(h.Group("B")), Is.False);
            Assert.That(h.Content, Is.EqualTo(crossFirst));
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(detached));
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(crossLast));
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(crossFirst));
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(initial));
            for (var i = 0; i < 4; i++) h.Key(KeyCode.Z, control: true, shift: true);
            Assert.That(h.Content, Is.EqualTo(crossFirst));
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Group("E"))));
            h.Tracks("E", "A0"); h.Tracks("B", "B0", "B1");
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("R"), .1f)));
            h.Roots("A", "B", "C", "A0", "R", "E");
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("R"), .9f)));
            h.Roots("A", "B", "C", "R", "A0", "E");
            foreach (var fraction in new[] { .1f, .9f })
            {
                var version = h.Context.Session.Version; var before = h.Content;
                h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("A0"), fraction)));
                Assert.That(h.Content, Is.EqualTo(before)); Assert.That(h.Context.Session.Version, Is.EqualTo(version));
            }
            var beforeAppend = h.Content;
            h.Drag(h.Track("A0"), new Vector2(90, h.Presenter.State.Rows.Count * 28 + 15));
            h.Roots("A", "B", "C", "R", "E", "A0");
            // 原位上下两边界不能生成空Undo，下一次Undo必须直接撤销追加。
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("A0"), .1f)));
            h.Drag(h.Track("A0"), new Vector2(90, h.Y(h.Track("A0"), .9f)));
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(beforeAppend));
        }

        [UnityTest] public IEnumerator ImguiCancelOwnershipAndReplacementMatrix() => CancelMatrix(false);
        [UnityTest] public IEnumerator ToolkitCancelOwnershipAndReplacementMatrix() => CancelMatrix(true);

        private static IEnumerator CancelMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            yield return null; yield return null; yield return null;
            var baseline = h.Content;
            var end = new Vector2(90, h.Y(h.Group("B")));
            var press = new Vector2(90, h.Y(h.Track("A0")));
            h.Pointer(EventType.MouseDown, press); h.Pointer(EventType.MouseDrag, press + Vector2.right * 3);
            Assert.That(h.Presenter.State.Insertion.Visible, Is.False, "正好3px仍为候选，不得开始拖动。");
            h.Pointer(EventType.MouseUp, press + Vector2.right * 3); h.AssertReleased();
            Assert.That(h.Context.Session.IsDirty, Is.False);
            h.Begin(h.Track("A0"), end);
            h.Pointer(EventType.MouseUp, end, 1);
            Assert.That(h.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
            Assert.That(h.Content, Is.EqualTo(baseline));
            if (h.Toolkit != null) Assert.That(h.Toolkit.Canvas.HasPointerCapture(h.Toolkit.Canvas.PointerId), Is.True);
            else Assert.That(GUIUtility.hotControl, Is.Not.Zero);
            h.Key(KeyCode.Escape); h.AssertReleased();
            h.Begin(h.Track("A0"), end); h.Pointer(EventType.MouseUp, new Vector2(-1, end.y));
            h.AssertReleased(); Assert.That(h.Content, Is.EqualTo(baseline));
            h.Begin(h.Track("A0"), end);
            if (h.Toolkit != null)
            {
                yield return null;
                Assert.That(h.Toolkit.Canvas.HasPointerCapture(h.Toolkit.Canvas.PointerId), Is.True);
                h.Toolkit.Canvas.ReleasePointer(h.Toolkit.Canvas.PointerId);
                h.Pointer(EventType.MouseDrag, end);
                yield return null;
            }
            else
            {
                GUIUtility.hotControl = 0;
                h.Pointer(EventType.MouseDrag, end);
            }
            h.AssertReleased(); Assert.That(h.Content, Is.EqualTo(baseline));
            h.Begin(h.Track("A0"), end);
            // 实际焦点切换触发窗口OnLostFocus，不直接调用取消方法。
            var other = ScriptableObject.CreateInstance<FocusWindow>();
            try { other.Show(); other.Focus(); yield return null; yield return null; h.AssertReleased(); }
            finally { other.Close(); }
            h.Window.Focus(); yield return null;
            h.Begin(h.Track("A0"), end);
            h.Context.Session.Open(new DocumentOpenData(new FeatureSampleDocument()));
            h.AssertReleased(); Assert.That(h.Context.Session.IsDirty, Is.False);
            h.Window.Repaint(); yield return null;
            end = new Vector2(90, h.Y(h.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(1)));
            h.Begin(h.Context.Document.AllTracks.First(), end);
            h.Close(); Assert.That(h.Context.Session.IsEditing, Is.False);
        }

        private sealed class FocusWindow : EditorWindow { }

        [UnityTest] public IEnumerator ImguiScrolledResizeSplitAndEdgeClamps() => EdgeMatrix(false);
        [UnityTest] public IEnumerator ToolkitScrolledResizeSplitAndEdgeClamps() => EdgeMatrix(true);

        private static IEnumerator EdgeMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit, 250);
            yield return null; yield return null; yield return null;
            var baseline = h.Content;
            foreach (var width in new[] { 220f, 360f })
            {
                h.Workspace.LeftWidth = width;
                h.Window.position = new Rect(60, 60, width == 220 ? 600 : 1000, 250);
                h.Window.Repaint(); yield return null; yield return null;
                h.Presenter.Scroll(80);
                var end = new Vector2(90, 12);
                h.Begin(h.Track("A2"), end);
                var scroll = h.Workspace.ScrollY;
                h.Presenter.Tick(.1);
                Assert.That(h.Workspace.ScrollY, Is.EqualTo(Math.Max(0, scroll - 15)).Within(.001f));
                Assert.That(h.Presenter.State.Insertion.Width, Is.EqualTo(h.Presenter.State.Size.Width));
                h.Presenter.Tick(10); Assert.That(h.Workspace.ScrollY, Is.Zero);
                h.Key(KeyCode.Escape); h.AssertReleased();
                h.Begin(h.Track("A0"), new Vector2(90, h.Presenter.State.Size.Height - 12));
                scroll = h.Workspace.ScrollY;
                var maximum = h.Presenter.State.Rows.Count * 28 + 32 - h.Presenter.State.Size.Height;
                h.Presenter.Tick(.1);
                Assert.That(h.Workspace.ScrollY, Is.EqualTo(Math.Min(maximum, scroll + 15)).Within(.001f));
                h.Presenter.Tick(10); Assert.That(h.Workspace.ScrollY, Is.EqualTo(maximum).Within(.001f));
                h.Pointer(EventType.MouseDrag, new Vector2(90, h.Presenter.State.Size.Height - 1));
                Assert.That(h.Presenter.State.Insertion.Visible, Is.True);
                h.Pointer(EventType.MouseUp, new Vector2(h.Presenter.State.Size.Width + 1, 80));
                h.AssertReleased(); Assert.That(h.Content, Is.EqualTo(baseline));
            }
        }

        [UnityTest] public IEnumerator ImguiLockedSourcesAndRejectedReceiversKeepGraph() => LockMatrix(false);
        [UnityTest] public IEnumerator ToolkitLockedSourcesAndRejectedReceiversKeepGraph() => LockMatrix(true);

        private static IEnumerator LockMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            yield return null; yield return null; yield return null;
            var baseline = h.Content;
            h.Pointer(EventType.MouseDown, new Vector2(90, h.Y(h.Track("C0"))));
            h.Pointer(EventType.MouseDrag, new Vector2(90, h.Y(h.Group("A"))));
            Assert.That(h.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Idle));
            h.Pointer(EventType.MouseUp, new Vector2(90, h.Y(h.Group("A")))); h.AssertReleased();
            Assert.That(h.Content, Is.EqualTo(baseline));
            // 继承锁定和目标锁定分别通过实际标题输入验证，不能只检查CanExecute。
            h.Group("A").IsLocked = true; h.Presenter.Refresh();
            var locked = h.Content;
            h.Pointer(EventType.MouseDown, new Vector2(90, h.Y(h.Track("A0"))));
            h.Pointer(EventType.MouseDrag, new Vector2(90, h.Y(h.Group("B"))));
            h.Pointer(EventType.MouseUp, new Vector2(90, h.Y(h.Group("B")))); h.AssertReleased();
            h.Pointer(EventType.MouseDown, new Vector2(90, h.Y(h.Group("A"))));
            h.Pointer(EventType.MouseDrag, new Vector2(90, h.Y(h.Group("B"))));
            h.Pointer(EventType.MouseUp, new Vector2(90, h.Y(h.Group("B")))); h.AssertReleased();
            Assert.That(h.Content, Is.EqualTo(locked));
            var target = new Vector2(90, h.Y(h.Group("A")));
            h.Begin(h.Track("B1"), target);
            Assert.That(h.Presenter.State.Insertion.Visible, Is.True);
            Assert.That(h.Presenter.State.Insertion.Valid, Is.False);
            Assert.That(h.Presenter.State.Insertion.Red, Is.GreaterThan(h.Presenter.State.Insertion.Blue));
            h.Pointer(EventType.MouseUp, target); h.AssertReleased();
            Assert.That(h.Error, Is.Not.Null); Assert.That(h.Content, Is.EqualTo(locked));
            h.Group("A").IsLocked = false; h.Presenter.Refresh();
            target = new Vector2(90, h.Y(h.Group("A")));
            h.Begin(h.Track("B1"), target);
            Assert.That(h.Presenter.State.Insertion.Valid, Is.True);
            // 不通知视图而改变目标锁，释放本身也必须重验当前规则。
            h.Group("A").IsLocked = true; locked = h.Content;
            h.Pointer(EventType.MouseUp, target); h.AssertReleased();
            Assert.That(h.Content, Is.EqualTo(locked)); Assert.That(h.Error, Is.Not.Null);
            h.Group("A").IsLocked = false; h.Presenter.Refresh();
            // 锁定子轨不阻止未锁定父组携带整块移动。
            h.Drag(h.Group("C"), new Vector2(90, h.Y(h.Group("A"), .1f)));
            h.Roots("C", "A", "B", "R", "E"); Assert.That(h.Track("C0").IsLocked, Is.True);
            h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(baseline));
        }

        [UnityTest] public IEnumerator ImguiUniqueAndAttachableRefuseThroughNativeInput() => ConstraintMatrix(false);
        [UnityTest] public IEnumerator ToolkitUniqueAndAttachableRefuseThroughNativeInput() => ConstraintMatrix(true);

        private static IEnumerator ConstraintMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit, 700, unique: true);
            yield return null; yield return null; yield return null;
            var baseline = h.Content; var version = h.Context.Session.Version;
            foreach (var target in new[] { "B", "N" })
            {
                var end = new Vector2(90, h.Y(h.Group(target)));
                h.Begin(h.Track("U0"), end);
                Assert.That(h.Presenter.State.Insertion.Visible, Is.True);
                Assert.That(h.Presenter.State.Insertion.Valid, Is.False);
                h.Pointer(EventType.MouseUp, end); h.AssertReleased();
                Assert.That(h.Content, Is.EqualTo(baseline));
                Assert.That(h.Context.Session.Version, Is.EqualTo(version));
                Assert.That(h.Context.Session.IsDirty, Is.False);
            }
            Assert.Throws<System.IO.InvalidDataException>(() => JsonDocumentCodec.Encode(h.Context.Document),
                "显式测试类型不能进入生产可保存目录。");
        }

        [UnityTest] public IEnumerator ImguiFirstAndLastChildFoldDeleteUndoAndPassiveSlot() => FoldMatrix(false);
        [UnityTest] public IEnumerator ToolkitFirstAndLastChildFoldDeleteUndoAndPassiveSlot() => FoldMatrix(true);

        private static IEnumerator FoldMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            yield return null; yield return null; yield return null;
            void FoldState(bool expected)
            {
                var row = h.Presenter.State.Rows.Single(r => r.Key == h.Presenter.KeyFor(h.Group("E")));
                Assert.That(row.CanFold, Is.EqualTo(expected));
                if (h.Toolkit == null) return;
                var element = h.Toolkit.Canvas.Q("timeline-row-" + row.Key);
                Assert.That(element.Q<Button>("timeline-fold") != null, Is.EqualTo(expected));
                if (!expected) Assert.That(element.Q("timeline-fold-placeholder").pickingMode, Is.EqualTo(PickingMode.Ignore));
            }
            FoldState(false);
            var before = h.Content;
            h.Pointer(EventType.MouseDown, new Vector2(12, h.Y(h.Group("E"))));
            h.Pointer(EventType.MouseUp, new Vector2(12, h.Y(h.Group("E"))));
            Assert.That(h.Workspace.IsCollapsed(h.Group("E")), Is.False); h.AssertReleased();
            Assert.That(h.Content, Is.EqualTo(before));
            h.Drag(h.Track("R"), new Vector2(90, h.Y(h.Group("E")))); FoldState(true);
            h.Key(KeyCode.Delete); FoldState(false); h.Tracks("E");
            h.Key(KeyCode.Z, control: true); FoldState(true); h.Tracks("E", "R");
            h.Key(KeyCode.Z, control: true, shift: true); FoldState(false);
            h.Key(KeyCode.Z, control: true); FoldState(true);
            h.Drag(h.Track("R"), new Vector2(19, h.Y(h.Group("E"), .9f))); FoldState(false);
            h.Key(KeyCode.Z, control: true); FoldState(true);
        }

        [UnityTest] public IEnumerator ImguiDragSurvivesOtherWindowRefreshAndClose() => OtherWindowMatrix(false);
        [UnityTest] public IEnumerator ToolkitDragSurvivesOtherWindowRefreshAndClose() => OtherWindowMatrix(true);

        private static IEnumerator OtherWindowMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            EditorWindow other = null;
            try
            {
                if (toolkit)
                {
                    var window = ScriptableObject.CreateInstance<ImguiWindow>();
                    other = window; window.BindContext(h.Context, h.Views, h.Settings);
                }
                else
                {
                    var window = ScriptableObject.CreateInstance<ToolkitWindow>();
                    other = window; window.BindContext(h.Context, h.Views, h.Settings);
                }
                other.Show(); other.position = new Rect(1100, 60, 550, 300);
                h.Window.Focus(); h.Window.Repaint(); yield return null; yield return null; yield return null;
                var before = h.Content; var end = new Vector2(90, h.Y(h.Group("B")));
                h.Begin(h.Track("A0"), end);
                h.Views.NotifyChanged(); other.Repaint(); yield return null;
                Assert.That(h.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                Assert.That(h.Content, Is.EqualTo(before));
                // 非所有者关闭不调用Discard，不能触碰共享作者会话或当前捕获。
                var graph = h.Context.Document; var version = h.Context.Session.Version;
                other.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(other, null);
                Assert.That(h.Views.Count, Is.EqualTo(1));
                Assert.That(h.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure), "非所有者释放不能取消当前手势。");
                Assert.That(h.Context.Session.IsDirty, Is.False); other.Close(); other = null;
                Assert.That(h.Views.Count, Is.EqualTo(1));
                Assert.That(h.Context.Document, Is.SameAs(graph), $"关闭干净非所有者不应替换共享图，version={version}->{h.Context.Session.Version}");
                // Unity关闭独立窗口可能把焦点交给Inspector；真实失焦必须取消，而不是强行保留捕获。
                if (EditorWindow.focusedWindow != h.Window)
                {
                    h.AssertReleased(); Assert.That(h.Content, Is.EqualTo(before));
                    h.Window.Focus(); yield return null;
                    h.Begin(h.Track("A0"), end);
                }
                else Assert.That(h.Presenter.Mode, Is.EqualTo(TimelineGestureMode.Structure));
                h.Pointer(EventType.MouseUp, end); h.AssertReleased();
                h.Tracks("B", "A0", "B0", "B1");
                h.Key(KeyCode.Z, control: true); Assert.That(h.Content, Is.EqualTo(before));
            }
            finally { if (other != null) { h.Context.Session.DiscardChanges(); other.Close(); } }
        }

        [UnityTest] public IEnumerator ImguiInsertionBlueAndRedRasterCoversFullBody() => RasterMatrix(false);
        [UnityTest] public IEnumerator ToolkitInsertionBlueAndRedRasterCoversFullBody() => RasterMatrix(true);

        private static IEnumerator RasterMatrix(bool toolkit)
        {
            using var h = new WindowInput(toolkit);
            yield return null; yield return null; yield return null;
            foreach (var locked in new[] { false, true })
            {
                h.Group("B").IsLocked = locked; h.Presenter.Refresh();
                h.Begin(h.Track("A0"), new Vector2(90, h.Y(h.Group("B"))));
                Assert.That(h.Presenter.State.Insertion.Valid, Is.EqualTo(!locked));
                h.Window.Repaint(); yield return null; yield return null;
                var line = h.Presenter.State.Insertion;
                var top = h.Toolkit == null ? 40 : h.Toolkit.Canvas.worldBound.yMin - h.Toolkit.rootVisualElement.worldBound.yMin;
                var image = ReadPixels(h.Window);
                try
                {
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(UnityEngine.Application.dataPath,
                        "../Temp/rf35-insertion-" + (toolkit ? "toolkit" : "imgui") + (locked ? "-red.png" : "-blue.png")), image.EncodeToPNG());
                    var scale = EditorGUIUtility.pixelsPerPoint;
                    foreach (var x in new[] { 1f, 60f, h.Presenter.State.ContentLeft + 50, line.Width - 3 })
                    {
                        var found = false;
                        for (var dy = -3; dy <= 3; dy++)
                        {
                            var px = Mathf.Clamp(Mathf.RoundToInt(x * scale), 0, image.width - 1);
                            var py = Mathf.Clamp(image.height - 1 - Mathf.RoundToInt((top + line.Y) * scale) + dy, 0, image.height - 1);
                            var color = image.GetPixel(px, py);
                            found |= locked ? color.r - color.b > .2f : color.b - color.r > .2f && color.g - color.r > .1f;
                        }
                        Assert.That(found, Is.True, $"实际绘制颜色/端点缺失: toolkit={toolkit}, locked={locked}, x={x}, y={line.Y}, top={top}");
                    }
                    if (toolkit)
                    {
                        var overlay = h.Toolkit.Canvas.Q("timeline-insertion-overlay");
                        Assert.That(overlay.Query<TextElement>().ToList(), Is.Empty, "结构提示只能画线，不能添加拖动文字。");
                        Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Ignore));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                h.Key(KeyCode.Escape); h.AssertReleased();
            }
        }

        private static Texture2D ReadPixels(EditorWindow window)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var host = typeof(EditorWindow).GetField("m_Parent", flags).GetValue(window);
            var scale = EditorGUIUtility.pixelsPerPoint;
            var raw = RenderTexture.GetTemporary(Mathf.RoundToInt(window.position.width * scale), Mathf.RoundToInt(window.position.height * scale), 0);
            var upright = RenderTexture.GetTemporary(raw.descriptor); var previous = RenderTexture.active;
            var image = new Texture2D(raw.width, raw.height, TextureFormat.RGB24, false);
            try
            {
                host.GetType().GetMethod("GrabPixels", flags).Invoke(host, new object[] { raw, new Rect(0, 0, raw.width, raw.height) });
                Graphics.Blit(raw, upright, EditorGUIUtility.Load("SceneView/BlitSceneViewCapture.mat") as Material);
                RenderTexture.active = upright; image.ReadPixels(new Rect(0, 0, upright.width, upright.height), 0, 0); image.Apply();
                return image;
            }
            catch { UnityEngine.Object.DestroyImmediate(image); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(upright); RenderTexture.ReleaseTemporary(raw); }
        }
    }
}
