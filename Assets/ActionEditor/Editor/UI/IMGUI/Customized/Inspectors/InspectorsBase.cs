using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor
{
    // IMGUI字段布局与折叠状态，不持有可写应用会话。
    public class InspectorsBase
    {
        protected object target;
        public PropertyPresenter Presenter { get; private set; }
        public void Bind(PropertyPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!ReferenceEquals(Presenter, presenter)) CancelInput();
            Presenter = presenter;
        }
        private NBC.ActionEditor.Presentation.Contracts.PropertyDraft input;
        private NBC.ActionEditor.Application.Editing.PropertyPath inputPath;
        public string InputError { get; private set; }

        private int drawDepth;
        private readonly Dictionary<NBC.ActionEditor.Application.Editing.PropertyPath, bool> collectionFoldouts =
            new Dictionary<NBC.ActionEditor.Application.Editing.PropertyPath, bool>();

        public void SetTarget(object t)
        {
            CancelInput();
            InputError = null;
            target = t;
            collectionFoldouts.Clear();
        }

        public virtual void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }

        public void DrawDefaultInspector()
        {
            DrawDefaultInspector(target);
        }

        public void DrawDefaultInspector(object obj, NBC.ActionEditor.Application.Editing.PropertyPath rootPath = null)
        {
            if (obj == null) { EditorGUILayout.LabelField("null"); return; }
            // 对象引用可能成环；与原生Toolkit相同，最多展开七层字段。
            if (drawDepth > 6) return;
            drawDepth++;
            try
            {
                foreach (var state in Presenter.CaptureFields(obj, rootPath)) DrawField(state);
            }
            finally { drawDepth--; }
        }

        protected void FieldDefaultInspector(FieldInfo field, object obj, NBC.ActionEditor.Application.Editing.PropertyPath rootPath = null)
        {
            DrawField(Presenter.Field(obj, field, rootPath));
        }

        // 字段快照已确定可见性和子字段，绘制期间不再反射作者对象。
        private void DrawField(NBC.ActionEditor.Presentation.Contracts.PropertyFieldState state)
        {
            var propertyPath = state.Path;
            var fieldType = state.Type;
            var showType = state.Type;
            var value = state.Value;
            var newValue = value;

            var description = state.Description;
            var name = state.Label;
            List<object> args = new List<object>();
            if (description.HasRange && (fieldType == typeof(float) || fieldType == typeof(int))) { showType = typeof(RangeAttribute); args.Add(description.Minimum); args.Add(description.Maximum); }
            if (description.OptionType != null) { showType = typeof(OptionParamAttribute); args = new List<object> { description.OptionType }; }
            if (description.ResourceType != null) { showType = typeof(SelectObjectPathAttribute); args = new List<object> { description.ResourceType }; }

            if (IsCollection(fieldType)) DrawCollection(state.Label, fieldType, (IList)value, propertyPath);
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
                if (labels.Length > 0)
                {
                    var currentIndex = state.OptionIndex;
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup(name, Mathf.Max(0, currentIndex), labels);
                    if (EditorGUI.EndChangeCheck()) newValue = state.OptionValue(selected);
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
            else if (value != null)
            {
                EditorGUILayout.LabelField(name);
                EditorGUI.indentLevel++;
                try { DrawDefaultInspector(value, propertyPath); }
                finally { EditorGUI.indentLevel--; }
            }
            else
            {
                EditorGUILayout.LabelField(name, "null");
                if (GUILayout.Button("创建 " + fieldType.Name)) newValue = Presenter.CreateElement(fieldType);
            }

            if (!Equals(value, newValue))
                Presenter.Write(propertyPath, newValue);
        }

        public object CreateElement(Type type) => Presenter.CreateElement(type);

        // 原生控件保留文本与焦点，作者提交和失效检查由Presenter持有。
        private void DrawDelayed(string label, Type type, object value, NBC.ActionEditor.Application.Editing.PropertyPath path)
        {
            // 撤销或换图后旧控件可能仍收到最后一次绘制，不能重新领取失效输入。
            if (!Presenter.CanInput(path))
            {
                if (inputPath != null && inputPath.SameLocation(path)) CancelInput();
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
                    if (input == null || !inputPath.SameLocation(path))
                    { input = Presenter.BeginDraft(path); inputPath = path; }
                    var committed = input;
                    input = null; inputPath = null;
                    Presenter.CommitDraft(committed, next);
                    InputError = null;
                }
                else if (focused && (input == null || !inputPath.SameLocation(path)))
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
            input = null; inputPath = null;
        }

        private object DrawElement(Type type, object value, NBC.ActionEditor.Application.Editing.PropertyPath propertyPath)
        {
            if (IsCollection(type)) { DrawCollection("Items", type, (IList)value, propertyPath); return value; }
            if (type == typeof(string) || type == typeof(int) || type == typeof(float))
            { DrawDelayed("", type, value, propertyPath); return value; }
            if (type == typeof(bool)) return EditorGUILayout.Toggle(value != null && (bool)value);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(value == null ? Color.white : (Color)value);
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field("", value == null ? Vector2.zero : (Vector2)value);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field("", value == null ? Vector3.zero : (Vector3)value);
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field("", value == null ? Vector4.zero : (Vector4)value);
            if (type == typeof(Vector2Int)) return EditorGUILayout.Vector2IntField("", value == null ? Vector2Int.zero : (Vector2Int)value);
            if (type == typeof(Vector3Int)) return EditorGUILayout.Vector3IntField("", value == null ? Vector3Int.zero : (Vector3Int)value);
            if (type == typeof(Rect)) return EditorGUILayout.RectField(value == null ? default : (Rect)value);
            if (type == typeof(RectInt)) return EditorGUILayout.RectIntField(value == null ? default : (RectInt)value);
            if (type == typeof(Bounds)) return EditorGUILayout.BoundsField(value == null ? default : (Bounds)value);
            if (type == typeof(BoundsInt)) return EditorGUILayout.BoundsIntField(value == null ? default : (BoundsInt)value);
            if (type == typeof(AnimationCurve))
            {
                EditorGUI.BeginChangeCheck();
                var copy = (AnimationCurve)NBC.ActionEditor.Presentation.Contracts.PropertyFieldState.CopyValue(value);
                var curve = EditorGUILayout.CurveField(copy ?? new AnimationCurve());
                return EditorGUI.EndChangeCheck() ? curve : value;
            }
            if (type.IsEnum) return EditorGUILayout.EnumPopup((Enum)(value ?? Activator.CreateInstance(type)));
            if (typeof(Object).IsAssignableFrom(type)) return EditorGUILayout.ObjectField((Object)value, type, false);
            if (value == null)
            {
                EditorGUILayout.LabelField("null (" + type.Name + ")");
                if (GUILayout.Button("创建元素")) return CreateElement(type);
                return null;
            }
            DrawDefaultInspector(value, propertyPath); return value;
        }

        protected virtual bool DrawRemoveButton() => GUILayout.Button("X", GUILayout.Width(20));
        protected virtual bool DrawAddButton() => GUILayout.Button("+", GUILayout.Width(30));
        protected void SetExpanded(NBC.ActionEditor.Application.Editing.PropertyPath path, bool expanded) => collectionFoldouts[path] = expanded;

        private static bool IsCollection(Type type) => type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        // 数组与列表共享原生布局，删除后结束本次枚举，递归集合也使用稳定字段路径。
        private void DrawCollection(string label, Type type, IList values, NBC.ActionEditor.Application.Editing.PropertyPath path)
        {
            if (drawDepth > 6) return;
            var expanded = EditorGUILayout.Foldout(GetCollectionFoldout(path), label);
            collectionFoldouts[path] = expanded;
            if (!expanded) return;
            var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            drawDepth++;
            try
            {
                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    for (var index = 0; index < (values?.Count ?? 0); index++)
                    {
                        bool remove;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Element " + index);
                            remove = DrawRemoveButton();
                        }
                        if (remove) { Presenter.Remove(path, type, index); break; }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(12);
                            using (new EditorGUILayout.VerticalScope())
                            {
                                var value = values[index];
                                var elementPath = path.Element(index);
                                var updated = DrawElement(elementType, value, elementPath);
                                if (!Equals(value, updated)) Presenter.Write(elementPath, updated);
                            }
                        }
                        DrawDivider();
                    }
                    GUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (DrawAddButton()) Presenter.Append(path, type);
                    }
                }
            }
            finally { drawDepth--; }
        }

        private bool GetCollectionFoldout(NBC.ActionEditor.Application.Editing.PropertyPath path)
        {
            return collectionFoldouts.TryGetValue(path, out var expanded) && expanded;
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