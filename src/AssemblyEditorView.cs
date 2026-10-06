using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using RTTUnitEditor.Domain;
using RTTUnitEditor.Editing;
namespace RTTUnitEditor.UI
{
    public sealed class AssemblyEditorView : EditorWorkspace
    {
        readonly ListBox list=new ListBox(); readonly TextBox name=new TextBox();
        readonly DataGridView rows,stock; readonly ComboBox ammo=new ComboBox();
        LoadoutDraft draft; string selectedEntry; readonly FlowLayoutPanel board=new FlowLayoutPanel(); List<WeaponEntry> undoMount; string afterMount; Point dragStart; string draggingEntry; readonly string dragOwner=Guid.NewGuid().ToString("D");
        public LoadoutDraft Current {get{return draft;}}
        public override string Caption {get{return draft==null?"武器分配":draft.Name+" · 仅供测试";}}
        public AssemblyEditorView(EditorSession s,EditorDialogs d,Func<bool,bool> save):base(s,d,save)
        {
            Name="AssemblyEditor";
            Button(Toolbar,"loadoutNew","新配装",()=>{if(!Commit())return;undoMount=null;draft=Session.CreateLoadout().Clone();LocalDirty=false;Render();});
            Button(Toolbar,"loadoutCopy","复制",()=>{if(draft==null||!Commit())return;undoMount=null;draft=Session.CopyLoadout(Session.Loadouts.First(x=>x.Id==draft.Id)).Clone();Render();});
            Button(Toolbar,"loadoutApply","应用",()=>Commit());Button(Toolbar,"loadoutCancel","取消修改",Synchronize);
            Button(Toolbar,"loadoutDelete","删除",()=>{if(draft!=null&&Dialogs.ConfirmAction(this,"删除此配装？已绑定单位的配装不能删除。")){Session.DeleteLoadout(draft.Id);draft=null;Render();}});FileActions("loadout");
            var work=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,80));PageLayout.Controls.Add(work,0,1);
            list.Name="loadoutList";list.Dock=DockStyle.Fill;list.IntegralHeight=false;list.HorizontalScrollbar=true;work.Controls.Add(list,0,0);
            var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6};content.RowStyles.Add(new RowStyle(SizeType.AutoSize));content.RowStyles.Add(new RowStyle(SizeType.AutoSize));content.RowStyles.Add(new RowStyle(SizeType.Percent,50));content.RowStyles.Add(new RowStyle(SizeType.AutoSize));content.RowStyles.Add(new RowStyle(SizeType.Percent,50));content.RowStyles.Add(new RowStyle(SizeType.Absolute,0));var contentScroll=new Panel{Name="AssemblyDetails",Dock=DockStyle.Fill,AutoScroll=true};work.Controls.Add(contentScroll,1,0);content.Dock=DockStyle.Top;content.Height=360;contentScroll.Controls.Add(content);contentScroll.SizeChanged+=delegate{content.Height=Math.Max(360,contentScroll.ClientSize.Height-6);};
            var title=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};title.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));title.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));title.Controls.Add(new Label{Text="配装名称",AutoSize=true},0,0);name.Name="loadoutName";name.Dock=DockStyle.Top;title.Controls.Add(name,1,0);content.Controls.Add(title,0,0);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};Button(actions,"entryAdd","＋武器",()=>{if(draft==null)return;var e=new WeaponEntry();draft.Entries.Add(e);selectedEntry=e.Id;Changed();RenderRows();});Button(actions,"entryRemove","移除",()=>{if(draft==null)return;draft.Entries.RemoveAll(e=>e.Id==selectedEntry);selectedEntry=null;Changed();RenderRows();});            var show=new CheckBox{Name="showMounts",Text="安装示意",AutoSize=true};actions.Controls.Add(show);show.CheckedChanged+=delegate{board.Visible=show.Checked;content.RowStyles[5].Height=show.Checked?145:0;};
            Button(actions,"mountUndo","撤销",UndoMount);content.Controls.Add(actions,0,1);
            board.Name="mountBoard";board.Dock=DockStyle.Fill;board.AutoScroll=true;board.Visible=false;content.Controls.Add(board,0,5);
            rows=Grid("weaponEntries");content.Controls.Add(rows,0,2);
            var stockActions=new FlowLayoutPanel{AutoSize=false,Height=38,WrapContents=false,AutoScroll=true,Dock=DockStyle.Top};stockActions.Controls.Add(new Label{Text="携弹",AutoSize=true,Margin=new Padding(0,5,3,0)});ammo.Name="entryAmmo";ammo.DropDownStyle=ComboBoxStyle.DropDownList;ammo.Width=105;stockActions.Controls.Add(ammo);Button(stockActions,"stockAdd","关联",AddAmmo);Button(stockActions,"stockRemove","移除",()=>{var e=Selected();if(e==null||stock.CurrentRow==null)return;var removed=(string)stock.CurrentRow.Tag;e.Inventory.Remove(removed);if(e.AmmoOrder!=null)e.AmmoOrder.RemoveAll(x=>AssemblyRules.Same(x,removed));Changed();RenderStock();});Button(stockActions,"stockOrderConfirm","确认顺序",()=>{var e=Selected();if(e==null)return;e.AmmoOrder=stock.Rows.Cast<DataGridViewRow>().Select(r=>(string)r.Tag).ToList();Changed();RenderStock();});Button(stockActions,"stockUp","上移",()=>MoveAmmo(-1));Button(stockActions,"stockDown","下移",()=>MoveAmmo(1));ammo.Width=100;ammo.DropDown+=delegate{FitDropDown(ammo);};content.Controls.Add(stockActions,0,3);
            stock=Grid("entryInventory");stock.Columns.Add(new DataGridViewTextBoxColumn{Name="ammoName",HeaderText="弹药",ReadOnly=true});stock.Columns.Add(new DataGridViewTextBoxColumn{Name="amount",HeaderText="实际总库存（发／具）"});content.Controls.Add(stock,0,4);
            list.SelectedIndexChanged+=delegate{if(Loading||list.SelectedItem==null)return;string id=((Choice)list.SelectedItem).Id;if(draft!=null&&id==draft.Id)return;if(!Commit()){Render();return;}undoMount=null;draft=Session.Loadouts.First(x=>x.Id==id).Clone();selectedEntry=null;Render();};
            name.TextChanged+=delegate{if(Loading||draft==null)return;draft.Name=name.Text;Changed();};
            rows.CurrentCellDirtyStateChanged+=delegate{if(rows.IsCurrentCellDirty)rows.CommitEdit(DataGridViewDataErrorContexts.Commit);};
                        rows.MouseDown+=delegate(object sender,MouseEventArgs e){dragStart=e.Location;};rows.MouseMove+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left||rows.CurrentRow==null||new Rectangle(dragStart.X-4,dragStart.Y-4,8,8).Contains(e.Location))return;rows.EndEdit();stock.EndEdit();draggingEntry=(string)rows.CurrentRow.Tag;var payload=new MountPayload{Owner=dragOwner,LoadoutId=draft.Id,EntryId=draggingEntry,Signature=MountSignature()};try{rows.DoDragDrop(payload,DragDropEffects.Move);}finally{draggingEntry=null;foreach(Button b in board.Controls)b.BackColor=SystemColors.Control;}};
            rows.QueryContinueDrag+=delegate(object sender,QueryContinueDragEventArgs e){if(e.EscapePressed){e.Action=DragAction.Cancel;draggingEntry=null;}};
            rows.CellValueChanged+=EditRow;rows.SelectionChanged+=delegate{if(Loading)return;selectedEntry=rows.CurrentRow==null?null:(string)rows.CurrentRow.Tag;RenderStock();};
            rows.DataError+=delegate(object sender,DataGridViewDataErrorEventArgs e){e.ThrowException=false;if(!Loading)Status.Text="武器或安装选项无效，请重新选择。";};
            stock.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){if(Loading||e.RowIndex<0||e.ColumnIndex!=1)return;var entry=Selected();if(entry==null)return;string text=Convert.ToString(stock.Rows[e.RowIndex].Cells[1].Value);entry.Inventory[(string)stock.Rows[e.RowIndex].Tag]=text.Length==0?null:text;Changed();};
            Render();
        }
        WeaponEntry Selected(){return draft==null?null:draft.Entries.FirstOrDefault(e=>e.Id==selectedEntry);}
        void AddAmmo(){var e=Selected();var a=ammo.SelectedItem as Choice;if(e==null||a==null)return;if(e.Inventory.Keys.Any(id=>AssemblyRules.Same(id,a.Id)))throw new InvalidOperationException("该弹药已关联，不重复添加");if(e.Inventory.Count==0)e.AmmoOrder=new List<string>();e.Inventory[a.Id]=null;if(e.AmmoOrder!=null)e.AmmoOrder.Add(a.Id);Changed();RenderStock();}
        void MoveAmmo(int delta){var e=Selected();if(e==null||stock.CurrentRow==null)return;if(e.AmmoOrder==null)throw new InvalidOperationException("请先确认择弹顺序");string id=(string)stock.CurrentRow.Tag;int i=e.AmmoOrder.FindIndex(x=>AssemblyRules.Same(x,id)),j=i+delta;if(i<0||j<0||j>=e.AmmoOrder.Count)return;string other=e.AmmoOrder[j];e.AmmoOrder[j]=e.AmmoOrder[i];e.AmmoOrder[i]=other;Changed();RenderStock();}
        void EditRow(object sender,DataGridViewCellEventArgs args)
        {
            if(Loading||draft==null||args.RowIndex<0)return;var row=rows.Rows[args.RowIndex];var e=draft.Entries.First(x=>x.Id==(string)row.Tag);string value=Convert.ToString(row.Cells[args.ColumnIndex].Value);value=value.Length==0?null:value;
            if(args.ColumnIndex==0){e.WeaponId=value;Changed();RenderRows();return;}if(args.ColumnIndex==1)e.Quantity=value;else if(args.ColumnIndex==2)e.MountKind=value;else if(args.ColumnIndex==3)e.MountIndex=value;else if(args.ColumnIndex==4){e.InstallationId=value;var m=Session.Bodies.Where(b=>b.Installations!=null).SelectMany(b=>b.Installations).FirstOrDefault(x=>AssemblyRules.Same(x.Id,value));if(m!=null){e.MountKind=m.Kind;e.MountIndex=m.Index;}}Changed();if(args.ColumnIndex>=2)RenderBoard();
        }
        public override void Synchronize(){undoMount=null;afterMount=null;if(draft!=null){var original=Session.Loadouts.FirstOrDefault(x=>x.Id==draft.Id);draft=original==null?null:original.Clone();}else if(Session.Loadouts.Count>0)draft=Session.Loadouts[0].Clone();LocalDirty=false;Session.RecalculateDirty();Render();}
        void Render()
        {
            Loading=true;list.Items.Clear();foreach(var c in Session.Loadouts)list.Items.Add(new Choice{Id=c.Id,Text=c.Name});list.SelectedIndex=draft==null?-1:Session.Loadouts.FindIndex(x=>x.Id==draft.Id);name.Text=draft==null?"":draft.Name;name.Enabled=draft!=null;Loading=false;RenderRows();ValidateView();Notify();
        }
        void RenderRows()
        {
            Loading=true;rows.Rows.Clear();rows.Columns.Clear();var choices=Session.Weapons.Select(w=>new Choice{Id=w.Id,Text=w.Name}).ToList();choices.Insert(0,new Choice{Id="",Text="未选择"});
            rows.Columns.Add(new DataGridViewComboBoxColumn{Name="weapon",HeaderText="武器",DataSource=choices,DisplayMember="Text",ValueMember="Id",MinimumWidth=180,FillWeight=45});rows.Columns.Add(new DataGridViewTextBoxColumn{Name="quantity",HeaderText="数量",MinimumWidth=65,FillWeight=18});
            var mounts=new List<Choice>{new Choice{Id="",Text="不适用／未配置"},new Choice{Id="hull",Text="车体"},new Choice{Id="cannon",Text="机炮炮塔"},new Choice{Id="commander",Text="车长"}};
            rows.Columns.Add(new DataGridViewComboBoxColumn{Name="mount",HeaderText="安装",DataSource=mounts,DisplayMember="Text",ValueMember="Id",MinimumWidth=105,FillWeight=25});rows.Columns.Add(new DataGridViewTextBoxColumn{Name="group",HeaderText="安装组",MinimumWidth=75,FillWeight=12});var positions=Session.Bodies.Where(b=>b.Installations!=null).SelectMany(b=>b.Installations.Select(m=>new Choice{Id=m.Id,Text=b.Name+" / "+m.Kind+" "+m.Index})).ToList();positions.Insert(0,new Choice{Id="",Text="未选择安装结构"});rows.Columns.Add(new DataGridViewComboBoxColumn{Name="installation",HeaderText="平台安装引用",DataSource=positions,DisplayMember="Text",ValueMember="Id",MinimumWidth=180,FillWeight=30});bool anyMounted=false;
            if(draft!=null)foreach(var e in draft.Entries){var w=Session.Weapons.FirstOrDefault(x=>x.Id==e.WeaponId);bool mounted=w!=null&&new[]{"vehicle_slot","cannon_main"}.Contains(w.Fields["slotRule"]);anyMounted|=mounted||e.MountKind!=null||e.MountIndex!=null;int index=rows.Rows.Add(e.WeaponId??"",e.Quantity,e.MountKind??"",e.MountIndex,e.InstallationId??"");var row=rows.Rows[index];row.Tag=e.Id;row.Cells[2].ReadOnly=row.Cells[3].ReadOnly=!mounted&&e.MountKind==null&&e.MountIndex==null;if(!mounted){row.Cells[2].Style.BackColor=row.Cells[3].Style.BackColor=Color.FromArgb(242,244,247);}if(e.Id==selectedEntry)rows.CurrentCell=row.Cells[0];}
            rows.Columns[2].Visible=rows.Columns[3].Visible=rows.Columns[4].Visible=anyMounted;Loading=false;if(rows.CurrentRow!=null)selectedEntry=(string)rows.CurrentRow.Tag;RenderStock();RenderBoard();
        }
        void RenderStock()
        {
            Loading=true;stock.Rows.Clear();ammo.Items.Clear();foreach(var a in Session.Ammo)ammo.Items.Add(new Choice{Id=a.Id,Text=a.Name});if(ammo.Items.Count>0)ammo.SelectedIndex=0;var e=Selected();if(e!=null)foreach(var pair in (e.AmmoOrder==null?e.Inventory:e.AmmoOrder.Where(id=>e.Inventory.Keys.Any(k=>AssemblyRules.Same(k,id))).Select(id=>e.Inventory.First(pair=>AssemblyRules.Same(pair.Key,id))).ToDictionary(pair=>pair.Key,pair=>pair.Value))){var a=Session.Ammo.FirstOrDefault(x=>x.Id==pair.Key);int index=stock.Rows.Add(a==null?"引用不存在":a.Name,pair.Value);stock.Rows[index].Tag=pair.Key;}stock.Enabled=ammo.Enabled=e!=null;Loading=false;
        }
        public sealed class MountPayload { public string Owner,LoadoutId,EntryId,Signature; }
        public MountPayload BeginMount(string entryId){if(draft==null)throw new InvalidOperationException("先选择配装");return new MountPayload{Owner=dragOwner,LoadoutId=draft.Id,EntryId=entryId,Signature=MountSignature()};}
        string MountSignature(){return draft==null?null:new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(draft.Entries);}
        List<WeaponEntry> MountCandidate(MountPayload payload,string kind,string index,string slot)
        {
            if(payload==null||draft==null||payload.Owner!=dragOwner||payload.LoadoutId!=draft.Id||payload.Signature!=MountSignature())throw new InvalidOperationException("拖拽已失效，原配装保留");
            var next=draft.Clone();var e=next.Entries.FirstOrDefault(x=>x.Id==payload.EntryId);var w=e==null?null:Session.Weapons.FirstOrDefault(x=>x.Id==e.WeaponId);if(w==null)throw new InvalidOperationException("先为行选择武器");
            string rule=w.Fields["slotRule"];if(rule!="cannon_main"&&rule!="vehicle_slot")throw new InvalidOperationException("人员武器由系统分配，不拖到载具槽");if(rule=="cannon_main"&&(kind!="cannon"||slot!="main")||rule=="vehicle_slot"&&slot!=(kind=="hull"?"hull":"coax"))throw new InvalidOperationException("武器与该安装槽不适用");
            e.MountKind=kind;e.MountIndex=index;var owner=Session.Bindings.FirstOrDefault(b=>AssemblyRules.Same(b.LoadoutId,next.Id));var platform=owner==null?null:Session.Bodies.FirstOrDefault(b=>AssemblyRules.Same(b.Id,owner.BodyId));if(platform!=null&&platform.Installations!=null){decimal target;var mount=platform.Installations.FirstOrDefault(m=>m.Kind==kind&&AssemblyRules.Count(m.Index,out target,true)&&target.ToString(System.Globalization.CultureInfo.InvariantCulture)==index);e.InstallationId=mount==null?null:mount.Id;}var report=AssemblyRules.ValidateLoadout(next,Session.Weapons,Session.Ammo);if(report.Errors.Count>0)throw new FormatException(string.Join("；",report.Errors));
            // Bound units must retain their valid mounting constraints too.
            var configs=Session.Loadouts.Select(x=>x.Id==next.Id?next:x).ToList();foreach(var binding in Session.AffectedBindings(new[]{next.Id},null)){var r=AssemblyRules.ValidateBinding(binding,Session.Bindings,configs,Session.Bodies,Session.Weapons,Session.Ammo);if(r.Errors.Count>0)throw new FormatException(string.Join("；",r.Errors));}return next.Entries;
        }
        public void CancelMount(MountPayload payload){if(payload!=null)payload.Signature=null;}
        public void DropMount(MountPayload payload,string kind,string index,string slot){var next=MountCandidate(payload,kind,index,slot);undoMount=draft.Entries.Select(x=>x.Clone()).ToList();draft.Entries=next;afterMount=MountSignature();payload.Signature=null;Changed();RenderRows();}
        public void UndoMount(){if(undoMount==null||MountSignature()!=afterMount)throw new InvalidOperationException("没有可撤销安装，或后续编辑已改变配装");var next=draft.Clone();next.Entries=undoMount.Select(x=>x.Clone()).ToList();var report=AssemblyRules.ValidateLoadout(next,Session.Weapons,Session.Ammo);if(report.Errors.Count>0)throw new FormatException(string.Join("；",report.Errors));var configs=Session.Loadouts.Select(x=>x.Id==next.Id?next:x).ToList();foreach(var b in Session.AffectedBindings(new[]{next.Id},null)){var r=AssemblyRules.ValidateBinding(b,Session.Bindings,configs,Session.Bodies,Session.Weapons,Session.Ammo);if(r.Errors.Count>0)throw new FormatException(string.Join("；",r.Errors));}draft=next;undoMount=null;afterMount=null;Changed();RenderRows();}
        void RenderBoard()
        {
            foreach(Control c in board.Controls.Cast<Control>().ToArray()){board.Controls.Remove(c);c.Dispose();}if(draft==null)return;
            var groups=draft.Entries.Where(e=>e.MountKind!=null&&e.MountIndex!=null).Select(e=>e.MountKind+"/"+e.MountIndex).Concat(new[]{"cannon/1","commander/1","hull/1"}).Distinct().ToList();
            foreach(var group in groups){var parts=group.Split('/');string kind=parts[0],index=parts[1];foreach(var slot in kind=="cannon"?new[]{"main","coax"}:kind=="hull"?new[]{"hull"}:new[]{"coax"}){string targetSlot=slot;var b=new Button{Text=(kind=="cannon"?"机炮 "+index:kind=="hull"?"车体":"车长")+" / "+(slot=="main"?"主槽":slot=="hull"?"随车体":"同轴")+"\r\n"+(kind=="cannon"?"共享120°/s":kind=="commander"?"360°/s":"绑定后检查底盘"),Width=125,Height=62,AllowDrop=true};
                b.DragEnter+=delegate(object sender,DragEventArgs e){var payload=e.Data.GetData(typeof(MountPayload)) as MountPayload;try{MountCandidate(payload,kind,index,targetSlot);e.Effect=DragDropEffects.Move;b.BackColor=Color.FromArgb(211,239,221);}catch(Exception error){e.Effect=DragDropEffects.None;b.BackColor=Color.FromArgb(235,220,220);Status.Text=error.Message;}};b.DragOver+=delegate(object sender,DragEventArgs e){var payload=e.Data.GetData(typeof(MountPayload)) as MountPayload;try{MountCandidate(payload,kind,index,targetSlot);e.Effect=DragDropEffects.Move;}catch(Exception error){e.Effect=DragDropEffects.None;Status.Text=error.Message;}};b.DragLeave+=delegate{b.BackColor=SystemColors.Control;};b.DragDrop+=delegate(object sender,DragEventArgs e){try{DropMount(e.Data.GetData(typeof(MountPayload)) as MountPayload,kind,index,targetSlot);}catch(Exception error){Dialogs.Error(this,error.Message);}};board.Controls.Add(b);
            }}
        }
        protected override void ValidateView(){if(draft==null){Status.Text="空列表；先新建配装。";return;}Report(AssemblyRules.ValidateLoadout(draft,Session.Weapons,Session.Ammo),"");}
        public override bool Commit(){rows.EndEdit();stock.EndEdit();if(!LocalDirty||draft==null)return true;try{Session.ApplyLoadout(draft);LocalDirty=false;Render();return true;}catch(Exception e){Dialogs.Error(this,e.Message);return false;}}
    }
}
