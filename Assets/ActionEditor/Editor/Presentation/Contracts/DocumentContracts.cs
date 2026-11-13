using System;

namespace NBC.ActionEditor.Presentation.Contracts
{
    // 文档意图所需的宿主交互，不暴露文件系统或窗口控件。
    public interface IDocumentInteraction
    {
        string ChooseSavePath(bool saveAs);
        void PickDocument();
        void CreateDocument();
        void CloseDocument();
        bool ConfirmSwitch();
        void ShowExport();
        void Saved();
        void Undo(bool redo);
    }
    public enum DocumentIntent { Create, Open, Close, Save, SaveAs, Export, Undo, Redo }
}
