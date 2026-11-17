using System;
using ActionSequenceEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Infrastructure.Sequences
{
    // Unity来源身份和资源加载留在边界；Presenter决定何时执行切换。
    public sealed class SequenceNavigation : ISequenceNavigation
    {
        private readonly Application.Sequences.SequenceSession session;
        private readonly Application.Sequences.SequenceLifecycle lifecycle;
        private readonly SequenceFileRepository repository;
        private readonly Application.Playback.SequencePlayback playback;
        private TextAsset source;
        public TextAsset Source => string.IsNullOrEmpty(session.Path) ? source : repository.Load(session.Path);
        public SequenceNavigation(Application.Sequences.SequenceSession session, Application.Sequences.SequenceLifecycle lifecycle,
            SequenceFileRepository repository, Application.Playback.SequencePlayback playback)
        { this.session = session; this.lifecycle = lifecycle; this.repository = repository; this.playback = playback; }
        public bool IsCurrent(object value) => ReferenceEquals(value, Source);
        public bool Accepts(object source)
        {
            if (source is not TextAsset text) return false;
            var path = AssetDatabase.GetAssetPath(text);
            if (string.IsNullOrEmpty(path) || SequenceFileRepository.IsDocumentPath(path)) return true;
            Debug.LogWarning("请选择 .json 格式的 ActionSequenceEditor 文档。"); return false;
        }
        public void Open(object value)
        {
            var candidate = (TextAsset)value;
            var result = lifecycle.Open(repository.ReadSource(candidate));
            if (!result.Succeeded) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Exception).Throw();
            source = candidate;
            playback.Stop(false);
        }
        public void OpenPath(string path)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (source == null) throw new InvalidOperationException("Unity 未返回已创建的文档资源。");
            Open(source);
        }
        public void Close() => Open(null);
    }
}
