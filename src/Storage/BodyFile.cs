using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using RTTUnitEditor.Domain;

namespace RTTUnitEditor.Storage
{
    public static class BodyFile
    {
        public const string Format = "rtt-unit-editor";
        public const int Version = 1;
        public static string Serialize(IEnumerable<BodyDraft> bodies, bool legacyRules=false)
        {
            var list = bodies.ToList();
            ValidateAll(list,legacyRules);
            var root = new Dictionary<string, object> {
                { "format", Format }, { "version", Version }, { "kind", "bodies" },
                { "bodies", list.Select(ToRecord).ToArray() }
            };
            return new JavaScriptSerializer().Serialize(root);
        }
        private static object ToRecord(BodyDraft body)
        {
            return new Dictionary<string, object> {
                { "id", body.Id }, { "name", body.Name }, { "unitType", body.UnitType },
                { "testOnly", body.TestOnly },
                { "source", new Dictionary<string, object> {
                    { "baseline", body.Baseline }, { "note", body.SourceNote }, { "copiedFromId", body.CopiedFromId } } },
                { "fields", body.Fields }, { "tags", body.Tags }
            };
        }
        public static List<BodyDraft> Parse(string json, bool legacyRules=false)
        {
            var root = Map(StrictJson.Parse(json), "根对象");
            Keys(root, new[] { "format", "version", "kind", "bodies" }, "根对象");
            if (Text(root["format"], "format") != Format) throw new FormatException("不支持的文件格式。");
            JsonNumber version = root["version"] as JsonNumber;
            if (version == null || version.Text != "1") throw new FormatException("不支持的格式版本；仅支持1。");
            if (Text(root["kind"], "kind") != "bodies") throw new FormatException("本轮仅支持本体定义。");
            var entries = root["bodies"] as List<object>;
            if (entries == null) throw new FormatException("bodies须为数组。");
            var result = new List<BodyDraft>();
            foreach (object value in entries) {
                var entry = Map(value, "本体");
                Keys(entry, new[] { "id", "name", "unitType", "testOnly", "source", "fields", "tags" }, "本体");
                var source = Map(entry["source"], "source");
                Keys(source, new[] { "baseline", "note", "copiedFromId" }, "source");
                var fields = Map(entry["fields"], "fields");
                bool legacy = fields.Count == BodyDraft.LegacyFieldKeys.Length;
                Keys(fields, legacy ? BodyDraft.LegacyFieldKeys : BodyDraft.FieldKeys, "fields");
                if (legacy) foreach(var key in BodyDraft.FieldKeys.Except(BodyDraft.LegacyFieldKeys)) fields[key] = null;
                if (!(entry["testOnly"] is bool)) throw new FormatException("testOnly须为布尔值。");
                var tags = entry["tags"] as List<object>;
                if (tags == null) throw new FormatException("tags须为数组。");
                var draft = new BodyDraft {
                    Id = Text(entry["id"], "id"), Name = Text(entry["name"], "name"),
                    UnitType = NullableText(entry["unitType"], "unitType"), TestOnly = (bool)entry["testOnly"],
                    Baseline = Text(source["baseline"], "source.baseline"), SourceNote = Text(source["note"], "source.note"),
                    CopiedFromId = NullableText(source["copiedFromId"], "source.copiedFromId"),
                    Fields = fields.ToDictionary(pair => pair.Key, pair => NullableText(pair.Value, "fields." + pair.Key)),
                    Tags = tags.Select(tag => Text(tag, "tag")).ToList()
                };
                result.Add(draft);
            }
            ValidateAll(result,legacyRules);
            return result;
        }
        public static List<BodyDraft> Read(string path)
        {
            // Tool technical size guard, not a game unit or field range.
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException("本轮文件读取上限为4MiB。");
            return Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
        }
        public static void Save(string path, IEnumerable<BodyDraft> bodies)
        {
            WriteJson(path, Serialize(bodies)); // Validate before touching destination.
        }
        public static void WriteJson(string path, string json)
        {
            if (Encoding.UTF8.GetByteCount(json) > 4 * 1024 * 1024) throw new IOException("用户文件写入上限4MiB；原文件保留。");
            string target = Path.GetFullPath(path);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        public static void ValidateAll(IList<BodyDraft> list, bool legacyRules=false)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var body in list) {
                var errors = BodyRules.Validate(body,legacyRules);
                if (errors.Count > 0) throw new FormatException(body.Name + "：" +
                    string.Join("；", errors.Select(pair => pair.Key + "：" + pair.Value)));
                if (!ids.Add(body.Id)) throw new FormatException("重复ID：" + body.Id);
                if (!names.Add(body.Name)) throw new FormatException("同名对象冲突：" + body.Name);
            }
        }
        private static Dictionary<string, object> Map(object value, string label)
        {
            var map = value as Dictionary<string, object>;
            if (map == null) throw new FormatException(label + "须为对象。");
            return map;
        }
        private static void Keys(Dictionary<string, object> map, IEnumerable<string> expected, string label)
        {
            var allowed = new HashSet<string>(expected);
            foreach (string key in map.Keys) if (!allowed.Contains(key)) throw new FormatException(label + "包含未知字段：" + key);
            foreach (string key in allowed) if (!map.ContainsKey(key)) throw new FormatException(label + "缺少字段：" + key);
        }
        private static string Text(object value, string label)
        {
            string text = value as string;
            if (text == null) throw new FormatException(label + "须为字符串。");
            return text;
        }
        private static string NullableText(object value, string label) { return value == null ? null : Text(value, label); }
    }
}
