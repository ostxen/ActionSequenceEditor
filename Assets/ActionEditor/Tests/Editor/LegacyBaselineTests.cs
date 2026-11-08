using System;
using NUnit.Framework;

namespace NBC.ActionEditor.Tests
{
    public class LegacyBaselineTests
    {
        [Serializable]
        public sealed class BaselineAsset : Asset { }

        [Serializable]
        public sealed class BaselineTrack : Track { }

        [Serializable]
        public sealed class BaselineClip : Clip
        {
            public string Message = "baseline";
            public int Amount = 7;
        }

        [Serializable]
        public sealed class BaselineSignal : ClipSignal { }

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

        private static BaselineAsset CreateSample()
        {
            var asset = new BaselineAsset();
            var group = new Group { Name = "Reference" };
            var track = new BaselineTrack { Name = "Events" };
            track.Clips.Add(new BaselineClip { StartTime = 0.25f, Length = 0.5f });
            track.Clips.Add(new BaselineSignal { StartTime = 1.25f });
            group.Tracks.Add(track);
            asset.groups.Add(group);
            asset.Init();
            return asset;
        }

        [Test]
        public void ObjectGraph_RebuildsParentsAndDuration()
        {
            var asset = CreateSample();
            var group = asset.groups[0];
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
            var json = Json.Serialize(original);
            Assert.That(json, Is.Not.Empty);
            var loaded = Json.Deserialize(typeof(Asset), json) as BaselineAsset;
            Assert.That(loaded, Is.Not.Null);
            loaded.Init();
            var clip = loaded.groups[0].Tracks[0].Clips[0] as BaselineClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.StartTime, Is.EqualTo(0.25f));
            Assert.That(clip.Length, Is.EqualTo(0.5f));
            Assert.That(clip.Message, Is.EqualTo("baseline"));
            Assert.That(clip.Amount, Is.EqualTo(7));
            Assert.That(loaded.groups[0].Tracks[0].Clips[1], Is.TypeOf<BaselineSignal>());
        }

        [Test]
        public void Signal_HasZeroDurationAndClampsNegativeStart()
        {
            var signal = new BaselineSignal { StartTime = -1 };
            Assert.That(signal.StartTime, Is.Zero);
            Assert.That(signal.Length, Is.Zero);
            Assert.That(signal.EndTime, Is.Zero);
        }

        [Test]
        public void DocumentLength_HasExistingMinimumForEmptyGraph()
        {
            Assert.That(new BaselineAsset().Length, Is.EqualTo(0.1f));
        }

        [Test]
        public void PreviewPointers_ForwardEndAndRewindHaveObservableCallbacks()
        {
            var clip = CreateSample().groups[0].Tracks[0].Clips[0];
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
