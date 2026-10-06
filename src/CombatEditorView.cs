using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RTTUnitEditor.Domain;
using RTTUnitEditor.Editing;
namespace RTTUnitEditor.UI {
 public sealed class CombatEditorView : UserControl {
  readonly EditorSession session; readonly EditorDialogs dialogs; readonly string kind; readonly Func<bool,bool> saveFile;
  readonly CheckedListBox allowedAmmo=new CheckedListBox();
  readonly ListBox list=new ListBox(); readonly TableLayoutPanel fields=new TableLayoutPanel(); readonly Panel scroll=new Panel();
  readonly Dictionary<string,Control> inputs=new Dictionary<string,Control>(); readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
  readonly Label status=new Label(), fileStatus=new Label(), derived=new Label(), impact=new Label();
  readonly ErrorProvider errors=new ErrorProvider(); readonly ToolTip tips=new ToolTip();
  CombatDraft draft; bool loading, localDirty;
  public event EventHandler ContextChanged;
  public string Caption { get { return draft==null?(kind=="weapon"?"武器":"共享弹药"):draft.Name+" · 仅供测试"+(session.Dirty?" *":""); } }
  List<CombatDraft> Items { get { return kind=="weapon"?session.Weapons:session.Ammo; } }
  public CombatEditorView(EditorSession s,EditorDialogs d,string type,Func<bool,bool> save) {
   session=s;dialogs=d;kind=type;saveFile=save;Name=type=="weapon"?"WeaponEditor":"AmmoEditor";Dock=DockStyle.Fill;BackColor=Color.White;
   errors.ContainerControl=this;errors.BlinkStyle=ErrorBlinkStyle.NeverBlink;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};
   layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,Margin=Padding.Empty};
   AddAction(actions,"New","新建",()=>{if(!CanLeave(true))return;draft=session.CreateCombat(kind).Clone();localDirty=false;RefreshView();});
   AddAction(actions,"Copy","复制",()=>{string id=draft==null?null:draft.Id;if(id==null||!CanLeave(true))return;var original=Items.FirstOrDefault(x=>x.Id==id);if(original==null)return;draft=session.CopyCombat(original).Clone();localDirty=false;RefreshView();});
   AddAction(actions,"Apply","应用修改",()=>Commit());
   AddAction(actions,"Cancel","取消修改",()=>{if(draft==null)return;var original=Items.FirstOrDefault(x=>x.Id==draft.Id);draft=original==null?null:original.Clone();localDirty=false;session.RecalculateDirty();RefreshView();});
   AddAction(actions,"Delete","删除",()=>{if(draft==null)return;try{if(kind=="ammo"&&session.AffectedWeapons(draft.Id).Count!=0){session.DeleteCombat(draft);return;}if(!dialogs.ConfirmAction(this,"删除「"+draft.Name+"」？"))return;session.DeleteCombat(draft);draft=null;localDirty=false;RefreshView();}catch(Exception ex){dialogs.Error(this,ex.Message);}});
   AddAction(actions,"Save","保存",()=>Save(false));AddAction(actions,"SaveAs","另存为",()=>Save(true));
   AddAction(actions,"Open","打开",()=>Replace(false));AddAction(actions,"Reload","重新加载",()=>Replace(true));
   layout.Controls.Add(actions,0,0);fileStatus.AutoSize=true;fileStatus.Dock=DockStyle.Top;layout.Controls.Add(fileStatus,0,1);
   var work=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};work.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168));work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   list.Name=kind+"List";list.Dock=DockStyle.Fill;list.IntegralHeight=false;list.HorizontalScrollbar=true;list.Margin=new Padding(0,0,10,0);work.Controls.Add(list,0,0);
   list.SelectedIndexChanged+=delegate{if(loading||list.SelectedIndex<0)return;string id=Items[list.SelectedIndex].Id;if(!CanLeave(true)){RefreshList();return;}draft=Items.FirstOrDefault(x=>x.Id==id);if(draft!=null)draft=draft.Clone();localDirty=false;RefreshView();};
   scroll.AutoScroll=true;scroll.Dock=DockStyle.Fill;fields.AutoSize=true;fields.AutoSizeMode=AutoSizeMode.GrowAndShrink;fields.Dock=DockStyle.Top;fields.ColumnCount=2;fields.Padding=new Padding(0,0,24,0);fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(fields);work.Controls.Add(scroll,1,0);layout.Controls.Add(work,0,2);
   status.AutoSize=true;status.Dock=DockStyle.Top;status.MaximumSize=new Size(200,0);layout.SizeChanged+=delegate{status.MaximumSize=new Size(Math.Max(100,layout.ClientSize.Width),0);};scroll.SizeChanged+=delegate{foreach(var label in inputs.Values.OfType<Label>())label.MaximumSize=new Size(Math.Max(80,scroll.ClientSize.Width-169),0);};layout.Controls.Add(status,0,3);
   TextField("name","名称");TextField("id","稳定ID",true);
   foreach(var pair in new[]{new[]{"faction","阵营"},new[]{"caliber","口径"}})TextField(pair[0],pair[1]);
   if(kind=="weapon") {
    Choice("slotRule","槽位／安装需求",new[]{null,"primary","secondary","both","operators_primary","emplaced_artillery","vehicle_slot","cannon_main","launcher_secondary"},new[]{"未配置（禁挂载）","一人主槽","一人副槽","一人主＋副槽","操作人数个主槽","架设火炮主槽","车载武器槽","机炮炮塔主槽","一次性火箭弹副槽"});
    TextField("specialEquipmentWeight","额外特殊装备 t");
    TextField("weight","重量 t");TextField("operators","最低操作人数");TextField("minRange","最小射程 m");TextField("maxRange","最大射程 m");TextField("spread","基础散布半径 m");
    Checks("tags","武器标签",CombatDraft.WeaponTags,new[]{"房屋内可开火","移动射击","消音","机械装填","攻顶"});
    TextField("movePenalty","移动散布惩罚倍率");TextField("aimMin","瞄准时间下限 s");TextField("aimMax","瞄准时间上限 s");
    TextField("rpm","有效射速 rpm");TextField("actualInterval","实际弹药间隔 s");Choice("consumption","每抽象弹丸消耗",new[]{null,"1","3"},new[]{"未配置","单发独立（1）","三发合一（3）"});
    Row("derived","游戏弹丸间隔",derived);TextField("capacity","待发／弹匣容量 发");
    Choice("reloadRule","装填适用性",new[]{null,"continuous","per_round"},new[]{"未配置","连射换弹（固定规则）","逐发／独立准备时间"});TextField("reload","基础装填时间 s");TextField("ignoreReduction","减伤无视 %");
    Checks("targets","允许攻击目标",CombatDraft.Targets,new[]{"步兵","地面载具","直升机","固定翼飞机","掩体工事"});
   } else {
    TextField("specialNote","特殊弹药声明");Choice("damageType","伤害类型",new[]{null,"kinetic","chemical"},new[]{"未配置","动能","化学能"});
    Choice("category","弹药类别",new[]{null,"AP","HE","HEAT"},new[]{"未配置","动能弹（AP）","高爆反人员（HE）","反甲化学能（HEAT）"});
    TextField("damage","标伤 D₀");TextField("penetration","穿深／破深 P₀/Pc");TextField("anchorRange","距离锚点 R m");TextField("anchorPenetration","锚点穿深 P_R");Row("derived","衰减系数 k",derived);
    TextField("speed","弹药初速 km/h");TextField("blast","爆炸半径 m");TextField("suppression","压制额度");TextField("moduleDamage","模块破坏值");TextField("supplyCost","补给费用");TextField("missingCost","缺弹费用");Row("impact","受影响配装",impact);
   }
   var rule=new Label{AutoSize=true,Dock=DockStyle.Top,MaximumSize=new Size(460,0),Text="制导：留空（待定）。弹药与武器仅在分配页组装；库存归配装；不另加待发库存。固定规则只读；不计算飞行或局内伤害。"};Row("rules","适用边界",rule);
   RefreshView();
  }

  void AddAction(FlowLayoutPanel p,string name,string text,Action action){var b=new Button{Name=kind+name,Text=text,AutoSize=true,Margin=new Padding(0,0,4,4)};b.Click+=delegate{action();};p.Controls.Add(b);}
  void Row(string key,string title,Control c){if(c.Name.Length==0)c.Name=kind+"Field_"+key;if(c is Label){c.AutoSize=true;((Label)c).MaximumSize=new Size(200,0);}int r=fields.RowCount++;var l=new Label{Text=title,AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,4,8,6)};c.Dock=DockStyle.Top;c.Margin=new Padding(0,3,0,6);fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));fields.Controls.Add(l,0,r);fields.Controls.Add(c,1,r);inputs[key]=c;labels[key]=l;}
  void TextField(string key,string title,bool readOnly=false){var t=new TextBox{Name=kind+"Field_"+key,ReadOnly=readOnly};Row(key,title,t);t.TextChanged+=delegate{if(loading||draft==null||t.ReadOnly)return;if(key=="name")draft.Name=t.Text;else draft.Fields[key]=t.Text.Length==0?null:t.Text;Changed();if(key=="rpm")RefreshView();};}
  void Choice(string key,string title,string[] values,string[] texts){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name=kind+"Field_"+key};c.Items.AddRange(texts);Row(key,title,c);c.SelectedIndexChanged+=delegate{if(loading||draft==null||c.SelectedIndex<0)return;var next=draft.Clone();next.Fields[key]=values[c.SelectedIndex];
    var cleared=new List<string>();
    if(key=="damageType"&&next.Fields[key]!="kinetic"){Clear(next,"anchorRange",cleared);Clear(next,"anchorPenetration",cleared);}
    if(key=="category"){
     if(next.Fields[key]=="AP")next.Fields["damageType"]="kinetic";
     if(next.Fields[key]=="HE"||next.Fields[key]=="HEAT"){next.Fields["damageType"]="chemical";Clear(next,"anchorRange",cleared);Clear(next,"anchorPenetration",cleared);}
     if(next.Fields[key]=="HE")Fixed(next,"moduleDamage","0",cleared);
     if(next.Fields[key]=="HEAT"){Fixed(next,"blast","0",cleared);Fixed(next,"suppression","0",cleared);}
    }
    if(key=="slotRule"&&next.Fields[key]=="emplaced_artillery")Fixed(next,"specialEquipmentWeight","0.1",cleared);
    if(key=="reloadRule"&&next.Fields[key]=="continuous")Fixed(next,"reload",next.Tags.Contains("mechanical_loading")?"4":"3",cleared);
    if(cleared.Count!=0&&!dialogs.ConfirmAction(this,"改变适用性将清除／替换以下已有值："+string.Join("、",cleared)+"。继续？")){RefreshView();return;}
    draft=next;Changed();RefreshView();
   };tips.SetToolTip(c,"类别与装填适用性只表达既有规则，空白不推定为默认；不按型号名称套用性能。");}
  static void Clear(CombatDraft d,string k,List<string> changes){if(d.Fields[k]!=null){changes.Add(k);d.Fields[k]=null;}}
  static void Fixed(CombatDraft d,string k,string value,List<string> changes){if(d.Fields[k]!=null&&d.Fields[k]!=value)changes.Add(k);d.Fields[k]=value;}
  void Checks(string key,string title,string[] values,string[] texts){var p=new FlowLayoutPanel{AutoSize=true};for(int i=0;i<values.Length;i++){string value=values[i];var c=new CheckBox{Text=texts[i],Tag=value,AutoSize=true};p.Controls.Add(c);c.CheckedChanged+=delegate{if(loading||draft==null)return;var next=draft.Clone();var target=key=="tags"?next.Tags:next.TargetTypes;if(c.Checked)target.Add(value);else target.Remove(value);var changes=new List<string>();if(key=="tags"&&value=="move_fire"&&!c.Checked)Clear(next,"movePenalty",changes);if(key=="tags"&&value=="mechanical_loading"&&next.Fields["reloadRule"]=="continuous")Fixed(next,"reload",c.Checked?"4":"3",changes);if(changes.Count!=0&&!dialogs.ConfirmAction(this,"改变标签将清除／替换："+string.Join("、",changes)+"。继续？")){RefreshView();return;}draft=next;Changed();RefreshView();};}Row(key,title,p);}
  void Changed(){localDirty=true;session.MarkChanged();ValidateView();fileStatus.Text=FileCaption();if(ContextChanged!=null)ContextChanged(this,EventArgs.Empty);}
  string FileCaption(){return(session.FilePath==null?"未保存到文件":System.IO.Path.GetFileName(session.FilePath))+(session.Dirty?" · 未保存 *":"")+(localDirty?" · 编辑尚未应用":"");}
  void RefreshList(){loading=true;list.Items.Clear();foreach(var d in Items)list.Items.Add(d.Name+" · 测试");list.SelectedIndex=draft==null?-1:Items.FindIndex(d=>d.Id==draft.Id);loading=false;}
  public void Synchronize(){if(draft!=null){var d=Items.FirstOrDefault(x=>x.Id==draft.Id);draft=d==null?null:d.Clone();}localDirty=false;RefreshView();}
  sealed class AmmoChoice{public string Id,Name;public override string ToString(){return Name;}}
  void RefreshView(){loading=true;allowedAmmo.Items.Clear();if(kind=="weapon")foreach(var a in session.Ammo)allowedAmmo.Items.Add(new AmmoChoice{Id=a.Id,Name=a.Name},draft!=null&&draft.AmmoIds.Any(id=>AssemblyRules.Same(id,a.Id)));scroll.Enabled=draft!=null;foreach(var p in inputs){var t=p.Value as TextBox;if(t!=null){t.Text=draft==null?"":p.Key=="name"?draft.Name:p.Key=="id"?draft.Id:draft.Fields[p.Key]??"";t.ReadOnly=p.Key=="id";}}
   if(draft!=null){
    foreach(var k in new[]{"consumption","reloadRule","damageType","category","slotRule"})if(inputs.ContainsKey(k)){string[] vals=k=="slotRule"?new[]{null,"primary","secondary","both","operators_primary","emplaced_artillery","vehicle_slot","cannon_main","launcher_secondary"}:k=="consumption"?new[]{null,"1","3"}:k=="reloadRule"?new[]{null,"continuous","per_round"}:k=="damageType"?new[]{null,"kinetic","chemical"}:new[]{null,"AP","HE","HEAT"};((ComboBox)inputs[k]).SelectedIndex=Array.IndexOf(vals,draft.Fields[k]);}
    foreach(var k in new[]{"tags","targets"})if(inputs.ContainsKey(k))foreach(CheckBox c in inputs[k].Controls)c.Checked=(k=="tags"?draft.Tags:draft.TargetTypes).Contains((string)c.Tag);
    if(kind=="weapon"){
     ((TextBox)inputs["specialEquipmentWeight"]).ReadOnly=draft.Fields["slotRule"]=="emplaced_artillery";
     ((TextBox)inputs["actualInterval"]).ReadOnly=draft.Fields["rpm"]!=null && draft.Fields["actualInterval"]==null; tips.SetToolTip(inputs["actualInterval"],draft.Fields["rpm"]==null?"无rpm时可填写独立实际间隔；不替代装填时间。":"实际间隔固定为60 / "+draft.Fields["rpm"]+" 秒（精确关系）。");
     inputs["movePenalty"].Enabled=draft.Tags.Contains("move_fire");labels["movePenalty"].Text=draft.Tags.Contains("move_fire")?"移动散布惩罚倍率":"移动惩罚（不适用）";
     ((TextBox)inputs["reload"]).ReadOnly=draft.Fields["reloadRule"]=="continuous";
    }else{
     bool kinetic=draft.Fields["damageType"]=="kinetic";foreach(var k in new[]{"anchorRange","anchorPenetration"})inputs[k].Enabled=kinetic;
     ((TextBox)inputs["moduleDamage"]).ReadOnly=draft.Fields["category"]=="HE";
     ((TextBox)inputs["blast"]).ReadOnly=((TextBox)inputs["suppression"]).ReadOnly=draft.Fields["category"]=="HEAT";
    }
   }
   loading=false;RefreshList();ValidateView();fileStatus.Text=FileCaption();if(ContextChanged!=null)ContextChanged(this,EventArgs.Empty);
  }
  void ValidateView(){errors.Clear();if(draft==null){status.Text="空列表；从新建开始。";derived.Text=impact.Text="";return;}var e=CombatRules.Validate(draft);foreach(var p in e)if(inputs.ContainsKey(p.Key))errors.SetError(inputs[p.Key],p.Value);
   var ws=session.Weapons.Select(x=>x.Id==draft.Id?draft:x).ToList();var am=session.Ammo.Select(x=>x.Id==draft.Id?draft:x).ToList();var cross=CombatRules.ValidateAll(ws,am);
   status.ForeColor=cross.Count==0?Color.FromArgb(90,105,122):Color.Firebrick;status.Text=(cross.Count==0?"未配置 "+draft.Fields.Count(p=>p.Value==null)+" 项；":"存在 "+cross.Count+" 项错误；请查看字段提示，保存／应用时显示对象与字段详情。")+"\r\n"+CombatRules.Unchecked;
   derived.Text=kind=="weapon"?draft.DerivedInterval():draft.DerivedK();
   if(kind=="ammo"){impact.Text=session.DescribeCombatImpact(draft);impact.MaximumSize=new Size(Math.Max(80,scroll.ClientSize.Width-169),0);impact.AutoSize=true;}
   tips.SetToolTip(status, session.DescribeCombatImpact(draft));
  }
  public bool Commit(){if(!localDirty||draft==null)return true;try{bool ok=session.ApplyCombat(draft,affected=>dialogs.ConfirmImpact(this,"定义「"+draft.Name+"」修改影响：\r\n"+session.DescribeCombatImpact(draft)));if(ok){localDirty=false;RefreshView();}return ok;}catch(Exception ex){dialogs.Error(this,"修改未应用，原数据保留。\r\n"+ex.Message);return false;}}  public bool Save(bool choosePath){if(!Commit())return false;bool ok=saveFile(choosePath);RefreshView();return ok;}
  public bool CanLeave(bool keepDraft){if(keepDraft)return Commit();if(!session.Dirty&&!localDirty)return true;var choice=dialogs.ConfirmLeave(this,keepDraft);if(choice==LeaveChoice.Cancel)return false;if(choice==LeaveChoice.Save)return Save(false);session.AllowLeave(LeaveChoice.Continue,()=>false);Synchronize();return true;}
  void Replace(bool reload){string path=reload?session.FilePath:dialogs.ChooseOpen(this,false);if(path==null||!CanLeave(false))return;try{session.Open(path);draft=Items.FirstOrDefault();if(draft!=null)draft=draft.Clone();localDirty=false;RefreshView();}catch(Exception ex){dialogs.Error(this,"文件未加载，当前数据保留。\r\n"+ex.Message);}}
  protected override void Dispose(bool disposing){if(disposing){errors.Dispose();tips.Dispose();}base.Dispose(disposing);}
 }
}
