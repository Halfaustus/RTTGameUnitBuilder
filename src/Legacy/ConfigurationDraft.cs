using System;
using System.Collections.Generic;
using System.Linq;
namespace RTTUnitEditor.Domain {
 public sealed class PositionDraft {
  public string Id, Kind;
  public PositionDraft Clone(){return new PositionDraft{Id=Id,Kind=Kind};}
  public static PositionDraft Create(string kind){return new PositionDraft{Id=Guid.NewGuid().ToString("D"),Kind=kind};}
  public string[] Slots {get{return Kind=="soldier"?new[]{"primary","secondary"}:Kind=="cannon"?new[]{"main","coax"}:Kind=="commander"?new[]{"coax"}:new[]{"hull"};}}
  public string TurnRate {get{return Kind=="cannon"?"120°/s":Kind=="commander"?"360°/s":Kind=="hull"?"随车体":"随人员";}}
 }
 public sealed class AssignmentDraft {
  public string Id, WeaponId, PositionId, Slot, Quantity, Priority;
  public List<string> OperatorIds=new List<string>();
  public Dictionary<string,string> Inventory=new Dictionary<string,string>();
  public AssignmentDraft Clone(){return new AssignmentDraft{Id=Id,WeaponId=WeaponId,PositionId=PositionId,Slot=Slot,Quantity=Quantity,Priority=Priority,OperatorIds=new List<string>(OperatorIds),Inventory=new Dictionary<string,string>(Inventory)};}
 }
 public sealed class ConfigurationDraft {
  public string Id, Name, BodyId, Category, SupplyWeight, SourceNote, CopiedFromId;
  public string Baseline=CombatRules.Baseline;
  public bool TestOnly=true;
  public List<PositionDraft> Positions=new List<PositionDraft>();
  public List<AssignmentDraft> Assignments=new List<AssignmentDraft>();
  public static ConfigurationDraft Create(string name){return new ConfigurationDraft{Id=Guid.NewGuid().ToString("D"),Name=name,SourceNote="工具新建，仅供测试"};}
  public ConfigurationDraft Clone(){return new ConfigurationDraft{Id=Id,Name=Name,BodyId=BodyId,Category=Category,SupplyWeight=SupplyWeight,SourceNote=SourceNote,CopiedFromId=CopiedFromId,Baseline=Baseline,TestOnly=TestOnly,Positions=Positions.Select(p=>p.Clone()).ToList(),Assignments=Assignments.Select(a=>a.Clone()).ToList()};}
  public ConfigurationDraft Copy(string name){var c=Clone();c.Id=Guid.NewGuid().ToString("D");c.Name=name;c.CopiedFromId=Id;c.TestOnly=true;var ids=new Dictionary<string,string>();foreach(var p in c.Positions){ids[p.Id]=Guid.NewGuid().ToString("D");p.Id=ids[p.Id];}foreach(var a in c.Assignments){a.Id=Guid.NewGuid().ToString("D");a.PositionId=ids[a.PositionId];a.OperatorIds=a.OperatorIds.Select(x=>ids[x]).ToList();}return c;}
 }
 public sealed class CardDraft {
  public string Id,Name,ConfigurationId,ValuePoints,DeploymentPoints,MaximumOnField,Icon,SourceNote,CopiedFromId;
  public string Baseline=CombatRules.Baseline;public bool TestOnly=true;
  public List<string> AssociatedCardIds=new List<string>();
  public static CardDraft Create(string name){return new CardDraft{Id=Guid.NewGuid().ToString("D"),Name=name,SourceNote="工具新建，仅供测试"};}
  public CardDraft Clone(){return new CardDraft{Id=Id,Name=Name,ConfigurationId=ConfigurationId,ValuePoints=ValuePoints,DeploymentPoints=DeploymentPoints,MaximumOnField=MaximumOnField,Icon=Icon,SourceNote=SourceNote,CopiedFromId=CopiedFromId,Baseline=Baseline,TestOnly=TestOnly,AssociatedCardIds=new List<string>(AssociatedCardIds)};}
  public CardDraft Copy(string name){var c=Clone();c.Id=Guid.NewGuid().ToString("D");c.Name=name;c.CopiedFromId=Id;c.TestOnly=true;return c;}
 }
 public sealed class ValidationReport {
  public List<string> Errors=new List<string>(), Pending=new List<string>(), Unchecked=new List<string>();
  public bool CanReferenceCard {get{return Errors.Count==0&&Pending.Count==0;}}
  public bool Ready {get{return Errors.Count==0&&Pending.Count==0&&Unchecked.Count==0;}}
 }
}
