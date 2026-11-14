using NBC.ActionEditor.Views.IMGUI;
using NBC.ActionEditor.Views.IMGUI.Properties;
using ImguiWindow = NBC.ActionEditor.Views.IMGUI.ActionEditorWindow;
using ToolkitWindow = NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow;
using ToolkitPreferences = NBC.ActionEditor.Views.UIToolkit.PreferencesWindow;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Presentation.Timeline;
using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NBC.ActionEditor.Samples.FeatureSample;
using NBC.ActionEditor.Storage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Tests
{
    public sealed class PropertyControlsTests : SharedContextFixture
    {
        private sealed class CollectionFixture
        {
            public System.Collections.Generic.List<string> Values = new System.Collections.Generic.List<string> { "one", "two" };
            public int[] Numbers = { 1, 2 };
            public string[] Empty;
            public System.Collections.Generic.List<int> Missing;
        }

        private sealed class CollectionInspector : InspectorsBase
        {
            public Rect RemoveRect, AddRect;
            protected override bool DrawAddButton()
            {
                var clicked = base.DrawAddButton();
                if (Event.current.type == EventType.Repaint) AddRect = GUILayoutUtility.GetLastRect();
                return clicked;
            }
            protected override bool DrawRemoveButton()
            {
                var clicked = base.DrawRemoveButton();
                if (Event.current.type == EventType.Repaint && RemoveRect.width == 0) RemoveRect = GUILayoutUtility.GetLastRect();
                return clicked;
            }
            public void Expand(CollectionFixture value, string name)
            {
                if (Presenter == null) Bind(Composition.PropertyComposition.Create(ActionEditorHost.Context, Prefs.Session));
                SetExpanded(Presenter.Path(value).Field(typeof(CollectionFixture).GetField(name)), true);
            }
            public void DrawField(CollectionFixture value, string name)
            { if (Presenter == null) Bind(Composition.PropertyComposition.Create(ActionEditorHost.Context, Prefs.Session)); FieldDefaultInspector(typeof(CollectionFixture).GetField(name), value); }
        }

        private sealed class CollectionWindow : EditorWindow
        {
            public CollectionFixture Value;
            public CollectionInspector Inspector;
            public string Field;
            public Vector2 ContentOffset;
            private void OnGUI()
            {
                ContentOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
                if (Value != null) Inspector.DrawField(Value, Field);
            }
        }

        [UnityTest] public IEnumerator ImguiCollectionDeleteClicksKeepGUILayoutBalancedAndNullArrayUnchanged()
        {
            var window = ScriptableObject.CreateInstance<CollectionWindow>();
            try
            {
                window.Value = new CollectionFixture(); window.Inspector = new CollectionInspector();
                window.Show(); window.position = new Rect(80, 80, 400, 400);
                foreach (var name in new[] { "Values", "Numbers" })
                {
                    window.Field = name; var value = typeof(CollectionFixture).GetField(name).GetValue(window.Value);
                    window.Inspector.Expand(window.Value, name); window.Inspector.RemoveRect = default;
                    window.Focus(); window.Repaint(); yield return null; yield return null; yield return null;
                    var button = window.Inspector.RemoveRect; Assert.That(button.width, Is.GreaterThan(0));
                    var point = button.center + window.ContentOffset;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                    yield return null;
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                    window.Repaint(); yield return null; yield return null;
                    Assert.That(((System.Collections.IList)typeof(CollectionFixture).GetField(name).GetValue(window.Value)).Count, Is.EqualTo(1), name + " button=" + button);
                }
                window.Field = "Empty"; window.Repaint(); yield return null; yield return null;
                Assert.That(window.Value.Empty, Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [UnityTest] public IEnumerator NullCollectionFoldoutsPersistAndFirstItemCanBeCreated()
        {
            var window = ScriptableObject.CreateInstance<CollectionWindow>();
            try
            {
                window.Value = new CollectionFixture(); window.Inspector = new CollectionInspector();
                window.Show(); window.position = new Rect(80, 80, 400, 400);
                foreach (var field in new[] { "Empty", "Missing" })
                {
                    window.Field = field; window.Inspector.AddRect = default;
                    window.Focus(); window.Repaint(); yield return null; yield return null;
                    Assert.That(window.Inspector.AddRect.width, Is.Zero, "Expanding another null field must not expand this field.");
                    var foldout = new Vector2(8, 8) + window.ContentOffset;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = foldout });
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = foldout });
                    window.Repaint(); yield return null; yield return null;
                    Assert.That(window.Inspector.AddRect.width, Is.GreaterThan(0), field + " plus button must remain visible after repaint.");
                    var add = window.Inspector.AddRect.center + window.ContentOffset;
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = add });
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = add });
                    window.Repaint(); yield return null; yield return null;
                    var value = typeof(CollectionFixture).GetField(field).GetValue(window.Value) as System.Collections.IList;
                    Assert.That(value, Is.Not.Null, field + " must retain expansion until the plus button can be clicked.");
                    Assert.That(value.Count, Is.EqualTo(1));
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [TestCase(typeof(string), "")]
        [TestCase(typeof(int), 0)]
        public void CollectionDefaultCreationSupportsPrimitives(Type type, object expected) => Assert.That(Composition.PropertyComposition.Create(ActionEditorHost.Context, Prefs.Session).CreateElement(type), Is.EqualTo(expected));
    }
}
