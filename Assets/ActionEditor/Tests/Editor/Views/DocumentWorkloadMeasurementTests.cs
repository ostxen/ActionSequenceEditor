using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using EditorSettings = NBC.ActionEditor.Application.Settings.EditorSettings;

namespace NBC.ActionEditor.Tests
{
    // 固定样例和合成大图走正式会话、Presenter及完整保存编解码，不接管全局文档。
    public sealed class DocumentWorkloadMeasurementTests
    {
        [Serializable] public sealed class Sample
        {
            public string Phase;
            public int Repetition;
            public double Milliseconds;
            public long ThreadAllocatedBytes, HeapBeforeBytes, HeapAfterBytes;
            public long EditorFrameAllocatedBytes;
        }
        [Serializable] public sealed class Report
        {
            public string CapturedUtc, UnityVersion, ProcessStartUtc, Scope;
            public int ProcessId, Groups, EmptyGroups, Tracks, Clips, Rows, RenderClips, JsonCharacters;
            public bool ThreadCounterSupported, FrameCounterSupported;
            public List<Sample> Measurements = new List<Sample>();
        }
        private sealed class View : ITimelineView
        {
            public void Render(TimelineRenderState state) { }
            public void SetCapture(bool value) { }
            public void ShowError(string message) => Assert.Fail(message);
        }
        private sealed class Playhead : ITimelinePlayhead
        {
            public float Time => 0;
            public void Seek(float time) { }
        }
        private static readonly Func<long> Allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>),
            typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes));

        private static FeatureSampleSequence Create(bool large)
        {
            var document = new FeatureSampleSequence();
            if (!large) return document;
            document.ClearRootContainers();
            for (var groupIndex = 0; groupIndex < 64; groupIndex++)
            {
                var group = new FeatureSampleGroup { Name = "Group " + groupIndex };
                for (var trackIndex = 0; trackIndex < 16; trackIndex++) group.Tracks.Add(Track(trackIndex, "Track " + trackIndex));
                document.AddRootContainer(group);
                document.AddRootContainer(Track(groupIndex, "Root " + groupIndex));
            }
            // 空组额外加入，不能通过减少非空组改变已选69632片段负载。
            document.AddRootContainer(new FeatureSampleGroup { Name = "Empty" });
            document.Init(); document.RangeMin = 0; document.RangeMax = 16;
            return document;
        }
        private static ActionTrack Track(int index, string name)
        {
            ActionTrack track = index % 2 == 0 ? new FeatureSampleIntervalTrack() : new FeatureSampleMarkerTrack();
            track.Name = name;
            for (var i = 0; i < 64; i++)
                track.Clips.Add(index % 2 == 0
                    ? (ActionClip)new FeatureSampleInterval { Name = "Interval " + i, StartTime = i * .25f, Length = .125f, Caption = "Fixed interval" }
                    : new FeatureSampleMarker { Name = "Point " + i, StartTime = i * .25f, EventCode = i, Message = "Fixed point" });
            return track;
        }
        private static IEnumerator Measure(Report report, int repetition, string phase, Action operation)
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 8);
            yield return null;
            var sample = new Sample { Phase = phase, Repetition = repetition, HeapBeforeBytes = GC.GetTotalMemory(false) };
            var allocated = Allocated(); var timer = Stopwatch.StartNew();
            operation(); timer.Stop();
            sample.Milliseconds = timer.Elapsed.TotalMilliseconds;
            sample.ThreadAllocatedBytes = report.ThreadCounterSupported ? Allocated() - allocated : -1;
            sample.HeapAfterBytes = GC.GetTotalMemory(false);
            yield return null;
            recorder.Stop();
            sample.EditorFrameAllocatedBytes = report.FrameCounterSupported && recorder.Valid
                ? Enumerable.Range(0, recorder.Count).Sum(i => recorder.GetSample(i).Value) : -1;
            report.Measurements.Add(sample);
        }

        [UnityTest] public IEnumerator SampleInitializationPresenterAndSerialization() => Run(false);
        [UnityTest] public IEnumerator LargeInitializationPresenterAndSerialization() => Run(true);

        private static IEnumerator Run(bool large)
        {
            var process = Process.GetCurrentProcess();
            var report = new Report { CapturedUtc = DateTime.UtcNow.ToString("O"), ProcessId = process.Id,
                ProcessStartUtc = process.StartTime.ToUniversalTime().ToString("O"), UnityVersion = UnityEngine.Application.unityVersion,
                Scope = "Three ordered repetitions in the named Editor process. Includes document construction, real composition/session open, Presenter construction and complete save snapshot/readback. First repetition is first invocation of this fixture, not proof of process cold start. Thread counter is calibrated; unsupported=-1. Heap endpoints include Editor noise, not retained ownership or peak memory. Frame allocation is a whole-Editor envelope, not attributable allocation. No performance budget or historical speedup claim." };
            var before = Allocated(); var probe = new byte[65536];
            report.ThreadCounterSupported = Allocated() - before >= probe.Length;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 16))
            {
                yield return null; var calibration = new byte[65536]; yield return null; yield return null;
                report.FrameCounterSupported = recorder.Valid && Enumerable.Range(0, recorder.Count).Sum(i => recorder.GetSample(i).Value) >= calibration.Length;
                GC.KeepAlive(calibration);
            }
            GC.KeepAlive(probe);
            for (var repetition = 0; repetition < 3; repetition++)
            {
                FeatureSampleSequence document = null;
                EditorFixture context = null; TimelinePresenter presenter = null;
                try
                {
                    yield return Measure(report, repetition, "document-construction-and-init", () => document = Create(large));
                    Assert.That(document.RootContainers.OfType<ActionGroup>().Count(), Is.EqualTo(large ? 65 : 3));
                    Assert.That(document.AllTracks.Count(), Is.EqualTo(large ? 1088 : 7));
                    Assert.That(document.AllTracks.Sum(t => t.Clips.Count), Is.EqualTo(large ? 69632 : 13));
                    report.Groups = document.RootContainers.OfType<ActionGroup>().Count();
                    report.EmptyGroups = document.RootContainers.OfType<ActionGroup>().Count(g => g.Tracks.Count == 0);
                    report.Tracks = document.AllTracks.Count(); report.Clips = document.AllTracks.Sum(t => t.Clips.Count);
                    yield return Measure(report, repetition, "composition-and-session-open", () =>
                    {
                        context = new EditorFixture(Composition.SequenceComposition.Create());
                        context.Session.Open(new SequenceOpenData(document));
                    });
                    var workspace = new TimelineWorkspace();
                    var settings = new EditorSettings(TimeStepMode.Seconds, .1f, 30, false, true, 0, "Assets/");
                    yield return Measure(report, repetition, "presenter-construction-and-render-state", () =>
                        presenter = new TimelinePresenter(context.Session, context.Selection, context.Structure, context.Nodes, context.ClipboardEditing,
                            context.Clips, workspace, new Playhead(), () => settings, new View()));
                    Assert.That(presenter.State.Clips.Count, Is.EqualTo(report.Clips));
                    report.Rows = presenter.State.Rows.Count; report.RenderClips = presenter.State.Clips.Count;
                    yield return Measure(report, repetition, "expanded-render-state-refresh", presenter.Refresh);
                    yield return Measure(report, repetition, "complete-save-snapshot-and-readback", () =>
                    {
                        var json = SequenceJsonCodec.CreateSaveSnapshot(document);
                        var decoded = SequenceJsonCodec.Decode(json);
                        report.JsonCharacters = json.Length;
                        Assert.That(decoded.RootContainers.Count, Is.EqualTo(document.RootContainers.Count));
                        Assert.That(decoded.AllTracks.Sum(t => t.Clips.Count), Is.EqualTo(report.Clips));
                        Assert.That(SequenceJsonCodec.GetContentKey(decoded), Is.EqualTo(SequenceJsonCodec.GetContentKey(document)));
                    });
                    workspace.SetCollapsed(document.RootContainers.OfType<ActionGroup>().First(), true);
                    yield return Measure(report, repetition, "collapsed-render-state-refresh", presenter.Refresh);
                    Assert.That(presenter.State.Clips.Count, Is.EqualTo(report.Clips - (large ? 1024 : 8)));
                    Assert.That(context.Session.IsEditing, Is.False);
                }
                finally { presenter?.Dispose(); context?.Dispose(); }
                document = null;
                // 仅在各轮结束回收测试负载；不把强制GC时间混入产品操作耗时。
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                yield return null;
            }
            var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/document-workload-" + (large ? "large" : "sample") + ".json"));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Assert.That(report.Measurements.Count, Is.EqualTo(18));
        }
    }
}
