using NBC.ActionEditor.Model;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public class DocumentModelTests
    {
        public static ActionDocument Sample()
        {
            var document = ScriptableObject.CreateInstance<ActionDocument>();
            var group = new DocumentGroup();
            var track = new DocumentTrack();
            track.Events.Add(new SignalEvent { Start = 0.25, Message = "Reference" });
            track.Events.Add(new IntervalEvent { Start = 1, Duration = 0.5, Value = 7 });
            group.Tracks.Add(track);
            document.Groups.Add(group);
            return document;
        }

        [Test]
        public void RuntimeModelHasStableIdsAndNoWorkspaceOrPlaybackFields()
        {
            var document = Sample();
            try
            {
                Assert.That(DocumentValidation.Inspect(document), Is.Empty);
                var fields = typeof(ActionDocument).GetFields();
                Assert.That(System.Array.Exists(fields, f => f.Name.Contains("Zoom") || f.Name.Contains("Selection") || f.Name.Contains("CurrentTime")), Is.False);
            }
            finally { Object.DestroyImmediate(document); }
        }

        [Test]
        public void InvalidAndUnknownDataAreDiagnosedWithoutMutation()
        {
            var document = Sample();
            try
            {
                var track = document.Groups[0].Tracks[0];
                track.Events[1].Id = track.Events[0].Id;
                track.Events[1].Duration = double.NaN;
                track.Events.Add(null);
                Assert.That(DocumentValidation.Inspect(document).Count, Is.EqualTo(3));
                Assert.That(track.Events.Count, Is.EqualTo(3));
                Assert.That(double.IsNaN(track.Events[1].Duration), Is.True);
            }
            finally { Object.DestroyImmediate(document); }
        }

        [Test]
        public void OverlapPolicyDoesNotRejectAdjacentIntervalsOrSameTimePoints()
        {
            var document = Sample();
            try
            {
                var track = document.Groups[0].Tracks[0];
                track.Overlap = OverlapPolicy.Reject;
                track.Events.Add(new IntervalEvent { Start = 1.5, Duration = 0.5 });
                track.Events.Add(new SignalEvent { Start = 0.25 });
                Assert.That(DocumentValidation.Inspect(document), Is.Empty);
                track.Events[2].Start = 1.25;
                Assert.That(DocumentValidation.Inspect(document).Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(document); }
        }

        [Test]
        public void UnityClonePreservesPolymorphismButDoesNotShareMutableEventState()
        {
            var original = Sample();
            var clone = Object.Instantiate(original);
            try
            {
                ((SignalEvent)clone.Groups[0].Tracks[0].Events[0]).Tags.Add("copy");
                Assert.That(((SignalEvent)original.Groups[0].Tracks[0].Events[0]).Tags, Is.Empty);
                Assert.That(clone.Id, Is.EqualTo(original.Id));
                Assert.That(clone.Groups[0].Tracks[0].Events[1], Is.TypeOf<IntervalEvent>());
            }
            finally { Object.DestroyImmediate(original); Object.DestroyImmediate(clone); }
        }
    }
}
