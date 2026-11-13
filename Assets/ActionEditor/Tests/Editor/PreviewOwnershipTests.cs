using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NBC.ActionEditor.Tests
{
    public sealed class PreviewOwnershipTests
    {
        [Test] public void FeatureSampleProjectionUsesValuesAndKeepsCapturedFramesIndependent()
        {
            var document = new FeatureSampleDocument();
            var time = .5f;
            var presenter = new NBC.ActionEditor.Presentation.Presenters.FeatureSamplePresenter(() => document, () => time);
            var state = presenter.Capture();
            Assert.That(state.Available, Is.True);
            Assert.That(state.Intervals.Count, Is.EqualTo(1));
            Assert.That(state.Intervals[0].Progress, Is.EqualTo(1f / 6).Within(.00001f));
            Assert.That(state.Loops.Count, Is.EqualTo(1));
            Assert.That(state.Loops[0].Iteration, Is.EqualTo(1));
            Assert.That(state.Loops[0].Time, Is.Zero);
            Assert.That(state.Markers.Count, Is.EqualTo(1));
            Assert.That(state.Markers[0].Time, Is.EqualTo(.5f));
            var caption = state.Intervals[0].Caption;
            ((FeatureSampleInterval)document.AllTracks.First().Clips[0]).Caption = "changed";
            time = 2;
            Assert.That(state.Time, Is.EqualTo(.5f));
            Assert.That(state.Intervals[0].Caption, Is.EqualTo(caption));
            var later = presenter.Capture();
            Assert.That(later.Intervals.Any(i => i.Caption == caption), Is.False);
            Assert.That(later.Loops[0].Iteration, Is.EqualTo(3));
            Assert.That(later.Loops[0].Time, Is.Zero);
            document = null;
            Assert.That(presenter.Capture().Available, Is.False);
            Assert.That(presenter.Capture().Intervals, Is.Empty);
        }

        private class Probe : PreviewBase
        {
            public int Disposals, Updates;
            public bool ThrowUpdate;
            public override void Update(float time, float previousTime) { Updates++; if (ThrowUpdate) throw new InvalidOperationException("preview failure"); }
            public override void Dispose() { Disposals++; }
        }
        private sealed class ReentrantProbe : Probe
        {
            public Action OnEnter, OnDispose;
            public override void Enter() => OnEnter?.Invoke();
            public override void Dispose() { base.Dispose(); OnDispose?.Invoke(); }
        }

        [Test] public void PreviewCallbackCanReleaseSessionWithoutEvaluatingDisposedPointers()
        {
            var document = new FeatureSampleDocument();
            var probes = new List<ReentrantProbe>();
            ActionEditorPreviewSession session = null;
            session = new ActionEditorPreviewSession(type =>
            {
                var probe = new ReentrantProbe { OnEnter = () => session.Dispose(), OnDispose = () => session.Dispose() };
                probes.Add(probe); return probe;
            });
            session.Initialize(document);
            session.Sample(1, 0, true);
            Assert.That(session.IsInitialized, Is.False);
            Assert.That(probes.All(p => p.Disposals == 1 && p.Updates == 0), Is.True);
            session.Sample(2, 1, true); session.Dispose();
            Assert.That(probes.All(p => p.Disposals == 1), Is.True);
        }
        [TestCase(false)] [TestCase(true)] public void PreviewFactoryCancellationReleasesReturnedButUnownedInstance(bool returnsNull)
        {
            var probe = new Probe();
            ActionEditorPreviewSession session = null;
            session = new ActionEditorPreviewSession(_ => { session.Dispose(); return returnsNull ? null : probe; });
            Assert.Throws<OperationCanceledException>(() => session.Initialize(new FeatureSampleDocument()));
            Assert.That(session.IsInitialized, Is.False);
            Assert.That(probe.Disposals, Is.EqualTo(returnsNull ? 0 : 1));
            session.Dispose();
            Assert.That(probe.Disposals, Is.EqualTo(returnsNull ? 0 : 1));
        }
        private class Target : TimelineClip { }
        [Test] public void PlaybackStoppedDuringLoopBoundaryCannotRecreatePreviewOrAdvancePreviousTime()
        {
            var asset = new FeatureSampleDocument();
            var probes = new List<ReentrantProbe>();
            ActionEditorPlayback playback = null;
            var player = new AssetPlayer(() => asset, _ =>
            {
                var probe = new ReentrantProbe { OnEnter = () => playback.Stop() };
                probes.Add(probe); return probe;
            });
            playback = new ActionEditorPlayback(player);
            playback.Play(0);
            playback.Update(asset.Length + 1, 1);
            Assert.That(playback.IsPlaying || player.IsInitialized, Is.False);
            Assert.That(player.CurrentTime, Is.Zero);
            Assert.That(player.previousTime, Is.Zero);
            Assert.That(probes.Count, Is.GreaterThan(0));
            Assert.That(probes.All(p => p.Disposals == 1 && p.Updates == 0), Is.True);
        }

        [Test] public void PlayedCallbackCanStopWithoutPlayOverwritingItsDecision()
        {
            var playback = new ActionEditorPlayback(new AssetPlayer(() => new FeatureSampleDocument()));
            playback.Played += () => playback.Stop();
            playback.Play(0);
            Assert.That(playback.IsPlaying || playback.IsPaused, Is.False);
        }

        private class DerivedTarget : Target { }
        [CustomPreview(typeof(Target))] private class First : Probe { }
        [CustomPreview(typeof(Target))] private class Override : First { }
        [CustomPreview(typeof(Target))] private class Conflict : Probe { }
        [Test] public void PreviewResolutionKeepsExactAndDerivedOverrideAndReportsConflicts()
        {
            Assert.That(ActionEditorTypeCatalog.ResolvePreview(new[] { typeof(First), typeof(Override) }, typeof(Target)), Is.EqualTo(typeof(Override)));
            Assert.That(ActionEditorTypeCatalog.ResolvePreview(new[] { typeof(First) }, typeof(DerivedTarget)), Is.Null, "Preview bindings remain exact, unlike opted-in inspectors.");
            var error = Assert.Throws<InvalidOperationException>(() => ActionEditorTypeCatalog.ResolvePreview(new[] { typeof(First), typeof(Conflict) }, typeof(Target)));
            Assert.That(error.Message, Does.Contain(typeof(First).FullName).And.Contain(typeof(Conflict).FullName));
        }
        [Test] public void PreviewSessionReusesResourcesAndDisposesOnceOnResetAndFailure()
        {
            var asset = new FeatureSampleDocument(); var probes = new List<Probe>();
            var player = new AssetPlayer(() => asset, type =>
            {
                if (type != typeof(FeatureSampleInterval)) return null;
                var probe = new Probe(); probes.Add(probe); return probe;
            });
            player.Sample(0); var count = probes.Count;
            Assert.That(count, Is.GreaterThan(0));
            player.Sample(1); player.Sample(2); Assert.That(probes.Count, Is.EqualTo(count));
            player.Reset(); player.Reset(); Assert.That(probes.All(p => p.Disposals == 1), Is.True);
            player.Sample(0); Assert.That(probes.Count, Is.EqualTo(count * 2));
            probes.Skip(count).First().ThrowUpdate = true;
            Assert.Throws<InvalidOperationException>(() => player.Sample(2));
            Assert.That(player.IsInitialized, Is.False);
            Assert.That(probes.All(p => p.Disposals == 1), Is.True);
            player.Reset(); Assert.That(probes.All(p => p.Disposals == 1), Is.True);
        }
        [Test] public void PartialFactoryFailureReleasesAlreadyCreatedResources()
        {
            var asset = new FeatureSampleDocument(); var probe = new Probe(); var count = 0;
            var player = new AssetPlayer(() => asset, type =>
            {
                if (++count == 2) throw new InvalidOperationException("factory failure");
                return probe;
            });
            Assert.Throws<InvalidOperationException>(() => player.Sample(1));
            Assert.That(player.IsInitialized, Is.False); Assert.That(probe.Disposals, Is.EqualTo(1));
            player.Reset(); Assert.That(probe.Disposals, Is.EqualTo(1));
        }
        [Test] public void PlaybackPauseLoopStepAndFaultKeepResourceOwnershipExplicit()
        {
            var asset = new FeatureSampleDocument(); var probes = new List<Probe>();
            var player = new AssetPlayer(() => asset, type => { if (type != typeof(FeatureSampleInterval)) return null; var p = new Probe(); probes.Add(p); return p; });
            var playback = new ActionEditorPlayback(player);
            playback.Play(10); playback.Update(11, 1); Assert.That(player.CurrentTime, Is.EqualTo(1));
            playback.Pause(true); playback.Update(12, 1); Assert.That(player.CurrentTime, Is.EqualTo(1));
            playback.Pause(false); playback.Update(13, 1); Assert.That(player.CurrentTime, Is.EqualTo(2));
            playback.StepBackward(.25f); Assert.That(player.CurrentTime, Is.EqualTo(1.75f));
            playback.StepForward(.25f); Assert.That(player.CurrentTime, Is.EqualTo(2));
            playback.IsRange = true; asset.RangeMin = 1; asset.RangeMax = 3;
            playback.Update(15, 1); Assert.That(player.CurrentTime, Is.EqualTo(2));
            Assert.That(probes.Any(p => p.Disposals == 1), Is.True, "Loop releases the old preview session.");
            foreach (var probe in probes.Where(p => p.Disposals == 0)) probe.ThrowUpdate = true;
            Assert.Throws<InvalidOperationException>(() => player.Sample(4));
            Assert.That(playback.IsPlaying || playback.IsPaused || player.IsInitialized, Is.False);
            playback.Stop(); Assert.That(player.CurrentTime, Is.Zero);
            Assert.That(probes.All(p => p.Disposals == 1), Is.True);
        }
        [Test] public void StopCallbackFailureStillReleasesPreviewAndClearsPlayback()
        {
            var asset = new FeatureSampleDocument(); var probes = new List<Probe>();
            var player = new AssetPlayer(() => asset, type => { var p = new Probe(); probes.Add(p); return p; });
            var playback = new ActionEditorPlayback(player); playback.Play(0); player.Sample(1);
            playback.Stopped += () => throw new InvalidOperationException("stop callback failure");
            Assert.Throws<InvalidOperationException>(() => playback.Stop());
            Assert.That(player.IsInitialized || playback.IsPlaying || playback.IsPaused, Is.False);
            Assert.That(probes.All(p => p.Disposals == 1), Is.True);
        }
        [UnityTest] public IEnumerator BothWindowsKeepExactlyOneSharedEditorDriver()
        {
            var previousWindow = ActionEditorHost.Window;
            var source = App.TextAsset; var recovery = App.Session.CaptureRecovery();
            var fixture = new TextAsset(Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument())); App.TextAsset = fixture;
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            var native = ScriptableObject.CreateInstance<Workbench.UIToolkitActionEditorWindow>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                Assert.That(EditorApplication.update.GetInvocationList().Count(callback => callback.Method.DeclaringType == typeof(ActionEditorHost) && callback.Method.Name == "UpdateSharedEditor"), Is.EqualTo(1));
                Assert.That(EditorApplication.update.GetInvocationList().Any(callback => callback.Target is ActionEditorWindow || callback.Target is Workbench.UIToolkitActionEditorWindow), Is.False);
                App.Context.Playback.Play((float)EditorApplication.timeSinceStartup);
                App.Context.Playback.Pause(true);
                native.Close();
                Assert.That(App.Context.Playback.IsPlaying, Is.True, "Closing one view must not stop the shared playback.");
                Assert.That(App.Context.Playback.IsPaused, Is.True);
            }
            finally
            {
                original.DiscardChanges();
                if (native != null) { native.DiscardChanges(); native.Close(); }
                original.Close(); ActionEditorHost.Window = previousWindow;
                App.TextAsset = source; App.Session.RestoreRecovery(recovery); UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        [Test] public void SharedDriverSkipsDetachedViewsAndIsolatesTickFailures()
        {
            var views = new ActionEditorViews();
            var first = new object(); var second = new object(); var third = new object();
            var calls = new List<string>(); var errors = new List<Exception>();
            foreach (var owner in new[] { first, second, third }) views.Attach(owner, () => { }, () => { });
            views.SetTick(first, () => { calls.Add("first"); views.Detach(second); throw new InvalidOperationException("tick"); });
            views.SetTick(second, () => calls.Add("detached"));
            views.SetTick(third, () => calls.Add("third"));
            views.Tick(errors.Add);
            Assert.That(calls, Is.EqualTo(new[] { "first", "third" }));
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => views.SetTick(second, () => { }));
            views.Detach(first); views.Detach(third); calls.Clear();
            views.Tick(errors.Add); Assert.That(calls, Is.Empty);
        }

        private sealed class Preferences : NBC.ActionEditor.Application.Settings.IPreferencesStore
        {
            public NBC.ActionEditor.Application.Settings.EditorSettings Read() => new NBC.ActionEditor.Application.Settings.EditorSettings(
                NBC.ActionEditor.Application.Settings.TimeStepMode.Seconds, .1f, 30, true, true, 0, "Assets/");
            public void Write(NBC.ActionEditor.Application.Settings.EditorSettings value) => throw new AssertionException("Closing a window must not write preferences.");
        }

        [Test] public void SharedUpdateDriverStillTicksViewsWhenPlaybackAndStopCallbacksFail()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            context.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            var views = new ActionEditorViews(); var owner = new object(); var ticks = 0;
            views.Attach(owner, () => throw new InvalidOperationException("repaint"), () => { });
            views.SetTick(owner, () => ticks++);
            var errors = new List<Exception>();
            var driver = new Composition.EditorUpdateDriver(context, views,
                new NBC.ActionEditor.Application.Settings.SettingsSession(new Preferences()), errors.Add);
            context.Playback.Play(context.Clock.PlaybackTime);
            context.Playback.Stopped += () => throw new InvalidOperationException("stop");
            driver.Tick();
            Assert.That(ticks, Is.EqualTo(1));
            Assert.That(errors.Select(e => e.Message), Is.EquivalentTo(new[] { "repaint", "stop" }));
            Assert.That(context.Playback.IsPlaying || context.Player.IsInitialized, Is.False);
            views.Detach(owner); driver.Tick(); Assert.That(ticks, Is.EqualTo(1));
        }

        [Test] public void IsolatedBindingsReleaseLastPlaybackAndSubscriptionsExactlyOnce()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            var fixture = new TextAsset(Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument()));
            var views = new ActionEditorViews();
            var settings = new NBC.ActionEditor.Application.Settings.SettingsSession(new Preferences());
            var redraws = 0; var stopped = 0;
            var first = new Composition.WindowBinding(new object(), () => redraws++, () => { }, context, views, settings, () => DateTime.MinValue);
            var second = new Composition.WindowBinding(new object(), () => redraws++, () => { }, context, views, settings, () => DateTime.MinValue);
            try
            {
                context.Open(fixture);
                first.Playback.Invoke(NBC.ActionEditor.Presentation.Contracts.PlaybackIntent.Next);
                Assert.That(context.Player.CurrentTime, Is.EqualTo(.1f).Within(.00001f), "Step must use the settings supplied to this binding.");
                first.Playback.Invoke(NBC.ActionEditor.Presentation.Contracts.PlaybackIntent.Previous);
                Assert.That(context.Player.CurrentTime, Is.Zero);
                context.Open(fixture); context.Playback.Play(0); context.Player.Sample(1); context.Playback.Pause(true);
                context.Playback.Stopped += () => stopped++;
                first.Dispose(); first.Dispose();
                Assert.That(views.Count, Is.EqualTo(1)); Assert.That(stopped, Is.Zero);
                Assert.That(context.Playback.IsPlaying && context.Playback.IsPaused, Is.True);
                second.Dispose(); second.Dispose();
                Assert.That(views.Count, Is.Zero); Assert.That(stopped, Is.EqualTo(1));
                Assert.That(context.Playback.IsPlaying || context.Playback.IsPaused || context.Player.IsInitialized, Is.False);
                Assert.That(context.Player.CurrentTime, Is.Zero);
                var count = redraws; views.Repaint(); views.NotifyChanged();
                Assert.That(redraws, Is.EqualTo(count));
                var listeners = typeof(NBC.ActionEditor.Application.Settings.SettingsSession).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(settings) as Delegate;
                Assert.That(listeners, Is.Null);
            }
            finally { first.Dispose(); second.Dispose(); UnityEngine.Object.DestroyImmediate(fixture); }
        }
    }
}
