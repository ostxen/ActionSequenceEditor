using System;
using System.Linq;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Composition
{
    // 为原生文档弹窗装配当前用例和显示目录，文件实现不进入View。
    public sealed class DocumentDialogComposition
    {
        private readonly ActionEditorContext context;
        public DocumentPresenter Presenter { get; }
        public string DocumentName => context.Source != null ? context.Source.name : "Action";
        public DocumentDialogComposition(ActionEditorContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Presenter = DocumentPresenterComposition.Create(context);
        }
        public Type[] CreationTypes() => ActionEditorTypeCatalog.DerivedFrom(typeof(ActionDocument)).Select(i => i.type)
            .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null).Distinct().OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        public string ValidateNewPath(string path) => DocumentCreationService.ValidateNewPath(path);
        public string[] ExportIds() => DocumentFormats.Registry.Exportable.Select(f => f.Id).ToArray();
        public string[] ExportLabels() => DocumentFormats.Registry.Exportable.Select(f => f.DisplayName).ToArray();
        public string ExportExtension(string id) => DocumentFormats.Registry.ById(id).Extension.TrimStart('.');
        public bool ConfirmSwitch() => DocumentPresenterComposition.ConfirmSwitch(context);
        public string TypeName(Type type) => type == typeof(Samples.FeatureSample.FeatureSampleDocument) ? Lan.FeatureSample
            : type.GetCustomAttributes(typeof(NameAttribute), false).OfType<NameAttribute>().FirstOrDefault()?.name ?? type.Name.SplitCamelCase();

        // 文件名策略只约束创建请求，视图不解释文件系统规则。
        public string ValidateName(string value)
        {
            var name = value?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..")
                throw new ArgumentException(Lan.InvalidResourceName);
            return name;
        }

        // 创建提交成功后才切换当前资源，失败保持当前会话。
        public void CreateAndOpen(Type type, string path)
        {
            if (!Infrastructure.Assets.ProductTypes.Includes(type)) throw new InvalidOperationException("该类型不属于产品创建目录。");
            if (!Presenter.Create(type, path)) return;
            context.Open(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        }
    }
}
