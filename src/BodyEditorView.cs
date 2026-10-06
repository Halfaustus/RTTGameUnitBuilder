using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using RTTUnitEditor.Domain;
using RTTUnitEditor.Editing;

namespace RTTUnitEditor.UI
{
    public sealed class BodyEditorView : UserControl
    {
        public readonly EditorSession Session = new EditorSession();
        private readonly EditorDialogs dialogs;
        private readonly ListBox list = new ListBox();
        private readonly TableLayoutPanel fields = new TableLayoutPanel();
        private readonly Dictionary<string, Control> inputs = new Dictionary<string, Control>();
        private readonly Dictionary<string, Control> labels = new Dictionary<string, Control>();
        private readonly Dictionary<string, int> rowIndices = new Dictionary<string, int>();
        private readonly Dictionary<string, CheckBox> tagChecks = new Dictionary<string, CheckBox>();
        private readonly ErrorProvider errors = new ErrorProvider();
        private readonly ToolTip tips = new ToolTip();
        private readonly Label status = new Label();
        private readonly Label fileStatus = new Label();
        private readonly Label offroadSpeed = new Label();
        private readonly Panel scroll = new Panel();
        private readonly Button copy, save, saveAs, reload;
        private BodyDraft current;
        private bool loading;
        readonly Dictionary<string,ComboBox> armorChoices=new Dictionary<string,ComboBox>();
        readonly Dictionary<string,TextBox> armorText=new Dictionary<string,TextBox>();

        public event EventHandler ContextChanged;
        public string CurrentCaption {
            get {
                if (current == null) return "单位本体";
                return current.Name + (Session.Dirty ? " *" : "") +
                    (current.TestOnly ? " · 仅供测试" : " · 来源参考（只读）");
            }
        }

        public BodyEditorView(EditorDialogs editorDialogs)
        {
            dialogs = editorDialogs;
            Name = "BodyEditor";
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            errors.ContainerControl = this;
            errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            actions.Controls.Add(Action("NewBody", "新建单位本体", delegate { NewBody(null); }));

            copy = Action("CopyBody", "复制", delegate { CopyBody(); });
            save = Action("SaveBodies", "保存", delegate { Save(false); });
            saveAs = Action("SaveBodiesAs", "另存为", delegate { Save(true); });
            reload = Action("ReloadBodies", "重新加载", delegate { ReplaceFile(true); });
            actions.Controls.Add(copy);
            actions.Controls.Add(Action("DeleteBody", "删除", delegate { if(current == null) return; try { if(!dialogs.ConfirmAction(this,"删除本体「"+current.Name+"」？")) return; Session.DeleteBody(current.Id); current=null; RefreshView(); } catch(Exception error) { dialogs.Error(this,error.Message); } }));
            actions.Controls.Add(save);
            actions.Controls.Add(saveAs);
            actions.Controls.Add(Action("OpenBodies", "打开", delegate { ReplaceFile(false); }));
            actions.Controls.Add(reload);
            actions.Controls.Add(Action("BodyInstallations","安装结构",delegate{if(current==null)return;using(var editor=new PlatformRelationEditor(current,m=>Session.ApplyPlatform(current.Id,m)))if(editor.ShowDialog(this)==DialogResult.OK){current=Session.Bodies.First(x=>x.Id==current.Id);RefreshView();}}));

            layout.Controls.Add(actions, 0, 0);
            fileStatus.AutoSize = true;
            fileStatus.Dock = DockStyle.Top;
            fileStatus.Margin = new Padding(0, 4, 0, 6);
            layout.Controls.Add(fileStatus, 0, 1);
            var work = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            work.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
            work.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            list.Name = "BodyList";
            list.Dock = DockStyle.Fill;
            list.HorizontalScrollbar = true;
            list.IntegralHeight = false;
            list.Margin = new Padding(0, 0, 10, 0);
            list.SelectedIndexChanged += OnSelect;
            work.Controls.Add(list, 0, 0);
            scroll.Name = "BodyProperties";
            scroll.AutoScroll = true;
            scroll.AutoScrollMinSize = new Size(340, 0);
            scroll.Dock = DockStyle.Fill;
            fields.Name = "BodyFields";
            fields.AutoSize = true;
            fields.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fields.Dock = DockStyle.Top;
            fields.ColumnCount = 2;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.Padding = new Padding(0, 0, 24, 0);
            scroll.Controls.Add(fields);
            work.Controls.Add(scroll, 1, 0);
            layout.Controls.Add(work, 0, 2);
            status.Name = "BodyValidation";
            status.AutoSize = true;
            status.Dock = DockStyle.Top;
            status.MaximumSize = new Size(650, 0);
            status.Margin = new Padding(0, 6, 0, 0);
            layout.Controls.Add(status, 0, 3);
            layout.SizeChanged += delegate { status.MaximumSize = new Size(Math.Max(100, layout.Width), 0); fileStatus.MaximumSize = status.MaximumSize; };
            AddText("name", "名称", false, "本体显示名称。名称不会改变稳定技术ID。");
            AddText("id", "技术ID", true, "工具稳定身份，不是新的游戏属性。复制生成新ID。");
                        var unitType=new ComboBox{Name="Field_unitType",DropDownStyle=ComboBoxStyle.DropDownList};unitType.Items.AddRange(new[]{"未配置","步兵","地面车辆"});AddRow("unitType","单位类型",unitType,"统一新建后选择类型；修改类型会明确提示受影响字段和配置。");unitType.SelectedIndexChanged+=delegate{if(loading||current==null||!current.TestOnly||unitType.SelectedIndex<0)return;ChangeType(new[]{null,"infantry","ground_vehicle"}[unitType.SelectedIndex]);};
            AddText("memberCount", "历史满编人数", false, "步兵人数为配置值；车辆人数固定1，不是载员或车组人数。留空表示未配置。");
            AddText("maximumHealth", "最大生命", false, "班组最大生命=满编人数×5；每人生命固定5。车辆生命10～20。留空是未配置。");
            AddText("roadSpeed", "公路速度（m/s）", false, "步兵未声明时默认5m/s，留空保持未声明，显式0保留。车辆留空仍为未配置。");
            var penalty = new ComboBox { Name = "Field_offroadPenalty", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
            penalty.Items.AddRange(new object[] { "未配置", "0%（步兵固定）", "20%（履带规则）", "50%（轮式规则）" });
            AddRow("offroadPenalty", "越野惩罚", penalty, "仅表达既有惩罚属性，不添加底盘标签；车辆不能选择步兵0%规则。");
            penalty.SelectedIndexChanged += delegate {
                if (loading || current == null || !current.TestOnly) return;
                current.Fields["offroadPenalty"] = new[] { null, "0", "0.2", "0.5" }[penalty.SelectedIndex];
                Changed();
            };
            offroadSpeed.Name = "Field_offroadSpeed";
            offroadSpeed.AutoSize = true;
            offroadSpeed.Dock = DockStyle.Top;
            AddRow("offroadSpeed", "越野速度（m/s）", offroadSpeed, "派生值：公路速度×(1－越野惩罚)。无配置依据时保持未配置。只降低位移，不降低转向。");
            AddText("turnSpeed", "本体转向（°/s）", false, "步兵固定360；车辆配置本体转速。与瞄准并行，不额外串行相加。");
            foreach (string energy in new[] { "kinetic", "chemical" })
                foreach (string face in new[] { "front", "side", "rear", "top" }) {
                    string energyLabel = energy == "kinetic" ? "动能" : "化学";
                    string faceLabel = new Dictionary<string, string> { { "front", "前" }, { "side", "侧" }, { "rear", "后" }, { "top", "顶" } }[face];
                                        AddText(energy + "." + face, energyLabel + "防护 · " + faceLabel, false, "车辆防护≥5；步兵防护选6／8／10并四面一致。留空为未配置。");
                    if(face=="front"){string key=energy+".front";armorText[key]=(TextBox)inputs[key];var choice=new ComboBox{Name="Choice_"+key,DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top};armorChoices[key]=choice;fields.Controls.Add(choice,1,rowIndices[key]);choice.Visible=false;choice.SelectedIndexChanged+=delegate{if(loading||current==null||current.UnitType!="infantry"||!current.TestOnly)return;string value=choice.SelectedIndex<=0?null:Convert.ToString(choice.SelectedItem);foreach(var side in new[]{"front","side","rear","top"})current.Fields[key.Substring(0,key.Length-6)+"."+side]=value;Changed();};}
                }
            var tags = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            string[] tagNames = { "冲刺", "烟雾×1", "烟雾×4", "镭射", "移动补给", "APS" };
            for (int index = 0; index < BodyDraft.LegalTags.Length; index++) {
                string tag = BodyDraft.LegalTags[index];
                var box = new CheckBox { Name = "Tag_" + tag, Text = tagNames[index], AutoSize = true, Margin = new Padding(0, 0, 10, 4) };
                tagChecks[tag] = box;
                tips.SetToolTip(box, tag == "aps" ? "仅注册合法标签，不启用APS功能。" :
                    tag == "sprint" ? "地面车辆不具步兵冲刺资格；架设火炮禁冲刺须在后续配装中检查。" :
                    tag.StartsWith("smoke") ? "烟雾×1与×4互斥，不自动删除另一项。" :
                    "仅配置已确认标签；相关设备／后勤参数与跨对象规则尚未检查。");
                box.CheckedChanged += delegate {
                    if (loading || current == null || !current.TestOnly) return;
                    if (box.Checked) current.Tags.Add(tag); else current.Tags.Remove(tag);
                    Changed();
                };
                tags.Controls.Add(box);
            }
            AddRow("tags", "能力标签", tags, "只有既有六项能力。标签不豁免使用资格或补足缺失参数。");
            AddText("faction", "阵营", false, "归属记录，不限制武器、弹药或单位配置选择。");
            AddText("specialization", "历史专精", false, "现行专精在单位配置页编辑；此字段仅保留旧文件来源。");
            AddText("weight", "车辆本体重量 t", false, "步兵不填写；运输重量只计算满编人数×0.1t加特殊装备，普通武器重量不重复相加。");
            AddText("maxPassengers", "最大乘员数", false, "载具独立座位硬限制；未配置不等于0。");
            AddText("totalLoad", "总运输载重 t", false, "人员、特殊装备及车载补给共用一个载重池；未配置不等于0。");
            AddText("source", "来源记录", false, "纯工具来源记录，不授予正式设计身份。");
            RefreshView();
        }

        private Button Action(string name, string text, Action run)
        {
            var button = new Button { Name = name, Text = text, AutoSize = true, Padding = new Padding(4, 2, 4, 2), Margin = new Padding(0, 0, 4, 4) };
            button.Click += delegate { run(); };
            return button;
        }
        private void AddText(string key, string caption, bool readOnly, string tooltip)
        {
            var text = new TextBox { Name = "Field_" + key, Dock = DockStyle.Top, ReadOnly = readOnly };
            AddRow(key, caption, text, tooltip);
            text.TextChanged += delegate {
                if (loading || current == null || !current.TestOnly || text.ReadOnly) return;
                if (key == "name") current.Name = text.Text;
                else if (key == "source") current.SourceNote = text.Text;
                else {
                    string value = text.Text.Length == 0 ? null : text.Text;
                    current.Fields[key] = value;
                    if (current.UnitType == "infantry" && (key == "kinetic.front" || key == "chemical.front")) {
                        string prefix = key.Split('.')[0];
                        foreach (string face in new[] { "side", "rear", "top" }) current.Fields[prefix + "." + face] = value;
                    }
                    if (key == "memberCount") {
                        current.UpdateInfantryHealth();
                        loading = true;
                        ((TextBox)inputs["maximumHealth"]).Text = current.Fields["maximumHealth"] ?? "";
                        loading = false;
                    }
                }
                Changed();
            };
        }
        private void AddRow(string key, string caption, Control input, string tooltip)
        {
            int row = fields.RowCount++;
            var label = new Label { Text = caption, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 3, 8, 6) };
            input.Margin = new Padding(0, 3, 0, 6);
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(label, 0, row);
            fields.Controls.Add(input, 1, row);
            rowIndices[key] = row;
            labels[key] = label;
            inputs[key] = input;
            tips.SetToolTip(label, tooltip);
            tips.SetToolTip(input, tooltip);
            errors.SetIconAlignment(input, ErrorIconAlignment.MiddleRight);
        }
        private void Changed()
        {
            Session.MarkChanged();
            RefreshList();
            ValidateCurrent();
            UpdateStatus();
        }
        private void RefreshList()
        {
            loading = true;
            int selected = current == null ? -1 : Session.Bodies.IndexOf(current);
            list.Items.Clear();
            foreach (var body in Session.Bodies)
                list.Items.Add(body.Name + (body.TestOnly ? " · 测试" : " · 只读"));
            list.SelectedIndex = selected;
            loading = false;
        }
        private void OnSelect(object sender, EventArgs args)
        {
            if (loading) return;
            int target = list.SelectedIndex;
            if (target < 0) return;
            string targetId = Session.Bodies[target].Id;
            if (!CanLeave(true)) { RefreshList(); return; }
            current = Session.Bodies.FirstOrDefault(body => body.Id == targetId);
            RefreshView();
        }
        private void ShowRow(string key, bool visible)
        {
            labels[key].Visible = visible;
            inputs[key].Visible = visible;
            fields.RowStyles[rowIndices[key]].SizeType = visible ? SizeType.AutoSize : SizeType.Absolute;
            fields.RowStyles[rowIndices[key]].Height = 0;
        }
        public void Synchronize() { current = current == null ? null : Session.Bodies.FirstOrDefault(b => b.Id == current.Id); RefreshView(); }
        private void RefreshView()
        {
            loading = true;
            bool exists = current != null;
            scroll.Enabled = exists;
            copy.Enabled = exists;
            save.Enabled = saveAs.Enabled = Session.Bodies.Count + Session.Weapons.Count + Session.Ammo.Count + Session.Loadouts.Count + Session.Bindings.Count > 0;
            reload.Enabled = Session.FilePath != null;
            if (exists) {
                bool infantry = current.UnitType == "infantry";
                ((TextBox)inputs["name"]).Text = current.Name;
                ((TextBox)inputs["name"]).ReadOnly = !current.TestOnly;
                ((TextBox)inputs["id"]).Text = current.Id;
                ((ComboBox)inputs["unitType"]).SelectedIndex = Array.IndexOf(new[]{null,"infantry","ground_vehicle"},current.UnitType);((ComboBox)inputs["unitType"]).Enabled=current.TestOnly;
                ((TextBox)inputs["source"]).Text = current.SourceNote;
                ((TextBox)inputs["source"]).ReadOnly = !current.TestOnly;
                tips.SetToolTip(inputs["source"], "来源基线：" + current.Baseline + "\r\n复制来源：" + (current.CopiedFromId ?? "无") + "\r\n来源声明不等于授权核实。");
                foreach (string key in BodyDraft.FieldKeys) {
                    if (key == "offroadPenalty") continue;
                    if(armorText.ContainsKey(key))inputs[key]=armorText[key];
                    var text = (TextBox)inputs[key];
                    text.Text = current.Fields[key] ?? "";
                    text.ReadOnly = key=="memberCount"||key=="specialization"||!current.TestOnly || (infantry && (key == "maximumHealth" || key == "turnSpeed")) || (!infantry && key == "memberCount");
                }
                var penalty = (ComboBox)inputs["offroadPenalty"];
                penalty.SelectedIndex = Array.IndexOf(new[] { null, "0", "0.2", "0.5" }, current.Fields["offroadPenalty"]);
                penalty.Enabled = current.TestOnly && !infantry;
                foreach(var key in new[]{"weight","maxPassengers","totalLoad"}) ShowRow(key,!infantry);
                foreach (string energy in new[] { "kinetic", "chemical" }) {
                    string front=energy+".front";var choice=armorChoices[front];choice.Items.Clear();choice.Items.AddRange(new[]{"未配置","6","8","10"});string value=current.Fields[front];if(value!=null&&!new[]{"6","8","10"}.Contains(value))choice.Items.Add(value);choice.SelectedIndex=value==null?0:choice.Items.IndexOf(value);choice.Visible=infantry;choice.Enabled=current.TestOnly;armorText[front].Visible=!infantry;inputs[front]=infantry?(Control)choice:armorText[front];
                    labels[energy + ".front"].Text = infantry ? (energy == "kinetic" ? "动能防护" : "化学防护") :
                        (energy == "kinetic" ? "动能防护 · 前" : "化学防护 · 前");
                    foreach (string face in new[] { "side", "rear", "top" }) ShowRow(energy + "." + face, !infantry);
                }
                                labels["roadSpeed"].Text=infantry?"速度 m/s（空＝5）":"公路速度（m/s）";
                foreach(string key in BodyDraft.FieldKeys.Where(k=>k!="faction"&&k!="specialization")){
                    bool visible=current.UnitType!=null&&(!infantry||!new[]{"weight","maxPassengers","totalLoad"}.Contains(key))&&(!infantry||!key.Contains(".")||key.EndsWith(".front"));ShowRow(key,visible);
                }
                ShowRow("offroadSpeed",current.UnitType!=null);ShowRow("tags",current.UnitType!=null);
                if(current.UnitType==null)foreach(var choice in armorChoices.Values)choice.Visible=false;
                foreach (var pair in tagChecks) {
                    pair.Value.Checked = current.Tags.Contains(pair.Key);
                    pair.Value.Enabled = current.TestOnly && (pair.Key != "sprint" || infantry);
                }
            } else {
                foreach (TextBox text in inputs.Values.OfType<TextBox>()) text.Text = "";
            }
            loading = false;
            RefreshList();
            ValidateCurrent();
            UpdateStatus();
        }
        private void ValidateCurrent()
        {
            errors.Clear();
            if (current == null) { status.Text = ""; return; }
            var problems = Session.Errors(current);
            foreach (var pair in problems)
                if (inputs.ContainsKey(pair.Key)) errors.SetError(inputs[pair.Key], pair.Value);
            status.ForeColor = problems.Count == 0 ? Color.FromArgb(90, 105, 122) : Color.Firebrick;
            status.Text = problems.Count == 0 ?
                (current.UnitType==null?"单位类型未配置；":"")+"未配置 " + current.Fields.Count(pair => pair.Value == null) + " 项；" + BodyRules.Unchecked :
                string.Join("；", problems.Select(pair => (labels.ContainsKey(pair.Key) ? labels[pair.Key].Text : pair.Key) + "：" + pair.Value));
            tips.SetToolTip(status, Session.DescribeBodyImpact(current.Id));
            if(Session.Bindings.Any(b=>AssemblyRules.Same(b.BodyId,current.Id))) status.Text += "\r\n"+Session.DescribeBodyImpact(current.Id);
            foreach(var binding in Session.AffectedBindings(new string[0],current.Id)){var report=AssemblyRules.ValidateBinding(binding,Session.Bindings,Session.Loadouts,Session.Bodies,Session.Weapons,Session.Ammo);if(report.Errors.Count>0)status.Text+="\r\n"+Session.BindingName(binding)+"："+string.Join("；",report.Errors.Take(2));}
            offroadSpeed.Text = current.OffroadSpeed() ?? "未配置（派生）";
            decimal speed;
            if (BodyRules.TryNumber(current.EffectiveRoadSpeed(), out speed)) {
                try { tips.SetToolTip(inputs["roadSpeed"], (current.Fields["roadSpeed"]==null&&current.UnitType=="infantry"?"未声明，使用默认速度；":"")+"精确值：" + current.EffectiveRoadSpeed() + "m/s；显示：" + decimal.Truncate(speed * 3.6m).ToString(CultureInfo.InvariantCulture) + "km/h。"); }
                catch (OverflowException) { tips.SetToolTip(inputs["roadSpeed"], "m/s值保留；km/h显示超出工具数值表示范围。"); }
            }
        }
        private void UpdateStatus()
        {
            fileStatus.Text = (Session.FilePath == null ? "未保存到文件" : System.IO.Path.GetFileName(Session.FilePath)) + (Session.Dirty ? " · 未保存 *" : "");
            tips.SetToolTip(fileStatus, Session.FilePath ?? "所有改动仅在当前草稿中。");
            if (ContextChanged != null) ContextChanged(this, EventArgs.Empty);
        }
        public bool CanLeave(bool keepDraft)
        {
            if (keepDraft || !Session.Dirty) return true;
            string currentId = current == null ? null : current.Id;
            bool allowed = Session.AllowLeave(dialogs.ConfirmLeave(this, keepDraft), delegate { return Save(false); });
            if (allowed) {
                current = Session.Bodies.FirstOrDefault(body => body.Id == currentId);
                RefreshView();
            }
            return allowed;
        }
        private void ChangeType(string type)
        {
            if(current.UnitType==type)return;var next=current.WithType(type);var changes=current.Fields.Where(p=>p.Value!=null&&p.Value!=next.Fields[p.Key]).Select(p=>p.Key+"："+p.Value+" → "+(next.Fields[p.Key]??"未配置")).ToList();
            if(current.Installations!=null&&next.Installations==null)changes.Add("移除不适用的载具安装结构；已有武器引用需重新配置");
            if(current.Tags.Except(next.Tags).Any())changes.Add("移除不适用标签："+string.Join("、",current.Tags.Except(next.Tags)));
            string impact=Session.DescribeBodyImpact(current.Id);
            if((changes.Count>0||Session.Bindings.Any(b=>b.BodyId==current.Id))&&!dialogs.ConfirmAction(this,"修改单位类型将更新以下字段：\r\n"+string.Join("\r\n",changes)+"\r\n"+impact+"\r\n继续？")){RefreshView();return;}
            int index=Session.Bodies.FindIndex(b=>b.Id==current.Id);Session.Bodies[index]=next;current=next;Session.MarkChanged();RefreshView();
        }
        private void NewBody(string type)
        {
            if (!CanLeave(true)) return;
            current = Session.Create(type);
            RefreshView();
            inputs["name"].Focus();
        }
        private void CopyBody()
        {
            if (current == null || !CanLeave(true)) return;
            if (current == null) return;
            try { current = Session.Copy(current); RefreshView(); }
            catch (Exception error) { dialogs.Error(this, error.Message); }
        }
        public bool Save(bool choosePath)
        {
            var invalid = Session.Bodies.FirstOrDefault(body => Session.Errors(body).Count != 0);
            if (invalid != null) {
                current = invalid;
                RefreshView();
                var problem = Session.Errors(invalid).First();
                if (inputs.ContainsKey(problem.Key)) {
                    scroll.ScrollControlIntoView(inputs[problem.Key]);
                    inputs[problem.Key].Focus();
                }
                dialogs.Error(this, "本体字段有错误，文件未写入。");
                return false;
            }
            string path = choosePath || Session.FilePath == null ? dialogs.ChooseSave(this, Session.FilePath) : Session.FilePath;
            if (path == null) return false;
            try { Session.Save(path); RefreshView(); return true; }
            catch (Exception error) { dialogs.Error(this, "保存失败，草稿与原文件保留。\r\n" + error.Message); return false; }
        }
        private void ReplaceFile(bool reloadFile)
        {
            string path = reloadFile ? Session.FilePath : dialogs.ChooseOpen(this, false);
            if (path == null || !CanLeave(false)) return;
            try {
                Session.Open(path);
                current = Session.Bodies.FirstOrDefault();
                RefreshView();
            }
            catch (Exception error) { dialogs.Error(this, "文件未加载，当前数据保留。\r\n" + error.Message); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { errors.Dispose(); tips.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
