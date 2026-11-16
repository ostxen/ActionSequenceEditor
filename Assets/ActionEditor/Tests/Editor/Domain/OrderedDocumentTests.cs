using System;
using System.IO;
using System.Linq;
using System.Text;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public sealed class OrderedDocumentTests
    {
        [Serializable] public sealed class FixtureDocument : ActionSequence
        {
            public float[] Numbers;
            public Texture2D Reference;
        }
        [Serializable] public sealed class FixtureTrack : ActionTrack { }
        [Serializable] public sealed class FixtureInterval : ActionClip { public AnimationCurve Curve; }
        [Serializable] public sealed class FixturePoint : PointClip { public string Payload; }

        // 独立手写字段和值，不从待测编码器生成预期。
        private const string Fixture = @"{
          ""format"":""actioneditor.json"",""version"":2,
          ""document"":{
            ""$type"":""NBC.ActionEditor.Tests.OrderedDocumentTests+FixtureDocument, NBC.ActionEditor.Editor.Tests"",
            ""Numbers"":[-0.0,1.401298464324817e-45,3.4028234663852886e38,-3.4028234663852886e38],
            ""Reference"":{""$unity"":{""guid"":""0a565ffb56d0d444c85082f1c0e7cc5c"",""localFileId"":2800000}},
            ""length"":1.5,""rangeMin"":0,""rangeMax"":6,
            ""rootItems"":[
              {""$type"":""NBC.ActionEditor.Model.ActionGroup, NBC.ActionEditor.Model"",""ActorId"":0,""name"":""first"",""active"":true,""isLocked"":false,""tracks"":[]},
              {""$type"":""NBC.ActionEditor.Tests.OrderedDocumentTests+FixtureTrack, NBC.ActionEditor.Editor.Tests"",""name"":""root"",""active"":true,""isLocked"":false,""color"":{""r"":1,""g"":1,""b"":1,""a"":1},""actionClips"":[
                {""$type"":""NBC.ActionEditor.Tests.OrderedDocumentTests+FixtureInterval, NBC.ActionEditor.Editor.Tests"",""name"":""interval"",""startTime"":0.5,""length"":1,
                 ""Curve"":{""keys"":[{""time"":0,""value"":0.25,""inTangent"":0,""outTangent"":0,""inWeight"":0.125,""outWeight"":0.5,""weightedMode"":3}],""preWrapMode"":2,""postWrapMode"":4}},
                {""$type"":""NBC.ActionEditor.Tests.OrderedDocumentTests+FixturePoint, NBC.ActionEditor.Editor.Tests"",""name"":""point"",""startTime"":1.5,""length"":1,""Payload"":null}
              ]},
              {""$type"":""NBC.ActionEditor.Model.ActionGroup, NBC.ActionEditor.Model"",""ActorId"":0,""name"":""last"",""active"":true,""isLocked"":false,""tracks"":[
                {""$type"":""NBC.ActionEditor.Tests.OrderedDocumentTests+FixtureTrack, NBC.ActionEditor.Editor.Tests"",""name"":""child"",""active"":true,""isLocked"":false,""color"":{""r"":1,""g"":1,""b"":1,""a"":1},""actionClips"":[]}
              ]}
            ]
          }
        }";
        private static SequenceFormatContext Context => SequenceFormats.CreateContext(typeof(FixtureDocument), typeof(FixtureTrack), typeof(FixtureInterval), typeof(FixturePoint));
        private static FixtureDocument Read(string json = Fixture) => (FixtureDocument)new SequenceJsonFormat().Deserialize(Encoding.UTF8.GetBytes(json), Context);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        [Test] public void HandwrittenMixedRootsKeepOwnershipValuesCurveReferenceAndExactBits()
        {
            var document = Read();
            Assert.That(document.RootContainers.Select(item => item.GetType()), Is.EqualTo(new[] { typeof(ActionGroup), typeof(FixtureTrack), typeof(ActionGroup) }));
            var root = (FixtureTrack)document.RootContainers[1]; var group = (ActionGroup)document.RootContainers[2];
            Assert.That(root.Parent, Is.Null); Assert.That(root.Root, Is.SameAs(document));
            Assert.That(group.Tracks[0].Parent, Is.SameAs(group));
            Assert.That(document.AllTracks, Is.EqualTo(new[] { root, group.Tracks[0] }));
            Assert.That(root.Clips[0].Parent, Is.SameAs(root)); Assert.That(root.Clips[1], Is.TypeOf<FixturePoint>());
            Assert.That(root.Clips[1].Length, Is.Zero); Assert.That(((FixturePoint)root.Clips[1]).Payload, Is.Null);
            Assert.That(document.Numbers.Select(Bits), Is.EqualTo(new[] { int.MinValue, 1, 0x7f7fffff, unchecked((int)0xff7fffff) }));
            Assert.That(document.Reference, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ActionEditor/Editor/Resources/nbc/Logo.png")));
            var curve = ((FixtureInterval)root.Clips[0]).Curve;
            Assert.That(curve.preWrapMode, Is.EqualTo(UnityEngine.WrapMode.Loop)); Assert.That(curve.postWrapMode, Is.EqualTo(UnityEngine.WrapMode.PingPong));
            Assert.That(curve.keys[0].inWeight, Is.EqualTo(.125f)); Assert.That(curve.keys[0].outWeight, Is.EqualTo(.5f));
            Assert.That(curve.keys[0].weightedMode, Is.EqualTo(WeightedMode.Both));
            foreach (var compressed in new[] { true, false })
            {
                var format = new SequenceJsonFormat();
                var loaded = (FixtureDocument)format.Deserialize(format.Serialize(document, new SequenceFormatOptions(compressed), Context), Context);
                Assert.That(JsonValueConverter.Capture(loaded, typeof(ActionSequence), Context).ContentKey(), Is.EqualTo(JsonValueConverter.Capture(document, typeof(ActionSequence), Context).ContentKey()));
            }
        }

        [Test] public void MixedRootContractRejectsAliasesMissingMembersNestedGroupsAndInvalidReferences()
        {
            foreach (var invalid in new[] {
                Fixture.Replace("\"version\":2", "\"version\":1"),
                Fixture.Replace("\"rootItems\":", "\"groups\":"),
                Fixture.Replace("\"rangeMin\":0,", ""),
                Fixture.Replace("\"rangeMin\":0,", "\"unknown\":0,\"rangeMin\":0,"),
                Fixture.Replace("\"tracks\":[]", "\"tracks\":[{\"$type\":\"NBC.ActionEditor.Model.ActionGroup, NBC.ActionEditor.Model\",\"ActorId\":0,\"name\":null,\"active\":true,\"isLocked\":false,\"tracks\":[]}]"),
                Fixture.Replace("FixtureInterval,", "MissingInterval,"),
                Fixture.Replace("3.4028234663852886e38", "1e999"),
                Fixture.Replace("2800000", "2800001") })
            {
                Assert.That(invalid, Is.Not.EqualTo(Fixture));
                Assert.Throws<InvalidDataException>(() => Read(invalid));
            }
            var document = Read(); document.AddRootContainer(document.RootContainers[1]);
            Assert.Throws<InvalidDataException>(() => new SequenceJsonFormat().Serialize(document, default, Context));
        }

        [Test] public void WindowWorkspacesDoNotEnterSnapshotsAndReleaseRemovedGraphReferences()
        {
            var document = Read(); var group = (ActionGroup)document.RootContainers[0];
            var first = new TimelineWorkspace(); var second = new TimelineWorkspace(); first.Bind(document); second.Bind(document);
            var before = SequenceCopy.GetContentKey(document, Context);
            first.SetRange(-2, 9); first.ScrollY = 45; first.LeftWidth = 350; first.SetCollapsed(group, true);
            Assert.That(second.ViewTimeMin, Is.Zero); Assert.That(second.ScrollY, Is.Zero); Assert.That(second.LeftWidth, Is.EqualTo(240)); Assert.That(second.IsCollapsed(group), Is.False);
            Assert.That(SequenceCopy.GetContentKey(document, Context), Is.EqualTo(before));
            document.RemoveRootContainer(group); first.Bind(document); Assert.That(first.IsCollapsed(group), Is.False);
            first.Bind(Read()); Assert.That(first.ScrollY, Is.EqualTo(45)); Assert.That(first.IsCollapsed(group), Is.False);
            first.Bind(null); Assert.That(first.ScrollY, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => first.SetRange(-float.MaxValue, float.MaxValue));
        }

        [Test] public void ClipSettersRetainSubDisplayPrecisionAndNegativeZero()
        {
            var clip = new FixtureInterval();
            clip.StartTime = float.Epsilon; Assert.That(Bits(clip.StartTime), Is.EqualTo(1));
            clip.StartTime = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0); Assert.That(Bits(clip.StartTime), Is.EqualTo(int.MinValue));
            clip.StartTime = 1; clip.EndTime = 1.00001f; Assert.That(clip.Length, Is.EqualTo(1.00001f - 1));
        }
    }
}
