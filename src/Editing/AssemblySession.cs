using System;
using System.Collections.Generic;
using System.Linq;
using RTTUnitEditor.Domain;
namespace RTTUnitEditor.Editing
{
    public sealed partial class EditorSession
    {
        public void ApplyConfiguration(UnitBinding binding,LoadoutDraft loadout)
        {
            int bi=Bindings.FindIndex(x=>AssemblyRules.Same(x.Id,binding.Id)),li=Loadouts.FindIndex(x=>AssemblyRules.Same(x.Id,loadout.Id));
            if(bi<0||li<0||!AssemblyRules.Same(binding.LoadoutId,loadout.Id))throw new InvalidOperationException("配置与配装引用无效");
            var bindings=Bindings.Select(x=>AssemblyRules.Same(x.Id,binding.Id)?binding:x).ToList();var configs=Loadouts.Select(x=>AssemblyRules.Same(x.Id,loadout.Id)?loadout:x).ToList();
            var affected=AffectedBindings(new[]{loadout.Id},binding.BodyId);var errors=AssemblyRules.ValidateAll(configs,bindings,Bodies,Weapons,Ammo).Where(e=>e.Contains("["+binding.Id+"]")||e.Contains("["+loadout.Id+"]")||affected.Any(x=>e.Contains("["+x.Id+"]"))).ToList();
            if(errors.Count>0)throw new FormatException(string.Join("\r\n",errors));
            var nextBinding=binding.Clone();var nextLoadout=loadout.Clone();Bindings[bi]=nextBinding;Loadouts[li]=nextLoadout;Dirty=true;
        }
        public ValidationReport ValidateConfiguration(string bindingId)
        {
            var b=Bindings.FirstOrDefault(x=>AssemblyRules.Same(x.Id,bindingId));if(b==null)throw new InvalidOperationException("配置不存在");
            var report=AssemblyRules.ValidateBinding(b,Bindings,Loadouts,Bodies,Weapons,Ammo);report.Errors.AddRange(AssemblyRules.ValidateAll(Loadouts,Bindings,Bodies,Weapons,Ammo).Where(e=>e.Contains("["+b.Id+"]")&&(e.Contains("身份重复")||e.Contains("关系ID"))));return report;
        }
        public void ApplyPlatform(string bodyId,List<PlatformMount> mounts)
        {
            int i=Bodies.FindIndex(x=>AssemblyRules.Same(x.Id,bodyId));if(i<0)throw new InvalidOperationException("基体不存在");
            if(!Bodies[i].TestOnly)throw new InvalidOperationException("来源基体只读，请复制后编辑");var next=Bodies[i].Clone();next.Installations=mounts==null?null:mounts.Select(x=>x.Clone()).ToList();
            var errors=BodyRules.Validate(next).Values.ToList();var occupied=new HashSet<string>(Bodies.Where(x=>!AssemblyRules.Same(x.Id,bodyId)).Where(x=>x.Installations!=null).SelectMany(x=>x.Installations.Select(m=>m.Id)).Concat(Bodies.Select(x=>x.Id)).Concat(Weapons.Select(x=>x.Id)).Concat(Ammo.Select(x=>x.Id)).Concat(Loadouts.Select(x=>x.Id)).Concat(Loadouts.SelectMany(x=>x.Entries.Select(e=>e.Id))).Concat(Bindings.Select(x=>x.Id)).Concat(Bindings.SelectMany(x=>x.Relations.Members.Select(m=>m.Id).Concat(x.Relations.Roles.Select(r=>r.Id)))),StringComparer.OrdinalIgnoreCase);if(next.Installations!=null&&next.Installations.Any(m=>occupied.Contains(m.Id)))errors.Add("安装结构ID与其他对象重复");var bodies=Bodies.Select(x=>AssemblyRules.Same(x.Id,bodyId)?next:x).ToList();
            foreach(var binding in AffectedBindings(new string[0],bodyId))errors.AddRange(AssemblyRules.ValidateBinding(binding,Bindings,Loadouts,bodies,Weapons,Ammo).Errors);
            if(errors.Count>0)throw new FormatException(string.Join("\r\n",errors));Bodies[i]=next;Dirty=true;
        }
        public LoadoutDraft CreateLoadout() { var c=new LoadoutDraft{Name=Unique("新配装",Loadouts.Select(x=>x.Name))};Loadouts.Add(c);Dirty=true;return c; }
        public LoadoutDraft CopyLoadout(LoadoutDraft c) { if(!Loadouts.Any(x=>x.Id==c.Id))throw new InvalidOperationException("配装不存在");var copy=c.Copy(Unique(c.Name+" 副本",Loadouts.Select(x=>x.Name)));Loadouts.Add(copy);Dirty=true;return copy; }
        public UnitBinding CreateBinding(string bodyId) { if(!Bodies.Any(body=>body.Id==bodyId))throw new InvalidOperationException("先选择单位");var b=new UnitBinding{BodyId=bodyId};Bindings.Add(b);Dirty=true;return b; }
        public UnitBinding CopyBinding(UnitBinding b) { var c=Loadouts.FirstOrDefault(x=>x.Id==b.LoadoutId);if(c==null)throw new InvalidOperationException("原绑定尚未选择配装");var loadout=c.Copy(Unique(c.Name+" 副本",Loadouts.Select(x=>x.Name)));var copy=b.Copy();copy.LoadoutId=loadout.Id;copy.Relations=b.Relations.Copy(c.Entries.Zip(loadout.Entries,(a,z)=>new{a.Id,NewId=z.Id}).ToDictionary(x=>x.Id,x=>x.NewId,StringComparer.OrdinalIgnoreCase));Loadouts.Add(loadout);Bindings.Add(copy);Dirty=true;return copy; }
        public List<LoadoutDraft> LoadoutsForCombat(CombatDraft d) { return Loadouts.Where(c=>c.Entries.Any(e=>d.Kind=="weapon"?AssemblyRules.Same(e.WeaponId,d.Id):e.Inventory.Keys.Any(id=>AssemblyRules.Same(id,d.Id)))).ToList(); }
        public List<UnitBinding> AffectedBindings(IEnumerable<string> loadoutIds,string bodyId) { var ids=new HashSet<string>(loadoutIds,StringComparer.OrdinalIgnoreCase);var result=Bindings.Where(b=>ids.Contains(b.LoadoutId??"")||bodyId!=null&&AssemblyRules.Same(b.BodyId,bodyId)).ToList();bool more;do{more=false;foreach(var b in Bindings)if(!result.Contains(b)&&b.AssociatedBindingIds.Any(id=>result.Any(x=>AssemblyRules.Same(x.Id,id)))){result.Add(b);more=true;}}while(more);return result; }
        public string BindingName(UnitBinding b) { var body=Bodies.FirstOrDefault(x=>x.Id==b.BodyId);var c=Loadouts.FirstOrDefault(x=>x.Id==b.LoadoutId);return(body==null?"未选择单位":body.Name)+" / "+(c==null?"未选择配装":c.Name); }
        public string DescribeCombatImpact(CombatDraft d) { var configs=LoadoutsForCombat(d);return "受影响配装："+(configs.Count==0?"无":string.Join("、",configs.Select(c=>c.Name)))+"\r\n受影响单位配置："+string.Join("、",AffectedBindings(configs.Select(c=>c.Id),null).Select(BindingName)); }
        public string DescribeBodyImpact(string id) { return "受影响单位配置："+string.Join("、",AffectedBindings(new string[0],id).Select(BindingName)); }
        public void ApplyLoadout(LoadoutDraft c) { int i=Loadouts.FindIndex(x=>x.Id==c.Id);if(i<0)throw new InvalidOperationException("配装不存在");var next=Loadouts.Select(x=>x.Id==c.Id?c:x).ToList();var affected=AffectedBindings(new[]{c.Id},null);var errors=AssemblyRules.ValidateAll(next,Bindings,Bodies,Weapons,Ammo).Where(e=>e.Contains("["+c.Id+"]")||affected.Any(b=>e.Contains("["+b.Id+"]"))).ToList();if(errors.Count>0)throw new FormatException(string.Join("\r\n",errors));Loadouts[i]=c.Clone();Dirty=true; }
        public void ApplyBinding(UnitBinding b) { int i=Bindings.FindIndex(x=>x.Id==b.Id);if(i<0)throw new InvalidOperationException("绑定不存在");var next=Bindings.Select(x=>x.Id==b.Id?b:x).ToList();var affected=AffectedBindings(new string[0],b.BodyId);var errors=AssemblyRules.ValidateAll(Loadouts,next,Bodies,Weapons,Ammo).Where(e=>e.Contains("["+b.Id+"]")||affected.Any(x=>e.Contains("["+x.Id+"]"))).ToList();if(errors.Count>0)throw new FormatException(string.Join("\r\n",errors));Bindings[i]=b.Clone();Dirty=true; }
        public void DeleteLoadout(string id) { var refs=Bindings.Where(b=>AssemblyRules.Same(b.LoadoutId,id)).ToList();if(refs.Count>0)throw new InvalidOperationException("配装仍被单位绑定："+string.Join("、",refs.Select(BindingName)));var c=Loadouts.FirstOrDefault(x=>x.Id==id);if(c==null)throw new InvalidOperationException("配装不存在");Loadouts.Remove(c);Dirty=true; }
        public void DeleteBinding(string id) { if(Bindings.Any(link=>link.AssociatedBindingIds.Any(x=>AssemblyRules.Same(x,id))))throw new InvalidOperationException("单位配置仍被运输关联引用");var b=Bindings.FirstOrDefault(x=>x.Id==id);if(b==null)throw new InvalidOperationException("绑定不存在");Bindings.Remove(b);Dirty=true; }
        public void DeleteBody(string id) { if(Bindings.Any(link=>AssemblyRules.Same(link.BodyId,id)))throw new InvalidOperationException("单位仍有配置绑定，请先解除绑定");var b=Bodies.FirstOrDefault(x=>x.Id==id);if(b==null||!b.TestOnly)throw new InvalidOperationException("单位不存在或只读");Bodies.Remove(b);Dirty=true; }
    }
}
