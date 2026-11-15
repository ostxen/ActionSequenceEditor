using System;
using NBC.ActionEditor.Presentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Infrastructure.Documents
{
    // Unity来源身份和资源加载留在边界；Presenter决定何时执行切换。
    public sealed class DocumentNavigation : IDocumentNavigation
    {
        private readonly Application.Documents.DocumentSession session;
        private readonly Application.Documents.DocumentLifecycle lifecycle;
        private readonly JsonDocumentRepository repository;
        private readonly Application.Playback.DocumentPlayback playback;
        private TextAsset source;
        public TextAsset Source => string.IsNullOrEmpty(session.Path) ? source : repository.Load(session.Path);
        public DocumentNavigation(Application.Documents.DocumentSession session, Application.Documents.DocumentLifecycle lifecycle,
            JsonDocumentRepository repository, Application.Playback.DocumentPlayback playback)
        { this.session = session; this.lifecycle = lifecycle; this.repository = repository; this.playback = playback; }
        public bool IsCurrent(object value) => ReferenceEquals(value, Source);
        public bool Accepts(object source)
        {
            if (source is not TextAsset text) return false;
            var path = AssetDatabase.GetAssetPath(text);
            if (string.IsNullOrEmpty(path) || JsonDocumentRepository.IsDocumentPath(path)) return true;
            Debug.LogWarning("请选择 .json 格式的 ActionEditor 文档。"); return false;
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
