using System;
using System.Collections.Generic;

namespace ActionSequenceEditor.Presentation.Contracts
{
    // 宿主提供当前可创建类型，不把反射发现或Editor菜单交给视图。
    public interface ITimelineTypeCatalog
    {
        IReadOnlyList<TimelineTypeChoice> DerivedFrom(Type baseType);
    }
    public readonly struct TimelineTypeChoice
    {
        public readonly Type Type;
        public readonly string Name, Category;
        public TimelineTypeChoice(Type type, string name, string category) { Type = type; Name = name; Category = category; }
    }
    // 菜单描述保存已校验的意图，执行时应用用例仍会重新校验。
    public sealed class TimelineMenuEntry
    {
        public string Text { get; }
        public bool Enabled { get; }
        public bool Checked { get; }
        public Action Invoke { get; }
        public TimelineMenuEntry(string text, bool enabled, Action invoke, bool check = false)
        { Text = text; Enabled = enabled; Invoke = invoke; Checked = check; }
    }
    // 原生确认框和重命名窗口由宿主处理，不进入领域或表现计算。
    public interface ITimelineDialogs
    {
        void Delete(string title, Action confirmed);
        void Rename(string initialName, Action<string> submit);
    }
}
