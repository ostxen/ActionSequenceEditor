using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 属性视图的字段描述、路径与作者提交入口，不持有原生控件。
    public sealed class PropertyPresenter
    {
        private readonly PropertyEditing editing;
        private readonly StructureEditing structure;
        private readonly ClipEditing clips;
        private readonly Func<EditorSettings> settings;
        private readonly SelectionState selection;
        private PropertyDraft draft;
        public PropertyPresenter(PropertyEditing editing, StructureEditing structure, ClipEditing clips, Func<EditorSettings> settings, SelectionState selection = null)
        {
            this.editing = editing ?? throw new ArgumentNullException(nameof(editing));
            this.structure = structure ?? throw new ArgumentNullException(nameof(structure));
            this.clips = clips ?? throw new ArgumentNullException(nameof(clips));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.selection = selection;
        }
        public object Document => editing.Document;
        public object Selected => selection?.First;
        public string Title(object value) => value == null ? string.Empty
            : value.GetType().GetCustomAttribute<NameAttribute>(true)?.name ?? value.GetType().Name;
        public string Description(object value) => value?.GetType().GetCustomAttribute<DescriptionAttribute>(true)?.description;
        public float DocumentLength => editing.Document?.Length ?? 0;
        public bool Locked(object value) => value is IDirectable item && item.IsLocked;
        public EditorSettings Settings => settings();
        // 业务有效性与存储结构分开：有限但无效的参数仍可定位并通过普通事务修正。
        public IReadOnlyList<PropertyIssueState> CaptureIssues() => editing.Document == null
            ? Array.Empty<PropertyIssueState>()
            : editing.Document.directables.Select((item, key) => (item, key))
                .Where(entry => entry.item is TimelineClip clip && !clip.IsValid)
                .Select(entry => new PropertyIssueState(entry.key, editing.Version, "检查片段参数：" + entry.item.Name)).ToArray();
        public void SelectIssue(PropertyIssueState issue)
        {
            if (issue.Version != editing.Version || editing.Document == null || issue.Key < 0
                || issue.Key >= editing.Document.directables.Count)
                throw new InvalidOperationException("诊断目标已改变，请刷新列表。");
            var item = editing.Document.directables[issue.Key] as TimelineClip;
            if (item == null || item.IsValid) throw new InvalidOperationException("诊断已失效，请刷新列表。");
            CancelDraft();
            selection?.Set(new IDirectable[] { item }, false);
        }
        // 路径绑定创建时的选择代次，不让已移除的控件继续编辑旧选择。
        public PropertyPath Path(object owner)
        {
            if (selection == null || !selection.Contains(owner as IDirectable)) return new PropertyPath(editing, owner);
            var version = selection.Version;
            return new PropertyPath(editing, owner, selection.Items.Where(item => item.GetType() == owner.GetType()).Cast<object>(),
                () => selection.Version == version);
        }
        public IReadOnlyList<PropertyDescription> Fields(object owner) => PropertyDescription.For(owner.GetType())
            .Where(d => !d.Field.IsInitOnly && d.Visible(owner)).OrderBy(d => d.Field.DeclaringType == d.Field.ReflectedType ? 1 : 0).ToArray();
        public PropertyDescription Describe(FieldInfo field) => PropertyDescription.For(field);
        public PropertyFieldState Field(object owner, FieldInfo field, PropertyPath parent = null) =>
            CaptureField(Describe(field), (parent ?? Path(owner)).Field(field), 0);

        // 默认视图只接收独立标量、只读集合与子字段；路径仅用于回传编辑意图。
        public IReadOnlyList<PropertyFieldState> CaptureFields(object owner, PropertyPath parent = null)
        {
            if (owner is PropertyObjectState state) return state.Fields;
            if (owner == null) return Array.Empty<PropertyFieldState>();
            return CaptureFields(owner, parent ?? Path(owner), 0);
        }
        private PropertyFieldState[] CaptureFields(object owner, PropertyPath parent, int depth) => depth > 6
            ? Array.Empty<PropertyFieldState>()
            : Fields(owner).Select(description => CaptureField(description, parent.Field(description.Field), depth)).ToArray();
        private PropertyFieldState CaptureField(PropertyDescription description, PropertyPath path, int depth) =>
            new PropertyFieldState(description, path, CaptureValue(path.Read(), path, depth));
        private object CaptureValue(object value, PropertyPath path, int depth)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal || value is UnityEngine.Object
                || value is UnityEngine.Vector2 || value is UnityEngine.Vector3 || value is UnityEngine.Vector4
                || value is UnityEngine.Vector2Int || value is UnityEngine.Vector3Int || value is UnityEngine.Color
                || value is UnityEngine.Rect || value is UnityEngine.RectInt || value is UnityEngine.Bounds || value is UnityEngine.BoundsInt)
                return value;
            if (value is UnityEngine.AnimationCurve) return PropertyFieldState.CopyValue(value);
            if (value is IList list)
            {
                var items = new object[depth > 6 ? 0 : list.Count];
                for (var index = 0; index < items.Length; index++) items[index] = CaptureValue(list[index], path.Element(index), depth + 1);
                return Array.AsReadOnly(items);
            }
            return new PropertyObjectState(CaptureFields(value, path, depth + 1));
        }
        public object CreateElement(Type type) => editing.CreateElement(type);
        public void Write(PropertyPath path, object value)
        {
            RequireAvailableEdit();
            path.Transform(_ => editing.CopyValue(value));
        }
        public bool CanInput(PropertyPath path) => path.CanEdit && !editing.IsEditing;

        // 同一窗口只保留当前焦点字段；另一窗口的修改不能被旧草稿覆盖。
        public PropertyDraft BeginDraft(PropertyPath path)
        {
            RequireAvailableEdit();
            path.ValidateOwner();
            if (draft != null && !draft.Path.CanEdit) CancelDraft();
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
        }
        public void Append(PropertyPath path, Type collectionType)
        {
            RequireAvailableEdit();
            var elementType = collectionType.IsArray ? collectionType.GetElementType() : collectionType.GetGenericArguments()[0];
            // 列表操作会原地修改容器；候选必须脱离作者图，不能早于事务改动快照基线。
            path.Transform(current => CollectionEditing.Append(collectionType, (IList)editing.CopyValue(current), CreateElement(elementType)));
        }
        public void Remove(PropertyPath path, Type collectionType, int index)
        {
            RequireAvailableEdit();
            // 整批候选预检成功前不触碰任何原列表，后续目标越界也不会留下半次删除。
            path.Transform(current => CollectionEditing.Remove(collectionType, (IList)editing.CopyValue(current), index));
        }
        private void RequireAvailableEdit()
        {
            if (editing.IsEditing) throw new InvalidOperationException("请先完成当前编辑。");
        }
        public void Rename(IDirectable item, string value)
        {
            RequireAvailableEdit();
            structure.Rename(item, value);
        }

        // 父级溢出、邻居边界与混合能力只在共享显示层解释一次。
        public ClipPropertyState ClipState(TimelineClip clip)
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

        public void Timing(TimelineClip clip, float start, float end) => clips.SetTiming(clip, start, end, Settings.SnapInterval);
        public void Blend(TimelineClip clip, bool incoming, float value) => clips.SetBlend(clip, incoming, value);
    }
}
