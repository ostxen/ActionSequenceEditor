using System;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Application.Ports;
using ActionSequenceEditor.Model;

namespace ActionSequenceEditor.Application.Editing
{
    // 属性变更进入作者事务，路径重验所属图与锁定状态。
    public sealed class PropertyEditing
    {
        private readonly SequenceSession session;
        private readonly IPropertyValueFactory values;
        public ActionSequence Sequence => session.Sequence;
        public long Version => session.Version;
        public bool IsEditing => session.IsEditing;
        public PropertyEditing(SequenceSession session, IPropertyValueFactory values)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.values = values ?? throw new ArgumentNullException(nameof(values)); }
        public void Change(Action change) => session.Edit("Edit properties", change);
        public object CreateElement(Type type) => values.Create(type);
        public object CopyValue(object value) => values.Copy(value);
    }
}
