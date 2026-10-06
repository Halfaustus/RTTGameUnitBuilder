using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using RTTUnitEditor.Domain;
using RTTUnitEditor.Editing;
namespace RTTUnitEditor.UI
{
    public sealed class ConfigurationRelationEditor : Form
    {
        readonly ConfigurationRelations original;readonly LoadoutDraft originalLoadout;readonly EditorSession session;
        public readonly DataGridView Members, Roles, Stock, Abilities, Equipment, Totals;
        public readonly CheckBox StockKnown=new CheckBox{Text="库存归属已明确",AutoSize=true};
        public ConfigurationRelations Result;
        public ConfigurationRelationEditor(UnitBinding binding,EditorSession session)
        {
            this.session=session;originalLoadout=session.Loadouts.FirstOrDefault(x=>AssemblyRules.Same(x.Id,binding.LoadoutId));if(originalLoadout!=null)originalLoadout=originalLoadout.Clone();original=binding.Relations.Clone();Name="ConfigurationRelations";Text="配置关系";Size=new Size(950,600);MinimumSize=new Size(650,400);
            var tabs=new TabControl{Dock=DockStyle.Fill};Controls.Add(tabs);
            Equipment=Table(tabs,"配装引用",new[]{"武器行ID","配置数量","安装结构ID","择弹顺序（逗号分隔；-表示无）"});Totals=Table(tabs,"库存总量",new[]{"武器行ID","弹药ID","实际总库存"});Equipment.AllowUserToAddRows=Equipment.AllowUserToDeleteRows=Totals.AllowUserToAddRows=Totals.AllowUserToDeleteRows=false;Equipment.Columns[0].ReadOnly=true;Totals.Columns[0].ReadOnly=Totals.Columns[1].ReadOnly=true;if(originalLoadout!=null)foreach(var entry in originalLoadout.Entries){Equipment.Rows.Add(entry.Id,entry.Quantity,entry.InstallationId,entry.AmmoOrder==null?null:entry.AmmoOrder.Count==0?"-":string.Join(",",entry.AmmoOrder));foreach(var pair in entry.Inventory)Totals.Rows.Add(entry.Id,pair.Key,pair.Value);}
            Members=Table(tabs,"成员",new[]{"成员ID"});Roles=Table(tabs,"岗位",new[]{"岗位ID","武器行ID","数量","优先级","操作成员ID（逗号分隔）","候选岗位ID（逗号分隔；-表示无）"});
            Stock=Table(tabs,"库存归属",new[]{"武器行ID","弹药ID","成员ID（空为通道共享）","数量"});Abilities=Table(tabs,"装备能力来源",new[]{"能力标签","已装备武器行ID"});
            foreach(var m in original.Members)Members.Rows.Add(m.Id);
            foreach(var r in original.Roles)Roles.Rows.Add(r.Id,r.EntryId,r.Quantity,r.Priority,string.Join(",",r.OperatorIds),r.CandidateRoleIds==null?null:r.CandidateRoleIds.Count==0?"-":string.Join(",",r.CandidateRoleIds));
            if(original.StockOwners!=null)foreach(var s in original.StockOwners)Stock.Rows.Add(s.EntryId,s.AmmoId,s.MemberId,s.Quantity);
            foreach(var a in original.Abilities)Abilities.Rows.Add(a.Tag,a.EntryId);
            Members.DefaultValuesNeeded+=delegate(object sender,DataGridViewRowEventArgs e){e.Row.Cells[0].Value=Guid.NewGuid().ToString("D");};Roles.DefaultValuesNeeded+=delegate(object sender,DataGridViewRowEventArgs e){e.Row.Cells[0].Value=Guid.NewGuid().ToString("D");};
            var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};Controls.Add(bottom);StockKnown.Checked=original.StockOwners!=null;Stock.Enabled=StockKnown.Checked;StockKnown.CheckedChanged+=delegate{Stock.Enabled=StockKnown.Checked;};bottom.Controls.Add(StockKnown);
            var ok=new Button{Text="应用关系",Name="relationsApply",AutoSize=true};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel};bottom.Controls.Add(ok);bottom.Controls.Add(cancel);CancelButton=cancel;
            var refs=new TextBox{Dock=DockStyle.Bottom,Multiline=true,ReadOnly=true,Height=85,ScrollBars=ScrollBars.Vertical};Controls.Add(refs);
            var loadout=session.Loadouts.FirstOrDefault(x=>AssemblyRules.Same(x.Id,binding.LoadoutId));refs.Text="武器行引用：\r\n"+(loadout==null?"未选配装":string.Join("\r\n",loadout.Entries.Select(e=>e.Id+" = "+(session.Weapons.FirstOrDefault(w=>AssemblyRules.Same(w.Id,e.WeaponId))??new CombatDraft()).Name+"；弹药 "+string.Join(",",e.Inventory.Keys))));
            ok.Click+=delegate{try{var next=binding.Clone();next.Relations=ReadRelations();var equipment=ReadLoadout();if(equipment==null)throw new InvalidOperationException("先为配置选择配装");session.ApplyConfiguration(next,equipment);Result=next.Relations;DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"配置关系",MessageBoxButtons.OK,MessageBoxIcon.Error);}};
        }
        static DataGridView Table(TabControl tabs,string title,string[] columns)
        {
            var tab=new TabPage(title);tabs.TabPages.Add(tab);var grid=new DataGridView{Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AllowUserToAddRows=true,AllowUserToDeleteRows=true};tab.Controls.Add(grid);foreach(var name in columns)grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=name});return grid;
        }
        static IEnumerable<DataGridViewRow> Rows(DataGridView grid){grid.EndEdit();return grid.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow);}
        static string Cell(DataGridViewRow row,int index){var s=Convert.ToString(row.Cells[index].Value);return s.Length==0?null:s;}
        static List<string> List(string text){return text==null?null:text=="-"?new List<string>():text.Split(',').Select(x=>x.Trim()).ToList();}
        public LoadoutDraft ReadLoadout()
        {
            if(originalLoadout==null)return null;var c=originalLoadout.Clone();
            foreach(var row in Rows(Equipment)){var e=c.Entries.First(x=>AssemblyRules.Same(x.Id,Cell(row,0)));e.Quantity=Cell(row,1);e.InstallationId=Cell(row,2);e.AmmoOrder=List(Cell(row,3));if(e.InstallationId!=null){var m=session.Bodies.Where(b=>b.Installations!=null).SelectMany(b=>b.Installations).FirstOrDefault(x=>AssemblyRules.Same(x.Id,e.InstallationId));if(m!=null){e.MountKind=m.Kind;e.MountIndex=m.Index;}}}
            foreach(var row in Rows(Totals)){var e=c.Entries.First(x=>AssemblyRules.Same(x.Id,Cell(row,0)));string key=e.Inventory.Keys.First(x=>AssemblyRules.Same(x,Cell(row,1)));e.Inventory[key]=Cell(row,2);}return c;
        }
        public ConfigurationRelations ReadRelations()
        {
            var c=original.Clone();c.Members=Rows(Members).Select(r=>new ConfigMember{Id=Cell(r,0)}).ToList();c.Roles=Rows(Roles).Select(r=>new WeaponRole{Id=Cell(r,0),EntryId=Cell(r,1),Quantity=Cell(r,2),Priority=Cell(r,3),OperatorIds=List(Cell(r,4))??new List<string>(),CandidateRoleIds=List(Cell(r,5))}).ToList();
            c.StockOwners=StockKnown.Checked?Rows(Stock).Select(r=>new StockOwner{EntryId=Cell(r,0),AmmoId=Cell(r,1),MemberId=Cell(r,2),Quantity=Cell(r,3)}).ToList():null;
            c.Abilities=Rows(Abilities).Select(r=>new AbilitySource{Tag=Cell(r,0),EntryId=Cell(r,1)}).ToList();return c;
        }
    }
    public sealed class PlatformRelationEditor : Form
    {
        public readonly DataGridView Mounts=new DataGridView{Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
        public readonly CheckBox Known=new CheckBox{Text="安装结构已明确（空表表示没有安装位置）",AutoSize=true};
        public List<PlatformMount> Result;
        public PlatformRelationEditor(BodyDraft body,Action<List<PlatformMount>> apply)
        {
            Text="基体安装结构";Size=new Size(720,400);Mounts.Columns.Add("id","安装ID");Mounts.Columns.Add("kind","类型 cannon / commander / hull");Mounts.Columns.Add("index","安装组");Controls.Add(Mounts);if(body.Installations!=null)foreach(var m in body.Installations)Mounts.Rows.Add(m.Id,m.Kind,m.Index);Known.Checked=body.Installations!=null;
            Mounts.DefaultValuesNeeded+=delegate(object sender,DataGridViewRowEventArgs e){e.Row.Cells[0].Value=Guid.NewGuid().ToString("D");};var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};Controls.Add(bar);bar.Controls.Add(Known);var ok=new Button{Text="应用",AutoSize=true};bar.Controls.Add(ok);var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel};bar.Controls.Add(cancel);CancelButton=cancel;
            ok.Click+=delegate{try{Mounts.EndEdit();Result=Known.Checked?Mounts.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow).Select(r=>new PlatformMount{Id=Convert.ToString(r.Cells[0].Value),Kind=Convert.ToString(r.Cells[1].Value),Index=Convert.ToString(r.Cells[2].Value)}).ToList():null;apply(Result);DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"安装结构",MessageBoxButtons.OK,MessageBoxIcon.Error);}};
        }
    }
}
