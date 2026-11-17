using System;
using System.Collections;

namespace ActionSequenceEditor.Application.Editing
{
    // 对数组和列表提供一致的集合语义，由属性事务负责原子提交。
    public static class CollectionEditing
    {
        public static IList Append(Type type, IList list, object value)
        {
            if (!type.IsArray) { var result = list ?? (IList)Activator.CreateInstance(type); result.Add(value); return result; }
            var array = Array.CreateInstance(type.GetElementType(), (list?.Count ?? 0) + 1);
            list?.CopyTo(array, 0); array.SetValue(value, array.Length - 1); return array;
        }
        public static IList Remove(Type type, IList list, int index)
        {
            if (list == null || index < 0 || index >= list.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (!type.IsArray) { list.RemoveAt(index); return list; }
            var array = Array.CreateInstance(type.GetElementType(), list.Count - 1);
            for (int source = 0, dest = 0; source < list.Count; source++) if (source != index) array.SetValue(list[source], dest++);
            return array;
        }
        public static void Move(IList list, int source, int destination)
        {
            if (list == null || source < 0 || destination < 0 || source >= list.Count || destination >= list.Count) throw new ArgumentOutOfRangeException();
            var value = list[source]; var direction = Math.Sign(destination - source);
            if (direction == 0) return;
            for (var index = source; index != destination; index += direction) list[index] = list[index + direction];
            list[destination] = value;
        }
    }
}
