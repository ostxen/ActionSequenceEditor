using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
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
        private sealed class BoundaryTrack : TimelineTrack { }
        private sealed class BoundaryInterval : TimelineClip { }
        private sealed class BoundaryPoint : PointClip { }
        private sealed class BoundaryPreview : PreviewBase
        {
            private readonly List<string> calls;
            public BoundaryPreview(List<string> calls) => this.calls = calls;
            public override void Enter() => calls.Add(directable.Name + ":enter");
            public override void Exit() => calls.Add(directable.Name + ":exit");
            public override void Update(float time, float previousTime) => calls.Add(directable.Name + ":update");
            public override void Dispose() { }
        }

        [Test] public void ForwardPreviewCrossesZeroShortIntervalAndEndpointOnlyOnce()
        {
            var document = new FeatureSampleDocument(); document.ClearRoots();
            var track = new BoundaryTrack(); document.AddRoot(track);
            track.Clips.Add(new BoundaryPoint { Name = "zero", StartTime = 0 });
            track.Clips.Add(new BoundaryInterval { Name = "short", StartTime = .25f, Length = .000001f });
            track.Clips.Add(new BoundaryPoint { Name = "endpoint", StartTime = 1 });
            document.Init();
            var calls = new List<string>();
            using var preview = new ActionEditorPreviewSession(type => typeof(TimelineClip).IsAssignableFrom(type) ? new BoundaryPreview(calls) : null);
            preview.Initialize(document);
            preview.Sample(1, 0, true);
            Assert.That(calls, Is.EqualTo(new[] { "zero:enter", "zero:update", "zero:update", "zero:exit",
                "short:enter", "short:update", "short:update", "short:exit",
                "endpoint:enter", "endpoint:update", "endpoint:update", "endpoint:exit" }));
            calls.Clear(); preview.Sample(1, 1, true); preview.Sample(2, 1, true);
            Assert.That(calls, Is.Empty, "Repeated samples and subsequent advances must not enter crossed points twice.");
        }

        [TestCase(.249999f, false)]
        [TestCase(.25f, true)]
        [TestCase(.749999f, true)]
        [TestCase(.75f, false)]
        public void PreviewContinuousUpdatesUseLeftClosedRightOpenInterval(float time, bool expected)
        {
            var document = new FeatureSampleDocument(); document.ClearRoots();
            var track = new BoundaryTrack(); document.AddRoot(track);
            var clip = new BoundaryInterval { Name = "interval", StartTime = .25f, Length = .5f };
            track.Clips.Add(clip); document.Init();
            var calls = new List<string>(); var target = new BoundaryPreview(calls); target.SetTarget(clip);
            IDirectableTimePointer pointer = new StartTimePointer(target);
            pointer.Update(time, .2f);
            Assert.That(calls.Count, Is.EqualTo(expected ? 1 : 0));
        }

        [Test] public void EqualTimePreviewBoundariesUseReverseTrackThenStableClipOrder()
        {
            var document = new FeatureSampleDocument(); document.ClearRoots();
            var first = new BoundaryTrack(); var second = new BoundaryTrack();
            first.Clips.Add(new BoundaryInterval { Name = "old", StartTime = 0, Length = 1 });
            first.Clips.Add(new BoundaryPoint { Name = "point", StartTime = 1 });
            first.Clips.Add(new BoundaryInterval { Name = "next", StartTime = 1, Length = 1 });
            second.Clips.Add(new BoundaryPoint { Name = "other", StartTime = 1 });
            document.AddRoot(first); document.AddRoot(second); document.Init();
            var calls = new List<string>();
            using var preview = new ActionEditorPreviewSession(type => typeof(TimelineClip).IsAssignableFrom(type) ? new BoundaryPreview(calls) : null);
            preview.Initialize(document); preview.Sample(.5f, 0, true); calls.Clear();
            preview.Sample(1, .5f, true);
            Assert.That(calls.Where(call => !call.EndsWith(":update")), Is.EqualTo(new[] {
                "other:enter", "other:exit", "old:exit", "point:enter", "point:exit", "next:enter" }));
            calls.Clear(); preview.Sample(1, 1, true);
            Assert.That(calls, Is.EqualTo(new[] { "next:update" }));
        }

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

        [Test] public void ExtensionContextsAndInspectorFactoriesUseOnlyTheirExplicitSession()
        {
            using var first = new ActionEditorContext(Composition.DocumentComposition.Create());
            using var second = new ActionEditorContext(Composition.DocumentComposition.Create());
            first.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            second.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            ((FeatureSampleDocument)first.Document).Version = 17;
            ((FeatureSampleDocument)second.Document).Version = 29;
            var a = new EditorExtensionContext(() => first.Document, () => .5f);
            var b = new EditorExtensionContext(() => second.Document, () => 2f);
            Assert.That(Composition.FeatureSampleComposition.Create(a).Capture().Time, Is.EqualTo(.5f));
            Assert.That(Composition.FeatureSampleComposition.Create(b).Capture().Time, Is.EqualTo(2f));
            var proxy = ScriptableObject.CreateInstance<InspectorPreviewAsset>();
            var settings = new NBC.ActionEditor.Application.Settings.SettingsSession(new Preferences());
            try
            {
                Assert.That(proxy.CreatePresenter(), Is.Null);
                proxy.Bind(() => Composition.PropertyComposition.Create(second, settings));
                var presenter = proxy.CreatePresenter();
                Assert.That(presenter.Document, Is.SameAs(second.Document));
                Assert.That(proxy.CreatePresenter(), Is.Not.SameAs(presenter));
                Assert.That(presenter.Settings, Is.SameAs(settings.Current));
                Assert.That(presenter.Settings.SnapInterval, Is.EqualTo(.1f));
                presenter.Write(presenter.Field(second.Document, typeof(FeatureSampleDocument).GetField("Version")).Path, 31);
                Assert.That(((FeatureSampleDocument)second.Document).Version, Is.EqualTo(31));
                Assert.That(((FeatureSampleDocument)first.Document).Version, Is.EqualTo(17));
            }
            finally { UnityEngine.Object.DestroyImmediate(proxy); }
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
            public Action OnEnter, OnDispose, OnUpdate;
            public override void Enter() => OnEnter?.Invoke();
            public override void Update(float time, float previousTime) { base.Update(time, previousTime); OnUpdate?.Invoke(); }
            public override void Dispose() { base.Dispose(); OnDispose?.Invoke(); }
        }

        [Test] public void PreviewUpdateCanReleaseAndClearTargetBeforePointerReturns()
        {
            var document = new FeatureSampleDocument();
            var clip = document.AllTracks.First().Clips.First();
            var probe = new ReentrantProbe();
            probe.SetTarget(clip);
            var active = true;
            IDirectableTimePointer pointer = new StartTimePointer(probe, () => active);
            pointer.TriggerForward(.3f, 0);
            probe.OnUpdate = () => { active = false; probe.Dispose(); probe.directable = null; };
            Assert.DoesNotThrow(() => pointer.Update(.4f, .3f));
            Assert.That(active, Is.False);
            Assert.That(probe.Disposals, Is.EqualTo(1));
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

            var source = ActionEditorHost.Context.Source; var recovery = ActionEditorHost.Context.Session.CaptureRecovery();
            var fixture = new TextAsset(Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument())); ActionEditorHost.Context.Open(fixture);
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                Assert.That(EditorApplication.update.GetInvocationList().Count(callback => callback.Method.DeclaringType == typeof(ActionEditorHost) && callback.Method.Name == "UpdateSharedEditor"), Is.EqualTo(1));
                Assert.That(EditorApplication.update.GetInvocationList().Any(callback => callback.Target is ImguiWindow || callback.Target is ToolkitWindow), Is.False);
                ActionEditorHost.Context.Playback.Play((float)EditorApplication.timeSinceStartup);
                ActionEditorHost.Context.Playback.Pause(true);
                native.Close();
                Assert.That(ActionEditorHost.Context.Playback.IsPlaying, Is.True, "Closing one view must not stop the shared playback.");
                Assert.That(ActionEditorHost.Context.Playback.IsPaused, Is.True);
            }
            finally
            {
                original.DiscardChanges();
                if (native != null) { native.DiscardChanges(); native.Close(); }
                original.Close();
                ActionEditorHost.Context.Open(source); ActionEditorHost.Context.Session.RestoreRecovery(recovery); UnityEngine.Object.DestroyImmediate(fixture);
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

        [Test] public void FailedWindowNotificationDoesNotBlockOtherBindingsOrHideWarning()
        {
            using var context = new ActionEditorContext(Composition.DocumentComposition.Create());
            var views = new ActionEditorViews();
            var settings = new NBC.ActionEditor.Application.Settings.SettingsSession(new Preferences());
            var changed = 0; var paints = 0;
            using var first = new Composition.WindowBinding(new object(), () => paints++,
                () => throw new InvalidOperationException("first window unavailable"), context, views, settings, () => DateTime.MinValue);
            using var second = new Composition.WindowBinding(new object(), () => { },
                () => changed++, context, views, settings, () => DateTime.MinValue);
            var before = paints;
            context.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(paints, Is.EqualTo(before + 1));
            Assert.That(context.Session.NotificationWarning, Does.Contain("first window unavailable"));
            first.Dispose();
            context.Session.Open(new NBC.ActionEditor.Application.Documents.DocumentOpenData(new FeatureSampleDocument()));
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(context.Session.NotificationWarning, Is.Null);
        }

        [Test] public void RepaintFailureAndDetachDoNotBlockOtherRegisteredWindows()
        {
            var views = new ActionEditorViews();
            var first = new object(); var removed = new object(); var last = new object();
            var calls = new List<string>();
            views.Attach(first, () => { calls.Add("first"); views.Detach(removed); throw new InvalidOperationException("repaint"); }, () => { });
            views.Attach(removed, () => calls.Add("removed"), () => { });
            views.Attach(last, () => calls.Add("last"), () => { });
            var error = Assert.Throws<AggregateException>(() => views.Repaint());
            Assert.That(calls, Is.EqualTo(new[] { "first", "last" }));
            Assert.That(error.Flatten().InnerExceptions.Single().Message, Is.EqualTo("repaint"));
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
            Assert.That(errors.SelectMany(error => error is AggregateException aggregate
                ? aggregate.Flatten().InnerExceptions.AsEnumerable() : new[] { error }).Select(error => error.Message),
                Is.EquivalentTo(new[] { "repaint", "stop" }));
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
