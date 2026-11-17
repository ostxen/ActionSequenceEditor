using ActionSequenceEditor.Infrastructure.Discovery;
using ActionSequenceEditor.Model;
using System;
using UnityEngine;

namespace ActionSequenceEditor.Infrastructure.Preview
{
    // 持有当前采样时间及唯一预览会话，重入Reset使旧采样结果失效。
    public sealed class SequenceSampler : ActionSequenceEditor.Application.Ports.ISequenceSampler, IDisposable
    {
        private readonly ActionSequenceEditor.Application.Sequences.SequenceSession session;
        private readonly Func<ActionSequence> getSequence;
        private readonly SequencePreviewSession preview;
        private float currentTime;
        private long generation;
        public event Action Faulted;
        public float PreviousTime { get; private set; }
        public bool IsInitialized => preview.IsInitialized;
        public SequenceSampler(Func<ActionSequence> getSequence, Func<Type, PreviewBase> factory = null,
            ActionSequenceEditor.Application.Sequences.SequenceSession session = null)
        {
            this.getSequence = getSequence ?? throw new ArgumentNullException(nameof(getSequence));
            preview = new SequencePreviewSession(factory ?? ActionSequenceEditorTypeCatalog.CreatePreview);
            this.session = session;
            if (session != null) session.ContentChanged += Reset;
        }
        public void Dispose() { if (session != null) session.ContentChanged -= Reset; Reset(); }
        private ActionSequence Sequence => getSequence();
        public bool HasSequence => Sequence != null;
        public float RangeMin => Sequence?.RangeMin ?? 0;
        public float RangeMax => Sequence?.RangeMax ?? 0;
        public float CurrentTime { get => currentTime; set => currentTime = Mathf.Clamp(value, 0, Length); }
        public float Length => Sequence?.Length ?? 0;
        public void Reset() { generation++; PreviousTime = 0; preview.Dispose(); }
        public void Sample() => Sample(currentTime);
        public void Sample(float time)
        {
            if (Sequence == null) { Reset(); return; }
            CurrentTime = time;
            if (IsInitialized && (currentTime == 0 || currentTime == Length) && PreviousTime == currentTime) return;
            var version = generation;
            try
            {
                if (!IsInitialized) InitializePreview();
                preview.Sample(currentTime, PreviousTime, UnityEngine.Application.isPlaying);
                if (generation == version) PreviousTime = currentTime;
            }
            catch { if (generation == version) { Reset(); Faulted?.Invoke(); } throw; }
        }
        public void InitializePreview() { PreviousTime = 0; preview.Initialize(Sequence); }
    }
}