using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Globalization;

namespace RTTUnitEditor.Domain
{
    // Raw text lives in the draft. Validation never replaces the user's input.
    public sealed class BodyDraft
    {
        public string Id;
        public string Name;
        public string UnitType;
        public bool TestOnly;
        public string Baseline;
        public string SourceNote;
        public string CopiedFromId;
        public Dictionary<string, string> Fields = new Dictionary<string, string>();
        public List<PlatformMount> Installations; // null = platform structure not supplied
        public List<string> Tags = new List<string>();

        public static readonly string[] FieldKeys = {
            "memberCount", "maximumHealth", "roadSpeed", "offroadPenalty", "turnSpeed",
            "kinetic.front", "kinetic.side", "kinetic.rear", "kinetic.top",
            "chemical.front", "chemical.side", "chemical.rear", "chemical.top",
            "faction", "specialization", "weight", "maxPassengers", "totalLoad"
        };
        public static readonly string[] LegacyFieldKeys = FieldKeys.Take(13).ToArray();
        public static readonly string[] LegalTags = { "sprint", "smoke_1", "smoke_4", "laser", "mobile_supply", "aps" };

        public static BodyDraft Create(string type, string name)
        {
            var draft = new BodyDraft {
                Id = Guid.NewGuid().ToString("D"), Name = name, UnitType = type,
                TestOnly = true, Baseline = BodyRules.Baseline, SourceNote = "工具新建，仅供测试"
            };
            foreach (string key in FieldKeys) draft.Fields[key] = null;
            if (type == "ground_vehicle") draft.Fields["memberCount"] = "1";
            else if (type == "infantry") {
                draft.Fields["offroadPenalty"] = "0";
                draft.Fields["turnSpeed"] = "360";
            }
            return draft;
        }

        public BodyDraft Clone()
        {
            return new BodyDraft {
                Id = Id, Name = Name, UnitType = UnitType, TestOnly = TestOnly,
                Baseline = Baseline, SourceNote = SourceNote, CopiedFromId = CopiedFromId,
                Fields = new Dictionary<string, string>(Fields), Tags = new List<string>(Tags), Installations=Installations==null?null:Installations.Select(x=>x.Clone()).ToList()
            };
        }

        public BodyDraft Copy(string name)
        {
            BodyDraft copy = Clone();
            copy.Id = Guid.NewGuid().ToString("D");
            copy.Name = name;
            copy.TestOnly = true;
            copy.CopiedFromId = Id;
            if(copy.Installations!=null)foreach(var mount in copy.Installations)mount.Id=Guid.NewGuid().ToString("D");
            copy.SourceNote = "复制自 " + Name + "；" + SourceNote;
            return copy;
        }

        public void UpdateInfantryHealth()
        {
            if (UnitType != "infantry") return;
            BigInteger count;
            Fields["maximumHealth"] = BodyRules.TryCount(Fields["memberCount"], out count)
                ? (count * 5).ToString(CultureInfo.InvariantCulture) : null;
        }

        public string EffectiveRoadSpeed() { return Fields["roadSpeed"] ?? (UnitType=="infantry" ? "5" : null); }
        public BodyDraft WithType(string type)
        {
            if(type!=null&&type!="infantry"&&type!="ground_vehicle")throw new ArgumentException("未知单位类型");
            var next=Clone();if(next.UnitType==type)return next;next.UnitType=type;
            next.Fields["memberCount"]=type=="ground_vehicle"?"1":null;next.Fields["maximumHealth"]=null;
            next.Fields["offroadPenalty"]=type=="infantry"?"0":null;next.Fields["turnSpeed"]=type=="infantry"?"360":null;
            if(type!="ground_vehicle")next.Installations=null;
            if(type=="ground_vehicle")next.Tags.Remove("sprint");
            if(type=="infantry"){
                foreach(var key in new[]{"weight","maxPassengers","totalLoad"})next.Fields[key]=null;
                foreach(var energy in new[]{"kinetic","chemical"}){
                    string front=next.Fields[energy+".front"];
                    if(!new[]{null,"6","8","10"}.Contains(front)||new[]{"side","rear","top"}.Any(face=>next.Fields[energy+"."+face]!=front))
                        foreach(var face in new[]{"front","side","rear","top"})next.Fields[energy+"."+face]=null;
                }
            }
            return next;
        }
        public string OffroadSpeed()
        {
            decimal speed, penalty;
            if (!BodyRules.TryNumber(EffectiveRoadSpeed(), out speed) ||
                !BodyRules.TryNumber(Fields["offroadPenalty"], out penalty)) return null;
            try { return (speed * (1 - penalty)).ToString(CultureInfo.InvariantCulture); }
            catch (OverflowException) { return null; }
        }
    }
}
