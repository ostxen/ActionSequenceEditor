using NBC.ActionEditor.Domain;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Workbench
{
    public sealed class UIToolkitInspectorTarget : ScriptableObject
    {
        [NonSerialized] public UIToolkitActionEditorWindow Owner;
    }

    [CustomEditor(typeof(UIToolkitInspectorTarget))]
    public sealed class UIToolkitSelectionInspector : Editor
    {
        public override VisualElement CreateInspectorGUI() => ((UIToolkitInspectorTarget)target).Owner?.CreatePropertyView() ?? new Label("窗口已关闭");
    }

    public static class UIToolkitPropertyView
    {
        public static void Populate(VisualElement root, object value, Action refresh)
        {
            if (value == null) return;
            if (value is IData)
            {
                var adapter = EditorCustomFactory.GetInspectorType(value.GetType());
                if (adapter != null && adapter != typeof(ActionClipInspector))
                    root.Add(new HelpBox("自定义Inspector尚未提供Toolkit适配：" + adapter.FullName + "。以下仅显示通用字段。", HelpBoxMessageType.Warning));
            }
            var title = new Label(value.GetType().RTGetAttribute<NameAttribute>(true)?.name ?? value.GetType().Name);
            title.style.fontSize = 18; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.backgroundColor = new Color(0, 0, 0, .2f); root.Add(title);
            var description = value.GetType().RTGetAttribute<DescriptionAttribute>(true)?.description;
            if (!string.IsNullOrEmpty(description)) root.Add(new HelpBox(description, HelpBoxMessageType.None));
            if (value is IDirectable directable)
            {
                if (value is TimelineGroup || value is TimelineTrack) Add(root, "Name", directable.Name, next => JsonEditCommands.Rename(directable, next));
                if (value is TimelineClip clip)
                {
                    DrawClipControls(root, clip, refresh);
                }
                root.SetEnabled(!directable.IsLocked);
            }
            DrawFields(root, value, refresh, 0);
        }

        private static void Change(Action change) => JsonPropertyEditing.Change(change);
        private static VisualElement Box(VisualElement root)
        {
            var box = new VisualElement(); box.style.marginTop = box.style.marginBottom = 2;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 4;
            box.style.backgroundColor = new Color(0, 0, 0, .12f); root.Add(box); return box;
        }
        private static void DrawClipControls(VisualElement root, TimelineClip clip, Action refresh)
        {
            if (!clip.IsValid) root.Add(new HelpBox("该剪辑无效。 请确保设置了所需的参数。", HelpBoxMessageType.Error));
            var previous = clip.GetPreviousSibling(); var next = clip.GetNextSibling();
            var bounds = ClipTimingRules.NeighborBounds(clip, previous, next, true);
            var minimum = bounds.x; var maximum = bounds.y;
            void Apply(float start, float end)
            {
                start = Mathf.Round(start / Prefs.SnapInterval) * Prefs.SnapInterval;
                end = Mathf.Round(end / Prefs.SnapInterval) * Prefs.SnapInterval;
                start = Mathf.Clamp(start, minimum, end); end = Mathf.Clamp(end, start, next != null ? maximum : float.PositiveInfinity);
                Change(() => { clip.StartTime = start; clip.EndTime = end; }); refresh();
            }
            var box = Box(root);
            if (clip.CanScale())
            {
                var row = new VisualElement { name = "json-clip-timing" }; row.style.flexDirection = FlexDirection.Row; box.Add(row);
                void Label(string text, float width) { var label = new Label(text); label.style.width = width; row.Add(label); }
                void Space() { var space = new VisualElement(); space.style.flexGrow = 1; row.Add(space); }
                void Field(string name, float value, Action<float> set)
                {
                    if (Prefs.timeStepMode == Prefs.TimeStepMode.Frames)
                    {
                        var field = new IntegerField { name = name, value = Mathf.RoundToInt(value * Prefs.FrameRate), isDelayed = true }; field.style.width = 80;
                        field.RegisterValueChangedCallback(e => set((float)e.newValue / Prefs.FrameRate)); row.Add(field);
                    }
                    else
                    {
                        var field = new FloatField { name = name, value = value, isDelayed = true }; field.style.width = 80;
                        field.RegisterValueChangedCallback(e => set(e.newValue)); row.Add(field);
                    }
                }
                Label("IN", 30); Field("json-clip-in", clip.StartTime, value => Apply(value, clip.EndTime)); Space();
                Label("◄", 15); Field("json-clip-length", clip.Length, value => Apply(clip.StartTime, clip.StartTime + value)); Label("►", 15); Space();
                Label("OUT", 30); Field("json-clip-out", clip.EndTime, value => Apply(clip.StartTime, value));
                if (clip.StartTime >= clip.Parent.StartTime && clip.EndTime <= clip.Parent.EndTime && maximum > minimum)
                {
                    var slider = new MinMaxSlider(clip.StartTime, clip.EndTime, minimum, maximum);
                    slider.RegisterValueChangedCallback(e => Apply(e.newValue.x, e.newValue.y)); box.Add(slider);
                }
            }
            else
            {
                var slider = new Slider("IN", 0, Mathf.Max(0, clip.Parent.EndTime)) { value = clip.StartTime, showInputField = true };
                slider.RegisterValueChangedCallback(e => Apply(e.newValue, e.newValue)); box.Add(slider);
            }
            if (clip.StartTime > clip.Parent.EndTime || clip.EndTime < clip.Parent.StartTime) box.Add(new HelpBox(Lan.OverflowInvalid, HelpBoxMessageType.Warning));
            else if (clip.EndTime > clip.Parent.EndTime) box.Add(new HelpBox(Lan.EndTimeOverflowInvalid, HelpBoxMessageType.Warning));
            else if (clip.StartTime < clip.Parent.StartTime) box.Add(new HelpBox(Lan.StartTimeOverflowInvalid, HelpBoxMessageType.Warning));
            if (clip.Length <= 0 || !clip.CanBlendIn() && !clip.CanBlendOut()) return;
            var blend = Box(root); blend.name = "json-clip-blending"; blend.style.flexDirection = FlexDirection.Row;
            void Blend(bool incoming)
            {
                var column = new VisualElement(); column.style.flexGrow = 1; column.style.flexBasis = 0; blend.Add(column);
                column.Add(new Label(incoming ? "Blend In" : "Blend Out"));
                var slider = new Slider(0, Mathf.Max(0, clip.Length - (incoming ? clip.BlendOut : clip.BlendIn))) { value = incoming ? clip.BlendIn : clip.BlendOut, showInputField = true };
                slider.RegisterValueChangedCallback(e => { Change(() => { if (incoming) clip.BlendIn = e.newValue; else clip.BlendOut = e.newValue; }); refresh(); }); column.Add(slider);
            }
            if (clip.CanBlendIn()) Blend(true); if (clip.CanBlendOut()) Blend(false);
        }
        private static void Add<T>(VisualElement root, string label, T value, Action<T> changed)
        {
            BaseField<T> field;
            if (typeof(T) == typeof(string)) field = (BaseField<T>)(object)new TextField(label) { isDelayed = true };
            else field = (BaseField<T>)(object)new FloatField(label) { isDelayed = true };
            field.value = value;
            field.RegisterValueChangedCallback(evt => changed(evt.newValue)); root.Add(field);
        }

        public static void DrawFields(VisualElement root, object obj, Action refresh, int depth, JsonPropertyPath propertyPath = null)
        {
            if (obj == null || depth > 6) return;
            propertyPath ??= new JsonPropertyPath(obj);
            foreach (var description in JsonFieldDescription.For(obj.GetType()))
            {
                var field = description.Field;
                if (field.IsInitOnly || !description.Visible(obj)) continue;
                var label = description.Label ?? field.Name;
                var current = field.GetValue(obj);
                var type = field.FieldType;
                var pathToField = propertyPath.Field(field);
                void Set(object next) { pathToField.Write(next); refresh(); }
                var options = description.Options;
                var path = description.ResourcePath;
                var range = description.Range;
                if (range != null && type == typeof(float)) Bind(root, new Slider(label, range.min, range.max) { showInputField = true }, current, Set);
                else if (range != null && type == typeof(int)) Bind(root, new SliderInt(label, Mathf.CeilToInt(range.min), Mathf.FloorToInt(range.max)) { showInputField = true }, current, Set);
                else if (options != null && type == typeof(int))
                {
                    var constants = description.Constants.ToArray();
                    var labels = constants.Select(f => f.GetCustomAttribute<MenuNameAttribute>()?.showName ?? f.Name).ToList();
                    if (labels.Count == 0) continue;
                    var control = new PopupField<string>(label, labels, Math.Max(0, Array.FindIndex(constants, f => Equals(f.GetRawConstantValue(), current))));
                    control.RegisterValueChangedCallback(e => Set(constants[control.index].GetRawConstantValue())); root.Add(control);
                }
                else if (path != null && type == typeof(string))
                {
                    var control = new ObjectField(label) { objectType = path.type, allowSceneObjects = false, value = AssetDatabase.LoadAssetAtPath<Object>(current as string ?? "") };
                    control.RegisterValueChangedCallback(e => Set(AssetDatabase.GetAssetPath(e.newValue))); root.Add(control);
                }
                else DrawValue(root, label, type, current, Set, refresh, depth, pathToField);
            }
        }

        private static void Bind<T>(VisualElement root, BaseField<T> control, object current, Action<object> set)
        {
            control.SetValueWithoutNotify(current is T v ? v : default);
            control.RegisterValueChangedCallback(e => set(e.newValue)); root.Add(control);
        }

        private static void DrawValue(VisualElement root, string label, Type type, object value, Action<object> set, Action refresh, int depth, JsonPropertyPath propertyPath)
        {
            if (type == typeof(string)) Bind(root, new TextField(label) { isDelayed = true }, value, set);
            else if (type == typeof(float)) Bind(root, new FloatField(label) { isDelayed = true }, value, set);
            else if (type == typeof(int)) Bind(root, new IntegerField(label) { isDelayed = true }, value, set);
            else if (type == typeof(bool)) Bind(root, new Toggle(label), value, set);
            else if (type == typeof(Color)) Bind(root, new ColorField(label), value, set);
            else if (type == typeof(Vector2)) { root.Add(new Label(label)); Bind(root, new Vector2Field(), value, set); }
            else if (type == typeof(Vector3)) { root.Add(new Label(label)); Bind(root, new Vector3Field(), value, set); }
            else if (type == typeof(Vector4)) { root.Add(new Label(label)); Bind(root, new Vector4Field(), value, set); }
            else if (type == typeof(Vector2Int)) Bind(root, new Vector2IntField(label), value, set);
            else if (type == typeof(Vector3Int)) Bind(root, new Vector3IntField(label), value, set);
            else if (type == typeof(Rect)) Bind(root, new RectField(label), value, set);
            else if (type == typeof(RectInt)) Bind(root, new RectIntField(label), value, set);
            else if (type == typeof(Bounds)) Bind(root, new BoundsField(label), value, set);
            else if (type == typeof(BoundsInt)) Bind(root, new BoundsIntField(label), value, set);
            else if (type == typeof(AnimationCurve)) Bind(root, new CurveField(label), value, set);
            else if (type.IsEnum)
            {
                var control = new EnumField(label, (Enum)(value ?? Activator.CreateInstance(type)));
                control.RegisterValueChangedCallback(e => set(e.newValue)); root.Add(control);
            }
            else if (typeof(Object).IsAssignableFrom(type))
            {
                var control = new ObjectField(label) { objectType = type, allowSceneObjects = false, value = value as Object };
                control.RegisterValueChangedCallback(e => set(e.newValue)); root.Add(control);
            }
            else if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                var fold = new Foldout { text = label, value = false }; root.Add(fold);
                var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                var list = value as IList;
                if (list != null) for (var i = 0; i < list.Count; i++)
                {
                    var index = i;
                    var row = new VisualElement(); row.style.marginLeft = 10; fold.Add(row);
                    DrawValue(row, "Element " + i, elementType, list[i], next => { propertyPath.Element(index).Write(next); refresh(); }, refresh, depth + 1, propertyPath.Element(index));
                    row.Add(new Button(() =>
                    {
                        propertyPath.Transform(current => JsonPropertyEditing.Remove(type, (IList)current, index)); refresh();
                    }) { text = "删除元素" });
                }
                fold.Add(new Button(() =>
                {
                    var item = JsonPropertyEditing.CreateElement(elementType);
                    propertyPath.Transform(current => JsonPropertyEditing.Append(type, (IList)current, item)); refresh();
                }) { text = "+ 元素" });
            }
            else if (depth < 6)
            {
                var fold = new Foldout { text = label, value = true }; root.Add(fold);
                if (value == null) fold.Add(new Button(() => set(JsonPropertyEditing.CreateElement(type))) { text = "创建 " + type.Name });
                else DrawFields(fold, value, refresh, depth + 1, propertyPath);
            }
        }
    }
}
