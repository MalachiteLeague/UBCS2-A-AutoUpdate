using System;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [UI] Form thông báo khẩn cấp (Popup).
    /// Đặc điểm: Luôn nổi lên trên (TopMost), nền nhấp nháy (Flashing).
    /// </summary>
    public class FlashTaskForm : Form
    {
        private System.Windows.Forms.Timer _flashTimer;
        private bool _isRed = false; // Biến cờ để đổi màu
        private TaskModel _task;

        // Sự kiện trả về khi bấm nút "Đã Nhận"
        public event Action<TaskModel> OnConfirm;

        public FlashTaskForm(TaskModel task)
        {
            _task = task;

            // 1. Cấu hình Form "Báo Động"
            this.Text = "⚠️ CÓ NHIỆM VỤ MỚI!";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen; // Hiện giữa màn hình
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow; // Khung viền nhỏ, không nút Min/Max
            this.TopMost = true; // [QUAN TRỌNG] Luôn hiện trên cùng các cửa sổ khác
            this.ShowInTaskbar = false; // Không cần hiện dưới thanh taskbar

            InitializeUI();
            StartFlashing();
        }

        private void InitializeUI()
        {
            // Label hiển thị thông tin to rõ
            var lblInfo = new Label()
            {
                Text = $"MÃ SID: {_task.SID}\n\nNội dung: {_task.NoiDung}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 100
            };
            this.Controls.Add(lblInfo);

            // Nút Xác nhận "ĐÃ NHẬN"
            var btnOk = new Button()
            {
                Text = "ĐÃ NHẬN (OK)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Height = 40,
                Width = 150,
                BackColor = Color.Gold,
                Cursor = Cursors.Hand,
                Location = new Point((this.ClientSize.Width - 150) / 2, 110) // Căn giữa
            };

            btnOk.Click += (s, e) =>
            {
                _flashTimer.Stop(); // Dừng chớp
                OnConfirm?.Invoke(_task); // Gọi sự kiện ra ngoài Context xử lý
                this.Close(); // Đóng form
            };
            this.Controls.Add(btnOk);
        }

        private void StartFlashing()
        {
            // Timer để tạo hiệu ứng chớp chớp
            _flashTimer = new System.Windows.Forms.Timer();
            _flashTimer.Interval = 500; // Chớp mỗi 0.5 giây
            _flashTimer.Tick += (s, e) =>
            {
                // Đổi màu nền luân phiên: Đỏ nhạt <-> Trắng
                if (_isRed) this.BackColor = Color.White;
                else this.BackColor = Color.LightPink;

                _isRed = !_isRed;
            };
            _flashTimer.Start();
        }
    }
}