using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NBC.ActionEditor.Tests
{
    public sealed class PreviewOwnershipTests
    {
        private class Probe : PreviewBase
        {
            public int Disposals, Updates;
            public bool ThrowUpdate;
            public override void Update(float time, float previousTime) { Updates++; if (ThrowUpdate) throw new InvalidOperationException("preview failure"); }
            public override void Dispose() { Disposals++; }
        }
        private class Target : TimelineClip { }
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
            var asset = new LegacyExperienceAsset(); var probes = new List<Probe>();
            var player = new AssetPlayer(() => asset, type =>
            {
                if (type != typeof(LegacyExperienceInterval)) return null;
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
            var asset = new LegacyExperienceAsset(); var probe = new Probe(); var count = 0;
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
            var asset = new LegacyExperienceAsset(); var probes = new List<Probe>();
            var player = new AssetPlayer(() => asset, type => { if (type != typeof(LegacyExperienceInterval)) return null; var p = new Probe(); probes.Add(p); return p; });
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
            var asset = new LegacyExperienceAsset(); var probes = new List<Probe>();
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
            var fixture = new TextAsset(Storage.JsonDocumentCodec.Encode(new LegacyExperienceAsset())); App.TextAsset = fixture;
            var original = ScriptableObject.CreateInstance<ActionEditorWindow>();
            var native = ScriptableObject.CreateInstance<Workbench.UIToolkitActionEditorWindow>();
            try
            {
                original.Show(); native.Show(); yield return null; yield return null;
                Assert.That(EditorApplication.update.GetInvocationList().Count(callback => callback.Method.DeclaringType == typeof(ActionEditorHost) && callback.Method.Name == "UpdateSharedEditor"), Is.EqualTo(1));
            }
            finally
            {
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close(); ActionEditorHost.Window = previousWindow;
                App.TextAsset = source; App.Session.RestoreRecovery(recovery); UnityEngine.Object.DestroyImmediate(fixture);
            }
        }
    }
}
