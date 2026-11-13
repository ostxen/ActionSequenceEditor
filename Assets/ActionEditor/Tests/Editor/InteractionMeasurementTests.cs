using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NBC.ActionEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;

namespace NBC.ActionEditor.Tests
{
    public sealed class InteractionMeasurementTests
    {
        [Serializable] public sealed class Measurement
        {
            public string Scenario;
            public int Repetition;
            public double WallMilliseconds;
            public long MainThreadAllocatedBytes, ManagedHeapDeltaBytes;
            public long RecordedFrameAllocationBytes;
            public long WorstRecordedFrameAllocationBytes;
            public int RecordedFrames;
            public List<double> SynchronousSliceMilliseconds = new List<double>();
            public double WorstSynchronousSliceMilliseconds;
        }
        [Serializable] public sealed class Report
        {
            public string CapturedUtc, UnityVersion;
            public int ProcessId, FrameRate;
            public float PixelsPerPoint, LeftWidth, SnapInterval;
            public bool Magnet, WheelZoom, ThreadAllocationCounterSupported;
            public bool ProfilerAllocationCounterSupported;
            public string Scope = "Current live Editor, three ordered repetitions, first invocation included. Not process cold start. Wall time includes yielded editor frames; recorded frame allocation is a whole-Editor envelope including boundary frames, test harness and unrelated work, not allocations attributable only to ActionEditor. Heap delta is not retained ownership or peak memory. No performance pass threshold.";
            public List<Measurement> Measurements = new List<Measurement>();
        }
        private static readonly Func<long> Allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>),
            typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes));

        private static bool AllocationCounterWorks()
        {
            var before = Allocated(); var probe = new byte[65536]; var after = Allocated();
            GC.KeepAlive(probe); return after - before >= probe.Length;
        }

        private static IEnumerator Measure(Report report, string scenario, int repetition, IEnumerator operation)
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1024);
            yield return null;
            var heap = GC.GetTotalMemory(false); var allocated = Allocated(); var clock = Stopwatch.StartNew();
            var slices = new List<double>();
            try
            {
                while (true)
                {
                    var slice = Stopwatch.StartNew();
                    var next = operation.MoveNext(); slice.Stop(); slices.Add(slice.Elapsed.TotalMilliseconds);
                    if (!next) break;
                    yield return operation.Current;
                }
                yield return null;
            }
            finally
            {
                (operation as IDisposable)?.Dispose(); clock.Stop();
                recorder.Stop();
                long frameBytes = 0, worstFrameBytes = 0;
                if (report.ProfilerAllocationCounterSupported)
                    for (var i = 0; i < recorder.Count; i++)
                    {
                        var bytes = recorder.GetSample(i).Value;
                        frameBytes += bytes; worstFrameBytes = Math.Max(worstFrameBytes, bytes);
                    }
                report.Measurements.Add(new Measurement { Scenario = scenario, Repetition = repetition,
                    WallMilliseconds = clock.Elapsed.TotalMilliseconds,
                    SynchronousSliceMilliseconds = slices,
                    WorstSynchronousSliceMilliseconds = slices.Count == 0 ? 0 : System.Linq.Enumerable.Max(slices),
                    MainThreadAllocatedBytes = report.ThreadAllocationCounterSupported ? Allocated() - allocated : -1,
                    ManagedHeapDeltaBytes = GC.GetTotalMemory(false) - heap,
                    RecordedFrameAllocationBytes = report.ProfilerAllocationCounterSupported && recorder.Valid ? frameBytes : -1,
                    WorstRecordedFrameAllocationBytes = report.ProfilerAllocationCounterSupported && recorder.Valid ? worstFrameBytes : -1,
                    RecordedFrames = recorder.Valid ? recorder.Count : 0 });
            }
        }
        private static IEnumerator Playback()
        {
            using (var context = new ActionEditorContext(NBC.ActionEditor.Composition.DocumentComposition.Create()))
            {
                var fixture = new TextAsset(Storage.JsonDocumentCodec.Encode(new FeatureSampleDocument()));
                try
                {
                    context.Open(fixture); context.Playback.Play(0);
                    for (var frame = 1; frame <= 600; frame++) context.Playback.Update(frame / 60f, 1);
                    Assert.That(context.Player.CurrentTime, Is.EqualTo(4).Within(.001f)); context.Playback.Stop();
                    Assert.That(context.Player.IsInitialized, Is.False);
                }
                finally { UnityEngine.Object.DestroyImmediate(fixture); }
            }
            yield break;
        }
        [UnityTest] public IEnumerator RecordCurrentSampleEntryPointsWithoutInventingPerformanceBudgets()
        {
            var report = new Report { CapturedUtc = DateTime.UtcNow.ToString("O"), UnityVersion = UnityEngine.Application.unityVersion,
                ProcessId = Process.GetCurrentProcess().Id, PixelsPerPoint = EditorGUIUtility.pixelsPerPoint,
                FrameRate = Prefs.FrameRate, LeftWidth = Styles.TimelineLeftWidth, SnapInterval = Prefs.SnapInterval,
                Magnet = Prefs.MagnetSnapping, WheelZoom = Prefs.scrollWheelZooms,
                ThreadAllocationCounterSupported = AllocationCounterWorks() };
            using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 16))
            {
                yield return null;
                var probe = new byte[65536]; yield return null; yield return null; GC.KeepAlive(probe);
                long measured = 0;
                if (calibration.Valid)
                    for (var i = 0; i < calibration.Count; i++) measured += calibration.GetSample(i).Value;
                report.ProfilerAllocationCounterSupported = measured >= probe.Length;
            }
            var cases = new Func<SharedJsonTests, IEnumerator>[] {
                test => test.BothWindowOpenOrdersShareContextAndRebuildWithoutRegistrationLeaks(),
                test => test.OriginalMultiMoveClampsOneDeltaAndKeepsSpacingAtZero(),
                test => test.NativeDragResizeCancelAndCaptureLossUseRealPointerEvents(),
                test => test.BaselineWindowNavigationRecordsCurrentPreferencesAndWheelPanResults()
            };
            var names = new[] { "both-window-open-orders-and-rebuild", "original-multidrag-cancel-undo", "native-drag-resize-capture-loss", "both-wheel-pan-layout" };
            for (var repetition = 0; repetition < 3; repetition++)
            {
                for (var i = 0; i < cases.Length; i++)
                {
                    var test = new SharedJsonTests(); test.Setup();
                    try { yield return Measure(report, names[i], repetition, cases[i](test)); }
                    finally { test.Teardown(); }
                }
                var property = new JsonPropertyTests(); property.SetUp();
                try { yield return Measure(report, "native-delayed-property-return-escape-blur", repetition, property.NativeDelayedInputKeepsIntermediateTextUntilReturnAndEscapeCancels()); }
                finally { property.TearDown(); }
                var extensions = new ExtensionEndToEndTests(); extensions.SetUp();
                try { yield return Measure(report, "both-menu-create-property-cancel-undo-readback", repetition, extensions.BothMenusAndPropertyControlsProduceEquivalentPointAndIntervalGraphs()); }
                finally { extensions.TearDown(); }
                yield return Measure(report, "shared-playback-600-updates-including-open", repetition, Playback());
            }
            Assert.That(report.Measurements.Count, Is.EqualTo(21));
            Directory.CreateDirectory(Path.Combine(UnityEngine.Application.dataPath, "../Temp"));
            File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "../Temp/rf14-current-editor-measurements.json"), JsonUtility.ToJson(report, true));
        }
    }
}
