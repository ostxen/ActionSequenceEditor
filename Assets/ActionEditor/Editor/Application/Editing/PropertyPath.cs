using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Application.Editing
{
    // 写入时逐段解析，并把嵌套值类型写回所属对象。
    public sealed class PropertyPath : IEquatable<PropertyPath>
    {
        private readonly PropertyEditing editing;
        private readonly Func<object> root;
        private readonly object[] segments;
        public PropertyPath(PropertyEditing editing, object value) : this(editing, () => value, Array.Empty<object>()) { }
        private PropertyPath(PropertyEditing editing, Func<object> root, object[] segments) { this.editing = editing; this.root = root; this.segments = segments; }
        public PropertyPath Field(FieldInfo field) => new PropertyPath(editing, root, segments.Concat(new object[] { field }).ToArray());
        public PropertyPath Element(int index) => new PropertyPath(editing, root, segments.Concat(new object[] { index }).ToArray());
        // IMGUI每次布局会重建路径，但同一根和字段序列仍指向同一输入。
        public bool SameLocation(PropertyPath other) => other != null && ReferenceEquals(editing, other.editing)
            && ReferenceEquals(root(), other.root()) && segments.SequenceEqual(other.segments);
        public bool Equals(PropertyPath other) => SameLocation(other);
        public override bool Equals(object other) => other is PropertyPath path && SameLocation(path);
        // 键只取根引用和不可变路径段，值类型装箱及作者值变化不改变路径身份。
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(editing);
                var value = root();
                hash = hash * 397 ^ (value == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value));
                foreach (var segment in segments) hash = hash * 397 ^ segment.GetHashCode();
                return hash;
            }
        }
        private static object Get(object owner, object segment)
        {
            if (owner == null) throw new InvalidOperationException("属性路径的父对象已为空。");
            return segment is FieldInfo field ? field.GetValue(owner) : ((IList)owner)[(int)segment];
        }
        private static void Put(object owner, object segment, object value)
        {
            if (segment is FieldInfo field) field.SetValue(owner, value); else ((IList)owner)[(int)segment] = value;
        }
        public object Read()
        {
            var value = root(); foreach (var segment in segments) value = Get(value, segment); return value;
        }
        public bool Mixed(IEnumerable<PropertyPath> others)
        {
            var value = Read(); return others.Any(path => !Equals(value, path.Read()));
        }
        public void ValidateOwner()
        {
            var current = root();
            if (current is IData data && !ReferenceEquals(current, editing.Document) && (editing.Document == null || !editing.Document.directables.Contains(data as IDirectable)))
                throw new InvalidOperationException("属性所属对象已不在当前文档中。");
            if (current is IDirectable directable && directable.IsLocked) throw new InvalidOperationException("属性所属对象已锁定。");
        }
        public bool CanEdit
        {
            get
            {
                var current = root();
                return (!(current is IData) || ReferenceEquals(current, editing.Document)
                    || editing.Document != null && editing.Document.directables.Contains(current as IDirectable))
                    && (!(current is IDirectable item) || !item.IsLocked);
            }
        }
        public void Set(object value)
        {
            if (segments.Length == 0) throw new InvalidOperationException("不能替换属性根对象。");
            ValidateOwner();
            var owners = new object[segments.Length]; var current = root();
            for (var i = 0; i < segments.Length; i++) { owners[i] = current; current = Get(current, segments[i]); }
            Put(owners[segments.Length - 1], segments[segments.Length - 1], value);
            for (var i = segments.Length - 2; i >= 0; i--)
                if (owners[i + 1].GetType().IsValueType) Put(owners[i], segments[i], owners[i + 1]);
        }
        public void Write(object value) => editing.Change(() => Set(value));
        public void Transform(Func<object, object> change) => editing.Change(() =>
        {
            ValidateOwner(); Set(change(Read()));
        });
    }
}
