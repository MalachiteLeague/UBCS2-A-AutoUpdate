using System;
using System.Windows.Forms;
using UBCS2_A.Models;
using UBCS2_A.Services;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [MANAGER] Quản lý logic giao diện Giao Task.
    /// [REFACTOR] Tách Function: Event chỉ gọi hàm, Logic tách riêng.
    /// </summary>
    public class TaskManager
    {
        private readonly TextBox _txtScan;
        private readonly TaskContext _context;

        public TaskManager(TextBox txtScan, TaskContext context)
        {
            _txtScan = txtScan;
            _context = context;

            Console.WriteLine("[TASK-MGR] 🟢 Khởi tạo TaskManager (Refactored).");
            RegisterEvents();
        }

        // =============================================================
        // [1] KHU VỰC EVENT - CHỈ GỌI HÀM
        // =============================================================

        private void RegisterEvents()
        {
            _txtScan.KeyDown += TxtScan_KeyDown;
        }

        private void TxtScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Gọi hàm logic
                ProcessScanInput(_txtScan.Text.Trim());

                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        // =============================================================
        // [2] KHU VỰC NGHIỆP VỤ (PRIVATE HELPERS)
        // =============================================================

        private void ProcessScanInput(string sid)
        {
            if (string.IsNullOrEmpty(sid)) return;

            Console.WriteLine($"[TASK-SCAN] 🔫 Đã quét SID: '{sid}'. Đang mở Popup...");

            // 1. Mở Popup và lấy dữ liệu
            var taskData = GetUserTaskInput(sid);

            // 2. Nếu người dùng nhập xong (có dữ liệu) -> Lưu
            if (taskData != null)
            {
                CreateAndSaveTask(taskData);
            }
            else
            {
                Console.WriteLine("[TASK-ACTION] ❌ User hủy giao việc.");
            }

            // 3. Dọn dẹp ô nhập để sẵn sàng mã tiếp theo
            ResetScanInput();
        }

        private TaskModel GetUserTaskInput(string sid)
        {
            using (var dialog = new TaskInputDialog(sid))
            {
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK)
                {
                    string area = dialog.CboKhuVuc.SelectedItem?.ToString() ?? "Khác";
                    string content = dialog.TxtNoiDung.Text.Trim();

                    Console.WriteLine($"[TASK-ACTION] ✅ User xác nhận: Giao cho {area} - Nội dung: {content}");

                    return new TaskModel()
                    {
                        ThoiGian = DateTime.Now.ToString("HH:mm dd/MM"),
                        SID = sid,
                        KhuVucNhan = area,
                        NoiDung = content,
                        TrangThai = 0
                    };
                }
            }
            return null;
        }

        private void CreateAndSaveTask(TaskModel task)
        {
            if (task != null && _context != null)
            {
                _context.AddNewTask(task);
            }
        }

        private void ResetScanInput()
        {
            _txtScan.Clear();
            _txtScan.Focus();
        }
    }
}