using System;
using System.Windows.Forms;
using RTTUnitEditor.Editing;

namespace RTTUnitEditor.UI
{
    // Replaceable UI boundary for deterministic cancellation/file-failure checks.
    public class EditorDialogs
    {
        public virtual LeaveChoice ConfirmLeave(IWin32Window owner, bool keepDraft)
        {
            string no = "否：丢弃当前文件的所有未保存改动并继续（未保存的新对象将删除）。";
            DialogResult result = MessageBox.Show(owner,
                "有未保存的数据。\r\n是：保存后继续。\r\n" + no + "\r\n取消：留在当前对象。",
                "未保存", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            return result == DialogResult.Yes ? LeaveChoice.Save :
                result == DialogResult.No ? LeaveChoice.Continue : LeaveChoice.Cancel;
        }
        public virtual string ChooseOpen(IWin32Window owner, bool import)
        {
            using (var dialog = new OpenFileDialog {
                Title = import ? "导入本体定义" : "打开用户文件",
                Filter = "RTT工具JSON (*.json)|*.json", CheckFileExists = true
            }) return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
        }
        public virtual string ChooseSave(IWin32Window owner, string currentPath)
        {
            using (var dialog = new SaveFileDialog {
                Title = "保存用户文件", Filter = "RTT工具JSON (*.json)|*.json",
                DefaultExt = "json", AddExtension = true, OverwritePrompt = true,
                FileName = currentPath == null ? "definitions.json" : System.IO.Path.GetFileName(currentPath)
            }) return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
        }
        public virtual bool ConfirmImpact(IWin32Window owner, string message) {
            return MessageBox.Show(owner, message + "\r\n应用此次修改？取消将保留原定义。", "共享定义修改", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }
        public virtual bool ConfirmAction(IWin32Window owner, string message) {
            return MessageBox.Show(owner, message, "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }
        public virtual void Error(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
