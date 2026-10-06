using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
namespace RTTUnitEditor.Domain {
 public sealed class CombatDraft {
  public string Id, Name, Kind, CopiedFromId;
  public bool TestOnly = true;
  public string Baseline = CombatRules.Baseline;
  public Dictionary<string,string> Fields = new Dictionary<string,string>();
  public List<string> Tags = new List<string>(), AmmoIds = new List<string>(), TargetTypes = new List<string>();
  public static readonly string[] WeaponKeys = { "caliber","faction","specialization","weight","operators","minRange","maxRange","spread","movePenalty","aimMin","aimMax","rpm","actualInterval","consumption","capacity","reloadRule","reload","ignoreReduction","slotRule","specialEquipmentWeight" };
  public static readonly string[] LegacyWeaponKeys = WeaponKeys.Take(18).ToArray();
  public static readonly string[] AmmoKeys = { "caliber","faction","specialization","specialNote","damageType","category","damage","penetration","anchorRange","anchorPenetration","speed","blast","suppression","moduleDamage","supplyCost","missingCost" };
  public static readonly string[] WeaponTags = { "indoor_fire","move_fire","silenced","mechanical_loading","top_attack" };
  public static readonly string[] Targets = { "infantry","ground_vehicle","helicopter","fixed_wing","fortification" };
  public static CombatDraft Create(string kind,string name) {
   var d=new CombatDraft {Id=Guid.NewGuid().ToString("D"),Name=name,Kind=kind};
   foreach(var k in kind=="weapon"?WeaponKeys:AmmoKeys) d.Fields[k]=null; return d;
  }
  public CombatDraft Clone() { return new CombatDraft { Id=Id,Name=Name,Kind=Kind,CopiedFromId=CopiedFromId,TestOnly=TestOnly,Baseline=Baseline,Fields=new Dictionary<string,string>(Fields),Tags=new List<string>(Tags),AmmoIds=new List<string>(AmmoIds),TargetTypes=new List<string>(TargetTypes) }; }
  public CombatDraft Copy(string name) {var c=Clone(); c.Id=Guid.NewGuid().ToString("D"); c.Name=name; c.CopiedFromId=Id; c.TestOnly=true; return c;}
  public string DerivedInterval() {
   decimal r,c; if(!BodyRules.TryNumber(Fields["consumption"],out c))return "未配置";
   if(!BodyRules.TryNumber(Fields["rpm"],out r)||r<=0) {
    decimal interval; if(!BodyRules.TryNumber(Fields["actualInterval"],out interval))return "未配置";
    return Fields["consumption"]+" × "+Fields["actualInterval"]+" 秒（精确关系；单武器）";
   }
   return Fields["consumption"]+" × 60 / "+Fields["rpm"]+" 秒（精确关系；单武器）";
  }
  public string DerivedK() {
   if(Fields["damageType"]==null)return "未配置：伤害类型尚未选择";
   if(Fields["damageType"]!="kinetic") return "不适用：化学破深不随距离衰减";
   decimal p,r,pr; if(!BodyRules.TryNumber(Fields["penetration"],out p)||!BodyRules.TryNumber(Fields["anchorRange"],out r)||r<=0||!BodyRules.TryNumber(Fields["anchorPenetration"],out pr)) return "未配置";
   return ((double)(p-pr)/Math.Log(1+(double)r/100)).ToString("G10",CultureInfo.InvariantCulture)+"（显示近似；按锚点派生）";
  }
 }
 public static class CombatRules {
  public const string Baseline="DB-2026-10-05-33";
  public const string Unchecked="武器与弹药独立编辑；可用弹种在武器页关联，配置库存与择弹顺序在分配页填写，人员与安装适用性在配置页检查。阵营与专精仅记录归属，不限制选择；多弹种独立属性作用范围待定。仅供测试。";
  public static Dictionary<string,string> Validate(CombatDraft d) {
   var e=new Dictionary<string,string>(); Guid id;
   if(!Guid.TryParseExact(d.Id,"D",out id)||id==Guid.Empty)e["id"]="须为非空GUID";
   if(string.IsNullOrWhiteSpace(d.Name))e["name"]="名称必填";
   if(!d.TestOnly)e["testOnly"]="用户武器／弹药须保留仅供测试身份";
   if(string.IsNullOrWhiteSpace(d.Baseline))e["baseline"]="来源基线必填";
   if(d.CopiedFromId!=null&&!Guid.TryParseExact(d.CopiedFromId,"D",out id))e["copiedFromId"]="复制来源ID无效";
   if(d.Kind!="weapon"&&d.Kind!="ammo"){e["kind"]="未知对象种类";return e;}
   var keys=d.Kind=="weapon"?CombatDraft.WeaponKeys:CombatDraft.AmmoKeys;
   foreach(var k in d.Fields.Keys)if(!keys.Contains(k))e[k]="未知字段";
   foreach(var k in keys)if(!d.Fields.ContainsKey(k))e[k]="缺少字段；未配置用null";
   if(keys.Any(k=>!d.Fields.ContainsKey(k)))return e;
   foreach(var k in new[]{"caliber","faction","specialization","specialNote"})if(d.Fields.ContainsKey(k)&&d.Fields[k]!=null&&string.IsNullOrWhiteSpace(d.Fields[k]))e[k]="留空须为未配置，不能仅含空格";
   var nums=d.Kind=="weapon"?new[]{"weight","operators","minRange","maxRange","spread","movePenalty","aimMin","aimMax","rpm","actualInterval","capacity","reload","ignoreReduction","specialEquipmentWeight"}:new[]{"damage","penetration","anchorRange","anchorPenetration","speed","blast","suppression","moduleDamage","supplyCost","missingCost"};
   foreach(var k in nums){decimal v;if(d.Fields[k]==null)continue; decimal min=k=="penetration"||k=="anchorPenetration"?5:k=="movePenalty"?1:0;
    if(!BodyRules.TryNumber(d.Fields[k],out v)||v<min||((k=="rpm"||k=="actualInterval"||k=="speed"||k=="anchorRange"||k=="operators"||k=="capacity")&&v<=0)||((k=="operators"||k=="capacity")&&decimal.Truncate(v)!=v)||(k=="ignoreReduction"&&v>100))e[k]="非法精确十进制数；"+(min==5?"最低5":min==1?"惩罚倍率最低1":"非负")+((k=="operators"||k=="capacity")?"，须为正整数":"")+(k=="ignoreReduction"?"，范围0～100%":"");
   }
   if(d.Kind=="weapon") {
    Choice(d,e,"slotRule",new[]{"primary","secondary","both","operators_primary","emplaced_artillery","vehicle_slot","cannon_main","launcher_secondary"});
    if(d.Fields["slotRule"]=="emplaced_artillery"&&d.Fields["specialEquipmentWeight"]!="0.1")e["specialEquipmentWeight"]="架设火炮额外特殊装备固定0.1t";
    if(d.Fields["slotRule"]=="launcher_secondary"&&d.Fields["rpm"]!=null)e["rpm"]="一次性火箭弹沿用准备时间／配置数量，不新增rpm";
    Choice(d,e,"consumption",new[]{"1","3"}); Choice(d,e,"reloadRule",new[]{"continuous","per_round"});
    if(d.Fields["rpm"]!=null && d.Fields["actualInterval"]!=null)e["actualInterval"]="有rpm时实际间隔由60/rpm派生，不可另填"; Range(d,e,"minRange","maxRange");Range(d,e,"aimMin","aimMax");
    if(d.Tags.Any(t=>!CombatDraft.WeaponTags.Contains(t))||d.Tags.Distinct().Count()!=d.Tags.Count)e["tags"]="未知或重复武器标签";
    if(d.TargetTypes.Any(t=>!CombatDraft.Targets.Contains(t))||d.TargetTypes.Distinct().Count()!=d.TargetTypes.Count)e["targets"]="未知或重复目标类型";
    if(d.AmmoIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=d.AmmoIds.Count)e["ammoIds"]="弹药引用重复";
    decimal cap,c;if(BodyRules.TryNumber(d.Fields["capacity"],out cap)&&BodyRules.TryNumber(d.Fields["consumption"],out c)&&cap<c)e["capacity"]="容量不足一次抽象消耗";
    if(!d.Tags.Contains("move_fire")&&d.Fields["movePenalty"]!=null)e["movePenalty"]="未启用移动射击，此项不适用";
    if(d.Fields["reloadRule"]=="continuous"&&d.Fields["reload"]!=(d.Tags.Contains("mechanical_loading")?"4":"3"))e["reload"]="连射换弹固定人工3秒／机械4秒";
   } else {
    Choice(d,e,"damageType",new[]{"kinetic","chemical"});Choice(d,e,"category",new[]{"AP","HE","HEAT"});
    if(d.Tags.Count!=0||d.TargetTypes.Count!=0||d.AmmoIds.Count!=0)e["structure"]="弹药不持有武器标签、目标列表或引用";
    if(d.Fields["damageType"]!="kinetic"&&(d.Fields["anchorRange"]!=null||d.Fields["anchorPenetration"]!=null))e["anchorRange"]="仅动能适用距离锚点";
    if(d.Fields["category"]=="AP"&&d.Fields["damageType"]!="kinetic")e["damageType"]="AP使用动能语义";
    if((d.Fields["category"]=="HE"||d.Fields["category"]=="HEAT")&&d.Fields["damageType"]!="chemical")e["damageType"]="HE／HEAT使用化学能语义";
    if(d.Fields["category"]=="HE"&&d.Fields["moduleDamage"]!="0")e["moduleDamage"]="高爆反人员弹模块破坏固定0";
    if(d.Fields["category"]=="HEAT"&&(d.Fields["blast"]!="0"||d.Fields["suppression"]!="0"))e["blast"]="反甲弹无范围伤害和步兵压制（固定0）";
    Range(d,e,"anchorPenetration","penetration");
   } return e;
  }
  static void Choice(CombatDraft d,Dictionary<string,string> e,string k,string[] choices){if(d.Fields[k]!=null&&!choices.Contains(d.Fields[k]))e[k]="未知语义取值";}
  static void Range(CombatDraft d,Dictionary<string,string> e,string lo,string hi){decimal a,b;if(BodyRules.TryNumber(d.Fields[lo],out a)&&BodyRules.TryNumber(d.Fields[hi],out b)&&a>b)e[hi]="不得小于"+lo;}
  public static List<string> ValidateAll(IList<CombatDraft> weapons,IList<CombatDraft> ammo,bool legacy=false) {
   var errors=new List<string>();var all=weapons.Concat(ammo).ToList();
   foreach(var d in all)foreach(var e in Validate(d))errors.Add(d.Name+" ["+d.Id+"] ["+e.Key+"]："+e.Value);
   foreach(var g in all.GroupBy(d=>d.Id,StringComparer.OrdinalIgnoreCase))if(g.Count()>1)errors.Add("重复ID："+g.Key);
   foreach(var w in weapons){if(w.Kind!="weapon")errors.Add(w.Name+"：武器列表类型错误");foreach(var aid in w.AmmoIds){var a=ammo.FirstOrDefault(d=>string.Equals(d.Id,aid,StringComparison.OrdinalIgnoreCase));if(a==null){errors.Add(w.Name+" ["+w.Id+"] [ammoIds]：引用不存在 "+aid);continue;}
    foreach(var k in new[]{"caliber"})if(w.Fields.ContainsKey(k)&&a.Fields.ContainsKey(k)&&w.Fields[k]!=null&&a.Fields[k]!=null&&w.Fields[k]!=a.Fields[k])errors.Add(w.Name+" ["+w.Id+"] ["+k+"]：与弹药「"+a.Name+"」不一致");
   }}
   foreach(var a in ammo)if(a.Kind!="ammo")errors.Add(a.Name+"：弹药列表类型错误");
   var defs=ammo.Where(a=>new[]{"faction","specialization","caliber","category","specialNote"}.All(k=>a.Fields.ContainsKey(k))&&a.Fields["specialNote"]==null&&new[]{"faction","specialization","caliber","category"}.All(k=>a.Fields[k]!=null));
   if(legacy)foreach(var g in defs.GroupBy(a=>string.Join("\u001f",new[]{a.Fields["faction"],a.Fields["specialization"],a.Fields["caliber"],a.Fields["category"]})))if(g.Count()>1)errors.Add("默认共享定义重复；请复用引用，独立特殊弹药须声明："+string.Join("、",g.Select(a=>a.Name+" ["+a.Id+"]")));
   return errors;
  }
 }
}
