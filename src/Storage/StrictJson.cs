using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RTTUnitEditor.Storage
{
    // Small strict reader: duplicate keys and trailing content must not silently disappear.
    internal sealed class JsonNumber
    {
        public string Text;
        public JsonNumber(string text) { Text = text; }
    }
    internal sealed class StrictJson
    {
        private readonly string text;
        private int position;
        private StrictJson(string source) { text = source; }
        public static object Parse(string source)
        {
            var reader = new StrictJson(source);
            object result = reader.Value(0);
            reader.Space();
            if (reader.position != source.Length) throw reader.Error("存在尾随内容");
            return result;
        }
        private FormatException Error(string message) { return new FormatException("JSON位置 " + position + "：" + message); }
        private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Take(char c) { Space(); if (position < text.Length && text[position] == c) { position++; return true; } return false; }
        private void Need(char c) { if (!Take(c)) throw Error("需要 " + c); }
        private object Value(int depth)
        {
            if (depth > 32) throw Error("嵌套过深");
            Space();
            if (position >= text.Length) throw Error("缺少值");
            char c = text[position];
            if (c == '{') {
                position++;
                var map = new Dictionary<string, object>();
                if (Take('}')) return map;
                do {
                    Space();
                    if (position >= text.Length || text[position] != '"') throw Error("对象键须为字符串");
                    string key = String();
                    if (map.ContainsKey(key)) throw Error("重复字段 " + key);
                    Need(':');
                    map.Add(key, Value(depth + 1));
                    if (Take('}')) return map;
                } while (Take(','));
                throw Error("对象未结束");
            }
            if (c == '[') {
                position++;
                var array = new List<object>();
                if (Take(']')) return array;
                do {
                    array.Add(Value(depth + 1));
                    if (Take(']')) return array;
                } while (Take(','));
                throw Error("数组未结束");
            }
            if (c == '"') return String();
            foreach (string token in new[] { "true", "false", "null" }) {
                if (text.Substring(position).StartsWith(token, StringComparison.Ordinal)) {
                    position += token.Length;
                    return token == "null" ? null : (object)(token == "true");
                }
            }
            var match = Regex.Match(text.Substring(position), @"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?");
            if (!match.Success) throw Error("非法值");
            position += match.Length;
            return new JsonNumber(match.Value);
        }
        private string String()
        {
            Need('"');
            var result = new StringBuilder();
            while (position < text.Length) {
                char c = text[position++];
                if (c == '"') return result.ToString();
                if (c < 32) throw Error("字符串含未转义控制字符");
                if (c != '\\') { result.Append(c); continue; }
                if (position >= text.Length) throw Error("缺少转义字符");
                c = text[position++];
                switch (c) {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length) throw Error("Unicode转义不完整");
                        ushort value;
                        if (!ushort.TryParse(text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                            throw Error("Unicode转义错误");
                        result.Append((char)value);
                        position += 4;
                        break;
                    default: throw Error("未知转义");
                }
            }
            throw Error("字符串未结束");
        }
    }
}
