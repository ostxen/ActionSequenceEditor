using System;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Application.Editing
{
    // 属性变更进入作者事务，路径重验所属图与锁定状态。
    public sealed class PropertyEditing
    {
        private readonly DocumentSession session;
        private readonly IPropertyValueFactory values;
        public ActionDocument Document => session.Document;
        public PropertyEditing(DocumentSession session, IPropertyValueFactory values)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.values = values ?? throw new ArgumentNullException(nameof(values)); }
        public void Change(Action change) => session.Edit("Edit properties", change);
        public object CreateElement(Type type) => values.Create(type);
    }
}
