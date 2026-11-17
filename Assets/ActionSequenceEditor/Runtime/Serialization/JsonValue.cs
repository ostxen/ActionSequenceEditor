using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ActionSequenceEditor.Serialization
{
    // 保存与磁盘格式无关的只读值树，供字段转换及作者内容比较共用。
    public sealed class JsonValue
    {
        public enum ValueKind { Null, Boolean, Number, String, Array, Object }
        public ValueKind Kind { get; }
        public string Scalar { get; }
        public IReadOnlyList<JsonValue> Items { get; }
        public IReadOnlyDictionary<string, JsonValue> Members { get; }
        private JsonValue(ValueKind kind, string scalar = null, IEnumerable<JsonValue> items = null, IDictionary<string, JsonValue> members = null)
        {
            Kind = kind; Scalar = scalar;
            Items = items == null ? null : Array.AsReadOnly(items.ToArray());
            Members = members == null ? null : new System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonValue>(new SortedDictionary<string, JsonValue>(members, StringComparer.Ordinal));
        }
        public static JsonValue Null() => new JsonValue(ValueKind.Null);
        public static JsonValue Boolean(bool value) => new JsonValue(ValueKind.Boolean, value ? "true" : "false");
        public static JsonValue Number(string value) => new JsonValue(ValueKind.Number, value);
        public static JsonValue String(string value) => value == null ? Null() : new JsonValue(ValueKind.String, value);
        public static JsonValue List(IEnumerable<JsonValue> items) => new JsonValue(ValueKind.Array, items: items);
        public static JsonValue Object(IDictionary<string, JsonValue> members) => new JsonValue(ValueKind.Object, members: members);
        // 类型及长度前缀区分值边界；顺序遵守字段契约，不以JSON排版比较内容。
        public string ContentKey()
        {
            var result = new StringBuilder();
            AppendKey(result); return result.ToString();
        }
        private void AppendKey(StringBuilder result)
        {
            result.Append((int)Kind).Append(':');
            if (Scalar != null) result.Append(Scalar.Length).Append(':').Append(Scalar);
            if (Items != null) { result.Append(Items.Count).Append('['); foreach (var item in Items) item.AppendKey(result); result.Append(']'); }
            if (Members != null) { result.Append(Members.Count).Append('{'); foreach (var pair in Members) { result.Append(pair.Key.Length).Append(':').Append(pair.Key); pair.Value.AppendKey(result); } result.Append('}'); }
        }
    }

}
