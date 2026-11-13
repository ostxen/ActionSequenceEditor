using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor
{
    public class InspectorsBase
    {
        protected object target;

        private Dictionary<int, bool> _unfoldDictionary = new Dictionary<int, bool>();
        private System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<FieldInfo, bool>> _collectionFoldouts =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<FieldInfo, bool>>();

        public void SetTarget(object t)
        {
            target = t;
            _unfoldDictionary.Clear();
            _collectionFoldouts = new System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<FieldInfo, bool>>();
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
            var descriptions = JsonFieldDescription.For(obj.GetType()).ToArray();
            Array.Sort(descriptions, (first, second) => FieldsSprtBy(first.Field, second.Field));
            foreach (var description in descriptions)
                if (description.Visible(obj)) FieldDefaultInspector(description.Field, obj, rootPath);
        }

        protected void FieldDefaultInspector(FieldInfo field, object obj, NBC.ActionEditor.Application.Editing.PropertyPath rootPath = null)
        {
            var propertyPath = (rootPath ?? new NBC.ActionEditor.Application.Editing.PropertyPath(App.Context.Properties, obj)).Field(field);
            var fieldType = field.FieldType;
            var showType = field.FieldType;
            var value = field.GetValue(obj);
            var newValue = value;

            var description = JsonFieldDescription.For(field);
            var name = description.Label ?? System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(field.Name);
            List<object> args = new List<object>();
            if (description.Range != null && fieldType == typeof(float)) { showType = typeof(RangeAttribute); args.Add(description.Range.min); args.Add(description.Range.max); }
            if (description.Options != null) { showType = typeof(OptionParamAttribute); args = new List<object> { description.Options.classType }; }
            if (description.ResourcePath != null) { showType = typeof(SelectObjectPathAttribute); args = new List<object> { description.ResourcePath.type }; }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = fieldType.GetGenericArguments()[0];
                IList list = (IList)value;
                var foldout = EditorGUILayout.Foldout(GetCollectionFoldout(obj, field, value), field.GetShowName());
                if (foldout)
                {
                    GUILayout.Space(6);
                    GUILayout.BeginVertical(GUI.skin.box);
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            object listItem = list[i];
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"Element {i}");
                            if (DrawRemoveButton())
                            {
                                EditorGUILayout.EndHorizontal();
                                propertyPath.Transform(current => NBC.ActionEditor.Application.Editing.CollectionEditing.Remove(fieldType, (IList)current, i));
                                i--;
                                continue;
                            }

                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(12);
                            EditorGUILayout.BeginVertical();
                            var updated = DrawElement(elementType, listItem, propertyPath.Element(i));
                            if (!Equals(listItem, updated)) propertyPath.Element(i).Write(updated);
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();
                            DrawDivider();
                        }
                    }

                    GUILayout.Space(6);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (DrawAddButton())
                    {
                        var added = CreateElement(elementType);
                        propertyPath.Transform(current => NBC.ActionEditor.Application.Editing.CollectionEditing.Append(fieldType, (IList)current, added));
                        list = (IList)propertyPath.Read();
                        SetFoldout(list, true);
                    }

                    EditorGUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }

                _collectionFoldouts.GetOrCreateValue(obj)[field] = foldout;
                SetFoldout(value, foldout);
            }
            // 处理数组类型
            else if (fieldType.IsArray)
            {
                Type elementType = fieldType.GetElementType();

                Array array = (Array)value;
                if (array == null)
                {
                    array = Array.CreateInstance(elementType, 0);
                }

                var foldout = EditorGUILayout.Foldout(GetCollectionFoldout(obj, field, value), field.GetShowName());
                if (foldout)
                {
                    GUILayout.Space(6);
                    GUILayout.BeginVertical(GUI.skin.box);
                    for (int i = 0; i < array.Length; i++)
                    {
                        object listItem = array.GetValue(i);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Element {i}");
                        if (DrawRemoveButton())
                        {
                            EditorGUILayout.EndHorizontal();
                            var newArray = (Array)NBC.ActionEditor.Application.Editing.CollectionEditing.Remove(fieldType, array, i);
                            RemoveFoldout(value);
                            array = newArray;
                            propertyPath.Write(newArray);
                            SetFoldout(newArray, true);
                            break; // 退出循环以避免数组长度更改引起的冲突
                        }

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(12);
                        EditorGUILayout.BeginVertical();
                        var updated = DrawElement(elementType, listItem, propertyPath.Element(i));
                        if (!Equals(listItem, updated)) propertyPath.Element(i).Write(updated);
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        DrawDivider();
                    }

                    GUILayout.Space(6);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (DrawAddButton())
                    {
                        var newArray = (Array)NBC.ActionEditor.Application.Editing.CollectionEditing.Append(fieldType, array, CreateElement(elementType));
                        RemoveFoldout(value);
                        array = newArray;
                        propertyPath.Write(array);
                        SetFoldout(newArray, true);
                    }

                    EditorGUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }

                _collectionFoldouts.GetOrCreateValue(obj)[field] = foldout;
                SetFoldout(value, foldout);
            }
            else if (showType == typeof(int))
            {
                newValue = EditorGUILayout.IntField(name, (int)value);
            }
            else if (showType == typeof(float))
            {
                newValue = EditorGUILayout.FloatField(name, (float)value);
            }
            else if (showType == typeof(bool))
            {
                newValue = EditorGUILayout.Toggle(name, (bool)value);
            }
            else if (showType == typeof(string))
            {
                newValue = EditorGUILayout.TextField(name, (string)value);
            }
            else if (showType == typeof(Color))
            {
                newValue = EditorGUILayout.ColorField(name, (Color)value);
            }
            else if (showType.IsSubclassOf(typeof(Object)))
            {
                newValue = EditorGUILayout.ObjectField(name, (Object)value, fieldType, false);
            }
            else if (showType.IsEnum)
            {
                newValue = EditorGUILayout.EnumPopup(name, (Enum)value);
            }
            else if (showType == typeof(AnimationCurve))
            {
                AnimationCurve curve = field.GetValue(obj) as AnimationCurve;
                if (curve == null)
                {
                    curve = new AnimationCurve();
                }

                newValue = EditorGUILayout.CurveField(name, curve);
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
            else if (showType == typeof(RangeAttribute))
            {
                if (fieldType == typeof(float))
                {
                    newValue = EditorGUILayout.Slider(name, (float)value, (float)args[0], (float)args[1]);
                }
            }
            else if (showType == typeof(OptionParamAttribute))
            {
                var constants = description.Constants;
                var labels = constants.Select(f => f.GetCustomAttribute<MenuNameAttribute>()?.showName ?? f.Name).ToArray();
                if (labels.Length > 0)
                {
                    var currentIndex = description.OptionIndex(value);
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup(name, Mathf.Max(0, currentIndex), labels);
                    if (EditorGUI.EndChangeCheck()) newValue = constants[selected].GetRawConstantValue();
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

            if (!Equals(value, newValue))
                propertyPath.Write(newValue);
        }

        private int FieldsSprtBy(FieldInfo f1, FieldInfo f2)
        {
            if (f1 == null || f2 == null) return 0;
            var e1 = f1.DeclaringType == f1.ReflectedType;
            var e2 = f2.DeclaringType == f2.ReflectedType;
            if (e1 != e2)
            {
                if (e1)
                {
                    return 1;
                }

                return -1;
            }

            return 0;
        }

        public static object CreateElement(Type type) => App.Context.Properties.CreateElement(type);

        private object DrawElement(Type type, object value, NBC.ActionEditor.Application.Editing.PropertyPath propertyPath)
        {
            if (type == typeof(string)) return EditorGUILayout.TextField(value as string ?? "");
            if (type == typeof(int)) return EditorGUILayout.IntField(value == null ? 0 : (int)value);
            if (type == typeof(float)) return EditorGUILayout.FloatField(value == null ? 0 : (float)value);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(value != null && (bool)value);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(value == null ? Color.white : (Color)value);
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field("", value == null ? Vector2.zero : (Vector2)value);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field("", value == null ? Vector3.zero : (Vector3)value);
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

        private bool GetCollectionFoldout(object owner, FieldInfo field, object value)
        {
            var fields = _collectionFoldouts.GetOrCreateValue(owner);
            return fields.TryGetValue(field, out var expanded) ? expanded : GetFoldout(value);
        }

        private bool GetFoldout(object obj)
        {
            if (obj == null) return false;
            if (!_unfoldDictionary.TryGetValue(obj.GetHashCode(), out var value))
            {
                _unfoldDictionary[obj.GetHashCode()] = false;
            }

            return value;
        }

        private void SetFoldout(object obj, bool unfold)
        {
            if (obj == null) return;
            _unfoldDictionary[obj.GetHashCode()] = unfold;
        }

        public void RemoveFoldout(object obj)
        {
            if (obj == null) return;
            _unfoldDictionary.Remove(obj.GetHashCode());
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