using System;
using System.Drawing;
using System.Windows.Forms;

namespace RTTUnitEditor.UI
{
    public sealed class MainWindow : Form
    {
        private readonly NavigationPage[] pages = NavigationPage.CreatePages();
        private readonly Button[] navigation;
        private readonly Label pageTitle;
        private readonly BodyEditorView bodyEditor;
        private readonly CombatEditorView weaponEditor, ammoEditor;
        private readonly AssemblyEditorView configurationEditor; private readonly BindingEditorView cardEditor;
        private int currentPage = -1;
        private readonly Color ink = Color.FromArgb(31, 43, 58);
        private readonly Color accent = Color.FromArgb(29, 83, 124);

        public MainWindow() : this(new EditorDialogs()) { }
        public MainWindow(EditorDialogs dialogs)
        {
            Text = "RTT 独立单位编辑器";
            Name = "RTTUnitEditorWindow";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = new Font("Microsoft YaHei UI", 10F);
            ForeColor = ink;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;

            var workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Padding = new Padding(12), Margin = Padding.Empty
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(workspace);

            var sidebar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = pages.Length + 1,
                Margin = new Padding(0, 0, 12, 0)
            };
            navigation = new Button[pages.Length];
            for (int index = 0; index < pages.Length; index++)
            {
                sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var button = new Button
                {
                    Name = "Navigation" + index, AccessibleName = pages[index].Title,
                    Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(128, 36),
                    Margin = new Padding(0, 0, 0, 4),
                    Padding = new Padding(8, 6, 8, 6), FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand,
                    UseVisualStyleBackColor = false, TabIndex = index
                };
                button.FlatAppearance.BorderSize = 0;
                int pageIndex = index;
                button.Click += delegate { SelectPage(pageIndex); };
                navigation[index] = button;
                sidebar.Controls.Add(button, 0, index);
            }
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.Controls.Add(sidebar, 0, 0);

            var content = new TableLayoutPanel
            {
                Name = "PageContent", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Padding = new Padding(16), Margin = Padding.Empty, BackColor = Color.White
            };
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageTitle = new Label
            {
                Name = "PageTitle", Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(400,0),
                Margin = new Padding(0, 0, 0, 8), Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false
            };
            content.SizeChanged+=delegate{pageTitle.MaximumSize=new Size(Math.Max(100,content.ClientSize.Width-content.Padding.Horizontal),0);};
            content.Controls.Add(pageTitle, 0, 0);
            bodyEditor = new BodyEditorView(dialogs);
            bodyEditor.ContextChanged += delegate { if (currentPage == 0) pageTitle.Text = bodyEditor.CurrentCaption; };
            content.Controls.Add(bodyEditor, 0, 1);
            weaponEditor = new CombatEditorView(bodyEditor.Session, dialogs, "weapon", bodyEditor.Save);
            ammoEditor = new CombatEditorView(bodyEditor.Session, dialogs, "ammo", bodyEditor.Save);
            weaponEditor.ContextChanged += delegate { if(currentPage == 1) pageTitle.Text = weaponEditor.Caption; };
            ammoEditor.ContextChanged += delegate { if(currentPage == 2) pageTitle.Text = ammoEditor.Caption; };
            content.Controls.Add(weaponEditor, 0, 1); content.Controls.Add(ammoEditor, 0, 1);
            configurationEditor = new AssemblyEditorView(bodyEditor.Session, dialogs, bodyEditor.Save);
            cardEditor = new BindingEditorView(bodyEditor.Session, dialogs, bodyEditor.Save);
            configurationEditor.ContextChanged += delegate { if(currentPage == 3) pageTitle.Text = configurationEditor.Caption; };
            cardEditor.ContextChanged += delegate { if(currentPage == 4) pageTitle.Text = cardEditor.Caption; };
            content.Controls.Add(configurationEditor,0,1);content.Controls.Add(cardEditor,0,1);
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (!CanLeave(false)) args.Cancel = true; };
            workspace.Controls.Add(content, 1, 0);
            SelectPage(0);
        }

        private bool CanLeave(bool keepDraft) {
            return currentPage == 3 ? configurationEditor.CanLeave(keepDraft) : currentPage == 4 ? cardEditor.CanLeave(keepDraft) : currentPage == 1 ? weaponEditor.CanLeave(keepDraft) : currentPage == 2 ? ammoEditor.CanLeave(keepDraft) : bodyEditor.CanLeave(keepDraft);
        }
        private void SelectPage(int index)
        {
            if (index == currentPage) return;
            if (!CanLeave(true)) return;
            currentPage = index;
            bodyEditor.Synchronize(); weaponEditor.Synchronize(); ammoEditor.Synchronize(); configurationEditor.Synchronize(); cardEditor.Synchronize();
            bodyEditor.Visible = index == 0; weaponEditor.Visible = index == 1; ammoEditor.Visible = index == 2; configurationEditor.Visible=index==3; cardEditor.Visible=index==4;
            pageTitle.Text = index == 0 ? bodyEditor.CurrentCaption : index == 1 ? weaponEditor.Caption : index == 2 ? ammoEditor.Caption : index == 3 ? configurationEditor.Caption : cardEditor.Caption;
            for (int item = 0; item < navigation.Length; item++)
            {
                bool selected = item == index;
                navigation[item].Text = (selected ? "●  " : "    ") + pages[item].Title;
                navigation[item].BackColor = selected ? accent : Color.FromArgb(232, 237, 243);
                navigation[item].ForeColor = selected ? Color.White : ink;
                navigation[item].AccessibleDescription = selected ? "当前页面" : "切换页面";
            }
        }
    }
}
