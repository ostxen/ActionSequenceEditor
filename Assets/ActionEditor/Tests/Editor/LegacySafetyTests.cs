using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;
using System.Collections;
using NBC.ActionEditor.Workbench;

namespace NBC.ActionEditor.Tests
{
    public class LegacySafetyTests
    {
        private TextAsset previousText;
        private string previousRecovery;
        [SetUp] public void SetUp()
        {
            previousText = App.TextAsset;
            previousRecovery = App.Session.CaptureRecovery();
            App.TextAsset = null;
        }
        [TearDown] public void TearDown()
        {
            App.Session.CancelEdit();
            App.TextAsset = previousText;
            App.Session.RestoreRecovery(previousRecovery);
        }
        [UnityTest]
        public IEnumerator SelectingLegacyJsonDrawsOriginalTimelineWithoutOpeningWorkbench()
        {
            var previous = App.TextAsset;
            var previousWindow = App.Window;
            var workbenches = Resources.FindObjectsOfTypeAll<ActionWorkbench>();
            var sample = new LegacyBaselineTests.BaselineAsset();
            var group = new Group { Name = "Reference" };
            group.Tracks.Add(new LegacyBaselineTests.BaselineTrack { Name = "Events" });
            sample.groups.Add(group); sample.Init();
            var json = new TextAsset(Json.Serialize(sample));
            var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600);
                // Same callback used by the welcome page's legacy resource picker.
                App.OnObjectPickerConfig(json);
                window.Repaint(); yield return null; yield return null;
                Assert.That(App.TextAsset, Is.SameAs(json));
                Assert.That(App.AssetData.groups[0].Tracks[0].Name, Is.EqualTo("Events"));
                var timeline = (TimelineView)typeof(ActionEditorWindow).GetField("_timelineView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                Assert.That(timeline.Position.width, Is.EqualTo(window.position.width).Within(1), "Original OnGUI must draw using the actual window geometry.");
                Assert.That(timeline.Position.height, Is.GreaterThan(200));
                var middle = (TimelineMiddleView)typeof(TimelineView).GetField("_middleView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(timeline);
                Assert.That(middle.Position.height, Is.GreaterThan(200));
                Assert.That(TimelineMiddleView.GetLastItem(), Is.Not.Null);
                Assert.That(Resources.FindObjectsOfTypeAll<ActionWorkbench>(), Is.EquivalentTo(workbenches));
                Assert.Throws<InvalidOperationException>(() => App.SaveAsset(), "In-memory fixture has no persistent save path.");
            }
            finally
            {
                window.Close(); App.Window = previousWindow; App.TextAsset = previous;
                UnityEngine.Object.DestroyImmediate(json);
            }
        }

        [Test]
        public void LegacyCreationRejectsInvalidTypeWithoutOpeningWorkbenchOrChangingLegacyData()
        {
            var existing = Resources.FindObjectsOfTypeAll<ActionWorkbench>();
            var document = App.AssetData;
            var target = App.TextAsset;
            try
            {
                Assert.Throws<ArgumentException>(() => CreateAssetWindow.CreateLegacyAsset(typeof(string), "Assets/InvalidLegacyCreation.json"));
                Assert.That(Resources.FindObjectsOfTypeAll<ActionWorkbench>(), Is.EquivalentTo(existing));
                Assert.That(App.AssetData, Is.SameAs(document));
                Assert.That(App.TextAsset, Is.SameAs(target));
                foreach (var window in Resources.FindObjectsOfTypeAll<ActionWorkbench>().Except(existing))
                    Assert.That(window.Session.Document, Is.Null, "Opening the workbench must not create or save a document.");
            }
            finally
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<ActionWorkbench>().Except(existing)) window.Close();
            }
        }

        [Test]
        public void LegacyAndWorkbenchMenusOpenSeparateWindowTypesWithoutChangingDocuments()
        {
            var oldWindows = Resources.FindObjectsOfTypeAll<ActionEditorWindow>();
            var workbenches = Resources.FindObjectsOfTypeAll<JsonActionWorkbench>();
            var document = App.AssetData;
            var target = App.TextAsset;
            try
            {
                Assert.DoesNotThrow(ActionEditorWindow.OpenDirectorWindow);
                Assert.That(Resources.FindObjectsOfTypeAll<ActionEditorWindow>(), Is.Not.Empty);
                Assert.That(Resources.FindObjectsOfTypeAll<JsonActionWorkbench>(), Is.EquivalentTo(workbenches));
                var legacy = Resources.FindObjectsOfTypeAll<ActionEditorWindow>().First();
                Assert.That(typeof(ActionEditorWindow).GetField("_timelineView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(legacy), Is.TypeOf<TimelineView>());
                Assert.That(typeof(ActionEditorWindow).GetField("_welcomeView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(legacy), Is.TypeOf<WelcomeView>());
                Assert.DoesNotThrow(JsonActionWorkbench.Open);
                Assert.That(Resources.FindObjectsOfTypeAll<JsonActionWorkbench>(), Is.Not.Empty);
                Assert.That(App.AssetData, Is.SameAs(document));
                Assert.That(App.TextAsset, Is.SameAs(target));
                foreach (var window in Resources.FindObjectsOfTypeAll<JsonActionWorkbench>()) Assert.That(window.Document, Is.SameAs(document));
                Assert.Throws<InvalidOperationException>(() => App.SaveAsset());
            }
            finally
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<ActionEditorWindow>().Except(oldWindows)) window.Close();
                foreach (var window in Resources.FindObjectsOfTypeAll<JsonActionWorkbench>().Except(workbenches)) window.Close();
            }
        }

        [Test]
        public void JsonSaveWithoutDocumentIsRejected()
        {
            using var session = new NBC.ActionEditor.Storage.JsonDocumentSession();
            Assert.Throws<InvalidOperationException>(() => session.Save());
        }

        [Test]
        public void LegacyAutoSaveNeverUpdatesTimestampOrWrites()
        {
            var before = App.LastSaveTime;
            // No elapsed interval or dirty document: scheduled autosave must be a no-op.
            App.TryAutoSave();
            Assert.That(App.LastSaveTime, Is.EqualTo(before));
        }

        [TestCase(60, 59, false)]
        [TestCase(60, 60, true)]
        [TestCase(120, 119, false)]
        [TestCase(120, 120, true)]
        [TestCase(120, 360, true)]
        [TestCase(0, 360, false)]
        public void AutoSaveUsesTotalMonotonicDuration(double interval, double elapsed, bool expected)
        {
            Assert.That(NBC.ActionEditor.Model.AutoSaveSchedule.IsDue(10, 10 + elapsed, interval), Is.EqualTo(expected));
        }

        [Test]
        public void FailedLegacyOpenDoesNotChangeDocumentOrTarget()
        {
            var previous = App.TextAsset;
            var valid = new TextAsset(Json.Serialize(new LegacyBaselineTests.BaselineAsset()));
            var invalid = new TextAsset("not json");
            try
            {
                App.TextAsset = valid;
                var document = App.AssetData;
                LogAssert.Expect(LogType.Error, new Regex(".*", RegexOptions.Singleline));
                LogAssert.Expect(LogType.Error, new Regex("ActionEditor 打开失败，原文档已保留：.*", RegexOptions.Singleline));
                App.TextAsset = invalid;
                Assert.That(App.TextAsset, Is.SameAs(valid));
                Assert.That(App.AssetData, Is.SameAs(document));
            }
            finally
            {
                App.TextAsset = previous;
                UnityEngine.Object.DestroyImmediate(valid);
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }
    }
}
