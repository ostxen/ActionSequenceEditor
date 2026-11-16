using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitCanvas = NBC.ActionEditor.Views.UIToolkit.TimelineCanvas;
using System.Collections;
using System.Linq;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    // 密集点事件走两个正式原生窗口，保留绘制、命中、端点及不标脏要求。
    public sealed class TimelineDensityTests
    {
        private sealed class Preferences : IPreferencesStore
        {
            public EditorSettings Read() => new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/");
            public void Write(EditorSettings value) => Assert.Fail("密集布局验证不得写偏好。");
        }

        [UnityTest] public IEnumerator DensePointsPaintAndHitInBothNativeWindowsWithoutChangingContent()
        {

            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            var document = new FeatureSampleSequence(); document.ClearRootContainers();
            var track = new FeatureSampleMarkerTrack { Name = "Dense points" };
            for (var i = 0; i <= 1000; i++) track.Clips.Add(new FeatureSampleMarker { StartTime = i / 200f });
            document.AddRootContainer(track); document.Init();
            context.Session.Open(new SequenceOpenData(document));
            var before = SequenceJsonCodec.GetContentKey(document);
            var views = new EditorViewRegistry(); var settings = new SettingsSession(new Preferences());
            ImguiWindow imgui = null;
            ToolkitWindow toolkit = null;
            try
            {
                foreach (var native in new[] { false, true })
                {
                    if (native)
                    {
                        toolkit = ScriptableObject.CreateInstance<ToolkitWindow>();
                        toolkit.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings)); toolkit.position = new Rect(60, 60, 1000, 500); toolkit.Show();
                    }
                    else
                    {
                        imgui = ScriptableObject.CreateInstance<ImguiWindow>();
                        imgui.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings)); imgui.position = new Rect(60, 60, 1000, 500); imgui.Show();
                    }
                    yield return null; yield return null;
                    var presenter = native ? toolkit.Canvas.Presenter : imgui.Presenter;
                    foreach (var span in new[] { 2f, 7f, 20f })
                    {
                        presenter.ViewRange(0, span);
                        if (native) toolkit.Canvas.MarkDirtyRepaint(); else imgui.Repaint();
                        yield return null; yield return null;
                        var state = presenter.State;
                        Assert.That(state.Clips.Count, Is.EqualTo(1001));
                        Assert.That(state.Clips.All(c => c.Point && c.Bounds.Width >= 10), Is.True);
                        var point = track.Clips[200];
                        var bounds = state.Clips.Single(c => c.Key == presenter.KeyFor(point)).Bounds;
                        var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
                        if (native)
                        {
                            foreach (var down in new[] { true, false })
                            {
                                var source = new Event { type = down ? EventType.MouseDown : EventType.MouseUp, button = 0,
                                    mousePosition = toolkit.Canvas.LocalToWorld(new Vector2(x, y)) };
                                if (down) { using var e = PointerDownEvent.GetPooled(source); e.target = toolkit.Canvas; toolkit.Canvas.SendEvent(e); }
                                else { using var e = PointerUpEvent.GetPooled(source); e.target = toolkit.Canvas; toolkit.Canvas.SendEvent(e); }
                            }
                        }
                        else
                        {
                            var offset = (Vector2)typeof(ImguiWindow).GetProperty("ContentOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(imgui);
                            foreach (var kind in new[] { EventType.MouseDown, EventType.MouseUp })
                                imgui.SendEvent(new Event { type = kind, button = 0, mousePosition = offset + new Vector2(x, y + 40) });
                        }
                        Assert.That(context.Selection.First, Is.TypeOf<FeatureSampleMarker>());
                        var scale = (state.Size.WindowWidth - state.ContentLeft) / span;
                        Assert.That(Mathf.Abs(context.Selection.First.StartTime - 1) * scale, Is.LessThanOrEqualTo(6.01f));
                        Assert.That(SequenceJsonCodec.GetContentKey(document), Is.EqualTo(before));
                        Assert.That(context.Session.IsDirty || context.Session.IsEditing, Is.False);
                    }
                    Assert.That(presenter.State.Clips.Any(c => c.Key == presenter.KeyFor(track.Clips[0])), Is.True);
                    Assert.That(presenter.State.Clips.Any(c => c.Key == presenter.KeyFor(track.Clips[1000])), Is.True);
                    if (native) { toolkit.Close(); toolkit = null; } else { imgui.Close(); imgui = null; }
                    Assert.That(views.Count, Is.Zero);
                }
            }
            finally
            {
                if (imgui != null) { imgui.DiscardChanges(); imgui.Close(); }
                if (toolkit != null) { toolkit.DiscardChanges(); toolkit.Close(); }

            }
        }

        [UnityTest] public IEnumerator BothNativeMenusSelectAndRevealCreatedTracksWithoutScrollingTheOtherWindow()
        {

            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            context.Session.Open(new SequenceOpenData(new FeatureSampleSequence()));
            var views = new EditorViewRegistry(); var settings = new SettingsSession(new Preferences());
            ImguiWindow imgui = null; ToolkitWindow toolkit = null;
            try
            {
                imgui = ScriptableObject.CreateInstance<ImguiWindow>(); imgui.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings));
                toolkit = ScriptableObject.CreateInstance<ToolkitWindow>(); toolkit.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, settings));
                imgui.position = new Rect(60, 60, 1000, 400); toolkit.position = new Rect(90, 90, 1000, 400);
                imgui.Show(); toolkit.Show(); yield return null; yield return null;
                foreach (var native in new[] { false, true })
                {
                    var presenter = native ? toolkit.Canvas.Presenter : imgui.Presenter;
                    var other = native ? imgui.Presenter : toolkit.Canvas.Presenter;
                    presenter.Scroll(0); other.Scroll(0);
                    var group = context.Sequence.RootContainers.OfType<ActionGroup>().First();
                    for (var index = 0; index < 22; index++)
                    {
                        presenter.Reveal(group);
                        var row = presenter.State.Rows.Single(r => r.Key == presenter.KeyFor(group));
                        var x = 30f; var y = row.Bounds.Y + 14;
                        var suffix = DirectableTypeInfo.For(typeof(FeatureSampleIntervalTrack)).MenuName;
                        if (native)
                        {
                            yield return NativeMenuInput.Choose(toolkit, new Vector2(x, y), suffix);
                        }
                        else
                        {
                            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                            var menu = imgui.Timeline.ContextMenu(x, y);
                            var items = (IEnumerable)(typeof(UnityEditor.GenericMenu).GetField("m_MenuItems", flags)?.GetValue(menu)
                                ?? typeof(UnityEditor.GenericMenu).GetProperty("menuItems", flags).GetValue(menu));
                            var entry = items.Cast<object>().Single(a => ((GUIContent)a.GetType().GetField("content", flags).GetValue(a)).text.EndsWith("/" + suffix));
                            ((UnityEditor.GenericMenu.MenuFunction)entry.GetType().GetField("func", flags).GetValue(entry))();
                        }
                        Assert.That(context.Selection.First, Is.SameAs(group.Tracks.Last()));
                        Assert.That(context.Session.IsEditing, Is.False);
                    }
                    var created = group.Tracks.Last();
                    var result = presenter.State.Rows.Single(r => r.Key == presenter.KeyFor(created));
                    Assert.That(presenter.Workspace.ScrollY, Is.GreaterThan(0));
                    Assert.That(result.Bounds.Y, Is.GreaterThanOrEqualTo(0));
                    Assert.That(result.Bounds.Y + result.Bounds.Height, Is.LessThanOrEqualTo(presenter.State.Size.Height + .01f));
                    Assert.That(other.Workspace.ScrollY, Is.Zero);
                    var before = group.Tracks.Count;
                    UnityEditor.Undo.PerformUndo();
                    Assert.That(context.Sequence.RootContainers.OfType<ActionGroup>().First().Tracks.Count, Is.EqualTo(before - 1));
                    UnityEditor.Undo.PerformRedo();
                    Assert.That(context.Sequence.RootContainers.OfType<ActionGroup>().First().Tracks.Count, Is.EqualTo(before));
                }
            }
            finally
            {
                if (imgui != null) { imgui.DiscardChanges(); imgui.Close(); }
                if (toolkit != null) { toolkit.DiscardChanges(); toolkit.Close(); }

            }
        }
    }
}
