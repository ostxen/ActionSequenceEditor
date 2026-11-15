using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Infrastructure.Documents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using NBC.ActionEditor.Views.IMGUI;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;

namespace NBC.ActionEditor.Tests
{
    // 重复真实输入、开闭与控件重建，按各自所有者核对引用和Unity对象归还。
    public sealed class ResourceOwnershipTests
    {
        public sealed class PreviewResource : ScriptableObject { }
        [CustomEditor(typeof(PreviewResource))]
        public sealed class FailingPreviewEditor : UnityEditor.Editor
        {
            public override bool HasPreviewGUI() => throw new InvalidOperationException("Preview capability rejected.");
        }
        public sealed class StablePreviewResource : ScriptableObject { }
        [CustomEditor(typeof(StablePreviewResource))]
        public sealed class StablePreviewEditor : UnityEditor.Editor
        {
            public override bool HasPreviewGUI() => true;
        }

        [Test] public void HundredPickerPreviewChangesReleaseUnusedEditorsAndKeepBorrowedResourcesAlive()
        {
            var baseline = Resources.FindObjectsOfTypeAll<StablePreviewEditor>().Length;
            var resources = new[] { ScriptableObject.CreateInstance<StablePreviewResource>(), ScriptableObject.CreateInstance<StablePreviewResource>() };
            var type = typeof(ObjectSelectorWindow).Assembly.GetType("NBC.ActionEditor.Views.IMGUI.ObjectPreviewCache", true);
            var cache = (IDisposable)Activator.CreateInstance(type, true);
            var indexer = type.GetProperty("Item"); var cleanup = type.GetMethod("CleanupUntouchedEditors");
            try
            {
                for (var index = 0; index < 100; index++)
                {
                    var resource = resources[index % 2];
                    var preview = indexer.GetValue(cache, new object[] { resource });
                    Assert.That(preview, Is.Not.Null);
                    Assert.That(indexer.GetValue(cache, new object[] { resource }), Is.SameAs(preview));
                    cleanup.Invoke(cache, null);
                    Assert.That(Resources.FindObjectsOfTypeAll<StablePreviewEditor>().Length, Is.EqualTo(baseline + 1));
                    Assert.That(resources.All(value => value != null), Is.True);
                }
                cache.Dispose(); cache.Dispose();
                Assert.That(Resources.FindObjectsOfTypeAll<StablePreviewEditor>().Length, Is.EqualTo(baseline));
                Assert.That(resources.All(value => value != null), Is.True);
            }
            finally
            {
                cache.Dispose();
                foreach (var resource in resources) UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        [Test] public void RejectedPreviewCapabilityReleasesTheEditorCreatedForThatAttempt()
        {
            var resource = ScriptableObject.CreateInstance<PreviewResource>();
            var baseline = Resources.FindObjectsOfTypeAll<FailingPreviewEditor>().Length;
            var assembly = typeof(ObjectSelectorWindow).Assembly;
            var wrapper = assembly.GetType("NBC.ActionEditor.Views.IMGUI.ObjectPreviewEditor", true);
            try
            {
                var error = Assert.Throws<TargetInvocationException>(() => wrapper.GetMethod("Make").Invoke(null,
                    new[] { (object)resource }));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(Resources.FindObjectsOfTypeAll<FailingPreviewEditor>().Length, Is.EqualTo(baseline));
            }
            finally
            {
                // 红例也只释放本次资源的Editor，不清理其他窗口或用户资源。
                foreach (var editor in Resources.FindObjectsOfTypeAll<FailingPreviewEditor>().Where(e => e.target == resource))
                    UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        private static int Subscribers(object owner, Type type, string name) =>
            ((Delegate)type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner))?.GetInvocationList().Length ?? 0;

        [UnityTest] public IEnumerator HundredNativeDragCancelUndoAndRebuildCyclesReleaseOwnedResources()
        {
            foreach (var toolkit in new[] { false, true })
            {
                using var h = new StructureInputMatrixTests.WindowInput(toolkit);
                yield return null; yield return null; yield return null;
                var baseline = h.Content;
                var session = h.Context.Session;
                var changed = Subscribers(session, session.GetType(), "Changed");
                var content = Subscribers(session, session.GetType(), "ContentChanged");
                var preferences = Subscribers(h.Settings, typeof(SettingsSession), "Changed");
                var undoObjects = Resources.FindObjectsOfTypeAll<DocumentUndoState>().Length;
                for (var i = 0; i < 100; i++)
                {
                    var end = new Vector2(90, h.Y(h.Group("B")));
                    h.Begin(h.Track("A0"), end);
                    if (i % 2 == 0) h.Key(KeyCode.Escape);
                    else
                    {
                        h.Pointer(EventType.MouseUp, end);
                        h.Tracks("B", "A0", "B0", "B1");
                        h.Key(KeyCode.Z, control: true);
                    }
                    h.AssertReleased(); Assert.That(h.Content, Is.EqualTo(baseline));
                    Assert.That(session.IsDirty, Is.False);
                    if (toolkit)
                    {
                        var previous = h.Toolkit.Canvas;
                        h.Toolkit.CreateGUI();
                        Assert.That(previous.panel, Is.Null);
                        Assert.That(previous.State, Is.Null);
                        Assert.That(previous.PointerId, Is.EqualTo(-1));
                        h.Window.Repaint(); yield return null;
                    }
                    Assert.That(Subscribers(session, session.GetType(), "Changed"), Is.EqualTo(changed), "cycle=" + i);
                    Assert.That(Subscribers(session, session.GetType(), "ContentChanged"), Is.EqualTo(content));
                    Assert.That(Subscribers(h.Settings, typeof(SettingsSession), "Changed"), Is.EqualTo(preferences));
                    Assert.That(Resources.FindObjectsOfTypeAll<DocumentUndoState>().Length, Is.EqualTo(undoObjects));
                    Assert.That(h.Views.Count, Is.EqualTo(1));
                }
                // 只计本会话订阅和本次窗口，不清理其他用户窗口或历史测试对象。
                h.Close();
                Assert.That(Subscribers(session, session.GetType(), "Changed"), Is.EqualTo(1));
                Assert.That(Subscribers(session, session.GetType(), "ContentChanged"), Is.EqualTo(1));
                Assert.That(Subscribers(h.Settings, typeof(SettingsSession), "Changed"), Is.Zero);
            }
        }

        [UnityTest] public IEnumerator HundredDualWindowOpenCloseCyclesKeepBindingsAndPreviewCountsStable()
        {
            using var h = new StructureInputMatrixTests.WindowInput(false);
            yield return null; yield return null; yield return null;
            var baseline = h.Content;
            var globalViews = EditorFixture.SharedViews.Count;
            var session = h.Context.Session;
            var changed = Subscribers(session, session.GetType(), "Changed");
            var contents = Subscribers(session, session.GetType(), "ContentChanged");
            var settings = Subscribers(h.Settings, typeof(SettingsSession), "Changed");
            var windows = Resources.FindObjectsOfTypeAll<ToolkitWindow>().Length;
            var proxies = Resources.FindObjectsOfTypeAll<NBC.ActionEditor.Views.UIToolkit.UIToolkitInspectorTarget>().Length;
            for (var i = 0; i < 100; i++)
            {
                var other = ScriptableObject.CreateInstance<ToolkitWindow>();
                try
                {
                    other.Bind((owner, repaint, changed) => h.Context.Bind(owner, repaint, changed, h.Views, h.Settings)); other.Show();
                    other.position = new Rect(1100, 60, 550, 300); other.CreateGUI();
                    h.Context.Player.Sample(.5f);
                    Assert.That(h.Context.Player.IsInitialized, Is.True);
                    other.Canvas.Focus(); h.Window.Focus();
                }
                finally { other.Close(); }
                Assert.That(h.Views.Count, Is.EqualTo(1));
                Assert.That(h.Context.Player.IsInitialized, Is.True, "关闭非最后窗口不释放共享预览。");
                h.Context.Player.Reset(); Assert.That(h.Context.Player.IsInitialized, Is.False);
                Assert.That(Resources.FindObjectsOfTypeAll<ToolkitWindow>().Length, Is.EqualTo(windows));
                Assert.That(Resources.FindObjectsOfTypeAll<NBC.ActionEditor.Views.UIToolkit.UIToolkitInspectorTarget>().Length, Is.EqualTo(proxies));
                Assert.That(Subscribers(session, session.GetType(), "Changed"), Is.EqualTo(changed));
                Assert.That(Subscribers(session, session.GetType(), "ContentChanged"), Is.EqualTo(contents));
                Assert.That(Subscribers(h.Settings, typeof(SettingsSession), "Changed"), Is.EqualTo(settings));
                Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(globalViews));
                Assert.That(session.IsEditing || session.IsDirty, Is.False);
                if (i % 10 == 0) yield return null;
            }
            Assert.That(h.Content, Is.EqualTo(baseline)); h.AssertReleased();
        }
    }
}
