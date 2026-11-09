using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.TestTools;

namespace NBC.ActionEditor.Tests
{
    public class WorkbenchTests
    {
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
                Assert.That(window.rootVisualElement.Query<TwoPaneSplitView>().ToList().Count, Is.EqualTo(2));
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
                Assert.That(window.rootVisualElement.Query<TwoPaneSplitView>().ToList().Count, Is.EqualTo(2));
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
