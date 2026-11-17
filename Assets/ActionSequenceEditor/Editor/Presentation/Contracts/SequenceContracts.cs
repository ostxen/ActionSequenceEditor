using System;

namespace ActionSequenceEditor.Presentation.Contracts
{
    // 弹窗只借用目录与输入校验，不自行构造存储设施。
    public interface ISequenceDialogCatalog
    {
        Type[] CreationTypes();
        string ValidateName(string value);
        string ValidateNewPath(string path);
        bool IsDocumentPath(string path);
        string[] ExportIds();
        string[] ExportLabels();
        string ExportExtension(string id);
    }
    // 文档意图所需的宿主交互，不暴露文件系统或窗口控件。
    public interface ISequenceInteraction
    {
        string ChooseSavePath(bool saveAs);
        void PickDocument(Action<object> selected);
        void CreateDocument(Func<Type, string, bool> create);
        SequenceSwitchChoice ConfirmSwitch(bool dirty);
        void ShowExport(Func<string, string, bool> export);
        void Saved();
        void Undo(bool redo);
    }
    public enum SequenceSwitchChoice { Cancel, Save, Discard }
    // 来源身份是不透明宿主对象，读取和切换由文档适配器完成。
    public interface ISequenceNavigation
    {
        bool IsCurrent(object source);
        bool Accepts(object source);
        void Open(object source);
        void OpenPath(string path);
        void Close();
    }
    public enum SequenceIntent { Create, Open, Close, Save, SaveAs, Export, Undo, Redo }
}
