using ActionSequenceEditor.Presentation.Properties;
using ActionSequenceEditor.Infrastructure.Discovery;
using ActionSequenceEditor.Views.IMGUI;
using ActionSequenceEditor.Views.IMGUI.Controls;
using ActionSequenceEditor.Views.IMGUI.Properties;
using ActionSequenceEditor.Views.IMGUI.Properties.Standard;
using ToolkitWindow = ActionSequenceEditor.Views.UIToolkit.ActionSequenceEditorWindow;
using ToolkitCanvas = ActionSequenceEditor.Views.UIToolkit.TimelineCanvas;
using ToolkitProperties = ActionSequenceEditor.Views.UIToolkit.PropertyFieldsView;
using ToolkitSettings = ActionSequenceEditor.Views.UIToolkit.SettingsWindow;
using ActionSequenceEditor.Model;
using System;
using System.Linq;
using ActionSequenceEditor.Samples.FeatureSample;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Tests
{
    public sealed class TypeCatalogTests
    {
        private static PropertyInspectorBase Inspector(object value)
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            return EditorExtensionFactory.CreateInspector(presenter.CaptureTarget(value), presenter);
        }
        private class BaseAsset : ActionSequence { }
        private sealed class ChildAsset : BaseAsset { }
        [CustomHeader(typeof(BaseAsset))] private class ExactHeader : SequenceHeaderExtension { public override void OnGUI(EditorExtensionContext context) { } }
        [CustomHeader(typeof(BaseAsset), true)] private class InheritedHeader : SequenceHeaderExtension { public override void OnGUI(EditorExtensionContext context) { } }
        private sealed class DerivedHeader : InheritedHeader { }
        [CustomHeader(typeof(BaseAsset), true)] private sealed class ConflictingHeader : SequenceHeaderExtension { public override void OnGUI(EditorExtensionContext context) { } }
        [CustomInspectors(typeof(ActionClip), false)] private class ExactInspector : PropertyInspectorBase { }
        [CustomInspectors(typeof(ActionClip), true)] private class InheritedInspector : PropertyInspectorBase { }
        [CustomInspectors(typeof(PointClip), true)] private sealed class SignalInspector : PropertyInspectorBase { }
        [Unique, Attachable(typeof(FeatureSampleGroup))] private sealed class UniqueTrack : ActionTrack { }
        private sealed class DisabledBlend : CrossBlendClip
        {
            public override float Length { get => length; set => length = value; }
            public override float BlendIn { get => -1; set { } }
        }
        [Serializable] public sealed class TestDocument : ActionSequence
        { public static int Constructors; public TestDocument() { Constructors++; } }
        [ToolkitHeader(typeof(BaseAsset), true)] private sealed class NativeHeader : ToolkitHeaderView
        { public override VisualElement CreateGUI(EditorExtensionContext context) => new Label("native"); }
        [ToolkitProperties(typeof(BaseAsset))] private class ExactProperties : ToolkitPropertyExtension
        { public override void Populate(VisualElement root, ActionSequenceEditor.Presentation.Properties.PropertyTargetState target, ActionSequenceEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh) => root.Add(new Label("exact")); }
        [ToolkitProperties(typeof(BaseAsset), true)] private class InheritedProperties : ToolkitPropertyExtension
        { public override void Populate(VisualElement root, ActionSequenceEditor.Presentation.Properties.PropertyTargetState target, ActionSequenceEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh) => root.Add(new Label("inherited")); }
        private sealed class DerivedProperties : InheritedProperties { }
        [ToolkitProperties(typeof(BaseAsset), true)] private sealed class ConflictingProperties : ToolkitPropertyExtension
        { public override void Populate(VisualElement root, ActionSequenceEditor.Presentation.Properties.PropertyTargetState target, ActionSequenceEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh) { } }

        [Test] public void NativePropertyBindingsAreIndependentExactOrExplicitlyInheritedAndRejectConflicts()
        {
            var binding = typeof(ToolkitPropertiesAttribute);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactProperties), typeof(InheritedHeader) }, typeof(BaseAsset), binding), Is.EqualTo(typeof(ExactProperties)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(ExactProperties) }, typeof(ChildAsset), binding), Is.Null);
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(InheritedProperties), typeof(DerivedProperties) }, typeof(ChildAsset), binding), Is.EqualTo(typeof(DerivedProperties)));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(DerivedProperties), typeof(InheritedProperties) }, typeof(ChildAsset), binding), Is.EqualTo(typeof(DerivedProperties)));
            var error = Assert.Throws<InvalidOperationException>(() => ExtensionResolver.ResolveExtension(new[] { typeof(InheritedProperties), typeof(ConflictingProperties) }, typeof(ChildAsset), binding));
            Assert.That(error.Message, Does.Contain(typeof(InheritedProperties).FullName).And.Contain(typeof(ConflictingProperties).FullName));
            Assert.That(ActionSequenceEditorTypeCatalog.DerivedFrom(typeof(ToolkitPropertyExtension)).Any(i => i.type == typeof(ExactProperties)), Is.False);
        }

        [Test] public void ProductDiscoveryAndSaveTypesExcludeTestsWithoutCallingConstructors()
        {
            var calls = TestDocument.Constructors;
            Assert.That(ActionSequenceEditorTypeCatalog.DerivedFrom(typeof(ActionSequence)).Any(i => i.type == typeof(TestDocument)), Is.False);
            var creation = new Infrastructure.Sequences.SequenceDialogCatalog();
            Assert.That(creation.CreationTypes().Contains(typeof(TestDocument)), Is.False);
            Assert.That(creation.CreationTypes(), Does.Contain(typeof(FeatureSampleSequence)));
            Assert.Throws<System.IO.InvalidDataException>(() => ActionSequenceEditor.Infrastructure.Serialization.SequenceFormats.Context.RequireType(typeof(TestDocument), "$"));
            Assert.That(TestDocument.Constructors, Is.EqualTo(calls));
            Assert.That(typeof(PropertyFieldMetadata).Assembly, Is.SameAs(typeof(ActionSequence).Assembly));
            Assert.That(typeof(CustomHeader).Assembly, Is.SameAs(typeof(EditorExtensionFactory).Assembly));
            Assert.That(ExtensionResolver.ResolveExtension(new[] { typeof(NativeHeader), typeof(InheritedHeader) }, typeof(ChildAsset), typeof(ToolkitHeaderAttribute)), Is.EqualTo(typeof(NativeHeader)));
            Assert.That(EditorExtensionFactory.CreateToolkitHeader(typeof(FeatureSampleSequence)), Is.TypeOf<Samples.FeatureSampleToolkitHeader>());
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID("Assets/ActionSequenceEditor/Samples/FeatureSample/README.md"), Is.EqualTo("30542892b6cdb57489102ba2218a9a47"));
        }

        [Test] public void AuthorSessionAndSelectionHaveNoConcreteWindowOrInspectorOwnership()
        {
            var fields = typeof(ActionSequenceEditor.Application.Sequences.SequenceSession).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(fields.Any(field => typeof(UnityEditor.EditorWindow).IsAssignableFrom(field.FieldType)
                || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(typeof(ActionSequenceEditorHost).GetMethod("ConfirmDocumentSwitch"), Is.Null);
            Assert.That(typeof(ActionSequenceEditorHost).GetMethod("OnObjectPickerConfig"), Is.Null);
            Assert.That(typeof(EditorGUIControls).GetNestedType("TypeMenuInfo"), Is.Null);
            var selection = new ActionSequenceEditor.Application.Editing.SelectionState();
            var requests = 0; selection.InspectorRequested += () => requests++;
            var item = new ActionGroup();
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
            // 固定移动清单逐项保留Unity资源身份，不以同名类型替代GUID验证。
            foreach (var item in new (string path, string guid)[]
            {
                ("Assets/ActionSequenceEditor/Runtime/Serialization/SequenceFormatContext.cs", "2f514e944df4eb94db342ce086773b86"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/SequenceJsonFormat.cs", "f8da1d40121eeb74d94460ef898fde55"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/ISequenceFormat.cs", "a5bb2d996ede4e540bf288314602d92e"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/JsonValueConverter.cs", "c9e750c69b213ee42b957bfb2507ae19"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/JsonValue.cs", "4669f0b1834658746b854f7fde3f80c2"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/JsonText.cs", "4141717fa2459d846810fb154a6c99b7"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/SequenceToolbar.cs", "44fe374f16874682881cb81e7e8ec16f"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/IAssetReferenceResolver.cs", "63549ffbab0d92141b398dde5ff8287e"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/Dialogs/SequenceExportWindow.cs", "98c7a27c628a2054c988fe68fc55f7eb"),
                ("Assets/ActionSequenceEditor/Runtime/Samples/FeatureSample/FeatureSampleSequence.cs", "85564f260125ced47ad7a93282cf1f6c"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/PhysicalCommitFiles.cs", "e2e4ac9e13bc84640b5e33133465e678"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/SequenceFileRepository.cs", "ea268efaef662614588a830855b6edef"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/SequenceNavigation.cs", "524e34b779ac9ee428dce9af433a038f"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/SequenceFactory.cs", "78ee56730b1942d4f8aae40a577cf306"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Preview/SequenceSampler.cs", "787fcb65b6bec4610bc29e3e87a0a869"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/UnityUndoHistory.cs", "12859735a36b4ce4280d685c02aa9d2d"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/SequenceExportService.cs", "2cd0dde7293a590439d2276a69f7cb45"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Preview/SequencePreviewSession.cs", "7aefd6eeb0002a24b916486132ff0f04"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/SequenceDialogCatalog.cs", "22d705571a231c64bbd270616d298a36"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/AtomicFileCommit.cs", "4897c0833f9449d4a8e76f601514b5d6"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Serialization/SequenceFormatRegistry.cs", "874a346eebdfb2d4da181ad060f2cc83"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Serialization/SequenceCopy.cs", "0f3dafad6f5870c45ab4b0007787740b"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Serialization/SequenceJsonCodec.cs", "0a4c86a687eab9944a10be8e1631f77b"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Serialization/SequenceFormats.cs", "d4b2c908a74f83147b8ac2d27471bd89"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/Properties/Standard/SequenceInspector.cs", "1d716975a9a545de861fefb88b2c17a6"),
                ("Assets/ActionSequenceEditor/Runtime/ActionSequenceEditor.Runtime.asmdef", "7d31d11ba62f20b4d89380d87c02f0e4"),
                ("Assets/ActionSequenceEditor/Editor/Presentation/Presenters/SequencePresenter.cs", "6ea8ead886aa7f145b4830b3ab0d64f5"),
                ("Assets/ActionSequenceEditor/Editor/Presentation/Presenters/ExtensionSequenceProjection.cs", "8886162a011dceb4b949b9134312ef00"),
                ("Assets/ActionSequenceEditor/Editor/Extensions/SequenceHeaderExtension.cs", "e2faf2ee3510498884548206b4aebbc3"),
                ("Assets/ActionSequenceEditor/Editor/Presentation/Contracts/ExtensionSequenceState.cs", "640b0ceb590daf94aa43897d7015bd37"),
                ("Assets/ActionSequenceEditor/Editor/Presentation/Contracts/SequenceContracts.cs", "54d3fe480a2cc5947a839a725a12a94f"),
                ("Assets/ActionSequenceEditor/Editor/Composition/SequenceComposition.cs", "5fb8957cff254d74a90974dc8fb110b0"),
                ("Assets/ActionSequenceEditor/Runtime/Model/WrapMode.cs", "899bb36d94eb34d76b0a9059af55efad"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ActionTrack.cs", "70fe174422714ce69a4eda6ae2a2eaeb"),
                ("Assets/ActionSequenceEditor/Editor/Application/Ports/SequencePorts.cs", "8cc736330e8c1884583d25b1f21956a2"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ActionContainer.cs", "8aa9d69a014c1824b9d9c3e374a01b91"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ActionGroup.cs", "2a2593f3511d46ed951d5afa7c4e8a2f"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ActionClip.cs", "b8f51f8184a541a0a08fda9e6aa68b9a"),
                ("Assets/ActionSequenceEditor/Runtime/Model/StructureRules.cs", "72ed5ba890853fc4fb8e980e0ed76655"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ISubClipContainable.cs", "e42df59013994ed1a7fb3a76e7859b65"),
                ("Assets/ActionSequenceEditor/Runtime/Model/IDirector.cs", "61b86d8d3ba6b4ef7b13a4c40214cb55"),
                ("Assets/ActionSequenceEditor/Runtime/Model/IDirectableExtensions.cs", "d090c4f69199c4ea3b847d3f5d6f5bcd"),
                ("Assets/ActionSequenceEditor/Runtime/Model/IDirectable.cs", "52142dfa5f334d399b2eb4225b710e94"),
                ("Assets/ActionSequenceEditor/Runtime/Model/IData.cs", "0f5edb0130f94f7ca215fd2f4ac63ab2"),
                ("Assets/ActionSequenceEditor/Editor/Application/Playback/SequencePlayback.cs", "9381285a432d0c148be075e9e0d3daa5"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ClipTimingRules.cs", "0e40cbe967321c146b1c9459ab2ba3ab"),
                ("Assets/ActionSequenceEditor/Runtime/Model/ActionSequence.cs", "a3d1500c02fb459d85ab07dbe606726d"),
                ("Assets/ActionSequenceEditor/Editor/Composition/SequencePresenterComposition.cs", "ca0302d13b411d4449cf60a751c1ec5e"),
                ("Assets/ActionSequenceEditor/Editor/Application/Sequences/SequenceEditTransaction.cs", "0079704cec0dcec4d9ad92b006251f95"),
                ("Assets/ActionSequenceEditor/Editor/Application/Sequences/SequenceAutoSave.cs", "831504f9b20655049a3c8f7c5b59dec4"),
                ("Assets/ActionSequenceEditor/Editor/Application/Sequences/SequenceLifecycle.cs", "be9377a2bb31ce244bc7b4da261e0664"),
                ("Assets/ActionSequenceEditor/Editor/Application/Sequences/SequenceSession.cs", "c2f2193dab98b9541a50c6367710e2e3"),
                ("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/Dialogs/SequenceCreationWindow.cs", "f0c055b5b34d5594dbb4ab984706ca6e"),
                ("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/Dialogs/SequenceExportWindow.cs", "d060bb1e28f5c1a448d573c2e9bfd4b9"),
                ("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/Dialogs/SequencePickerWindow.cs", "023449fa797e098479c9c0a4d6204f9e"),
                ("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/Dialogs/RenameWindow.cs", "dbe22f972bbba1343b7f9a44adc55944"),
                ("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/Dialogs/DialogWindowBase.cs", "195c9c9cd4cbb494a8af9421b80413d2"),
                ("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/Dialogs/EditorDialogAdapter.cs", "ea6b8b81babda174fab6e757ca214568"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/Dialogs/SequenceCreationWindow.cs", "1a368b48e50f45e797eb364d1bff797f"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/Dialogs/AssetPickerWindow.cs", "f7f5f91a03634e98ac7aff43da23fb15"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/Dialogs/RenameWindow.cs", "58a9ae359aabde84fac0ca5be5a4ffb9"),
                ("Assets/ActionSequenceEditor/Editor/Views/IMGUI/Dialogs/EditorDialogAdapter.cs", "ddd3c9bda412f7340aa738ca3f45c644"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Dialogs/IEditorDialogs.cs", "ef4f2891356172c4687a9d6fa3aaf95d"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Dialogs/SequenceCreationPrompt.cs", "e9572aa3c1e1dea4b81f9a2473dc6f16"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Dialogs/AssetPickerDiscovery.cs", "87786544b985287469ee19b5c176afa4"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Appearance/Styles.cs", "e001fc02d4424aaab245be5232f6eede"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Appearance/DirectableAppearance.cs", "5ec48bf41a864cefa3fb754b75a8ed6c"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Appearance/ColorExtensions.cs", "89d80dc0ab2fa4beea9552404242a4b1"),
                ("Assets/ActionSequenceEditor/Editor/Views/Shared/Dialogs/SequenceInteraction.cs", "0745a65cd081eb746aeea1c8179165ab"),
                ("Assets/ActionSequenceEditor/Editor/Views/Localization/PropertyWarningText.cs", "66c31007b18eb064baa3ed273fbfed42"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/SessionStateRecoveryStore.cs", "405a0a48009577049ae0f0f16a9aa3a2"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Sequences/JsonRecoveryCodec.cs", "110a6bfc2d423ef4ab6dd11055dc7f68"),
                ("Assets/ActionSequenceEditor/Editor/Infrastructure/Preview/PlaybackControlAdapter.cs", "efdda6ce61d943849968c00da283ee49"),
                ("Assets/ActionSequenceEditor/Editor/Samples/FeatureSample/FeatureSamplePreviewWindow.cs", "8057cd80c2f7e7649be696ad53a648e8"),
                ("Assets/ActionSequenceEditor/Editor/Samples/FeatureSample/FeatureSampleComposition.cs", "ba52c705b138c7e4faf13dc210c30b63"),
                ("Assets/ActionSequenceEditor/Editor/Presentation/Samples/FeatureSample/FeatureSamplePresenter.cs", "c2dd0385adca9bb489392a1a1b31631b"),
                ("Assets/ActionSequenceEditor/Editor/Extensions/Preview/PreviewBase.cs", "56bba5dc7abf8465c8e3802c977a6dcd"),
                ("Assets/ActionSequenceEditor/Runtime/Metadata/PropertyFieldMetadata.cs", "24fa94a40f752a142bddcde4c236fd8e"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/SequenceGraphValidation.cs", "71fc40a1e0a1b7f468093c6e2a7877cb"),
                ("Assets/ActionSequenceEditor/Runtime/Serialization/JsonFloatEncoding.cs", "107863859ba94b9580581de9746adeef"),
            })
                Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(item.path), Is.EqualTo(item.guid), item.path);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>("Assets/ActionSequenceEditor/Editor/Samples/FeatureSample/FeatureSamplePreviewWindow.cs").GetClass(), Is.EqualTo(typeof(Samples.FeatureSamplePreviewWindow)));
            const string ui = "Assets/ActionSequenceEditor/Editor/Views/UIToolkit/";
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + typeof(ToolkitWindow).Name + ".cs"), Is.EqualTo("bd2bd6eded8ebf14687952b313d079d9"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + "Timeline/" + typeof(ToolkitCanvas).Name + ".cs"), Is.EqualTo("f7548d331bc116c4ba695eb4b55bdf0f"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + "Properties/" + typeof(ToolkitProperties).Name + ".cs"), Is.EqualTo("947256fa11135cc43b04ba402a1f03c9"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(ui + typeof(ToolkitSettings).Name + ".cs"), Is.EqualTo("69c0eced166ee5a45ad033187ae1070e"));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + typeof(ToolkitWindow).Name + ".cs").GetClass(), Is.EqualTo(typeof(ToolkitWindow)));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + typeof(ToolkitSettings).Name + ".cs").GetClass(), Is.EqualTo(typeof(ToolkitSettings)));
            const string imgui = "Assets/ActionSequenceEditor/Editor/Views/IMGUI/";
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(imgui + "Dialogs/SequenceCreationWindow.cs"), Is.EqualTo("1a368b48e50f45e797eb364d1bff797f"));
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(imgui + "Dialogs/SequenceCreationWindow.cs").GetClass(), Is.EqualTo(typeof(SequenceCreationWindow)));
            foreach (var type in new[] { typeof(ActionSequenceEditor.Views.UIToolkit.UIToolkitInspectorTarget), typeof(ActionSequenceEditor.Views.UIToolkit.UIToolkitSelectionInspector) })
                Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + "Properties/" + type.Name + ".cs").GetClass(), Is.EqualTo(type));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(imgui + "Properties/Standard/SequenceInspector.cs"), Is.EqualTo("1d716975a9a545de861fefb88b2c17a6"));
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(imgui + "Properties/Standard/ClipInspector.cs"), Is.EqualTo("1d4a2bbcab364421fbac5f6dbe2fd18d"));
            Assert.That(Inspector(new FeatureSampleSequence()), Is.TypeOf<SequenceInspector>());
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ActionSequenceEditor/Editor/Views/UIToolkit/TimelineCanvas.uss"), Is.Not.Null);
            Assert.That(Resources.Load<GUISkin>("ActionSequenceEditor/StyleSheet"), Is.Not.Null);
            Assert.That(Styles.WhiteTexture, Is.Not.Null);
            Assert.That(Styles.PlayIcon, Is.Not.Null);
            foreach (var type in new[] { typeof(Views.UIToolkit.SequenceCreationWindow), typeof(Views.UIToolkit.SequenceExportWindow), typeof(Views.UIToolkit.RenameWindow), typeof(Views.UIToolkit.SequencePickerWindow) })
            {
                Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(ui + "Dialogs/" + type.Name + ".cs").GetClass(), Is.EqualTo(type));
                Assert.That(type.GetMethod("OnGUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null);
            }
            Assert.That(typeof(Styles).GetProperties().Any(property => property.PropertyType == typeof(GUIStyle) || property.PropertyType == typeof(GUISkin)), Is.False);
            foreach (var field in typeof(Styles).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(field => field.FieldType == typeof(Texture2D)))
                Assert.That(field.GetValue(null), Is.Not.Null, field.Name + "必须绑定实际纹理。");
            Assert.That(EditorExtensionFactory.CreateHeader(typeof(FeatureSampleSequence)), Is.Not.Null);
            Assert.That(Inspector(new FeatureSampleInterval()), Is.TypeOf<ClipInspector>());
            Assert.That(typeof(Composition.SettingsSessionComposition).Assembly, Is.SameAs(typeof(Composition.WindowBinding).Assembly));
            Assert.That(typeof(ActionSequence).Assembly.GetReferencedAssemblies().Any(a => a.Name == typeof(Composition.WindowBinding).Assembly.GetName().Name), Is.False);
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
            var original = ReflectionTools.GetImplementationsOf(typeof(ActionTrack)).Where(t => Infrastructure.Discovery.ProductTypeFilter.Includes(t) && !t.ContainsGenericParameters && !t.IsDefined(typeof(ObsoleteAttribute), true))
                .Select(t => (type: t, name: t.RTGetAttribute<NameAttribute>(true)?.name ?? t.Name.SplitCamelCase(), category: t.RTGetAttribute<CategoryAttribute>(true)?.category))
                .OrderBy(t => t.name).ThenBy(t => t.category).Select(t => t.type).ToArray();
            var actual = ActionSequenceEditorTypeCatalog.DerivedFrom(typeof(ActionTrack));
            Assert.That(actual.Select(i => i.type), Is.EqualTo(original));
            var item = actual.Single(i => i.type == typeof(FeatureSampleIntervalTrack));
            item.attachableTypes[0] = typeof(ActionSequence); actual.Clear();
            Assert.That(ActionSequenceEditorTypeCatalog.DerivedFrom(typeof(ActionTrack)).Single(i => i.type == typeof(FeatureSampleIntervalTrack)).attachableTypes, Is.EqualTo(new[] { typeof(FeatureSampleGroup) }));
            var info = DirectableTypeInfo.For(typeof(FeatureSampleIntervalTrack));
            Assert.That(info, Is.SameAs(DirectableTypeInfo.For(typeof(FeatureSampleIntervalTrack))));
            Assert.That(info.Color, Is.EqualTo(new Color(.3f, .65f, 1)));
        }
        [Test] public void CapabilitiesRemainIndependentOfLocksAndRespectSignalsAndBlendSentinels()
        {
            var asset = new FeatureSampleSequence();
            var track = asset.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[0]; var clip = track.Clips[0];
            Assert.That(clip.CanScale() && clip.CanBlendIn() && clip.CanBlendOut(), Is.True);
            track.IsLocked = true; track.IsActive = false;
            Assert.That(clip.CanScale() && clip.CanBlendIn(), Is.True);
            var marker = asset.RootContainers.OfType<ActionGroup>().ElementAt(0).Tracks[2].Clips[0];
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
            Assert.That(new ActionGroup().CanAddTrackOfType(typeof(UniqueTrack)), Is.False);
            Assert.That(group.CanAddTrackOfType(typeof(ActionTrack)), Is.False);
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
