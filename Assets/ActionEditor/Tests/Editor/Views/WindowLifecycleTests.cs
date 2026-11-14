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
    public sealed class WindowLifecycleTests : SharedContextFixture
    {
        [Test] public void JsonPickerRejectsNonJsonBeforeDecodingAndKeepsCurrentDocument()
        {
            var before = ActionEditorHost.Context.Document;
            var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionEditor/Runtime/Interfaces/IDirectable.cs");
            Assert.That(script, Is.Not.Null);
            LogAssert.Expect(LogType.Warning, "请选择 .json 格式的 ActionEditor 文档。");
            Composition.DocumentPresenterComposition.OpenPicked(ActionEditorHost.Context, script);
            Assert.That(ActionEditorHost.Context.Document, Is.SameAs(before));
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
                filter.SetValue(selector, (Predicate<string>)NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository.IsDocumentPath);
                init.Invoke(selector, new object[] { "TextAsset", null });
                foreach (var entry in (Array)list.GetValue(selector))
                    Assert.That(NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository.IsDocumentPath((string)entry.GetType().GetField("path").GetValue(entry)), Is.True);
                var allowed = new System.Collections.Generic.List<int> { script.GetInstanceID() };
                init.Invoke(selector, new object[] { "TextAsset", allowed });
                Assert.That(((Array)list.GetValue(selector)).Length, Is.Zero);
                filter.SetValue(selector, null);
                init.Invoke(selector, new object[] { "TextAsset", allowed });
                Assert.That(((Array)list.GetValue(selector)).Length, Is.EqualTo(1), "Generic resource pickers still accept non-JSON TextAssets.");
                Assert.That(NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository.IsDocumentPath("Assets/Sample.JSON"), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(selector); }
        }

        [Test] public void ControlledQuitRejectsUnsavedDocumentAndCancelsWhenEditingStartsAfterAcceptance()
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var types = AppDomain.CurrentDomain.GetAssemblies();
            var command = types.Select(a => a.GetType("NBC.ActionEditor.Verification.ControlledQuitCommands")).Single(t => t != null).GetMethod("ControlledQuit");
            var lifecycle = types.Select(a => a.GetType("Unity.Pipeline.EditorQuit")).Single(t => t != null);
            var cancel = lifecycle.GetMethod("CancelScheduledExit", flags);
            var pending = lifecycle.GetProperty("IsExitScheduled", flags);
            var exit = lifecycle.GetField("ExitAction", flags);
            var update = typeof(EditorApplication).GetField("update", flags);
            Assert.That(pending.GetValue(null), Is.False, "never interfere with an existing exit");
            var previousExit = exit.GetValue(null); var exitAttempts = 0;
            Action<int> trap = _ => exitAttempts++;
            exit.SetValue(null, trap);
            try
            {
                var preview = command.Invoke(null, new object[] { false, true });
                var blockers = (string[])preview.GetType().GetProperty("blockers").GetValue(preview);
                Assert.That(blockers.Any(b => b.Contains("no existing disk source")), Is.True);
                Assert.Throws<System.Reflection.TargetInvocationException>(() => command.Invoke(null, new object[] { true, false }));
                Assert.That(pending.GetValue(null), Is.False);
                ActionEditorHost.Context.Open(null);
                var before = ((Delegate)update.GetValue(null)).GetInvocationList();
                command.Invoke(null, new object[] { true, false });
                Assert.That(pending.GetValue(null), Is.True);
                var added = ((Delegate)update.GetValue(null)).GetInvocationList().Where(d => !before.Contains(d)).ToArray();
                Assert.That(added.Length, Is.EqualTo(2), "our guard must precede Pipeline Tick");
                ActionEditorHost.Context.Open(fixture);
                added[0].DynamicInvoke();
                Assert.That(pending.GetValue(null), Is.False, "new unsaved document cancels scheduled exit");
                added[0].DynamicInvoke();
                Assert.That(exitAttempts, Is.Zero);
            }
            finally
            {
                cancel.Invoke(null, null);
                exit.SetValue(null, previousExit);
            }
        }

        [Test] public void SharedContextOwnsStateAndServicesWithoutAnEditorWindow()
        {
            using (var context = new ActionEditorContext(NBC.ActionEditor.Composition.DocumentComposition.Create()))
            {
                context.Open(fixture);
                var clip = context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                context.Selection.Set(new IDirectable[] { clip }, false);
                context.Clipboard.Item = clip;
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Document, Is.Not.SameAs(ActionEditorHost.Context.Document));
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

        [Test] public void FailedOpenKeepsContextAndInjectedPlayer()
        {
            var context = ActionEditorHost.Context; var document = context.Document;
            var clip = document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
            ActionEditorHost.Context.Selection.Set(new IDirectable[] { clip }, false);
            var invalid = new TextAsset("{}");
            try
            {
                Assert.Throws<System.IO.InvalidDataException>(() => ActionEditorHost.Context.Open(invalid));
                Assert.That(context.Document, Is.SameAs(document));
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Selection.First, Is.SameAs(clip));
                Assert.That(typeof(AssetPlayer).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), Is.Empty);
                ActionEditorHost.Context.ClipboardEditing.Copy(clip);
                Assert.That(context.Clipboard.Item, Is.SameAs(ActionEditorHost.Context.Clipboard.Item));
                Assert.That(context.Clipboard.Type, Is.EqualTo(clip.GetType()));
                ActionEditorHost.Context.Playback.Pause(true); Assert.That(context.Playback.IsPaused, Is.True);
                context.Playback.Pause(false); Assert.That(ActionEditorHost.Context.Playback.IsPaused, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(invalid); }
        }

        [Test] public void ViewRegistrationIsIdempotentAndDetachesNotifications()
        {
            var views = new ActionEditorViews(); var first = new object(); var second = new object();
            var paints = 0; var changes = 0;
            views.Attach(first, () => paints++, () => changes++);
            views.Attach(first, () => paints++, () => changes++);
            views.Attach(second, () => paints++, () => changes++);
            Assert.That(views.Count, Is.EqualTo(2));
            views.Repaint(); views.NotifyChanged();
            Assert.That(paints, Is.EqualTo(2)); Assert.That(changes, Is.EqualTo(2));
            views.Detach(first); views.Detach(first);
            views.Repaint(); views.NotifyChanged();
            Assert.That(paints, Is.EqualTo(3)); Assert.That(changes, Is.EqualTo(3));
            views.Detach(second);
            Assert.That(views.Count, Is.Zero);
            views.Repaint(); views.NotifyChanged();
            Assert.That(paints, Is.EqualTo(3)); Assert.That(changes, Is.EqualTo(3));
        }

        [UnityTest] public IEnumerator BothWindowOpenOrdersShareContextAndRebuildWithoutRegistrationLeaks()
        {
            var count = ActionEditorHost.Views.Count;
            ImguiWindow original = null; ToolkitWindow native = null;
            var alternate = new TextAsset(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument { Version = 7 }));
            try
            {
                foreach (var nativeFirst in new[] { false, true })
                {
                    if (nativeFirst)
                    {
                        native = ScriptableObject.CreateInstance<ToolkitWindow>(); native.Show();
                        original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                    }
                    else
                    {
                        original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                        native = ScriptableObject.CreateInstance<ToolkitWindow>(); native.Show();
                    }
                    yield return null; yield return null;
                    Assert.That(ActionEditorHost.Views.Count, Is.EqualTo(count + 2));
                    Assert.That(original.Context, Is.SameAs(native.Context));
                    Assert.That(original.Context.Session, Is.SameAs(ActionEditorHost.Context.Session));
                    native.CreateGUI(); native.CreateGUI();
                    Assert.That(ActionEditorHost.Views.Count, Is.EqualTo(count + 2));
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(ToolkitWindow).GetMethod("OnEnable", flags).Invoke(native, null);
                    typeof(ImguiWindow).GetMethod("OnEnable", flags).Invoke(original, null);
                    Assert.That(ActionEditorHost.Views.Count, Is.EqualTo(count + 2), "Repeated enable must replace registrations rather than duplicate them.");
                    native.Select(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]);
                    Assert.That(original.Context.Selection.First, Is.SameAs(ActionEditorHost.Context.Selection.First));
                    ActionEditorHost.Context.Open(alternate);
                    Assert.That(original.Context.Document, Is.SameAs(native.Document));
                    Assert.That(((FeatureSampleDocument)native.Document).Version, Is.EqualTo(7));
                    Assert.That(ActionEditorHost.Context.Selection.Count, Is.Zero);
                    ActionEditorHost.Context.Open(fixture);
                    if (nativeFirst) { native.Close(); native = null; original.Close(); original = null; }
                    else { original.Close(); original = null; native.Close(); native = null; }
                    Assert.That(ActionEditorHost.Views.Count, Is.EqualTo(count));
                }
            }
            finally
            {
                if (original != null) { original.DiscardChanges(); original.Close(); }
                if (native != null) { native.DiscardChanges(); native.Close(); }
                UnityEngine.Object.DestroyImmediate(alternate);
            }
        }

        [UnityTest] public IEnumerator DiscardFromEitherWindowRestoresSharedBaselineAndClearsPendingUndo()
        {

            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                foreach (var window in new EditorWindow[] { original, native })
                {
                    ActionEditorHost.Context.Session.Edit("discard payload", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 19);
                    ActionEditorHost.Context.Session.BeginEdit();
                    ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = 1;
                    window.DiscardChanges();
                    Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
                    Assert.That(ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                    Assert.That(ActionEditorHost.Context.Session.IsDirty, Is.False);
                    Assert.That(ActionEditorHost.Context.Session.IsEditing, Is.False);
                    Assert.That(native.Document, Is.SameAs(ActionEditorHost.Context.Document));
                    Undo.PerformUndo(); Undo.PerformRedo();
                    Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1), "Discarded changes must not return through Undo/Redo.");
                    yield return null; yield return null;
                    Assert.That(original.hasUnsavedChanges, Is.False);
                    Assert.That(native.hasUnsavedChanges, Is.False);
                }
                ActionEditorHost.Context.Session.Edit("new edit", () => ((FeatureSampleDocument)ActionEditorHost.Context.Document).Version = 7);
                Undo.PerformUndo(); Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(1));
                Undo.PerformRedo(); Assert.That(((FeatureSampleDocument)ActionEditorHost.Context.Document).Version, Is.EqualTo(7));
            }
            finally
            {
                ActionEditorHost.Context.Open(fixture);
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
            }
        }

        [UnityTest] public IEnumerator ClosingEitherWindowPreservesPlaybackUntilLastViewCloses()
        {

            var existingViews = ActionEditorHost.Views.Count;
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var stops = 0;
            Action stopped = () => stops++;
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                ActionEditorHost.Context.Playback.Stopped += stopped;
                ActionEditorHost.Context.Playback.Play(ActionEditorHost.Context.Clock.PlaybackTime); ActionEditorHost.Context.Player.Sample(1); ActionEditorHost.Context.Playback.Pause(true);
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying && ActionEditorHost.Context.Playback.IsPaused, Is.True);
                original.Close(); original = null;
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying && ActionEditorHost.Context.Playback.IsPaused, Is.True);
                Assert.That(ActionEditorHost.Context.Player.CurrentTime, Is.EqualTo(1));
                Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.True);
                Assert.That(stops, Is.Zero);
                yield return null; yield return null;
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying && ActionEditorHost.Context.Playback.IsPaused, Is.True, "The remaining view owns the same paused playback.");
                original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                yield return null; yield return null;
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying && ActionEditorHost.Context.Playback.IsPaused, Is.True);
                Assert.That(ActionEditorHost.Context.Player.CurrentTime, Is.EqualTo(1));
                native.Close(); native = null;
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying && ActionEditorHost.Context.Playback.IsPaused, Is.True);
                ActionEditorHost.Context.Playback.Play(ActionEditorHost.Context.Clock.PlaybackTime); ActionEditorHost.Context.Player.Sample(1);
                original.Close(); original = null;
                Assert.That(ActionEditorHost.Views.Count, Is.EqualTo(existingViews), "The test must release exactly its own two views.");
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying, Is.EqualTo(existingViews != 0));
                Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.EqualTo(existingViews != 0));
                Assert.That(ActionEditorHost.Context.Player.CurrentTime, Is.EqualTo(existingViews == 0 ? 0 : 1));
                Assert.That(stops, Is.EqualTo(existingViews == 0 ? 1 : 0));
            }
            finally
            {
                ActionEditorHost.Context.Playback.Stopped -= stopped;
                if (original != null) { original.DiscardChanges(); original.Close(); }
                if (native != null) { native.DiscardChanges(); native.Close(); }

            }
        }

        [UnityTest] public IEnumerator ProductTermsMatchBothLanguageCatalogsAndRealWindowEntrypoints()
        {
            var fields = typeof(Lan).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Where(f => f.FieldType == typeof(string)).ToArray();
            var previousTerms = fields.Select(f => f.GetValue(null)).ToArray();

            ImguiWindow original = null; ToolkitWindow native = null;
            try
            {
                foreach (var language in new[] { typeof(LanCHS), typeof(LanEN) })
                {
                    ActionEditorHost.Context.Open(null);
                    original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                    native = ScriptableObject.CreateInstance<ToolkitWindow>(); native.Show();
                    foreach (var field in fields)
                    {
                        var translated = language.GetField(field.Name);
                        Assert.That(translated, Is.Not.Null, language.Name + "." + field.Name);
                        Assert.That(translated.GetValue(null) as string, Is.Not.Null.And.Not.Empty);
                        field.SetValue(null, translated.GetValue(null));
                    }
                    original.Repaint(); native.CreateGUI();
                    yield return null; yield return null;
                    Assert.That(original.titleContent.text, Is.EqualTo(Lan.Title));
                    Assert.That(native.titleContent.text, Is.EqualTo(Lan.Title + " · UI Toolkit"));
                    var buttons = native.rootVisualElement.Q("timeline-welcome").Query<Button>().ToList();
                    Assert.That(buttons.Select(b => b.text), Does.Contain(Lan.CreateAsset).And.Contain(Lan.SelectAsset));
                    var labels = native.rootVisualElement.Q("timeline-welcome").Query<Label>().ToList();
                    Assert.That(labels.Select(l => l.text), Does.Contain(Lan.WelcomeHint).And.Contain(Lan.WelcomeHelp));
                    Assert.That(new Composition.DocumentDialogComposition(ActionEditorHost.Context).TypeName(typeof(FeatureSampleDocument)), Is.EqualTo(Lan.FeatureSample));
                    var menu = new System.Collections.Generic.List<string>(); Composition.DocumentPresenterComposition.PopulateSaveMenu(Composition.DocumentPresenterComposition.Create(ActionEditorHost.Context), (label, action) => menu.Add(label));
                    Assert.That(menu, Is.EqualTo(new[] { Lan.Save, Lan.SaveAs, Lan.Export }));
                    Assert.That(string.Join(" ", menu) + Lan.WelcomeHint + Lan.CreateSafety + Lan.FeatureSample, Does.Not.Contain("JSON").And.Not.Contain("旧版").And.Not.Contain("新版"));
                    native.Close(); original.Close(); native = null; original = null;
                }
            }
            finally
            {
                if (native != null) native.Close(); if (original != null) original.Close();
                for (var i = 0; i < fields.Length; i++) fields[i].SetValue(null, previousTerms[i]);

            }
        }

        [Test] public void BothSaveChangesEntrypointsUseTheSameCancelablePathRequest()
        {

            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            context.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            var views = new ActionEditorViews();
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var requests = 0;
            try
            {
                Func<bool, string> choosePath = saveAs => { Assert.That(saveAs, Is.False); requests++; return null; };
                original.BindContext(context, views, Prefs.Session, choosePath);
                native.BindContext(context, views, Prefs.Session, choosePath);
                context.Session.Edit("change", () => ((FeatureSampleDocument)context.Document).Version = 9);
                var source = context.Document; var before = context.Session.CaptureRecovery();
                original.SaveChanges(); native.SaveChanges();
                Assert.That(requests, Is.EqualTo(2)); Assert.That(context.Document, Is.SameAs(source));
                Assert.That(context.Session.CaptureRecovery(), Is.EqualTo(before)); Assert.That(context.Session.IsDirty, Is.True);
                original.Presenter.Key(new KeyInput(TimelineKey.Save));
                native.Canvas.Presenter.Key(new KeyInput(TimelineKey.Save));
                Assert.That(requests, Is.EqualTo(4), "Timeline shortcuts must retain the same window-owned path request.");
                Assert.That(context.Session.CaptureRecovery(), Is.EqualTo(before));
                var binding = (Composition.WindowBinding)typeof(ImguiWindow).GetProperty("Binding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(original);
                var entries = new System.Collections.Generic.List<string>(); binding.PopulateSaveMenu((label, action) => entries.Add(label));
                Assert.That(entries, Is.EqualTo(new[] { Lan.Save, Lan.SaveAs, Lan.Export }));
            }
            finally
            {
                original.DiscardChanges(); native.DiscardChanges();
                UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(native);

            }
        }

        [Test] public void PreviewResetDisposesOwnedCallbacksAndAllowsRebuild()
        {
            ActionEditorHost.Context.Player.Reset(); ActionEditorHost.Context.Player.Sample(0);
            Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.True, "First sample at zero must initialize preview.");
            ActionEditorHost.Context.Player.Sample(1); Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.True);
            ActionEditorHost.Context.Player.Reset(); Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.False);
            ActionEditorHost.Context.Player.Sample(2); Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.True);
            ActionEditorHost.Context.Session.Edit("remove", () => ActionEditorHost.Context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.RemoveAt(0));
            Assert.That(ActionEditorHost.Context.Player.IsInitialized, Is.False);
        }

        [UnityTest] public IEnumerator ImguiRepaintKeepsOwnedPanelsAndCloseReleasesThem()
        {

            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.Show(); window.Focus(); window.Repaint(); yield return null; yield return null;
                window.Repaint(); yield return null; yield return null;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var welcome = typeof(ImguiWindow).GetField("_welcomeView", flags);
                var header = typeof(ImguiWindow).GetField("header", flags);
                var ownedWelcome = welcome.GetValue(window); var ownedHeader = header.GetValue(window);
                Assert.That(ownedWelcome, Is.Not.Null); Assert.That(ownedHeader, Is.Not.Null);
                for (var i = 0; i < 5; i++) { window.Repaint(); yield return null; yield return null; }
                Assert.That(welcome.GetValue(window), Is.SameAs(ownedWelcome));
                Assert.That(header.GetValue(window), Is.SameAs(ownedHeader));
                window.DiscardChanges();
            }
            finally { window.DiscardChanges(); window.Close(); }
            Assert.That(window.Timeline, Is.Null);
            Assert.That(typeof(ImguiWindow).GetField("_welcomeView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window), Is.Null);
            Assert.That(typeof(ImguiWindow).GetField("header", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window), Is.Null);
        }
    }
}
