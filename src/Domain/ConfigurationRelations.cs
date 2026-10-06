using System;
using System.Collections.Generic;
using System.Linq;
namespace RTTUnitEditor.Domain
{
    // DB33 static relationships; not runtime personnel or weapon channels.
    public sealed class PlatformMount
    {
        public string Id=Guid.NewGuid().ToString("D"), Kind, Index;
        public PlatformMount Clone(){return new PlatformMount{Id=Id,Kind=Kind,Index=Index};}
    }
    public sealed class ConfigMember
    {
        public string Id = Guid.NewGuid().ToString("D");
        public ConfigMember Clone() { return new ConfigMember { Id = Id }; }
    }
    public sealed class WeaponRole
    {
        public string Id = Guid.NewGuid().ToString("D"), EntryId, Quantity, Priority;
        public List<string> OperatorIds = new List<string>();
        // null = unknown; explicit empty = no replacement candidates.
        public List<string> CandidateRoleIds;
        public WeaponRole Clone() { return new WeaponRole { Id=Id, EntryId=EntryId, Quantity=Quantity, Priority=Priority, OperatorIds=new List<string>(OperatorIds), CandidateRoleIds=CandidateRoleIds==null?null:new List<string>(CandidateRoleIds) }; }
    }
    public sealed class StockOwner
    {
        public string EntryId, AmmoId, MemberId, Quantity;
        // An explicit null MemberId assigns stock to this configuration's entry/channel.
        public StockOwner Clone() { return new StockOwner { EntryId=EntryId, AmmoId=AmmoId, MemberId=MemberId, Quantity=Quantity }; }
    }
    public sealed class AbilitySource
    {
        public string Tag, EntryId;
        public AbilitySource Clone() { return new AbilitySource { Tag=Tag, EntryId=EntryId }; }
    }
    public sealed class ConfigurationRelations
    {
        public string Name, MemberCount, Specialization;
        public List<ConfigMember> Members = new List<ConfigMember>();
        public List<WeaponRole> Roles = new List<WeaponRole>();
        public List<StockOwner> StockOwners;
        public List<AbilitySource> Abilities = new List<AbilitySource>();
        public ConfigurationRelations Clone() { return new ConfigurationRelations { Name=Name,MemberCount=MemberCount,Specialization=Specialization,Members=Members.Select(x=>x.Clone()).ToList(),Roles=Roles.Select(x=>x.Clone()).ToList(),StockOwners=StockOwners==null?null:StockOwners.Select(x=>x.Clone()).ToList(),Abilities=Abilities.Select(x=>x.Clone()).ToList() }; }
        public ConfigurationRelations Copy(IDictionary<string,string> entries)
        {
            var c=Clone();var ids=Members.Select(x=>x.Id).Concat(Roles.Select(x=>x.Id)).ToDictionary(x=>x,x=>Guid.NewGuid().ToString("D"),StringComparer.OrdinalIgnoreCase);
            Func<string,string> remap=x=>x!=null&&ids.ContainsKey(x)?ids[x]:x;
            foreach(var m in c.Members)m.Id=remap(m.Id);
            foreach(var r in c.Roles){r.Id=remap(r.Id);if(r.EntryId!=null&&entries.ContainsKey(r.EntryId))r.EntryId=entries[r.EntryId];r.OperatorIds=r.OperatorIds.Select(remap).ToList();if(r.CandidateRoleIds!=null)r.CandidateRoleIds=r.CandidateRoleIds.Select(remap).ToList();}
            if(c.StockOwners!=null)foreach(var s in c.StockOwners){if(s.EntryId!=null&&entries.ContainsKey(s.EntryId))s.EntryId=entries[s.EntryId];s.MemberId=remap(s.MemberId);}
            foreach(var a in c.Abilities)if(a.EntryId!=null&&entries.ContainsKey(a.EntryId))a.EntryId=entries[a.EntryId];return c;
        }
    }
}
