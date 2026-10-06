using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace RTTUnitEditor.Domain
{
    public static class RelationRules
    {
        static bool Id(string s){Guid g;return Guid.TryParseExact(s,"D",out g)&&g!=Guid.Empty;}
        static string Key(string id){return id==null?"":id.ToLowerInvariant();}
        public static decimal? ConfiguredQuantity(LoadoutDraft loadout,string weaponId)
        {
            decimal total=0,n;try{foreach(var e in loadout.Entries.Where(x=>AssemblyRules.Same(x.WeaponId,weaponId))){if(!AssemblyRules.Count(e.Quantity,out n,true))return null;total+=n;}return total;}catch(OverflowException){return null;}
        }
        public static BodyDraft ResolveBody(BodyDraft body,UnitBinding binding)
        {
            var b=body.Clone();if(b.UnitType=="infantry"){b.Fields["memberCount"]=binding.Relations.MemberCount;b.UpdateInfantryHealth();}
            b.Fields["specialization"]=binding.Relations.Specialization;return b;
        }
        public static List<string> DeclaredAbilities(BodyDraft body,UnitBinding binding){return body.Tags.Concat(binding.Relations.Abilities.Select(x=>x.Tag)).Distinct().ToList();}
        public static ValidationReport ValidatePlatform(BodyDraft b)
        {
            var r=new ValidationReport();if(b.Installations==null)return r;
            var occupied=new HashSet<string>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var p in b.Installations){string prefix="安装结构 ["+p.Id+"] ";decimal n;
                if(!Id(p.Id)||!ids.Add(p.Id))r.Errors.Add(prefix+"id 无效或重复");
                if(b.UnitType!="ground_vehicle")r.Errors.Add(prefix+"仅地面载具适用");
                if(!new[]{"cannon","commander","hull"}.Contains(p.Kind))r.Errors.Add(prefix+"未知安装类型");
                if(!AssemblyRules.Count(p.Index,out n,true)||p.Kind!="cannon"&&n!=1)r.Errors.Add(prefix+"安装组须为正整数，车长／车体组为1");
                else if(!occupied.Add(p.Kind+"/"+n.ToString(CultureInfo.InvariantCulture)))r.Errors.Add(prefix+"同一安装位置重复声明");
            }return r;
        }
        public static ValidationReport Validate(UnitBinding binding,BodyDraft body,LoadoutDraft loadout,IList<CombatDraft> weapons)
        {
            var r=new ValidationReport();var c=binding.Relations;decimal members;
            if(string.IsNullOrWhiteSpace(c.Name))r.Pending.Add("配置名称未提供");
            if(c.Specialization==null)r.Pending.Add("配置专精未提供");else if(string.IsNullOrWhiteSpace(c.Specialization))r.Errors.Add("配置专精空白须用null");
            bool infantry=body.UnitType=="infantry";
            if(c.MemberCount==null)r.Pending.Add("配置满编人数未提供");else if(!AssemblyRules.Count(c.MemberCount,out members,true)||body.UnitType=="ground_vehicle"&&members!=1)r.Errors.Add("配置满编人数须为正整数；单体车辆为1（不是车组数量）");
            var memberIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var m in c.Members)if(!Id(m.Id)||!memberIds.Add(m.Id))r.Errors.Add("成员 ["+m.Id+"] ID无效或重复");
            if(infantry&&AssemblyRules.Count(c.MemberCount,out members,true)){if(c.Members.Count>members)r.Errors.Add("成员关系数量超过配置满编人数");else if(c.Members.Count<members)r.Pending.Add("成员关系数量与配置满编人数不一致");}
            var roleIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var slots=new HashSet<string>();
            foreach(var role in c.Roles){if(!Id(role.Id)||!roleIds.Add(role.Id))r.Errors.Add("岗位 ["+role.Id+"] ID无效或重复");}
            foreach(var role in c.Roles){string p="岗位 ["+role.Id+"] ";var e=loadout.Entries.FirstOrDefault(x=>AssemblyRules.Same(x.Id,role.EntryId));decimal n,operators,priority;
                if(e==null){r.Errors.Add(p+"武器分配引用不存在或不属于当前配装");continue;}
                if(role.Quantity==null)r.Pending.Add(p+"数量未提供");else if(!AssemblyRules.Count(role.Quantity,out n,true))r.Errors.Add(p+"数量须为正整数");
                if(role.Priority==null)r.Pending.Add(p+"接替优先级未提供");else if(!AssemblyRules.Count(role.Priority,out priority,false))r.Errors.Add(p+"优先级须为非负整数");
                if(role.OperatorIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=role.OperatorIds.Count)r.Errors.Add(p+"操作人员重复");
                foreach(var id in role.OperatorIds)if(!memberIds.Contains(id))r.Errors.Add(p+"操作人员不属于当前配置 "+id);
                if(role.CandidateRoleIds==null)r.Pending.Add(p+"接替候选关系未提供");else{
                    if(role.CandidateRoleIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=role.CandidateRoleIds.Count)r.Errors.Add(p+"候选岗位重复");
                    foreach(var id in role.CandidateRoleIds)if(!roleIds.Contains(id)||AssemblyRules.Same(id,role.Id))r.Errors.Add(p+"候选岗位不存在或自引用 "+id);
                }
                var w=weapons.FirstOrDefault(x=>AssemblyRules.Same(x.Id,e.WeaponId));if(w==null)continue;
                if(!AssemblyRules.Count(role.Quantity,out n,true)||!AssemblyRules.Count(w.Fields["operators"],out operators,true))continue;
                string rule=w.Fields["slotRule"];
                decimal required;try{required=(rule=="launcher_secondary"?1:n)*operators;}catch(OverflowException){r.Errors.Add(p+"操作人数超出表示范围");continue;}
                if(role.OperatorIds.Count==0)r.Pending.Add(p+"操作人员未提供，最低 "+required);else if(role.OperatorIds.Count<required)r.Errors.Add(p+"操作人数不足，最低 "+required);
                // Mutual exclusion is checked per declared slot, not by aggregate head count.
                if(infantry)foreach(var id in role.OperatorIds){foreach(var slot in rule=="both"?new[]{"primary","secondary"}:rule=="secondary"||rule=="launcher_secondary"?new[]{"secondary"}:new[]{"primary"})if(!slots.Add(Key(id)+"/"+slot))r.Errors.Add(p+"人员同时占用互斥槽位 "+id+"/"+slot);}
            }
            foreach(var e in loadout.Entries){var w=weapons.FirstOrDefault(x=>AssemblyRules.Same(x.Id,e.WeaponId));if(w==null)continue;decimal n;
                var roles=c.Roles.Where(x=>AssemblyRules.Same(x.EntryId,e.Id)).ToList();
                if(roles.Count==0)r.Pending.Add("武器行 ["+e.Id+"] 岗位与操作资格未配置");
                else if(AssemblyRules.Count(e.Quantity,out n,true)){decimal total=0;bool known=true;try{foreach(var role in roles){decimal q;if(!AssemblyRules.Count(role.Quantity,out q,true)){known=false;break;}total+=q;}if(known&&total!=n)r.Errors.Add("武器行 ["+e.Id+"] 岗位数量合计与配置武器数量不一致");}catch(OverflowException){r.Errors.Add("岗位数量超出表示范围");}}
                bool mounted=w.Fields["slotRule"]=="vehicle_slot"||w.Fields["slotRule"]=="cannon_main";
                if(mounted){if(body.Installations==null)r.Pending.Add("基体安装结构未提供");if(e.InstallationId==null)r.Pending.Add("武器行 ["+e.Id+"] 安装结构引用未提供");else{var m=body.Installations==null?null:body.Installations.FirstOrDefault(x=>AssemblyRules.Same(x.Id,e.InstallationId));if(m==null)r.Errors.Add("武器行 ["+e.Id+"] 安装引用不属于当前基体");else{decimal a,b;if(e.MountKind!=m.Kind||!AssemblyRules.Count(e.MountIndex,out a,true)||!AssemblyRules.Count(m.Index,out b,true)||a!=b)r.Errors.Add("武器行 ["+e.Id+"] 安装选择与平台结构不一致");if(m.Kind=="cannon"&&w.Fields["slotRule"]!="cannon_main"&&!loadout.Entries.Any(x=>AssemblyRules.Same(x.InstallationId,m.Id)&&weapons.Any(z=>AssemblyRules.Same(z.Id,x.WeaponId)&&z.Fields["slotRule"]=="cannon_main")))r.Errors.Add("同轴安装缺少同一炮塔的机炮主武器");}}}
                else if(e.InstallationId!=null)r.Errors.Add("人员武器不能引用载具安装结构");
            }
            ValidateStock(r,c,loadout,memberIds);
            var tags=new HashSet<string>();foreach(var a in c.Abilities){if(!BodyDraft.LegalTags.Contains(a.Tag)||!tags.Add(a.Tag))r.Errors.Add("配置能力未知或重复 "+a.Tag);if(a.EntryId==null||!loadout.Entries.Any(x=>AssemblyRules.Same(x.Id,a.EntryId)))r.Errors.Add("配置能力来源不是已装备武器行 "+a.Tag);else r.Unchecked.Add("能力 ["+a.Tag+"] 装备来源已关联，具体装备赋予该能力的资格依据仍需配置核对");}
            if(body.Tags.Count>0&&body.Baseline!=BodyRules.Baseline)r.Unchecked.Add("旧基线能力标签的固有／装备来源尚未核对，保留原值不自动授予资格");
            var all=DeclaredAbilities(body,binding);if(all.Contains("smoke_1")&&all.Contains("smoke_4"))r.Errors.Add("基体与配置合并后烟雾×1与×4互斥");if(!infantry&&all.Contains("sprint"))r.Errors.Add("地面车辆不具有步兵冲刺资格");
            return r;
        }
        static void ValidateStock(ValidationReport r,ConfigurationRelations c,LoadoutDraft loadout,HashSet<string> members)
        {
            if(c.StockOwners==null){if(loadout.Entries.Any(e=>e.Inventory.Count>0))r.Pending.Add("初始弹药库存个人／通道归属未提供");return;}
            var keys=new HashSet<string>();foreach(var s in c.StockOwners){string p="库存归属 ["+s.EntryId+"/"+s.AmmoId+"] ";var e=loadout.Entries.FirstOrDefault(x=>AssemblyRules.Same(x.Id,s.EntryId));decimal q;
                if(e==null||!e.Inventory.Keys.Any(x=>AssemblyRules.Same(x,s.AmmoId))){r.Errors.Add(p+"引用不属于当前配置库存");continue;}
                if(s.MemberId!=null&&!members.Contains(s.MemberId))r.Errors.Add(p+"成员引用不存在");
                if(!keys.Add(Key(s.EntryId)+"/"+Key(s.AmmoId)+"/"+Key(s.MemberId)))r.Errors.Add(p+"重复归属记录");
                if(s.Quantity==null)r.Pending.Add(p+"数量未提供");else if(!AssemblyRules.Count(s.Quantity,out q,false))r.Errors.Add(p+"数量须为非负整数");
            }
            foreach(var e in loadout.Entries)foreach(var stock in e.Inventory){var owners=c.StockOwners.Where(x=>AssemblyRules.Same(x.EntryId,e.Id)&&AssemblyRules.Same(x.AmmoId,stock.Key)).ToList();decimal expected,total=0,q;
                if(owners.Count==0){r.Pending.Add("库存 ["+e.Id+"/"+stock.Key+"] 归属未提供");continue;}
                try{bool known=AssemblyRules.Count(stock.Value,out expected,false);foreach(var s in owners){if(!AssemblyRules.Count(s.Quantity,out q,false)){known=false;break;}total+=q;}if(known&&expected!=total)r.Errors.Add("库存 ["+e.Id+"/"+stock.Key+"] 归属数量合计不等于实际总库存");}catch(OverflowException){r.Errors.Add("库存归属合计超出表示范围");}
            }
        }
    }
}
