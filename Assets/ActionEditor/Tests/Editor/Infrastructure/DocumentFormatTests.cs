using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace NBC.ActionEditor.Tests
{
    public sealed class DocumentFormatTests
    {
        [Serializable] public sealed class Values
        {
            public float[] Numbers;
            public List<string> Nullable;
            public Vector4 Vector;
            public Quaternion Rotation;
            public Color Color;
            public AnimationCurve Curve;
            public Texture2D Reference;
            public Nested Nested;
            public Business Polymorphic;
        }
        [Serializable] public struct Nested { public float Number; public int Count; }
        [Serializable] public abstract class Business { [SerializeField] private float baseValue = .1f; public float BaseValue => baseValue; }
        [Serializable] public sealed class DerivedBusiness : Business { public string Text; }
        [Serializable] public class CollisionBase { public int Same; }
        [Serializable] public sealed class Collision : CollisionBase { public new int Same; }
        [Serializable] public sealed class RewritingGroup : ActionGroup
        {
            public int Counter;
            public override void OnAfterDeserialize() { Counter++; }
        }
        [Serializable] public sealed class PayloadAsset : ActionSequence
        {
            public Values Payload;
            public PayloadAsset() { ConstructorCalls++; }
            [NonSerialized] public static int ConstructorCalls;
        }
        [Serializable] public sealed class CallbackGroup : ActionGroup
        {
            public int Counter;
            public override void OnBeforeSerialize() { Counter++; }
        }
        private static SequenceFormatContext Context => SequenceFormats.CreateContext(typeof(Values), typeof(Nested), typeof(PayloadAsset), typeof(CallbackGroup), typeof(DerivedBusiness));
        private static readonly SequenceJsonFormat Format = new SequenceJsonFormat();
        private static string Text(ActionSequence asset, bool compressed = true) => Encoding.UTF8.GetString(Format.Serialize(asset, new SequenceFormatOptions(compressed), Context));
        private static ActionSequence Read(string json) => Format.Deserialize(Encoding.UTF8.GetBytes(json), Context);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        [Test] public void TypeAndFieldMetadataStayIsolatedAcrossContextsAndPublicCopies()
        {
            var trusted = new SequenceFormatContext(new[] { typeof(Values), typeof(Nested) });
            var denied = new SequenceFormatContext(Array.Empty<Type>());
            var expected = typeof(Values).FullName + ", " + typeof(Values).Assembly.GetName().Name;
            Assert.That(trusted.ResolveType(expected, typeof(Values), "$"), Is.EqualTo(typeof(Values)));
            trusted.RequireType(typeof(Values), "$");
            Assert.Throws<InvalidDataException>(() => denied.RequireType(typeof(Values), "$"));
            Assert.Throws<InvalidDataException>(() => denied.ResolveType(expected, typeof(Values), "$"));
            var fields = JsonValueConverter.Fields(typeof(Values));
            var first = fields[0]; fields[0] = null;
            Assert.That(JsonValueConverter.Fields(typeof(Values))[0], Is.EqualTo(first));
            var source = new Values { Numbers = new[] { float.Epsilon, -.0f } };
            var captured = JsonValueConverter.Capture(source, typeof(Values), trusted);
            var restored = (Values)JsonValueConverter.Restore(captured, typeof(Values), trusted);
            Assert.That(Bits(restored.Numbers[0]), Is.EqualTo(1));
            Assert.Throws<InvalidDataException>(() => JsonValueConverter.Capture(source, typeof(Values), denied));
            Assert.Throws<InvalidDataException>(() => JsonValueConverter.Restore(captured, typeof(Values), denied));
        }

        [Test] public void IndependentContractFixturesReplaceConstructorDefaultsAndPreserveSignalStorage()
        {
            var contract = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "../Docs/ActionEditor-Contracts.md"));
            var fixtures = System.Text.RegularExpressions.Regex.Matches(contract, "```json\\s*([\\s\\S]*?)```").Cast<System.Text.RegularExpressions.Match>().Take(2).Select(m => m.Groups[1].Value).ToArray();
            Assert.That(fixtures.Length, Is.EqualTo(2));
            var empty = (FeatureSampleSequence)Read(fixtures[0]);
            Assert.That(empty.RootContainers.OfType<ActionGroup>(), Is.Empty); Assert.That(empty.Notes, Is.Null);
            Assert.That(Bits(empty.Length), Is.EqualTo(unchecked((int)0x3dcccccd)));
            Assert.That(empty.RangeMax, Is.EqualTo(6));
            var point = (FeatureSampleSequence)Read(fixtures[1]);
            Assert.That(point.RootContainers.OfType<ActionGroup>().Count(), Is.EqualTo(1)); Assert.That(point.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks.Count, Is.EqualTo(1));
            var track = point.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]; Assert.That(track.Clips.Count, Is.EqualTo(1));
            var marker = (FeatureSampleMarker)track.Clips[0];
            Assert.That(marker.EventCode, Is.EqualTo(7)); Assert.That(marker.Notes, Is.Null);
            Assert.That(Bits(marker.StartTime), Is.EqualTo(unchecked((int)0x3f000000)));
            Assert.That(marker.Length, Is.Zero); Assert.That((float)typeof(ActionClip).GetField("length", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(marker), Is.EqualTo(1));
            Assert.That(marker.Parent, Is.SameAs(track)); Assert.That(marker.Root, Is.SameAs(point));
            Assert.That(SequenceFormatContext.Identity(typeof(FeatureSampleSequence)), Is.EqualTo("NBC.ActionEditor.Samples.FeatureSample.FeatureSampleSequence, NBC.ActionEditor.Model"));
            Assert.That(SequenceFormatContext.Identity(typeof(ActionGroup)), Is.EqualTo("NBC.ActionEditor.Model.ActionGroup, NBC.ActionEditor.Model"));
            // 旧身份必须拒绝，不以别名、旧程序集或模型基类回退解析。
            foreach (var oldIdentity in new[] {
                "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument, NBC.ActionEditor.Domain",
                "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleSequence, NBC.ActionEditor.Domain",
                "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument, NBC.ActionEditor.Model" })
            {
                var invalid = fixtures[0].Replace("NBC.ActionEditor.Samples.FeatureSample.FeatureSampleSequence, NBC.ActionEditor.Model", oldIdentity);
                Assert.That(invalid, Is.Not.EqualTo(fixtures[0]));
                Assert.Throws<InvalidDataException>(() => Read(invalid));
            }
            var oldGroup = fixtures[1].Replace("NBC.ActionEditor.Samples.FeatureSample.FeatureSampleGroup, NBC.ActionEditor.Model", "NBC.ActionEditor.Domain.TimelineGroup, NBC.ActionEditor.Domain");
            Assert.That(oldGroup, Is.Not.EqualTo(fixtures[1]));
            Assert.Throws<InvalidDataException>(() => Read(oldGroup));
        }
        [Test] public void CompleteProductionSampleAndBothOutputsKeepAllFieldsAndHierarchy()
        {
            var source = new FeatureSampleSequence();
            foreach (var compressed in new[] { false, true })
            {
                var json = Text(source, compressed);
                Assert.That(json, Does.Contain("actioneditor.json"));
                var loaded = (FeatureSampleSequence)Read(json);
                Assert.That(loaded.RootContainers.OfType<ActionGroup>().Count(), Is.EqualTo(3));
                Assert.That(loaded.RootContainers.OfType<ActionGroup>().Sum(g => g.Tracks.Count), Is.EqualTo(7));
                Assert.That(loaded.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks).Sum(t => t.Clips.Count), Is.EqualTo(13));
                Assert.That(JsonValueConverter.Capture(loaded, typeof(ActionSequence), Context).ContentKey(), Is.EqualTo(JsonValueConverter.Capture(source, typeof(ActionSequence), Context).ContentKey()));
            }
        }
        [Test] public void FiniteFloatBitsIncludeNegativeZeroSubnormalsExtremesAndRandomPayloads()
        {
            var bits = new List<int> { 0, int.MinValue, 1, -2147483647, 0x007fffff, 0x00800000, 0x7f7fffff, unchecked((int)0xff7fffff), 0x3dcccccd };
            var random = new System.Random(1937);
            for (var i = 0; i < 4096; i++) { var bytes = new byte[4]; random.NextBytes(bytes); var raw = BitConverter.ToInt32(bytes, 0); if ((raw & 0x7f800000) != 0x7f800000) bits.Add(raw); }
            var values = bits.Select(b => BitConverter.ToSingle(BitConverter.GetBytes(b), 0)).ToArray();
            var source = new PayloadAsset { Payload = new Values { Numbers = values, Nullable = null, Vector = new Vector4(.1f, float.Epsilon, -0f, float.MaxValue), Rotation = new Quaternion(.1f, .2f, .3f, .4f), Color = new Color(.1f, .2f, .3f, .4f) } };
            foreach (var compressed in new[] { false, true })
            {
                var bytes = Format.Serialize(source, new SequenceFormatOptions(compressed), Context);
                Assert.That(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), Is.False);
                Assert.That(Encoding.UTF8.GetString(bytes), Does.Contain("-0.0"));
                var loaded = (PayloadAsset)Format.Deserialize(bytes, Context);
                Assert.That(loaded.Payload.Numbers.Select(Bits), Is.EqualTo(bits));
                Assert.That(loaded.Payload.Nullable, Is.Null);
                Assert.That(JsonValueConverter.Capture(loaded.Payload, typeof(Values), Context).ContentKey(), Is.EqualTo(JsonValueConverter.Capture(source.Payload, typeof(Values), Context).ContentKey()));
            }
        }
        [Test] public void WeightedCurveAndWrapModesPreserveAllComponents()
        {
            var key = new Keyframe(.1f, .2f, -.3f, .4f, .1234567f, .7654321f) { weightedMode = WeightedMode.Both };
            var source = new PayloadAsset { Payload = new Values { Curve = new AnimationCurve(key, new Keyframe(2, 3)) { preWrapMode = UnityEngine.WrapMode.Loop, postWrapMode = UnityEngine.WrapMode.PingPong } } };
            var loaded = (PayloadAsset)Read(Text(source));
            Assert.That(JsonValueConverter.Capture(loaded.Payload, typeof(Values), Context).ContentKey(), Is.EqualTo(JsonValueConverter.Capture(source.Payload, typeof(Values), Context).ContentKey()));
        }
        [Test] public void InvalidEnvelopeTypesMembersAndNumbersFailBeforeBusinessConstruction()
        {
            var json = Text(new PayloadAsset { Payload = new Values { Numbers = new[] { .1f } } });
            var variants = new[] { "{}", json.Replace("\"version\":2", "\"version\":1"), json.Replace("\"version\":2", "\"version\":2,\"version\":2"),
                json.Replace("\"Payload\":", "\"Unknown\":"), json.Replace("\"Nullable\":null,", ""),
                json.Replace(SequenceFormatContext.Identity(typeof(PayloadAsset)), "Unknown, Missing"),
                json.Replace("\"Numbers\":[0.10000000149011612]", "\"Numbers\":[1e999]"), json + "x" };
            foreach (var variant in variants)
            {
                Assert.That(variant, Is.Not.EqualTo(json), "Invalid test mutation must actually change input.");
                PayloadAsset.ConstructorCalls = 0;
                var error = Assert.Throws<InvalidDataException>(() => Read(variant));
                Assert.That(error.Message, Is.Not.Empty);
                Assert.That(PayloadAsset.ConstructorCalls, Is.Zero);
            }
            Assert.Throws<InvalidDataException>(() => Read("{\"format\":\"actioneditor.json\",\"version\":1,\"document\":null}"));
            var source = new PayloadAsset { Payload = new Values { Numbers = new[] { float.NaN } } };
            Assert.Throws<InvalidDataException>(() => Text(source)); source.Payload.Numbers[0] = float.PositiveInfinity;
            Assert.Throws<InvalidDataException>(() => Text(source));
        }
        [Test] public void SnapshotCallbacksAndAuthorContentNeverMutateSourceOrDropWorkspace()
        {
            var source = new PayloadAsset(); var group = new CallbackGroup(); source.AddRootContainer(group); source.Init();
            var workspace = new TimelineWorkspace(); workspace.Bind(source); workspace.ViewTimeMax = 7;
            var clone = SequenceCopy.Capture(source, true, Context);
            Assert.That(group.Counter, Is.Zero); Assert.That(((CallbackGroup)clone.RootContainers.OfType<ActionGroup>().ElementAt(0)).Counter, Is.EqualTo(1));
            var before = SequenceCopy.GetContentKey(source, Context);
            workspace.ViewTimeMax = 9; workspace.SetCollapsed(group, true);
            Assert.That(SequenceCopy.GetContentKey(source, Context), Is.EqualTo(before));
            var restored = SequenceCopy.Capture(source, false, Context);
            Assert.That(workspace.ViewTimeMax, Is.EqualTo(9)); Assert.That(workspace.IsCollapsed(group), Is.True);
            Assert.That(Text(restored), Does.Not.Contain("viewTime").And.Not.Contain("isCollapsed"));
            group.Counter = 7; Assert.That(SequenceCopy.GetContentKey(source, Context), Is.Not.EqualTo(before));
        }
        [Test] public void ProductionContextRejectsTestTypesAndUnresolvedTemporaryReferences()
        {
            var source = new PayloadAsset();
            Assert.Throws<InvalidDataException>(() => Format.Serialize(source, default, SequenceFormats.Context));
            var texture = new Texture2D(1, 1);
            try
            {
                source.Payload = new Values { Reference = texture };
                Assert.Throws<InvalidDataException>(() => Text(source));
                Assert.Throws<InvalidDataException>(() => JsonValueConverter.Capture(texture, typeof(Texture2D), new SequenceFormatContext(Array.Empty<Type>())));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        [Test] public void InvalidStructuralGraphAndNegativeRawTimingAreNotRepaired()
        {
            var source = new FeatureSampleSequence(); source.AddRootContainer(source.RootContainers.OfType<ActionGroup>().ElementAt(0));
            Assert.Throws<InvalidDataException>(() => Text(source)); source.RemoveRootContainer(source.RootContainers.Last());
            source.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0].Clips.Add(null); Assert.Throws<InvalidDataException>(() => Text(source));
            var json = Text(new FeatureSampleSequence());
            var mutated = json.Replace("\"startTime\":0.25", "\"startTime\":-1");
            Assert.That(mutated, Is.Not.EqualTo(json)); Assert.Throws<InvalidDataException>(() => Read(mutated));
        }
        [Test] public void ClipboardUsesSameConverterAndPreservesNestedPayload()
        {
            var source = new FeatureSampleSequence(); var track = source.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0];
            var restored = (ActionTrack)SequenceCopy.RestoreFragment(track.GetType(), SequenceCopy.CaptureFragment(track));
            Assert.That(restored.Clips.Count, Is.EqualTo(3));
            Assert.That(JsonValueConverter.Capture(restored, typeof(ActionTrack), Context).ContentKey(), Is.EqualTo(JsonValueConverter.Capture(track, typeof(ActionTrack), Context).ContentKey()));
        }
        [Test] public void NestedValuesPolymorphicFieldsAndBasePrivateMembersUseOneContract()
        {
            var source = new PayloadAsset { Payload = new Values { Nested = new Nested { Number = float.Epsilon, Count = 7 }, Polymorphic = new DerivedBusiness { Text = "nested" }, Nullable = new List<string>() } };
            var loaded = (PayloadAsset)Read(Text(source));
            Assert.That(Bits(loaded.Payload.Nested.Number), Is.EqualTo(1));
            Assert.That(loaded.Payload.Nested.Count, Is.EqualTo(7));
            Assert.That(loaded.Payload.Nullable, Is.Empty);
            Assert.That(loaded.Payload.Polymorphic, Is.TypeOf<DerivedBusiness>());
            Assert.That(Bits(loaded.Payload.Polymorphic.BaseValue), Is.EqualTo(Bits(.1f)));
            Assert.That(((DerivedBusiness)loaded.Payload.Polymorphic).Text, Is.EqualTo("nested"));
        }
        [Test] public void UnknownEnumsUnsupportedContainersAndFieldCollisionsFailExplicitly()
        {
            Assert.Throws<InvalidDataException>(() => JsonValueConverter.Capture(new Dictionary<string, int>(), typeof(Dictionary<string, int>), Context));
            Assert.Throws<InvalidDataException>(() => JsonValueConverter.Capture(new Collision(), typeof(Collision), SequenceFormats.CreateContext(typeof(Collision))));
            Assert.Throws<InvalidDataException>(() => JsonValueConverter.Capture((FeatureSampleDirection)90, typeof(FeatureSampleDirection), Context));
            var json = Text(new FeatureSampleSequence());
            foreach (var variant in new[] { json.Replace("\"Direction\":0", "\"Direction\":99"), json.Replace("\"rootItems\":[", "\"$id\":1,\"rootItems\":["), json.Replace("\"startTime\":0.25", "\"startTime\":3.4028236e38") })
            {
                Assert.That(variant, Is.Not.EqualTo(json)); Assert.Throws<InvalidDataException>(() => Read(variant));
            }
        }
        private sealed class ExportOnly : ISequenceFormat
        {
            public string Id => "tests.export";
            public string DisplayName => "Test export only";
            public string Extension => ".test";
            public bool CanRead => false; public bool CanWrite => true; public bool CanExport => true;
            public byte[] Serialize(ActionSequence asset, SequenceFormatOptions options, SequenceFormatContext context) => throw new NotSupportedException();
            public ActionSequence Deserialize(byte[] bytes, SequenceFormatContext context) => throw new NotSupportedException();
        }
        [Test] public void RegistryIsAtomicAndReadWriteExportCapabilitiesAreIndependent()
        {
            Assert.That(SequenceFormats.Registry.Editable.Select(f => f.Id), Is.EqualTo(new[] { "actioneditor.json" }));
            Assert.That(SequenceFormats.Registry.Exportable.Select(f => f.DisplayName), Is.EqualTo(new[] { "JSON (.json)" }));
            Assert.That(SequenceFormats.Registry.EditablePath("Assets/X.JSON").Id, Is.EqualTo("actioneditor.json"));
            Assert.Throws<InvalidDataException>(() => new SequenceFormatRegistry(new ISequenceFormat[] { Format, new SequenceJsonFormat() }));
            var registry = new SequenceFormatRegistry(new ISequenceFormat[] { Format, new ExportOnly() });
            Assert.That(registry.Editable.Count, Is.EqualTo(1)); Assert.That(registry.Exportable.Count, Is.EqualTo(2));
            Assert.Throws<InvalidDataException>(() => registry.EditablePath("Assets/X.test"));
            Assert.That(SequenceFormats.Registry.Editable.Count, Is.EqualTo(1));
        }
        [Test] public void StoredSignalDurationAndInitializationChangesCannotBeSilentlyRepaired()
        {
            var source = new FeatureSampleSequence();
            var marker = source.RootContainers.OfType<ActionGroup>().SelectMany(g => g.Tracks).SelectMany(t => t.Clips).OfType<PointClip>().First();
            var length = typeof(ActionClip).GetField("length", BindingFlags.Instance | BindingFlags.NonPublic);
            length.SetValue(marker, -1f);
            Assert.That(marker.Length, Is.Zero);
            Assert.Throws<InvalidDataException>(() => Text(source));
            var candidate = new PayloadAsset(); candidate.AddRootContainer(new RewritingGroup());
            var context = SequenceFormats.CreateContext(typeof(PayloadAsset), typeof(RewritingGroup));
            var bytes = Format.Serialize(candidate, default, context);
            Assert.Throws<InvalidDataException>(() => Format.Deserialize(bytes, context));
            Assert.That(((RewritingGroup)candidate.RootContainers.OfType<ActionGroup>().ElementAt(0)).Counter, Is.Zero);
        }
        [Test] public void ReadbackComparesRawSnapshotRatherThanTwoNormalizedCopies()
        {
            var source = new FeatureSampleSequence();
            var json = SequenceJsonCodec.CreateSaveSnapshot(source);
            var tree = JsonText.Parse(json);
            var root = tree.Members["document"].Members.ToDictionary(p => p.Key, p => p.Value);
            root["length"] = JsonValue.Number("99");
            var envelope = tree.Members.ToDictionary(p => p.Key, p => p.Value);
            envelope["document"] = JsonValue.Object(root);
            var mutated = JsonText.Print(JsonValue.Object(envelope), true);
            var loaded = SequenceJsonCodec.Decode(mutated);
            Assert.That(loaded.Length, Is.EqualTo(source.Length));
            Assert.That(SequenceJsonCodec.ReadbackDifferences(mutated, loaded), Is.EqualTo(new[] { "$.length (value)" }));
        }
    }
}
