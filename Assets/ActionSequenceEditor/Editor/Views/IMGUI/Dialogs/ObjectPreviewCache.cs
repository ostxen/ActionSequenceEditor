using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionSequenceEditor.Views.IMGUI
{
    // 拥有一次对象预览创建的Editor，能力检查失败或抛错时也必须归还。
    internal sealed class ObjectPreviewEditor : IDisposable
    {
        private Editor editor;
        private ObjectPreviewEditor() { }
        public string name => editor.target.name;
        public bool HasPreviewGUI() => editor.HasPreviewGUI();
        public void OnPreviewSettings() => editor.OnPreviewSettings();
        public void OnInteractivePreviewGUI(Rect rect, GUIStyle background) => editor.OnInteractivePreviewGUI(rect, background);
        public string GetInfoString() => editor.GetInfoString();

        public static ObjectPreviewEditor Make(Object target)
        {
            var preview = new ObjectPreviewEditor();
            try
            {
                preview.editor = Editor.CreateEditor(target);
                if (preview.editor != null && preview.editor.HasPreviewGUI()) return preview;
                preview.Dispose();
                return null;
            }
            catch
            {
                // 缓存接管前的失败仍由创建者释放，不销毁传入的用户资源。
                preview.Dispose();
                throw;
            }
        }
        public void Dispose()
        {
            var owned = editor;
            editor = null;
            if (owned != null) Object.DestroyImmediate(owned);
        }
    }

    // 选择器逐窗拥有预览缓存；每个绘制周期回收未使用的Editor，关闭时全部释放。
    internal sealed class ObjectPreviewCache : IDisposable
    {
        private readonly Dictionary<Object, ObjectPreviewEditor> editors = new Dictionary<Object, ObjectPreviewEditor>();
        private readonly HashSet<Object> used = new HashSet<Object>();

        public ObjectPreviewEditor this[Object target]
        {
            get
            {
                if (!editors.TryGetValue(target, out var preview))
                {
                    preview = ObjectPreviewEditor.Make(target);
                    editors.Add(target, preview);
                }
                used.Add(target);
                return preview;
            }
        }
        public void CleanupUntouchedEditors()
        {
            foreach (var target in editors.Keys.Where(target => !used.Contains(target)).ToArray())
            {
                var preview = editors[target];
                editors.Remove(target);
                preview?.Dispose();
            }
            used.Clear();
        }
        public void Dispose()
        {
            used.Clear();
            CleanupUntouchedEditors();
        }
    }
}
