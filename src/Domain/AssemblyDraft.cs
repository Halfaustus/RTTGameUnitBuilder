using System;
using System.Collections.Generic;
using System.Linq;
namespace RTTUnitEditor.Domain
{
    public sealed class WeaponEntry
    {
        public string Id = Guid.NewGuid().ToString("D");
        public string WeaponId, Quantity, MountKind, MountIndex, InstallationId;
        public List<string> AmmoOrder;
        // Actual total stock for this row, not an extra ready-magazine allowance.
        public Dictionary<string, string> Inventory = new Dictionary<string, string>();
        public WeaponEntry Clone() { return new WeaponEntry { Id = Id, WeaponId = WeaponId, Quantity = Quantity, MountKind = MountKind, MountIndex = MountIndex, Inventory = new Dictionary<string, string>(Inventory), InstallationId=InstallationId, AmmoOrder=AmmoOrder==null?null:new List<string>(AmmoOrder) }; }
    }
    public sealed class LoadoutDraft
    {
        public string Id = Guid.NewGuid().ToString("D"), Name, Baseline = CombatRules.Baseline;
        public string SourceNote = "工具新建，仅供测试", CopiedFromId;
        public bool TestOnly = true;
        public List<WeaponEntry> Entries = new List<WeaponEntry>();
        public LoadoutDraft Clone() { return new LoadoutDraft { Id = Id, Name = Name, Baseline = Baseline, SourceNote = SourceNote, CopiedFromId = CopiedFromId, TestOnly = TestOnly, Entries = Entries.Select(e => e.Clone()).ToList() }; }
        public LoadoutDraft Copy(string name) { var c = Clone(); c.Id = Guid.NewGuid().ToString("D"); c.Name = name; c.CopiedFromId = Id; foreach (var e in c.Entries) e.Id = Guid.NewGuid().ToString("D"); return c; }
    }
    public sealed class UnitBinding
    {
        public string Id = Guid.NewGuid().ToString("D"), BodyId, LoadoutId, Category;
        public string ValuePoints, DeploymentPoints, MaximumOnField, Icon, SupplyWeight;
        public string Baseline = CombatRules.Baseline, SourceNote = "工具新建，仅供测试", CopiedFromId;
        public bool TestOnly = true;
        public ConfigurationRelations Relations = new ConfigurationRelations();
        public List<string> AssociatedBindingIds = new List<string>();
        public UnitBinding Clone() { return new UnitBinding { Id = Id, BodyId = BodyId, LoadoutId = LoadoutId, Category = Category, ValuePoints = ValuePoints, DeploymentPoints = DeploymentPoints, MaximumOnField = MaximumOnField, Icon = Icon, SupplyWeight = SupplyWeight, Baseline = Baseline, SourceNote = SourceNote, CopiedFromId = CopiedFromId, TestOnly = TestOnly, AssociatedBindingIds = new List<string>(AssociatedBindingIds), Relations=Relations.Clone() }; }
        public UnitBinding Copy() { var c = Clone(); c.Id = Guid.NewGuid().ToString("D"); c.CopiedFromId = Id; c.Relations=Relations.Copy(new Dictionary<string,string>()); return c; }
    }
}
