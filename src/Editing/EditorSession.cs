using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using RTTUnitEditor.Domain;
using RTTUnitEditor.Storage;
namespace RTTUnitEditor.Editing
{
    public enum LeaveChoice { Save, Continue, Cancel }
    public sealed partial class EditorSession
    {
        UserDocument document=new UserDocument(), saved=new UserDocument();
        readonly HashSet<string> protectedPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,string> references=new Dictionary<string,string>();
        public List<BodyDraft> Bodies { get { return document.Bodies; } }
        public List<CombatDraft> Weapons { get { return document.Weapons; } }
        public List<CombatDraft> Ammo { get { return document.Ammo; } }
        public List<LoadoutDraft> Loadouts { get { return document.Loadouts; } }
        public List<UnitBinding> Bindings { get { return document.Bindings; } }
        public string FilePath { get; private set; }
        public bool Dirty { get; private set; }
        public string MigrationNote { get { return document.MigrationNote; } }
        public UserDocument Document { get { return document; } }
        static UserDocument CopyDocument(UserDocument d) { return new UserDocument{Bodies=d.Bodies.Select(x=>x.Clone()).ToList(),Weapons=d.Weapons.Select(x=>x.Clone()).ToList(),Ammo=d.Ammo.Select(x=>x.Clone()).ToList(),Loadouts=d.Loadouts.Select(x=>x.Clone()).ToList(),Bindings=d.Bindings.Select(x=>x.Clone()).ToList(),LegacyArchive=d.LegacyArchive,MigrationNote=d.MigrationNote}; }
        public void MarkChanged() { Dirty=true; }
        public void RecalculateDirty() { var j=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};Dirty=j.Serialize(document)!=j.Serialize(saved); }
        public bool AllowLeave(LeaveChoice choice,Func<bool> save) { if(!Dirty)return true;if(choice==LeaveChoice.Cancel)return false;if(choice==LeaveChoice.Save)return save();document=CopyDocument(saved);Dirty=false;return true; }
        public static string Unique(string stem,IEnumerable<string> names) { var set=new HashSet<string>(names);string result=stem;int n=2;while(set.Contains(result))result=stem+" "+n++;return result; }
        public string UniqueName(string stem) { return Unique(stem,Bodies.Select(x=>x.Name)); }
        public BodyDraft Create(string type) { var d=BodyDraft.Create(type,UniqueName("新单位本体"));Bodies.Add(d);Dirty=true;return d; }
        public BodyDraft Copy(BodyDraft d) { if(BodyRules.Validate(d).Count!=0)throw new FormatException("先修正本体字段");var c=d.Copy(UniqueName(d.Name+" 副本"));Bodies.Add(c);Dirty=true;return c; }
        public Dictionary<string,string> Errors(BodyDraft b) { var e=BodyRules.Validate(b);if(Bodies.Any(x=>x.Id!=b.Id&&x.Name==b.Name))e["name"]="同名单位冲突";if(!b.TestOnly&&references.ContainsKey(b.Id)&&BodyFile.Serialize(new[]{b},true)!=references[b.Id])e["reference"]="来源参考只读，请复制后编辑";return e; }
        public void Open(string path) { var next=UserFile.Read(path);var nextRefs=next.Bodies.Where(x=>!x.TestOnly).ToDictionary(x=>x.Id,x=>BodyFile.Serialize(new[]{x},true));document=next;saved=CopyDocument(next);references=nextRefs;FilePath=Path.GetFullPath(path);Dirty=false;if(nextRefs.Count>0)protectedPaths.Add(FilePath); }
        public void Reload() { if(FilePath==null)throw new InvalidOperationException("尚无打开文件");Open(FilePath); }
        public void Save(string path) { var absolute=Path.GetFullPath(path);if(protectedPaths.Contains(absolute))throw new IOException("来源参考文件受保护，请另存");foreach(var b in Bodies){var e=Errors(b);if(e.Count>0)throw new FormatException(b.Name+"："+string.Join("；",e.Values));}UserFile.Save(absolute,document);saved=CopyDocument(document);FilePath=absolute;Dirty=false; }
        public CombatDraft CreateCombat(string kind) { if(kind!="weapon"&&kind!="ammo")throw new ArgumentException("未知定义种类");var list=kind=="weapon"?Weapons:Ammo;var d=CombatDraft.Create(kind,Unique(kind=="weapon"?"新武器":"新弹药",list.Select(x=>x.Name)));list.Add(d);Dirty=true;return d; }
        public CombatDraft CopyCombat(CombatDraft d) { var list=d.Kind=="weapon"?Weapons:Ammo;if(!list.Any(x=>x.Id==d.Id))throw new InvalidOperationException("原定义不存在");var c=d.Copy(Unique(d.Name+" 副本",list.Select(x=>x.Name)));list.Add(c);Dirty=true;return c; }
        public List<CombatDraft> AffectedWeapons(string ammoId) { var ids=Loadouts.SelectMany(c=>c.Entries.Where(e=>e.Inventory.Keys.Any(id=>AssemblyRules.Same(id,ammoId))).Select(e=>e.WeaponId));return Weapons.Where(w=>w.AmmoIds.Any(id=>AssemblyRules.Same(id,ammoId))||ids.Any(id=>AssemblyRules.Same(id,w.Id))).ToList(); }
        public bool ApplyCombat(CombatDraft edited,Func<List<CombatDraft>,bool> confirm) { var list=edited.Kind=="weapon"?Weapons:Ammo;int i=list.FindIndex(x=>x.Id==edited.Id);if(i<0)throw new InvalidOperationException("定义不存在");var ws=Weapons.Select(x=>x.Id==edited.Id?edited:x).ToList();var am=Ammo.Select(x=>x.Id==edited.Id?edited:x).ToList();var errors=CombatRules.ValidateAll(ws,am);var configs=LoadoutsForCombat(edited);var bindings=AffectedBindings(configs.Select(c=>c.Id),null);errors.AddRange(AssemblyRules.ValidateAll(Loadouts,Bindings,Bodies,ws,am).Where(e=>configs.Any(c=>e.Contains("["+c.Id+"]"))||bindings.Any(b=>e.Contains("["+b.Id+"]"))));if(errors.Count>0)throw new FormatException(string.Join("\r\n",errors));if(configs.Count>0&&!confirm(edited.Kind=="ammo"?AffectedWeapons(edited.Id):new List<CombatDraft>()))return false;list[i]=edited.Clone();Dirty=true;return true; }
        public void DeleteCombat(CombatDraft d) { var refs=LoadoutsForCombat(d);if(d.Kind=="ammo"&&Weapons.Any(w=>w.AmmoIds.Any(id=>AssemblyRules.Same(id,d.Id))))throw new InvalidOperationException("弹药仍被武器可用弹种引用");if(refs.Count>0)throw new InvalidOperationException("定义仍被配装引用："+string.Join("、",refs.Select(x=>x.Name)));var list=d.Kind=="weapon"?Weapons:Ammo;var item=list.FirstOrDefault(x=>x.Id==d.Id);if(item==null)throw new InvalidOperationException("定义不存在");list.Remove(item);Dirty=true; }
    }
}
