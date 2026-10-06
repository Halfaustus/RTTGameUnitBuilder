using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using RTTUnitEditor.Domain;
using RTTUnitEditor.Editing;
namespace RTTUnitEditor.UI
{
    public sealed class BindingEditorView : EditorWorkspace
    {
        readonly ListBox units=new ListBox(), configs=new ListBox();
        readonly ComboBox loadout=new ComboBox(), category=new ComboBox();
        readonly TableLayoutPanel fields=new TableLayoutPanel();
        readonly Dictionary<string,TextBox> inputs=new Dictionary<string,TextBox>();
        readonly CheckedListBox associated=new CheckedListBox();readonly Label summary=new Label(), identity=new Label(), weaponNames=new Label();
        UnitBinding draft;string unitId;
        public UnitBinding Current {get{return draft;}}
        public override string Caption {get{var body=Session.Bodies.FirstOrDefault(b=>b.Id==unitId);return body==null?"单位配置":body.Name+" · 配置管理";}}
        public BindingEditorView(EditorSession s,EditorDialogs d,Func<bool,bool> save):base(s,d,save)
        {
            Name="BindingEditor";
            Button(Toolbar,"bindingNew","绑定配装",()=>{if(unitId==null)throw new InvalidOperationException("先选择单位");if(!Commit())return;draft=Session.CreateBinding(unitId).Clone();LocalDirty=false;Render();});
            Button(Toolbar,"bindingCopy","复制为新配置",()=>{if(draft==null||!Commit())return;draft=Session.CopyBinding(Session.Bindings.First(x=>x.Id==draft.Id)).Clone();LocalDirty=false;Render();});
            Button(Toolbar,"bindingApply","应用",()=>Commit());Button(Toolbar,"bindingCancel","取消修改",Synchronize);
            Button(Toolbar,"bindingDelete","解除绑定",()=>{if(draft!=null&&Dialogs.ConfirmAction(this,"解除此单位配置的绑定？配装及共享定义保留。")){Session.DeleteBinding(draft.Id);draft=null;Render();}});FileActions("binding");
            var work=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));PageLayout.Controls.Add(work,0,1);
            var navigation=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};navigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));navigation.RowStyles.Add(new RowStyle(SizeType.Percent,45));navigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));navigation.RowStyles.Add(new RowStyle(SizeType.Percent,55));work.Controls.Add(navigation,0,0);
            navigation.Controls.Add(new Label{Text="单位基体",AutoSize=true,Padding=new Padding(4)},0,0);navigation.Controls.Add(new Label{Text="单位配置",AutoSize=true,Padding=new Padding(4)},0,2);
            units.Name="bindingUnits";units.Dock=DockStyle.Fill;units.IntegralHeight=false;units.HorizontalScrollbar=true;configs.Name="unitConfigurations";configs.Dock=DockStyle.Fill;configs.IntegralHeight=false;configs.HorizontalScrollbar=true;navigation.Controls.Add(units,0,1);navigation.Controls.Add(configs,0,3);
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(8)};work.Controls.Add(scroll,1,0);fields.AutoSize=true;fields.AutoSizeMode=AutoSizeMode.GrowAndShrink;fields.Dock=DockStyle.Top;fields.ColumnCount=2;fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(fields);
            identity.Name="bindingIdentity";identity.AutoSize=true;identity.MaximumSize=new Size(200,0);Row("单位基体",identity);weaponNames.Name="bindingWeaponNames";weaponNames.AutoSize=true;weaponNames.MaximumSize=new Size(200,0);Row("武器名称",weaponNames);loadout.DropDown+=delegate{FitDropDown(loadout);};
            loadout.Name="bindingLoadout";loadout.DropDownStyle=ComboBoxStyle.DropDownList;Row("配装",loadout);category.Name="bindingCategory";category.DropDownStyle=ComboBoxStyle.DropDownList;category.Items.AddRange(new[]{"未配置","侦查","步兵","装甲","支援"});Row("编组类别",category);
            TextField("configName","配置名称");TextField("memberCount","配置满编人数");TextField("specialization","配置专精");
            TextField("valuePoints","价值分");TextField("deploymentPoints","出动分");TextField("maximumOnField","最大在场数");TextField("icon","识别图案");TextField("supplyWeight","已有补给 t");
            summary.Name="bindingSummary";summary.AutoSize=true;summary.MaximumSize=new Size(260,0);Row("检查结果",summary);scroll.SizeChanged+=delegate{foreach(var label in new[]{summary,identity,weaponNames})label.MaximumSize=new Size(Math.Max(80,scroll.ClientSize.Width-130),0);};
            var relationButton=new Button{Name="bindingRelations",Text="编辑成员／岗位／库存归属／能力来源",AutoSize=true};Row("配置关系",relationButton);relationButton.Click+=delegate{if(draft==null)return;using(var editor=new ConfigurationRelationEditor(draft,Session))if(editor.ShowDialog(this)==DialogResult.OK){draft=Session.Bindings.First(x=>x.Id==draft.Id).Clone();LocalDirty=false;Render();}};
            associated.Name="bindingAssociations";associated.Height=90;associated.IntegralHeight=false;associated.CheckOnClick=true;Row("运输关联",associated);
            units.SelectedIndexChanged+=delegate{if(Loading||units.SelectedItem==null)return;string id=((Choice)units.SelectedItem).Id;if(id==unitId)return;if(!Commit()){Render();return;}unitId=id;draft=Session.Bindings.FirstOrDefault(b=>b.BodyId==id);if(draft!=null)draft=draft.Clone();Render();};
            configs.SelectedIndexChanged+=delegate{if(Loading||configs.SelectedItem==null)return;string id=((Choice)configs.SelectedItem).Id;if(draft!=null&&draft.Id==id)return;if(!Commit()){Render();return;}draft=Session.Bindings.First(x=>x.Id==id).Clone();Render();};
            loadout.SelectedIndexChanged+=delegate{if(Loading||draft==null)return;draft.LoadoutId=loadout.SelectedItem==null?null:((Choice)loadout.SelectedItem).Id;Changed();};
            category.SelectedIndexChanged+=delegate{if(Loading||draft==null)return;draft.Category=category.SelectedIndex<=0?null:AssemblyRules.Categories[category.SelectedIndex-1];Changed();};
            associated.ItemCheck+=delegate(object sender,ItemCheckEventArgs e){if(Loading||draft==null)return;string id=((Choice)associated.Items[e.Index]).Id;if(e.NewValue==CheckState.Checked)draft.AssociatedBindingIds.Add(id);else draft.AssociatedBindingIds.RemoveAll(x=>x==id);Changed();};
            Render();
        }
        void Row(string text,Control control){int i=fields.RowCount++;fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));fields.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(3,7,3,4)},0,i);control.Dock=DockStyle.Top;control.Margin=new Padding(3,3,5,5);fields.Controls.Add(control,1,i);}
        void TextField(string key,string title){var t=new TextBox{Name="binding_"+key};inputs[key]=t;Row(title,t);t.TextChanged+=delegate{if(Loading||draft==null)return;string value=t.Text.Length==0?null:t.Text;if(key=="configName")draft.Relations.Name=value;else if(key=="memberCount")draft.Relations.MemberCount=value;else if(key=="specialization")draft.Relations.Specialization=value;else if(key=="valuePoints")draft.ValuePoints=value;else if(key=="deploymentPoints")draft.DeploymentPoints=value;else if(key=="maximumOnField")draft.MaximumOnField=value;else if(key=="icon")draft.Icon=value;else draft.SupplyWeight=value;Changed();};}
        void ShowRow(Control control,bool visible){int row=fields.GetPositionFromControl(control).Row;foreach(Control c in fields.Controls)if(fields.GetPositionFromControl(c).Row==row)c.Visible=visible;fields.RowStyles[row].SizeType=visible?SizeType.AutoSize:SizeType.Absolute;fields.RowStyles[row].Height=0;}
        public override void Synchronize(){if(unitId!=null&&!Session.Bodies.Any(b=>b.Id==unitId)){unitId=null;draft=null;}if(unitId==null&&Session.Bodies.Count>0)unitId=Session.Bodies[0].Id;if(draft!=null){var b=Session.Bindings.FirstOrDefault(x=>x.Id==draft.Id);draft=b==null?null:b.Clone();}if(draft==null){var b=Session.Bindings.FirstOrDefault(x=>x.BodyId==unitId);draft=b==null?null:b.Clone();}LocalDirty=false;Session.RecalculateDirty();Render();}
        void Render()
        {
            Loading=true;units.Items.Clear();foreach(var b in Session.Bodies)units.Items.Add(new Choice{Id=b.Id,Text=b.Name});units.SelectedIndex=Session.Bodies.FindIndex(b=>b.Id==unitId);configs.Items.Clear();foreach(var b in Session.Bindings.Where(x=>x.BodyId==unitId)){var c=Session.Loadouts.FirstOrDefault(x=>x.Id==b.LoadoutId);configs.Items.Add(new Choice{Id=b.Id,Text=b.Relations.Name??(c==null?"未选择配装":c.Name)});}for(int i=0;i<configs.Items.Count;i++)if(draft!=null&&((Choice)configs.Items[i]).Id==draft.Id)configs.SelectedIndex=i;
            loadout.Items.Clear();loadout.Items.Add(new Choice{Id=null,Text="未选择"});foreach(var c in Session.Loadouts.Where(c=>draft!=null&&c.Id==draft.LoadoutId||!Session.Bindings.Any(b=>b.Id!=(draft==null?null:draft.Id)&&b.LoadoutId==c.Id)))loadout.Items.Add(new Choice{Id=c.Id,Text=c.Name});loadout.SelectedIndex=0;if(draft!=null)for(int i=1;i<loadout.Items.Count;i++)if(((Choice)loadout.Items[i]).Id==draft.LoadoutId)loadout.SelectedIndex=i;category.SelectedIndex=draft==null||draft.Category==null?0:Array.IndexOf(AssemblyRules.Categories,draft.Category)+1;
            foreach(var pair in inputs)pair.Value.Text=draft==null?"":pair.Key=="configName"?draft.Relations.Name:pair.Key=="memberCount"?draft.Relations.MemberCount:pair.Key=="specialization"?draft.Relations.Specialization:pair.Key=="valuePoints"?draft.ValuePoints:pair.Key=="deploymentPoints"?draft.DeploymentPoints:pair.Key=="maximumOnField"?draft.MaximumOnField:pair.Key=="icon"?draft.Icon:draft.SupplyWeight;
            var body=Session.Bodies.FirstOrDefault(b=>b.Id==unitId);bool infantry=body!=null&&body.UnitType=="infantry";ShowRow(inputs["supplyWeight"],body!=null&&!infantry||draft!=null&&draft.SupplyWeight!=null);associated.Items.Clear();foreach(var b in Session.Bindings.Where(x=>x.Id!=(draft==null?null:draft.Id))){var carrier=Session.Bodies.FirstOrDefault(x=>x.Id==b.BodyId);if(carrier!=null&&carrier.UnitType=="ground_vehicle"||draft!=null&&draft.AssociatedBindingIds.Contains(b.Id))associated.Items.Add(new Choice{Id=b.Id,Text=Session.BindingName(b)},draft!=null&&draft.AssociatedBindingIds.Contains(b.Id));}ShowRow(associated,infantry||draft!=null&&draft.AssociatedBindingIds.Count>0);fields.Enabled=draft!=null;Loading=false;ValidateView();Notify();
        }
        protected override void ValidateView()
        {
            identity.Text=Session.Bodies.Where(x=>x.Id==unitId).Select(x=>x.Name).FirstOrDefault()??"未选择";var selectedLoadout=draft==null?null:Session.Loadouts.FirstOrDefault(x=>x.Id==draft.LoadoutId);weaponNames.Text=selectedLoadout==null?"未选择配装":string.Join("\r\n",selectedLoadout.Entries.Select(e=>Session.Weapons.Where(w=>w.Id==e.WeaponId).Select(w=>w.Name).FirstOrDefault()??"武器引用不存在"));
            if(draft==null){Status.Text=unitId==null?"先在单位页创建单位，再为它绑定配装。":"此单位尚无配置；点击绑定配装。";summary.Text="";return;}
            var next=Session.Bindings.Select(x=>x.Id==draft.Id?draft:x).ToList();var report=AssemblyRules.ValidateBinding(draft,next,Session.Loadouts,Session.Bodies,Session.Weapons,Session.Ammo);Report(report,"");
            var b=Session.Bodies.FirstOrDefault(x=>x.Id==draft.BodyId);var c=Session.Loadouts.FirstOrDefault(x=>x.Id==draft.LoadoutId);if(b==null||c==null){summary.Text="单位或配装尚未选择";return;}var weight=AssemblyRules.TransportWeight(RelationRules.ResolveBody(b,draft),c,Session.Weapons);summary.Text="配置满编生命："+(RelationRules.ResolveBody(b,draft).Fields["maximumHealth"]??"未配置")+"\r\n运输重量："+(weight==null?"未配置":weight.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+" t")+"\r\n配置关系记录成员、岗位及优先级；不运行战场接替。";if(report.Errors.Count>0)summary.Text+="\r\n"+string.Join("\r\n",report.Errors.Take(4));else if(report.Pending.Count>0)summary.Text+="\r\n"+string.Join("\r\n",report.Pending.Take(4));
        }
        public override bool Commit(){if(!LocalDirty||draft==null)return true;try{Session.ApplyBinding(draft);LocalDirty=false;Render();return true;}catch(Exception e){Dialogs.Error(this,e.Message);return false;}}
    }
}
