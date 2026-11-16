using System.Linq;
using NBC.ActionEditor.Infrastructure.Preview;
using NBC.ActionEditor.Model;
using System;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests
{
    public class DomainBehaviorTests
    {
        [Serializable]
        public sealed class FixtureDocument : ActionSequence { }

        [Serializable]
        public sealed class FixtureTrack : ActionTrack { }

        [Serializable]
        public sealed class FixtureInterval : ActionClip
        {
            public string Message = "payload";
            public int Amount = 7;
        }

        [Serializable]
        public sealed class FixturePoint : PointClip { }

        private sealed class RecordingPreview : PreviewBase
        {
            public int Enters;
            public int Exits;
            public int Reverses;
            public override void Enter() { Enters++; }
            public override void Exit() { Exits++; }
            public override void Reverse() { Reverses++; }
            public override void Update(float time, float previousTime) { }
        }

        private static FixtureDocument CreateSample()
        {
            var asset = new FixtureDocument();
            var group = new ActionGroup { Name = "Reference" };
            var track = new FixtureTrack { Name = "Events" };
            track.Clips.Add(new FixtureInterval { StartTime = 0.25f, Length = 0.5f });
            track.Clips.Add(new FixturePoint { StartTime = 1.25f });
            group.Tracks.Add(track);
            asset.AddRootContainer(group);
            asset.Init();
            return asset;
        }

        [Test]
        public void ObjectGraph_RebuildsParentsAndDuration()
        {
            var asset = CreateSample();
            var group = asset.RootContainers.OfType<ActionGroup>().ElementAt(0);
            var track = group.Tracks[0];
            Assert.That(track.Parent, Is.SameAs(group));
            Assert.That(track.Clips[0].Parent, Is.SameAs(track));
            Assert.That(track.Clips[0].Root, Is.SameAs(asset));
            Assert.That(asset.directables.Count, Is.EqualTo(4));
            Assert.That(asset.Length, Is.EqualTo(1.25f));
        }

        [Test]
        public void Json_RoundTripPreservesPolymorphicTimesAndParameters()
        {
            var original = CreateSample();
            original.OnBeforeSerialize();
            var context = Infrastructure.Serialization.SequenceFormats.CreateContext(typeof(FixtureDocument), typeof(FixtureTrack), typeof(FixtureInterval), typeof(FixturePoint));
            var format = new Serialization.SequenceJsonFormat();
            var json = format.Serialize(original, default, context);
            Assert.That(json, Is.Not.Empty);
            var loaded = format.Deserialize(json, context) as FixtureDocument;
            Assert.That(loaded, Is.Not.Null);
            loaded.Init();
            var clip = loaded.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0] as FixtureInterval;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.StartTime, Is.EqualTo(0.25f));
            Assert.That(clip.Length, Is.EqualTo(0.5f));
            Assert.That(clip.Message, Is.EqualTo("payload"));
            Assert.That(clip.Amount, Is.EqualTo(7));
            Assert.That(loaded.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[1], Is.TypeOf<FixturePoint>());
        }

        [Test]
        public void Signal_HasZeroDurationAndClampsNegativeStart()
        {
            var signal = new FixturePoint { StartTime = -1 };
            Assert.That(signal.StartTime, Is.Zero);
            Assert.That(signal.Length, Is.Zero);
            Assert.That(signal.EndTime, Is.Zero);
        }

        [Test]
        public void EmptyDocumentHasMinimumLength()
        {
            Assert.That(new FixtureDocument().Length, Is.EqualTo(0.1f));
        }

        [Test]
        public void PreviewPointers_ForwardEndAndRewindHaveObservableCallbacks()
        {
            var clip = CreateSample().RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips[0];
            var preview = new RecordingPreview();
            preview.SetTarget(clip);
            IDirectableTimePointer start = new StartTimePointer(preview);
            IDirectableTimePointer end = new EndTimePointer(preview);
            start.TriggerForward(0.3f, 0);
            start.TriggerForward(0.4f, 0.3f);
            Assert.That(preview.Enters, Is.EqualTo(1));
            end.TriggerForward(0.75f, 0.4f);
            Assert.That(preview.Exits, Is.EqualTo(1));
            start.TriggerBackward(0, 0.75f);
            Assert.That(preview.Reverses, Is.EqualTo(1));
        }
    }
}
