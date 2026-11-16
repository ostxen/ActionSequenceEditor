using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NBC.ActionEditor.Serialization
{
    // 严格读写JSON；拒绝重复成员、尾随内容及非法数字。
    public static class JsonText
    {
        public static JsonValue Parse(string text) => new Parser(text).Read();
        public static string Print(JsonValue value, bool compact)
        {
            var output = new StringBuilder(); Write(value, output, compact, 0); return output.ToString();
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
        private static void Line(StringBuilder output, bool compact, int depth)
        { if (!compact) output.Append('\n').Append(' ', depth * 2); }
        private static void Write(JsonValue value, StringBuilder output, bool compact, int depth)
        {
            switch (value.Kind)
            {
                case JsonValue.ValueKind.Null: output.Append("null"); break;
                case JsonValue.ValueKind.Boolean:
                case JsonValue.ValueKind.Number: output.Append(value.Scalar); break;
                case JsonValue.ValueKind.String: Quote(value.Scalar, output); break;
                case JsonValue.ValueKind.Array:
                    output.Append('[');
                    for (var i = 0; i < value.Items.Count; i++) { if (i != 0) output.Append(','); Line(output, compact, depth + 1); Write(value.Items[i], output, compact, depth + 1); }
                    if (value.Items.Count != 0) Line(output, compact, depth); output.Append(']'); break;
                case JsonValue.ValueKind.Object:
                    output.Append('{'); var first = true;
                    foreach (var pair in value.Members) { if (!first) output.Append(','); first = false; Line(output, compact, depth + 1); Quote(pair.Key, output); output.Append(compact ? ":" : ": "); Write(pair.Value, output, compact, depth + 1); }
                    if (!first) Line(output, compact, depth); output.Append('}'); break;
            }
        }
        // 逐字符解析并限制嵌套深度，不加载或构造业务类型。
        private sealed class Parser
        {
            private readonly string text;
            private int position;
            public Parser(string text) { this.text = text ?? throw new InvalidDataException("$: missing JSON."); }
            private InvalidDataException Error() => new InvalidDataException("$: invalid JSON at offset " + position + ".");
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private bool Take(char c) { Space(); if (position >= text.Length || text[position] != c) return false; position++; return true; }
            public JsonValue Read() { var value = Value(0); Space(); if (position != text.Length) throw Error(); return value; }
            private JsonValue Value(int depth)
            {
                if (depth > 128) throw Error(); Space(); if (position >= text.Length) throw Error();
                var c = text[position];
                if (c == '"') return JsonValue.String(Text());
                if (Take('{'))
                {
                    var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                    if (!Take('}')) do { Space(); if (position >= text.Length || text[position] != '"') throw Error(); var key = Text(); if (!Take(':') || members.ContainsKey(key)) throw Error(); members.Add(key, Value(depth + 1)); if (Take('}')) return JsonValue.Object(members); } while (Take(','));
                    else return JsonValue.Object(members);
                    throw Error();
                }
                if (Take('['))
                {
                    var items = new List<JsonValue>();
                    if (!Take(']')) do { items.Add(Value(depth + 1)); if (Take(']')) return JsonValue.List(items); } while (Take(','));
                    else return JsonValue.List(items);
                    throw Error();
                }
                foreach (var literal in new[] { "null", "true", "false" })
                    if (position + literal.Length <= text.Length && string.CompareOrdinal(text, position, literal, 0, literal.Length) == 0)
                    { position += literal.Length; return literal == "null" ? JsonValue.Null() : JsonValue.Boolean(literal == "true"); }
                var start = position;
                if (c == '-') position++;
                if (position >= text.Length) throw Error();
                if (text[position] == '0') position++;
                else { if (text[position] < '1' || text[position] > '9') throw Error(); Digits(); }
                if (position < text.Length && text[position] == '.') { position++; RequireDigits(); }
                if (position < text.Length && (text[position] == 'e' || text[position] == 'E')) { position++; if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++; RequireDigits(); }
                return JsonValue.Number(text.Substring(start, position - start));
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
