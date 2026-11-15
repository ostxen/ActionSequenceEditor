using NBC.ActionEditor.Presentation.Properties;

namespace NBC.ActionEditor
{
    // 两套属性控件共用当前语言的诊断文案，不互相依赖具体视图。
    public static class PropertyText
    {
        public static string Warning(ClipPropertyWarning warning) => warning == ClipPropertyWarning.Outside ? Lan.OverflowInvalid
            : warning == ClipPropertyWarning.EndAfterParent ? Lan.EndTimeOverflowInvalid
            : warning == ClipPropertyWarning.StartBeforeParent ? Lan.StartTimeOverflowInvalid : null;
    }
}
