using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using RTTUnitEditor.Domain;
namespace RTTUnitEditor.Storage
{
    public sealed class UserDocument
    {
        public List<BodyDraft> Bodies=new List<BodyDraft>();
        public List<CombatDraft> Weapons=new List<CombatDraft>(), Ammo=new List<CombatDraft>();
        public List<LoadoutDraft> Loadouts=new List<LoadoutDraft>();
        public List<UnitBinding> Bindings=new List<UnitBinding>();
        public string LegacyArchive, MigrationNote;
    }
    public static class UserFile
    {
        public const int Version=5;
        static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=4*1024*1024 };
        public static string Serialize(UserDocument doc)
        {
            BodyFile.ValidateAll(doc.Bodies);
            var errors=CombatRules.ValidateAll(doc.Weapons,doc.Ammo);
            errors.AddRange(AssemblyRules.ValidateAll(doc.Loadouts,doc.Bindings,doc.Bodies,doc.Weapons,doc.Ammo));
            var ids=doc.Bodies.Select(x=>x.Id).Concat(doc.Weapons.Select(x=>x.Id)).Concat(doc.Ammo.Select(x=>x.Id)).Concat(doc.Loadouts.Select(x=>x.Id)).Concat(doc.Loadouts.SelectMany(x=>x.Entries.Select(e=>e.Id))).Concat(doc.Bindings.Select(x=>x.Id)).Concat(doc.Bodies.Where(x=>x.Installations!=null).SelectMany(x=>x.Installations.Select(m=>m.Id))).Concat(doc.Bindings.SelectMany(x=>x.Relations.Members.Select(m=>m.Id).Concat(x.Relations.Roles.Select(r=>r.Id)))).ToList();
            if(ids.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=ids.Count)errors.Add("文档对象ID重复");
            if(errors.Count>0)throw new FormatException(string.Join("\r\n",errors));
            var body=Map(StrictJson.Parse(BodyFile.Serialize(doc.Bodies)));
            return Json.Serialize(new Dictionary<string,object>{{"format",BodyFile.Format},{"version",Version},{"kind","definitions"},{"bodies",body["bodies"]},{"weapons",doc.Weapons.Select(Definition).ToArray()},{"ammo",doc.Ammo.Select(Definition).ToArray()},{"loadouts",doc.Loadouts},{"bindings",doc.Bindings},{"installations",doc.Bodies.ToDictionary(b=>b.Id,b=>(object)b.Installations)},{"legacyArchive",doc.LegacyArchive},{"migrationNote",doc.MigrationNote}});
        }
        static object Definition(CombatDraft d){return new Dictionary<string,object>{{"id",d.Id},{"name",d.Name},{"kind",d.Kind},{"testOnly",d.TestOnly},{"baseline",d.Baseline},{"copiedFromId",d.CopiedFromId},{"fields",d.Fields},{"tags",d.Tags},{"targets",d.TargetTypes},{"ammoIds",d.AmmoIds}};}
        public static UserDocument Parse(string text)
        {
            var root=Map(StrictJson.Parse(text));object version;if(!root.TryGetValue("version",out version)||!(version is JsonNumber))throw new FormatException("版本无效");
            bool v4=((JsonNumber)version).Text=="4"; if(!v4&&((JsonNumber)version).Text!="5")return Migrate(text);
            Keys(root,v4?new[]{"format","version","kind","bodies","weapons","ammo","loadouts","bindings","legacyArchive","migrationNote"}:new[]{"format","version","kind","bodies","weapons","ammo","loadouts","bindings","installations","legacyArchive","migrationNote"});
            if(Text(root["format"])!=BodyFile.Format||Text(root["kind"])!="definitions")throw new FormatException("工具格式无效");
            var definitions=new Dictionary<string,object>{{"format",BodyFile.Format},{"version",3},{"kind","definitions"},{"bodies",root["bodies"]},{"weapons",RestoreDefinition(root["weapons"],v4)},{"ammo",RestoreDefinition(root["ammo"],v4)},{"configurations",new object[0]},{"cards",new object[0]}};
            var old=LegacyUserFile.Parse(Json.Serialize(definitions),false);var doc=new UserDocument{Bodies=old.Bodies,Weapons=old.Weapons,Ammo=old.Ammo,LegacyArchive=Nullable(root["legacyArchive"]),MigrationNote=Nullable(root["migrationNote"])};
            foreach(var raw in Array(root["loadouts"])){
                var m=Map(raw);Keys(m,new[]{"Id","Name","Baseline","SourceNote","CopiedFromId","TestOnly","Entries"});
                var c=new LoadoutDraft{Id=Text(m["Id"]),Name=Text(m["Name"]),Baseline=Text(m["Baseline"]),SourceNote=Text(m["SourceNote"]),CopiedFromId=Nullable(m["CopiedFromId"]),TestOnly=Boolean(m["TestOnly"])};
                foreach(var rawE in Array(m["Entries"])){var e=Map(rawE);Keys(e,v4?new[]{"Id","WeaponId","Quantity","MountKind","MountIndex","Inventory"}:new[]{"Id","WeaponId","Quantity","MountKind","MountIndex","Inventory","InstallationId","AmmoOrder"});c.Entries.Add(new WeaponEntry{Id=Text(e["Id"]),WeaponId=Nullable(e["WeaponId"]),Quantity=Nullable(e["Quantity"]),MountKind=Nullable(e["MountKind"]),MountIndex=Nullable(e["MountIndex"]),InstallationId=v4?null:Nullable(e["InstallationId"]),AmmoOrder=v4?null:StringsOrNull(e["AmmoOrder"]),Inventory=Map(e["Inventory"]).ToDictionary(p=>p.Key,p=>Nullable(p.Value))});}doc.Loadouts.Add(c);
            }
            foreach(var raw in Array(root["bindings"])){var m=Map(raw);Keys(m,v4?new[]{"Id","BodyId","LoadoutId","Category","ValuePoints","DeploymentPoints","MaximumOnField","Icon","SupplyWeight","Baseline","SourceNote","CopiedFromId","TestOnly","AssociatedBindingIds"}:new[]{"Id","BodyId","LoadoutId","Category","ValuePoints","DeploymentPoints","MaximumOnField","Icon","SupplyWeight","Baseline","SourceNote","CopiedFromId","TestOnly","AssociatedBindingIds","Relations"});doc.Bindings.Add(new UnitBinding{Id=Text(m["Id"]),BodyId=Nullable(m["BodyId"]),LoadoutId=Nullable(m["LoadoutId"]),Category=Nullable(m["Category"]),ValuePoints=Nullable(m["ValuePoints"]),DeploymentPoints=Nullable(m["DeploymentPoints"]),MaximumOnField=Nullable(m["MaximumOnField"]),Icon=Nullable(m["Icon"]),SupplyWeight=Nullable(m["SupplyWeight"]),Baseline=Text(m["Baseline"]),SourceNote=Text(m["SourceNote"]),CopiedFromId=Nullable(m["CopiedFromId"]),TestOnly=Boolean(m["TestOnly"]),Relations=v4?new ConfigurationRelations():ReadRelations(m["Relations"]),AssociatedBindingIds=Array(m["AssociatedBindingIds"]).Select(Text).ToList()});}
            if(v4){MigrateFields(doc);if(doc.LegacyArchive==null)doc.LegacyArchive=text;doc.MigrationNote="已迁移v4配置人数／专精／名称；岗位、库存归属、安装结构和择弹顺序缺失保持未提供。";}else{var mounts=Map(root["installations"]);if(mounts.Count!=doc.Bodies.Count||mounts.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=mounts.Count||mounts.Keys.Any(id=>!doc.Bodies.Any(b=>AssemblyRules.Same(b.Id,id))))throw new FormatException("安装结构所属基体未知、重复或缺失");foreach(var b in doc.Bodies){var mountValue=mounts.First(pair=>AssemblyRules.Same(pair.Key,b.Id)).Value;if(mountValue==null){b.Installations=null;continue;}b.Installations=new List<PlatformMount>();foreach(var raw in Array(mountValue)){var m=Map(raw);Keys(m,new[]{"Id","Kind","Index"});b.Installations.Add(new PlatformMount{Id=Text(m["Id"]),Kind=Nullable(m["Kind"]),Index=Nullable(m["Index"])});}}}
            Serialize(doc);return doc;
        }
        static object RestoreDefinition(object value,bool v4){return Array(value).Select(raw=>{var m=Map(raw);Keys(m,v4?new[]{"id","name","kind","testOnly","baseline","copiedFromId","fields","tags","targets"}:new[]{"id","name","kind","testOnly","baseline","copiedFromId","fields","tags","targets","ammoIds"});if(v4)m["ammoIds"]=new object[0];return m;}).ToArray();}
        static UserDocument Migrate(string text)
        {
            var old=LegacyUserFile.Parse(text);var doc=new UserDocument{Bodies=old.Bodies,Weapons=old.Weapons,Ammo=old.Ammo,LegacyArchive=text,MigrationNote="已在内存迁移旧工具文件；旧岗位、优先级、卡名及未使用的武器弹药关联原样保存在legacyArchive。未覆盖原文件，绑定需重新检查。"};
            foreach(var c in old.Configurations){var next=new LoadoutDraft{Id=c.Id,Name=c.Name,Baseline=c.Baseline,SourceNote=c.SourceNote,CopiedFromId=c.CopiedFromId,TestOnly=c.TestOnly};foreach(var a in c.Assignments){var entry=new WeaponEntry{Id=a.Id,WeaponId=a.WeaponId,Quantity=a.Quantity,Inventory=new Dictionary<string,string>(a.Inventory)};var w=old.Weapons.FirstOrDefault(x=>AssemblyRules.Same(x.Id,a.WeaponId));if(w!=null)foreach(var id in w.AmmoIds)if(!entry.Inventory.ContainsKey(id))entry.Inventory[id]=null;var position=c.Positions.FirstOrDefault(x=>AssemblyRules.Same(x.Id,a.PositionId));if(position!=null&&position.Kind!="soldier"){entry.MountKind=position.Kind;entry.MountIndex=position.Kind=="cannon"?(c.Positions.Where(x=>x.Kind=="cannon").ToList().FindIndex(x=>x.Id==position.Id)+1).ToString():"1";}next.Entries.Add(entry);}doc.Loadouts.Add(next);
                var cards=old.Cards.Where(x=>AssemblyRules.Same(x.ConfigurationId,c.Id)).ToList();if(cards.Count==0&&c.BodyId!=null)doc.Bindings.Add(new UnitBinding{BodyId=c.BodyId,LoadoutId=c.Id,Category=c.Category,SupplyWeight=c.SupplyWeight,SourceNote=c.SourceNote,Baseline=c.Baseline});
                foreach(var card in cards)doc.Bindings.Add(new UnitBinding{Id=card.Id,BodyId=c.BodyId,LoadoutId=c.Id,Category=c.Category,SupplyWeight=c.SupplyWeight,ValuePoints=card.ValuePoints,DeploymentPoints=card.DeploymentPoints,MaximumOnField=card.MaximumOnField,Icon=card.Icon,SourceNote=card.SourceNote,Baseline=card.Baseline,CopiedFromId=card.CopiedFromId,AssociatedBindingIds=new List<string>(card.AssociatedCardIds)});
            }
            // Multiple historical cards may refer to the same body/config pair. Preserve them;
            // do not silently merge. The user resolves these reported duplicates before v4 save.
            foreach(var w in doc.Weapons)w.AmmoIds.Clear();MigrateFields(doc);return doc;
        }
        static List<string> StringsOrNull(object o){return o==null?null:Array(o).Select(Text).ToList();}
        static ConfigurationRelations ReadRelations(object raw)
        {
            var m=Map(raw);Keys(m,new[]{"Name","MemberCount","Specialization","Members","Roles","StockOwners","Abilities"});
            var c=new ConfigurationRelations{Name=Nullable(m["Name"]),MemberCount=Nullable(m["MemberCount"]),Specialization=Nullable(m["Specialization"])};
            foreach(var item in Array(m["Members"])){var x=Map(item);Keys(x,new[]{"Id"});c.Members.Add(new ConfigMember{Id=Text(x["Id"])});}
            foreach(var item in Array(m["Roles"])){var x=Map(item);Keys(x,new[]{"Id","EntryId","Quantity","Priority","OperatorIds","CandidateRoleIds"});c.Roles.Add(new WeaponRole{Id=Text(x["Id"]),EntryId=Nullable(x["EntryId"]),Quantity=Nullable(x["Quantity"]),Priority=Nullable(x["Priority"]),OperatorIds=Array(x["OperatorIds"]).Select(Text).ToList(),CandidateRoleIds=StringsOrNull(x["CandidateRoleIds"])});}
            if(m["StockOwners"]!=null){c.StockOwners=new List<StockOwner>();foreach(var item in Array(m["StockOwners"])){var x=Map(item);Keys(x,new[]{"EntryId","AmmoId","MemberId","Quantity"});c.StockOwners.Add(new StockOwner{EntryId=Nullable(x["EntryId"]),AmmoId=Nullable(x["AmmoId"]),MemberId=Nullable(x["MemberId"]),Quantity=Nullable(x["Quantity"])});}}
            foreach(var item in Array(m["Abilities"])){var x=Map(item);Keys(x,new[]{"Tag","EntryId"});c.Abilities.Add(new AbilitySource{Tag=Nullable(x["Tag"]),EntryId=Nullable(x["EntryId"])});}return c;
        }
        static void MigrateFields(UserDocument doc)
        {
            foreach(var binding in doc.Bindings){var body=doc.Bodies.FirstOrDefault(x=>AssemblyRules.Same(x.Id,binding.BodyId));var loadout=doc.Loadouts.FirstOrDefault(x=>AssemblyRules.Same(x.Id,binding.LoadoutId));if(body!=null){binding.Relations.MemberCount=body.Fields["memberCount"];binding.Relations.Specialization=body.Fields["specialization"];}if(loadout!=null)binding.Relations.Name=loadout.Name;}
        }
        static Dictionary<string,object> Map(object o){var m=o as Dictionary<string,object>;if(m==null)throw new FormatException("须为对象");return m;}
        static List<object> Array(object o){var a=o as List<object>;if(a==null)throw new FormatException("须为数组");return a;}
        static string Text(object o){var s=o as string;if(s==null)throw new FormatException("须为字符串");return s;}
        static string Nullable(object o){return o==null?null:Text(o);}
        static bool Boolean(object o){if(!(o is bool))throw new FormatException("须为布尔值");return (bool)o;}
        static void Keys(Dictionary<string,object> m,IEnumerable<string> keys){var set=new HashSet<string>(keys);if(m.Keys.Any(k=>!set.Contains(k))||set.Any(k=>!m.ContainsKey(k)))throw new FormatException("未知字段或缺少字段");}
        public static UserDocument Read(string path){if(new FileInfo(path).Length>4*1024*1024)throw new IOException("文件上限4MiB");return Parse(File.ReadAllText(path,new UTF8Encoding(false,true)));}
        public static void Save(string path,UserDocument doc){BodyFile.WriteJson(path,Serialize(doc));}
    }
}
