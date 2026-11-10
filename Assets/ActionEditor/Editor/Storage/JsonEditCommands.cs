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
        public static bool HasClipboard => clipboard != null;
        public static Type CopiedType => copiedType;
        public static void Copy(IDirectable item, bool cut = false)
        {
            if (item == null || cut && item.IsLocked) throw new InvalidOperationException("无法剪切锁定对象。");
            clipboard = Json.Serialize(item); copiedType = item.GetType(); cutSource = cut ? item : null;
            App.CopyAsset = item; App.IsCut = cut;
        }
        public static bool CanPaste(IDirectable parent)
        {
            if (parent == null || parent.IsLocked || clipboard == null) return false;
            if (typeof(Track).IsAssignableFrom(copiedType) && parent is Group group) return group.CanAddTrackOfType(copiedType);
            if (typeof(Clip).IsAssignableFrom(copiedType) && parent is Track)
            {
                var attach = copiedType.RTGetAttribute<AttachableAttribute>(true);
                return attach?.Types == null || attach.Types.Contains(parent.GetType());
            }
            return false;
        }
        public static IDirectable Paste(IDirectable parent, float time = 0)
        {
            if (cutSource != null && copiedType == parent?.GetType() && cutSource == parent) throw new InvalidOperationException("不能把对象剪切到自身。");
            if (cutSource is Track && parent is Group cutGroup && cutSource.Parent != cutGroup && cutSource.IsLocked)
                throw new InvalidOperationException("剪切源已锁定。");
            if (!CanPaste(parent)) throw new InvalidOperationException("剪贴板类型不适用于当前轨道/分组，或目标被锁定。");
            var clone = Json.Deserialize(copiedType, clipboard) as IDirectable;
            if (clone == null) throw new InvalidOperationException("剪贴板反序列化失败。");
            App.Session.Edit("Paste", () =>
            {
                if (cutSource != null && Contains(cutSource)) DeleteCore(cutSource);
                if (parent is Group group && clone is Track track) group.Tracks.Add(track);
                else if (parent is Track target && clone is Clip clip) { clip.StartTime = Mathf.Max(0, time); target.Clips.Add(clip); }
                App.AssetData.Init();
            });
            cutSource = null; App.IsCut = false; App.Select(clone); return clone;
        }
        public static bool Contains(IDirectable item) => App.AssetData != null && App.AssetData.directables.Contains(item);
        public static void Delete(IDirectable item) => App.Session.Edit("Delete", () => DeleteCore(item));
        private static void DeleteCore(IDirectable item)
        {
            if (item == null || item.IsLocked) throw new InvalidOperationException("锁定对象不能删除。");
            if (item is Group group) App.AssetData.groups.Remove(group);
            else if (item is Track track && track.Parent is Group parent) parent.Tracks.Remove(track);
            else if (item is Clip clip && clip.Parent is Track owner) owner.Clips.Remove(clip);
        }
        public static void Duplicate(IDirectable item)
        {
            if (item == null || item.IsLocked) throw new InvalidOperationException("锁定对象不能复制副本。");
            var parent = item.Parent;
            Copy(item); Paste(parent, item is Clip clip ? clip.EndTime : 0);
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
            var items = App.SelectItems;
            if (items.Any(i => i.IsLocked)) throw new InvalidOperationException("选择中有锁定对象，未删除任何内容。");
            App.Session.Edit("Delete selection", () => { foreach (var item in items) DeleteCore(item); });
        }

        public static void MoveTrack(Track track, Group target, int index)
        {
            if (track == null || target == null || track.IsLocked || target.IsLocked) throw new InvalidOperationException("锁定轨道或分组不能重排。");
            if (track.Group != target && !target.CanAddTrackOfType(track.GetType())) throw new InvalidOperationException("此轨道类型不适用于目标分组。");
            App.Session.Edit("Move track", () =>
            {
                var source = track.Group; var oldIndex = source.Tracks.IndexOf(track);
                if (source == target && oldIndex < index) index--;
                source.Tracks.Remove(track); target.Tracks.Insert(Mathf.Clamp(index, 0, target.Tracks.Count), track);
            });
        }
    }
}
