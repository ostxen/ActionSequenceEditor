using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Infrastructure.Assets;
using NBC.ActionEditor.Views.IMGUI;
using NBC.ActionEditor.Views.IMGUI.Controls;
using NBC.ActionEditor.Views.IMGUI.Properties;
using NBC.ActionEditor.Views.IMGUI.Properties.Standard;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitCanvas = NBC.ActionEditor.Views.UIToolkit.TimelineCanvas;
using ToolkitProperties = NBC.ActionEditor.Views.UIToolkit.PropertyView;
using ToolkitPreferences = NBC.ActionEditor.Views.UIToolkit.PreferencesWindow;
using NBC.ActionEditor.Domain;
using System;
using System.Linq;
using NBC.ActionEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class TypeCatalogTests
    {
        private static InspectorsBase Inspector(object value)
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            return EditorExtensionFactory.GetInspector(presenter.CaptureTarget(value), presenter);
        }
        private class BaseAsset : ActionDocument { }
        private sealed class ChildAsset : BaseAsset { }
        [CustomHeader(typeof(BaseAsset))] private class ExactHeader : HeaderBase { public override void OnGUI(EditorExtensionContext context) { } }
        [CustomHeader(typeof(BaseAsset), true)] private class InheritedHeader : HeaderBase { public override void OnGUI(EditorExtensionContext context) { } }
        private sealed class DerivedHeader : InheritedHeader { }
        [CustomHeader(typeof(BaseAsset), true)] private sealed class ConflictingHeader : HeaderBase { public override void OnGUI(EditorExtensionContext context) { } }
        [CustomInspectors(typeof(TimelineClip), false)] private class ExactInspector : InspectorsBase { }
        [CustomInspectors(typeof(TimelineClip), true)] private class InheritedInspector : InspectorsBase { }
        [CustomInspectors(typeof(PointClip), true)] private sealed class SignalInspector : InspectorsBase { }
        [Unique, Attachable(typeof(FeatureSampleGroup))] private sealed class UniqueTrack : TimelineTrack { }
        private sealed class DisabledBlend : CrossBlendClip
        {
            public override float Length { get => length; set => length = value; }
            public override float BlendIn { get => -1; set { } }
        }
        [Serializable] public sealed class TestDocument : ActionDocument
        { public static int Constructors; public TestDocument() { Constructors++; } }
        [ToolkitHeader(typeof(BaseAsset), true)] private sealed class NativeHeader : ToolkitHeaderView
        { public override VisualElement CreateGUI(EditorExtensionContext context) => new Label("native"); }
        [ToolkitProperties(typeof(BaseAsset))] private class ExactProperties : ToolkitPropertyExtension
        { public override void Populate(VisualElement root, NBC.ActionEditor.Presentation.Properties.PropertyTargetState target, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh) => root.Add(new Label("exact")); }
        [ToolkitProperties(typeof(BaseAsset), true)] private class InheritedProperties : ToolkitPropertyExtension
        { public override void Populate(VisualElement root, NBC.ActionEditor.Presentation.Properties.PropertyTargetState target, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh) => root.Add(new Label("inherited")); }
        private sealed class DerivedProperties : InheritedProperties { }
        [ToolkitProperties(typeof(BaseAsset), true)] private sealed class ConflictingProperties : ToolkitPropertyExtension
        { public override void Populate(VisualElement root, NBC.ActionEditor.Presentation.Properties.PropertyTargetState target, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh) { } }

        [Test] public void NativePropertyBindingsAreIndependentExactOrExplicitlyInheritedAndRejectConflicts()
        {
            var binding = typeof(ToolkitPropertiesAttribute);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactProperties), typeof(InheritedHeader) }, typeof(BaseAsset), binding), Is.EqualTo(typeof(ExactProperties)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactProperties) }, typeof(ChildAsset), binding), Is.Null);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(InheritedProperties), typeof(DerivedProperties) }, typeof(ChildAsset), binding), Is.EqualTo(typeof(DerivedProperties)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(DerivedProperties), typeof(InheritedProperties) }, typeof(ChildAsset), binding), Is.EqualTo(typeof(DerivedProperties)));
            var error = Assert.Throws<InvalidOperationException>(() => ExtensionResolver.ResolveExtension(new[] { typeof(InheritedProperties), typeof(ConflictingProperties) }, typeof(ChildAsset), binding));
            Assert.That(error.Message, Does.Contain(typeof(InheritedProperties).FullName).And.Contain(typeof(ConflictingProperties).FullName));
            Assert.That(ActionEditorTypeCatalog.DerivedFrom(typeof(ToolkitPropertyExtension)).Any(i => i.type == typeof(ExactProperties)), Is.False);
        }

        [Test] public void ProductDiscoveryAndSaveTypesExcludeTestsWithoutCallingConstructors()
        {
            var calls = TestDocument.Constructors;
            Assert.That(ActionEditorTypeCatalog.DerivedFrom(typeof(ActionDocument)).Any(i => i.type == typeof(TestDocument)), Is.False);
            var creation = new Infrastructure.Documents.DocumentDialogCatalog();
            Assert.That(creation.CreationTypes().Contains(typeof(TestDocument)), Is.False);
            Assert.That(creation.CreationTypes(), Does.Contain(typeof(FeatureSampleDocument)));
            Assert.Throws<System.IO.InvalidDataException>(() => NBC.ActionEditor.Infrastructure.Serialization.DocumentFormats.Context.RequireType(typeof(TestDocument), "$"));
            Assert.That(TestDocument.Constructors, Is.EqualTo(calls));
            Assert.That(typeof(PropertyDescription).Assembly, Is.SameAs(typeof(ActionDocument).Assembly));
            Assert.That(typeof(CustomHeader).Assembly, Is.SameAs(typeof(EditorExtensionFactory).Assembly));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(NativeHeader), typeof(InheritedHeader) }, typeof(ChildAsset), typeof(ToolkitHeaderAttribute)), Is.EqualTo(typeof(NativeHeader)));
            Assert.That(EditorExtensionFactory.GetToolkitHeader(typeof(FeatureSampleDocument)), Is.TypeOf<Samples.FeatureSampleToolkitHeader>());
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID("Assets/ActionEditor/Samples/FeatureSample/README.md"), Is.EqualTo("30542892b6cdb57489102ba2218a9a47"));
        }

        [Test] public void AuthorSessionAndSelectionHaveNoConcreteWindowOrInspectorOwnership()
        {
            var fields = typeof(NBC.ActionEditor.Application.Documents.DocumentSession).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(fields.Any(field => typeof(UnityEditor.EditorWindow).IsAssignableFrom(field.FieldType)
                || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(typeof(ActionEditorHost).GetMethod("ConfirmDocumentSwitch"), Is.Null);
            Assert.That(typeof(ActionEditorHost).GetMethod("OnObjectPickerConfig"), Is.Null);
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

        [Test] public void ResourcesAndExtensionsResolveCurrentTypes()
        {
            const string ui = "Assets/ActionEditor/Editor/Views/UIToolkit/";
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + typeof(ToolkitWindow).Name + ".cs"), Is.EqualTo("bd2bd6eded8ebf14687952b313d079d9"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + typeof(ToolkitCanvas).Name + ".cs"), Is.EqualTo("f7548d331bc116c4ba695eb4b55bdf0f"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + typeof(ToolkitProperties).Name + ".cs"), Is.EqualTo("947256fa11135cc43b04ba402a1f03c9"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + typeof(ToolkitPreferences).Name + ".cs"), Is.EqualTo("69c0eced166ee5a45ad033187ae1070e"));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + typeof(ToolkitWindow).Name + ".cs").GetClass(), Is.EqualTo(typeof(ToolkitWindow)));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + typeof(ToolkitPreferences).Name + ".cs").GetClass(), Is.EqualTo(typeof(ToolkitPreferences)));
            const string imgui = "Assets/ActionEditor/Editor/Views/IMGUI/";
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(imgui + "CreateDocumentWindow.cs"), Is.EqualTo("1a368b48e50f45e797eb364d1bff797f"));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(imgui + "CreateDocumentWindow.cs").GetClass(), Is.EqualTo(typeof(CreateDocumentWindow)));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(imgui + "Properties/Standard/DocumentInspector.cs"), Is.EqualTo("1d716975a9a545de861fefb88b2c17a6"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(imgui + "Properties/Standard/ClipInspector.cs"), Is.EqualTo("1d4a2bbcab364421fbac5f6dbe2fd18d"));
            Assert.That(Inspector(new FeatureSampleDocument()), Is.TypeOf<DocumentInspector>());
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ActionEditor/Editor/Views/UIToolkit/TimelineCanvas.uss"), Is.Not.Null);
            Assert.That(Resources.Load<GUISkin>("nbc/StyleSheet"), Is.Not.Null);
            Assert.That(Styles.WhiteTexture, Is.Not.Null);
            Assert.That(Styles.PlayIcon, Is.Not.Null);
            foreach (var field in typeof(Styles).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(field => field.FieldType == typeof(Texture2D)))
                Assert.That(field.GetValue(null), Is.Not.Null, field.Name + "必须绑定实际纹理。");
            Assert.That(EditorExtensionFactory.GetHeader(typeof(FeatureSampleDocument)), Is.Not.Null);
            Assert.That(Inspector(new FeatureSampleInterval()), Is.TypeOf<ClipInspector>());
            Assert.That(typeof(Composition.PreferencesComposition).Assembly, Is.SameAs(typeof(Composition.WindowBinding).Assembly));
            Assert.That(typeof(ActionDocument).Assembly.GetReferencedAssemblies().Any(a => a.Name == typeof(Composition.WindowBinding).Assembly.GetName().Name), Is.False);
        }

        [Test] public void BindingsRespectExactOptInNearestBaseAndDiagnoseBothSources()
        {
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactHeader) }, typeof(BaseAsset), false), Is.EqualTo(typeof(ExactHeader)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactHeader) }, typeof(ChildAsset), false), Is.Null);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(InheritedHeader), typeof(DerivedHeader) }, typeof(ChildAsset), false), Is.EqualTo(typeof(DerivedHeader)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactInspector) }, typeof(FeatureSampleMarker), true), Is.Null);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(InheritedInspector), typeof(SignalInspector) }, typeof(FeatureSampleMarker), true), Is.EqualTo(typeof(SignalInspector)));
            var error = Assert.Throws<InvalidOperationException>(() => ExtensionResolver.ResolveExtension(new[] { typeof(InheritedHeader), typeof(ConflictingHeader) }, typeof(ChildAsset), false));
            Assert.That(error.Message, Does.Contain(typeof(InheritedHeader).FullName).And.Contain(typeof(ConflictingHeader).FullName));
        }
        [Test] public void MetadataCachePreservesMenuOrderAndDoesNotExposeMutableParents()
        {
            var original = ReflectionTools.GetImplementationsOf(typeof(TimelineTrack)).Where(t => Infrastructure.Assets.ProductTypes.Includes(t) && !t.ContainsGenericParameters && !t.IsDefined(typeof(ObsoleteAttribute), true))
                .Select(t => (type: t, name: t.RTGetAttribute<NameAttribute>(true)?.name ?? t.Name.SplitCamelCase(), category: t.RTGetAttribute<CategoryAttribute>(true)?.category))
                .OrderBy(t => t.name).ThenBy(t => t.category).Select(t => t.type).ToArray();
            var actual = ActionEditorTypeCatalog.DerivedFrom(typeof(TimelineTrack));
            Assert.That(actual.Select(i => i.type), Is.EqualTo(original));
            var item = actual.Single(i => i.type == typeof(FeatureSampleIntervalTrack));
            item.attachableTypes[0] = typeof(ActionDocument); actual.Clear();
            Assert.That(ActionEditorTypeCatalog.DerivedFrom(typeof(TimelineTrack)).Single(i => i.type == typeof(FeatureSampleIntervalTrack)).attachableTypes, Is.EqualTo(new[] { typeof(FeatureSampleGroup) }));
            var info = DirectableTypeInfo.For(typeof(FeatureSampleIntervalTrack));
            Assert.That(info, Is.SameAs(DirectableTypeInfo.For(typeof(FeatureSampleIntervalTrack))));
            Assert.That(info.Color, Is.EqualTo(new Color(.3f, .65f, 1)));
        }
        [Test] public void CapabilitiesRemainIndependentOfLocksAndRespectSignalsAndBlendSentinels()
        {
            var asset = new FeatureSampleDocument();
            var track = asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            Assert.That(clip.CanScale() && clip.CanBlendIn() && clip.CanBlendOut(), Is.True);
            track.IsLocked = true; track.IsActive = false;
            Assert.That(clip.CanScale() && clip.CanBlendIn(), Is.True);
            var marker = asset.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[2].Clips[0];
            Assert.That(DirectableTypeInfo.For(marker.GetType()).IsSignal, Is.True);
            Assert.That(marker.CanScale() || marker.CanBlendIn(), Is.False);
            Assert.That(new DisabledBlend().CanBlendIn(), Is.False);
            var disabled = new DisabledBlend();
            ClipTimingRules.ApplyTiming(disabled, 0, .01f);
            Assert.That(disabled.BlendIn, Is.EqualTo(-1));
            var cached = new FeatureSampleInterval { Length = 1, BlendIn = .25f, BlendOut = .25f };
            cached.SetCrossBlendIn(.4f); cached.SetCrossBlendOut(.3f);
            ClipTimingRules.ApplyTiming(cached, .5f, cached.Length);
            cached.SetCrossBlendIn(0); cached.SetCrossBlendOut(0);
            Assert.That(cached.BlendIn, Is.EqualTo(.25f));
            Assert.That(cached.BlendOut, Is.EqualTo(.25f));
            cached.SetCrossBlendIn(.4f); cached.SetCrossBlendOut(.3f);
            ClipTimingRules.ApplyTiming(cached, .5f, .2f);
            Assert.That(cached.BlendIn, Is.EqualTo(.4f));
            Assert.That(cached.BlendOut, Is.EqualTo(.3f));
            cached.SetCrossBlendIn(0); cached.SetCrossBlendOut(0);
            Assert.That(cached.BlendIn, Is.EqualTo(.2f));
            Assert.That(cached.BlendOut, Is.Zero);
            Assert.That(Inspector(clip), Is.TypeOf<ClipInspector>());
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var root = new VisualElement(); new ToolkitProperties(presenter).Populate(root, presenter.CaptureTarget(clip), () => { });
            Assert.That(root.Q("timeline-clip-timing"), Is.Not.Null);
        }
        [Test] public void ParentAndUniqueConstraintsRemainExactAndInstanceAware()
        {
            var group = new FeatureSampleGroup();
            Assert.That(DirectableTypeInfo.For(typeof(UniqueTrack)).IsUnique, Is.True);
            Assert.That(group.CanAddTrackOfType(typeof(UniqueTrack)), Is.True);
            group.Tracks.Add(new UniqueTrack());
            Assert.That(group.CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(new TimelineGroup().CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(group.CanAddTrackOfType(typeof(TimelineTrack)), Is.False);
        }
        [Test] public void NeighborPolicyKeepsPropertyCrossBlendSeparateFromStrictEdges()
        {
            var clip = new FeatureSampleInterval { StartTime = 2, Length = 2 };
            var previous = new FeatureSampleInterval { StartTime = 0, Length = 2 };
            var next = new FeatureSampleInterval { StartTime = 4, Length = 4 };
            Assert.That(ClipTimingRules.NeighborBounds(clip, previous, next, true), Is.EqualTo(new Vector2(1, 5)));
            Assert.That(ClipTimingRules.NeighborBounds(clip, previous, next, false), Is.EqualTo(new Vector2(2, 4)));
            Assert.That(ClipTimingRules.NeighborBounds(new FeatureSampleMarker(), previous, next, true), Is.EqualTo(new Vector2(2, 4)));
            Assert.That(ClipTimingRules.NeighborBounds(clip, null, null, true).x, Is.Zero);
        }
    }
}
