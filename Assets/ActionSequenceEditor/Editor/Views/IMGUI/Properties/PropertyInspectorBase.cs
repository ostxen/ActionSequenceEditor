using ActionSequenceEditor.Presentation.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using ActionSequenceEditor.Presentation.Presenters;

namespace ActionSequenceEditor.Views.IMGUI.Properties
{
    // IMGUI字段布局与折叠状态，不持有可写应用会话。
    public class PropertyInspectorBase
    {
        protected PropertyTargetState target;
        public PropertyPresenter Presenter { get; private set; }
        public void Bind(PropertyPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!ReferenceEquals(Presenter, presenter)) CancelInput();
            Presenter = presenter;
        }
        private ActionSequenceEditor.Presentation.Properties.PropertyDraft draft;
        private PropertyFieldId draftFieldId;
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
        private void DrawField(PropertyInputState state, bool isCollectionElement = false)
        {
            var mixed = EditorGUI.showMixedValue;
            try { EditorGUI.showMixedValue = state.Mixed; DrawFieldValue(state, isCollectionElement); }
            finally { EditorGUI.showMixedValue = mixed; }
        }
        private void DrawFieldValue(PropertyInputState state, bool isCollectionElement)
        {
            var fieldId = state.Id;
            var fieldType = state.Type;
            var controlType = state.Type;
            var value = state.Value;
            var newValue = value;

            var fieldState = state;
            var name = isCollectionElement ? "" : state.Label;
            List<object> controlArguments = new List<object>();
            if (fieldState.HasRange && (fieldType == typeof(float) || fieldType == typeof(int))) { controlType = typeof(RangeAttribute); controlArguments.Add(fieldState.Minimum); controlArguments.Add(fieldState.Maximum); }
            if (fieldState.HasOptions) controlType = typeof(OptionParamAttribute);
            if (fieldState.ResourceType != null) { controlType = typeof(SelectObjectPathAttribute); controlArguments = new List<object> { fieldState.ResourceType }; }

            if (IsCollection(fieldType)) DrawCollection(state, isCollectionElement ? "Items" : state.Label);
            else if (controlType == typeof(int))
            {
                DrawDelayed(name, fieldType, value, fieldId);
            }
            else if (controlType == typeof(float))
            {
                DrawDelayed(name, fieldType, value, fieldId);
            }
            else if (controlType == typeof(bool))
            {
                newValue = EditorGUILayout.Toggle(name, (bool)value);
            }
            else if (controlType == typeof(string))
            {
                if (!isCollectionElement && value != null && DrawNullButton(name)) Presenter.Write(fieldId, null);
                DrawDelayed(name, fieldType, value, fieldId);
            }
            else if (controlType == typeof(Color))
            {
                newValue = EditorGUILayout.ColorField(name, (Color)value);
            }
            else if (typeof(Object).IsAssignableFrom(controlType))
            {
                newValue = EditorGUILayout.ObjectField(name, (Object)value, fieldType, false);
            }
            else if (controlType.IsEnum)
            {
                newValue = EditorGUILayout.EnumPopup(name, (Enum)value);
            }
            else if (controlType == typeof(AnimationCurve))
            {
                EditorGUI.BeginChangeCheck();
                var curve = EditorGUILayout.CurveField(name, (AnimationCurve)value ?? new AnimationCurve());
                if (EditorGUI.EndChangeCheck()) Presenter.Write(fieldId, curve);
                return;
            }
            else if (controlType == typeof(Vector2))
            {
                newValue = EditorGUILayout.Vector2Field(name, (Vector2)value);
            }
            else if (controlType == typeof(Vector3))
            {
                newValue = EditorGUILayout.Vector3Field(name, (Vector3)value);
            }
            else if (controlType == typeof(Vector4))
            {
                newValue = EditorGUILayout.Vector4Field(name, (Vector4)value);
            }
            else if (controlType == typeof(Vector2Int))
            {
                newValue = EditorGUILayout.Vector2IntField(name, (Vector2Int)value);
            }
            else if (controlType == typeof(Vector3Int))
            {
                newValue = EditorGUILayout.Vector3IntField(name, (Vector3Int)value);
            }
            else if (controlType == typeof(Rect))
            {
                newValue = EditorGUILayout.RectField(name, (Rect)value);
            }
            else if (controlType == typeof(RectInt))
            {
                newValue = EditorGUILayout.RectIntField(name, (RectInt)value);
            }
            else if (controlType == typeof(Bounds))
            {
                newValue = EditorGUILayout.BoundsField(name, (Bounds)value);
            }
            else if (controlType == typeof(BoundsInt))
            {
                newValue = EditorGUILayout.BoundsIntField(name, (BoundsInt)value);
            }
            else if (controlType == typeof(RangeAttribute))
            {
                if (fieldType == typeof(float))
                {
                    newValue = EditorGUILayout.Slider(name, (float)value, (float)controlArguments[0], (float)controlArguments[1]);
                }
                else newValue = EditorGUILayout.IntSlider(name, (int)value, Mathf.CeilToInt((float)controlArguments[0]), Mathf.FloorToInt((float)controlArguments[1]));
            }
            else if (controlType == typeof(OptionParamAttribute))
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
            else if (controlType == typeof(SelectObjectPathAttribute))
            {
                if (controlArguments[0] is Type type)
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
                if (!isCollectionElement) { EditorGUILayout.LabelField(name); EditorGUI.indentLevel++; }
                try { DrawFields(state.Children); }
                finally { if (!isCollectionElement) EditorGUI.indentLevel--; }
            }
            else
            {
                if (isCollectionElement) EditorGUILayout.LabelField("null (" + fieldType.Name + ")");
                else EditorGUILayout.LabelField(name, "null");
                if (GUILayout.Button(isCollectionElement ? "创建元素" : "创建 " + fieldType.Name)) Presenter.CreateValue(state.Id);
            }

            if (!Equals(value, newValue))
                Presenter.Write(fieldId, newValue);
        }


        // 原生控件保留文本与焦点，作者提交和失效检查由Presenter持有。
        private void DrawDelayed(string label, Type type, object value, PropertyFieldId fieldId)
        {
            // 撤销或换图后旧控件可能仍收到最后一次绘制，不能重新领取失效输入。
            if (!Presenter.CanInput(fieldId))
            {
                if (draft != null && draftFieldId.Equals(fieldId)) CancelInput();
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
                    if (draft == null || !ReferenceEquals(draftFieldId.ViewKey, fieldId.ViewKey))
                    { draft = Presenter.BeginDraft(fieldId); draftFieldId = fieldId; }
                    var draftToCommit = draft;
                    draft = null; draftFieldId = default;
                    Presenter.CommitDraft(draftToCommit, next);
                    InputError = null;
                }
                else if (focused && (draft == null || !draftFieldId.Equals(fieldId)))
                { draft = Presenter.BeginDraft(fieldId); draftFieldId = fieldId; }
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                CancelInput(); InputError = error.Message;
            }
            if (InputError != null) EditorGUILayout.HelpBox(InputError, MessageType.Warning);
        }

        public void CancelInput()
        {
            if (draft != null) Presenter?.CancelDraft(draft);
            draft = null; draftFieldId = default;
        }

        protected virtual bool DrawRemoveButton() => GUILayout.Button("X", GUILayout.Width(20));
        protected virtual bool DrawAddButton() => GUILayout.Button("+", GUILayout.Width(30));
        protected void SetExpanded(PropertyFieldId fieldId, bool expanded) => collectionFoldouts[fieldId.ViewKey] = expanded;

        private static bool IsCollection(Type type) => type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        // 数组与列表共享原生布局，删除后结束本次枚举，递归集合也使用稳定字段路径。
        private void DrawCollection(PropertyInputState state, string label)
        {
            if (drawDepth > 6) return;
            var fieldId = state.Id;
            var expanded = EditorGUILayout.Foldout(GetCollectionFoldout(fieldId), label);
            collectionFoldouts[fieldId.ViewKey] = expanded;
            if (!expanded) return;
            if (!state.IsNull && DrawNullButton(label)) { Presenter.Write(fieldId, null); return; }
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
                        if (remove) { Presenter.Remove(fieldId, index); return; }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(12);
                            using (new EditorGUILayout.VerticalScope()) DrawField(state.Children[index], true);
                        }
                        if (!Presenter.CanInput(fieldId)) return;
                        DrawDivider();
                    }
                    GUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (DrawAddButton()) Presenter.Append(fieldId);
                    }
                }
            }
            finally { drawDepth--; }
        }

        protected virtual bool DrawNullButton(string label) => GUILayout.Button("设为空值 " + label);

        private bool GetCollectionFoldout(PropertyFieldId fieldId)
        {
            return collectionFoldouts.TryGetValue(fieldId.ViewKey, out var expanded) && expanded;
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