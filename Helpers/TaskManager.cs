using System;
using System.Windows.Forms;
using UBCS2_A.Models;
using UBCS2_A.Services;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [MANAGER] Quản lý logic giao diện Giao Task.
    /// Nhiệm vụ:
    /// 1. Lắng nghe ô Textbox quét mã (txtTaskSID).
    /// 2. Hiển thị Popup nhập liệu (TaskInputDialog).
    /// 3. Gọi Context để lưu Task.
    /// </summary>
    public class TaskManager
    {
        private readonly TextBox _txtScan;   // Ô để quét mã
        private readonly TaskContext _context; // Nơi xử lý dữ liệu

        public TaskManager(TextBox txtScan, TaskContext context)
        {
            _txtScan = txtScan;
            _context = context;

            Console.WriteLine("[TASK-MGR] 🟢 Khởi tạo TaskManager.");
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            // Bắt sự kiện phím Enter trên ô quét mã
            _txtScan.KeyDown += TxtScan_KeyDown;
        }

        private void TxtScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string sid = _txtScan.Text.Trim();

                if (!string.IsNullOrEmpty(sid))
                {
                    Console.WriteLine($"[TASK-SCAN] 🔫 Đã quét SID: '{sid}'. Đang mở Popup...");

                    // 1. Mở cửa sổ Popup
                    ShowTaskDialog(sid);

                    // 2. Clear ô quét và Focus lại để sẵn sàng cho mẫu tiếp theo
                    _txtScan.Clear();
                    _txtScan.Focus();
                }

                // Chặn tiếng "Beep" khó chịu của Windows khi nhấn Enter
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void ShowTaskDialog(string sid)
        {
            // Tạo form popup và hiển thị
            using (var dialog = new TaskInputDialog(sid))
            {
                // ShowDialog: Hiện form và chặn không cho thao tác form chính đến khi đóng
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK)
                {
                    // Người dùng bấm "Giao Việc"
                    string area = dialog.CboKhuVuc.SelectedItem?.ToString() ?? "Khác";
                    string content = dialog.TxtNoiDung.Text.Trim();

                    Console.WriteLine($"[TASK-ACTION] ✅ User xác nhận: Giao cho {area} - Nội dung: {content}");

                    // Tạo Model Task
                    var task = new TaskModel()
                    {
                        ThoiGian = DateTime.Now.ToString("HH:mm dd/MM"),
                        SID = sid,
                        KhuVucNhan = area,
                        NoiDung = content,
                        TrangThai = 0 // Mới
                    };

                    // Gọi Context để xử lý lưu trữ
                    _context.AddNewTask(task);
                }
                else
                {
                    // Người dùng bấm "Hủy" hoặc tắt form
                    Console.WriteLine("[TASK-ACTION] ❌ User hủy giao việc.");
                }
            }
        }
    }
}