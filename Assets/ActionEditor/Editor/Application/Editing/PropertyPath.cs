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
        private readonly object[] peers;
        private readonly object[] segments;
        private readonly Func<bool> current;
        public PropertyPath(PropertyEditing editing, object value, IEnumerable<object> peers = null, Func<bool> current = null)
            : this(editing, () => value, Array.Empty<object>(), peers?.Where(p => !ReferenceEquals(p, value)).ToArray() ?? Array.Empty<object>(), current) { }
        private PropertyPath(PropertyEditing editing, Func<object> root, object[] segments, object[] peers, Func<bool> current)
        { this.editing = editing; this.root = root; this.segments = segments; this.peers = peers; this.current = current; }
        public PropertyPath Field(FieldInfo field) => new PropertyPath(editing, root, segments.Concat(new object[] { field }).ToArray(), peers, current);
        public PropertyPath Element(int index) => new PropertyPath(editing, root, segments.Concat(new object[] { index }).ToArray(), peers, current);
        // IMGUI每次布局会重建路径，但同一根和字段序列仍指向同一输入。
        public bool SameLocation(PropertyPath other) => other != null && ReferenceEquals(editing, other.editing)
            && ReferenceEquals(root(), other.root()) && segments.SequenceEqual(other.segments)
            && peers.Length == other.peers.Length && peers.Zip(other.peers, ReferenceEquals).All(equal => equal);
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
                foreach (var peer in peers) hash = hash * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(peer);
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
        private object Read(object value)
        {
            foreach (var segment in segments) value = Get(value, segment); return value;
        }
        public object Read() => Read(root());
        public object[] ReadValues() => peers.Prepend(root()).Select(Read).ToArray();
        public bool IsMixed
        {
            get
            {
                try { return Mixed(peers.Select(peer => new PropertyPath(editing, () => peer, segments, Array.Empty<object>(), current))); }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentOutOfRangeException || error is IndexOutOfRangeException)
                { return true; }
            }
        }
        public bool Mixed(IEnumerable<PropertyPath> others)
        {
            var value = Read(); return others.Any(path => !Equals(value, path.Read()));
        }
        public void ValidateOwner()
        {
            // 原生控件可能迟到提交；选择换代后不能写回先前的目标集合。
            if (current != null && !current()) throw new InvalidOperationException("属性选择已改变，请重新输入。");
            ValidateOwner(root());
            foreach (var peer in peers) ValidateOwner(peer);
        }
        private void ValidateOwner(object current)
        {
            if (current is IData data && !ReferenceEquals(current, editing.Document) && (editing.Document == null || !editing.Document.directables.Contains(data as IDirectable)))
                throw new InvalidOperationException("属性所属对象已不在当前文档中。");
            if (current is IDirectable directable && directable.IsLocked) throw new InvalidOperationException("属性所属对象已锁定。");
        }
        public bool CanEdit
        {
            get
            {
                try { ValidateOwner(); ReadValues(); return true; }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentOutOfRangeException || error is IndexOutOfRangeException)
                { return false; }
            }
        }
        public void Set(object value)
        {
            if (peers.Length != 0) { Write(value); return; }
            ValidateOwner();
            Set(root(), value);
        }
        private void Set(object current, object value)
        {
            if (segments.Length == 0) throw new InvalidOperationException("不能替换属性根对象。");
            var owners = new object[segments.Length];
            for (var i = 0; i < segments.Length; i++) { owners[i] = current; current = Get(current, segments[i]); }
            Put(owners[segments.Length - 1], segments[segments.Length - 1], value);
            for (var i = segments.Length - 2; i >= 0; i--)
                if (owners[i + 1].GetType().IsValueType) Put(owners[i], segments[i], owners[i + 1]);
        }
        public void Write(object value) => Transform(_ => editing.CopyValue(value));
        // 先重验整批路径并构造各自候选，再以一个事务写回，避免部分对象成功。
        public void Transform(Func<object, object> change)
        {
            ValidateOwner();
            var targets = peers.Prepend(root()).ToArray();
            var values = targets.Select(target => change(Read(target))).ToArray();
            editing.Change(() =>
            {
                ValidateOwner();
                for (var index = 0; index < targets.Length; index++) Set(targets[index], values[index]);
            });
        }
    }
}
