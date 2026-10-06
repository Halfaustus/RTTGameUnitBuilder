using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace RTTUnitEditor.Domain
{
    public static class BodyRules
    {
        public const string Baseline = "DB-2026-10-05-33";
        public const string Unchecked = "本体页仅检查自身字段；武器配装见分配页，人员与运输资格见单位配置页；局内能力参数未检查";
        private static readonly Regex NumberSyntax = new Regex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?$", RegexOptions.CultureInvariant);

        public static bool TryCount(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            return text != null && Regex.IsMatch(text, @"^[1-9][0-9]*$") &&
                BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        // Parse exact decimal input. Reject unsupported precision rather than let TryParse round it.
        public static bool TryNumber(string text, out decimal value)
        {
            value = 0;
            if (text == null || !NumberSyntax.IsMatch(text) ||
                !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out value)) return false;
            return NormalizedDecimal(text) == NormalizedDecimal(value.ToString(CultureInfo.InvariantCulture));
        }
        private static string NormalizedDecimal(string text)
        {
            bool negative = text.StartsWith("-");
            string unsigned = negative ? text.Substring(1) : text;
            string[] parts = unsigned.Split('.');
            string fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
            string result = parts[0] + (fraction.Length == 0 ? "" : "." + fraction);
            return negative && result != "0" ? "-" + result : result;
        }

        public static Dictionary<string, string> Validate(BodyDraft draft, bool legacy=false)
        {
            var errors = new Dictionary<string, string>();
            Guid id;
            if (!Guid.TryParseExact(draft.Id, "D", out id) || id == Guid.Empty) errors["id"] = "技术ID须为非空GUID。";
            if (string.IsNullOrWhiteSpace(draft.Name)) errors["name"] = "名称不能为空。";
            if (draft.UnitType != null && draft.UnitType != "infantry" && draft.UnitType != "ground_vehicle") errors["unitType"] = "仅支持班组与地面车辆。";
            if (string.IsNullOrWhiteSpace(draft.Baseline)) errors["source"] = "须保留来源基线。";
            if (draft.SourceNote == null) errors["source"] = "须保留来源记录。";
            if (draft.CopiedFromId != null && !Guid.TryParseExact(draft.CopiedFromId, "D", out id)) errors["source"] = "复制来源ID格式错误。";
            foreach (string key in draft.Fields.Keys)
                if (!BodyDraft.FieldKeys.Contains(key)) errors[key] = "未知本体字段，不能扩展游戏语义。";
            foreach (string key in BodyDraft.FieldKeys)
                if (!draft.Fields.ContainsKey(key)) errors[key] = "缺少字段；未配置须明确写为null。";
            if (errors.Values.Any(v => v.StartsWith("缺少"))) return errors;
            bool infantry = draft.UnitType == "infantry";
            BigInteger count;
            if (infantry) {
                if (draft.Fields["memberCount"] != null && !TryCount(draft.Fields["memberCount"], out count))
                    errors["memberCount"] = "满编人数须为正整数，或留空未配置。";
                string expected = TryCount(draft.Fields["memberCount"], out count)
                    ? (count * 5).ToString(CultureInfo.InvariantCulture) : null;
                if (draft.Fields["maximumHealth"] != expected)
                    errors["maximumHealth"] = "班组最大生命由人数×5派生，不可独立修改。";
                if (draft.Fields["turnSpeed"] != "360") errors["turnSpeed"] = "步兵转向固定360°/s。";
                if (draft.Fields["offroadPenalty"] != "0") errors["offroadPenalty"] = "步兵越野惩罚固定0%。";
            } else if (draft.UnitType == "ground_vehicle") {
                if (draft.Fields["memberCount"] != "1") errors["memberCount"] = "单体车辆人数固定1，不是载员或车组人数。";
                decimal health;
                if (draft.Fields["maximumHealth"] != null &&
                    (!TryNumber(draft.Fields["maximumHealth"], out health) || health < 10 || health > 20))
                    errors["maximumHealth"] = "车辆本体生命须为10～20，或留空未配置。";
                if (draft.Tags.Contains("sprint")) errors["tags"] = "地面车辆不具步兵冲刺资格。";
                if (draft.Fields["offroadPenalty"] != null &&
                    draft.Fields["offroadPenalty"] != "0.2" && draft.Fields["offroadPenalty"] != "0.5")
                    errors["offroadPenalty"] = "当前已确认越野惩罚为20%或50%；其他规则未定义。";
                CheckNumber(draft, errors, "turnSpeed", 0);
            }
            CheckNumber(draft, errors, "roadSpeed", 0);
            foreach(var key in new[]{"faction","specialization"}) if(draft.Fields[key]!=null && string.IsNullOrWhiteSpace(draft.Fields[key])) errors[key]="身份值不能仅含空格。";
            CheckNumber(draft, errors, "weight", 0); CheckNumber(draft, errors, "totalLoad", 0);
            decimal passengers;
            if(draft.Fields["maxPassengers"]!=null && (!TryNumber(draft.Fields["maxPassengers"],out passengers)||passengers<0||decimal.Truncate(passengers)!=passengers)) errors["maxPassengers"]="最大乘员须为非负整数。";
            if(infantry && new[]{"weight","maxPassengers","totalLoad"}.Any(k=>draft.Fields[k]!=null)) errors["weight"]="步兵运输重量由满编人数与特殊装备派生；载具容量字段不适用。";
            foreach (string energy in new[] { "kinetic", "chemical" }) {
                foreach (string face in new[] { "front", "side", "rear", "top" })
                    CheckNumber(draft, errors, energy + "." + face, 5);
                if(infantry&&!legacy&&draft.Fields[energy+".front"]!=null&&!new[]{"6","8","10"}.Contains(draft.Fields[energy+".front"]))errors[energy+".front"]="步兵防护只能选择6、8或10，或留空未配置。";
                if (infantry && new[] { "side", "rear", "top" }.Any(face =>
                    draft.Fields[energy + "." + face] != draft.Fields[energy + ".front"]))
                    errors[energy + ".front"] = "步兵同类防护四面须相同（含未配置状态）。";
            }
            if (draft.Tags.Any(tag => !BodyDraft.LegalTags.Contains(tag)) || draft.Tags.Distinct().Count() != draft.Tags.Count)
                errors["tags"] = "未知或重复能力标签。";
            if (draft.Tags.Contains("smoke_1") && draft.Tags.Contains("smoke_4"))
                errors["tags"] = "烟雾×1与烟雾×4互斥。";
            foreach(var issue in RelationRules.ValidatePlatform(draft).Errors)errors["installations:"+errors.Count]=issue;
            return errors;
        }
        private static void CheckNumber(BodyDraft draft, Dictionary<string, string> errors, string key, decimal min)
        {
            decimal value;
            if (draft.Fields[key] != null && (!TryNumber(draft.Fields[key], out value) || value < min))
                errors[key] = "须为精确十进制数且≥" + min + "，或留空未配置；不接受取整、舍入或非有限值。";
        }
    }
}
