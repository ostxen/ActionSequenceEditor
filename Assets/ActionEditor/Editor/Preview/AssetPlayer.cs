using NBC.ActionEditor.Domain;
using System;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 持有当前采样时间及唯一预览会话，重入Reset使旧采样结果失效。
    public class AssetPlayer
    {
        private readonly Func<ActionDocument> document;
        private readonly ActionEditorPreviewSession preview;
        private float currentTime;
        private long generation;
        public event Action Faulted;
        public float previousTime { get; private set; }
        public bool IsInitialized => preview.IsInitialized;
        public AssetPlayer(Func<ActionDocument> document, Func<Type, PreviewBase> factory = null)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            preview = new ActionEditorPreviewSession(factory ?? ActionEditorTypeCatalog.CreatePreview);
        }
        public ActionDocument Asset => document();
        public float CurrentTime { get => currentTime; set => currentTime = Mathf.Clamp(value, 0, Length); }
        public float Length => Asset?.Length ?? 0;
        public void Reset() { generation++; previousTime = 0; preview.Dispose(); }
        public void Sample() => Sample(currentTime);
        public void Sample(float time)
        {
            if (Asset == null) { Reset(); return; }
            CurrentTime = time;
            if (IsInitialized && (currentTime == 0 || currentTime == Length) && previousTime == currentTime) return;
            var version = generation;
            try
            {
                if (!IsInitialized) InitializePreviewPointers();
                preview.Sample(currentTime, previousTime, UnityEngine.Application.isPlaying);
                if (generation == version) previousTime = currentTime;
            }
            catch { if (generation == version) { Reset(); Faulted?.Invoke(); } throw; }
        }
        public void InitializePreviewPointers() { previousTime = 0; preview.Initialize(Asset); }
    }
}