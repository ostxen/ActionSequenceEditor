using NBC.ActionEditor.Model;
using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEngine;
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public class TimelineTests
    {
        [UnityTest]
        public IEnumerator RealDenseCanvasPaintsAndHitsWithoutChangingDefinition()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1200, 700);
                window.Show();
                yield return null;
                window.NewDocument();
                var track = window.Session.Document.Groups[0].Tracks[0];
                for (var i = 0; i < 1000; i++) track.Events.Add(new SignalEvent { Start = i / 200.0 });
                var before = UnityEditor.EditorJsonUtility.ToJson(window.Session.Document);
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                foreach (var scale in new[] { 40.0, 200.0, 800.0 })
                {
                    window.Session.Workspace.PixelsPerSecond = scale;
                    window.Session.Workspace.ScrollTime = 0.5;
                    canvas.MarkDirtyRepaint();
                    yield return null;
                    yield return null;
                    var hit = canvas.Hit(new Vector2(canvas.Transform.ToX(1), canvas.Transform.ToY(1) + 10));
                    Assert.That(hit, Is.Not.Null);
                    Assert.That(Mathf.Abs((float)(hit.Start - 1) * (float)scale), Is.LessThanOrEqualTo(5));
                }
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) window.Close(); }
        }

        [TestCase(10, -2)]
        [TestCase(100, 0)]
        [TestCase(1000, 3)]
        public void CoordinateRoundtripAndEventHitRectUseSameTransform(double scale, double scroll)
        {
            var transform = new TimelineTransform(scale, scroll, 16);
            foreach (var time in new[] { 0.0, 0.25, 5.0 }) Assert.That(transform.ToTime(transform.ToX(time)), Is.EqualTo(time).Within(0.00001));
            var item = new IntervalEvent { Start = 0.25, Duration = 0.00001 };
            var rect = transform.EventRect(item, 2);
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(6));
            Assert.That(rect.Contains(new Vector2(transform.ToX(0.25) + 2, transform.ToY(2) + 10)), Is.True);
            var reverse = TimelineTransform.SelectionRect(new Vector2(100, 100), new Vector2(10, 20));
            Assert.That(reverse, Is.EqualTo(new Rect(10, 20, 90, 80)));
        }

        [Test]
        public void ZeroAndEndpointSignalsRemainVisibleAndDrawingSetupDoesNotDirtyContent()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.CreateGUI(); window.NewDocument();
                var before = UnityEditor.EditorJsonUtility.ToJson(window.Session.Document);
                var canvas = new TimelineCanvas(window.Session);
                window.Session.Workspace.ScrollTime = -1;
                window.Session.Workspace.PixelsPerSecond = 400;
                canvas.MarkDirtyRepaint(); canvas.Rows();
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(before));
                var transform = canvas.Transform;
                Assert.That(transform.EventRect(new SignalEvent { Start = 0 }, 0).width, Is.EqualTo(10));
                Assert.That(transform.ToTime(transform.ToX(5)), Is.EqualTo(5));
                window.Session.DiscardAndClose();
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
