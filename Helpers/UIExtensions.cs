using System.Windows.Forms;

namespace UBCS2_A.Helpers
{
    public static class UIExtensions
    {
        /// <summary>
        /// Tự động tìm TabPage chứa Control này và kích hoạt nó (Focus).
        /// </summary>
        public static void FocusOnTab(this Control control)
        {
            if (control == null) return;

            Control current = control.Parent;
            while (current != null)
            {
                // Nếu tìm thấy TabPage và nó đang nằm trong một TabControl
                if (current is TabPage page && page.Parent is TabControl tabControl)
                {
                    tabControl.SelectedTab = page; // Kích hoạt Tab
                    break; // Xong việc, thoát vòng lặp
                }
                current = current.Parent; // Leo lên cấp cao hơn
            }

            // Sau khi bật Tab, focus vào chính control đó
            if (control.CanFocus) control.Focus();
        }
    }
}