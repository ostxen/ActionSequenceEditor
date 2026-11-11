using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace NBC.ActionEditor.Serialization
{
    /// <summary>Field/value tree shared by conversion and author-content comparison, independent of a disk format.</summary>
    public sealed class DocumentValue
    {
        public enum ValueKind { Null, Boolean, Number, String, Array, Object }
        public ValueKind Kind { get; }
        public string Scalar { get; }
        public IReadOnlyList<DocumentValue> Items { get; }
        public IReadOnlyDictionary<string, DocumentValue> Members { get; }
        private DocumentValue(ValueKind kind, string scalar = null, IEnumerable<DocumentValue> items = null, IDictionary<string, DocumentValue> members = null)
        {
            Kind = kind; Scalar = scalar;
            Items = items == null ? null : Array.AsReadOnly(items.ToArray());
            Members = members == null ? null : new System.Collections.ObjectModel.ReadOnlyDictionary<string, DocumentValue>(new SortedDictionary<string, DocumentValue>(members, StringComparer.Ordinal));
        }
        public static DocumentValue Null() => new DocumentValue(ValueKind.Null);
        public static DocumentValue Boolean(bool value) => new DocumentValue(ValueKind.Boolean, value ? "true" : "false");
        public static DocumentValue Number(string value) => new DocumentValue(ValueKind.Number, value);
        public static DocumentValue String(string value) => value == null ? Null() : new DocumentValue(ValueKind.String, value);
        public static DocumentValue List(IEnumerable<DocumentValue> items) => new DocumentValue(ValueKind.Array, items: items);
        public static DocumentValue Object(IDictionary<string, DocumentValue> members) => new DocumentValue(ValueKind.Object, members: members);
        // Length-prefixed typed framing, not JSON normalization. Ordering belongs to the field contract.
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

    /// <summary>Strict JSON transport: duplicate names, trailing bytes and non-JSON numbers are errors.</summary>
    public static class DocumentJson
    {
        public static DocumentValue Parse(string text) => new Parser(text).Read();
        public static string Print(DocumentValue value, bool compressed)
        {
            var output = new StringBuilder(); Write(value, output, compressed, 0); return output.ToString();
        }
        private static void Quote(string value, StringBuilder output)
        {
            output.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default: if (c < 32 || char.IsSurrogate(c)) output.Append("\\u").Append(((int)c).ToString("x4")); else output.Append(c); break;
                }
            }
            output.Append('"');
        }
        private static void Line(StringBuilder output, bool compressed, int depth)
        { if (!compressed) output.Append('\n').Append(' ', depth * 2); }
        private static void Write(DocumentValue value, StringBuilder output, bool compressed, int depth)
        {
            switch (value.Kind)
            {
                case DocumentValue.ValueKind.Null: output.Append("null"); break;
                case DocumentValue.ValueKind.Boolean:
                case DocumentValue.ValueKind.Number: output.Append(value.Scalar); break;
                case DocumentValue.ValueKind.String: Quote(value.Scalar, output); break;
                case DocumentValue.ValueKind.Array:
                    output.Append('[');
                    for (var i = 0; i < value.Items.Count; i++) { if (i != 0) output.Append(','); Line(output, compressed, depth + 1); Write(value.Items[i], output, compressed, depth + 1); }
                    if (value.Items.Count != 0) Line(output, compressed, depth); output.Append(']'); break;
                case DocumentValue.ValueKind.Object:
                    output.Append('{'); var first = true;
                    foreach (var pair in value.Members) { if (!first) output.Append(','); first = false; Line(output, compressed, depth + 1); Quote(pair.Key, output); output.Append(compressed ? ":" : ": "); Write(pair.Value, output, compressed, depth + 1); }
                    if (!first) Line(output, compressed, depth); output.Append('}'); break;
            }
        }
        private sealed class Parser
        {
            private readonly string text;
            private int position;
            public Parser(string text) { this.text = text ?? throw new InvalidDataException("$: missing JSON."); }
            private InvalidDataException Error() => new InvalidDataException("$: invalid JSON at offset " + position + ".");
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private bool Take(char c) { Space(); if (position >= text.Length || text[position] != c) return false; position++; return true; }
            public DocumentValue Read() { var value = Value(0); Space(); if (position != text.Length) throw Error(); return value; }
            private DocumentValue Value(int depth)
            {
                if (depth > 128) throw Error(); Space(); if (position >= text.Length) throw Error();
                var c = text[position];
                if (c == '"') return DocumentValue.String(Text());
                if (Take('{'))
                {
                    var members = new Dictionary<string, DocumentValue>(StringComparer.Ordinal);
                    if (!Take('}')) do { Space(); if (position >= text.Length || text[position] != '"') throw Error(); var key = Text(); if (!Take(':') || members.ContainsKey(key)) throw Error(); members.Add(key, Value(depth + 1)); if (Take('}')) return DocumentValue.Object(members); } while (Take(','));
                    else return DocumentValue.Object(members);
                    throw Error();
                }
                if (Take('['))
                {
                    var items = new List<DocumentValue>();
                    if (!Take(']')) do { items.Add(Value(depth + 1)); if (Take(']')) return DocumentValue.List(items); } while (Take(','));
                    else return DocumentValue.List(items);
                    throw Error();
                }
                foreach (var literal in new[] { "null", "true", "false" })
                    if (position + literal.Length <= text.Length && string.CompareOrdinal(text, position, literal, 0, literal.Length) == 0)
                    { position += literal.Length; return literal == "null" ? DocumentValue.Null() : DocumentValue.Boolean(literal == "true"); }
                var start = position;
                if (c == '-') position++;
                if (position >= text.Length) throw Error();
                if (text[position] == '0') position++;
                else { if (text[position] < '1' || text[position] > '9') throw Error(); Digits(); }
                if (position < text.Length && text[position] == '.') { position++; RequireDigits(); }
                if (position < text.Length && (text[position] == 'e' || text[position] == 'E')) { position++; if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++; RequireDigits(); }
                return DocumentValue.Number(text.Substring(start, position - start));
            }
            private void Digits() { while (position < text.Length && text[position] >= '0' && text[position] <= '9') position++; }
            private void RequireDigits() { var start = position; Digits(); if (position == start) throw Error(); }
            private string Text()
            {
                if (!Take('"')) throw Error(); var output = new StringBuilder();
                while (position < text.Length)
                {
                    var c = text[position++]; if (c == '"') return output.ToString(); if (c < 32) throw Error();
                    if (c != '\\') { output.Append(c); continue; }
                    if (position >= text.Length) throw Error();
                    switch (text[position++])
                    {
                        case '"': output.Append('"'); break; case '\\': output.Append('\\'); break; case '/': output.Append('/'); break;
                        case 'b': output.Append('\b'); break; case 'f': output.Append('\f'); break; case 'n': output.Append('\n'); break; case 'r': output.Append('\r'); break; case 't': output.Append('\t'); break;
                        case 'u':
                            if (position + 4 > text.Length || !ushort.TryParse(text.Substring(position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)) throw Error();
                            output.Append((char)code); position += 4; break;
                        default: throw Error();
                    }
                }
                throw Error();
            }
        }
    }
}
