using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
namespace RTTUnitEditor.Domain
{
    public static class AssemblyRules
    {
        public static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        public static readonly string[] Categories={"recon","infantry","armor","support"};
        public static bool Count(string text, out decimal n, bool positive) { return BodyRules.TryNumber(text,out n)&&n>=(positive?1:0)&&decimal.Truncate(n)==n; }
        static void Identity(ValidationReport r, string id, bool test, string baseline, string source, string copied)
        {
            Guid g;
            if (!Guid.TryParseExact(id, "D", out g) || g == Guid.Empty) r.Errors.Add("id：非空GUID必填");
            if (!test) r.Errors.Add("testOnly：必须保留仅供测试身份");
            if (string.IsNullOrWhiteSpace(baseline) || source == null) r.Errors.Add("source：基线和来源必填");
            if (copied != null && !Guid.TryParseExact(copied, "D", out g)) r.Errors.Add("copiedFromId：无效GUID");
        }
        static void Compatible(ValidationReport r, string prefix, Dictionary<string,string> left, Dictionary<string,string> right, string[] keys)
        {
            foreach (var k in keys) {
                if (left[k] == null || right[k] == null) r.Pending.Add(prefix + k + " 尚未配置");
                else if (left[k] != right[k]) r.Errors.Add(prefix + k + " 不兼容");
            }
        }
        public static ValidationReport ValidateLoadout(LoadoutDraft c, IList<CombatDraft> weapons, IList<CombatDraft> ammo)
        {
            var r = new ValidationReport(); Identity(r,c.Id,c.TestOnly,c.Baseline,c.SourceNote,c.CopiedFromId);
            if (string.IsNullOrWhiteSpace(c.Name)) r.Errors.Add("name：配装名称必填");
            // DB33 explicitly permits complete unarmed transport/supply configurations.
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var occupied = new HashSet<string>();
            foreach (var e in c.Entries) {
                string p = "武器行 [" + e.Id + "] "; Guid id;
                if (!Guid.TryParseExact(e.Id,"D",out id) || id == Guid.Empty || !ids.Add(e.Id)) r.Errors.Add(p+"id 无效或重复");
                decimal n;
                if (e.Quantity == null) r.Pending.Add(p+"数量未配置");
                else if (!Count(e.Quantity,out n,true)) r.Errors.Add(p+"quantity：须为正整数");
                var w = weapons.FirstOrDefault(x=>Same(x.Id,e.WeaponId));
                if (e.WeaponId == null) { r.Pending.Add(p+"武器未选择"); continue; }
                if (w == null) { r.Errors.Add(p+"weaponId：引用不存在"); continue; }
                foreach (var x in CombatRules.Validate(w)) r.Errors.Add(p+"武器 "+x.Key+"："+x.Value);
                foreach (var k in new[]{"slotRule","operators","consumption","specialEquipmentWeight","maxRange","spread","aimMin","aimMax","ignoreReduction"}) if (w.Fields[k]==null) r.Pending.Add(p+"武器 "+k+" 未配置");
                if (w.TargetTypes.Count==0) r.Pending.Add(p+"允许攻击目标未配置");
                if (w.Fields["slotRule"]=="launcher_secondary") { if(w.Fields["reload"]==null)r.Pending.Add(p+"准备时间未配置"); if(w.Fields["consumption"]!=null&&w.Fields["consumption"]!="1")r.Errors.Add(p+"一次性发射每次消耗一具"); }
                else { if(w.Fields["rpm"]==null&&w.Fields["actualInterval"]==null)r.Pending.Add(p+"发射间隔未配置"); foreach(var k in new[]{"capacity","reloadRule","reload"})if(w.Fields[k]==null)r.Pending.Add(p+k+" 未配置"); }
                bool mounted=w.Fields["slotRule"]=="vehicle_slot"||w.Fields["slotRule"]=="cannon_main";
                if(mounted){if(e.MountKind==null)r.Pending.Add(p+"安装方式未选择");else if(!new[]{"hull","cannon","commander"}.Contains(e.MountKind))r.Errors.Add(p+"mountKind：未知安装方式");
                    if(w.Fields["slotRule"]=="cannon_main"&&e.MountKind!=null&&e.MountKind!="cannon")r.Errors.Add(p+"机炮主武器要求机炮炮塔");
                    decimal index;if(e.MountIndex==null)r.Pending.Add(p+"安装组未配置");else if(!Count(e.MountIndex,out index,true)||e.MountKind!="cannon"&&index!=1)r.Errors.Add(p+"mountIndex：安装组须为正整数，默认车长／车体组为1");
                    decimal normalized;if(e.MountKind!=null&&Count(e.MountIndex,out normalized,true)){string key=e.MountKind+"/"+normalized.ToString(CultureInfo.InvariantCulture)+"/"+(w.Fields["slotRule"]=="cannon_main"?"main":"coax");if(!occupied.Add(key))r.Errors.Add(p+"安装槽已占用，不覆盖原分配");} if(e.Quantity!=null&&Count(e.Quantity,out n,true)&&n!=1)r.Errors.Add(p+"一个明确车载安装槽只能分配一把武器");
                }else if(e.MountKind!=null||e.MountIndex!=null)r.Errors.Add(p+"人员武器不填写载具安装字段");
                if(e.Inventory.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=e.Inventory.Count)r.Errors.Add(p+"弹药关联重复（ID大小写不区分）");
                if(e.AmmoOrder==null&&e.Inventory.Count>0)r.Pending.Add(p+"择弹顺序未提供");else if(e.AmmoOrder!=null&&(e.AmmoOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=e.AmmoOrder.Count||e.AmmoOrder.Count!=e.Inventory.Count||e.AmmoOrder.Any(aid=>!e.Inventory.Keys.Any(k=>Same(k,aid)))))r.Errors.Add(p+"择弹顺序须恰好包含每个库存弹种一次");
                if(w.AmmoIds.Count==0&&e.Inventory.Count>0)r.Pending.Add(p+"武器可用弹种关联未提供");
                if(e.Inventory.Count==0)r.Pending.Add(p+"尚未关联弹药");if(e.Inventory.Count>1)r.Unchecked.Add(p+"多弹种共享武器级攻顶等属性的作用范围未决");
                decimal stock=0;bool known=true;
                foreach(var pair in e.Inventory){var a=ammo.FirstOrDefault(x=>Same(x.Id,pair.Key));if(a==null){r.Errors.Add(p+"ammoId：引用不存在 "+pair.Key);continue;}
                    if(w.AmmoIds.Count>0&&!w.AmmoIds.Any(aid=>Same(aid,pair.Key)))r.Errors.Add(p+"弹药不属于武器可用弹种 "+pair.Key); Compatible(r,p+"弹药「"+a.Name+"」 ",w.Fields,a.Fields,new[]{"caliber"});
                    foreach(var x in CombatRules.Validate(a))r.Errors.Add(p+"弹药 "+x.Key+"："+x.Value);
                    foreach(var k in new[]{"damageType","category","damage","penetration","speed","blast","suppression","moduleDamage"})if(a.Fields[k]==null)r.Pending.Add(p+"弹药 "+k+" 未配置");
                    if(a.Fields["damageType"]=="kinetic"&&(a.Fields["anchorRange"]==null||a.Fields["anchorPenetration"]==null))r.Pending.Add(p+"动能锚点未配置");
                    if(pair.Value==null){r.Pending.Add(p+"携弹「"+a.Name+"」未配置");known=false;}else if(!Count(pair.Value,out n,false))r.Errors.Add(p+"携弹「"+a.Name+"」：须为非负整数");else try{stock+=n;}catch(OverflowException){r.Errors.Add(p+"库存超出表示范围");}
                }
                if(w.Fields["slotRule"]=="launcher_secondary"&&known&&e.Inventory.Count>0&&Count(e.Quantity,out n,true)&&stock!=n)r.Errors.Add(p+"一次性发射器配置数量须与初始总库存一致");
            }
            return r;
        }
        public static decimal? TransportWeight(BodyDraft b,LoadoutDraft c,IList<CombatDraft> weapons)
        {
            decimal result;if(b.UnitType==null)return null;if(b.UnitType!="infantry")return BodyRules.TryNumber(b.Fields["weight"],out result)?(decimal?)result:null;
            if(!Count(b.Fields["memberCount"],out result,true))return null;
            try{result*=0.1m;foreach(var e in c.Entries){var w=weapons.FirstOrDefault(x=>Same(x.Id,e.WeaponId));decimal extra,n;if(w==null||!BodyRules.TryNumber(w.Fields["specialEquipmentWeight"],out extra)||!Count(e.Quantity,out n,true))return null;result+=extra*n;}return result;}catch(OverflowException){return null;}
        }
        public static ValidationReport ValidateBinding(UnitBinding link,IList<UnitBinding> links,IList<LoadoutDraft> configs,IList<BodyDraft> bodies,IList<CombatDraft> weapons,IList<CombatDraft> ammo)
        {
            var r=new ValidationReport();Identity(r,link.Id,link.TestOnly,link.Baseline,link.SourceNote,link.CopiedFromId);
            if(link.Category==null)r.Pending.Add("编组类别未配置");else if(!Categories.Contains(link.Category))r.Errors.Add("category：未知编组类别");
            foreach(var x in new[]{new[]{"valuePoints",link.ValuePoints},new[]{"deploymentPoints",link.DeploymentPoints}}){decimal v;if(x[1]==null)r.Pending.Add(x[0]+" 未配置");else if(!Count(x[1],out v,false)||v%5!=0)r.Errors.Add(x[0]+"：须为非负整数且5的倍数");}
            decimal maximum;if(link.MaximumOnField==null)r.Pending.Add("最大在场数未配置");else if(!Count(link.MaximumOnField,out maximum,true))r.Errors.Add("maximumOnField：须为正整数");
            if(string.IsNullOrWhiteSpace(link.Icon))r.Pending.Add("单位识别图案未配置");
            var b=bodies.FirstOrDefault(x=>Same(x.Id,link.BodyId));var c=configs.FirstOrDefault(x=>Same(x.Id,link.LoadoutId));
            if(b==null){if(link.BodyId==null)r.Pending.Add("单位未选择");else r.Errors.Add("bodyId：单位不存在");}
            if(c==null){if(link.LoadoutId==null)r.Pending.Add("配装未选择");else r.Errors.Add("loadoutId：配装不存在");}
            if(links.Any(x=>x.Id!=link.Id&&Same(x.LoadoutId,link.LoadoutId)&&link.LoadoutId!=null))r.Errors.Add("配装已经绑定，不能重复绑定至单位；需要变体请复制配装");
            if(b==null||c==null)return r;
            var relations=RelationRules.Validate(link,b,c,weapons);r.Errors.AddRange(relations.Errors);r.Pending.AddRange(relations.Pending);r.Unchecked.AddRange(relations.Unchecked);
            b=RelationRules.ResolveBody(b,link);b.Tags=RelationRules.DeclaredAbilities(b,link);
            foreach(var e in BodyRules.Validate(b))r.Errors.Add("单位 "+e.Key+"："+e.Value);
            if(b.UnitType==null){r.Pending.Add("单位类型未选择，人员和运输适用性尚未检查");return r;} foreach(var k in BodyDraft.LegacyFieldKeys)if(b.Fields[k]==null&&!(k=="roadSpeed"&&b.UnitType=="infantry"))r.Pending.Add("单位 "+k+" 未配置");
            var cr=ValidateLoadout(c,weapons,ammo);r.Errors.AddRange(cr.Errors);r.Pending.AddRange(cr.Pending);r.Unchecked.AddRange(cr.Unchecked);
            decimal primary=0,secondary=0,members;bool infantry=b.UnitType=="infantry",countsKnown=Count(b.Fields["memberCount"],out members,true);
            var occupied=new HashSet<string>();
            foreach(var e in c.Entries){var w=weapons.FirstOrDefault(x=>Same(x.Id,e.WeaponId));if(w==null)continue;string rule=w.Fields["slotRule"];if(rule==null)continue;decimal n,operators;if(!Count(e.Quantity,out n,true)||!Count(w.Fields["operators"],out operators,true)){countsKnown=false;continue;}
                bool vehicle=rule=="vehicle_slot"||rule=="cannon_main";
                if(infantry&&vehicle||!infantry&&!vehicle){r.Errors.Add("武器「"+w.Name+"」：人员／车载安装不适用于当前单位");continue;}
                if(infantry){try{if(rule=="operators_primary"||rule=="emplaced_artillery")primary+=operators*n;else if(rule=="primary"||rule=="both"){primary+=n;if(operators!=1&&!link.Relations.Roles.Any(role=>Same(role.EntryId,e.Id)&&role.OperatorIds.Count>=operators))r.Unchecked.Add("单人槽位模式的额外操作人员分配未提供");}else if(operators!=1&&!link.Relations.Roles.Any(role=>Same(role.EntryId,e.Id)&&role.OperatorIds.Count>=operators))r.Unchecked.Add("副槽模式的额外操作人员分配未提供");if(rule=="secondary"||rule=="both")secondary+=n;if(rule=="launcher_secondary")secondary+=1;}catch(OverflowException){r.Errors.Add("人员占槽总数超出工具表示范围");}
                    if(rule=="emplaced_artillery"&&(b.Tags.Contains("sprint")||link.Category!="support"))r.Errors.Add("架设火炮配置须为支援类别且单位不能冲刺");
                    foreach(var id in e.Inventory.Keys){var a=ammo.FirstOrDefault(x=>Same(x.Id,id));if(a!=null&&a.Fields["category"]=="HEAT"&&a.Fields["moduleDamage"]!="100")r.Errors.Add("步兵反甲弹模块破坏固定100");}
                }else{string slot=rule=="cannon_main"?"main":e.MountKind=="hull"?"hull":"coax";decimal group;if(e.MountKind!=null&&Count(e.MountIndex,out group,true)&&!occupied.Add(e.MountKind+"/"+group.ToString(CultureInfo.InvariantCulture)+"/"+slot))r.Errors.Add("安装槽已占用："+e.MountKind+" "+e.MountIndex+" "+slot);if(e.MountKind=="hull"&&b.Fields["offroadPenalty"]=="0.5"&&b.Installations==null)r.Errors.Add("轮式默认无车体武器槽；特殊结构尚未实现");if(b.Fields["offroadPenalty"]==null)r.Pending.Add("底盘适用性未配置");}
            }
            if(infantry){if(countsKnown){if(primary>members)r.Errors.Add("主槽不足：需要 "+primary+"，满编 "+members);if(secondary>members)r.Errors.Add("副槽不足：需要 "+secondary+"，满编 "+members);}else r.Pending.Add("人员占槽尚未检查，数量／操作人数未配置");if(link.SupplyWeight!=null)r.Errors.Add("步兵不填写车载已有补给");}
            else{decimal supply,load;if(link.SupplyWeight==null)r.Pending.Add("车载已有补给重量未配置（不能当0）");else if(!BodyRules.TryNumber(link.SupplyWeight,out supply)||supply<0)r.Errors.Add("supplyWeight：须为非负精确吨位");else if(BodyRules.TryNumber(b.Fields["totalLoad"],out load)&&supply>load)r.Errors.Add("已有补给超过总运输载重");foreach(var k in new[]{"weight","maxPassengers","totalLoad"})if(b.Fields[k]==null)r.Pending.Add("载具 "+k+" 未配置");}
            if(link.AssociatedBindingIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=link.AssociatedBindingIds.Count)r.Errors.Add("关联重复");
            foreach(var id in link.AssociatedBindingIds){var other=links.FirstOrDefault(x=>Same(x.Id,id));if(other==null||Same(id,link.Id)){r.Errors.Add("associatedBindingIds：不存在或自引用");continue;}var carrier=bodies.FirstOrDefault(x=>Same(x.Id,other.BodyId));if(carrier==null){r.Pending.Add("关联单位未选择");continue;}if(!infantry||carrier.UnitType!="ground_vehicle"){r.Errors.Add("当前只支持步兵→运载车关联；卡车→火炮用途表示未决");continue;}decimal seats,load,supply;var weight=TransportWeight(b,c,weapons);if(!Count(carrier.Fields["maxPassengers"],out seats,false)||!BodyRules.TryNumber(carrier.Fields["totalLoad"],out load)||!BodyRules.TryNumber(other.SupplyWeight,out supply)||weight==null||!Count(b.Fields["memberCount"],out members,true))r.Pending.Add("关联座位／重量／补给未配置");else{if(members>seats)r.Errors.Add("关联运输车座位不足");try{if(weight.Value+supply>load)r.Errors.Add("人员＋特殊装备＋已有补给超过同一个总运输载重池");}catch(OverflowException){r.Errors.Add("运输重量超出表示范围");}}}
            return r;
        }
        public static List<string> ValidateAll(IList<LoadoutDraft> configs,IList<UnitBinding> bindings,IList<BodyDraft> bodies,IList<CombatDraft> weapons,IList<CombatDraft> ammo)
        {
            var errors=new List<string>();
            var identities=bodies.Select(x=>x.Id).Concat(bodies.Where(x=>x.Installations!=null).SelectMany(x=>x.Installations.Select(m=>m.Id))).Concat(weapons.Select(x=>x.Id)).Concat(ammo.Select(x=>x.Id)).Concat(configs.Select(x=>x.Id)).Concat(configs.SelectMany(x=>x.Entries.Select(e=>e.Id))).Concat(bindings.Select(x=>x.Id)).Concat(bindings.SelectMany(x=>x.Relations.Members.Select(m=>m.Id).Concat(x.Relations.Roles.Select(r=>r.Id)))).ToList();
            var duplicateIds=new HashSet<string>(identities.GroupBy(x=>x,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key),StringComparer.OrdinalIgnoreCase);
            foreach(var binding in bindings){var body=bodies.FirstOrDefault(x=>Same(x.Id,binding.BodyId));var config=configs.FirstOrDefault(x=>Same(x.Id,binding.LoadoutId));var reachable=new List<string>{binding.Id,binding.BodyId,binding.LoadoutId};if(body!=null&&body.Installations!=null)reachable.AddRange(body.Installations.Select(x=>x.Id));if(config!=null)foreach(var entry in config.Entries){reachable.Add(entry.Id);reachable.Add(entry.WeaponId);reachable.AddRange(entry.Inventory.Keys);}if(reachable.Any(x=>x!=null&&duplicateIds.Contains(x)))errors.Add("单位绑定 ["+binding.Id+"]：引用对象身份重复");}
            foreach(var binding in bindings)if(binding.Relations.Members.Any(m=>duplicateIds.Contains(m.Id))||binding.Relations.Roles.Any(r=>duplicateIds.Contains(r.Id)))errors.Add("单位绑定 ["+binding.Id+"]：关系ID与其他对象重复");
            foreach(var c in configs)errors.AddRange(ValidateLoadout(c,weapons,ammo).Errors.Select(e=>c.Name+" ["+c.Id+"]："+e));foreach(var b in bindings)errors.AddRange(ValidateBinding(b,bindings,configs,bodies,weapons,ammo).Errors.Select(e=>"单位绑定 ["+b.Id+"]："+e));return errors;
        }
    }
}
