using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using RTTUnitEditor.Domain;
namespace RTTUnitEditor.Storage {
 public sealed class LegacyDocument {
  public List<BodyDraft> Bodies=new List<BodyDraft>();
  public List<CombatDraft> Weapons=new List<CombatDraft>(), Ammo=new List<CombatDraft>();
  public List<ConfigurationDraft> Configurations=new List<ConfigurationDraft>(); public List<CardDraft> Cards=new List<CardDraft>();
 }
 public static class LegacyUserFile {
  public const int Version=3;
  public static string Serialize(LegacyDocument doc,bool legacyRules=true) {
   var problems=CombatRules.ValidateAll(doc.Weapons,doc.Ammo,legacyRules);
   BodyFile.ValidateAll(doc.Bodies,legacyRules);
   problems.AddRange(ConfigurationRules.ValidateAll(doc.Configurations,doc.Cards,doc.Bodies,doc.Weapons,doc.Ammo));
   if(doc.Bodies.Select(b=>b.Id).Concat(doc.Weapons.Select(w=>w.Id)).Concat(doc.Ammo.Select(a=>a.Id)).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=doc.Bodies.Count+doc.Weapons.Count+doc.Ammo.Count)problems.Add("跨对象重复ID");
   var allIds=doc.Bodies.Select(b=>b.Id).Concat(doc.Weapons.Select(w=>w.Id)).Concat(doc.Ammo.Select(a=>a.Id)).Concat(doc.Configurations.Select(c=>c.Id)).Concat(doc.Cards.Select(c=>c.Id)).Concat(doc.Configurations.SelectMany(c=>c.Positions.Select(p=>p.Id).Concat(c.Assignments.Select(a=>a.Id)))).ToList();
   if(allIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=allIds.Count)problems.Add("整个文档对象／位置／分配ID重复");
   if(problems.Count!=0)throw new FormatException(string.Join("\r\n",problems));
   var bodyRoot=(Dictionary<string,object>)StrictJson.Parse(BodyFile.Serialize(doc.Bodies,legacyRules));
   var root=new Dictionary<string,object>{{"format",BodyFile.Format},{"version",Version},{"kind","definitions"},{"bodies",bodyRoot["bodies"]},{"weapons",doc.Weapons.Select(Record).ToArray()},{"ammo",doc.Ammo.Select(Record).ToArray()},{"configurations",doc.Configurations.Select(ConfigurationRecord).ToArray()},{"cards",doc.Cards.Select(CardRecord).ToArray()}};
   return new JavaScriptSerializer().Serialize(root);
  }
  static object Record(CombatDraft d) {return new Dictionary<string,object>{{"id",d.Id},{"name",d.Name},{"kind",d.Kind},{"testOnly",d.TestOnly},{"baseline",d.Baseline},{"copiedFromId",d.CopiedFromId},{"fields",d.Fields},{"tags",d.Tags},{"targets",d.TargetTypes},{"ammoIds",d.AmmoIds}};}
  public static LegacyDocument Parse(string json,bool legacyRules=true) {
   var root=Map(StrictJson.Parse(json));
   object v; if(!root.TryGetValue("version",out v)||!(v is JsonNumber))throw new FormatException("文件版本无效");
   if(((JsonNumber)v).Text=="1")return new LegacyDocument{Bodies=BodyFile.Parse(json,legacyRules)};
   bool legacy=((JsonNumber)v).Text=="2"; if(!legacy&&((JsonNumber)v).Text!="3")throw new FormatException("不支持的格式版本；支持1、2、3");
   Keys(root,legacy?new[]{"format","version","kind","bodies","weapons","ammo"}:new[]{"format","version","kind","bodies","weapons","ammo","configurations","cards"});
   if(Text(root["format"])!=BodyFile.Format||Text(root["kind"])!="definitions")throw new FormatException("不支持的工具文件格式");
   var bodyRoot=new Dictionary<string,object>{{"format",BodyFile.Format},{"version",1},{"kind","bodies"},{"bodies",root["bodies"]}};
   var doc=new LegacyDocument{Bodies=BodyFile.Parse(new JavaScriptSerializer().Serialize(bodyRoot),legacyRules),Weapons=ReadList(root["weapons"],"weapon",legacy),Ammo=ReadList(root["ammo"],"ammo",legacy)};
   if(!legacy){doc.Configurations=ReadConfigurations(root["configurations"]);doc.Cards=ReadCards(root["cards"]);}
   Serialize(doc,legacyRules);return doc;
  }
  static List<CombatDraft> ReadList(object value,string kind,bool legacy) {
   var values=value as List<object>;if(values==null)throw new FormatException(kind+"须为数组");var result=new List<CombatDraft>();
   foreach(var x in values){var m=Map(x);Keys(m,new[]{"id","name","kind","testOnly","baseline","copiedFromId","fields","tags","targets","ammoIds"});
    if(!(m["testOnly"] is bool))throw new FormatException("testOnly须为布尔值");
    var fields=Map(m["fields"]);Keys(fields,kind=="weapon"?(legacy?CombatDraft.LegacyWeaponKeys:CombatDraft.WeaponKeys):CombatDraft.AmmoKeys);
    if(legacy&&kind=="weapon")foreach(var key in CombatDraft.WeaponKeys.Except(CombatDraft.LegacyWeaponKeys))fields[key]=null;
    var d=new CombatDraft{Id=Text(m["id"]),Name=Text(m["name"]),Kind=Text(m["kind"]),TestOnly=(bool)m["testOnly"],Baseline=Text(m["baseline"]),CopiedFromId=NullableText(m["copiedFromId"]),Fields=fields.ToDictionary(p=>p.Key,p=>NullableText(p.Value)),Tags=Strings(m["tags"]),TargetTypes=Strings(m["targets"]),AmmoIds=Strings(m["ammoIds"])};
    if(d.Kind!=kind)throw new FormatException("对象列表类型不一致");result.Add(d);
   }return result;
  }
  static object ConfigurationRecord(ConfigurationDraft c){return new Dictionary<string,object>{{"id",c.Id},{"name",c.Name},{"bodyId",c.BodyId},{"category",c.Category},{"supplyWeight",c.SupplyWeight},{"sourceNote",c.SourceNote},{"copiedFromId",c.CopiedFromId},{"baseline",c.Baseline},{"testOnly",c.TestOnly},{"positions",c.Positions.Select(p=>new Dictionary<string,object>{{"id",p.Id},{"kind",p.Kind}}).ToArray()},{"assignments",c.Assignments.Select(a=>new Dictionary<string,object>{{"id",a.Id},{"weaponId",a.WeaponId},{"positionId",a.PositionId},{"slot",a.Slot},{"quantity",a.Quantity},{"priority",a.Priority},{"operatorIds",a.OperatorIds},{"inventory",a.Inventory}}).ToArray()}};}
  static object CardRecord(CardDraft c){return new Dictionary<string,object>{{"id",c.Id},{"name",c.Name},{"configurationId",c.ConfigurationId},{"valuePoints",c.ValuePoints},{"deploymentPoints",c.DeploymentPoints},{"maximumOnField",c.MaximumOnField},{"icon",c.Icon},{"sourceNote",c.SourceNote},{"copiedFromId",c.CopiedFromId},{"baseline",c.Baseline},{"testOnly",c.TestOnly},{"associatedCardIds",c.AssociatedCardIds}};}
  static List<object> Array(object v){var list=v as List<object>;if(list==null)throw new FormatException("须为数组");return list;}
  static bool Boolean(object v){if(!(v is bool))throw new FormatException("testOnly须为布尔值");return (bool)v;}
  static List<ConfigurationDraft> ReadConfigurations(object value){var result=new List<ConfigurationDraft>();foreach(var item in Array(value)){var m=Map(item);Keys(m,new[]{"id","name","bodyId","category","supplyWeight","sourceNote","copiedFromId","baseline","testOnly","positions","assignments"});var c=new ConfigurationDraft{Id=Text(m["id"]),Name=Text(m["name"]),BodyId=NullableText(m["bodyId"]),Category=NullableText(m["category"]),SupplyWeight=NullableText(m["supplyWeight"]),SourceNote=Text(m["sourceNote"]),CopiedFromId=NullableText(m["copiedFromId"]),Baseline=Text(m["baseline"]),TestOnly=Boolean(m["testOnly"])};
    foreach(var itemP in Array(m["positions"])){var p=Map(itemP);Keys(p,new[]{"id","kind"});c.Positions.Add(new PositionDraft{Id=Text(p["id"]),Kind=Text(p["kind"])});}
    foreach(var itemA in Array(m["assignments"])){var a=Map(itemA);Keys(a,new[]{"id","weaponId","positionId","slot","quantity","priority","operatorIds","inventory"});c.Assignments.Add(new AssignmentDraft{Id=Text(a["id"]),WeaponId=Text(a["weaponId"]),PositionId=Text(a["positionId"]),Slot=Text(a["slot"]),Quantity=NullableText(a["quantity"]),Priority=NullableText(a["priority"]),OperatorIds=Strings(a["operatorIds"]),Inventory=Map(a["inventory"]).ToDictionary(x=>x.Key,x=>NullableText(x.Value))});}result.Add(c);
   }return result;}
  static List<CardDraft> ReadCards(object value){var result=new List<CardDraft>();foreach(var item in Array(value)){var m=Map(item);Keys(m,new[]{"id","name","configurationId","valuePoints","deploymentPoints","maximumOnField","icon","sourceNote","copiedFromId","baseline","testOnly","associatedCardIds"});result.Add(new CardDraft{Id=Text(m["id"]),Name=Text(m["name"]),ConfigurationId=NullableText(m["configurationId"]),ValuePoints=NullableText(m["valuePoints"]),DeploymentPoints=NullableText(m["deploymentPoints"]),MaximumOnField=NullableText(m["maximumOnField"]),Icon=NullableText(m["icon"]),SourceNote=Text(m["sourceNote"]),CopiedFromId=NullableText(m["copiedFromId"]),Baseline=Text(m["baseline"]),TestOnly=Boolean(m["testOnly"]),AssociatedCardIds=Strings(m["associatedCardIds"])});}return result;}  static Dictionary<string,object> Map(object x){var m=x as Dictionary<string,object>;if(m==null)throw new FormatException("须为JSON对象");return m;}
  static string Text(object x){var s=x as string;if(s==null)throw new FormatException("须为字符串");return s;}
  static string NullableText(object x){return x==null?null:Text(x);}
  static List<string> Strings(object x){var l=x as List<object>;if(l==null)throw new FormatException("须为字符串数组");return l.Select(Text).ToList();}
  static void Keys(Dictionary<string,object> m,IEnumerable<string> keys){var set=new HashSet<string>(keys);if(m.Keys.Any(k=>!set.Contains(k))||set.Any(k=>!m.ContainsKey(k)))throw new FormatException("对象存在未知字段或缺少字段");}
  public static LegacyDocument Read(string path){if(new FileInfo(path).Length>4*1024*1024)throw new IOException("文件读取上限4MiB");return Parse(File.ReadAllText(path,new UTF8Encoding(false,true)));}
  public static void Save(string path,LegacyDocument doc){BodyFile.WriteJson(path,Serialize(doc));}
 }
}
