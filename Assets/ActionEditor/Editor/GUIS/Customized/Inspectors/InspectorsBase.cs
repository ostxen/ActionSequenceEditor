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

        public void DrawDefaultInspector(object obj)
        {
            if (obj == null) { EditorGUILayout.LabelField("null"); return; }
            var t = obj.GetType();
            //得到字段的值,只能得到public类型的字典的值
            FieldInfo[] fieldInfos = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            //排序一下，子类的字段在后，父类的在前
            Array.Sort(fieldInfos, FieldsSprtBy);

            //判断需要过滤不显示的字段
            List<FieldInfo> needShowField = new List<FieldInfo>();
            foreach (var field in fieldInfos)
            {
                var need = true;
                var attributes = field.GetCustomAttributes();
                foreach (var attribute in attributes)
                {
                    if (attribute is HideInInspector hide)
                    {
                        need = false;
                        break;
                    }

                    if (attribute is OptionRelateParamAttribute option)
                    {
                        var relate = Array.Find(fieldInfos, f1 => f1.Name == option.argsName);
                        if (relate != null)
                        {
                            var value = relate.GetValue(obj);
                            var index = Array.FindIndex(option.argsValue, v1 => v1.Equals(value));
                            if (index < 0)
                            {
                                need = false;
                                break;
                            }
                        }
                    }
                }

                if (need)
                {
                    needShowField.Add(field);
                }
            }

            foreach (var field in needShowField)
            {
                FieldDefaultInspector(field, obj);
            }
        }

        protected void FieldDefaultInspector(FieldInfo field, object obj)
        {
            var fieldType = field.FieldType;
            var showType = field.FieldType;
            var value = field.GetValue(obj);
            var newValue = value;

            var name = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(field.Name);

            List<object> args = new List<object>();

            var attributes = field.GetCustomAttributes();
            foreach (var attribute in attributes)
            {
                var t = attribute.GetType();
                if (attribute is MenuNameAttribute menuNameAttribute)
                {
                    name = menuNameAttribute.showName;
                }
                else if (attribute is RangeAttribute rangeAttribute && fieldType == typeof(float))
                {
                    showType = t;
                    args = new List<object> { rangeAttribute.min, rangeAttribute.max };
                }
                else if (attribute is OptionParamAttribute option)
                {
                    showType = t;
                    args = new List<object> { option.classType };
                }
                else if (attribute is SelectObjectPathAttribute selectObjectPathAttribute)
                {
                    showType = t;
                    args = new List<object> { selectObjectPathAttribute.type };
                }
            }


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
                                list.RemoveAt(i);
                                i--;
                                continue;
                            }

                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(12);
                            EditorGUILayout.BeginVertical();
                            list[i] = DrawElement(elementType, listItem);
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
                        if (list == null)
                        {
                            list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                            field.SetValue(obj, list);
                        }

                        list.Add(CreateElement(elementType));
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
                            Array newArray = Array.CreateInstance(elementType, array.Length - 1);
                            for (int j = 0, k = 0; j < array.Length; j++)
                            {
                                if (j != i) // 跳过被移除的元素
                                {
                                    newArray.SetValue(array.GetValue(j), k);
                                    k++;
                                }
                            }

                            RemoveFoldout(value);
                            array = newArray;
                            field.SetValue(obj, newArray);
                            SetFoldout(newArray, true);
                            break; // 退出循环以避免数组长度更改引起的冲突
                        }

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(12);
                        EditorGUILayout.BeginVertical();
                        array.SetValue(DrawElement(elementType, listItem), i);
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        DrawDivider();
                    }

                    GUILayout.Space(6);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (DrawAddButton())
                    {
                        Array newArray = Array.CreateInstance(elementType, array != null ? array.Length + 1 : 1);
                        if (array != null)
                        {
                            array.CopyTo(newArray, 0);
                        }

                        newArray.SetValue(CreateElement(elementType), newArray.Length - 1);
                        RemoveFoldout(value);
                        array = newArray;
                        field.SetValue(obj, array);
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
                // LookAtType.None
                var t = (Type)args[0];
                var fields = t.GetFields();
                //先对fields排序
                Array.Sort(fields, FieldsSort);

                var title = new List<string>();
                var mask = 0;
                foreach (var f in fields)
                {
                    var menuNameAttr = f.GetCustomAttribute<MenuNameAttribute>();
                    title.Add(menuNameAttr != null ? menuNameAttr.showName : f.Name);
                }

                mask = (int)value;

                newValue = EditorGUILayout.Popup(name, mask, title.ToArray());
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

            if (value != newValue)
                field.SetValue(obj, newValue);
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

        public static object CreateElement(Type type)
        {
            if (type == typeof(string)) return "";
            if (typeof(Object).IsAssignableFrom(type)) return null;
            if (type.IsAbstract || type.IsInterface || !type.IsValueType && type.GetConstructor(Type.EmptyTypes) == null) return null;
            return Activator.CreateInstance(type);
        }

        private object DrawElement(Type type, object value)
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
            DrawDefaultInspector(value); return value;
        }

        protected virtual bool DrawRemoveButton() => GUILayout.Button("X", GUILayout.Width(20));
        protected virtual bool DrawAddButton() => GUILayout.Button("+", GUILayout.Width(30));

        private bool GetCollectionFoldout(object owner, FieldInfo field, object value)
        {
            var fields = _collectionFoldouts.GetOrCreateValue(owner);
            return fields.TryGetValue(field, out var expanded) ? expanded : GetFoldout(value);
        }

        private int FieldsSort(FieldInfo f1, FieldInfo f2)
        {
            var sort1 = f1.GetCustomAttribute<OptionSortAttribute>();
            var sort2 = f2.GetCustomAttribute<OptionSortAttribute>();
            var i1 = 99;
            var i2 = 99;
            if (sort1 != null)
            {
                i1 = sort1.sort;
            }

            if (sort2 != null)
            {
                i2 = sort2.sort;
            }

            return i1 - i2;
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