using System;
using System.Globalization;
using System.IO;

namespace NBC.ActionEditor.Serialization
{
    // 保留有限 Single 的原始位和负零，不参与文档结构转换。
    public static class JsonFloatEncoding
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