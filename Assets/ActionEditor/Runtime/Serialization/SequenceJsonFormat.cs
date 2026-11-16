using NBC.ActionEditor.Model;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NBC.ActionEditor.Serialization
{
    // 只接受完整 v2 封套，预检通过后才建立候选作者图。
    public sealed class SequenceJsonFormat : ISequenceFormat
    {
        public string Id => "actioneditor.json";
        public string DisplayName => "JSON (.json)";
        public string Extension => ".json";
        public bool CanRead => true;
        public bool CanWrite => true;
        public bool CanExport => true;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public byte[] Serialize(ActionSequence sequence, SequenceFormatOptions options, SequenceFormatContext context)
        {
            if (sequence == null) throw new InvalidDataException("$.document: missing document.");
            var tree = JsonValueConverter.Capture(sequence, typeof(ActionSequence), context);
            SequenceGraphValidation.RequireValid(sequence);
            var envelope = JsonValueConverter.Map(("format", JsonValue.String(Id)),
                ("version", JsonValue.Number("2")), ("document", tree));
            return Utf8.GetBytes(JsonText.Print(envelope, options.Compact));
        }
        public ActionSequence Deserialize(byte[] bytes, SequenceFormatContext context)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidDataException("$: empty document.");
            JsonValue envelope;
            try { envelope = JsonText.Parse(Utf8.GetString(bytes)); }
            catch (DecoderFallbackException) { throw new InvalidDataException("$: invalid UTF-8 document."); }
            if (envelope.Kind != JsonValue.ValueKind.Object
                || !envelope.Members.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "document", "format", "version" })
                || envelope.Members["format"].Kind != JsonValue.ValueKind.String || envelope.Members["format"].Scalar != Id
                || envelope.Members["version"].Kind != JsonValue.ValueKind.Number || envelope.Members["version"].Scalar != "2")
                throw new InvalidDataException("$: unsupported format, version or envelope members.");
            var candidate = JsonValueConverter.Restore(envelope.Members["document"], typeof(ActionSequence), context) as ActionSequence;
            SequenceGraphValidation.Initialize(candidate, context);
            return candidate;
        }
    }
}
