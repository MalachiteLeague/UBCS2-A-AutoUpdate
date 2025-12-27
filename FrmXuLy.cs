using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace UBCS2_A
{
    public class FrmXuLy : Form
    {
        // Kết quả trả về
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string KetQua { get; private set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsThatBai { get; private set; } = false; // Đánh dấu là thất bại

        private TextBox txtNoiDung;
        private Button btnXong, btnHuy;

        public FrmXuLy(string noiDungCu)
        {
            // 1. Setup Form
            this.Text = "Xác nhận kết quả";
            this.Size = new Size(400, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false; this.MinimizeBox = false;

            // 2. Ô nhập liệu (Mặc định = Nội dung cũ + đã hoàn thành)
            Label lbl = new Label { Text = "Nội dung / Kết quả xử lý:", Top = 15, Left = 15, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

            txtNoiDung = new TextBox
            {
                Top = 40,
                Left = 15,
                Width = 350,
                Height = 100,
                Multiline = true,
                Font = new Font("Segoe UI", 10),
                Text = noiDungCu + " - Đã hoàn thành" // <-- Theo yêu cầu của bạn
            };

            // 3. Nút HOÀN THÀNH (Xanh)
            btnXong = new Button
            {
                Text = "✅ ĐÃ XONG",
                Top = 150,
                Left = 15,
                Width = 170,
                Height = 40,
                BackColor = Color.ForestGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnXong.Click += (s, e) => {
                IsThatBai = false; // Thành công
                KetQua = txtNoiDung.Text;
            };

            // 4. Nút KHÔNG ĐƯỢC (Đỏ)
            btnHuy = new Button
            {
                Text = "❌ KHÔNG LÀM ĐƯỢC",
                Top = 150,
                Left = 200,
                Width = 170,
                Height = 40,
                BackColor = Color.OrangeRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnHuy.Click += (s, e) => {
                IsThatBai = true; // Thất bại
                KetQua = txtNoiDung.Text; // Lấy nội dung đã chỉnh sửa
            };

            this.Controls.AddRange(new Control[] { lbl, txtNoiDung, btnXong, btnHuy });
        }
    }
}