using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using RTTUnitEditor.Editing;
using RTTUnitEditor.Domain;
namespace RTTUnitEditor.UI
{
    public abstract class EditorWorkspace : UserControl
    {
        protected readonly EditorSession Session;
        protected readonly EditorDialogs Dialogs;
        protected bool Loading, LocalDirty;
        protected readonly Label Status = new Label();
        protected readonly TableLayoutPanel PageLayout = new TableLayoutPanel();
        protected readonly FlowLayoutPanel Toolbar = new FlowLayoutPanel();
        readonly Func<bool,bool> saveFile; readonly ToolTip reportTips=new ToolTip();
        public event EventHandler ContextChanged;
        public abstract string Caption { get; }
        protected EditorWorkspace(EditorSession s,EditorDialogs dialogs,Func<bool,bool> save)
        {
            Session=s;Dialogs=dialogs;saveFile=save;Dock=DockStyle.Fill;BackColor=Color.White;
            PageLayout.Dock=DockStyle.Fill;PageLayout.ColumnCount=1;PageLayout.RowCount=3;
            PageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));PageLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));PageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));Controls.Add(PageLayout);
            Toolbar.Dock=DockStyle.Fill;Toolbar.AutoSize=true;Toolbar.Margin=Padding.Empty;PageLayout.Controls.Add(Toolbar,0,0);
            var details=new Panel{Name="ValidationDetails",Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(8),BackColor=Color.FromArgb(244,247,250)};Status.Name="ValidationSummary";Status.AutoSize=true;Status.MinimumSize=new Size(0,40);Status.Dock=DockStyle.Top;details.Controls.Add(Status);details.SizeChanged+=delegate{Status.MaximumSize=new Size(Math.Max(80,details.ClientSize.Width-36),0);};PageLayout.Controls.Add(details,0,2);
        }
        protected Button Button(FlowLayoutPanel panel,string name,string text,Action action)
        {
            var b=new Button{Name=name,Text=text,AutoSize=true,Margin=new Padding(0,0,4,4)};
            b.Click+=delegate{try{action();}catch(Exception e){Dialogs.Error(this,"操作未完成，原有效数据保留。\r\n"+e.Message);}};panel.Controls.Add(b);return b;
        }
        protected void FileActions(string prefix)
        {
            Button(Toolbar,prefix+"Save","保存",()=>Save(false));Button(Toolbar,prefix+"SaveAs","另存为",()=>Save(true));
            Button(Toolbar,prefix+"Open","打开",()=>Replace(false));Button(Toolbar,prefix+"Reload","重载",()=>Replace(true));
        }
        protected void Changed() { LocalDirty=true;Session.MarkChanged();ValidateView();Notify(); }
        protected void Notify() { if(ContextChanged!=null)ContextChanged(this,EventArgs.Empty); }
        protected void Report(ValidationReport r,string extra)
        {
            Status.ForeColor=r.Errors.Count>0?Color.Firebrick:Color.FromArgb(90,105,122);
            Status.Text="检查结果：错误 "+r.Errors.Count+" · 待完善 "+r.Pending.Count+" · 尚未检查 "+r.Unchecked.Count+(Session.Dirty?" · 未保存":"")+extra+"\r\n"+string.Join("\r\n",r.Errors.Select(x=>"错误："+x).Concat(r.Pending.Select(x=>"待完善："+x)).Concat(r.Unchecked.Select(x=>"尚未检查："+x)));
            Status.Tag=r;Status.AccessibleDescription=string.Join("\r\n",r.Errors.Concat(r.Pending).Concat(r.Unchecked));reportTips.SetToolTip(Status,Status.AccessibleDescription);
        }
        public abstract void Synchronize();
        public abstract bool Commit();
        protected abstract void ValidateView();
        public bool Save(bool choosePath) { if(!Commit())return false;bool ok=saveFile(choosePath);ValidateView();Notify();return ok; }
        public bool CanLeave(bool keepDraft)
        {
            if(keepDraft)return Commit();
            if(!Session.Dirty&&!LocalDirty)return true;
            var choice=Dialogs.ConfirmLeave(this,false);
            if(choice==LeaveChoice.Cancel)return false;
            if(choice==LeaveChoice.Save)return Save(false);
            Session.AllowLeave(LeaveChoice.Continue,()=>false);Synchronize();return true;
        }
        void Replace(bool reload)
        {
            string path=reload?Session.FilePath:Dialogs.ChooseOpen(this,false);
            if(path==null||!CanLeave(false))return;
            Session.Open(path);Synchronize();
            if(Session.MigrationNote!=null)Status.Text=Session.MigrationNote;
        }
        protected override void Dispose(bool disposing){if(disposing)reportTips.Dispose();base.Dispose(disposing);}
        protected sealed class Choice
        {
            public string Id {get;set;} public string Text {get;set;}
            public override string ToString(){return Text;}
        }
        protected static void FitDropDown(ComboBox combo){int width=combo.Width;foreach(var item in combo.Items)width=Math.Max(width,TextRenderer.MeasureText(combo.GetItemText(item),combo.Font).Width+32);combo.DropDownWidth=Math.Min(1000,width);}
        protected DataGridView Grid(string name)
        {
            var grid=new DataGridView{Name=name,Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.FixedSingle,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,EditMode=DataGridViewEditMode.EditOnEnter};grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.DefaultCellStyle.Padding=new Padding(4,5,4,5);grid.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;grid.RowTemplate.MinimumHeight=32;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;grid.EditingControlShowing+=delegate(object sender,DataGridViewEditingControlShowingEventArgs e){var combo=e.Control as ComboBox;if(combo!=null)FitDropDown(combo);};return grid;
        }
    }
}
