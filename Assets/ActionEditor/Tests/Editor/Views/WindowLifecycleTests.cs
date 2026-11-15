using NBC.ActionEditor.Application.Playback;
using NBC.ActionEditor.Infrastructure.Preview;
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
using NBC.ActionEditor.Infrastructure.Serialization;
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
            var before = EditorFixture.Shared.Document;
            var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionEditor/Runtime/Domain/IDirectable.cs");
            Assert.That(script, Is.Not.Null);
            LogAssert.Expect(LogType.Warning, "请选择 .json 格式的 ActionEditor 文档。");
            EditorFixture.Shared.CreateDocuments().OpenPicked(script);
            Assert.That(EditorFixture.Shared.Document, Is.SameAs(before));
        }

        [Test] public void DocumentPickerFiltersBothDiscoveryPathsWithoutRestrictingGenericResources()
        {
            var selector = ScriptableObject.CreateInstance<ObjectSelectorWindow>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(ObjectSelectorWindow);
            var filter = type.GetField("_assetPathFilter", flags);
            var init = type.GetMethod("DiscoverEntries", flags);
            var list = type.GetField("_entries", flags);
            try
            {
                var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionEditor/Runtime/Domain/IDirectable.cs");
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
                EditorFixture.Shared.Open(null);
                var before = ((Delegate)update.GetValue(null)).GetInvocationList();
                command.Invoke(null, new object[] { true, false });
                Assert.That(pending.GetValue(null), Is.True);
                var added = ((Delegate)update.GetValue(null)).GetInvocationList().Where(d => !before.Contains(d)).ToArray();
                Assert.That(added.Length, Is.EqualTo(2), "our guard must precede Pipeline Tick");
                EditorFixture.Shared.Open(fixture);
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
            using (var context = new EditorFixture(NBC.ActionEditor.Composition.DocumentComposition.Create()))
            {
                context.Open(fixture);
                var clip = context.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                context.Selection.Set(new IDirectable[] { clip }, false);
                context.Clipboard.Item = clip;
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Document, Is.Not.SameAs(EditorFixture.Shared.Document));
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
            var context = EditorFixture.Shared; var document = context.Document;
            var clip = document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
            var invalid = new TextAsset("{}");
            try
            {
                Assert.Throws<System.IO.InvalidDataException>(() => EditorFixture.Shared.Open(invalid));
                Assert.That(context.Document, Is.SameAs(document));
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Selection.First, Is.SameAs(clip));
                Assert.That(typeof(DocumentSampler).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), Is.Empty);
                EditorFixture.Shared.ClipboardEditing.Copy(clip);
                Assert.That(context.Clipboard.Item, Is.SameAs(EditorFixture.Shared.Clipboard.Item));
                Assert.That(context.Clipboard.Type, Is.EqualTo(clip.GetType()));
                EditorFixture.Shared.Playback.Pause(true); Assert.That(context.Playback.IsPaused, Is.True);
                context.Playback.Pause(false); Assert.That(EditorFixture.Shared.Playback.IsPaused, Is.False);
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
            var count = EditorFixture.SharedViews.Count;
            ImguiWindow original = null; ToolkitWindow native = null;
            var alternate = new TextAsset(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Encode(new FeatureSampleDocument { Version = 7 }));
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
                    Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(count + 2));
                    Assert.That(Session(original.Documents), Is.SameAs(Session(native.Documents)));
                    Assert.That(Session(original.Documents), Is.SameAs(EditorFixture.Shared.Session));
                    native.CreateGUI(); native.CreateGUI();
                    Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(count + 2));
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(ToolkitWindow).GetMethod("OnEnable", flags).Invoke(native, null);
                    typeof(ImguiWindow).GetMethod("OnEnable", flags).Invoke(original, null);
                    Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(count + 2), "Repeated enable must replace registrations rather than duplicate them.");
                    native.Select(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]);
                    Assert.That(original.Presenter.State.Clips.Single(c => c.Selected).Key, Is.EqualTo(original.Presenter.KeyFor(EditorFixture.Shared.Selection.First)));
                    EditorFixture.Shared.Open(alternate);
                    Assert.That(Session(original.Documents).Document, Is.SameAs(Session(native.Documents).Document));
                    Assert.That(((FeatureSampleDocument)Session(native.Documents).Document).Version, Is.EqualTo(7));
                    Assert.That(EditorFixture.Shared.Selection.Count, Is.Zero);
                    EditorFixture.Shared.Open(fixture);
                    if (nativeFirst) { native.Close(); native = null; original.Close(); original = null; }
                    else { original.Close(); original = null; native.Close(); native = null; }
                    Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(count));
                }
            }
            finally
            {
                if (original != null) { original.DiscardChanges(); original.Close(); }
                if (native != null) { native.DiscardChanges(); native.Close(); }
                UnityEngine.Object.DestroyImmediate(alternate);
            }
        }

        private static NBC.ActionEditor.Application.Documents.DocumentSession Session(NBC.ActionEditor.Presentation.Presenters.DocumentPresenter presenter)
            => (NBC.ActionEditor.Application.Documents.DocumentSession)typeof(NBC.ActionEditor.Presentation.Presenters.DocumentPresenter)
                .GetField("session", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(presenter);

        [UnityTest] public IEnumerator DiscardFromEitherWindowRestoresSharedBaselineAndClearsPendingUndo()
        {

            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                foreach (var window in new EditorWindow[] { original, native })
                {
                    EditorFixture.Shared.Session.Edit("discard payload", () => ((FeatureSampleDocument)EditorFixture.Shared.Document).Version = 19);
                    EditorFixture.Shared.Session.BeginEdit();
                    EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = 1;
                    window.DiscardChanges();
                    Assert.That(((FeatureSampleDocument)EditorFixture.Shared.Document).Version, Is.EqualTo(1));
                    Assert.That(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                    Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
                    Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                    Assert.That(Session(native.Documents).Document, Is.SameAs(EditorFixture.Shared.Document));
                    Undo.PerformUndo(); Undo.PerformRedo();
                    Assert.That(((FeatureSampleDocument)EditorFixture.Shared.Document).Version, Is.EqualTo(1), "Discarded changes must not return through Undo/Redo.");
                    yield return null; yield return null;
                    Assert.That(original.hasUnsavedChanges, Is.False);
                    Assert.That(native.hasUnsavedChanges, Is.False);
                }
                EditorFixture.Shared.Session.Edit("new edit", () => ((FeatureSampleDocument)EditorFixture.Shared.Document).Version = 7);
                Undo.PerformUndo(); Assert.That(((FeatureSampleDocument)EditorFixture.Shared.Document).Version, Is.EqualTo(1));
                Undo.PerformRedo(); Assert.That(((FeatureSampleDocument)EditorFixture.Shared.Document).Version, Is.EqualTo(7));
            }
            finally
            {
                EditorFixture.Shared.Open(fixture);
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
            }
        }

        [UnityTest] public IEnumerator ClosingEitherWindowPreservesPlaybackUntilLastViewCloses()
        {

            var existingViews = EditorFixture.SharedViews.Count;
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var stops = 0;
            Action stopped = () => stops++;
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                EditorFixture.Shared.Playback.Stopped += stopped;
                EditorFixture.Shared.Playback.Play(EditorFixture.Shared.Clock.PlaybackTime); EditorFixture.Shared.Player.Sample(1); EditorFixture.Shared.Playback.Pause(true);
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                original.Close(); original = null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                Assert.That(EditorFixture.Shared.Player.CurrentTime, Is.EqualTo(1));
                Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.True);
                Assert.That(stops, Is.Zero);
                yield return null; yield return null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True, "The remaining view owns the same paused playback.");
                original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                yield return null; yield return null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                Assert.That(EditorFixture.Shared.Player.CurrentTime, Is.EqualTo(1));
                native.Close(); native = null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                EditorFixture.Shared.Playback.Play(EditorFixture.Shared.Clock.PlaybackTime); EditorFixture.Shared.Player.Sample(1);
                original.Close(); original = null;
                Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(existingViews), "The test must release exactly its own two views.");
                Assert.That(EditorFixture.Shared.Playback.IsPlaying, Is.EqualTo(existingViews != 0));
                Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.EqualTo(existingViews != 0));
                Assert.That(EditorFixture.Shared.Player.CurrentTime, Is.EqualTo(existingViews == 0 ? 0 : 1));
                Assert.That(stops, Is.EqualTo(existingViews == 0 ? 1 : 0));
            }
            finally
            {
                EditorFixture.Shared.Playback.Stopped -= stopped;
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
                    EditorFixture.Shared.Open(null);
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
                    Assert.That(Views.IMGUI.CreateDocumentWindow.TypeName(typeof(FeatureSampleDocument)), Is.EqualTo(Lan.FeatureSample));
                    var menu = new System.Collections.Generic.List<string>(); Composition.DocumentPresenterComposition.PopulateSaveMenu(EditorFixture.Shared.CreateDocuments(), (label, action) => menu.Add(label));
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

            using var context = new EditorFixture(Composition.DocumentComposition.Create());
            context.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            var views = new ActionEditorViews();
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var requests = 0;
            try
            {
                Func<bool, string> choosePath = saveAs => { Assert.That(saveAs, Is.False); requests++; return null; };
                original.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, EditorFixture.SharedSettings, choosePath));
                native.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, EditorFixture.SharedSettings, choosePath));
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
            EditorFixture.Shared.Player.Reset(); EditorFixture.Shared.Player.Sample(0);
            Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.True, "First sample at zero must initialize preview.");
            EditorFixture.Shared.Player.Sample(1); Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.True);
            EditorFixture.Shared.Player.Reset(); Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.False);
            EditorFixture.Shared.Player.Sample(2); Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.True);
            EditorFixture.Shared.Session.Edit("remove", () => EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips.RemoveAt(0));
            Assert.That(EditorFixture.Shared.Player.IsInitialized, Is.False);
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
