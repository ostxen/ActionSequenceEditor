using NBC.ActionEditor.Presentation.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Views.IMGUI.Properties
{
    // IMGUI字段布局与折叠状态，不持有可写应用会话。
    public class InspectorsBase
    {
        protected PropertyTargetState target;
        public PropertyPresenter Presenter { get; private set; }
        public void Bind(PropertyPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!ReferenceEquals(Presenter, presenter)) CancelInput();
            Presenter = presenter;
        }
        private NBC.ActionEditor.Presentation.Properties.PropertyDraft input;
        private PropertyFieldId inputPath;
        public string InputError { get; private set; }

        private int drawDepth;
        private readonly Dictionary<object, bool> collectionFoldouts =
            new Dictionary<object, bool>();

        public void SetTarget(PropertyTargetState t)
        {
            if (!ReferenceEquals(target?.Id.ViewKey, t?.Id.ViewKey))
            { CancelInput(); InputError = null; collectionFoldouts.Clear(); }
            target = t;
        }

        public virtual void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }

        public void DrawDefaultInspector()
        {
            DrawDefaultInspector(target);
        }

        public void DrawDefaultInspector(PropertyTargetState value) => DrawFields(value?.Fields);
        private void DrawFields(IReadOnlyList<PropertyInputState> fields)
        {
            if (fields == null || drawDepth > 6) return;
            drawDepth++;
            try { foreach (var state in fields) DrawField(state); }
            finally { drawDepth--; }
        }
        protected void FieldDefaultInspector(PropertyInputState field) => DrawField(field);

        // 字段快照已确定可见性和子字段，绘制期间不再反射作者对象。
        private void DrawField(PropertyInputState state, bool element = false)
        {
            var mixed = EditorGUI.showMixedValue;
            try { EditorGUI.showMixedValue = state.Mixed; DrawFieldValue(state, element); }
            finally { EditorGUI.showMixedValue = mixed; }
        }
        private void DrawFieldValue(PropertyInputState state, bool element)
        {
            var propertyPath = state.Id;
            var fieldType = state.Type;
            var showType = state.Type;
            var value = state.Value;
            var newValue = value;

            var description = state;
            var name = element ? "" : state.Label;
            List<object> args = new List<object>();
            if (description.HasRange && (fieldType == typeof(float) || fieldType == typeof(int))) { showType = typeof(RangeAttribute); args.Add(description.Minimum); args.Add(description.Maximum); }
            if (description.HasOptions) showType = typeof(OptionParamAttribute);
            if (description.ResourceType != null) { showType = typeof(SelectObjectPathAttribute); args = new List<object> { description.ResourceType }; }

            if (IsCollection(fieldType)) DrawCollection(state, element ? "Items" : state.Label);
            else if (showType == typeof(int))
            {
                DrawDelayed(name, fieldType, value, propertyPath);
            }
            else if (showType == typeof(float))
            {
                DrawDelayed(name, fieldType, value, propertyPath);
            }
            else if (showType == typeof(bool))
            {
                newValue = EditorGUILayout.Toggle(name, (bool)value);
            }
            else if (showType == typeof(string))
            {
                if (!element && value != null && DrawNullButton(name)) Presenter.Write(propertyPath, null);
                DrawDelayed(name, fieldType, value, propertyPath);
            }
            else if (showType == typeof(Color))
            {
                newValue = EditorGUILayout.ColorField(name, (Color)value);
            }
            else if (typeof(Object).IsAssignableFrom(showType))
            {
                newValue = EditorGUILayout.ObjectField(name, (Object)value, fieldType, false);
            }
            else if (showType.IsEnum)
            {
                newValue = EditorGUILayout.EnumPopup(name, (Enum)value);
            }
            else if (showType == typeof(AnimationCurve))
            {
                EditorGUI.BeginChangeCheck();
                var curve = EditorGUILayout.CurveField(name, (AnimationCurve)value ?? new AnimationCurve());
                if (EditorGUI.EndChangeCheck()) Presenter.Write(propertyPath, curve);
                return;
            }
            else if (showType == typeof(Vector2))
            {
                newValue = EditorGUILayout.Vector2Field(name, (Vector2)value);
            }
            else if (showType == typeof(Vector3))
            {
                newValue = EditorGUILayout.Vector3Field(name, (Vector3)value);
            }
            else if (showType == typeof(Vector4))
            {
                newValue = EditorGUILayout.Vector4Field(name, (Vector4)value);
            }
            else if (showType == typeof(Vector2Int))
            {
                newValue = EditorGUILayout.Vector2IntField(name, (Vector2Int)value);
            }
            else if (showType == typeof(Vector3Int))
            {
                newValue = EditorGUILayout.Vector3IntField(name, (Vector3Int)value);
            }
            else if (showType == typeof(Rect))
            {
                newValue = EditorGUILayout.RectField(name, (Rect)value);
            }
            else if (showType == typeof(RectInt))
            {
                newValue = EditorGUILayout.RectIntField(name, (RectInt)value);
            }
            else if (showType == typeof(Bounds))
            {
                newValue = EditorGUILayout.BoundsField(name, (Bounds)value);
            }
            else if (showType == typeof(BoundsInt))
            {
                newValue = EditorGUILayout.BoundsIntField(name, (BoundsInt)value);
            }
            else if (showType == typeof(RangeAttribute))
            {
                if (fieldType == typeof(float))
                {
                    newValue = EditorGUILayout.Slider(name, (float)value, (float)args[0], (float)args[1]);
                }
                else newValue = EditorGUILayout.IntSlider(name, (int)value, Mathf.CeilToInt((float)args[0]), Mathf.FloorToInt((float)args[1]));
            }
            else if (showType == typeof(OptionParamAttribute))
            {
                var labels = state.OptionLabels;
                if (labels.Count > 0)
                {
                    var currentIndex = state.OptionIndex;
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup(name, Mathf.Max(0, currentIndex), labels.ToArray());
                    if (EditorGUI.EndChangeCheck()) Presenter.SelectOption(state.Id, selected);
                }
            }
            else if (showType == typeof(SelectObjectPathAttribute))
            {
                if (args[0] is Type type)
                {
                    Object o = null;
                    var path = value as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        o = AssetDatabase.LoadAssetAtPath(path, type);
                    }

                    var newObj = EditorGUILayout.ObjectField(name, o, type, false);
                    if (newObj != o)
                    {
                        newValue = AssetDatabase.GetAssetPath(newObj);
                    }
                }
            }
            else if (!state.IsNull)
            {
                if (!element) { EditorGUILayout.LabelField(name); EditorGUI.indentLevel++; }
                try { DrawFields(state.Children); }
                finally { if (!element) EditorGUI.indentLevel--; }
            }
            else
            {
                if (element) EditorGUILayout.LabelField("null (" + fieldType.Name + ")");
                else EditorGUILayout.LabelField(name, "null");
                if (GUILayout.Button(element ? "创建元素" : "创建 " + fieldType.Name)) Presenter.CreateValue(state.Id);
            }

            if (!Equals(value, newValue))
                Presenter.Write(propertyPath, newValue);
        }


        // 原生控件保留文本与焦点，作者提交和失效检查由Presenter持有。
        private void DrawDelayed(string label, Type type, object value, PropertyFieldId path)
        {
            // 撤销或换图后旧控件可能仍收到最后一次绘制，不能重新领取失效输入。
            if (!Presenter.CanInput(path))
            {
                if (input != null && inputPath.Equals(path)) CancelInput();
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField(label, value?.ToString() ?? "");
                return;
            }
            var name = "property-input-" + GUIUtility.GetControlID(FocusType.Passive);
            var escape = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            GUI.SetNextControlName(name);
            object next;
            if (type == typeof(int)) next = EditorGUILayout.DelayedIntField(label, value == null ? 0 : (int)value);
            else if (type == typeof(float)) next = EditorGUILayout.DelayedFloatField(label, value == null ? 0 : (float)value);
            else next = EditorGUILayout.DelayedTextField(label, value as string ?? "");
            var focused = GUI.GetNameOfFocusedControl() == name;
            if (escape && focused) { CancelInput(); return; }
            try
            {
                if (!Equals(next, value))
                {
                    if (input == null || !ReferenceEquals(inputPath.ViewKey, path.ViewKey))
                    { input = Presenter.BeginDraft(path); inputPath = path; }
                    var committed = input;
                    input = null; inputPath = default;
                    Presenter.CommitDraft(committed, next);
                    InputError = null;
                }
                else if (focused && (input == null || !inputPath.Equals(path)))
                { input = Presenter.BeginDraft(path); inputPath = path; }
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                CancelInput(); InputError = error.Message;
            }
            if (InputError != null) EditorGUILayout.HelpBox(InputError, MessageType.Warning);
        }

        public void CancelInput()
        {
            if (input != null) Presenter?.CancelDraft(input);
            input = null; inputPath = default;
        }

        protected virtual bool DrawRemoveButton() => GUILayout.Button("X", GUILayout.Width(20));
        protected virtual bool DrawAddButton() => GUILayout.Button("+", GUILayout.Width(30));
        protected void SetExpanded(PropertyFieldId path, bool expanded) => collectionFoldouts[path.ViewKey] = expanded;

        private static bool IsCollection(Type type) => type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        // 数组与列表共享原生布局，删除后结束本次枚举，递归集合也使用稳定字段路径。
        private void DrawCollection(PropertyInputState state, string label)
        {
            if (drawDepth > 6) return;
            var path = state.Id;
            var expanded = EditorGUILayout.Foldout(GetCollectionFoldout(path), label);
            collectionFoldouts[path.ViewKey] = expanded;
            if (!expanded) return;
            if (!state.IsNull && DrawNullButton(label)) { Presenter.Write(path, null); return; }
            drawDepth++;
            try
            {
                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    for (var index = 0; index < state.Children.Count; index++)
                    {
                        bool remove;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Element " + index);
                            remove = DrawRemoveButton();
                        }
                        if (remove) { Presenter.Remove(path, index); return; }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(12);
                            using (new EditorGUILayout.VerticalScope()) DrawField(state.Children[index], true);
                        }
                        if (!Presenter.CanInput(path)) return;
                        DrawDivider();
                    }
                    GUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (DrawAddButton()) Presenter.Append(path);
                    }
                }
            }
            finally { drawDepth--; }
        }

        protected virtual bool DrawNullButton(string label) => GUILayout.Button("设为空值 " + label);

        private bool GetCollectionFoldout(PropertyFieldId path)
        {
            return collectionFoldouts.TryGetValue(path.ViewKey, out var expanded) && expanded;
        }

        private void DrawDivider()
        {
            GUILayout.Space(2);
            Color color = Color.black.WithAlpha(0.1f);
            Rect rect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(rect, color);
            GUILayout.Space(2);
        }
    }
}