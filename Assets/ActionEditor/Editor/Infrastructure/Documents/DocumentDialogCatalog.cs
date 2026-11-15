using NBC.ActionEditor.Infrastructure.Assets;
using System;
using System.Linq;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Infrastructure.Serialization;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Infrastructure.Documents
{
    // 文档弹窗只读目录与输入约束，不持有会话或执行文档操作。
    public sealed class DocumentDialogCatalog : IDocumentDialogCatalog
    {
        public Type[] CreationTypes() => ActionEditorTypeCatalog.DerivedFrom(typeof(ActionDocument)).Select(i => i.type)
            .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null).Distinct().OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        public string ValidateNewPath(string path) => new Infrastructure.Documents.JsonDocumentRepository().ValidateNewPath(path);
        public bool IsDocumentPath(string path) => JsonDocumentRepository.IsDocumentPath(path);
        public string[] ExportIds() => DocumentFormats.Registry.Exportable.Select(f => f.Id).ToArray();
        public string[] ExportLabels() => DocumentFormats.Registry.Exportable.Select(f => f.DisplayName).ToArray();
        public string ExportExtension(string id) => DocumentFormats.Registry.ById(id).Extension.TrimStart('.');

        // 文件名策略只约束创建请求，视图不解释文件系统规则。
        public string ValidateName(string value)
        {
            var name = value?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..")
                throw new ArgumentException("Invalid document name.", nameof(value));
            return name;
        }

    }
}
