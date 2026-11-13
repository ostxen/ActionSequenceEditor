using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using NBC.ActionEditor.Workbench;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class JsonPropertyTests
    {
        public static class SparseOptions
        {
            [MenuName("Later"), OptionSort(1)] public const int Later = 20;
            [MenuName("First"), OptionSort(0)] public const int First = 7;
        }
        public struct NestedValue { public int Count; }
        private sealed class SameHash
        {
            public override int GetHashCode() => 1;
        }
        public sealed class Fields
        {
            [OptionParam(typeof(SparseOptions))] public int Mode = 7;
            [OptionRelateParam("Mode", 20)] public string Conditional = "shown";
            public NestedValue Value;
            public List<NestedValue> Values = new List<NestedValue> { new NestedValue { Count = 3 } };
            public int[] Numbers;
            [HideInInspector] public int Hidden;
        }
        private TextAsset previous, fixture;
        [ToolkitProperties(typeof(FeatureSampleInterval))]
        private sealed class IntervalProperties : ToolkitPropertyExtension
        {
            public override void Populate(VisualElement root, object target, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh)
            {
                var state = presenter.Field(target, typeof(FeatureSampleInterval).GetField("Count"));
                var control = new IntegerField("Extension count") { name = "extension-count", value = (int)state.Value, isDelayed = true };
                control.RegisterValueChangedCallback(e => { presenter.Write(state.Path, e.newValue); refresh(); });
                root.Add(control);
            }
        }
        private sealed class StructInspector : InspectorsBase
        {
            public void Draw(Fields value)
            {
                if (Presenter == null) Bind(Composition.PropertyComposition.Create(App.Context));
                FieldDefaultInspector(typeof(Fields).GetField("Value"), value);
            }
        }
        private sealed class StructWindow : EditorWindow
        {
            public Fields Value = new Fields();
            public Rect Input;
            public Vector2 Offset;
            private readonly StructInspector inspector = new StructInspector();
            private void OnGUI()
            {
                Offset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                inspector.Draw(Value);
                if (Event.current.type == EventType.Repaint) Input = GUILayoutUtility.GetLastRect();
            }
        }
        private sealed class OptionsInspector : InspectorsBase
        {
            public bool ConditionalVisible;
            public bool FocusOptions;
            public int? SelectIndex;
            public void Draw(Fields value)
            {
                if (Presenter == null) Bind(Composition.PropertyComposition.Create(App.Context));
                ConditionalVisible = false;
                foreach (var description in Presenter.Fields(value))
                {
                    if (description.Field.Name != "Mode" && description.Field.Name != "Conditional") continue;
                    if (description.Field.Name == "Conditional") ConditionalVisible = true;
                    if (description.Field.Name == "Mode") GUI.SetNextControlName("sparse-options");
                    FieldDefaultInspector(description.Field, value);
                }
                if (FocusOptions) { GUI.FocusControl("sparse-options"); FocusOptions = false; }
                if (SelectIndex.HasValue && Event.current.type == EventType.Repaint)
                {
                    var index = SelectIndex.Value; SelectIndex = null;
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
                    var callbackType = typeof(EditorGUI).GetNestedType("PopupCallbackInfo", System.Reflection.BindingFlags.NonPublic);
                    var callback = Activator.CreateInstance(callbackType, flags, null, new object[] { GUIUtility.keyboardControl }, null);
                    callbackType.GetField("instance", flags).SetValue(null, callback);
                    callbackType.GetMethod("SetEnumValueDelegate", flags).Invoke(callback, new object[] { null, new[] { "First", "Later" }, index });
                }
            }
        }
        private sealed class OptionsWindow : EditorWindow
        {
            public readonly Fields Value = new Fields();
            public readonly OptionsInspector Inspector = new OptionsInspector();
            private void OnGUI() => Inspector.Draw(Value);
            private void OnDisable() => Inspector.CancelInput();
        }
        private struct NestedCollection { public int[] Items; }
        private sealed class NestedCollectionOwner { public NestedCollection Value; }
        private sealed class NestedCollectionInspector : InspectorsBase
        {
            public Rect AddRect;
            public void Draw(NestedCollectionOwner value)
            {
                if (Presenter == null) Bind(Composition.PropertyComposition.Create(App.Context));
                DrawDefaultInspector(value);
            }
            protected override bool DrawAddButton()
            {
                var result = base.DrawAddButton();
                if (Event.current.type == EventType.Repaint) AddRect = GUILayoutUtility.GetLastRect();
                return result;
            }
        }
        private sealed class NestedCollectionWindow : EditorWindow
        {
            public readonly NestedCollectionOwner Value = new NestedCollectionOwner();
            public readonly NestedCollectionInspector Inspector = new NestedCollectionInspector();
            public Vector2 Offset;
            private void OnGUI()
            {
                Offset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                Inspector.Draw(Value);
            }
            private void OnDisable() => Inspector.CancelInput();
        }
        private string recovery;
        [SetUp] public void SetUp()
        {
            previous = App.TextAsset; recovery = App.Session.CaptureRecovery();
            fixture = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument())); App.TextAsset = fixture;
        }
        [TearDown] public void TearDown()
        {
            App.Session.CancelEdit(); App.TextAsset = previous; App.Session.RestoreRecovery(recovery);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
        [Test] public void DescriptionsCacheConditionsAndRealOptionValuesWithoutChangingOrder()
        {
            var value = new Fields(); var fields = PropertyDescription.For(typeof(Fields));
            Assert.That(PropertyDescription.For(typeof(Fields)), Is.SameAs(fields));
            Assert.That(fields.Select(d => d.Field.Name), Is.EqualTo(typeof(Fields).GetFields().Select(f => f.Name)));
            var options = fields.Single(d => d.Field.Name == "Mode");
            Assert.That(options.Constants.Select(f => f.GetRawConstantValue()), Is.EqualTo(new object[] { 7, 20 }));
            Assert.That(options.OptionIndex(20), Is.EqualTo(1), "The old popup treated authored value20 as a popup index, not a constant value.");
            Assert.That(options.OptionIndex(99), Is.EqualTo(-1));
            var conditional = fields.Single(d => d.Field.Name == "Conditional");
            Assert.That(conditional.Visible(value), Is.False); value.Mode = 20; Assert.That(conditional.Visible(value), Is.True);
            Assert.That(fields.Single(d => d.Field.Name == "Hidden").Visible(value), Is.False);
            Assert.That(typeof(PropertyDescription).GetProperties().Any(p => typeof(Attribute).IsAssignableFrom(p.PropertyType)), Is.False,
                "Cached descriptions must not expose mutable declaration attributes.");
        }
        [Test] public void ImguiFoldoutsUseReferenceIdentityAndResetWithTarget()
        {
            var inspector = new InspectorsBase();
            var first = new SameHash(); var second = new SameHash();
            var firstPath = new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, first);
            var secondPath = new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, second);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var set = typeof(InspectorsBase).GetMethod("SetExpanded", flags);
            var get = typeof(InspectorsBase).GetMethod("GetCollectionFoldout", flags);
            set.Invoke(inspector, new object[] { firstPath, true });
            Assert.That(get.Invoke(inspector, new object[] { firstPath }), Is.True);
            Assert.That(get.Invoke(inspector, new object[] { secondPath }), Is.False);
            inspector.SetTarget(second);
            Assert.That(get.Invoke(inspector, new object[] { firstPath }), Is.False);
        }

        [Test] public void PathsWriteNestedStructsAndReResolveCollectionIndices()
        {
            var value = new Fields(); var root = new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, value);
            var field = typeof(NestedValue).GetField("Count");
            root.Field(typeof(Fields).GetField("Value")).Field(field).Set(8);
            Assert.That(value.Value.Count, Is.EqualTo(8));
            var equivalent = new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, value)
                .Field(typeof(Fields).GetField("Value")).Field(field);
            var original = root.Field(typeof(Fields).GetField("Value")).Field(field);
            Assert.That(equivalent, Is.EqualTo(original));
            Assert.That(equivalent.GetHashCode(), Is.EqualTo(original.GetHashCode()));
            var states = new Dictionary<NBC.ActionEditor.Application.Editing.PropertyPath, bool> { [original] = true };
            Assert.That(states[equivalent], Is.True);
            var element = root.Field(typeof(Fields).GetField("Values")).Element(0).Field(field);
            element.Set(11); Assert.That(value.Values[0].Count, Is.EqualTo(11));
            value.Values = new List<NestedValue> { new NestedValue { Count = 21 } };
            element.Set(22); Assert.That(value.Values[0].Count, Is.EqualTo(22));
            Assert.That(element.Mixed(new[] { root.Field(typeof(Fields).GetField("Value")).Field(field) }), Is.True);
            value.Values.Clear(); Assert.Throws<ArgumentOutOfRangeException>(() => element.Set(30));
            Assert.That(value.Value.Count, Is.EqualTo(8));
        }
        [Test] public void SharedCollectionOperationsCoverNullFirstArrayRemovalReorderAndBounds()
        {
            var array = (int[])NBC.ActionEditor.Application.Editing.CollectionEditing.Append(typeof(int[]), null, 7);
            array = (int[])NBC.ActionEditor.Application.Editing.CollectionEditing.Append(typeof(int[]), array, 20);
            NBC.ActionEditor.Application.Editing.CollectionEditing.Move(array, 0, 1); Assert.That(array, Is.EqualTo(new[] { 20, 7 }));
            array = (int[])NBC.ActionEditor.Application.Editing.CollectionEditing.Remove(typeof(int[]), array, 0); Assert.That(array, Is.EqualTo(new[] { 7 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => NBC.ActionEditor.Application.Editing.CollectionEditing.Remove(typeof(int[]), array, 1));
            var list = NBC.ActionEditor.Application.Editing.CollectionEditing.Append(typeof(List<string>), null, "first");
            Assert.That(list.Count, Is.EqualTo(1)); NBC.ActionEditor.Application.Editing.CollectionEditing.Remove(typeof(List<string>), list, 0); Assert.That(list.Count, Is.Zero);
            Assert.That(App.Context.Properties.CreateElement(typeof(string)), Is.EqualTo(""));
            Assert.That(App.Context.Properties.CreateElement(typeof(UnityEngine.Object)), Is.Null);
        }

        [Test] public void PresenterOwnsVisibleSparseOptionsCollectionAndStalePathWrites()
        {
            var presenter = Composition.PropertyComposition.Create(App.Context); var value = new Fields();
            Assert.That(presenter.Fields(value).Any(f => f.Field.Name == "Conditional"), Is.False);
            var mode = presenter.Field(value, typeof(Fields).GetField("Mode"));
            Assert.That(mode.OptionLabels, Is.EqualTo(new[] { "First", "Later" }));
            mode.OptionLabels[0] = "changed by view";
            Assert.That(mode.OptionLabels[0], Is.EqualTo("First"));
            presenter.Write(mode.Path, mode.OptionValue(1));
            Assert.That(value.Mode, Is.EqualTo(20));
            Assert.That(presenter.Fields(value).Any(f => f.Field.Name == "Conditional"), Is.True);
            var numbers = presenter.Field(value, typeof(Fields).GetField("Numbers"));
            presenter.Append(numbers.Path, typeof(int[]));
            Assert.That(value.Numbers, Is.EqualTo(new[] { 0 }));
            presenter.Write(numbers.Path.Element(0), 7);
            presenter.Remove(numbers.Path, typeof(int[]), 0);
            Assert.That(value.Numbers, Is.Empty);
            var clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
            var path = presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Count")).Path;
            presenter.Write(path, 12); Undo.PerformUndo();
            Assert.Throws<InvalidOperationException>(() => presenter.Write(path, 15));
            clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0]; clip.Parent.IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => presenter.Write(presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Count")).Path, 20));
        }
        [Test] public void ClipPropertyTimingCannotBorrowAnActiveGestureOrCrossItsNextBound()
        {
            var presenter = Composition.PropertyComposition.Create(App.Context);
            var clip = App.AssetData.AllTracks.First().Clips[0];
            var before = JsonDocumentCodec.Encode(App.AssetData);
            using (var token = App.Session.BeginEdit(this))
            {
                Assert.Throws<InvalidOperationException>(() => presenter.Timing(clip, 10, 10));
                Assert.Throws<InvalidOperationException>(() => presenter.Blend(clip, true, .1f));
                var field = presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Count"));
                Assert.Throws<InvalidOperationException>(() => presenter.Write(field.Path, 15));
                Assert.Throws<InvalidOperationException>(() => presenter.BeginDraft(field.Path));
                Assert.Throws<InvalidOperationException>(() => presenter.Rename(clip.Parent, "foreign"));
                var collection = presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Parameters"));
                Assert.Throws<InvalidOperationException>(() => presenter.Append(collection.Path, collection.Type));
                Assert.Throws<InvalidOperationException>(() => presenter.Remove(collection.Path, collection.Type, 0));
                Assert.That(token.IsActive, Is.True);
                Assert.That(JsonDocumentCodec.Encode(App.AssetData), Is.EqualTo(before));
                token.Commit();
            }
            var maximum = presenter.ClipState(clip).Maximum;
            presenter.Timing(clip, 10, 10);
            Assert.That(clip.Length, Is.GreaterThanOrEqualTo(.01f));
            Assert.That(clip.EndTime, Is.LessThanOrEqualTo(maximum));
            Assert.That(App.Session.IsEditing, Is.False);
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(App.AssetData), Is.EqualTo(before));
        }

        [Test] public void ClipPropertyRejectsNonFiniteAndInvalidStepWithoutLeavingEdit()
        {
            var clip = App.AssetData.AllTracks.First().Clips[0];
            var before = JsonDocumentCodec.Encode(App.AssetData);
            Assert.Throws<ArgumentOutOfRangeException>(() => App.Context.Clips.SetTiming(clip, float.NaN, 1, .1f));
            Assert.That(JsonDocumentCodec.Encode(App.AssetData), Is.EqualTo(before));
            Assert.That(App.Session.IsEditing, Is.False);
            clip = App.AssetData.AllTracks.First().Clips[0];
            Assert.Throws<ArgumentOutOfRangeException>(() => App.Context.Clips.SetTiming(clip, 0, 1, 0));
            Assert.That(JsonDocumentCodec.Encode(App.AssetData), Is.EqualTo(before));
            Assert.That(App.Session.IsEditing, Is.False);
        }

        [Test] public void PointPropertyMovePreservesStoredLengthAndUndo()
        {
            var clip = App.AssetData.AllTracks.SelectMany(t => t.Clips).OfType<FeatureSampleMarker>().First();
            var length = typeof(TimelineClip).GetField("length", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var stored = length.GetValue(clip);
            var start = clip.StartTime;
            App.Context.Clips.SetTiming(clip, 1, 1, .1f);
            Assert.That(clip.StartTime, Is.EqualTo(1));
            Assert.That(clip.Length, Is.Zero);
            Assert.That(length.GetValue(clip), Is.EqualTo(stored));
            Undo.PerformUndo();
            clip = App.AssetData.AllTracks.SelectMany(t => t.Clips).OfType<FeatureSampleMarker>().First();
            Assert.That(clip.StartTime, Is.EqualTo(start));
            Assert.That(length.GetValue(clip), Is.EqualTo(stored));
        }

        [Test] public void PresenterDraftCancelsRejectsConcurrentChangesAndCommitsOneUndo()
        {
            var presenter = Composition.PropertyComposition.Create(App.Context);
            var clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
            var path = presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Count")).Path;
            var draft = presenter.BeginDraft(path);
            presenter.PreviewDraft(draft, "12");
            Assert.That(clip.Count, Is.EqualTo(3));
            Assert.That(App.Session.IsEditing, Is.False);
            presenter.CancelDraft(draft);
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(draft, 12));
            draft = presenter.BeginDraft(path);
            presenter.CommitDraft(draft, 12);
            Assert.That(clip.Count, Is.EqualTo(12));
            draft = presenter.BeginDraft(path);
            presenter.Write(path, 15);
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(draft, 20));
            Assert.That(clip.Count, Is.EqualTo(15));
            Undo.PerformUndo(); Undo.PerformUndo();
            Assert.That(((FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0]).Count, Is.EqualTo(3));
        }

        [Test] public void DraftInvalidatedByLockReleasesOnlyItsOwnInput()
        {
            var presenter = Composition.PropertyComposition.Create(App.Context);
            var clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
            var path = presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Count")).Path;
            var input = presenter.BeginDraft(path);
            clip.Parent.IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(input, 21));
            Assert.That(input.Active, Is.False);
            Assert.That(clip.Count, Is.EqualTo(3));
            clip.Parent.IsLocked = false;
            var next = presenter.BeginDraft(path);
            presenter.CancelDraft(input);
            Assert.That(next.Active, Is.True);
            presenter.CommitDraft(next, 22);
            Assert.That(clip.Count, Is.EqualTo(22));
        }

        [Test] public void ComplexPropertySnapshotsDoNotExposeAuthorObjectsOrMutableCollections()
        {
            var presenter = Composition.PropertyComposition.Create(App.Context);
            var value = new Fields();
            var fields = presenter.CaptureFields(value);
            var collection = (IList)fields.Single(f => f.Description.Field.Name == "Values").Value;
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => collection[0] = null);
            var nested = (NBC.ActionEditor.Presentation.Contracts.PropertyObjectState)collection[0];
            Assert.That(nested.Fields.Single().Value, Is.EqualTo(3));
            value.Values[0] = new NestedValue { Count = 12 };
            Assert.That(nested.Fields.Single().Value, Is.EqualTo(3));
            presenter.Write(nested.Fields.Single().Path, 21);
            Assert.That(value.Values[0].Count, Is.EqualTo(21));
        }

        [Test] public void CurveDisplayAndSubmittedValueDoNotAliasAuthorData()
        {
            var presenter = Composition.PropertyComposition.Create(App.Context);
            var clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
            var before = JsonDocumentCodec.Encode(App.AssetData);
            var field = presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Curve"));
            var displayed = (AnimationCurve)field.Value;
            Assert.That(displayed, Is.Not.SameAs(clip.Curve));
            displayed.AddKey(.5f, 2);
            Assert.That(JsonDocumentCodec.Encode(App.AssetData), Is.EqualTo(before));
            presenter.Write(field.Path, displayed);
            Assert.That(clip.Curve.length, Is.EqualTo(3));
            displayed.AddKey(.75f, 3);
            Assert.That(clip.Curve.length, Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(App.AssetData), Is.EqualTo(before));
        }

        [Test] public void AuthoredPathsUseOneUndoCancelAndRejectStaleOrLockedRoots()
        {
            FeatureSampleInterval Clip() => (FeatureSampleInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
            NBC.ActionEditor.Application.Editing.PropertyPath Path(FeatureSampleInterval clip) => new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, clip).Field(typeof(FeatureSampleInterval).GetField("Parameters")).Element(0).Field(typeof(FeatureSampleParameter).GetField("Value"));
            var path = Path(Clip()); var token = App.Session.BeginEdit(this);
            path.Write(4f); path.Write(6f); token.Commit();
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            Undo.PerformUndo(); Assert.That(Clip().Parameters[0].Value, Is.EqualTo(1));
            Undo.PerformRedo(); Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => path.Write(9f));
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            token = App.Session.BeginEdit(this); Path(Clip()).Write(12f); token.Cancel();
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => Path(Clip()).Write(15f));
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
        }
        [UnityTest] public IEnumerator ExplicitNativePropertyExtensionUsesRealControlAndSharedUndo()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var presenter = Composition.PropertyComposition.Create(App.Context);
                var clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
                var refreshes = 0;
                var properties = new UIToolkitPropertyView(presenter, type =>
                {
                    var adapter = ExtensionResolver.ResolveExtension(new[] { typeof(IntervalProperties) }, type, typeof(ToolkitPropertiesAttribute));
                    return adapter == null ? null : (ToolkitPropertyExtension)Activator.CreateInstance(adapter, true);
                });
                properties.Populate(root, clip, () => refreshes++);
                Assert.That(root.Query<IntegerField>().ToList().Count, Is.EqualTo(1));
                root.Q<IntegerField>("extension-count").value = 17;
                Assert.That(clip.Count, Is.EqualTo(17)); Assert.That(refreshes, Is.EqualTo(1));
                Undo.PerformUndo();
                Assert.That(((FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0]).Count, Is.EqualTo(3));
                Assert.That(EditorCustomFactory.GetToolkitProperties(typeof(FeatureSampleInterval)), Is.Null, "Injected test adapters must not enter the product registry.");
                clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
                clip.Parent.IsLocked = true;
                root.Clear(); properties.Populate(root, clip, () => refreshes++);
                Assert.That(root.Q<IntegerField>("extension-count").enabledInHierarchy, Is.False);
                root.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator NativeDelayedFieldsReportStaleAndLockedInputWithoutChangingTheGraph()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var presenter = Composition.PropertyComposition.Create(App.Context);
                var clip = (FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0];
                new UIToolkitPropertyView(presenter).DrawFields(root, clip, () => { }, 0);
                var field = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                clip.Parent.IsLocked = true;
                field.value = 17;
                Assert.That(clip.Count, Is.EqualTo(3));
                Assert.That(root.Q<HelpBox>(), Is.Not.Null);
                clip.Parent.IsLocked = false;
                presenter.Write(presenter.Field(clip, typeof(FeatureSampleInterval).GetField("Count")).Path, 12);
                Undo.PerformUndo();
                field.value = 19;
                Assert.That(((FeatureSampleInterval)App.AssetData.AllTracks.First().Clips[0]).Count, Is.EqualTo(3));
                Assert.That(root.Q<HelpBox>(), Is.Not.Null);
                root.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator NativeFieldsUseSharedDescriptionsAndWriteNestedValueTypes()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root); var value = new Fields();
                void Refresh() { root.Clear(); new UIToolkitPropertyView(Composition.PropertyComposition.Create(App.Context)).DrawFields(root, value, Refresh, 0); }
                Refresh();
                var popup = root.Q<PopupField<string>>(); Assert.That(popup.value, Is.EqualTo("First")); popup.index = 1;
                Assert.That(value.Mode, Is.EqualTo(20));
                Assert.That(root.Query<TextField>().ToList().Any(f => f.label == "Conditional"), Is.True);
                root.Query<IntegerField>().ToList().First(f => f.label == "Count").value = 42;
                Assert.That(value.Value.Count, Is.EqualTo(42));
                Assert.That(root.Query<IntegerField>().ToList().Any(f => f.label == "Hidden"), Is.False);
                var numbers = root.Query<Foldout>().ToList().Single(f => f.text == "Numbers");
                numbers.value = true;
                yield return null; yield return null;
                var add = numbers.Q<Button>();
                Assert.That(value.Numbers, Is.Null);
                var position = add.LocalToWorld(add.contentRect.center);
                using (var down = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = position }))
                { down.target = add; add.SendEvent(down); }
                using (var up = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = position }))
                { up.target = add; add.SendEvent(up); }
                yield return null;
                Assert.That(value.Numbers, Is.EqualTo(new[] { 0 }), "Null arrays must be created through the actual add control.");
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
        [UnityTest] public IEnumerator ImguiNullArrayInsideStructKeepsFoldoutAcrossBoxingAndAddsThroughButton()
        {
            var window = ScriptableObject.CreateInstance<NestedCollectionWindow>();
            try
            {
                window.position = new Rect(100, 100, 440, 250); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null;
                var fold = new Vector2(22, EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 8) + window.Offset;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = fold });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = fold });
                window.Repaint(); yield return null; yield return null;
                Assert.That(window.Inspector.AddRect.width, Is.GreaterThan(0), "Each repaint boxes the struct again, but its field path stays the same.");
                var add = window.Inspector.AddRect.center + window.Offset;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = add });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = add });
                window.Repaint(); yield return null;
                Assert.That(window.Value.Value.Items, Is.EqualTo(new[] { 0 }));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiSparseOptionsUseNativePopupCallbackAndRevealConditionalField()
        {
            var window = ScriptableObject.CreateInstance<OptionsWindow>();
            try
            {
                window.position = new Rect(120, 120, 440, 160); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null;
                Assert.That(window.Value.Mode, Is.EqualTo(7));
                Assert.That(window.Inspector.ConditionalVisible, Is.False);
                window.Inspector.FocusOptions = true; window.Repaint(); yield return null;
                window.Inspector.SelectIndex = 1; window.Repaint(); yield return null; yield return null;
                Assert.That(window.Value.Mode, Is.EqualTo(20), "Popup index must resolve to the declared sparse value.");
                Assert.That(window.Inspector.ConditionalVisible, Is.True);
                window.Inspector.SelectIndex = 0; window.Repaint(); yield return null; yield return null;
                Assert.That(window.Value.Mode, Is.EqualTo(7));
                Assert.That(window.Inspector.ConditionalVisible, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiNestedStructDelayedControlCommitsAndEscapeKeepsValue()
        {
            var window = ScriptableObject.CreateInstance<StructWindow>();
            try
            {
                window.position = new Rect(100, 100, 440, 160); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null;
                var point = new Vector2(window.Input.xMax - 45, window.Input.center.y) + window.Offset;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, control = true });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha7, character = '7' });
                Assert.That(window.Value.Value.Count, Is.Zero);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\n' });
                yield return null;
                Assert.That(window.Value.Value.Count, Is.EqualTo(7));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, control = true });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha9, character = '9' });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                yield return null;
                Assert.That(window.Value.Value.Count, Is.EqualTo(7)); LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
        [UnityTest] public IEnumerator NativeDelayedInputKeepsIntermediateTextUntilReturnAndEscapeCancels()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.Show(); window.Focus(); yield return null; yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var clip = (FeatureSampleInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                new UIToolkitPropertyView(Composition.PropertyComposition.Create(App.Context)).DrawFields(root, clip, () => { }, 0);
                var field = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                Assert.That(field.isDelayed, Is.True);
                yield return null; yield return null;
                var container = field.Q("unity-text-input");
                var input = container.Q<TextElement>(); Assert.That(input, Is.Not.Null); input.Focus(); yield return null;
                Assert.That(window.rootVisualElement.focusController.focusedElement, Is.SameAs(field));
                void Key(KeyCode key, char character = '\0', bool control = false)
                {
                    using var e = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, character = character, control = control });
                    e.target = input; input.SendEvent(e);
                }
                Key(KeyCode.A, control: true); Key(KeyCode.Alpha9, '9');
                Assert.That(clip.Count, Is.EqualTo(3), "Delayed field must not write each intermediate text change.");
                Key(KeyCode.Return, '\n'); yield return null;
                Assert.That(clip.Count, Is.EqualTo(9));
                Key(KeyCode.A, control: true); Key(KeyCode.Alpha4, '4'); Key(KeyCode.Escape); yield return null;
                window.Canvas.Focus(); yield return null;
                Assert.That(clip.Count, Is.EqualTo(9), "Escape must restore committed input instead of committing the pending text on blur.");
                Undo.PerformUndo(); yield return null;
                Assert.That(((FeatureSampleInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                root.Clear(); clip = (FeatureSampleInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                new UIToolkitPropertyView(Composition.PropertyComposition.Create(App.Context)).DrawFields(root, clip, () => { }, 0);
                field = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                yield return null; input = field.Q("unity-text-input").Q<TextElement>(); input.Focus(); yield return null;
                Key(KeyCode.A, control: true); Key(KeyCode.Alpha5, '5');
                Assert.That(clip.Count, Is.EqualTo(3));
                window.Canvas.Focus(); yield return null;
                Assert.That(clip.Count, Is.EqualTo(5), "Blur commits delayed text once.");
                Undo.PerformUndo(); yield return null;
                Assert.That(((FeatureSampleInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
    }
}
