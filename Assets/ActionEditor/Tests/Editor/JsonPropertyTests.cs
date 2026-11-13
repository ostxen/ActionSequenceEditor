using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Samples.LegacyExperience;
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
        private string recovery;
        [SetUp] public void SetUp()
        {
            previous = App.TextAsset; recovery = App.Session.CaptureRecovery();
            fixture = new TextAsset(JsonDocumentCodec.Encode(new LegacyExperienceAsset())); App.TextAsset = fixture;
        }
        [TearDown] public void TearDown()
        {
            App.Session.CancelEdit(); App.TextAsset = previous; App.Session.RestoreRecovery(recovery);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
        [Test] public void DescriptionsCacheConditionsAndRealOptionValuesWithoutChangingOrder()
        {
            var value = new Fields(); var fields = JsonFieldDescription.For(typeof(Fields));
            Assert.That(JsonFieldDescription.For(typeof(Fields)), Is.SameAs(fields));
            Assert.That(fields.Select(d => d.Field.Name), Is.EqualTo(typeof(Fields).GetFields().Select(f => f.Name)));
            var options = fields.Single(d => d.Field.Name == "Mode");
            Assert.That(options.Constants.Select(f => f.GetRawConstantValue()), Is.EqualTo(new object[] { 7, 20 }));
            Assert.That(options.OptionIndex(20), Is.EqualTo(1), "The old popup treated authored value20 as a popup index, not a constant value.");
            Assert.That(options.OptionIndex(99), Is.EqualTo(-1));
            var conditional = fields.Single(d => d.Field.Name == "Conditional");
            Assert.That(conditional.Visible(value), Is.False); value.Mode = 20; Assert.That(conditional.Visible(value), Is.True);
            Assert.That(fields.Single(d => d.Field.Name == "Hidden").Visible(value), Is.False);
        }
        [Test] public void PathsWriteNestedStructsAndReResolveCollectionIndices()
        {
            var value = new Fields(); var root = new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, value);
            var field = typeof(NestedValue).GetField("Count");
            root.Field(typeof(Fields).GetField("Value")).Field(field).Set(8);
            Assert.That(value.Value.Count, Is.EqualTo(8));
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
        [Test] public void AuthoredPathsUseOneUndoCancelAndRejectStaleOrLockedRoots()
        {
            LegacyExperienceInterval Clip() => (LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
            NBC.ActionEditor.Application.Editing.PropertyPath Path(LegacyExperienceInterval clip) => new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, clip).Field(typeof(LegacyExperienceInterval).GetField("Parameters")).Element(0).Field(typeof(LegacyExperienceParameter).GetField("Value"));
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
        [UnityTest] public IEnumerator NativeFieldsUseSharedDescriptionsAndWriteNestedValueTypes()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.Show(); yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root); var value = new Fields();
                void Refresh() { root.Clear(); UIToolkitPropertyView.DrawFields(root, value, Refresh, 0); }
                Refresh();
                var popup = root.Q<PopupField<string>>(); Assert.That(popup.value, Is.EqualTo("First")); popup.index = 1;
                Assert.That(value.Mode, Is.EqualTo(20));
                Assert.That(root.Query<TextField>().ToList().Any(f => f.label == "Conditional"), Is.True);
                root.Query<IntegerField>().ToList().First(f => f.label == "Count").value = 42;
                Assert.That(value.Value.Count, Is.EqualTo(42));
                Assert.That(root.Query<IntegerField>().ToList().Any(f => f.label == "Hidden"), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
        [UnityTest] public IEnumerator NativeDelayedInputKeepsIntermediateTextUntilReturnAndEscapeCancels()
        {
            var window = ScriptableObject.CreateInstance<UIToolkitActionEditorWindow>();
            try
            {
                window.Show(); window.Focus(); yield return null; yield return null;
                var root = new VisualElement(); window.rootVisualElement.Add(root);
                var clip = (LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                UIToolkitPropertyView.DrawFields(root, clip, () => { }, 0);
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
                Assert.That(((LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                root.Clear(); clip = (LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0];
                UIToolkitPropertyView.DrawFields(root, clip, () => { }, 0);
                field = root.Query<IntegerField>().ToList().Single(f => f.label == "次数");
                yield return null; input = field.Q("unity-text-input").Q<TextElement>(); input.Focus(); yield return null;
                Key(KeyCode.A, control: true); Key(KeyCode.Alpha5, '5');
                Assert.That(clip.Count, Is.EqualTo(3));
                window.Canvas.Focus(); yield return null;
                Assert.That(clip.Count, Is.EqualTo(5), "Blur commits delayed text once.");
                Undo.PerformUndo(); yield return null;
                Assert.That(((LegacyExperienceInterval)App.AssetData.RootItems.OfType<TimelineGroup>().ElementAt(0).Tracks[0].Clips[0]).Count, Is.EqualTo(3));
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.DiscardChanges(); window.Close(); }
        }
    }
}
