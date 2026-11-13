using NBC.ActionEditor.Domain;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NBC.ActionEditor.Serialization
{
    // 只接受完整 v2 封套，预检通过后才建立候选作者图。
    public sealed class ActionDocumentJsonFormat : IActionDocumentFormat
    {
        public string Id => "actioneditor.json";
        public string DisplayName => "JSON (.json)";
        public string Extension => ".json";
        public bool CanRead => true;
        public bool CanWrite => true;
        public bool CanExport => true;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public byte[] Serialize(ActionDocument document, DocumentFormatOptions options, DocumentFormatContext context)
        {
            if (document == null) throw new InvalidDataException("$.document: missing document.");
            var tree = DocumentValueConverter.Capture(document, typeof(ActionDocument), context);
            ActionDocumentGraph.RequireValid(document);
            var envelope = DocumentValueConverter.Map(("format", DocumentValue.String(Id)),
                ("version", DocumentValue.Number("2")), ("document", tree));
            return Utf8.GetBytes(DocumentJson.Print(envelope, options.Compressed));
        }
        public ActionDocument Deserialize(byte[] bytes, DocumentFormatContext context)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidDataException("$: empty document.");
            DocumentValue envelope;
            try { envelope = DocumentJson.Parse(Utf8.GetString(bytes)); }
            catch (DecoderFallbackException) { throw new InvalidDataException("$: invalid UTF-8 document."); }
            if (envelope.Kind != DocumentValue.ValueKind.Object
                || !envelope.Members.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "document", "format", "version" })
                || envelope.Members["format"].Kind != DocumentValue.ValueKind.String || envelope.Members["format"].Scalar != Id
                || envelope.Members["version"].Kind != DocumentValue.ValueKind.Number || envelope.Members["version"].Scalar != "2")
                throw new InvalidDataException("$: unsupported format, version or envelope members.");
            var candidate = DocumentValueConverter.Restore(envelope.Members["document"], typeof(ActionDocument), context) as ActionDocument;
            ActionDocumentGraph.Initialize(candidate, context);
            return candidate;
        }
    }
}
