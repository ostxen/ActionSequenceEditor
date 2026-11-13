using System;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Serialization;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Infrastructure.Documents
{
    // 让会话使用受控隔离快照，不依赖具体 JSON 类型。
    public sealed class DocumentSnapshot : IDocumentSnapshot
    {
        public string Capture(ActionDocument document) => JsonDocumentCodec.Encode(document);
        public ActionDocument Restore(string snapshot) => JsonDocumentCodec.Decode(snapshot);
        public string Content(ActionDocument document) => JsonDocumentCodec.Content(document);
        public void ValidateAndInitialize(ActionDocument document) => ActionDocumentGraph.Initialize(document, DocumentFormats.Context);
    }

    // 仅用于 Unity Undo 存储，不作为 Inspector 选择代理。
    public sealed class DocumentUndoState : ScriptableObject { [TextArea] public string Snapshot; }

    // 隐藏对象和全局 Undo 订阅由本适配器独占并释放。
    public sealed class UnityUndoHistory : IUndoHistory
    {
        private DocumentUndoState state;
        private string observedSnapshot;
        private bool disposed;
        public event Action<string> Restored;
        public UnityUndoHistory() { Undo.undoRedoPerformed += OnUndoRedo; }
        public void Reset(string snapshot)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityUndoHistory));
            var next = snapshot == null ? null : ScriptableObject.CreateInstance<DocumentUndoState>();
            if (next != null) { next.hideFlags = HideFlags.HideAndDontSave; next.Snapshot = snapshot; }
            Release();
            state = next;
            observedSnapshot = snapshot;
        }
        public void Record(string before, string after, string label)
        {
            if (state == null) throw new InvalidOperationException("撤销历史未绑定文档。");
            state.Snapshot = before;
            Undo.IncrementCurrentGroup(); var group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(state, label);
            state.Snapshot = after; EditorUtility.SetDirty(state); Undo.CollapseUndoOperations(group);
            observedSnapshot = after;
        }
        // Unity通知所有会话；只替换实际被撤销的图，不能清除另一窗口的事务和选择。
        private void OnUndoRedo()
        {
            if (state == null || state.Snapshot == observedSnapshot) return;
            observedSnapshot = state.Snapshot;
            Restored?.Invoke(observedSnapshot);
        }
        private void Release()
        {
            if (state == null) return;
            Undo.ClearUndo(state); UnityEngine.Object.DestroyImmediate(state); state = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Undo.undoRedoPerformed -= OnUndoRedo; Release();
        }
    }

    // 恢复封套单独验证版本，作者内容仍由快照端口解析。
    public sealed class RecoveryCodec : IRecoveryCodec
    {
        public string Encode(DocumentRecovery recovery) => JsonUtility.ToJson(recovery);
        public DocumentRecovery Decode(string text) => JsonUtility.FromJson<DocumentRecovery>(text);
    }
}
