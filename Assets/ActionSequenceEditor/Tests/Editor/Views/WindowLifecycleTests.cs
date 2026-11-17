using ActionSequenceEditor.Application.Playback;
using ActionSequenceEditor.Infrastructure.Preview;
using ActionSequenceEditor.Views.IMGUI;
using ImguiWindow = ActionSequenceEditor.Views.IMGUI.ActionSequenceEditorWindow;
using ToolkitWindow = ActionSequenceEditor.Views.UIToolkit.ActionSequenceEditorWindow;
using ToolkitSettings = ActionSequenceEditor.Views.UIToolkit.SettingsWindow;
using ActionSequenceEditor.Infrastructure.Sequences;
using ActionSequenceEditor.Presentation.Timeline;
using ActionSequenceEditor.Model;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ActionSequenceEditor.Samples.FeatureSample;
using ActionSequenceEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Tests
{
    public sealed class WindowLifecycleTests : SharedContextFixture
    {
        [Test] public void JsonPickerRejectsNonJsonBeforeDecodingAndKeepsCurrentDocument()
        {
            var before = EditorFixture.Shared.Sequence;
            var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionSequenceEditor/Runtime/Model/IDirectable.cs");
            Assert.That(script, Is.Not.Null);
            LogAssert.Expect(LogType.Warning, "请选择 .json 格式的 ActionSequenceEditor 文档。");
            EditorFixture.Shared.CreateSequencePresenter().OpenPicked(script);
            Assert.That(EditorFixture.Shared.Sequence, Is.SameAs(before));
        }

        [UnityTest] public IEnumerator DocumentPickerFiltersBothDiscoveryPathsWithoutRestrictingGenericResources()
        {
            var selector = ScriptableObject.CreateInstance<AssetPickerWindow>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(AssetPickerWindow);
            var filter = type.GetField("_assetPathFilter", flags);
            var init = type.GetMethod("DiscoverEntries", flags);
            var list = type.GetField("_entries", flags);
            try
            {
                var script = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ActionSequenceEditor/Runtime/Model/IDirectable.cs");
                type.GetField("_folderPath", flags).SetValue(selector, "Assets/ActionSequenceEditor");
                filter.SetValue(selector, (Predicate<string>)ActionSequenceEditor.Infrastructure.Sequences.SequenceFileRepository.IsDocumentPath);
                init.Invoke(selector, new object[] { "TextAsset", null });
                foreach (var entry in (Array)list.GetValue(selector))
                    Assert.That(ActionSequenceEditor.Infrastructure.Sequences.SequenceFileRepository.IsDocumentPath((string)entry.GetType().GetField("path").GetValue(entry)), Is.True);
                var allowed = new System.Collections.Generic.List<int> { script.GetInstanceID() };
                init.Invoke(selector, new object[] { "TextAsset", allowed });
                Assert.That(((Array)list.GetValue(selector)).Length, Is.Zero);
                filter.SetValue(selector, null);
                init.Invoke(selector, new object[] { "TextAsset", allowed });
                Assert.That(((Array)list.GetValue(selector)).Length, Is.EqualTo(1), "Generic resource pickers still accept non-JSON TextAssets.");
                Assert.That(ActionSequenceEditor.Infrastructure.Sequences.SequenceFileRepository.IsDocumentPath("Assets/Sample.JSON"), Is.True);
                var nativeBefore = Resources.FindObjectsOfTypeAll<Views.UIToolkit.SequencePickerWindow>();
                var host = ScriptableObject.CreateInstance<ToolkitWindow>();
                Views.UIToolkit.SequencePickerWindow native = null;
                var callbacks = 0;
                try
                {
                    host.Dialogs.Pick(new SequenceDialogCatalog(), _ => callbacks++);
                    native = Resources.FindObjectsOfTypeAll<Views.UIToolkit.SequencePickerWindow>().Except(nativeBefore).Single();
                    native.CreateGUI();
                    yield return null; yield return null;
                    Assert.That(native.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                    var nativeEntries = (Array)native.GetType().GetField("entries", flags).GetValue(native);
                    foreach (var entry in nativeEntries)
                        Assert.That(SequenceFileRepository.IsDocumentPath((string)entry.GetType().GetField("path").GetValue(entry)), Is.True);
                    var search = native.rootVisualElement.Q<UnityEditor.UIElements.ToolbarSearchField>("document-search");
                    search.value = "a-query-that-cannot-match-an-asset-627849a3";
                    var nativeList = native.rootVisualElement.Q<ListView>("document-list");
                    Assert.That(nativeList.itemsSource.Count, Is.EqualTo(1), "None remains available when filtering all assets.");
                    Assert.That(callbacks, Is.Zero, "Search and initial selection never commit a document switch.");
                    Assert.That(native.rootVisualElement.Q<TwoPaneSplitView>("document-preview-split"), Is.Not.Null);
                    Assert.That(native.rootVisualElement.Q<TextField>("document-text-preview").isReadOnly, Is.True);
                    nativeList.selectedIndex = -1; nativeList.selectedIndex = 0;
                    Assert.That(callbacks, Is.EqualTo(1), "Explicit None selection uses the same callback contract as IMGUI.");
                    using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                    { escape.target = search; search.SendEvent(escape); }
                    yield return null;
                    Assert.That(native == null, Is.True, "Escape closes the native picker even while search owns focus.");
                    native = null;
                    Assert.That(callbacks, Is.EqualTo(1), "Closing does not commit another selection.");
                }
                finally { if (native != null) native.Close(); UnityEngine.Object.DestroyImmediate(host); }
            }
            finally { UnityEngine.Object.DestroyImmediate(selector); }
        }

        [Test] public void ControlledQuitRejectsUnsavedDocumentAndCancelsWhenEditingStartsAfterAcceptance()
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var types = AppDomain.CurrentDomain.GetAssemblies();
            var command = types.Select(a => a.GetType("ActionSequenceEditor.Verification.ControlledQuitCommands")).Single(t => t != null).GetMethod("ControlledQuit");
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
            using (var context = new EditorFixture(ActionSequenceEditor.Composition.SequenceComposition.Create()))
            {
                context.Open(fixture);
                var clip = context.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
                context.Selection.Set(new IDirectable[] { clip }, false);
                context.Clipboard.Item = clip;
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Sequence, Is.Not.SameAs(EditorFixture.Shared.Sequence));
                Assert.That(context.Selection.First, Is.SameAs(clip));
                Assert.That(context.Clipboard.Item, Is.SameAs(clip));
                context.Playback.Play(10);
                Assert.That(context.Playback.Update(10.25f, 1), Is.True);
                Assert.That(context.Sampler.CurrentTime, Is.EqualTo(.25f).Within(.0001f));
                context.Playback.SetPaused(true); context.Playback.Update(11, 1);
                Assert.That(context.Sampler.CurrentTime, Is.EqualTo(.25f).Within(.0001f));
                context.Playback.SetPaused(false); context.Playback.Update(11.25f, 1);
                Assert.That(context.Sampler.CurrentTime, Is.EqualTo(.5f).Within(.0001f));
                context.Open(null);
                Assert.That(context.Selection.Count, Is.Zero);
                Assert.That(context.Playback.IsPlaying || context.Playback.IsPaused || context.Sampler.IsInitialized, Is.False);
                Assert.That(context.Sampler.CurrentTime, Is.Zero);
            }
        }

        [Test] public void FailedOpenKeepsContextAndInjectedPlayer()
        {
            var context = EditorFixture.Shared; var document = context.Sequence;
            var clip = document.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
            var invalid = new TextAsset("{}");
            try
            {
                Assert.Throws<System.IO.InvalidDataException>(() => EditorFixture.Shared.Open(invalid));
                Assert.That(context.Sequence, Is.SameAs(document));
                Assert.That(context.Source, Is.SameAs(fixture));
                Assert.That(context.Selection.First, Is.SameAs(clip));
                Assert.That(typeof(SequenceSampler).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), Is.Empty);
                EditorFixture.Shared.ClipboardEditing.Copy(clip);
                Assert.That(context.Clipboard.Item, Is.SameAs(EditorFixture.Shared.Clipboard.Item));
                Assert.That(context.Clipboard.Type, Is.EqualTo(clip.GetType()));
                EditorFixture.Shared.Playback.SetPaused(true); Assert.That(context.Playback.IsPaused, Is.True);
                context.Playback.SetPaused(false); Assert.That(EditorFixture.Shared.Playback.IsPaused, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(invalid); }
        }

        [Test] public void ViewRegistrationIsIdempotentAndDetachesNotifications()
        {
            var views = new EditorViewRegistry(); var first = new object(); var second = new object();
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
            var alternate = new TextAsset(ActionSequenceEditor.Infrastructure.Serialization.SequenceJsonCodec.Encode(new FeatureSampleSequence { Version = 7 }));
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
                    Assert.That(Session(original.Sequence), Is.SameAs(Session(native.Sequence)));
                    Assert.That(Session(original.Sequence), Is.SameAs(EditorFixture.Shared.Session));
                    native.CreateGUI(); native.CreateGUI();
                    Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(count + 2));
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(ToolkitWindow).GetMethod("OnEnable", flags).Invoke(native, null);
                    typeof(ImguiWindow).GetMethod("OnEnable", flags).Invoke(original, null);
                    Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(count + 2), "Repeated enable must replace registrations rather than duplicate them.");
                    native.Select(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0]);
                    Assert.That(original.Presenter.State.Clips.Single(c => c.Selected).Key, Is.EqualTo(original.Presenter.KeyFor(EditorFixture.Shared.Selection.First)));
                    EditorFixture.Shared.Open(alternate);
                    Assert.That(Session(original.Sequence).Sequence, Is.SameAs(Session(native.Sequence).Sequence));
                    Assert.That(((FeatureSampleSequence)Session(native.Sequence).Sequence).Version, Is.EqualTo(7));
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

        private static ActionSequenceEditor.Application.Sequences.SequenceSession Session(ActionSequenceEditor.Presentation.Presenters.SequencePresenter presenter)
            => (ActionSequenceEditor.Application.Sequences.SequenceSession)typeof(ActionSequenceEditor.Presentation.Presenters.SequencePresenter)
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
                    EditorFixture.Shared.Session.Edit("discard payload", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 19);
                    EditorFixture.Shared.Session.BeginEdit();
                    EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime = 1;
                    window.DiscardChanges();
                    Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
                    Assert.That(EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0].StartTime, Is.EqualTo(.25f));
                    Assert.That(EditorFixture.Shared.Session.IsDirty, Is.False);
                    Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                    Assert.That(Session(native.Sequence).Sequence, Is.SameAs(EditorFixture.Shared.Sequence));
                    Undo.PerformUndo(); Undo.PerformRedo();
                    Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1), "Discarded changes must not return through Undo/Redo.");
                    yield return null; yield return null;
                    Assert.That(original.hasUnsavedChanges, Is.False);
                    Assert.That(native.hasUnsavedChanges, Is.False);
                }
                EditorFixture.Shared.Session.Edit("new edit", () => ((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version = 7);
                Undo.PerformUndo(); Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(1));
                Undo.PerformRedo(); Assert.That(((FeatureSampleSequence)EditorFixture.Shared.Sequence).Version, Is.EqualTo(7));
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
                EditorFixture.Shared.Playback.Play(EditorFixture.Shared.Clock.PlaybackTime); EditorFixture.Shared.Sampler.Sample(1); EditorFixture.Shared.Playback.SetPaused(true);
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                original.Close(); original = null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.EqualTo(1));
                Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.True);
                Assert.That(stops, Is.Zero);
                yield return null; yield return null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True, "The remaining view owns the same paused playback.");
                original = ScriptableObject.CreateInstance<ImguiWindow>(); original.Show();
                yield return null; yield return null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.EqualTo(1));
                native.Close(); native = null;
                Assert.That(EditorFixture.Shared.Playback.IsPlaying && EditorFixture.Shared.Playback.IsPaused, Is.True);
                EditorFixture.Shared.Playback.Play(EditorFixture.Shared.Clock.PlaybackTime); EditorFixture.Shared.Sampler.Sample(1);
                original.Close(); original = null;
                Assert.That(EditorFixture.SharedViews.Count, Is.EqualTo(existingViews), "The test must release exactly its own two views.");
                Assert.That(EditorFixture.Shared.Playback.IsPlaying, Is.EqualTo(existingViews != 0));
                Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.EqualTo(existingViews != 0));
                Assert.That(EditorFixture.Shared.Sampler.CurrentTime, Is.EqualTo(existingViews == 0 ? 0 : 1));
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
            var fields = typeof(EditorText).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Where(f => f.FieldType == typeof(string)).ToArray();
            var previousTerms = fields.Select(f => f.GetValue(null)).ToArray();

            ImguiWindow original = null; ToolkitWindow native = null;
            try
            {
                foreach (var language in new[] { typeof(ChineseSimplifiedText), typeof(EnglishText) })
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
                    Assert.That(original.titleContent.text, Is.EqualTo(EditorText.Title));
                    Assert.That(native.titleContent.text, Is.EqualTo(EditorText.Title + " · UI Toolkit"));
                    var buttons = native.rootVisualElement.Q("timeline-welcome").Query<Button>().ToList();
                    Assert.That(buttons.Select(b => b.text), Does.Contain(EditorText.CreateSequence).And.Contain(EditorText.OpenSequence));
                    var labels = native.rootVisualElement.Q("timeline-welcome").Query<Label>().ToList();
                    Assert.That(labels.Select(l => l.text), Does.Contain(EditorText.WelcomeHint).And.Contain(EditorText.WelcomeHelp));
                    Assert.That(Views.IMGUI.SequenceCreationWindow.TypeName(typeof(FeatureSampleSequence)), Is.EqualTo(EditorText.FeatureSample));
                    var menu = new System.Collections.Generic.List<string>(); Composition.SequencePresenterComposition.PopulateSaveMenu(EditorFixture.Shared.CreateSequencePresenter(), (label, action) => menu.Add(label));
                    Assert.That(menu, Is.EqualTo(new[] { EditorText.Save, EditorText.SaveAs, EditorText.Export }));
                    Assert.That(string.Join(" ", menu) + EditorText.WelcomeHint + EditorText.CreateSafety + EditorText.FeatureSample, Does.Not.Contain("JSON").And.Not.Contain("旧版").And.Not.Contain("新版"));
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

            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            context.Session.Open(new ActionSequenceEditor.Application.Sequences.SequenceOpenData(new FeatureSampleSequence()));
            var views = new EditorViewRegistry();
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var requests = 0;
            try
            {
                Func<bool, string> choosePath = saveAs => { Assert.That(saveAs, Is.False); requests++; return null; };
                original.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, EditorFixture.SharedSettings, choosePath));
                native.Bind((owner, repaint, changed) => context.Bind(owner, repaint, changed, views, EditorFixture.SharedSettings, choosePath));
                context.Session.Edit("change", () => ((FeatureSampleSequence)context.Sequence).Version = 9);
                var source = context.Sequence; var before = context.Session.CaptureRecovery();
                original.SaveChanges(); native.SaveChanges();
                Assert.That(requests, Is.EqualTo(2)); Assert.That(context.Sequence, Is.SameAs(source));
                Assert.That(context.Session.CaptureRecovery(), Is.EqualTo(before)); Assert.That(context.Session.IsDirty, Is.True);
                original.Presenter.Key(new KeyInput(TimelineKey.Save));
                native.Canvas.Presenter.Key(new KeyInput(TimelineKey.Save));
                Assert.That(requests, Is.EqualTo(4), "Timeline shortcuts must retain the same window-owned path request.");
                Assert.That(context.Session.CaptureRecovery(), Is.EqualTo(before));
                var binding = (Composition.WindowBinding)typeof(ImguiWindow).GetProperty("Binding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(original);
                var entries = new System.Collections.Generic.List<string>(); binding.PopulateSaveMenu((label, action) => entries.Add(label));
                Assert.That(entries, Is.EqualTo(new[] { EditorText.Save, EditorText.SaveAs, EditorText.Export }));
            }
            finally
            {
                original.DiscardChanges(); native.DiscardChanges();
                UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(native);

            }
        }

        [Test] public void PreviewResetDisposesOwnedCallbacksAndAllowsRebuild()
        {
            EditorFixture.Shared.Sampler.Reset(); EditorFixture.Shared.Sampler.Sample(0);
            Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.True, "First sample at zero must initialize preview.");
            EditorFixture.Shared.Sampler.Sample(1); Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.True);
            EditorFixture.Shared.Sampler.Reset(); Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.False);
            EditorFixture.Shared.Sampler.Sample(2); Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.True);
            EditorFixture.Shared.Session.Edit("remove", () => EditorFixture.Shared.Sequence.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.RemoveAt(0));
            Assert.That(EditorFixture.Shared.Sampler.IsInitialized, Is.False);
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
