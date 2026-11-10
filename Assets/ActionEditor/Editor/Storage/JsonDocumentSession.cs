using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using FullSerializer;

namespace NBC.ActionEditor.Storage
{
    public sealed class JsonUndoState : ScriptableObject
    {
        [TextArea] public string Json;
    }

    /// <summary>One document shared by both renderers. Undo owns serialized snapshots, not Inspector proxies.</summary>
    public sealed class JsonDocumentSession : IDisposable
    {
        public Asset Document { get; private set; }
        public string Path { get; private set; }
        public bool IsDirty => Document != null && Content(Document) != saved;
        public event Action Changed;
        public bool IsEditing => pending != null;
        [Serializable] private sealed class Recovery
        {
            public string Json, Path, Saved, Revision;
        }
        public string CaptureRecovery() => Document == null ? "" : JsonUtility.ToJson(new Recovery
        {
            Json = Encode(Document), Path = Path, Saved = saved, Revision = revision == null ? null : Convert.ToBase64String(revision)
        });
        public void RestoreRecovery(string json)
        {
            if (string.IsNullOrEmpty(json)) { Open(null); return; }
            var data = JsonUtility.FromJson<Recovery>(json);
            var document = Decode(data.Json);
            var bytes = string.IsNullOrEmpty(data.Revision) ? null : Convert.FromBase64String(data.Revision);
            ReleaseState(); Document = document; Path = data.Path; saved = data.Saved; revision = bytes; pending = null;
            state = ScriptableObject.CreateInstance<JsonUndoState>(); state.hideFlags = HideFlags.HideAndDontSave; state.Json = Encode(document);
            Changed?.Invoke();
        }
        private JsonUndoState state;
        private string saved;
        private byte[] revision;
        private string pending;
        public JsonDocumentSession() { Undo.undoRedoPerformed += UndoRedo; }

        public static string Encode(Asset asset)
        {
            if (asset == null) throw new InvalidOperationException("没有打开的文档。");
            asset.OnBeforeSerialize();
            return Json.Serialize(asset);
        }

        public static Asset Decode(string json)
        {
            var asset = Json.Deserialize(typeof(Asset), json) as Asset;
            if (asset == null) throw new InvalidDataException("无法读取 ActionEditor JSON；当前文档未改变。");
            RequireValid(asset);
            asset.Init();
            return asset;
        }

        public static void RequireValid(Asset asset)
        {
            if (asset.groups == null) throw new InvalidDataException("分组列表为空引用。");
            var seen = new System.Collections.Generic.HashSet<object>();
            foreach (var group in asset.groups)
            {
                if (group == null || !seen.Add(group) || group.Tracks == null) throw new InvalidDataException("无效或共享的分组。");
                foreach (var track in group.Tracks)
                {
                    if (track == null || !seen.Add(track) || track.Clips == null) throw new InvalidDataException("无效或共享的轨道。");
                    foreach (var clip in track.Clips)
                    {
                        if (clip == null || !seen.Add(clip) || !Finite(clip.StartTime) || !Finite(clip.Length) || clip.StartTime < 0 || clip.Length < 0
                            || !Finite(clip.EndTime) || !Finite(clip.BlendIn) || !Finite(clip.BlendOut))
                            throw new InvalidDataException("无效片段或非有限时间，拒绝保存。");
                    }
                }
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Open(TextAsset text)
        {
            var candidate = text == null ? null : Decode(text.text);
            var path = text == null ? null : AssetDatabase.GetAssetPath(text);
            var bytes = !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllBytes(path) : null;
            var encoded = candidate == null ? null : Encode(candidate);
            var content = candidate == null ? null : Normalize(encoded);
            ReleaseState();
            Document = candidate; Path = path; revision = bytes; pending = null;
            if (candidate != null)
            {
                state = ScriptableObject.CreateInstance<JsonUndoState>(); state.hideFlags = HideFlags.HideAndDontSave;
                state.Json = encoded; saved = content;
            }
            else saved = null;
            Changed?.Invoke();
        }

        public void BeginEdit() { if (Document != null && pending == null) pending = Encode(Document); }
        public void EndEdit(string label = "Edit ActionEditor document")
        {
            if (pending == null || Document == null) return;
            var before = pending;
            string after;
            try { RequireValid(Document); Document.Init(); after = Encode(Document); }
            catch { CancelEdit(); throw; }
            pending = null;
            if (Normalize(before) == Normalize(after)) return;
            state.Json = before;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(state, label);
            state.Json = after;
            EditorUtility.SetDirty(state);
            Undo.CollapseUndoOperations(group);
            Changed?.Invoke();
        }
        public void Edit(string label, Action action)
        {
            var ownsEdit = !IsEditing;
            BeginEdit();
            try { action(); if (ownsEdit) EndEdit(label); }
            catch { CancelEdit(); throw; }
        }
        public void CancelEdit()
        {
            if (pending == null) return;
            var before = pending; pending = null; Document = Decode(before); Changed?.Invoke();
        }
        private void UndoRedo()
        {
            if (state == null || string.IsNullOrEmpty(state.Json)) return;
            Document = Decode(state.Json); pending = null; Changed?.Invoke();
        }

        public void Save(string target = null)
        {
            if (Document == null) throw new InvalidOperationException("没有打开的文档。");
            target = ValidatePath(target ?? Path);
            var same = string.Equals(target, Path, StringComparison.OrdinalIgnoreCase);
            if (File.Exists(target) && (!same || revision == null)) throw new IOException("另存目标已存在，拒绝覆盖。");
            var serialized = CreateSaveSnapshot();
            new ReliableFileCommit().Save(target, () => new UTF8Encoding(false).GetBytes(serialized), data =>
            {
                var decoded = Decode(Encoding.UTF8.GetString(data));
                if (decoded.GetType() != Document.GetType()) throw new InvalidDataException("文档类型读回不一致。");
                var differences = new System.Collections.Generic.List<string>();
                FindDifferences(fsJsonParser.Parse(Normalize(serialized)), fsJsonParser.Parse(Normalize(Json.Serialize(decoded))), "$", differences);
                if (differences.Count != 0) throw new InvalidDataException("JSON 读回内容不一致，拒绝提交。差异字段：" + string.Join(", ", differences.Take(8)));
            }, same ? revision : null);
            // Advance the revision before import. A failed import must not make the committed bytes unknown.
            Path = target; revision = File.ReadAllBytes(target); saved = Content(Document);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(target) == null) throw new IOException("JSON 字节已提交，但导入失败，请检查目标，不要重复创建。");
            Changed?.Invoke();
        }
        public string CreateSaveSnapshot()
        {
            if (Document == null) throw new InvalidOperationException("没有打开的文档。");
            RequireValid(Document);
            // Public graph edits and serialization callbacks may change the last active clip.
            // Decode rebuilds this cache too; serialize the same derived duration on both sides.
            Document.OnBeforeSerialize();
            RequireValid(Document);
            Document.UpdateMaxTime();
            return Json.Serialize(Document);
        }
        public static string ValidatePath(string path)
        {
            path = path?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("请先选择 JSON 保存路径。");
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || path.Contains("..") || path.Contains(":") || !Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                throw new ArgumentException("文档必须保存在 Assets 下已有目录的 .json 文件中。");
            if (path.StartsWith("Assets/CombatEditor/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets/ScriptableObjects/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("原始 Combat/ScriptableObjects 资源不在编辑范围内。");
            return path;
        }
        private void ReleaseState() { if (state != null) { Undo.ClearUndo(state); UnityEngine.Object.DestroyImmediate(state); } state = null; }
        private static string Content(Asset asset)
        {
            return Normalize(Json.Serialize(asset));
        }
        public string[] InspectReadbackDifferences()
        {
            if (Document == null) return Array.Empty<string>();
            var source = Json.Serialize(Document);
            var decoded = Decode(source);
            var changes = new System.Collections.Generic.List<string>();
            FindDifferences(fsJsonParser.Parse(Normalize(source)), fsJsonParser.Parse(Normalize(Json.Serialize(decoded))), "$", changes);
            return changes.ToArray();
        }
        private static void FindDifferences(fsData before, fsData after, string path, System.Collections.Generic.List<string> changes)
        {
            if (before.IsDictionary && after.IsDictionary)
            {
                foreach (var key in before.AsDictionary.Keys.Union(after.AsDictionary.Keys))
                {
                    if (!before.AsDictionary.TryGetValue(key, out var a) || !after.AsDictionary.TryGetValue(key, out var b)) changes.Add(path + "." + key + " (missing)");
                    else FindDifferences(a, b, path + "." + key, changes);
                }
            }
            else if (before.IsList && after.IsList)
            {
                if (before.AsList.Count != after.AsList.Count) changes.Add(path + " (count)");
                for (var i = 0; i < Math.Min(before.AsList.Count, after.AsList.Count); i++) FindDifferences(before.AsList[i], after.AsList[i], path + "[" + i + "]", changes);
            }
            else if (!before.Equals(after)) changes.Add(path + " (value)");
        }
        private static string Normalize(string json)
        {
            var data = fsJsonParser.Parse(json);
            if (data.IsDictionary)
            {
                var root = data.AsDictionary;
                foreach (var key in root.Keys.Where(k => k.Equals("viewTimeMin", StringComparison.OrdinalIgnoreCase)
                    || k.Equals("viewTimeMax", StringComparison.OrdinalIgnoreCase) || k.Equals("viewTime", StringComparison.OrdinalIgnoreCase)).ToArray()) root.Remove(key);
                if (root.TryGetValue("groups", out var groups) && groups.IsList)
                    foreach (var group in groups.AsList)
                        if (group.IsDictionary)
                            foreach (var key in group.AsDictionary.Keys.Where(k => k.Equals("isCollapsed", StringComparison.OrdinalIgnoreCase)).ToArray()) group.AsDictionary.Remove(key);
            }
            return fsJsonPrinter.CompressedJson(data);
        }
        public void Dispose() { Undo.undoRedoPerformed -= UndoRedo; ReleaseState(); Document = null; }
    }
}
