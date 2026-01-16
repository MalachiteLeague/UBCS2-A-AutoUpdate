using System;
using System.ComponentModel;
using System.Drawing; // Thư viện để vẽ giao diện (Point, Size, Color...)
using System.Windows.Forms; // Thư viện Winform chuẩn

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [UI] Form Popup nhập liệu khi giao việc.
    /// Form này được tạo hoàn toàn bằng code (không cần Designer).
    /// </summary>
    public class TaskInputDialog : Form
    {
        // --- CÁC CONTROL CÔNG KHAI (Để bên ngoài lấy dữ liệu) ---
        // [FIX LỖI WFO1000] Thêm dòng này trước mỗi Property
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ComboBox CboKhuVuc { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextBox TxtNoiDung { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Button BtnOK { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Button BtnCancel { get; private set; }

        /// <summary>
        /// Khởi tạo Form với mã SID đã quét được.
        /// </summary>
        /// <param name="sid">Mã xét nghiệm vừa quét</param>
        public TaskInputDialog(string sid)
        {
            // 1. Cấu hình cơ bản cho Form
            this.Text = "Giao Việc Nhanh"; // Tiêu đề cửa sổ
            this.Size = new Size(450, 280); // Kích thước: Rộng 450, Cao 280
            this.StartPosition = FormStartPosition.CenterParent; // Hiện giữa form cha
            this.FormBorderStyle = FormBorderStyle.FixedDialog; // Không cho kéo giãn
            this.MaximizeBox = false; // Ẩn nút phóng to
            this.MinimizeBox = false; // Ẩn nút thu nhỏ
            this.BackColor = Color.WhiteSmoke; // Màu nền nhẹ

            Console.WriteLine($"[TASK-UI] 🖼️ Đang khởi tạo Popup cho SID: {sid}");

            // 2. Gọi hàm vẽ các chi tiết lên Form
            InitializeControls(sid);
        }

        /// <summary>
        /// Hàm tự động vẽ các nút, ô nhập liệu lên Form.
        /// </summary>
        private void InitializeControls(string sid)
        {
            // Các thông số căn chỉnh vị trí
            int padding = 20;   // Khoảng cách lề
            int currentY = 20;  // Vị trí dòng hiện tại (tăng dần xuống dưới)
            int labelW = 100;   // Chiều rộng nhãn
            int inputW = 280;   // Chiều rộng ô nhập

            // --- A. HIỂN THỊ SID (Header) ---
            var lblTitle = new Label()
            {
                Text = $"SID: {sid}",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                AutoSize = true,
                Location = new Point(padding, currentY)
            };
            this.Controls.Add(lblTitle);

            currentY += 45; // Xuống dòng

            // --- B. CHỌN KHU VỰC (COMBOBOX) ---
            var lblArea = new Label()
            {
                Text = "Nơi nhận:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(padding, currentY + 3), // +3 để căn giữa dòng với ComboBox
                Width = labelW
            };
            this.Controls.Add(lblArea);

            CboKhuVuc = new ComboBox()
            {
                Location = new Point(padding + labelW, currentY),
                Width = inputW,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList // Chỉ cho chọn, không cho gõ linh tinh
            };
            // Thêm danh sách các phòng ban (Khớp với hệ thống phân quyền của bạn)
            // Cập nhật danh sách khớp 100% với Form1/AuthManager
            CboKhuVuc.Items.AddRange(new string[] {
                "Huyết học T1",
                "Sinh hóa T1",
                "Miễn dịch T1",
                "Huyết học T3",
                "SH-MD T3",
                
                // Tách Hành Chánh ra làm 2 để khớp với người nhận
                "Hành Chánh T1",
                "Hành Chánh T3",

                "Khác"
            });
            if (CboKhuVuc.Items.Count > 0) CboKhuVuc.SelectedIndex = 0; // Mặc định chọn cái đầu
            this.Controls.Add(CboKhuVuc);

            currentY += 45; // Xuống dòng

            // --- C. NHẬP NỘI DUNG (TEXTBOX) ---
            var lblContent = new Label()
            {
                Text = "Nội dung:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(padding, currentY + 3),
                Width = labelW
            };
            this.Controls.Add(lblContent);

            TxtNoiDung = new TextBox()
            {
                Location = new Point(padding + labelW, currentY),
                Width = inputW,
                Font = new Font("Segoe UI", 10)
            };
            this.Controls.Add(TxtNoiDung);

            currentY += 60; // Xuống dòng xa hơn chút để đặt nút

            // --- D. CÁC NÚT BẤM (BUTTONS) ---

            // Nút OK (Giao Việc)
            BtnOK = new Button()
            {
                Text = "Giao Việc",
                DialogResult = DialogResult.OK, // Quan trọng: Bấm nút này trả về OK
                Location = new Point(130, currentY),
                Width = 110,
                Height = 35,
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat // Phẳng cho đẹp
            };
            BtnOK.FlatAppearance.BorderSize = 0;
            this.Controls.Add(BtnOK);

            // Nút Cancel (Hủy)
            BtnCancel = new Button()
            {
                Text = "Hủy Bỏ",
                DialogResult = DialogResult.Cancel, // Quan trọng: Bấm nút này trả về Cancel
                Location = new Point(250, currentY),
                Width = 90,
                Height = 35,
                BackColor = Color.LightGray,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            BtnCancel.FlatAppearance.BorderSize = 0;
            this.Controls.Add(BtnCancel);

            // --- E. CẤU HÌNH PHÍM TẮT ---
            this.AcceptButton = BtnOK;     // Bấm Enter = Click OK
            this.CancelButton = BtnCancel; // Bấm ESC = Click Cancel

            // Sự kiện: Khi form hiện lên, tự động đặt con trỏ chuột vào ô Nội dung
            this.Load += (s, e) => TxtNoiDung.Focus();
        }
    }
}