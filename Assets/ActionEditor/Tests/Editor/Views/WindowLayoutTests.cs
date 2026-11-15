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
    public sealed class WindowLayoutTests : SharedContextFixture
    {
        private sealed class PropertyEvidenceWindow : EditorWindow
        {
            public bool Imgui;
            private Editor inspector;
            private InspectorSelectionTarget preview;
            public void Initialize(ToolkitWindow owner)
            {
                if (!Imgui) rootVisualElement.Add(owner.CreatePropertyView());
                else
                {
                    preview = CreateInstance<InspectorSelectionTarget>();
                    preview.Bind(() => EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings));
                    inspector = Editor.CreateEditor(preview);
                }
            }
            private void OnGUI() { if (Imgui && inspector != null) inspector.OnInspectorGUI(); }
            private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); if (preview != null) DestroyImmediate(preview); }
        }

        [Serializable] private sealed class NavigationEvidence
        {
            public string CapturedUtc, UnityVersion;
            public float PixelsPerPoint, SnapInterval, LeftWidth;
            public int FrameRate, TimeStepMode, AutoSaveSeconds, Groups, Tracks, Clips;
            public bool MagnetSnapping, ScrollWheelZooms;
            public Vector2 ImguiMinSize, ToolkitMinSize;
            public float ImguiWheelMax, ToolkitWheelMax, ImguiPanMin, ToolkitPanMin;
            public float ToolkitScale, ToolkitHeaderHeight, ToolkitFooterHeight;
        }

        [UnityTest] public IEnumerator WindowNavigationRecordsPreferencesAndWheelPanResults()
        {

            var originalWindow = ScriptableObject.CreateInstance<ImguiWindow>();
            ToolkitWindow nativeWindow = null;
            var baseline = new NavigationEvidence
            {
                CapturedUtc = DateTime.UtcNow.ToString("O"), UnityVersion = UnityEngine.Application.unityVersion,
                PixelsPerPoint = EditorGUIUtility.pixelsPerPoint, SnapInterval = EditorFixture.SharedSettings.Current.SnapInterval,
                FrameRate = EditorFixture.SharedSettings.Current.FrameRate, TimeStepMode = (int)EditorFixture.SharedSettings.Current.TimeStepMode, AutoSaveSeconds = EditorFixture.SharedSettings.Current.AutoSaveSeconds,
                MagnetSnapping = EditorFixture.SharedSettings.Current.MagnetSnapping, ScrollWheelZooms = EditorFixture.SharedSettings.Current.ScrollWheelZooms,
                Groups = EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().Count(), Tracks = EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().Sum(g => g.Tracks.Count),
                Clips = EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().SelectMany(g => g.Tracks).Sum(t => t.Clips.Count)
            };
            try
            {
                originalWindow.Show(); originalWindow.position = new Rect(60, 60, 1000, 600);
                originalWindow.Focus(); originalWindow.Repaint(); yield return null; yield return null; yield return null;
                baseline.ImguiMinSize = originalWindow.minSize;
                baseline.LeftWidth = Styles.TimelineLeftWidth;
                originalWindow.Workspace.ViewTimeMax = 8; originalWindow.Workspace.ViewTimeMin = 1;
                var point = new Vector2(Styles.TimelineLeftTotalWidth + 100, 100);
                originalWindow.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = point, delta = new Vector2(0, 120) });
                baseline.ImguiWheelMax = originalWindow.Workspace.ViewTimeMax;
                var originalFraction = 100 / (1000 - Styles.TimelineLeftTotalWidth);
                Assert.That(baseline.ImguiWheelMax, Is.EqualTo(EditorFixture.SharedSettings.Current.ScrollWheelZooms ? 9.575f - 1.575f * originalFraction : 8).Within(.001f));
                Assert.That(originalWindow.Workspace.ViewTimeMin, Is.EqualTo(EditorFixture.SharedSettings.Current.ScrollWheelZooms ? 1 - 1.575f * originalFraction : 1).Within(.001f));
                Assert.That(originalWindow.Workspace.ViewTimeMin + originalWindow.Workspace.ViewTime * originalFraction, Is.EqualTo(1 + 7 * originalFraction).Within(.001f), "Hovered time must remain fixed.");
                originalWindow.Workspace.ViewTimeMax = 8; originalWindow.Workspace.ViewTimeMin = 1;
                originalWindow.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 2 });
                originalWindow.SendEvent(new Event { type = EventType.MouseDrag, mousePosition = point + new Vector2(20, 0), button = 2 });
                originalWindow.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point + new Vector2(20, 0), button = 2 });
                baseline.ImguiPanMin = originalWindow.Workspace.ViewTimeMin;
                Assert.That(baseline.ImguiPanMin, Is.EqualTo(1 - 20 * 7 / (1000 - Styles.TimelineLeftTotalWidth)).Within(.002f));
                Assert.That(originalWindow.Workspace.ViewTime, Is.EqualTo(7).Within(.001f));

                nativeWindow = ScriptableObject.CreateInstance<ToolkitWindow>();
                nativeWindow.Show(); nativeWindow.position = new Rect(60, 60, 1000, 600);
                nativeWindow.Focus(); yield return null; yield return null;
                baseline.ToolkitMinSize = nativeWindow.minSize;
                nativeWindow.Workspace.ViewTimeMax = 8; nativeWindow.Workspace.ViewTimeMin = 1;
                var canvas = nativeWindow.Canvas;
                point = new Vector2(canvas.Left + 100, 100);
                using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel,
                    mousePosition = canvas.LocalToWorld(point), delta = new Vector2(0, 120) }))
                { wheel.target = canvas; canvas.SendEvent(wheel); }
                baseline.ToolkitWheelMax = nativeWindow.Workspace.ViewTimeMax;
                var nativeFraction = 100 / (1000 - canvas.Left);
                Assert.That(baseline.ToolkitWheelMax, Is.EqualTo(EditorFixture.SharedSettings.Current.ScrollWheelZooms ? 9.575f - 1.575f * nativeFraction : 8).Within(.001f));
                Assert.That(nativeWindow.Workspace.ViewTimeMin, Is.EqualTo(EditorFixture.SharedSettings.Current.ScrollWheelZooms ? 1 - 1.575f * nativeFraction : 1).Within(.001f));
                nativeWindow.Workspace.ViewTimeMax = 8; nativeWindow.Workspace.ViewTimeMin = 1;
                baseline.ToolkitScale = canvas.Scale;
                Pointer(canvas, EventType.MouseDown, point, 2);
                Pointer(canvas, EventType.MouseDrag, point + new Vector2(20, 0), 2);
                Pointer(canvas, EventType.MouseUp, point + new Vector2(20, 0), 2);
                baseline.ToolkitPanMin = nativeWindow.Workspace.ViewTimeMin;
                Assert.That(baseline.ToolkitPanMin, Is.EqualTo(1 - 20 / baseline.ToolkitScale).Within(.001f));
                Assert.That(nativeWindow.Workspace.ViewTime, Is.EqualTo(7).Within(.001f));
                baseline.ToolkitHeaderHeight = nativeWindow.rootVisualElement.Q("timeline-header").resolvedStyle.height;
                baseline.ToolkitFooterHeight = nativeWindow.rootVisualElement.Q("timeline-footer").resolvedStyle.height;
                foreach (var size in new[] { new Vector2(500, 250), new Vector2(1000, 600) })
                {
                    nativeWindow.position = new Rect(new Vector2(60, 60), size); yield return null; yield return null;
                    Assert.That(canvas.Scale, Is.EqualTo((size.x - canvas.Left) / nativeWindow.Workspace.ViewTime).Within(.01f));
                    Assert.That(canvas.contentRect.height, Is.EqualTo(size.y - 56).Within(1));
                    Assert.That(nativeWindow.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                }
                Assert.That(EditorFixture.Shared.Session.IsDirty || EditorFixture.Shared.Session.IsEditing, Is.False);
                File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/window-navigation-evidence.json"), JsonUtility.ToJson(baseline, true));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (nativeWindow != null) { nativeWindow.DiscardChanges(); nativeWindow.Close(); }
                if (originalWindow != null) { originalWindow.DiscardChanges(); originalWindow.Close(); }
            }
        }

        [UnityTest] public IEnumerator SharedLayoutEvidenceCapturesBothOwnedWindows()
        {
            var original = ScriptableObject.CreateInstance<ImguiWindow>();
            var native = ScriptableObject.CreateInstance<ToolkitWindow>();

            var previousLeft = Styles.TimelineLeftWidth;
            try
            {
                original.Workspace.LeftWidth = 320; original.Workspace.ScrollY = 0;
                original.Workspace.SetRange(0, 7); native.Workspace.SetRange(0, 7); EditorFixture.Shared.Playback.Stop(); EditorFixture.Shared.Playback.IsRange = false; EditorFixture.Shared.Selection.Set(new IDirectable[] {  }, false);
                original.Show(); original.position = new Rect(60, 60, 1000, 600);
                yield return null; yield return null; yield return null; original.Repaint(); yield return null;
                Capture(original, "shared-json-original.png");
                native.Show(); native.position = new Rect(60, 60, 1000, 600);
                native.LeftWidth = 320; native.ScrollY = 0; native.Refresh();
                yield return null; yield return null; yield return null; native.Repaint(); yield return null;
                Capture(native, "shared-json-native.png");
                Assert.That(native.rootVisualElement.Q("timeline-transport").resolvedStyle.width, Is.EqualTo(320).Within(1));
                Assert.That(native.rootVisualElement.Q("timeline-document-toolbar").worldBound.xMin - native.rootVisualElement.worldBound.xMin, Is.EqualTo(325).Within(1));
                Assert.That(native.Canvas.Scale, Is.EqualTo((1000 - 329) / 7f).Within(1));
                Assert.That(native.rootVisualElement.Q("timeline-loop-start"), Is.Null);
                Assert.That(native.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(native.rootVisualElement.Q("timeline-custom-header").Q<Button>().text, Is.EqualTo(Lan.PreviewTitle));
                foreach (var width in new[] { 850, 1024, 1400 })
                {
                    native.position = new Rect(60, 60, width, 600); yield return null; yield return null;
                    Assert.That(native.Canvas.contentRect.width, Is.GreaterThan(width - 25));
                    Assert.That(native.rootVisualElement.Q("timeline-ruler").worldBound.xMin - native.rootVisualElement.worldBound.xMin, Is.EqualTo(native.Canvas.Left).Within(1));
                }
                original.position = native.position = new Rect(60, 60, 1000, 600);
                var selected = EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]; EditorFixture.Shared.Selection.Set(new IDirectable[] { selected }, false);
                EditorFixture.Shared.Playback.Play(EditorFixture.Shared.Clock.PlaybackTime); EditorFixture.Shared.Playback.Pause(true); EditorFixture.Shared.Player.Sample(1); EditorFixture.Shared.Playback.IsRange = true;
                original.Focus(); original.Repaint(); yield return null; yield return null;
                Capture(original, "shared-json-original-selected.png");
                native.Refresh(); native.Focus(); native.Repaint(); yield return null; yield return null;
                Capture(native, "shared-json-native-selected.png");
                EditorFixture.Shared.Playback.Stop(); EditorFixture.Shared.Playback.IsRange = false;
                original.Workspace.SetCollapsed(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(2), false); native.Workspace.SetCollapsed(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(2), false); EditorFixture.SharedViews.Repaint();
                original.position = native.position = new Rect(60, 60, 1000, 240);
                original.Workspace.ScrollY = 60; native.ScrollY = 60;
                original.Focus(); original.Repaint(); yield return null; yield return null;
                original.Workspace.ScrollY = 60; original.Repaint(); yield return null; yield return null;
                Capture(original, "shared-json-original-scrolled.png");
                native.Refresh(); native.Focus(); native.Repaint(); yield return null; yield return null;
                native.ScrollY = 60; native.SyncView(); native.Repaint(); yield return null; yield return null;
                Capture(native, "shared-json-native-scrolled.png");
                Assert.That(native.ScrollY, Is.EqualTo(60).Within(.01f));
                Assert.That(native.rootVisualElement.Q<Scroller>().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(native.Canvas.Scale, Is.EqualTo((1000 - 329) / native.Workspace.ViewTime).Within(1));
            }
            finally { original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close(); Styles.TimelineLeftWidth = previousLeft; }
        }

        [UnityTest] public IEnumerator ImguiInspectorWelcomeAndSettingsHaveToolkitCounterparts()
        {
            var original = ScriptableObject.CreateInstance<ImguiWindow>(); var native = ScriptableObject.CreateInstance<ToolkitWindow>();
            var originalProperty = ScriptableObject.CreateInstance<PropertyEvidenceWindow>(); originalProperty.Imgui = true;
            var nativeProperty = ScriptableObject.CreateInstance<PropertyEvidenceWindow>(); var preferences = ScriptableObject.CreateInstance<ToolkitPreferences>();

            try
            {
                native.Show(); native.position = new Rect(60, 60, 1000, 600); yield return null; yield return null;
                native.Select(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]);
                originalProperty.Show(); originalProperty.position = new Rect(60, 60, 640, 850); originalProperty.Initialize(native);
                originalProperty.Repaint(); yield return null; yield return null;
                Capture(originalProperty, "shared-json-original-inspector.png");
                nativeProperty.Show(); nativeProperty.position = new Rect(60, 60, 640, 850); nativeProperty.Initialize(native);
                yield return null; yield return null; Capture(nativeProperty, "shared-json-native-inspector.png");
                Assert.That(nativeProperty.rootVisualElement.Q("timeline-clip-timing").resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
                Assert.That(nativeProperty.rootVisualElement.Q("timeline-clip-blending").resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
                var frames = EditorFixture.SharedSettings.Current.TimeStepMode == NBC.ActionEditor.Application.Settings.TimeStepMode.Frames;
                if (frames) nativeProperty.rootVisualElement.Q<IntegerField>("timeline-clip-length").value = Mathf.RoundToInt(1.2f * EditorFixture.SharedSettings.Current.FrameRate);
                else nativeProperty.rootVisualElement.Q<FloatField>("timeline-clip-length").value = 1.2f;
                yield return null;
                Assert.That(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].Length, Is.EqualTo(1.2f).Within(EditorFixture.SharedSettings.Current.SnapInterval));
                Undo.PerformUndo(); yield return null;
                Assert.That(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0].Length, Is.EqualTo(1.5f));
                preferences.Bind(EditorFixture.SharedSettings); preferences.Show(); preferences.position = new Rect(60, 60, 400, 310); yield return null; yield return null;
                Assert.That(preferences.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(preferences.rootVisualElement.Query<Toggle>().ToList().Count, Is.EqualTo(2));
                Capture(preferences, "shared-json-native-settings.png"); preferences.Close();
                EditorFixture.Shared.Open(null); original.Show(); original.position = new Rect(60, 60, 1000, 600);
                original.Repaint(); yield return null; yield return null; Capture(original, "shared-json-original-welcome.png");
                native.Refresh(); native.Focus(); yield return null; yield return null;
                Assert.That(native.rootVisualElement.Q("timeline-header").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                var welcome = native.rootVisualElement.Q("timeline-welcome");
                Assert.That(welcome.resolvedStyle.alignItems, Is.EqualTo(Align.Center));
                Assert.That(welcome.Query<Button>().ToList().Count, Is.EqualTo(3));
                Capture(native, "shared-json-native-welcome.png");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                originalProperty.Close(); nativeProperty.Close(); if (preferences != null) preferences.Close();
                original.DiscardChanges(); native.DiscardChanges(); original.Close(); native.Close();
            }
        }
    }
}
