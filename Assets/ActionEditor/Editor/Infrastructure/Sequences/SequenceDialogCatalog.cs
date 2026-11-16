using NBC.ActionEditor.Infrastructure.Discovery;
using System;
using System.Linq;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Infrastructure.Serialization;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Infrastructure.Sequences
{
    // 文档弹窗只读目录与输入约束，不持有会话或执行文档操作。
    public sealed class SequenceDialogCatalog : ISequenceDialogCatalog
    {
        public Type[] CreationTypes() => ActionEditorTypeCatalog.DerivedFrom(typeof(ActionSequence)).Select(i => i.type)
            .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null).Distinct().OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        public string ValidateNewPath(string path) => new Infrastructure.Sequences.SequenceFileRepository().ValidateNewPath(path);
        public bool IsDocumentPath(string path) => SequenceFileRepository.IsDocumentPath(path);
        public string[] ExportIds() => SequenceFormats.Registry.Exportable.Select(f => f.Id).ToArray();
        public string[] ExportLabels() => SequenceFormats.Registry.Exportable.Select(f => f.DisplayName).ToArray();
        public string ExportExtension(string id) => SequenceFormats.Registry.ById(id).Extension.TrimStart('.');

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
