using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.TestTools;
using System.Linq;
using NBC.ActionEditor.Model;
using UnityEditor;

namespace NBC.ActionEditor.Tests
{
    public class WorkbenchTests
    {
        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using var evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        [UnityTest]
        public IEnumerator TrackHeadersTargetNewEventsAndViewControlsDoNotEditDefinition()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(80, 80, 1200, 700); window.Show(); yield return null; yield return null;
                var propertyView = window.CreatePropertyView();
                propertyView.style.position = Position.Absolute; propertyView.style.right = 0; propertyView.style.width = 280;
                window.rootVisualElement.Add(propertyView);
                Assert.That(window.rootVisualElement.Q("document-controls").enabledInHierarchy, Is.False);
                Assert.That(window.rootVisualElement.Q("timeline-empty").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Submit(window.rootVisualElement.Q<Button>("new-document"));
                Submit(window.rootVisualElement.Q<Button>("add-track"));
                yield return null; yield return null;
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                var secondId = window.Session.Document.Groups[0].Tracks[1].Id;
                Assert.That(window.Session.Workspace.SelectedTrackId, Is.EqualTo(secondId));
                window.AddEvent("actioneditor.interval");
                Assert.That(window.Session.Document.Groups[0].Tracks[0].Events, Is.Empty);
                Assert.That(window.Session.Document.Groups[0].Tracks[1].Events.Single(), Is.TypeOf<IntervalEvent>());
                yield return null; yield return null;
                var vector = window.Properties.Q<Vector3Field>("event-Offset");
                foreach (var input in vector.Query<FloatField>().ToList())
                    Assert.That(input.Q(className: "unity-base-field__input").resolvedStyle.width, Is.GreaterThan(15), "Vector component must have usable input width.");
                // Real pointer callback on the first header selects that track and shows its inspector.
                using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0,
                    mousePosition = canvas.LocalToWorld(new Vector2(30, canvas.Transform.ToY(1) + 10)) }))
                { down.target = canvas; canvas.SendEvent(down); }
                Assert.That(window.Session.Workspace.SelectedTrackId, Is.EqualTo(window.Session.Document.Groups[0].Tracks[0].Id));
                window.Properties.Q<TextField>("track-name").value = "Selected track";
                window.AddEvent("actioneditor.signal");
                Assert.That(window.Session.Document.Groups[0].Tracks[0].Events.Single(), Is.TypeOf<SignalEvent>());
                var before = EditorJsonUtility.ToJson(window.Session.Document);
                Submit(window.rootVisualElement.Q<Button>("fit-timeline"));
                window.rootVisualElement.Q<DoubleField>("playhead-time").value = 2;
                Assert.That(window.Session.Workspace.Playhead, Is.EqualTo(2));
                var range = window.rootVisualElement.Q<MinMaxSlider>("time-range");
                range.value = new Vector2(1, 3);
                Assert.That(canvas.Transform.ToTime(TimelineTransform.HeaderWidth), Is.EqualTo(1).Within(0.001));
                Assert.That(canvas.Transform.ToTime(canvas.contentRect.width), Is.EqualTo(3).Within(0.001));
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                // Long lists reveal the newly created track rather than silently adding it offscreen.
                for (var i = 0; i < 20; i++) Submit(window.rootVisualElement.Q<Button>("add-track"));
                yield return null; yield return null;
                Assert.That(window.Session.Workspace.ScrollY, Is.GreaterThan(0));
                Assert.That(canvas.Transform.ToY(canvas.Rows().Count - 1) + TimelineTransform.RowHeight, Is.LessThanOrEqualTo(canvas.contentRect.height + 1));
                Assert.That(window.rootVisualElement.Q<Label>("selected-track").text, Is.EqualTo("Track 22"));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) { window.Session?.DiscardAndClose(); window.Close(); } }
        }

        [UnityTest]
        public IEnumerator LayoutRestoresFullWidthTimelineAndNativeUnityInspector()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            Editor editor = null;
            try
            {
                window.Show(); yield return null; window.NewDocument();
                window.ShowProperties();
                Assert.That(Selection.activeObject, Is.TypeOf<WorkbenchInspectorTarget>());
                Assert.That(((WorkbenchInspectorTarget)Selection.activeObject).Owner, Is.SameAs(window));
                editor = Editor.CreateEditor(Selection.activeObject);
                var propertyView = editor.CreateInspectorGUI();
                // Exercise the actual CustomEditor view on the same real panel, without a permanent workbench pane.
                propertyView.style.position = Position.Absolute; propertyView.style.right = 0; propertyView.style.width = 280;
                window.rootVisualElement.Add(propertyView);
                propertyView.Q<TextField>("document-title").value = "Inspector edit";
                Assert.That(window.Session.Document.Title, Is.EqualTo("Inspector edit"));
                propertyView.RemoveFromHierarchy();
                foreach (var width in new[] { 850, 1024, 1400 })
                {
                    window.position = new Rect(60, 60, width, 600); yield return null; yield return null;
                    var root = window.rootVisualElement; var canvas = root.Q<TimelineCanvas>();
                    Assert.That(root.Q("property-pane"), Is.Null);
                    Assert.That(canvas.contentRect.width, Is.GreaterThan(width - 25));
                    Assert.That(root.Q("timeline-header").resolvedStyle.height, Is.EqualTo(20).Within(1));
                    Assert.That(root.Q("timeline-range-bar").resolvedStyle.height, Is.EqualTo(16).Within(1));
                    Assert.That(canvas.LeftWidth, Is.EqualTo(249));
                    Assert.That(root.Q<MinMaxSlider>().worldBound.xMin - canvas.worldBound.xMin, Is.EqualTo(canvas.LeftWidth).Within(4));
                }
                var before = EditorJsonUtility.ToJson(window.Session.Document);
                var c = window.rootVisualElement.Q<TimelineCanvas>();
                Assert.That(c.Rows()[0].Track, Is.Null);
                var groupId = c.Rows()[0].Group.Id;
                window.Interaction.Begin(new Vector2(10, c.Transform.ToY(0) + 10), 0, false);
                Assert.That(c.Rows().Count, Is.EqualTo(1));
                Assert.That(window.Session.Workspace.SelectedTrackId, Is.Null);
                window.Interaction.Begin(new Vector2(10, c.Transform.ToY(0) + 10), 0, false);
                Assert.That(c.Rows().Count, Is.EqualTo(2));
                var splitter = new Vector2(c.LeftWidth - 7, 70);
                window.Interaction.Begin(splitter, 0, false); window.Interaction.End(splitter + new Vector2(50, 0));
                Assert.That(c.LeftWidth, Is.EqualTo(299));
                Assert.That(c.Transform.ToTime(c.LeftWidth), Is.EqualTo(window.Session.Workspace.ScrollTime));
                window.Interaction.Begin(new Vector2(c.LeftWidth - 7, 70), 0, false);
                window.Interaction.Move(new Vector2(c.LeftWidth + 30, 70)); window.Interaction.Cancel();
                Assert.That(c.LeftWidth, Is.EqualTo(299));
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                Submit(window.rootVisualElement.Q<Button>("add-group")); Submit(window.rootVisualElement.Q<Button>("add-track"));
                Assert.That(window.Session.Document.Groups.Count, Is.EqualTo(2));
                Assert.That(window.Session.Document.Groups[1].Tracks.Count, Is.EqualTo(1));
                Assert.That(window.Session.Document.Groups[0].Id, Is.EqualTo(groupId));
                window.Session.DiscardAndClose();
            }
            finally { if (editor != null) Object.DestroyImmediate(editor); if (window != null) { window.Session?.DiscardAndClose(); window.Close(); } }
        }
        [UnityTest]
        public IEnumerator LayoutEvidenceCapturesOwnedWindowOnly()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(60, 60, 1200, 700); window.Show(); yield return null; yield return null;
                window.NewDocument();
                for (var i = 0; i < 4; i++) Submit(window.rootVisualElement.Q<Button>("add-track"));
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                canvas.SelectTrack(canvas.Rows()[2].Track.Id);
                window.Session.Workspace.Playhead = 1.25;
                window.AddEvent("actioneditor.interval");
                Submit(window.rootVisualElement.Q<Button>("fit-timeline"));
                window.Focus(); window.Repaint(); yield return null; yield return null;
                var scale = EditorGUIUtility.pixelsPerPoint;
                var host = typeof(EditorWindow).GetField("m_Parent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
                var raw = RenderTexture.GetTemporary(Mathf.RoundToInt(window.position.width * scale), Mathf.RoundToInt(window.position.height * scale), 0);
                var upright = RenderTexture.GetTemporary(raw.descriptor);
                var previous = RenderTexture.active;
                var snapshot = new Texture2D(raw.width, raw.height, TextureFormat.RGB24, false);
                try
                {
                    host.GetType().GetMethod("GrabPixels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(host, new object[] { raw, new Rect(0, 0, raw.width, raw.height) });
                    var blit = EditorGUIUtility.Load("SceneView/BlitSceneViewCapture.mat") as Material;
                    Assert.That(blit, Is.Not.Null);
                    Graphics.Blit(raw, upright, blit);
                    RenderTexture.active = upright;
                    snapshot.ReadPixels(new Rect(0, 0, upright.width, upright.height), 0, 0); snapshot.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Temp/workbench-layout.png"), snapshot.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(snapshot); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(upright); RenderTexture.ReleaseTemporary(raw); }
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) { window.Session?.DiscardAndClose(); window.Close(); } }
        }

        [UnityTest]
        public IEnumerator ToolbarTrackCreationUpdatesRowsInLaidOutCanvas()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(100, 100, 1200, 700); window.Show();
                yield return null; yield return null;
                void Click(Button button)
                {
                    Assert.That(button, Is.Not.Null);
                    using var submit = NavigationSubmitEvent.GetPooled(); submit.target = button; button.SendEvent(submit);
                }
                Click(window.rootVisualElement.Q<Button>("new-document"));
                yield return null; yield return null;
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                Assert.That(canvas.contentRect.height, Is.GreaterThan(100));
                Assert.That(canvas.contentRect.width, Is.GreaterThan(200));
                var trackButton = window.rootVisualElement.Query<Button>().ToList().Find(b => b.text == "+ Track");
                Click(trackButton);
                yield return null; yield return null;
                Assert.That(canvas.Rows().Count, Is.EqualTo(3));
                Assert.That(canvas.Rows()[1].Track.Name, Is.EqualTo("Track 1"));
                Assert.That(canvas.Rows()[2].Track.Name, Is.EqualTo("Track 2"));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) { window.Session?.DiscardAndClose(); window.Close(); } }
        }

        [UnityTest]
        public IEnumerator RealWindowLayoutResizesAndCanBeClosedAndReopened()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(100, 100, 1000, 600);
                window.Show();
                yield return null;
                yield return null;
                var host = window.rootVisualElement.Q<VisualElement>("timeline-host");
                Assert.That(host.resolvedStyle.width, Is.GreaterThan(100));
                Assert.That(host.resolvedStyle.height, Is.GreaterThan(100));
                window.position = new Rect(100, 100, 1200, 700);
                yield return null;
                yield return null;
                Assert.That(host.resolvedStyle.width, Is.GreaterThan(300));
                window.Close();
                window = ScriptableObject.CreateInstance<ActionWorkbench>();
                window.Show();
                yield return null;
                Assert.That(window.Session.Document, Is.Null);
                Assert.That(window.rootVisualElement.Query<TwoPaneSplitView>().ToList().Count, Is.EqualTo(1));
            }
            finally { if (window != null) window.Close(); }
        }

        [Test]
        public void RebuildingNativeTreeDoesNotAccumulateControlsOrCallbacks()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.CreateGUI(); window.CreateGUI(); window.CreateGUI();
                Assert.That(window.rootVisualElement.Query<IMGUIContainer>().ToList(), Is.Empty);
                Assert.That(window.rootVisualElement.Query<TwoPaneSplitView>().ToList().Count, Is.EqualTo(1));
                window.NewDocument();
                Assert.That(window.Session.Document.Groups[0].Tracks.Count, Is.EqualTo(1));
                Assert.That(window.Session.IsDirty, Is.True);
                window.Session.DiscardAndClose();
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void IndependentWindowsDoNotShareDocumentOrSelection()
        {
            var a = ScriptableObject.CreateInstance<ActionWorkbench>();
            var b = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                a.CreateGUI(); b.CreateGUI(); a.NewDocument(); b.NewDocument();
                a.Session.Document.Title = "A";
                a.Session.Selection.Add("A");
                Assert.That(b.Session.Document.Title, Is.EqualTo("Untitled"));
                Assert.That(b.Session.Selection, Is.Empty);
                a.Session.DiscardAndClose(); b.Session.DiscardAndClose();
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}
