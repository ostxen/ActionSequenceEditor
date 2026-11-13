using NBC.ActionEditor.Domain;
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
        private class BaseAsset : ActionDocument { }
        private sealed class ChildAsset : BaseAsset { }
        [CustomHeader(typeof(BaseAsset))] private class ExactHeader : HeaderBase { public override void OnGUI() { } }
        [CustomHeader(typeof(BaseAsset), true)] private class InheritedHeader : HeaderBase { public override void OnGUI() { } }
        private sealed class DerivedHeader : InheritedHeader { }
        [CustomHeader(typeof(BaseAsset), true)] private sealed class ConflictingHeader : HeaderBase { public override void OnGUI() { } }
        [CustomInspectors(typeof(TimelineClip), false)] private class ExactInspector : InspectorsBase { }
        [CustomInspectors(typeof(TimelineClip), true)] private class InheritedInspector : InspectorsBase { }
        [CustomInspectors(typeof(PointClip), true)] private sealed class SignalInspector : InspectorsBase { }
        [Unique, Attachable(typeof(LegacyExperienceGroup))] private sealed class UniqueTrack : TimelineTrack { }
        private sealed class DisabledBlend : CrossBlendClip { public override float BlendIn { get => -1; set { } } }

        [Test] public void SharedFacadeAndSelectionHaveNoConcreteWindowOrInspectorOwnership()
        {
            var fields = typeof(App).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(fields.Any(field => typeof(UnityEditor.EditorWindow).IsAssignableFrom(field.FieldType)
                || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(typeof(App).GetMethod("ConfirmDocumentSwitch"), Is.Null);
            Assert.That(typeof(App).GetMethod("OnObjectPickerConfig"), Is.Null);
            Assert.That(typeof(EditorTools).GetNestedType("TypeMetaInfo"), Is.Null);
            var selection = new NBC.ActionEditor.Application.Editing.SelectionState();
            var requests = 0; selection.InspectorRequested += () => requests++;
            var item = new TimelineGroup();
            selection.Set(new IDirectable[] { item }, false);
            Assert.That(requests, Is.Zero);
            selection.Set(Array.Empty<IDirectable>(), false);
            selection.Set(new IDirectable[] { item }, true);
            selection.Set(new IDirectable[] { item }, true);
            Assert.That(requests, Is.EqualTo(1));
            selection.Set(null, true);
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test] public void RelocatedStylesAndExtensionAdaptersKeepTheirResourceAndTypeBindings()
        {
            const string ui = "Assets/ActionEditor/Editor/UI/UIToolkit/";
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + nameof(UIToolkitActionEditorWindow) + ".cs"), Is.EqualTo("bd2bd6eded8ebf14687952b313d079d9"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + nameof(UIToolkitTimelineCanvas) + ".cs"), Is.EqualTo("f7548d331bc116c4ba695eb4b55bdf0f"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + nameof(UIToolkitPropertyView) + ".cs"), Is.EqualTo("947256fa11135cc43b04ba402a1f03c9"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + nameof(UIToolkitPreferencesWindow) + ".cs"), Is.EqualTo("69c0eced166ee5a45ad033187ae1070e"));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + nameof(UIToolkitActionEditorWindow) + ".cs").GetClass(), Is.EqualTo(typeof(UIToolkitActionEditorWindow)));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + nameof(UIToolkitPreferencesWindow) + ".cs").GetClass(), Is.EqualTo(typeof(UIToolkitPreferencesWindow)));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ActionEditor/Editor/Workbench/JsonTimelineCanvas.uss"), Is.Not.Null);
            Assert.That(Resources.Load<GUISkin>("nbc/StyleSheet"), Is.Not.Null);
            Assert.That(Styles.WhiteTexture, Is.Not.Null);
            Assert.That(Styles.PlayIcon, Is.Not.Null);
            Assert.That(EditorCustomFactory.GetHeader(new LegacyExperienceAsset()), Is.Not.Null);
            Assert.That(EditorCustomFactory.GetInspector(new LegacyExperienceInterval()), Is.TypeOf<ActionClipInspector>());
            Assert.That(typeof(Prefs).Assembly, Is.SameAs(typeof(App).Assembly));
            Assert.That(typeof(ActionDocument).Assembly.GetReferencedAssemblies().Any(a => a.Name == typeof(App).Assembly.GetName().Name), Is.False);
        }

        [Test] public void BindingsRespectExactOptInNearestBaseAndDiagnoseBothSources()
        {
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactHeader) }, typeof(BaseAsset), false), Is.EqualTo(typeof(ExactHeader)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactHeader) }, typeof(ChildAsset), false), Is.Null);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(InheritedHeader), typeof(DerivedHeader) }, typeof(ChildAsset), false), Is.EqualTo(typeof(DerivedHeader)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactInspector) }, typeof(LegacyExperienceMarker), true), Is.Null);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(InheritedInspector), typeof(SignalInspector) }, typeof(LegacyExperienceMarker), true), Is.EqualTo(typeof(SignalInspector)));
            var error = Assert.Throws<InvalidOperationException>(() => ExtensionResolver.ResolveExtension(new[] { typeof(InheritedHeader), typeof(ConflictingHeader) }, typeof(ChildAsset), false));
            Assert.That(error.Message, Does.Contain(typeof(InheritedHeader).FullName).And.Contain(typeof(ConflictingHeader).FullName));
        }
        [Test] public void MetadataCachePreservesMenuOrderAndDoesNotExposeMutableParents()
        {
            var original = ReflectionTools.GetImplementationsOf(typeof(TimelineTrack)).Where(t => !t.ContainsGenericParameters && !t.IsDefined(typeof(ObsoleteAttribute), true))
                .Select(t => (type: t, name: t.RTGetAttribute<NameAttribute>(true)?.name ?? t.Name.SplitCamelCase(), category: t.RTGetAttribute<CategoryAttribute>(true)?.category))
                .OrderBy(t => t.name).ThenBy(t => t.category).Select(t => t.type).ToArray();
            var actual = ActionEditorTypeCatalog.DerivedFrom(typeof(TimelineTrack));
            Assert.That(actual.Select(i => i.type), Is.EqualTo(original));
            var item = actual.Single(i => i.type == typeof(LegacyIntervalTrack));
            item.attachableTypes[0] = typeof(ActionDocument); actual.Clear();
            Assert.That(ActionEditorTypeCatalog.DerivedFrom(typeof(TimelineTrack)).Single(i => i.type == typeof(LegacyIntervalTrack)).attachableTypes, Is.EqualTo(new[] { typeof(LegacyExperienceGroup) }));
            var info = DirectableTypeInfo.For(typeof(LegacyIntervalTrack));
            Assert.That(info, Is.SameAs(DirectableTypeInfo.For(typeof(LegacyIntervalTrack))));
            Assert.That(info.Color, Is.EqualTo(new Color(.3f, .65f, 1)));
        }
        [Test] public void CapabilitiesRemainIndependentOfLocksAndRespectSignalsAndBlendSentinels()
        {
            var asset = new LegacyExperienceAsset();
            var track = asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            Assert.That(clip.CanScale() && clip.CanBlendIn() && clip.CanBlendOut(), Is.True);
            track.IsLocked = true; track.IsActive = false;
            Assert.That(clip.CanScale() && clip.CanBlendIn(), Is.True);
            var marker = asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[2].Clips[0];
            Assert.That(DirectableTypeInfo.For(marker.GetType()).IsSignal, Is.True);
            Assert.That(marker.CanScale() || marker.CanBlendIn(), Is.False);
            Assert.That(new DisabledBlend().CanBlendIn(), Is.False);
            Assert.That(EditorCustomFactory.GetInspector(clip), Is.TypeOf<ActionClipInspector>());
            var root = new VisualElement(); UIToolkitPropertyView.Populate(root, clip, () => { });
            Assert.That(root.Q("json-clip-timing"), Is.Not.Null);
        }
        [Test] public void ParentAndUniqueConstraintsRemainExactAndInstanceAware()
        {
            var group = new LegacyExperienceGroup();
            Assert.That(DirectableTypeInfo.For(typeof(UniqueTrack)).IsUnique, Is.True);
            Assert.That(group.CanAddTrackOfType(typeof(UniqueTrack)), Is.True);
            group.Tracks.Add(new UniqueTrack());
            Assert.That(group.CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(new TimelineGroup().CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(group.CanAddTrackOfType(typeof(TimelineTrack)), Is.False);
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
