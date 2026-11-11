using System;
using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class TypeCatalogTests
    {
        private class BaseAsset : Asset { }
        private sealed class ChildAsset : BaseAsset { }
        [CustomHeader(typeof(BaseAsset))] private class ExactHeader : HeaderBase { public override void OnGUI() { } }
        [CustomHeader(typeof(BaseAsset), true)] private class InheritedHeader : HeaderBase { public override void OnGUI() { } }
        private sealed class DerivedHeader : InheritedHeader { }
        [CustomHeader(typeof(BaseAsset), true)] private sealed class ConflictingHeader : HeaderBase { public override void OnGUI() { } }
        [CustomInspectors(typeof(Clip), false)] private class ExactInspector : InspectorsBase { }
        [CustomInspectors(typeof(Clip), true)] private class InheritedInspector : InspectorsBase { }
        [CustomInspectors(typeof(ClipSignal), true)] private sealed class SignalInspector : InspectorsBase { }
        [Unique, Attachable(typeof(LegacyExperienceGroup))] private sealed class UniqueTrack : Track { }
        private sealed class DisabledBlend : ClipCrossBlend { public override float BlendIn { get => -1; set { } } }

        [Test] public void BindingsRespectExactOptInNearestBaseAndDiagnoseBothSources()
        {
            Assert.That(EditorCustomFactory.ResolveExtension(new[] { typeof(ExactHeader) }, typeof(BaseAsset), false), Is.EqualTo(typeof(ExactHeader)));
            Assert.That(EditorCustomFactory.ResolveExtension(new[] { typeof(ExactHeader) }, typeof(ChildAsset), false), Is.Null);
            Assert.That(EditorCustomFactory.ResolveExtension(new[] { typeof(InheritedHeader), typeof(DerivedHeader) }, typeof(ChildAsset), false), Is.EqualTo(typeof(DerivedHeader)));
            Assert.That(EditorCustomFactory.ResolveExtension(new[] { typeof(ExactInspector) }, typeof(LegacyExperienceMarker), true), Is.Null);
            Assert.That(EditorCustomFactory.ResolveExtension(new[] { typeof(InheritedInspector), typeof(SignalInspector) }, typeof(LegacyExperienceMarker), true), Is.EqualTo(typeof(SignalInspector)));
            var error = Assert.Throws<InvalidOperationException>(() => EditorCustomFactory.ResolveExtension(new[] { typeof(InheritedHeader), typeof(ConflictingHeader) }, typeof(ChildAsset), false));
            Assert.That(error.Message, Does.Contain(typeof(InheritedHeader).FullName).And.Contain(typeof(ConflictingHeader).FullName));
        }
        [Test] public void MetadataCachePreservesMenuOrderAndDoesNotExposeMutableParents()
        {
            var original = ReflectionTools.GetImplementationsOf(typeof(Track)).Where(t => !t.ContainsGenericParameters && !t.IsDefined(typeof(ObsoleteAttribute), true))
                .Select(t => (type: t, name: t.RTGetAttribute<NameAttribute>(true)?.name ?? t.Name.SplitCamelCase(), category: t.RTGetAttribute<CategoryAttribute>(true)?.category))
                .OrderBy(t => t.name).ThenBy(t => t.category).Select(t => t.type).ToArray();
            var actual = EditorTools.GetTypeMetaDerivedFrom(typeof(Track));
            Assert.That(actual.Select(i => i.type), Is.EqualTo(original));
            var item = actual.Single(i => i.type == typeof(LegacyIntervalTrack));
            item.attachableTypes[0] = typeof(Asset); actual.Clear();
            Assert.That(EditorTools.GetTypeMetaDerivedFrom(typeof(Track)).Single(i => i.type == typeof(LegacyIntervalTrack)).attachableTypes, Is.EqualTo(new[] { typeof(LegacyExperienceGroup) }));
            var info = DirectableTypeInfo.For(typeof(LegacyIntervalTrack));
            Assert.That(info, Is.SameAs(DirectableTypeInfo.For(typeof(LegacyIntervalTrack))));
            Assert.That(info.Color, Is.EqualTo(new Color(.3f, .65f, 1)));
        }
        [Test] public void CapabilitiesRemainIndependentOfLocksAndRespectSignalsAndBlendSentinels()
        {
            var asset = new LegacyExperienceAsset();
            var track = asset.groups[0].Tracks[0]; var clip = track.Clips[0];
            Assert.That(clip.CanScale() && clip.CanBlendIn() && clip.CanBlendOut(), Is.True);
            track.IsLocked = true; track.IsActive = false;
            Assert.That(clip.CanScale() && clip.CanBlendIn(), Is.True);
            var marker = asset.groups[0].Tracks[2].Clips[0];
            Assert.That(DirectableTypeInfo.For(marker.GetType()).IsSignal, Is.True);
            Assert.That(marker.CanScale() || marker.CanBlendIn(), Is.False);
            Assert.That(new DisabledBlend().CanBlendIn(), Is.False);
            Assert.That(EditorCustomFactory.GetInspector(clip), Is.TypeOf<ActionClipInspector>());
            var root = new VisualElement(); JsonPropertyView.Populate(root, clip, () => { });
            Assert.That(root.Q("json-clip-timing"), Is.Not.Null);
        }
        [Test] public void ParentAndUniqueConstraintsRemainExactAndInstanceAware()
        {
            var group = new LegacyExperienceGroup();
            Assert.That(DirectableTypeInfo.For(typeof(UniqueTrack)).IsUnique, Is.True);
            Assert.That(group.CanAddTrackOfType(typeof(UniqueTrack)), Is.True);
            group.Tracks.Add(new UniqueTrack());
            Assert.That(group.CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(new Group().CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(group.CanAddTrackOfType(typeof(Track)), Is.False);
        }
        [Test] public void NeighborPolicyKeepsPropertyCrossBlendSeparateFromStrictEdges()
        {
            var clip = new LegacyExperienceInterval { StartTime = 2, Length = 2 };
            var previous = new LegacyExperienceInterval { StartTime = 0, Length = 2 };
            var next = new LegacyExperienceInterval { StartTime = 4, Length = 4 };
            Assert.That(ClipTimingRules.NeighborBounds(clip, previous, next, true), Is.EqualTo(new Vector2(1, 5)));
            Assert.That(ClipTimingRules.NeighborBounds(clip, previous, next, false), Is.EqualTo(new Vector2(2, 4)));
            Assert.That(ClipTimingRules.NeighborBounds(new LegacyExperienceMarker(), previous, next, true), Is.EqualTo(new Vector2(2, 4)));
            Assert.That(ClipTimingRules.NeighborBounds(clip, null, null, true).x, Is.Zero);
        }
    }
}
