using NBC.ActionEditor.Views.IMGUI;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Presentation.Contracts;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;
using System.Collections;

namespace NBC.ActionEditor.Tests
{
    public class DocumentEntryTests
    {
        private TextAsset previousText;
        private string previousRecovery;
        [SetUp] public void SetUp()
        {
            previousText = EditorFixture.Shared.Source;
            previousRecovery = EditorFixture.Shared.Session.CaptureRecovery();
            EditorFixture.Shared.Open(null);
        }
        [TearDown] public void TearDown()
        {
            EditorFixture.Shared.Session.CancelEdit();
            EditorFixture.Shared.Open(previousText);
            EditorFixture.Shared.Session.RestoreRecovery(previousRecovery);
        }
        [UnityTest]
        public IEnumerator SelectingDocumentDrawsImguiTimelineWithoutOpeningToolkit()
        {
            var previous = EditorFixture.Shared.Source;

            var toolkitWindows = Resources.FindObjectsOfTypeAll<ToolkitWindow>();
            var sample = new NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument();
            var group = new NBC.ActionEditor.Samples.FeatureSample.FeatureSampleGroup { Name = "Reference" };
            group.Tracks.Add(new NBC.ActionEditor.Samples.FeatureSample.FeatureSampleIntervalTrack { Name = "Events" });
            sample.ClearRoots(); sample.AddRoot(group); sample.Init();
            var json = new TextAsset(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Encode(sample));
            var window = ScriptableObject.CreateInstance<ImguiWindow>();
            try
            {
                window.Show(); window.position = new Rect(60, 60, 1000, 600);
                EditorFixture.Shared.CreateDocuments().OpenPicked(json);
                window.Repaint(); yield return null; yield return null;
                Assert.That(EditorFixture.Shared.Source, Is.SameAs(json));
                Assert.That(EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Name, Is.EqualTo("Events"));
                var timeline = window.Timeline.State;
                Assert.That(timeline.Size.Width, Is.EqualTo(window.position.width).Within(1), "IMGUI must use the actual window geometry.");
                Assert.That(timeline.Size.Height, Is.GreaterThan(200));
                Assert.That(timeline.Rows.Count, Is.EqualTo(2));
                Assert.That(timeline.Rows[1].Title, Is.EqualTo("Events"));
                Assert.That(Resources.FindObjectsOfTypeAll<ToolkitWindow>(), Is.EquivalentTo(toolkitWindows));
                var documents = EditorFixture.Shared.CreateDocuments(_ => null);
                Assert.DoesNotThrow(() => documents.Invoke(DocumentIntent.Save), "Canceling the save path leaves the in-memory fixture unchanged.");
                Assert.That(EditorFixture.Shared.Source, Is.SameAs(json));
            }
            finally
            {
                window.Close(); EditorFixture.Shared.Open(previous);
                UnityEngine.Object.DestroyImmediate(json);
            }
        }

        [Test]
        public void InvalidCreationTypeKeepsDocumentAndBothWindowSets()
        {
            var imgui = Resources.FindObjectsOfTypeAll<ImguiWindow>();
            var toolkit = Resources.FindObjectsOfTypeAll<ToolkitWindow>();
            var document = EditorFixture.Shared.Document;
            var target = EditorFixture.Shared.Source;
            var result = EditorFixture.Shared.Lifecycle.Create(typeof(string), "Assets/InvalidDocumentCreation.json");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Exception, Is.TypeOf<ArgumentException>());
            Assert.That(result.BytesCommitted, Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<ImguiWindow>(), Is.EquivalentTo(imgui));
            Assert.That(Resources.FindObjectsOfTypeAll<ToolkitWindow>(), Is.EquivalentTo(toolkit));
            Assert.That(EditorFixture.Shared.Document, Is.SameAs(document));
            Assert.That(EditorFixture.Shared.Source, Is.SameAs(target));
        }

        [Test]
        public void NativeMenusOpenSeparateWindowTypesWithoutChangingDocuments()
        {
            var existingImguiWindows = Resources.FindObjectsOfTypeAll<ImguiWindow>();
            var toolkitWindows = Resources.FindObjectsOfTypeAll<ToolkitWindow>();
            var document = EditorFixture.Shared.Document;
            var target = EditorFixture.Shared.Source;
            try
            {
                Assert.DoesNotThrow(ImguiWindow.Open);
                Assert.That(Resources.FindObjectsOfTypeAll<ImguiWindow>(), Is.Not.Empty);
                Assert.That(Resources.FindObjectsOfTypeAll<ToolkitWindow>(), Is.EquivalentTo(toolkitWindows));
                var imguiWindow = Resources.FindObjectsOfTypeAll<ImguiWindow>().First();
                Assert.That(imguiWindow.Timeline, Is.TypeOf<NBC.ActionEditor.Views.IMGUI.TimelineSurface>());
                Assert.That(typeof(ImguiWindow).GetField("_welcomeView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(imguiWindow), Is.TypeOf<WelcomeView>());
                Assert.DoesNotThrow(ToolkitWindow.Open);
                Assert.That(Resources.FindObjectsOfTypeAll<ToolkitWindow>(), Is.Not.Empty);
                Assert.That(EditorFixture.Shared.Document, Is.SameAs(document));
                Assert.That(EditorFixture.Shared.Source, Is.SameAs(target));
                foreach (var window in Resources.FindObjectsOfTypeAll<ToolkitWindow>()) Assert.That(window.Documents.DocumentType, Is.EqualTo(document?.GetType()));
                var documents = EditorFixture.Shared.CreateDocuments(_ => null);
                Assert.DoesNotThrow(() => documents.Invoke(DocumentIntent.Save));
                Assert.That(EditorFixture.Shared.Document, Is.SameAs(document));
            }
            finally
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<ImguiWindow>().Except(existingImguiWindows)) window.Close();
                foreach (var window in Resources.FindObjectsOfTypeAll<ToolkitWindow>().Except(toolkitWindows)) window.Close();
            }
        }

        [Test]
        public void SaveWithoutDocumentIsRejected()
        {
            using var session = NBC.ActionEditor.Composition.DocumentComposition.Create();
            Assert.Throws<InvalidOperationException>(() => session.Save());
        }

        [Test]
        public void EmptyDocumentAutoSaveNeverUpdatesTimestampOrWrites()
        {
            using var context = new EditorFixture(Composition.DocumentComposition.Create());
            var before = context.AutoSave.LastSaved;
            Assert.That(context.AutoSave.Tick(1), Is.Null);
            Assert.That(context.AutoSave.LastSaved, Is.EqualTo(before));
        }

        [Test]
        public void FailedOpenDoesNotChangeDocumentOrTarget()
        {
            var previous = EditorFixture.Shared.Source;
            var valid = new TextAsset(NBC.ActionEditor.Infrastructure.Serialization.JsonDocumentCodec.Encode(new NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument()));
            var invalid = new TextAsset("not json");
            try
            {
                EditorFixture.Shared.Open(valid);
                var document = EditorFixture.Shared.Document;
                Assert.Throws<System.IO.InvalidDataException>(() => EditorFixture.Shared.Open(invalid));
                Assert.That(EditorFixture.Shared.Source, Is.SameAs(valid));
                Assert.That(EditorFixture.Shared.Document, Is.SameAs(document));
            }
            finally
            {
                EditorFixture.Shared.Open(previous);
                UnityEngine.Object.DestroyImmediate(valid);
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }
    }
}
