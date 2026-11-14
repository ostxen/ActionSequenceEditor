namespace NBC.ActionEditor
{
    // 将类型标识转换为可读的默认菜单名称。
    public static class StringExtensions
    {
        // 首字母大写，并分隔小写字母后的大写词边界。
        public static string SplitCamelCase(this string s) {
            if ( string.IsNullOrEmpty(s) ) return s;
            s = char.ToUpper(s[0]) + s.Substring(1);
            return System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z])([A-Z])", " $1").Trim();
        }
    }
}