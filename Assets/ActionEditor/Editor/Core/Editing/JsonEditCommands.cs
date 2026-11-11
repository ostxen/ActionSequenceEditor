using System;
using System.Linq;
using UnityEngine;

namespace NBC.ActionEditor.Storage
{
    public static class JsonEditCommands
    {
        private static string clipboard { get => App.Context.Clipboard.Json; set => App.Context.Clipboard.Json = value; }
        private static Type copiedType { get => App.Context.Clipboard.Type; set => App.Context.Clipboard.Type = value; }
        private static IDirectable cutSource { get => App.Context.Clipboard.CutSource; set => App.Context.Clipboard.CutSource = value; }
        public static JsonEditCommand Creation(object parent, Type type, float time = 0) => new JsonEditCommand("Create timeline item",
            () => CreationError(parent, type, time), () =>
            {
                if (parent is Asset asset) asset.AddGroup(type);
                else if (parent is Group group) group.AddTrack(type);
                else if (parent is Track track) track.AddClip(type, time);
            });
        private static string CreationError(object parent, Type type, float time)
        {
            if (App.AssetData == null || type == null || type.IsAbstract || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null)
                return "没有可创建的具体类型。";
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) return "创建时间必须为有限非负值。";
            var info = DirectableTypeInfo.For(type);
            if (parent is Asset asset)
            {
                if (!ReferenceEquals(asset, App.AssetData) || !typeof(Group).IsAssignableFrom(type)) return "目标文档或分组类型已失效。";
                if (info.AttachableTypes != null && !info.AttachableTypes.Contains(asset.GetType())) return "分组类型不适用于当前文档。";
                if (info.IsUnique && asset.groups.Any(g => g.GetType() == type)) return "当前文档已存在唯一分组。";
            }
            else if (parent is IDirectable directable)
            {
                if (!Contains(directable) || directable.IsLocked) return "目标已失效或被锁定。";
                if (parent is Group group)
                { if (!group.CanAddTrackOfType(type)) return "轨道类型、唯一性或挂载规则不允许创建。"; }
                else if (parent is Track track)
                {
                    if (!typeof(Clip).IsAssignableFrom(type) || info.AttachableTypes != null && !info.AttachableTypes.Contains(track.GetType())) return "片段类型不适用于当前轨道。";
                    if (info.IsUnique && track.Clips.Any(c => c.GetType() == type)) return "当前轨道已存在唯一片段。";
                    var category = track.Clips.FirstOrDefault()?.GetType().RTGetAttribute<CategoryAttribute>(true)?.category;
                    if (category != null && category != info.Category) return "片段分类不适用于当前轨道。";
                }
                else return "目标不是分组或轨道。";
            }
            else return "没有有效创建目标。";
            return null;
        }
        public static Group AddGroup(Type type)
        {
            var asset = App.AssetData; var command = Creation(asset, type);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var index = asset.groups.Count;
            command.Execute(); return asset.groups[index];
        }
        public static Track AddTrack(Group group, Type type)
        {
            var command = Creation(group, type);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var index = group.Tracks.Count; command.Execute(); return group.Tracks[index];
        }
        public static Clip AddClip(Track track, Type type, float time)
        {
            var command = Creation(track, type, time);
            if (!command.CanExecute(out var reason)) throw new InvalidOperationException(reason);
            var before = track.Clips.ToArray(); command.Execute(); return track.Clips.Except(before).Single();
        }
        public static JsonEditCommand Deletion(params IDirectable[] items) => new JsonEditCommand("Delete timeline items",
            () => items == null || items.Length == 0 ? "没有选择对象。" : items.Any(i => i == null || !Contains(i) || i.IsLocked) ? "选择中有失效或锁定对象，未删除任何内容。" : null,
            () => { foreach (var item in items.Distinct()) DeleteCore(item); });
        public static JsonEditCommand Reorder(Track track, Group target, int index) => new JsonEditCommand("Move track", () =>
        {
            if (track == null || target == null || !Contains(track) || !Contains(target) || track.IsLocked || target.IsLocked) return "锁定或失效轨道/分组不能重排。";
            if (track.Group != target && !target.CanAddTrackOfType(track.GetType())) return "此轨道类型不适用于目标分组。";
            return null;
        }, () =>
        {
            var source = track.Group; var destination = index;
            if (source == target && source.Tracks.IndexOf(track) < destination) destination--;
            source.Tracks.Remove(track); target.Tracks.Insert(Mathf.Clamp(destination, 0, target.Tracks.Count), track);
        });
        public static bool HasClipboard => clipboard != null;
        public static Type CopiedType => copiedType;
        public static void Copy(IDirectable item, bool cut = false)
        {
            new JsonEditCommand(cut ? "Cut" : "Copy", () => item == null || !Contains(item) || cut && item.IsLocked
                ? "无法复制失效对象或剪切锁定对象。" : null, () =>
            {
                var serialized = Json.Serialize(item);
                clipboard = serialized; copiedType = item.GetType(); cutSource = cut ? item : null;
                App.CopyAsset = item; App.IsCut = cut;
            }).Execute();
        }
        private static string PasteError(IDirectable parent, float time)
        {
            if (clipboard == null || copiedType == null) return "剪贴板为空。";
            if (parent == null || !Contains(parent) || parent.IsLocked) return "粘贴目标已失效或锁定。";
            if (float.IsNaN(time) || float.IsInfinity(time)) return "粘贴时间必须为有限值。";
            if (cutSource != null && Contains(cutSource) && cutSource.IsLocked) return "剪切源已锁定。";
            if (cutSource == parent) return "不能把对象剪切到自身。";
            if (typeof(Track).IsAssignableFrom(copiedType) && parent is Group group)
                return group.CanAddTrackOfType(copiedType) ? null : "轨道类型、唯一性或挂载规则不允许粘贴。";
            if (typeof(Clip).IsAssignableFrom(copiedType) && parent is Track track)
            {
                var info = DirectableTypeInfo.For(copiedType);
                if (info.AttachableTypes != null && !info.AttachableTypes.Contains(track.GetType())) return "片段类型不适用于当前轨道。";
                if (info.IsUnique && track.Clips.Any(c => c.GetType() == copiedType && c != cutSource)) return "当前轨道已存在唯一片段。";
                return null;
            }
            return "剪贴板类型不适用于当前目标。";
        }
        public static bool CanPaste(IDirectable parent) => PasteError(parent, 0) == null;
        public static IDirectable Paste(IDirectable parent, float time = 0)
        {
            IDirectable clone = null;
            new JsonEditCommand("Paste", () => PasteError(parent, time), () =>
            {
                clone = Json.Deserialize(copiedType, clipboard) as IDirectable;
                if (clone == null) throw new InvalidOperationException("剪贴板反序列化失败。");
                if (cutSource != null && Contains(cutSource)) DeleteCore(cutSource);
                if (parent is Group group && clone is Track track) group.Tracks.Add(track);
                else if (parent is Track target && clone is Clip clip) { clip.StartTime = Mathf.Max(0, time); target.Clips.Add(clip); }
                App.AssetData.Init();
            }).Execute();
            cutSource = null; App.IsCut = false; App.Select(clone); return clone;
        }        public static bool Contains(IDirectable item) => item != null && App.AssetData != null && App.AssetData.directables.Contains(item);
        public static void Delete(IDirectable item) => Deletion(item).Execute();
        private static void DeleteCore(IDirectable item)
        {
            if (item == null || item.IsLocked) throw new InvalidOperationException("锁定对象不能删除。");
            if (item is Group group) App.AssetData.groups.Remove(group);
            else if (item is Track track && track.Parent is Group parent) parent.Tracks.Remove(track);
            else if (item is Clip clip && clip.Parent is Track owner) owner.Clips.Remove(clip);
        }
        public static void Duplicate(IDirectable item)
        {
            if (item == null || !Contains(item) || item.IsLocked) throw new InvalidOperationException("锁定或失效对象不能复制副本。");
            var previousJson = clipboard; var previousType = copiedType; var previousCut = cutSource;
            var previousItem = App.CopyAsset; var wasCut = App.IsCut;
            try { Copy(item); Paste(item.Parent, item is Clip clip ? clip.EndTime : 0); }
            catch
            {
                clipboard = previousJson; copiedType = previousType; cutSource = previousCut;
                App.CopyAsset = previousItem; App.IsCut = wasCut; throw;
            }
        }
        public static void Rename(IDirectable item, string name)
        {
            if (item.IsLocked) throw new InvalidOperationException("锁定对象不能重命名。");
            App.Session.Edit("Rename", () =>
            {
                if (item is Group group) group.Name = name ?? "";
                else if (item is Track track) track.Name = name ?? "";
                else if (item is Clip clip) clip.Name = name ?? "";
            });
        }
        public static void ToggleLock(IDirectable item)
        {
            if (item.Parent?.IsLocked == true) throw new InvalidOperationException("请先解锁父分组。");
            App.Session.Edit("Toggle lock", () => item.IsLocked = !item.IsLocked);
        }
        public static void ToggleActive(IDirectable item)
        {
            if (item.IsLocked) throw new InvalidOperationException("请先解锁对象。");
            App.Session.Edit("Toggle active", () => item.IsActive = !item.IsActive);
        }
        public static void DeleteSelection()
        {
            Deletion(App.SelectItems.ToArray()).Execute();
        }

        public static void MoveTrack(Track track, Group target, int index)
        {
            Reorder(track, target, index).Execute();
        }
    }
}
