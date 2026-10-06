using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
namespace RTTUnitEditor.Domain {
 public static class ConfigurationRules {
  public static readonly string[] Categories={"recon","infantry","armor","support"};
  public static bool Same(string a,string b){return string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
  public static bool Count(string text,out decimal n,bool positive){return BodyRules.TryNumber(text,out n)&&n>=(positive?1:0)&&decimal.Truncate(n)==n;}
  static bool GuidValue(string id){Guid value;return Guid.TryParseExact(id,"D",out value)&&value!=Guid.Empty;}
  static void Identity(ValidationReport r,string id,string name,bool test,string baseline,string source,string copied){if(!GuidValue(id))r.Errors.Add("id：非空GUID必填");if(string.IsNullOrWhiteSpace(name))r.Errors.Add("name：名称必填");if(!test)r.Errors.Add("testOnly：必须保留仅供测试身份");if(string.IsNullOrWhiteSpace(baseline)||source==null)r.Errors.Add("source：来源必填");if(copied!=null&&!GuidValue(copied))r.Errors.Add("copiedFromId：无效GUID");}
  public static ValidationReport Validate(ConfigurationDraft c,IList<BodyDraft> bodies,IList<CombatDraft> weapons,IList<CombatDraft> ammo) {
   var r=new ValidationReport();Identity(r,c.Id,c.Name,c.TestOnly,c.Baseline,c.SourceNote,c.CopiedFromId);
   if(c.Category==null)r.Pending.Add("编组类别未配置");else if(!Categories.Contains(c.Category))r.Errors.Add("category：类别未知／航空未启用");
   var b=bodies.FirstOrDefault(x=>Same(x.Id,c.BodyId));
   if(c.BodyId==null){r.Pending.Add("本体未选择");if(c.Assignments.Count+c.Positions.Count!=0)r.Errors.Add("bodyId：有位置／挂载但没有本体");return r;}
   if(b==null){r.Errors.Add("bodyId：本体引用不存在");return r;}
   foreach(var e in BodyRules.Validate(b,true))r.Errors.Add("本体 "+b.Name+" ["+e.Key+"]："+e.Value);
   foreach(var k in BodyDraft.LegacyFieldKeys)if(b.Fields[k]==null)r.Pending.Add("本体 "+k+" 未配置");
   foreach(var k in new[]{"faction","specialization"})if(b.Fields[k]==null)r.Pending.Add("本体 "+k+" 未配置，共享身份未检查");
   bool infantry=b.UnitType=="infantry";decimal members;
   if(!Count(b.Fields["memberCount"],out members,true))members=0;
   if(c.Positions.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=c.Positions.Count)r.Errors.Add("positions：位置ID重复");
   foreach(var p in c.Positions){if(!GuidValue(p.Id))r.Errors.Add("positions.id：无效GUID");if(!new[]{"soldier","hull","cannon","commander"}.Contains(p.Kind))r.Errors.Add("positions.kind：未知安装位置");if(infantry&&p.Kind!="soldier"||!infantry&&p.Kind=="soldier")r.Errors.Add("positions：人员／载具安装不适用");}
   if(infantry){if(c.Positions.Count>members&&members>0)r.Errors.Add("positions：人员位置超过满编人数");if(c.Positions.Count!=members)r.Pending.Add("满编人员位置尚未完整建立");if(c.SupplyWeight!=null)r.Errors.Add("supplyWeight：本轮只表达运输载具已有补给重量，步兵不适用");}
   else {
    if(c.Positions.Count(p=>p.Kind=="commander")>1)r.Errors.Add("positions：默认结构只有一个车长位置，特殊例外未实现");
    if(c.Positions.Count(p=>p.Kind=="commander")!=1)r.Pending.Add("默认车长位置未完整配置（不推断特殊例外）");
    int hulls=c.Positions.Count(p=>p.Kind=="hull");if(b.Fields["offroadPenalty"]=="0.5"&&hulls>0)r.Errors.Add("positions：轮式默认无车体槽；特殊声明结构本轮未实现");if(hulls>1)r.Errors.Add("positions：当前只支持默认一个车体槽");
    if(b.Fields["offroadPenalty"]=="0.2"&&hulls==0)r.Pending.Add("履带默认车体槽未建立");
    decimal supply,load;if(c.SupplyWeight==null)r.Pending.Add("车载已有补给重量未配置（不能视为0）");else if(!BodyRules.TryNumber(c.SupplyWeight,out supply)||supply<0)r.Errors.Add("supplyWeight：须为非负精确吨位");else if(BodyRules.TryNumber(b.Fields["totalLoad"],out load)&&supply>load)r.Errors.Add("supplyWeight：已有补给超过总运输载重");
    foreach(var k in new[]{"weight","maxPassengers","totalLoad"})if(b.Fields[k]==null)r.Pending.Add("载具 "+k+" 未配置");
   }
   var occupied=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var a in c.Assignments){string prefix="挂载 ["+a.Id+"] ";if(!GuidValue(a.Id)||!ids.Add(a.Id))r.Errors.Add(prefix+"id：无效／重复");
    var w=weapons.FirstOrDefault(x=>Same(x.Id,a.WeaponId));if(w==null){r.Errors.Add(prefix+"weaponId：引用不存在");continue;}
    var p=c.Positions.FirstOrDefault(x=>Same(x.Id,a.PositionId));if(p==null){r.Errors.Add(prefix+"positionId：引用不存在");continue;}
    foreach(var e in CombatRules.Validate(w))r.Errors.Add(prefix+"武器 ["+e.Key+"]："+e.Value);
    foreach(var k in new[]{"maxRange","spread","aimMin","aimMax","consumption","ignoreReduction","slotRule","specialEquipmentWeight"})if(w.Fields[k]==null)r.Pending.Add(prefix+"武器 "+k+" 未配置");
    if(w.Fields["slotRule"]=="launcher_secondary"){if(w.Fields["reload"]==null)r.Pending.Add(prefix+"准备时间未配置");}else {if(w.Fields["rpm"]==null&&w.Fields["actualInterval"]==null)r.Pending.Add(prefix+"发射间隔未配置");foreach(var key in new[]{"capacity","reloadRule","reload"})if(w.Fields[key]==null)r.Pending.Add(prefix+key+" 未配置");} if(w.AmmoIds.Count==0||w.TargetTypes.Count==0)r.Pending.Add(prefix+"弹药引用／攻击目标未配置");
    foreach(var k in new[]{"faction","specialization"}){if(w.Fields[k]==null)r.Pending.Add(prefix+k+" 未配置");else if(b.Fields[k]!=null&&b.Fields[k]!=w.Fields[k])r.Errors.Add(prefix+k+"：与本体不兼容");}
    decimal quantity;if(!Count(a.Quantity,out quantity,true))r.Errors.Add(prefix+"quantity：须为正整数");
    decimal priority;if(a.Priority!=null&&!Count(a.Priority,out priority,false))r.Errors.Add(prefix+"priority：须为非负整数，优先级细化不由ID决定");
    r.Unchecked.Add(prefix+"接替优先级由系统处理；当前DB未提供完整排序算法，不要求手填且不伪造结果"); string rule=w.Fields["slotRule"];
    if(rule==null){r.Errors.Add(prefix+"武器槽位未配置，现有挂载无法确认合法");continue;}
    if(rule!="launcher_secondary"&&quantity!=1)r.Errors.Add(prefix+"quantity：每个已确认挂载位置只放一个武器；多把须分配到不同位置，不能视为齐射");
    if(rule=="launcher_secondary"&&w.Fields["consumption"]!="1")r.Errors.Add(prefix+"consumption：一次性火箭弹每次消耗一具");
    bool person=p.Kind=="soldier";var taken=new List<string>();
    if(person){
     if(new[]{"vehicle_slot","cannon_main"}.Contains(rule))r.Errors.Add(prefix+"slot：车载武器不能分配给人员");
     decimal operators;if(!Count(w.Fields["operators"],out operators,true)){r.Pending.Add(prefix+"最低操作人数未配置");operators=0;}
     if(a.OperatorIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=a.OperatorIds.Count||a.OperatorIds.Any(id=>!c.Positions.Any(x=>Same(x.Id,id)&&x.Kind=="soldier")))r.Errors.Add(prefix+"operatorIds：操作人员无效／重复");
     if(operators>0&&a.OperatorIds.Count<operators)r.Errors.Add(prefix+"operatorIds：不足最低操作人数");
     if(!a.OperatorIds.Any(id=>Same(id,p.Id)))r.Errors.Add(prefix+"operatorIds：目标人员须在操作组内");
     if(rule=="operators_primary"||rule=="emplaced_artillery"){
      if(a.Slot!="primary")r.Errors.Add(prefix+"slot：多人武器占操作人员主槽");foreach(var id in a.OperatorIds)taken.Add(id+"/primary");
      if(a.OperatorIds.Count!=operators&&operators>0)r.Errors.Add(prefix+"operatorIds：已确认模式占最低操作人数个主槽；额外岗位未定义");
      if(rule=="emplaced_artillery"&&(b.Tags.Contains("sprint")||c.Category!="support"))r.Errors.Add(prefix+"资格：架设火炮禁冲刺且固定归支援类别");
     }else {
      if(a.OperatorIds.Count!=1||operators>1)r.Errors.Add(prefix+"operatorIds：单人模式不能推定额外岗位");
      string required=rule=="secondary"||rule=="launcher_secondary"?"secondary":"primary";
      if(a.Slot!=required)r.Errors.Add(prefix+"slot：要求"+required);
      taken.Add(p.Id+"/"+required);if(rule=="both")taken.Add(p.Id+"/secondary");
     }
    }else{
     if(a.OperatorIds.Count!=0)r.Errors.Add(prefix+"operatorIds：载具不套用本体人数1作车组岗位");
     if(rule=="cannon_main"){if(p.Kind!="cannon"||a.Slot!="main")r.Errors.Add(prefix+"slot：机炮仅安装机炮炮塔主槽");}
     else if(rule=="vehicle_slot"){if(a.Slot!="coax"&&a.Slot!="hull")r.Errors.Add(prefix+"slot：车载武器仅车体／同轴槽");}
     else r.Errors.Add(prefix+"slot：人员武器不能隐式改为车载定义");
     if(!p.Slots.Contains(a.Slot))r.Errors.Add(prefix+"slot：位置没有该槽");taken.Add(p.Id+"/"+a.Slot);
     r.Unchecked.Add(prefix+"载具车组操作资格尚未有完整岗位规则");
    }
    foreach(var key in taken)if(!occupied.Add(key))r.Errors.Add(prefix+"slot：目标已占用 "+key);
    foreach(var k in a.Inventory.Keys)if(!w.AmmoIds.Any(id=>Same(id,k)))r.Errors.Add(prefix+"inventory：不是此武器的弹药引用 "+k);
    decimal inventorySum=0;bool stockKnown=true;
    foreach(var id in w.AmmoIds){var am=ammo.FirstOrDefault(x=>Same(x.Id,id));if(am==null){r.Errors.Add(prefix+"ammoId：共享弹药不存在");continue;}
     string stock;if(!a.Inventory.TryGetValue(id,out stock)||stock==null){r.Pending.Add(prefix+"携弹「"+am.Name+"」未配置");stockKnown=false;}else {decimal count;if(!Count(stock,out count,false))r.Errors.Add(prefix+"inventory ["+id+"]：须为非负整数");else try{inventorySum+=count;}catch(OverflowException){r.Errors.Add(prefix+"inventory：超出工具表示范围");}}
     foreach(var e in CombatRules.Validate(am))r.Errors.Add(prefix+"弹药「"+am.Name+"」 ["+e.Key+"]："+e.Value);
     foreach(var k in new[]{"damageType","category","damage","penetration","speed","blast","suppression","moduleDamage"})if(am.Fields[k]==null)r.Pending.Add(prefix+"弹药 "+k+" 未配置");
     if(am.Fields["damageType"]=="kinetic"&&(am.Fields["anchorRange"]==null||am.Fields["anchorPenetration"]==null))r.Pending.Add(prefix+"动能锚点未配置");
     if(infantry&&am.Fields["category"]=="HEAT"&&am.Fields["moduleDamage"]!="100")r.Errors.Add(prefix+"moduleDamage：步兵反甲固定100");
    }
    if(rule=="launcher_secondary"&&stockKnown&&inventorySum!=quantity)r.Errors.Add(prefix+"inventory：出场一次性发射器数量与实际库存须一致，不另加待发库存");
   }
   return r;
  }
  public static decimal? TransportWeight(ConfigurationDraft c,IList<BodyDraft> bodies,IList<CombatDraft> weapons){var b=bodies.FirstOrDefault(x=>Same(x.Id,c.BodyId));if(b==null)return null;decimal weight;if(b.UnitType!="infantry")return BodyRules.TryNumber(b.Fields["weight"],out weight)?(decimal?)weight:null;if(!BodyRules.TryNumber(b.Fields["memberCount"],out weight))return null;try{weight*=0.1m;foreach(var a in c.Assignments){var w=weapons.FirstOrDefault(x=>Same(x.Id,a.WeaponId));decimal extra,n;if(w==null||!BodyRules.TryNumber(w.Fields["specialEquipmentWeight"],out extra)||!Count(a.Quantity,out n,true))return null;weight+=extra*n;}return weight;}catch(OverflowException){return null;}}
  public static string Channel(ConfigurationDraft c,string weaponId,IList<BodyDraft> bodies,IList<CombatDraft> weapons){var w=weapons.FirstOrDefault(x=>Same(x.Id,weaponId));var b=bodies.FirstOrDefault(x=>Same(x.Id,c.BodyId));if(w==null||b==null)return "未配置";if(b.UnitType!="infantry")return "按安装独立，不合并／不推定齐射";decimal n=0,q;foreach(var a in c.Assignments.Where(a=>Same(a.WeaponId,weaponId))){if(!Count(a.Quantity,out q,true))return "数量未配置";try{n+=q;}catch(OverflowException){return "数量超出表示范围";}}if(n==0)return "未分配";return w.Fields["slotRule"]=="launcher_secondary"?(w.Fields["reload"]??"未配置准备时间")+" / "+n.ToString(CultureInfo.InvariantCulture)+" 秒；N固定，不是齐射":w.DerivedInterval()+" / N="+n.ToString(CultureInfo.InvariantCulture)+"；每次一枚，不是齐射";}
  public static ValidationReport ValidateCard(CardDraft card,IList<CardDraft> cards,IList<ConfigurationDraft> configs,IList<BodyDraft> bodies,IList<CombatDraft> weapons,IList<CombatDraft> ammo){var r=new ValidationReport();Identity(r,card.Id,card.Name,card.TestOnly,card.Baseline,card.SourceNote,card.CopiedFromId);
   foreach(var x in new[]{new[]{"valuePoints",card.ValuePoints},new[]{"deploymentPoints",card.DeploymentPoints}}){decimal v;if(x[1]==null)r.Pending.Add(x[0]+" 未配置");else if(!Count(x[1],out v,false)||v%5!=0)r.Errors.Add(x[0]+"：须为非负整数且为5的倍数");}
   if(string.IsNullOrWhiteSpace(card.Icon))r.Pending.Add("单位识别图案未配置"); decimal maximum;if(card.MaximumOnField==null)r.Pending.Add("最大在场数未配置；允许选择范围未定");else if(!Count(card.MaximumOnField,out maximum,true))r.Errors.Add("maximumOnField：须为正整数；设计上限范围尚未检查");
   var c=configs.FirstOrDefault(x=>Same(x.Id,card.ConfigurationId));if(card.ConfigurationId==null){r.Pending.Add("配置引用未选择");if(card.AssociatedCardIds.Count!=0)r.Errors.Add("关联：没有主配置");return r;}if(c==null){r.Errors.Add("configurationId：引用不存在");return r;}
   r.Unchecked.Add("配置卡最大在场数可选上限范围、作战群额度和完整组卡预算尚未检查"); var cr=Validate(c,bodies,weapons,ammo);foreach(var e in cr.Errors)r.Errors.Add("配置「"+c.Name+"」："+e);if(cr.Pending.Count!=0)r.Errors.Add("configurationId：配置「"+c.Name+"」尚未完成："+string.Join("、",cr.Pending.Distinct().Take(5))); r.Unchecked.AddRange(cr.Unchecked);
   if(card.AssociatedCardIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=card.AssociatedCardIds.Count)r.Errors.Add("关联重复");
   var primary=bodies.FirstOrDefault(x=>Same(x.Id,c.BodyId));
   foreach(var id in card.AssociatedCardIds){var other=cards.FirstOrDefault(x=>Same(x.Id,id));if(other==null||Same(id,card.Id)){r.Errors.Add("associatedCardIds：不存在／自引用");continue;}var otherConfig=configs.FirstOrDefault(x=>Same(x.Id,other.ConfigurationId));var carrier=otherConfig==null?null:bodies.FirstOrDefault(x=>Same(x.Id,otherConfig.BodyId));if(primary==null||carrier==null){r.Pending.Add("关联两端配置未完整选择");continue;}
    if(primary.UnitType!="infantry"||carrier.UnitType!="ground_vehicle"){r.Errors.Add("关联「"+other.Name+"」：本轮支持步兵→运载车；卡车→火炮缺少已确认用途表示，禁用");continue;}
    foreach(var k in new[]{"faction","specialization"})if(primary.Fields[k]!=null&&carrier.Fields[k]!=null&&primary.Fields[k]!=carrier.Fields[k])r.Errors.Add("关联「"+other.Name+"」："+k+"不兼容");
    decimal people,seats,load,supply;var weight=TransportWeight(c,bodies,weapons);
    if(!BodyRules.TryNumber(primary.Fields["memberCount"],out people)||!BodyRules.TryNumber(carrier.Fields["maxPassengers"],out seats)||!BodyRules.TryNumber(carrier.Fields["totalLoad"],out load)||!BodyRules.TryNumber(otherConfig.SupplyWeight,out supply)||weight==null)r.Pending.Add("关联「"+other.Name+"」：座位／满编重量／已有补给／总载重尚未检查");
    else{if(people>seats)r.Errors.Add("关联「"+other.Name+"」：最大乘员不足");try{if(weight.Value+supply>load)r.Errors.Add("关联「"+other.Name+"」：人员＋特殊装备＋已有补给超过同一个总载重池");}catch(OverflowException){r.Errors.Add("关联：吨位计算超出工具表示范围");}}
    var or=Validate(otherConfig,bodies,weapons,ammo);foreach(var e in or.Errors)r.Errors.Add("关联配置「"+otherConfig.Name+"」："+e);if(or.Pending.Count!=0)r.Errors.Add("关联配置尚未完成："+string.Join("、",or.Pending.Take(5))); r.Unchecked.AddRange(or.Unchecked);
   }
   if(card.AssociatedCardIds.Count!=0)r.Unchecked.Add("关联分项数量及组合预算呈现未决；没有永久绑定或自动定价");
   return r;
  }
  public static List<string> ValidateAll(IList<ConfigurationDraft> configs,IList<CardDraft> cards,IList<BodyDraft> bodies,IList<CombatDraft> weapons,IList<CombatDraft> ammo){var e=new List<string>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var c in configs){if(!ids.Add(c.Id))e.Add("重复配置ID "+c.Id);foreach(var x in Validate(c,bodies,weapons,ammo).Errors)e.Add(c.Name+" ["+c.Id+"]："+x);}foreach(var c in cards){if(!ids.Add(c.Id))e.Add("重复卡ID "+c.Id);foreach(var x in ValidateCard(c,cards,configs,bodies,weapons,ammo).Errors)e.Add(c.Name+" ["+c.Id+"]："+x);}return e;}
 }
}
