using ActionSequenceEditor.Infrastructure.Discovery;
using ActionSequenceEditor.Application.Playback;
using ActionSequenceEditor.Infrastructure.Preview;
using ImguiWindow = ActionSequenceEditor.Views.IMGUI.ActionSequenceEditorWindow;
using ToolkitWindow = ActionSequenceEditor.Views.UIToolkit.ActionSequenceEditorWindow;
using ActionSequenceEditor.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ActionSequenceEditor.Tests
{
    public sealed class PreviewOwnershipTests
    {
        private sealed class BoundaryTrack : ActionTrack { }
        private sealed class BoundaryInterval : ActionClip { }
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
            var document = new FeatureSampleSequence(); document.ClearRootContainers();
            var track = new BoundaryTrack(); document.AddRootContainer(track);
            track.Clips.Add(new BoundaryPoint { Name = "zero", StartTime = 0 });
            track.Clips.Add(new BoundaryInterval { Name = "short", StartTime = .25f, Length = .000001f });
            track.Clips.Add(new BoundaryPoint { Name = "endpoint", StartTime = 1 });
            document.Init();
            var calls = new List<string>();
            using var preview = new SequencePreviewSession(type => typeof(ActionClip).IsAssignableFrom(type) ? new BoundaryPreview(calls) : null);
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
            var document = new FeatureSampleSequence(); document.ClearRootContainers();
            var track = new BoundaryTrack(); document.AddRootContainer(track);
            var clip = new BoundaryInterval { Name = "interval", StartTime = .25f, Length = .5f };
            track.Clips.Add(clip); document.Init();
            var calls = new List<string>(); var target = new BoundaryPreview(calls); target.SetTarget(clip);
            IDirectableTimePointer pointer = new StartTimePointer(target);
            pointer.Update(time, .2f);
            Assert.That(calls.Count, Is.EqualTo(expected ? 1 : 0));
        }

        [Test] public void EqualTimePreviewBoundariesUseReverseTrackThenStableClipOrder()
        {
            var document = new FeatureSampleSequence(); document.ClearRootContainers();
            var first = new BoundaryTrack(); var second = new BoundaryTrack();
            first.Clips.Add(new BoundaryInterval { Name = "old", StartTime = 0, Length = 1 });
            first.Clips.Add(new BoundaryPoint { Name = "point", StartTime = 1 });
            first.Clips.Add(new BoundaryInterval { Name = "next", StartTime = 1, Length = 1 });
            second.Clips.Add(new BoundaryPoint { Name = "other", StartTime = 1 });
            document.AddRootContainer(first); document.AddRootContainer(second); document.Init();
            var calls = new List<string>();
            using var preview = new SequencePreviewSession(type => typeof(ActionClip).IsAssignableFrom(type) ? new BoundaryPreview(calls) : null);
            preview.Initialize(document); preview.Sample(.5f, 0, true); calls.Clear();
            preview.Sample(1, .5f, true);
            Assert.That(calls.Where(call => !call.EndsWith(":update")), Is.EqualTo(new[] {
                "other:enter", "other:exit", "old:exit", "point:enter", "point:exit", "next:enter" }));
            calls.Clear(); preview.Sample(1, 1, true);
            Assert.That(calls, Is.EqualTo(new[] { "next:update" }));
        }

        [Test] public void FeatureSampleProjectionUsesValuesAndKeepsCapturedFramesIndependent()
        {
            var document = new FeatureSampleSequence();
            var time = .5f;
            var presenter = new ActionSequenceEditor.Presentation.Presenters.FeatureSamplePresenter(new ActionSequenceEditor.Presentation.Presenters.ExtensionSequenceProjection(() => document, () => time).Capture);
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
            using var first = new EditorFixture(Composition.SequenceComposition.Create());
            using var second = new EditorFixture(Composition.SequenceComposition.Create());
            first.Session.Open(new ActionSequenceEditor.Application.Sequences.SequenceOpenData(new FeatureSampleSequence()));
            second.Session.Open(new ActionSequenceEditor.Application.Sequences.SequenceOpenData(new FeatureSampleSequence()));
            ((FeatureSampleSequence)first.Sequence).Version = 17;
            ((FeatureSampleSequence)second.Sequence).Version = 29;
            var a = new EditorExtensionContext(new ActionSequenceEditor.Presentation.Presenters.ExtensionSequenceProjection(() => first.Sequence, () => .5f).Capture);
            var b = new EditorExtensionContext(new ActionSequenceEditor.Presentation.Presenters.ExtensionSequenceProjection(() => second.Sequence, () => 2f).Capture);
            Assert.That(Composition.FeatureSampleComposition.Create(a).Capture().Time, Is.EqualTo(.5f));
            Assert.That(Composition.FeatureSampleComposition.Create(b).Capture().Time, Is.EqualTo(2f));
            var proxy = ScriptableObject.CreateInstance<InspectorSelectionTarget>();
            var settings = new ActionSequenceEditor.Application.Settings.SettingsSession(new Preferences());
            var staleTarget = new TextAsset("Inspector target no longer has its original managed type");
            Editor staleInspector = null;
            Editor boundInspector = null;
            try
            {
                staleInspector = Editor.CreateEditor(staleTarget, typeof(Views.IMGUI.Properties.InspectorSelectionInspector));
                Assert.That(staleInspector, Is.TypeOf<Views.IMGUI.Properties.InspectorSelectionInspector>());
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Assert.That(typeof(Views.IMGUI.Properties.InspectorSelectionInspector).GetField("presenter", flags).GetValue(staleInspector), Is.Null);
                LogAssert.NoUnexpectedReceived();
                Assert.That(proxy.CreatePresenter(), Is.Null);
                boundInspector = Editor.CreateEditor(proxy);
                var presenterField = typeof(Views.IMGUI.Properties.InspectorSelectionInspector).GetField("presenter", flags);
                var enable = typeof(Views.IMGUI.Properties.InspectorSelectionInspector).GetMethod("OnEnable", flags);
                var disable = typeof(Views.IMGUI.Properties.InspectorSelectionInspector).GetMethod("OnDisable", flags);
                Assert.That(presenterField.GetValue(boundInspector), Is.Null);
                proxy.Bind(() => second.CreateProperties(settings));
                enable.Invoke(boundInspector, null);
                var boundPresenter = (ActionSequenceEditor.Presentation.Presenters.PropertyPresenter)presenterField.GetValue(boundInspector);
                Assert.That(boundPresenter.CaptureSequenceInputs().Single(f => f.Name == "Version").Value, Is.EqualTo(29));
                disable.Invoke(boundInspector, null);
                Assert.That(presenterField.GetValue(boundInspector), Is.Null);
                enable.Invoke(boundInspector, null);
                Assert.That(presenterField.GetValue(boundInspector), Is.Not.Null.And.Not.SameAs(boundPresenter));
                var presenter = proxy.CreatePresenter();
                Assert.That(presenter.CaptureSequenceInputs().Single(f => f.Name == "Version").Value, Is.EqualTo(29));
                Assert.That(proxy.CreatePresenter(), Is.Not.SameAs(presenter));
                Assert.That(presenter.Settings, Is.SameAs(settings.Current));
                Assert.That(presenter.Settings.SnapInterval, Is.EqualTo(.1f));
                presenter.Write(presenter.CaptureSequenceInputs().Single(f => f.Name == "Version").Id, 31);
                Assert.That(((FeatureSampleSequence)second.Sequence).Version, Is.EqualTo(31));
                Assert.That(((FeatureSampleSequence)first.Sequence).Version, Is.EqualTo(17));
                UnityEngine.Object.DestroyImmediate(proxy);
                Assert.DoesNotThrow(() => enable.Invoke(boundInspector, null));
                Assert.That(presenterField.GetValue(boundInspector), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (staleInspector != null) UnityEngine.Object.DestroyImmediate(staleInspector);
                if (boundInspector != null) UnityEngine.Object.DestroyImmediate(boundInspector);
                UnityEngine.Object.DestroyImmediate(staleTarget);
                UnityEngine.Object.DestroyImmediate(proxy);
            }
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
            var document = new FeatureSampleSequence();
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
            var document = new FeatureSampleSequence();
            var probes = new List<ReentrantProbe>();
            SequencePreviewSession session = null;
            session = new SequencePreviewSession(type =>
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
            SequencePreviewSession session = null;
            session = new SequencePreviewSession(_ => { session.Dispose(); return returnsNull ? null : probe; });
            Assert.Throws<OperationCanceledException>(() => session.Initialize(new FeatureSampleSequence()));
            Assert.That(session.IsInitialized, Is.False);
            Assert.That(probe.Disposals, Is.EqualTo(returnsNull ? 0 : 1));
            session.Dispose();
            Assert.That(probe.Disposals, Is.EqualTo(returnsNull ? 0 : 1));
        }
        private class Target : ActionClip { }
        [Test] public void PlaybackStoppedDuringLoopBoundaryCannotRecreatePreviewOrAdvancePreviousTime()
        {
            var asset = new FeatureSampleSequence();
            var probes = new List<ReentrantProbe>();
            SequencePlayback playback = null;
            var player = new SequenceSampler(() => asset, _ =>
            {
                var probe = new ReentrantProbe { OnEnter = () => playback.Stop() };
                probes.Add(probe); return probe;
            });
            playback = new SequencePlayback(player);
            playback.Play(0);
            playback.Update(asset.Length + 1, 1);
            Assert.That(playback.IsPlaying || player.IsInitialized, Is.False);
            Assert.That(player.CurrentTime, Is.Zero);
            Assert.That(player.PreviousTime, Is.Zero);
            Assert.That(probes.Count, Is.GreaterThan(0));
            Assert.That(probes.All(p => p.Disposals == 1 && p.Updates == 0), Is.True);
        }

        [Test] public void PlayedCallbackCanStopWithoutPlayOverwritingItsDecision()
        {
            var playback = new SequencePlayback(new SequenceSampler(() => new FeatureSampleSequence()));
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
            Assert.That(ActionSequenceEditorTypeCatalog.ResolvePreview(new[] { typeof(First), typeof(Override) }, typeof(Target)), Is.EqualTo(typeof(Override)));
            Assert.That(ActionSequenceEditorTypeCatalog.ResolvePreview(new[] { typeof(First) }, typeof(DerivedTarget)), Is.Null, "Preview bindings remain exact, unlike opted-in inspectors.");
            var error = Assert.Throws<InvalidOperationException>(() => ActionSequenceEditorTypeCatalog.ResolvePreview(new[] { typeof(First), typeof(Conflict) }, typeof(Target)));
            Assert.That(error.Message, Does.Contain(typeof(First).FullName).And.Contain(typeof(Conflict).FullName));
        }
        [Test] public void PreviewSessionReusesResourcesAndDisposesOnceOnResetAndFailure()
        {
            var asset = new FeatureSampleSequence(); var probes = new List<Probe>();
            var player = new SequenceSampler(() => asset, type =>
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
            var asset = new FeatureSampleSequence(); var probe = new Probe(); var count = 0;
            var player = new SequenceSampler(() => asset, type =>
            {
                if (++count == 2) throw new InvalidOperationException("factory failure");
                return probe;
            });
            Assert.Throws<InvalidOperationException>(() => player.Sample(1));
            Assert.That(player.IsInitialized, Is.False); Assert.That(probe.Disposals, Is.EqualTo(1));
            player.Reset(); Assert.That(probe.Disposals, Is.EqualTo(1));
        }

        [Test] public void ReinitializingFromDisposeKeepsLatestPreviewWithoutMergingGraphs()
        {
            var first = new FeatureSampleSequence();
            var outer = new FeatureSampleSequence();
            var latest = new FeatureSampleSequence();
            var probes = new List<ReentrantProbe>();
            SequencePreviewSession session = null;
            session = new SequencePreviewSession(_ => { var probe = new ReentrantProbe(); probes.Add(probe); return probe; });
            try
            {
                session.Initialize(first); var original = probes.ToArray();
                original[0].OnDispose = () => session.Initialize(latest);
                Assert.Throws<OperationCanceledException>(() => session.Initialize(outer));
                Assert.That(session.IsInitialized, Is.True);
                Assert.That(original.All(p => p.Disposals == 1), Is.True);
                Assert.That(probes.Count, Is.EqualTo(original.Length * 2), "旧初始化不能在新一代之后再创建第二套资源。");
                Assert.That(probes.Skip(original.Length).All(p => ReferenceEquals(p.directable.Root, latest)), Is.True);
                session.Sample(1, 0, false);
            }
            finally { session.Dispose(); }
            Assert.That(probes.All(p => p.Disposals == 1), Is.True);
        }
        [Test] public void PlaybackPauseLoopStepAndFaultKeepResourceOwnershipExplicit()
        {
            var asset = new FeatureSampleSequence(); var probes = new List<Probe>();
            var player = new SequenceSampler(() => asset, type => { if (type != typeof(FeatureSampleInterval)) return null; var p = new Probe(); probes.Add(p); return p; });
            var playback = new SequencePlayback(player);
            playback.Play(10); playback.Update(11, 1); Assert.That(player.CurrentTime, Is.EqualTo(1));
            playback.SetPaused(true); playback.Update(12, 1); Assert.That(player.CurrentTime, Is.EqualTo(1));
            playback.SetPaused(false); playback.Update(13, 1); Assert.That(player.CurrentTime, Is.EqualTo(2));
            playback.StepBackward(.25f); Assert.That(player.CurrentTime, Is.EqualTo(1.75f));
            playback.StepForward(.25f); Assert.That(player.CurrentTime, Is.EqualTo(2));
            playback.RangePlaybackEnabled = true; asset.RangeMin = 1; asset.RangeMax = 3;
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
            var asset = new FeatureSampleSequence(); var probes = new List<Probe>();
            var player = new SequenceSampler(() => asset, type => { var p = new Probe(); probes.Add(p); return p; });
            var playback = new SequencePlayback(player); playback.Play(0); player.Sample(1);
            playback.Stopped += () => throw new InvalidOperationException("stop callback failure");
            Assert.Throws<InvalidOperationException>(() => playback.Stop());
            Assert.That(player.IsInitialized || playback.IsPlaying || playback.IsPaused, Is.False);
            Assert.That(probes.All(p => p.Disposals == 1), Is.True);
        }
        [UnityTest] public IEnumerator BothWindowsKeepExactlyOneSharedEditorDriver()
        {

            var source = EditorFixture.Shared.Source; var recovery = EditorFixture.Shared.Session.CaptureRecovery();
            var fixture = new TextAsset(Infrastructure.Serialization.SequenceJsonCodec.Encode(new FeatureSampleSequence())); EditorFixture.Shared.Open(fixture);
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                Assert.That(EditorApplication.update.GetInvocationList().Count(callback => callback.Method.DeclaringType == typeof(ActionSequenceEditorHost) && callback.Method.Name == "UpdateSharedEditor"), Is.EqualTo(1));
                Assert.That(EditorApplication.update.GetInvocationList().Any(callback => callback.Target is ImguiWindow || callback.Target is ToolkitWindow), Is.False);
                EditorFixture.Shared.Playback.Play((float)EditorApplication.timeSinceStartup);
                EditorFixture.Shared.Playback.SetPaused(true);
                native.Close();
                Assert.That(EditorFixture.Shared.Playback.IsPlaying, Is.True, "Closing one view must not stop the shared playback.");
                Assert.That(EditorFixture.Shared.Playback.IsPaused, Is.True);
            }
            finally
            {
                original.DiscardChanges();
                if (native != null) { native.DiscardChanges(); native.Close(); }
                original.Close();
                EditorFixture.Shared.Open(source); EditorFixture.Shared.Session.RestoreRecovery(recovery); UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        [Test] public void SharedDriverSkipsDetachedViewsAndIsolatesTickFailures()
        {
            var views = new EditorViewRegistry();
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
            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            var views = new EditorViewRegistry();
            var settings = new ActionSequenceEditor.Application.Settings.SettingsSession(new Preferences());
            var changed = 0; var paints = 0;
            using var first = context.Bind(new object(), () => paints++,
                () => throw new InvalidOperationException("first window unavailable"), views, settings);
            using var second = context.Bind(new object(), () => { },
                () => changed++, views, settings);
            var before = paints;
            context.Session.Open(new ActionSequenceEditor.Application.Sequences.SequenceOpenData(new FeatureSampleSequence()));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(paints, Is.EqualTo(before + 1));
            Assert.That(context.Session.NotificationWarning, Does.Contain("first window unavailable"));
            first.Dispose();
            context.Session.Open(new ActionSequenceEditor.Application.Sequences.SequenceOpenData(new FeatureSampleSequence()));
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(context.Session.NotificationWarning, Is.Null);
        }

        [Test] public void RepaintFailureAndDetachDoNotBlockOtherRegisteredWindows()
        {
            var views = new EditorViewRegistry();
            var first = new object(); var removed = new object(); var last = new object();
            var calls = new List<string>();
            views.Attach(first, () => { calls.Add("first"); views.Detach(removed); throw new InvalidOperationException("repaint"); }, () => { });
            views.Attach(removed, () => calls.Add("removed"), () => { });
            views.Attach(last, () => calls.Add("last"), () => { });
            var error = Assert.Throws<AggregateException>(() => views.Repaint());
            Assert.That(calls, Is.EqualTo(new[] { "first", "last" }));
            Assert.That(error.Flatten().InnerExceptions.Single().Message, Is.EqualTo("repaint"));
        }

        private sealed class Preferences : ActionSequenceEditor.Application.Settings.IPreferencesStore
        {
            public ActionSequenceEditor.Application.Settings.EditorSettings Read() => new ActionSequenceEditor.Application.Settings.EditorSettings(
                ActionSequenceEditor.Application.Settings.TimeStepMode.Seconds, .1f, 30, true, true, 0, "Assets/");
            public void Write(ActionSequenceEditor.Application.Settings.EditorSettings value) => throw new AssertionException("Closing a window must not write preferences.");
        }

        [Test] public void SharedUpdateDriverStillTicksViewsWhenPlaybackAndStopCallbacksFail()
        {
            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            context.Session.Open(new ActionSequenceEditor.Application.Sequences.SequenceOpenData(new FeatureSampleSequence()));
            var views = new EditorViewRegistry(); var owner = new object(); var ticks = 0;
            views.Attach(owner, () => throw new InvalidOperationException("repaint"), () => { });
            views.SetTick(owner, () => ticks++);
            var errors = new List<Exception>();
            var driver = new Composition.EditorUpdateDriver(context.AutoSave, context.Playback, context.Clock, views,
                new ActionSequenceEditor.Application.Settings.SettingsSession(new Preferences()), errors.Add);
            context.Playback.Play(context.Clock.PlaybackTime);
            context.Playback.Stopped += () => throw new InvalidOperationException("stop");
            driver.Tick();
            Assert.That(ticks, Is.EqualTo(1));
            Assert.That(errors.SelectMany(error => error is AggregateException aggregate
                ? aggregate.Flatten().InnerExceptions.AsEnumerable() : new[] { error }).Select(error => error.Message),
                Is.EquivalentTo(new[] { "repaint", "stop" }));
            Assert.That(context.Playback.IsPlaying || context.Sampler.IsInitialized, Is.False);
            views.Detach(owner); driver.Tick(); Assert.That(ticks, Is.EqualTo(1));
        }

        [Test] public void IsolatedBindingsReleaseLastPlaybackAndSubscriptionsExactlyOnce()
        {
            using var context = new EditorFixture(Composition.SequenceComposition.Create());
            var fixture = new TextAsset(Infrastructure.Serialization.SequenceJsonCodec.Encode(new FeatureSampleSequence()));
            var views = new EditorViewRegistry();
            var settings = new ActionSequenceEditor.Application.Settings.SettingsSession(new Preferences());
            var redraws = 0; var stopped = 0;
            var first = context.Bind(new object(), () => redraws++, () => { }, views, settings);
            var second = context.Bind(new object(), () => redraws++, () => { }, views, settings);
            try
            {
                context.Open(fixture);
                first.Playback.Invoke(ActionSequenceEditor.Presentation.Contracts.PlaybackIntent.Next);
                Assert.That(context.Sampler.CurrentTime, Is.EqualTo(.1f).Within(.00001f), "Step must use the settings supplied to this binding.");
                first.Playback.Invoke(ActionSequenceEditor.Presentation.Contracts.PlaybackIntent.Previous);
                Assert.That(context.Sampler.CurrentTime, Is.Zero);
                context.Open(fixture); context.Playback.Play(0); context.Sampler.Sample(1); context.Playback.SetPaused(true);
                context.Playback.Stopped += () => stopped++;
                first.Dispose(); first.Dispose();
                Assert.That(views.Count, Is.EqualTo(1)); Assert.That(stopped, Is.Zero);
                Assert.That(context.Playback.IsPlaying && context.Playback.IsPaused, Is.True);
                second.Dispose(); second.Dispose();
                Assert.That(views.Count, Is.Zero); Assert.That(stopped, Is.EqualTo(1));
                Assert.That(context.Playback.IsPlaying || context.Playback.IsPaused || context.Sampler.IsInitialized, Is.False);
                Assert.That(context.Sampler.CurrentTime, Is.Zero);
                var count = redraws; views.Repaint(); views.NotifyChanged();
                Assert.That(redraws, Is.EqualTo(count));
                var listeners = typeof(ActionSequenceEditor.Application.Settings.SettingsSession).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(settings) as Delegate;
                Assert.That(listeners, Is.Null);
            }
            finally { first.Dispose(); second.Dispose(); UnityEngine.Object.DestroyImmediate(fixture); }
        }
    }
}
