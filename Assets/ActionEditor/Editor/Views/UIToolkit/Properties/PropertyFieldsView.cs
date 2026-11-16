using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Presentation.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Presentation.Presenters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Views.UIToolkit
{


    // 原生属性控件绑定显式Presenter，控件不取得全局会话。
    public sealed class PropertyFieldsView
    {
        private readonly PropertyPresenter presenter;
        private readonly Func<Type, ToolkitPropertyExtension> extensions;
        public PropertyFieldsView(PropertyPresenter presenter, Func<Type, ToolkitPropertyExtension> extensions = null)
        {
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            this.extensions = extensions ?? EditorExtensionFactory.CreateToolkitPropertyExtension;
        }
        public void Populate(VisualElement root, PropertyTargetState value, Action refresh)
        {
            if (value == null) return;
            // 自定义属性适配也服从共同锁定状态，不能绕过默认面板的禁用范围。
            root.SetEnabled(!value.Locked);
            var extension = extensions(value.Type);
            if (extension != null) { extension.Populate(root, value, presenter, refresh); return; }
            var title = new Label(value.Title);
            title.style.fontSize = 18; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.backgroundColor = new Color(0, 0, 0, .2f); root.Add(title);
            var description = value.Description;
            if (!string.IsNullOrEmpty(description)) root.Add(new HelpBox(description, HelpBoxMessageType.None));
            if (value.CanRename) Add(root, "Name", value.Name, next => { presenter.Rename(value.Id, next); refresh(); });
            if (value.Clip != null) DrawClipControls(root, value.Id, value.Clip, refresh);
            DrawFields(root, value.Fields, refresh, 0);
        }

        private static VisualElement Box(VisualElement root)
        {
            var box = new VisualElement(); box.style.marginTop = box.style.marginBottom = 2;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 4;
            box.style.backgroundColor = new Color(0, 0, 0, .12f); root.Add(box); return box;
        }
        private void DrawClipControls(VisualElement root, PropertyFieldId clipId, ClipPropertyState state, Action refresh)
        {
            if (!state.Valid) root.Add(new HelpBox("该剪辑无效。 请确保设置了所需的参数。", HelpBoxMessageType.Error));
            var minimum = state.Minimum; var maximum = state.Maximum;
            void Apply(float start, float end)
            {
                presenter.Timing(clipId, start, end); refresh();
            }
            var box = Box(root);
            if (state.Scalable)
            {
                var row = new VisualElement { name = "timeline-clip-timing" }; row.style.flexDirection = FlexDirection.Row; box.Add(row);
                void Label(string text, float width) { var label = new Label(text); label.style.width = width; row.Add(label); }
                void Space() { var space = new VisualElement(); space.style.flexGrow = 1; row.Add(space); }
                void Field(string name, float value, Action<float> set)
                {
                    if (presenter.Settings.TimeStepMode == NBC.ActionEditor.Application.Settings.TimeStepMode.Frames)
                    {
                        var field = new IntegerField { name = name, value = Mathf.RoundToInt(value * presenter.Settings.FrameRate), isDelayed = true }; field.style.width = 80;
                        field.RegisterValueChangedCallback(e => set((float)e.newValue / presenter.Settings.FrameRate)); row.Add(field);
                    }
                    else
                    {
                        var field = new FloatField { name = name, value = value, isDelayed = true }; field.style.width = 80;
                        field.RegisterValueChangedCallback(e => set(e.newValue)); row.Add(field);
                    }
                }
                Label("IN", 30); Field("timeline-clip-in", state.Start, value => Apply(value, state.End)); Space();
                Label("◄", 15); Field("timeline-clip-length", state.Length, value => Apply(state.Start, state.Start + value)); Label("►", 15); Space();
                Label("OUT", 30); Field("timeline-clip-out", state.End, value => Apply(state.Start, value));
                if (state.ShowRange)
                {
                    var slider = new MinMaxSlider(state.Start, state.End, minimum, maximum);
                    slider.RegisterValueChangedCallback(e => Apply(e.newValue.x, e.newValue.y)); box.Add(slider);
                }
            }
            else
            {
                var slider = new Slider("IN", 0, state.PointMaximum) { value = state.Start, showInputField = true };
                slider.RegisterValueChangedCallback(e => Apply(e.newValue, e.newValue)); box.Add(slider);
            }
            var warning = PropertyWarningText.Warning(state.Warning);
            if (warning != null) box.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
            if (!state.BlendInEnabled && !state.BlendOutEnabled) return;
            var blend = Box(root); blend.name = "timeline-clip-blending"; blend.style.flexDirection = FlexDirection.Row;
            void Blend(bool isBlendIn)
            {
                var column = new VisualElement(); column.style.flexGrow = 1; column.style.flexBasis = 0; blend.Add(column);
                column.Add(new Label(isBlendIn ? "Blend In" : "Blend Out"));
                var slider = new Slider(0, isBlendIn ? state.BlendInMaximum : state.BlendOutMaximum) { value = isBlendIn ? state.BlendIn : state.BlendOut, showInputField = true };
                slider.RegisterValueChangedCallback(e => { presenter.Blend(clipId, isBlendIn, e.newValue); refresh(); }); column.Add(slider);
            }
            if (state.BlendInEnabled) Blend(true); if (state.BlendOutEnabled) Blend(false);
        }
        private static void Add<T>(VisualElement root, string label, T value, Action<T> changed)
        {
            BaseField<T> field;
            if (typeof(T) == typeof(string)) field = (BaseField<T>)(object)new TextField(label) { isDelayed = true };
            else field = (BaseField<T>)(object)new FloatField(label) { isDelayed = true };
            field.value = value;
            field.RegisterValueChangedCallback(evt => changed(evt.newValue)); root.Add(field);
        }

        public void DrawFields(VisualElement root, IReadOnlyList<PropertyInputState> fields, Action refresh, int depth)
        {
            if (fields == null || depth > 6) return;
            foreach (var state in fields)
            {
                var label = state.Label;
                var current = state.Value;
                var type = state.Type;
                var fieldId = state.Id;
                void Set(object next) { presenter.Write(fieldId, next); refresh(); }
                if (state.HasRange && type == typeof(float)) Bind(root, new Slider(label, state.Minimum, state.Maximum) { showInputField = true }, current, Set);
                else if (state.HasRange && type == typeof(int)) Bind(root, new SliderInt(label, Mathf.CeilToInt(state.Minimum), Mathf.FloorToInt(state.Maximum)) { showInputField = true }, current, Set);
                else if (state.HasOptions && type == typeof(int))
                {
                    var labels = state.OptionLabels.ToList();
                    if (labels.Count == 0) continue;
                    var control = new PopupField<string>(label, labels, Math.Max(0, state.OptionIndex));
                    control.RegisterValueChangedCallback(e => { presenter.SelectOption(state.Id, control.index); refresh(); }); root.Add(control);
                }
                else if (state.ResourceType != null && type == typeof(string))
                {
                    var control = new ObjectField(label) { objectType = state.ResourceType, allowSceneObjects = false, value = AssetDatabase.LoadAssetAtPath<Object>(current as string ?? "") };
                    control.RegisterValueChangedCallback(e => Set(AssetDatabase.GetAssetPath(e.newValue))); root.Add(control);
                }
                else DrawValue(root, state, Set, refresh, depth);
            }
        }

        private static void Bind<T>(VisualElement root, BaseField<T> control, object current, Action<object> set)
        {
            control.SetValueWithoutNotify(current is T v ? v : default);
            control.RegisterValueChangedCallback(e => set(e.newValue)); root.Add(control);
        }

        private void DrawValue(VisualElement root, PropertyInputState state, Action<object> set, Action refresh, int depth)
        {
            if (depth > 6) return;
            var label = state.Label; var type = state.Type; var value = state.Value; var fieldId = state.Id;
            if (type == typeof(string))
            {
                BindDelayed(root, new TextField(label) { isDelayed = true }, value, state, refresh);
                if (value != null) root.Add(new Button(() => set(null)) { text = "设为空值 " + label });
            }
            else if (type == typeof(float)) BindDelayed(root, new FloatField(label) { isDelayed = true }, value, state, refresh);
            else if (type == typeof(int)) BindDelayed(root, new IntegerField(label) { isDelayed = true }, value, state, refresh);
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
                if (!state.IsNull) fold.Add(new Button(() => set(null)) { text = "设为空值 " + label });
                for (var i = 0; i < state.Children.Count; i++)
                {
                    var index = i;
                    var child = state.Children[i];
                    var row = new VisualElement(); row.style.marginLeft = 10; fold.Add(row);
                    DrawValue(row, child, next => { presenter.Write(child.Id, next); refresh(); }, refresh, depth + 1);
                    row.Add(new Button(() =>
                    {
                        presenter.Remove(fieldId, index); refresh();
                    }) { text = "删除元素" });
                }
                fold.Add(new Button(() =>
                {
                    presenter.Append(fieldId); refresh();
                }) { text = "+ 元素" });
            }
            else if (depth < 6)
            {
                var fold = new Foldout { text = label, value = true }; root.Add(fold);
                if (state.IsNull) fold.Add(new Button(() => { presenter.CreateValue(fieldId); refresh(); }) { text = "创建 " + type.Name });
                else DrawFields(fold, state.Children, refresh, depth + 1);
            }
        }

        // 控件负责原生文本缓冲，Presenter负责输入身份、取消和最终作者提交。
        private void BindDelayed<T>(VisualElement root, BaseField<T> control, object value,
            PropertyInputState state, Action refresh)
        {
            var fieldId = state.Id;
            NBC.ActionEditor.Presentation.Properties.PropertyDraft draft = null;
            HelpBox error = null;
            void CancelInput()
            {
                if (draft != null) presenter.CancelDraft(draft);
                draft = null;
            }
            void Input(Action action)
            {
                try
                {
                    if (!presenter.CanInput(fieldId)) throw new InvalidOperationException("属性已失效、锁定或正在进行其他编辑，请重新选择。");
                    action();
                    error?.RemoveFromHierarchy(); error = null;
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
                {
                    // 预期冲突作为字段反馈呈现，不把旧文本重新绑定到更新后的作者值。
                    CancelInput();
                    control.SetValueWithoutNotify(value is T original ? original : default);
                    if (error == null) { error = new HelpBox(exception.Message, HelpBoxMessageType.Warning); root.Add(error); }
                    else error.text = exception.Message;
                }
            }
            control.SetValueWithoutNotify(value is T typed ? typed : default);
            control.showMixedValue = state.Mixed;
            control.RegisterCallback<FocusInEvent>(_ => Input(() => draft = presenter.BeginDraft(fieldId)));
            control.RegisterCallback<InputEvent>(e => Input(() =>
            {
                if (draft == null) draft = presenter.BeginDraft(fieldId);
                presenter.PreviewDraft(draft, e.newData);
            }));
            control.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                CancelInput();
            }, TrickleDown.TrickleDown);
            control.RegisterValueChangedCallback(e => Input(() =>
            {
                if (draft == null) draft = presenter.BeginDraft(fieldId);
                presenter.CommitDraft(draft, e.newValue); draft = null; value = e.newValue; refresh();
            }));
            control.RegisterCallback<DetachFromPanelEvent>(_ => CancelInput());
            root.Add(control);
        }
    }
}
