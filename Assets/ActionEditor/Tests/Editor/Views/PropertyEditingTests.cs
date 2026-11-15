using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Infrastructure.Assets;
using NBC.ActionEditor.Views.IMGUI.Properties;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitProperties = NBC.ActionEditor.Views.UIToolkit.PropertyView;
using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Infrastructure.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class PropertyEditingTests
    {
        public static class SparseOptions
        {
            [MenuName("Later"), OptionSort(1)] public const int Later = 20;
            [MenuName("First"), OptionSort(0)] public const int First = 7;
        }
        public struct NestedValue { public int Count; }
        [Serializable] public struct ReferenceValue { public List<FeatureSampleParameter> Items; }
        [Serializable] public struct OuterValue { public ReferenceValue Inner; }
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
            public override void Populate(VisualElement root, NBC.ActionEditor.Presentation.Properties.PropertyTargetState target, NBC.ActionEditor.Presentation.Presenters.PropertyPresenter presenter, Action refresh)
            {
                var state = target.Fields.Single(f => f.Name == "Count");
                var control = new IntegerField("Extension count") { name = "extension-count", value = (int)state.Value, isDelayed = true };
                control.RegisterValueChangedCallback(e => { presenter.Write(state.Id, e.newValue); refresh(); });
                root.Add(control);
            }
        }
        private sealed class StructInspector : InspectorsBase
        {
            public void Draw(Fields value)
            {
                if (Presenter == null) Bind(EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings));
                FieldDefaultInspector(Presenter.CaptureInputs(value).Single(f => f.Name == "Value"));
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
        private sealed class IssueWindow : EditorWindow
        {
            public Editor Inspector;
            public Vector2 Offset;
            private void OnGUI()
            {
                Offset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                Inspector?.OnInspectorGUI();
            }
        }
        private sealed class OptionsInspector : InspectorsBase
        {
            public bool ConditionalVisible;
            public bool FocusOptions;
            public int? SelectIndex;
            public void Draw(Fields value)
            {
                if (Presenter == null) Bind(EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings));
                ConditionalVisible = false;
                foreach (var description in Presenter.CaptureInputs(value))
                {
                    if (description.Name != "Mode" && description.Name != "Conditional") continue;
                    if (description.Name == "Conditional") ConditionalVisible = true;
                    if (description.Name == "Mode") GUI.SetNextControlName("sparse-options");
                    FieldDefaultInspector(description);
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
        private sealed class NullInspector : InspectorsBase
        {
            public Rect ButtonRect;
            private string buttonLabel;
            public void Draw(object value, string field)
            {
                if (Presenter == null) Bind(EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings));
                var state = Presenter.CaptureInputs(value).Single(f => f.Name == field);
                buttonLabel = state.Label;
                FieldDefaultInspector(state);
            }
            protected override bool DrawNullButton(string label)
            {
                var result = base.DrawNullButton(label);
                if (Event.current.type == EventType.Repaint && label == buttonLabel) ButtonRect = GUILayoutUtility.GetLastRect();
                return result;
            }
        }
        private sealed class NullWindow : EditorWindow
        {
            public object Value;
            public string Field;
            public Vector2 Offset;
            public readonly NullInspector Inspector = new NullInspector();
            private void OnGUI()
            {
                Offset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                if (Value != null) Inspector.Draw(Value, Field);
            }
            private void OnDisable() => Inspector.CancelInput();
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
                if (Presenter == null) Bind(EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings));
                DrawDefaultInspector(Presenter.CaptureTarget(value));
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
            previous = EditorFixture.Shared.Source; recovery = EditorFixture.Shared.Session.CaptureRecovery();
            fixture = new TextAsset(JsonDocumentCodec.Encode(new FeatureSampleDocument())); EditorFixture.Shared.Open(fixture);
        }
        [TearDown] public void TearDown()
        {
            EditorFixture.Shared.Session.CancelEdit(); EditorFixture.Shared.Open(previous); EditorFixture.Shared.Session.RestoreRecovery(recovery);
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
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var firstPath = presenter.CaptureTarget(first).Id;
            var secondPath = presenter.CaptureTarget(second).Id;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var set = typeof(InspectorsBase).GetMethod("SetExpanded", flags);
            var get = typeof(InspectorsBase).GetMethod("GetCollectionFoldout", flags);
            set.Invoke(inspector, new object[] { firstPath, true });
            Assert.That(get.Invoke(inspector, new object[] { firstPath }), Is.True);
            Assert.That(get.Invoke(inspector, new object[] { secondPath }), Is.False);
            inspector.SetTarget(presenter.CaptureTarget(second));
            Assert.That(get.Invoke(inspector, new object[] { firstPath }), Is.False);
        }

        [Test] public void PathsWriteNestedStructsAndReResolveCollectionIndices()
        {
            var value = new Fields(); var root = new NBC.ActionEditor.Application.Editing.PropertyPath(EditorFixture.Shared.Properties, value);
            var field = typeof(NestedValue).GetField("Count");
            root.Field(typeof(Fields).GetField("Value")).Field(field).Set(8);
            Assert.That(value.Value.Count, Is.EqualTo(8));
            var equivalent = new NBC.ActionEditor.Application.Editing.PropertyPath(EditorFixture.Shared.Properties, value)
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
            Assert.That(EditorFixture.Shared.Properties.CreateElement(typeof(string)), Is.EqualTo(""));
            Assert.That(EditorFixture.Shared.Properties.CreateElement(typeof(UnityEngine.Object)), Is.Null);
        }

        [Test] public void PresenterOwnsVisibleSparseOptionsCollectionAndStalePathWrites()
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings); var value = new Fields();
            Assert.That(presenter.CaptureInputs(value).Any(f => f.Name == "Conditional"), Is.False);
            var mode = presenter.CaptureInputs(value).Single(f => f.Name == "Mode");
            Assert.That(mode.OptionLabels, Is.EqualTo(new[] { "First", "Later" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)mode.OptionLabels)[0] = "changed by view");
            Assert.That(mode.OptionLabels[0], Is.EqualTo("First"));
            presenter.SelectOption(mode.Id, 1);
            Assert.That(value.Mode, Is.EqualTo(20));
            Assert.That(presenter.CaptureInputs(value).Any(f => f.Name == "Conditional"), Is.True);
            var numbers = presenter.CaptureInputs(value).Single(f => f.Name == "Numbers");
            presenter.Append(numbers.Id);
            Assert.That(value.Numbers, Is.EqualTo(new[] { 0 }));
            numbers = presenter.CaptureInputs(value).Single(f => f.Name == "Numbers");
            presenter.Write(numbers.Children[0].Id, 7);
            numbers = presenter.CaptureInputs(value).Single(f => f.Name == "Numbers");
            presenter.Remove(numbers.Id, 0);
            Assert.That(value.Numbers, Is.Empty);
            var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            var path = presenter.CaptureInputs(clip).Single(f => f.Name == "Count").Id;
            presenter.Write(path, 12); Undo.PerformUndo();
            Assert.Throws<InvalidOperationException>(() => presenter.Write(path, 15));
            clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0]; clip.Parent.IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => presenter.Write(presenter.CaptureInputs(clip).Single(f => f.Name == "Count").Id, 20));
        }
        [Test] public void SelectedPropertyWritesAndCollectionEditsAreAtomicAndDoNotShareMutableValues()
        {
            var clips = EditorFixture.Shared.Document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
            EditorFixture.Shared.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var count = presenter.CaptureInputs(clips[0]).Single(f => f.Name == "Count").Id;
            presenter.Write(count, 17);
            Assert.That(clips.Select(c => c.Count), Is.EqualTo(new[] { 17, 17 }));
            NBC.ActionEditor.Presentation.Properties.PropertyInputState Parameters() => presenter.CaptureInputs(clips[0]).Single(f => f.Name == "Parameters");
            var parameters = Parameters();
            presenter.Append(parameters.Id);
            Assert.That(clips.Select(c => c.Parameters.Count), Is.EqualTo(new[] { 3, 3 }));
            Assert.That(clips[0].Parameters[2], Is.Not.SameAs(clips[1].Parameters[2]));
            presenter.Write(Parameters().Children[2].Children.Single(f => f.Name == "Value").Id, 8f);
            Assert.That(clips.Select(c => c.Parameters[2].Value), Is.EqualTo(new[] { 8f, 8f }));
            var curve = new AnimationCurve(new Keyframe(0, 4));
            presenter.Write(presenter.CaptureInputs(clips[0]).Single(f => f.Name == "Curve").Id, curve);
            Assert.That(clips[0].Curve, Is.Not.SameAs(clips[1].Curve));
            Assert.That(clips[0].Curve, Is.Not.SameAs(curve));
            presenter.Write(Parameters().Id, new List<FeatureSampleParameter> { new FeatureSampleParameter { Value = 14 } });
            Assert.That(clips[0].Parameters, Is.Not.SameAs(clips[1].Parameters));
            Assert.That(clips[0].Parameters[0], Is.Not.SameAs(clips[1].Parameters[0]));
            clips[1].Parameters.Clear();
            var unchanged = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            var unequal = Parameters().Children[0].Children.Single(f => f.Name == "Value").Id;
            Assert.That(presenter.CanInput(unequal), Is.False);
            Assert.DoesNotThrow(() => presenter.CaptureInputs(clips[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => presenter.Write(unequal, 99f));
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(unchanged));
            EditorFixture.Shared.Document.AllTracks.First().Clips.Remove(clips[1]);
            EditorFixture.Shared.Document.AllTracks.Skip(1).First().Clips.Add(clips[1]); EditorFixture.Shared.Document.Init();
            Assert.That(clips[0].IsLocked, Is.False);
            clips[1].Parent.IsLocked = true;
            var locked = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            Assert.Throws<InvalidOperationException>(() => presenter.Write(count, 99));
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(locked));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
        }

        [UnityTest] public IEnumerator NativeIssueButtonSelectsInvalidClipAndParameterEditClearsDiagnosticWithUndo()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); yield return null;
                var clip = EditorFixture.Shared.Document.AllTracks.SelectMany(track => track.Clips).OfType<FeatureSampleLoop>().First();
                clip.Period = 0;
                EditorFixture.Shared.Selection.Set(Array.Empty<IDirectable>(), false);
                var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                var issues = presenter.CaptureIssues();
                Assert.That(issues.Count, Is.EqualTo(1));
                var view = window.CreatePropertyView(); window.rootVisualElement.Add(view);
                var button = view.Query<Button>().ToList().Single(control => control.name.StartsWith("property-issue-"));
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(clip));
                yield return null; yield return null;
                var period = view.Query<FloatField>().ToList().Single(control => control.label == "源片段周期（秒）");
                period.value = 2;
                Assert.That(clip.Period, Is.EqualTo(2));
                Assert.That(presenter.CaptureIssues(), Is.Empty);
                yield return null; yield return null;
                Assert.That(view.Query<Button>().ToList().Any(control => control.name.StartsWith("property-issue-")), Is.False);
                Assert.Throws<InvalidOperationException>(() => presenter.SelectIssue(issues[0]));
                Undo.PerformUndo();
                Assert.That(presenter.CaptureIssues().Count, Is.EqualTo(1));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                view.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiIssueButtonSelectsClipWithoutChangingAuthorContent()
        {
            var window = ScriptableObject.CreateInstance<IssueWindow>();
            var proxy = ScriptableObject.CreateInstance<InspectorSelectionTarget>();
            Editor inspector = null;
            try
            {
                var clip = EditorFixture.Shared.Document.AllTracks.SelectMany(track => track.Clips).OfType<FeatureSampleLoop>().First();
                clip.Period = 0;
                EditorFixture.Shared.Selection.Set(Array.Empty<IDirectable>(), false);
                var before = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
                proxy.Bind(() => EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings));
                inspector = Editor.CreateEditor(proxy); window.Inspector = inspector;
                window.position = new Rect(100, 100, 450, 400); window.Show(); window.Focus(); window.Repaint();
                yield return null; yield return null;
                var point = window.Offset + new Vector2(220, 9);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                yield return null;
                Assert.That(EditorFixture.Shared.Selection.First, Is.SameAs(clip));
                Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
                Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Inspector = null;
                if (inspector != null) UnityEngine.Object.DestroyImmediate(inspector);
                window.Close(); UnityEngine.Object.DestroyImmediate(proxy);
            }
        }

        [Test] public void ValueTypePropertyCopiesKeepNestedMutableFieldsIndependent()
        {
            var factory = new NBC.ActionEditor.Infrastructure.Serialization.PropertyValueFactory(
                DocumentFormats.CreateContext(typeof(ReferenceValue), typeof(OuterValue)));
            var source = new OuterValue { Inner = new ReferenceValue { Items = new List<FeatureSampleParameter>
                { new FeatureSampleParameter { Key = "independent", Value = 7 } } } };
            var first = (OuterValue)factory.Copy(source);
            var second = (OuterValue)factory.Copy(source);
            Assert.That(first.Inner.Items, Is.Not.SameAs(source.Inner.Items));
            Assert.That(first.Inner.Items, Is.Not.SameAs(second.Inner.Items));
            Assert.That(first.Inner.Items[0], Is.Not.SameAs(second.Inner.Items[0]));
            first.Inner.Items[0].Value = 21;
            Assert.That(source.Inner.Items[0].Value, Is.EqualTo(7));
            Assert.That(second.Inner.Items[0].Value, Is.EqualTo(7));
            Assert.That(factory.Copy(new Vector3(2, 3, 4)), Is.EqualTo(new Vector3(2, 3, 4)));
        }

        [Test] public void SelectionChangeRejectsDelayedBatchWritesAndOldFieldCallbacks()
        {
            var clips = EditorFixture.Shared.Document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
            EditorFixture.Shared.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var path = presenter.CaptureInputs(clips[0]).Single(f => f.Name == "Count").Id;
            var input = presenter.BeginDraft(path);
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clips[1] }, false);
            var before = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(input, 41));
            Assert.That(input.Active, Is.False);
            Assert.That(presenter.CanInput(path), Is.False);
            Assert.Throws<InvalidOperationException>(() => presenter.Write(path, 42));
            EditorFixture.Shared.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            Assert.Throws<InvalidOperationException>(() => presenter.Write(path, 44), "Returning to the same selection must not revive old controls.");
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clips[1] }, false);
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            var current = presenter.CaptureInputs(clips[1]).Single(f => f.Name == "Count").Id;
            presenter.Write(current, 43);
            Assert.That(clips.Select(clip => clip.Count), Is.EqualTo(new[] { 3, 43 }));
            var old = presenter.BeginDraft(presenter.CaptureInputs(clips[1]).Single(f => f.Name == "Count").Id);
            EditorFixture.Shared.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clips[1] }, false);
            var replacement = presenter.BeginDraft(presenter.CaptureInputs(clips[1]).Single(f => f.Name == "Count").Id);
            Assert.That(replacement, Is.Not.SameAs(old));
            Assert.That(old.Active, Is.False);
            presenter.CommitDraft(replacement, 45);
            Assert.That(clips.Select(clip => clip.Count), Is.EqualTo(new[] { 3, 45 }));
        }

        [UnityTest] public IEnumerator NativeSelectionFieldsEditBothValuesAndNullButtonsUseOneUndo()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); yield return null;
                FeatureSampleInterval[] Clips() => EditorFixture.Shared.Document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
                var selected = Clips(); selected[1].Count = 9;
                EditorFixture.Shared.Selection.Set(selected.Cast<IDirectable>().ToArray(), false);
                var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                void Refresh() { root.Clear(); new ToolkitProperties(presenter).DrawFields(root, presenter.CaptureInputs(Clips()[0]), Refresh, 0); }
                void Submit(string text)
                {
                    var button = root.Query<Button>().ToList().Single(b => b.text == text);
                    using var submit = NavigationSubmitEvent.GetPooled(); submit.target = button; button.SendEvent(submit);
                }
                Refresh();
                var count = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                Assert.That(count.showMixedValue, Is.True);
                count.value = 23;
                Assert.That(Clips().Select(c => c.Count), Is.EqualTo(new[] { 23, 23 }));
                Undo.PerformUndo();
                Assert.That(Clips().Select(c => c.Count), Is.EqualTo(new[] { 3, 9 }));
                Undo.PerformRedo();
                Assert.That(Clips().Select(c => c.Count), Is.EqualTo(new[] { 23, 23 }));
                EditorFixture.Shared.Selection.Set(Clips().Cast<IDirectable>().ToArray(), false); Refresh();
                Submit("设为空值 标题");
                Assert.That(Clips().All(c => c.Caption == null), Is.True);
                Undo.PerformUndo();
                Assert.That(Clips().Select(c => c.Caption), Is.EqualTo(new[] { "准备", "主体" }));
                EditorFixture.Shared.Selection.Set(Clips().Cast<IDirectable>().ToArray(), false); Refresh();
                Submit("设为空值 参数列表");
                Assert.That(Clips().All(c => c.Parameters == null), Is.True);
                Undo.PerformUndo();
                Assert.That(Clips().All(c => c.Parameters.Count == 2), Is.True);
                root.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ImguiNullButtonsApplyToSelectionAndUndoRestoresBothValues()
        {
            var window = ScriptableObject.CreateInstance<NullWindow>();
            try
            {
                FeatureSampleInterval[] Clips() => EditorFixture.Shared.Document.AllTracks.First().Clips.Take(2).Cast<FeatureSampleInterval>().ToArray();
                window.position = new Rect(100, 100, 450, 250); window.Show();
                foreach (var field in new[] { "Caption", "Parameters" })
                {
                    var clips = Clips(); EditorFixture.Shared.Selection.Set(clips.Cast<IDirectable>().ToArray(), false);
                    window.Value = clips[0]; window.Field = field; window.Focus(); window.Repaint();
                    if (field == "Parameters")
                    {
                        var path = window.Inspector.Presenter.CaptureInputs(clips[0]).Single(f => f.Name == field).Id;
                        var folds = (Dictionary<object, bool>)typeof(InspectorsBase)
                            .GetField("collectionFoldouts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window.Inspector);
                        folds[path.ViewKey] = true;
                    }
                    yield return null; yield return null;
                    var point = window.Offset + window.Inspector.ButtonRect.center;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                    Assert.That(Clips().All(c => typeof(FeatureSampleInterval).GetField(field).GetValue(c) == null), Is.True,
                        field + ", button=" + window.Inspector.ButtonRect + ", pointer=" + point + ", offset=" + window.Offset);
                    Undo.PerformUndo();
                    Assert.That(Clips().All(c => typeof(FeatureSampleInterval).GetField(field).GetValue(c) != null), Is.True);
                    window.Value = null;
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Value = null; window.Close(); }
        }

        [Test] public void ClipPropertyTimingCannotBorrowAnActiveGestureOrCrossItsNextBound()
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var clip = EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            var before = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            using (var token = EditorFixture.Shared.Session.BeginEdit(this))
            {
                Assert.Throws<InvalidOperationException>(() => presenter.Timing(presenter.CaptureTarget(clip).Id, 10, 10));
                Assert.Throws<InvalidOperationException>(() => presenter.Blend(presenter.CaptureTarget(clip).Id, true, .1f));
                var field = presenter.CaptureInputs(clip).Single(f => f.Name == "Count");
                Assert.Throws<InvalidOperationException>(() => presenter.Write(field.Id, 15));
                Assert.Throws<InvalidOperationException>(() => presenter.BeginDraft(field.Id));
                Assert.Throws<InvalidOperationException>(() => presenter.Rename(presenter.CaptureTarget(clip.Parent).Id, "foreign"));
                var collection = presenter.CaptureInputs(clip).Single(f => f.Name == "Parameters");
                Assert.Throws<InvalidOperationException>(() => presenter.Append(collection.Id));
                Assert.Throws<InvalidOperationException>(() => presenter.Remove(collection.Id, 0));
                Assert.That(token.IsActive, Is.True);
                Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
                token.Commit();
            }
            var maximum = presenter.CaptureTarget(clip).Clip.Maximum;
            presenter.Timing(presenter.CaptureTarget(clip).Id, 10, 10);
            Assert.That(clip.Length, Is.GreaterThanOrEqualTo(.01f));
            Assert.That(clip.EndTime, Is.LessThanOrEqualTo(maximum));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
        }

        [Test] public void ClipPropertyRejectsNonFiniteAndInvalidStepWithoutLeavingEdit()
        {
            var clip = EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            var before = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorFixture.Shared.Clips.SetTiming(clip, float.NaN, 1, .1f));
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            clip = EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorFixture.Shared.Clips.SetTiming(clip, 0, 1, 0));
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            clip = EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            presenter.Timing(presenter.CaptureTarget(clip).Id, .2f, .4f);
            Assert.That(clip.Length, Is.EqualTo(.2f).Within(.00001f));
            Assert.That(clip.BlendIn, Is.InRange(0, clip.Length));
            Assert.That(clip.BlendOut, Is.InRange(0, clip.Length - clip.BlendIn));
            var shortened = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
            Undo.PerformRedo();
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(shortened));
            Undo.PerformUndo();

            var loop = EditorFixture.Shared.Document.AllTracks.SelectMany(t => t.Clips).OfType<FeatureSampleLoop>().First();
            EditorFixture.Shared.Clips.MatchLoop(loop, 1);
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before), "Next loop cannot cross the next start, even for cross-blend peers.");
            EditorFixture.Shared.Clips.MatchLoop(loop, -1);
            Assert.That(loop.Length, Is.EqualTo(1.5f));
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
            loop = EditorFixture.Shared.Document.AllTracks.SelectMany(t => t.Clips).OfType<FeatureSampleLoop>().First();
            loop.Period = .625f;
            EditorFixture.Shared.Clips.MatchLoop(loop, 1);
            Assert.That(loop.EndTime, Is.EqualTo(loop.GetNextSibling().StartTime), "An exact next-start boundary remains allowed.");
        }

        [Test] public void PointPropertyMovePreservesStoredLengthAndUndo()
        {
            var clip = EditorFixture.Shared.Document.AllTracks.SelectMany(t => t.Clips).OfType<FeatureSampleMarker>().First();
            var length = typeof(TimelineClip).GetField("length", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var stored = length.GetValue(clip);
            var start = clip.StartTime;
            EditorFixture.Shared.Clips.SetTiming(clip, 1, 1, .1f);
            Assert.That(clip.StartTime, Is.EqualTo(1));
            Assert.That(clip.Length, Is.Zero);
            Assert.That(length.GetValue(clip), Is.EqualTo(stored));
            Undo.PerformUndo();
            clip = EditorFixture.Shared.Document.AllTracks.SelectMany(t => t.Clips).OfType<FeatureSampleMarker>().First();
            Assert.That(clip.StartTime, Is.EqualTo(start));
            Assert.That(length.GetValue(clip), Is.EqualTo(stored));
        }

        [Test] public void PresenterDraftCancelsRejectsConcurrentChangesAndCommitsOneUndo()
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
            NBC.ActionEditor.Presentation.Properties.PropertyFieldId Field() => presenter.CaptureSelectionInputs().Single(f => f.Name == "Count").Id;
            var field = Field();
            var draft = presenter.BeginDraft(field);
            presenter.PreviewDraft(draft, "12");
            Assert.That(clip.Count, Is.EqualTo(3));
            Assert.That(EditorFixture.Shared.Session.IsEditing, Is.False);
            presenter.CancelDraft(draft);
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(draft, 12));
            draft = presenter.BeginDraft(field);
            presenter.CommitDraft(draft, 12);
            Assert.That(clip.Count, Is.EqualTo(12));
            field = Field();
            draft = presenter.BeginDraft(field);
            presenter.Write(field, 15);
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(draft, 20));
            Assert.That(clip.Count, Is.EqualTo(15));
            Undo.PerformUndo(); Undo.PerformUndo();
            Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0]).Count, Is.EqualTo(3));
        }

        [Test] public void DraftInvalidatedByLockReleasesOnlyItsOwnInput()
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            EditorFixture.Shared.Selection.Set(new IDirectable[] { clip }, false);
            var field = presenter.CaptureSelectionInputs().Single(f => f.Name == "Count").Id;
            var input = presenter.BeginDraft(field);
            clip.Parent.IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => presenter.CommitDraft(input, 21));
            Assert.That(input.Active, Is.False);
            Assert.That(clip.Count, Is.EqualTo(3));
            clip.Parent.IsLocked = false;
            var next = presenter.BeginDraft(field);
            presenter.CancelDraft(input);
            Assert.That(next.Active, Is.True);
            presenter.CommitDraft(next, 22);
            Assert.That(clip.Count, Is.EqualTo(22));
        }

        [Test] public void ComplexPropertySnapshotsDoNotExposeAuthorObjectsOrMutableCollections()
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var value = new Fields();
            var fields = presenter.CaptureInputs(value);
            var collection = (IList<NBC.ActionEditor.Presentation.Properties.PropertyInputState>)fields.Single(f => f.Name == "Values").Children;
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => collection[0] = null);
            var nested = collection[0];
            Assert.That(nested.Children.Single().Value, Is.EqualTo(3));
            value.Values[0] = new NestedValue { Count = 12 };
            Assert.That(nested.Children.Single().Value, Is.EqualTo(3));
            presenter.Write(nested.Children.Single().Id, 21);
            Assert.That(value.Values[0].Count, Is.EqualTo(21));
        }

        [Test] public void CurveDisplayAndSubmittedValueDoNotAliasAuthorData()
        {
            var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
            var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
            var before = JsonDocumentCodec.Encode(EditorFixture.Shared.Document);
            var field = presenter.CaptureInputs(clip).Single(f => f.Name == "Curve");
            var displayed = (AnimationCurve)field.Value;
            Assert.That(displayed, Is.Not.SameAs(clip.Curve));
            displayed.AddKey(.5f, 2);
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
            presenter.Write(field.Id, displayed);
            Assert.That(clip.Curve.length, Is.EqualTo(3));
            displayed.AddKey(.75f, 3);
            Assert.That(clip.Curve.length, Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(JsonDocumentCodec.Encode(EditorFixture.Shared.Document), Is.EqualTo(before));
        }

        [Test] public void AuthoredPathsUseOneUndoCancelAndRejectStaleOrLockedRoots()
        {
            FeatureSampleInterval Clip() => (FeatureSampleInterval)EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
            NBC.ActionEditor.Application.Editing.PropertyPath Path(FeatureSampleInterval clip) => new NBC.ActionEditor.Application.Editing.PropertyPath(EditorFixture.Shared.Properties, clip).Field(typeof(FeatureSampleInterval).GetField("Parameters")).Element(0).Field(typeof(FeatureSampleParameter).GetField("Value"));
            var path = Path(Clip()); var token = EditorFixture.Shared.Session.BeginEdit(this);
            path.Write(4f); path.Write(6f); token.Commit();
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            Undo.PerformUndo(); Assert.That(Clip().Parameters[0].Value, Is.EqualTo(1));
            Undo.PerformRedo(); Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => path.Write(9f));
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            token = EditorFixture.Shared.Session.BeginEdit(this); Path(Clip()).Write(12f); token.Cancel();
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
            EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => Path(Clip()).Write(15f));
            Assert.That(Clip().Parameters[0].Value, Is.EqualTo(6));
        }
        [UnityTest] public IEnumerator ExplicitNativePropertyExtensionUsesRealControlAndSharedUndo()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
                var refreshes = 0;
                var properties = new ToolkitProperties(presenter, type =>
                {
                    var adapter = ExtensionResolver.ResolveExtension(new[] { typeof(IntervalProperties) }, type, typeof(ToolkitPropertiesAttribute));
                    return adapter == null ? null : (ToolkitPropertyExtension)Activator.CreateInstance(adapter, true);
                });
                properties.Populate(root, presenter.CaptureTarget(clip), () => refreshes++);
                Assert.That(root.Query<IntegerField>().ToList().Count, Is.EqualTo(1));
                root.Q<IntegerField>("extension-count").value = 17;
                Assert.That(clip.Count, Is.EqualTo(17)); Assert.That(refreshes, Is.EqualTo(1));
                Undo.PerformUndo();
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0]).Count, Is.EqualTo(3));
                Assert.That(EditorExtensionFactory.GetToolkitProperties(typeof(FeatureSampleInterval)), Is.Null, "Injected test adapters must not enter the product registry.");
                clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
                clip.Parent.IsLocked = true;
                root.Clear(); properties.Populate(root, presenter.CaptureTarget(clip), () => refreshes++);
                Assert.That(root.Q<IntegerField>("extension-count").enabledInHierarchy, Is.False);
                root.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator ActualToolkitInspectorKeepsVectorComponentsUsableAtNarrowWidth()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            Editor editor = null;
            try
            {
                window.Show(); yield return null;
                window.Select(EditorFixture.Shared.Document.AllTracks.First().Clips[0]);
                editor = Editor.CreateEditor(Selection.activeObject);
                var view = editor.CreateInspectorGUI();
                view.style.position = Position.Absolute; view.style.right = 0; view.style.width = 280;
                view.style.height = 500; window.rootVisualElement.Add(view);
                yield return null; yield return null;
                var vector = view.Q<Vector3Field>(); Assert.That(vector, Is.Not.Null);
                foreach (var component in vector.Query<FloatField>().ToList())
                    Assert.That(component.Q(className: "unity-base-field__input").resolvedStyle.width, Is.GreaterThan(15));
                vector.value = new Vector3(7, 8, 9);
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0]).Offset, Is.EqualTo(new Vector3(7, 8, 9)));
                view.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { if (editor != null) UnityEngine.Object.DestroyImmediate(editor); window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator NativeDelayedFieldsReportStaleAndLockedInputWithoutChangingTheGraph()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0];
                new ToolkitProperties(presenter).DrawFields(root, presenter.CaptureInputs(clip), () => { }, 0);
                var field = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                clip.Parent.IsLocked = true;
                field.value = 17;
                Assert.That(clip.Count, Is.EqualTo(3));
                Assert.That(root.Q<HelpBox>(), Is.Not.Null);
                clip.Parent.IsLocked = false;
                presenter.Write(presenter.CaptureInputs(clip).Single(f => f.Name == "Count").Id, 12);
                Undo.PerformUndo();
                field.value = 19;
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Document.AllTracks.First().Clips[0]).Count, Is.EqualTo(3));
                Assert.That(root.Q<HelpBox>(), Is.Not.Null);
                root.RemoveFromHierarchy(); LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }

        [UnityTest] public IEnumerator NativeFieldsUseSharedDescriptionsAndWriteNestedValueTypes()
        {
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root); var value = new Fields();
                var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                void Refresh() { root.Clear(); new ToolkitProperties(presenter).DrawFields(root, presenter.CaptureInputs(value), Refresh, 0); }
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
            var window = ScriptableObject.CreateInstance<ToolkitWindow>();
            try
            {
                window.Show(); window.Focus(); yield return null; yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var clip = (FeatureSampleInterval)EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                var presenter = EditorFixture.Shared.CreateProperties(EditorFixture.SharedSettings);
                new ToolkitProperties(presenter).DrawFields(root, presenter.CaptureInputs(clip), () => { }, 0);
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
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                root.Clear(); clip = (FeatureSampleInterval)EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                new ToolkitProperties(presenter).DrawFields(root, presenter.CaptureInputs(clip), () => { }, 0);
                field = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                yield return null; input = field.Q("unity-text-input").Q<TextElement>(); input.Focus(); yield return null;
                Key(KeyCode.A, control: true); Key(KeyCode.Alpha5, '5');
                Assert.That(clip.Count, Is.EqualTo(3));
                window.Canvas.Focus(); yield return null;
                Assert.That(clip.Count, Is.EqualTo(5), "Blur commits delayed text once.");
                Undo.PerformUndo(); yield return null;
                Assert.That(((FeatureSampleInterval)EditorFixture.Shared.Document.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
    }
}
