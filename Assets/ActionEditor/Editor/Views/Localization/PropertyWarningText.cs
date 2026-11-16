using NBC.ActionEditor.Presentation.Properties;

namespace NBC.ActionEditor
{
    // 两套属性控件共用当前语言的诊断文案，不互相依赖具体视图。
    public static class PropertyWarningText
    {
        public static string Warning(ClipPropertyWarning warning) => warning == ClipPropertyWarning.Outside ? EditorText.OverflowInvalid
            : warning == ClipPropertyWarning.EndAfterParent ? EditorText.EndTimeOverflowInvalid
            : warning == ClipPropertyWarning.StartBeforeParent ? EditorText.StartTimeOverflowInvalid : null;
    }
}
