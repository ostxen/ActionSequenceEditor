using NBC.ActionEditor.Presentation.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 属性视图的字段描述、路径与作者提交入口，不持有原生控件。
    public sealed class PropertyPresenter
    {
        private readonly PropertyEditing editing;
        private readonly NodeEditing nodes;
        private readonly ClipEditing clips;
        private readonly Func<EditorSettings> settings;
        private readonly SelectionState selection;
        private PropertyDraft draft;
        private readonly object fieldIssuer = new object();
        private readonly Dictionary<PropertyFieldId, PropertyPath> inputPaths = new Dictionary<PropertyFieldId, PropertyPath>();
        private readonly Dictionary<PropertyPath, PropertyFieldId> inputIds = new Dictionary<PropertyPath, PropertyFieldId>();
        private readonly Dictionary<PropertyFieldId, PropertyFieldMetadata> inputDescriptions = new Dictionary<PropertyFieldId, PropertyFieldMetadata>();
        private readonly Dictionary<PropertyFieldId, Type> inputTypes = new Dictionary<PropertyFieldId, Type>();
        private readonly Dictionary<PropertyPath, object> viewKeys = new Dictionary<PropertyPath, object>();
        private ActionSequence inputSequence;
        private long inputSequenceVersion = -1, inputSelectionVersion = -1, inputGeneration;
        public PropertyPresenter(PropertyEditing editing, NodeEditing nodes, ClipEditing clips, Func<EditorSettings> settings, SelectionState selection = null)
        {
            this.editing = editing ?? throw new ArgumentNullException(nameof(editing));
            this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            this.clips = clips ?? throw new ArgumentNullException(nameof(clips));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.selection = selection;
        }
        private string Title(object value) => value == null ? string.Empty
            : value.GetType().GetCustomAttribute<NameAttribute>(true)?.name ?? value.GetType().Name;
        private string Description(object value) => value?.GetType().GetCustomAttribute<DescriptionAttribute>(true)?.description;
        public float SequenceLength => editing.Sequence?.Length ?? 0;
        private bool Locked(object value) => value is IDirectable item && item.IsLocked;
        public EditorSettings Settings => settings();

        // 正式界面从当前文档/选择捕获，不把目标图传给调用方。
        public IReadOnlyList<PropertyInputState> CaptureSequenceInputs() => CaptureInputs(editing.Sequence);
        public IReadOnlyList<PropertyInputState> CaptureSelectionInputs() => CaptureInputs(selection?.First);
        public PropertyTargetState CaptureSequenceTarget() => CaptureTarget(editing.Sequence);
        public PropertyTargetState CaptureSelectionTarget() => CaptureTarget(selection?.First);
        public PropertyTargetState CaptureTarget(object owner)
        {
            SynchronizeInputs();
            if (owner == null) return null;
            var path = Path(owner);
            if (!inputIds.TryGetValue(path, out var id))
            {
                id = new PropertyFieldId(fieldIssuer, inputGeneration, inputPaths.Count, ViewKey(path));
                inputIds.Add(path, id); inputPaths.Add(id, path);
            }
            return new PropertyTargetState(id, owner.GetType(), Title(owner), Description(owner),
                (owner as IDirectable)?.Name, Locked(owner), owner is ActionGroup || owner is ActionTrack,
                owner is ActionClip clip ? ClipState(clip) : null, CaptureInputs(owner, path, 0));
        }
        public IReadOnlyList<PropertyInputState> CaptureInputs(object owner)
        {
            SynchronizeInputs();
            return owner == null ? Array.Empty<PropertyInputState>() : CaptureInputs(owner, Path(owner), 0);
        }
        private PropertyInputState[] CaptureInputs(object owner, PropertyPath path, int depth) => depth > 6
            ? Array.Empty<PropertyInputState>()
            : Fields(owner).Select(d => CaptureInput(path.Field(d.Field), d.Field.FieldType,
                d.Field.Name, d.Label ?? d.Field.Name, d, depth)).ToArray();
        private PropertyInputState CaptureInput(PropertyPath path, Type type, string name, string label,
            PropertyFieldMetadata description, int depth)
        {
            if (!inputIds.TryGetValue(path, out var id))
            {
                id = new PropertyFieldId(fieldIssuer, inputGeneration, inputPaths.Count, ViewKey(path));
                inputIds.Add(path, id); inputPaths.Add(id, path);
            }
            inputTypes[id] = type;
            if (description != null) inputDescriptions[id] = description;
            var value = path.Read();
            var children = Array.Empty<PropertyInputState>();
            object display = null;
            if (value is IList list)
            {
                if (depth <= 6)
                {
                    var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    children = Enumerable.Range(0, list.Count).Select(index => CaptureInput(path.Element(index),
                        elementType, index.ToString(), "Element " + index, null, depth + 1)).ToArray();
                }
            }
            else if (value != null)
            {
                var actual = value.GetType();
                if (actual.IsPrimitive || actual.IsEnum || value is string || value is decimal
                    || value is UnityEngine.Object || value is UnityEngine.Vector2 || value is UnityEngine.Vector3
                    || value is UnityEngine.Vector4 || value is UnityEngine.Vector2Int || value is UnityEngine.Vector3Int
                    || value is UnityEngine.Color || value is UnityEngine.Rect || value is UnityEngine.RectInt
                    || value is UnityEngine.Bounds || value is UnityEngine.BoundsInt || value is UnityEngine.AnimationCurve)
                    display = CopyDisplayValue(value);
                else children = CaptureInputs(value, path, depth + 1);
            }
            var options = description == null ? Array.Empty<string>() : description.Constants.Select(field =>
                field.GetCustomAttribute<MenuNameAttribute>()?.showName ?? field.Name).ToArray();
            return new PropertyInputState(id, name, label, type, display, value == null, path.IsMixed, description,
                options, description?.OptionIndex(value) ?? -1, children);
        }
        private void SynchronizeInputs()
        {
            var selected = selection?.Version ?? 0;
            if (inputSequenceVersion == editing.Version && inputSelectionVersion == selected) return;
            if (!ReferenceEquals(inputSequence, editing.Sequence) || inputSelectionVersion != selected) viewKeys.Clear();
            inputSequence = editing.Sequence;
            inputSequenceVersion = editing.Version; inputSelectionVersion = selected; inputGeneration++;
            inputPaths.Clear(); inputIds.Clear();
            inputDescriptions.Clear(); inputTypes.Clear();
        }
        private object ViewKey(PropertyPath path)
        {
            if (!viewKeys.TryGetValue(path, out var key)) viewKeys[path] = key = new object();
            return key;
        }
        private PropertyPath ResolveInput(PropertyFieldId id)
        {
            SynchronizeInputs();
            if (!ReferenceEquals(id.Issuer, fieldIssuer) || id.Generation != inputGeneration
                || !inputPaths.TryGetValue(id, out var path))
                throw new InvalidOperationException("属性字段已失效，请刷新属性界面。");
            path.ValidateOwner();
            return path;
        }
        public void Write(PropertyFieldId id, object value)
        {
            var path = ResolveInput(id);
            if (!inputTypes.ContainsKey(id)) throw new InvalidOperationException("不能替换属性目标。");
            Write(path, value);
        }
        public void Rename(PropertyFieldId id, string value) => Rename(ResolveInput(id).Read() as IDirectable, value);
        public void Timing(PropertyFieldId id, float start, float end)
        {
            var clip = ResolveInput(id).Read() as ActionClip;
            if (clip == null) throw new InvalidOperationException("时间编辑目标已失效。");
            Timing(clip, start, end);
        }
        public void Blend(PropertyFieldId id, bool incoming, float value)
        {
            var clip = ResolveInput(id).Read() as ActionClip;
            if (clip == null) throw new InvalidOperationException("混合编辑目标已失效。");
            Blend(clip, incoming, value);
        }
        public bool CanInput(PropertyFieldId id)
        {
            try { return CanInput(ResolveInput(id)); }
            catch (InvalidOperationException) { return false; }
        }
        public PropertyDraft BeginDraft(PropertyFieldId id)
        {
            var path = ResolveInput(id);
            var type = inputTypes[id];
            if (type != typeof(string) && type != typeof(int) && type != typeof(float))
                throw new InvalidOperationException("该字段不支持延迟文本输入。");
            var input = BeginDraft(path);
            input.FieldId = id;
            return input;
        }
        public void Append(PropertyFieldId id) => Append(ResolveInput(id), inputTypes[id]);
        public void Remove(PropertyFieldId id, int index) => Remove(ResolveInput(id), inputTypes[id], index);
        public void CreateValue(PropertyFieldId id)
        {
            var path = ResolveInput(id);
            Write(path, CreateElement(inputTypes[id]));
        }
        public void SelectOption(PropertyFieldId id, int index)
        {
            var path = ResolveInput(id);
            if (!inputDescriptions.TryGetValue(id, out var description)
                || index < 0 || index >= description.Constants.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            // 声明解析留在Presenter，不向扩展交付反射成员。
            Write(path, description.Constants[index].GetRawConstantValue());
        }
        // 业务有效性与存储结构分开：有限但无效的参数仍可定位并通过普通事务修正。
        public IReadOnlyList<PropertyIssueState> CaptureIssues() => editing.Sequence == null
            ? Array.Empty<PropertyIssueState>()
            : editing.Sequence.directables.Select((item, key) => (item, key))
                .Where(entry => entry.item is ActionClip clip && !clip.IsValid)
                .Select(entry => new PropertyIssueState(entry.key, editing.Version, "检查片段参数：" + entry.item.Name)).ToArray();
        public void SelectIssue(PropertyIssueState issue)
        {
            if (issue.Version != editing.Version || editing.Sequence == null || issue.Key < 0
                || issue.Key >= editing.Sequence.directables.Count)
                throw new InvalidOperationException("诊断目标已改变，请刷新列表。");
            var item = editing.Sequence.directables[issue.Key] as ActionClip;
            if (item == null || item.IsValid) throw new InvalidOperationException("诊断已失效，请刷新列表。");
            CancelDraft();
            selection?.Set(new IDirectable[] { item }, false);
        }
        // 路径绑定创建时的选择代次，不让已移除的控件继续编辑旧选择。
        private PropertyPath Path(object owner)
        {
            if (selection == null || !selection.Contains(owner as IDirectable)) return new PropertyPath(editing, owner);
            var version = selection.Version;
            return new PropertyPath(editing, owner, selection.Items.Where(item => item.GetType() == owner.GetType()).Cast<object>(),
                () => selection.Version == version);
        }
        private IReadOnlyList<PropertyFieldMetadata> Fields(object owner) => PropertyFieldMetadata.For(owner.GetType())
            .Where(d => !d.Field.IsInitOnly && d.Visible(owner)).OrderBy(d => d.Field.DeclaringType == d.Field.ReflectedType ? 1 : 0).ToArray();
        // 曲线控件可以原位修改，显示副本必须与作者对象分离。
        private static object CopyDisplayValue(object value) => value is UnityEngine.AnimationCurve curve
            ? new UnityEngine.AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode }
            : value;
        private object CreateElement(Type type) => editing.CreateElement(type);
        private void Write(PropertyPath path, object value)
        {
            RequireAvailableEdit();
            path.Transform(_ => editing.CopyValue(value));
        }
        private bool CanInput(PropertyPath path) => path.CanEdit && !editing.IsEditing;

        // 同一窗口只保留当前焦点字段；另一窗口的修改不能被旧草稿覆盖。
        private PropertyDraft BeginDraft(PropertyPath path)
        {
            RequireAvailableEdit();
            path.ValidateOwner();
            if (draft != null && (draft.Version != editing.Version || !draft.Path.CanEdit)) CancelDraft();
            if (draft != null && draft.Path.SameLocation(path)) return draft;
            CancelDraft();
            return draft = new PropertyDraft(path, editing.Version);
        }
        public void PreviewDraft(PropertyDraft input, object value)
        {
            RequireDraft(input);
            input.Value = value;
        }
        public void CommitDraft(PropertyDraft input, object value)
        {
            RequireDraft(input);
            if (editing.IsEditing) throw new InvalidOperationException("请先完成当前编辑。");
            try
            {
                input.Path.ValidateOwner();
                if (input.Version != editing.Version || !input.Originals.SequenceEqual(input.Path.ReadValues()))
                    throw new InvalidOperationException("属性已被其他操作修改，请重新输入。");
            }
            catch
            {
                // 已失效的路径不能继续占用输入；取消不影响其他窗口的作者事务。
                CancelDraft(input);
                throw;
            }
            input.Value = value;
            // 先清输入所有权，提交通知即使同步重建控件也不会二次提交。
            CancelDraft(input);
            if (input.Originals.Any(original => !Equals(original, value))) Write(input.Path, value);
        }
        public void CancelDraft(PropertyDraft input = null)
        {
            if (input != null && !ReferenceEquals(input, draft)) return;
            if (draft != null) draft.Active = false;
            draft = null;
        }
        private void RequireDraft(PropertyDraft input)
        {
            if (input == null || !input.Active || !ReferenceEquals(input, draft))
                throw new InvalidOperationException("属性输入已取消或失去焦点。");
            if (input.FieldId.HasValue)
            {
                try { ResolveInput(input.FieldId.Value); }
                catch { CancelDraft(input); throw; }
            }
        }
        private void Append(PropertyPath path, Type collectionType)
        {
            RequireAvailableEdit();
            var elementType = collectionType.IsArray ? collectionType.GetElementType() : collectionType.GetGenericArguments()[0];
            // 列表操作会原地修改容器；候选必须脱离作者图，不能早于事务改动快照基线。
            path.Transform(current => CollectionEditing.Append(collectionType, (IList)editing.CopyValue(current), CreateElement(elementType)));
        }
        private void Remove(PropertyPath path, Type collectionType, int index)
        {
            RequireAvailableEdit();
            // 整批候选预检成功前不触碰任何原列表，后续目标越界也不会留下半次删除。
            path.Transform(current => CollectionEditing.Remove(collectionType, (IList)editing.CopyValue(current), index));
        }
        private void RequireAvailableEdit()
        {
            if (editing.IsEditing) throw new InvalidOperationException("请先完成当前编辑。");
        }
        private void Rename(IDirectable item, string value)
        {
            RequireAvailableEdit();
            nodes.Rename(item, value);
        }

        // 父级溢出、邻居边界与混合能力只在共享显示层解释一次。
        private ClipPropertyState ClipState(ActionClip clip)
        {
            var parentStart = clip.Parent?.StartTime ?? 0;
            var parentEnd = clip.Parent?.EndTime ?? clip.EndTime;
            var bounds = ClipTimingRules.NeighborBounds(clip, clip.GetPreviousSibling(), clip.GetNextSibling(), true);
            var warning = clip.StartTime > parentEnd || clip.EndTime < parentStart ? ClipPropertyWarning.Outside
                : clip.EndTime > parentEnd ? ClipPropertyWarning.EndAfterParent
                : clip.StartTime < parentStart ? ClipPropertyWarning.StartBeforeParent : ClipPropertyWarning.None;
            return new ClipPropertyState(
                valid: clip.IsValid, scalable: clip.CanScale(),
                showRange: clip.StartTime >= parentStart && clip.EndTime <= parentEnd && bounds.y > bounds.x,
                start: clip.StartTime, end: clip.EndTime,
                minimum: bounds.x, maximum: bounds.y, pointMaximum: Math.Max(0, parentEnd),
                blendInEnabled: clip.Length > 0 && clip.CanBlendIn(), blendOutEnabled: clip.Length > 0 && clip.CanBlendOut(),
                blendIn: clip.BlendIn, blendOut: clip.BlendOut,
                blendInMaximum: Math.Max(0, clip.Length - clip.BlendOut), blendOutMaximum: Math.Max(0, clip.Length - clip.BlendIn),
                warning: warning);
        }

        private void Timing(ActionClip clip, float start, float end) => clips.SetTiming(clip, start, end, Settings.SnapInterval);
        private void Blend(ActionClip clip, bool incoming, float value) => clips.SetBlend(clip, incoming, value);
    }
}
