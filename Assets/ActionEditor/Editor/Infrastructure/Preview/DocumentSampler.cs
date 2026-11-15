using NBC.ActionEditor.Infrastructure.Assets;
using NBC.ActionEditor.Domain;
using System;
using UnityEngine;

namespace NBC.ActionEditor.Infrastructure.Preview
{
    // 持有当前采样时间及唯一预览会话，重入Reset使旧采样结果失效。
    public sealed class DocumentSampler : NBC.ActionEditor.Application.Ports.IDocumentSampler, IDisposable
    {
        private readonly NBC.ActionEditor.Application.Documents.DocumentSession session;
        private readonly Func<ActionDocument> document;
        private readonly DocumentPreviewSession preview;
        private float currentTime;
        private long generation;
        public event Action Faulted;
        public float previousTime { get; private set; }
        public bool IsInitialized => preview.IsInitialized;
        public DocumentSampler(Func<ActionDocument> document, Func<Type, PreviewBase> factory = null,
            NBC.ActionEditor.Application.Documents.DocumentSession session = null)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            preview = new DocumentPreviewSession(factory ?? ActionEditorTypeCatalog.CreatePreview);
            this.session = session;
            if (session != null) session.ContentChanged += Reset;
        }
        public void Dispose() { if (session != null) session.ContentChanged -= Reset; Reset(); }
        private ActionDocument Document => document();
        public bool HasDocument => Document != null;
        public float RangeMin => Document?.RangeMin ?? 0;
        public float RangeMax => Document?.RangeMax ?? 0;
        public float CurrentTime { get => currentTime; set => currentTime = Mathf.Clamp(value, 0, Length); }
        public float Length => Document?.Length ?? 0;
        public void Reset() { generation++; previousTime = 0; preview.Dispose(); }
        public void Sample() => Sample(currentTime);
        public void Sample(float time)
        {
            if (Document == null) { Reset(); return; }
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
        public void InitializePreviewPointers() { previousTime = 0; preview.Initialize(Document); }
    }
}