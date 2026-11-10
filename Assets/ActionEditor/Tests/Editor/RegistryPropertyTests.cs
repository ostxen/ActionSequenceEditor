using System;
using System.Collections;
using System.Linq;
using NBC.ActionEditor.Extensibility;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    [Serializable]
    public sealed class ProbeEvent : TimelineEvent
    {
        public int Count;
        public override string TypeId => "tests.probe";
        public override EventKind Kind => EventKind.Point;
    }

    public class RegistryPropertyTests
    {
        private static EventDescriptor Probe(string owner = "test provider") => new("tests.probe", typeof(ProbeEvent), owner,
            () => new ProbeEvent(), e => "Count " + ((ProbeEvent)e).Count,
            context => context.Integer("Count", e => ((ProbeEvent)e).Count, (e, value) => ((ProbeEvent)e).Count = value),
            e => ((ProbeEvent)e).Count < 0 ? new[] { "Count must be nonnegative." } : Array.Empty<string>());

        [UnityTest]
        public IEnumerator RegistrationIsAtomicAndCustomTypeUsesTheSameTransactionBoundary()
        {
            var registry = new EventRegistry();
            registry.Register(new[] { Probe("first") });
            var error = Assert.Throws<InvalidOperationException>(() => registry.Register(new[] { Probe("second") }));
            StringAssert.Contains("first", error.Message); StringAssert.Contains("second", error.Message);
            Assert.That(registry.Descriptors.Count, Is.EqualTo(1));
            using var session = new DocumentSession(registry: registry);
            session.New();
            session.Document.Groups.Add(new DocumentGroup());
            var track = new DocumentTrack(); session.Document.Groups[0].Tracks.Add(track);
            using (var edit = new DocumentTransaction(session, "custom add"))
            { DocumentCommands.Add(edit, track.Id, registry.Create("tests.probe")); edit.Commit(); }
            var id = session.Selection.Single();
            using (var edit = new DocumentTransaction(session, "invalid custom value"))
            {
                ((ProbeEvent)DocumentCommands.Event(edit.Candidate, id)).Count = -1;
                Assert.Throws<InvalidOperationException>(() => edit.Commit());
            }
            Assert.That(((ProbeEvent)DocumentCommands.Event(session.Document, id)).Count, Is.Zero);
            var host = new VisualElement();
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.Show(); yield return null;
                window.rootVisualElement.Add(host);
                registry.Find(DocumentCommands.Event(session.Document, id)).Properties(new EventPropertyContext(session, host, Assert.Fail));
                host.Q<IntegerField>("event-Count").value = 7;
                Assert.That(((ProbeEvent)DocumentCommands.Event(session.Document, id)).Count, Is.EqualTo(7));
                Undo.PerformUndo();
                Assert.That(((ProbeEvent)DocumentCommands.Event(session.Document, id)).Count, Is.Zero);
                Undo.PerformRedo();
                Assert.That(((ProbeEvent)DocumentCommands.Event(session.Document, id)).Count, Is.EqualTo(7));
            }
            finally { if (window != null) window.Close(); }
        }

        [Test]
        public void ConflictingBatchAndSharedFactoryCannotPartiallyPublish()
        {
            var registry = new EventRegistry();
            Assert.Throws<InvalidOperationException>(() => registry.Register(new[] { Probe("one"), Probe("two") }));
            Assert.That(registry.Descriptors, Is.Empty);
            var singleton = new ProbeEvent();
            Assert.Throws<InvalidOperationException>(() => registry.Register(new[] {
                new EventDescriptor(singleton.TypeId, typeof(ProbeEvent), "bad factory", () => singleton, e => e.Name, _ => { }) }));
            Assert.That(registry.Descriptors, Is.Empty);
        }

        [UnityTest]
        public IEnumerator NativeFieldsCollectionsNullAndMultiSelectionRoundTripWithUndo()
        {
            var window = ScriptableObject.CreateInstance<ActionWorkbench>();
            try
            {
                window.position = new Rect(50, 50, 1200, 700); window.Show(); yield return null;
                var propertyView = window.CreatePropertyView();
                propertyView.style.position = Position.Absolute; propertyView.style.right = 0; propertyView.style.width = 280;
                window.rootVisualElement.Add(propertyView);
                window.NewDocument(); window.AddEvent("actioneditor.signal");
                var canvas = window.rootVisualElement.Q<TimelineCanvas>();
                canvas.Focus();
                var playhead = window.Session.Workspace.Playhead;
                window.Interaction.Begin(new Vector2(250, 10), 0, false);
                Assert.That(window.Interaction.IsActive, Is.True);
                using (var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                { escape.target = canvas; canvas.SendEvent(escape); }
                Assert.That(window.Interaction.IsActive, Is.False);
                Assert.That(window.Session.Workspace.Playhead, Is.EqualTo(playhead));
                var a = window.Session.Selection.Single();
                window.AddEvent("actioneditor.signal"); var b = window.Session.Selection.Single();
                window.Session.Selection.Clear(); window.Session.Selection.Add(a); window.Session.Selection.Add(b); window.Session.NotifyChanged();
                window.Properties.Q<TextField>("event-Message").value = "edited both";
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, a)).Message, Is.EqualTo("edited both"));
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, b)).Message, Is.EqualTo("edited both"));
                var add = window.Properties.Q<Button>("add-Tags");
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = add; add.SendEvent(submit); }
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, a)).Tags.Count, Is.EqualTo(1));
                window.Properties.Q<TextField>("Tags-0").value = "tag";
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, b)).Tags[0], Is.EqualTo("tag"));
                var nullList = window.Properties.Q<Button>("null-Tags");
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = nullList; nullList.SendEvent(submit); }
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, a)).Tags, Is.Null);
                Undo.PerformUndo();
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, a)).Tags.Single(), Is.EqualTo("tag"));
                var nullText = window.Properties.Q<Button>("null-Message");
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = nullText; nullText.SendEvent(submit); }
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, a)).Message, Is.Null);
                Undo.PerformUndo();
                Assert.That(((SignalEvent)DocumentCommands.Event(window.Session.Document, b)).Message, Is.EqualTo("edited both"));
                var previous = EditorJsonUtility.ToJson(window.Session.Document);
                window.Properties.Q<DoubleField>("event-Start").value = -1;
                Assert.That(EditorJsonUtility.ToJson(window.Session.Document), Is.EqualTo(previous));
                StringAssert.Contains("invalid", window.rootVisualElement.Q<Label>("document-status").text);
                window.AddEvent("actioneditor.interval");
                var intervalId = window.Session.Selection.Single();
                window.Properties.Q<DoubleField>("event-Value").value = 3.25;
                window.Properties.Q<Vector3Field>("event-Offset").value = new Vector3(1, 2, 3);
                Assert.That(((IntervalEvent)DocumentCommands.Event(window.Session.Document, intervalId)).Value, Is.EqualTo(3.25f));
                Assert.That(((IntervalEvent)DocumentCommands.Event(window.Session.Document, intervalId)).Offset, Is.EqualTo(new Vector3(1, 2, 3)));
                ((IntervalEvent)DocumentCommands.Event(window.Session.Document, intervalId)).Value = float.NaN;
                window.Session.Selection.Clear(); window.Session.NotifyChanged();
                var issue = window.Properties.Q<Button>("issue-" + intervalId);
                Assert.That(issue, Is.Not.Null);
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = issue; issue.SendEvent(submit); }
                Assert.That(window.Session.Selection, Is.EquivalentTo(new[] { intervalId }));
                window.Properties.Q<DoubleField>("event-Value").value = 2;
                Assert.That(window.Session.Registry.Inspect(window.Session.Document), Is.Empty);
                window.Session.DiscardAndClose();
            }
            finally { if (window != null) window.Close(); }
        }

        [Test]
        public void UnknownTypesAndTransientReferencesAreDiagnosedWithoutDiscardingContent()
        {
            using var session = new DocumentSession(); session.New();
            var track = new DocumentTrack(); session.Document.Groups.Add(new DocumentGroup { Tracks = { track } });
            var unknown = new ProbeEvent { Count = 123 };
            track.Events.Add(unknown);
            var before = EditorJsonUtility.ToJson(session.Document);
            Assert.That(session.Registry.Inspect(session.Document).Any(i => i.TargetId == unknown.Id && i.Message.Contains("Unregistered")), Is.True);
            Assert.Throws<InvalidOperationException>(() => session.Save("Assets/not-written.asset"));
            Assert.That(EditorJsonUtility.ToJson(session.Document), Is.EqualTo(before));
            track.Events.Clear();
            var resource = new Texture2D(1, 1);
            try
            {
                track.Events.Add(new SignalEvent { Resource = resource });
                Assert.That(session.Registry.Inspect(session.Document).Any(i => i.Message.Contains("resource reference")), Is.True);
                Assert.Throws<InvalidOperationException>(() => session.Save("Assets/not-written.asset"));
                Assert.That(((SignalEvent)track.Events[0]).Resource, Is.SameAs(resource));
            }
            finally { UnityEngine.Object.DestroyImmediate(resource); }
        }
    }
}
