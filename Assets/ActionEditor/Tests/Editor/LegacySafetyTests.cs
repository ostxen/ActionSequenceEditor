using NBC.ActionEditor.Domain;
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
        private Func<string> previousSaveRequest;
        [SetUp] public void SetUp()
        {
            previousText = App.TextAsset;
            previousRecovery = App.Session.CaptureRecovery();
            previousSaveRequest = App.SavePathRequested;
            App.SavePathRequested = () => null;
            App.TextAsset = null;
        }
        [TearDown] public void TearDown()
        {
            App.Session.CancelEdit();
            App.TextAsset = previousText;
            App.Session.RestoreRecovery(previousRecovery);
            App.SavePathRequested = previousSaveRequest;
        }
        [UnityTest]
        public IEnumerator SelectingLegacyJsonDrawsOriginalTimelineWithoutOpeningWorkbench()
        {
            var previous = App.TextAsset;
            var previousWindow = ActionEditorHost.Window;
            var workbenches = Resources.FindObjectsOfTypeAll<ActionWorkbench>();
            var sample = new NBC.ActionEditor.Samples.LegacyExperience.LegacyExperienceAsset();
            var group = new NBC.ActionEditor.Samples.LegacyExperience.LegacyExperienceGroup { Name = "Reference" };
            group.Tracks.Add(new NBC.ActionEditor.Samples.LegacyExperience.LegacyIntervalTrack { Name = "Events" });
            sample.ClearRoots(); sample.AddRoot(group); sample.Init();
            var json = new TextAsset(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(sample));
            var window = ScriptableObject.CreateInstance<ActionEditorWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600);
                // Same callback used by the welcome page's legacy resource picker.
                ActionEditorHost.OnObjectPickerConfig(json);
                window.Repaint(); yield return null; yield return null;
                Assert.That(App.TextAsset, Is.SameAs(json));
                Assert.That(App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo("Events"));
                var timeline = window.Timeline.State;
                Assert.That(timeline.Size.Width, Is.EqualTo(window.position.width).Within(1), "IMGUI must use the actual window geometry.");
                Assert.That(timeline.Size.Height, Is.GreaterThan(200));
                Assert.That(timeline.Rows.Count, Is.EqualTo(2));
                Assert.That(timeline.Rows[1].Title, Is.EqualTo("Events"));
                Assert.That(Resources.FindObjectsOfTypeAll<ActionWorkbench>(), Is.EquivalentTo(workbenches));
                Assert.DoesNotThrow(() => App.SaveAsset(), "Canceling the save path leaves the in-memory fixture unchanged.");
                Assert.That(App.TextAsset, Is.SameAs(json));
            }
            finally
            {
                window.Close(); ActionEditorHost.Window = previousWindow; App.TextAsset = previous;
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
                Assert.Throws<ArgumentException>(() => DocumentCreationService.Create(typeof(string), "Assets/InvalidLegacyCreation.json"));
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
            var workbenches = Resources.FindObjectsOfTypeAll<UIToolkitActionEditorWindow>();
            var document = App.AssetData;
            var target = App.TextAsset;
            try
            {
                Assert.DoesNotThrow(ActionEditorWindow.OpenDirectorWindow);
                Assert.That(Resources.FindObjectsOfTypeAll<ActionEditorWindow>(), Is.Not.Empty);
                Assert.That(Resources.FindObjectsOfTypeAll<UIToolkitActionEditorWindow>(), Is.EquivalentTo(workbenches));
                var legacy = Resources.FindObjectsOfTypeAll<ActionEditorWindow>().First();
                Assert.That(legacy.Timeline, Is.TypeOf<NBC.ActionEditor.Views.IMGUI.TimelineSurface>());
                Assert.That(typeof(ActionEditorWindow).GetField("_welcomeView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(legacy), Is.TypeOf<WelcomeView>());
                Assert.DoesNotThrow(UIToolkitActionEditorWindow.Open);
                Assert.That(Resources.FindObjectsOfTypeAll<UIToolkitActionEditorWindow>(), Is.Not.Empty);
                Assert.That(App.AssetData, Is.SameAs(document));
                Assert.That(App.TextAsset, Is.SameAs(target));
                foreach (var window in Resources.FindObjectsOfTypeAll<UIToolkitActionEditorWindow>()) Assert.That(window.Document, Is.SameAs(document));
                Assert.DoesNotThrow(() => App.SaveAsset());
                Assert.That(App.AssetData, Is.SameAs(document));
            }
            finally
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<ActionEditorWindow>().Except(oldWindows)) window.Close();
                foreach (var window in Resources.FindObjectsOfTypeAll<UIToolkitActionEditorWindow>().Except(workbenches)) window.Close();
            }
        }

        [Test]
        public void JsonSaveWithoutDocumentIsRejected()
        {
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create();
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
            var valid = new TextAsset(NBC.ActionEditor.Storage.JsonDocumentCodec.Encode(new NBC.ActionEditor.Samples.LegacyExperience.LegacyExperienceAsset()));
            var invalid = new TextAsset("not json");
            try
            {
                App.TextAsset = valid;
                var document = App.AssetData;
                Assert.Throws<System.IO.InvalidDataException>(() => App.TextAsset = invalid);
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
