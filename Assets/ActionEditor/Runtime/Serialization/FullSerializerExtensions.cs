using System;
using System.Globalization;
using System.IO;

namespace NBC.ActionEditor.Serialization
{
    // JSON number transport only, not a second serializer or document format.
    public static class DocumentNumber
    {
        public static string Single(float value, string path)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException(path + ": non-finite Single.");
            return value == 0 && BitConverter.ToInt32(BitConverter.GetBytes(value), 0) < 0
                ? "-0.0" : ((double)value).ToString("R", CultureInfo.InvariantCulture);
        }
        public static float Single(string text, string path)
        {
            try
            {
                var value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException(path + ": non-finite Single.");
                return value == 0 && text[0] == '-' ? BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0) : value;
            }
            catch (Exception error) when (error is FormatException || error is OverflowException)
            { throw new InvalidDataException(path + ": numeric overflow or invalid Single."); }
        }
    }
}