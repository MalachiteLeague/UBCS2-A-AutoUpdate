using System;
using System.Drawing;
using System.Windows.Forms;
using System.Media;

namespace UBCS2_A
{
    public class FrmCanhBao : Form
    {
        private System.Windows.Forms.Timer timerBlink;
        private bool isRed = true;
        private Label lblTitle, lblInfo;
        private Button btnOk;

        // Thêm tham số isKhan
        public FrmCanhBao(string sid, string moTa, string tuKhuVuc, bool isKhan)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(600, 350);
            this.TopMost = true;
            this.ShowInTaskbar = false;

            // Nếu Khẩn -> Chớp nhanh (300ms)
            timerBlink = new System.Windows.Forms.Timer { Interval = isKhan ? 300 : 500 };

            timerBlink.Tick += (s, e) => {
                if (isRed)
                {
                    this.BackColor = isKhan ? Color.Red : Color.OrangeRed; // Khẩn -> Đỏ đậm
                    lblTitle.ForeColor = Color.Yellow;
                    lblInfo.ForeColor = Color.White;
                }
                else
                {
                    this.BackColor = Color.Yellow;
                    lblTitle.ForeColor = Color.Red;
                    lblInfo.ForeColor = Color.Black;
                }
                isRed = !isRed;
            };
            timerBlink.Start();

            string titleText = isKhan ? "⚡ CẤP CỨU / MẪU KHẨN ⚡" : "⚠ CÓ NHIỆM VỤ MỚI! ⚠";

            lblTitle = new Label
            {
                Text = titleText,
                Dock = DockStyle.Top,
                Height = 80,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 24, FontStyle.Bold)
            };

            string khanText = isKhan ? "[ƯU TIÊN GẤP]\n" : "";
            lblInfo = new Label
            {
                Text = $"{khanText}Từ: {tuKhuVuc}\nSID: {sid}\n\nNội dung: {moTa}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(150, 10, 150, 10), BackColor = Color.Transparent };
            btnOk = new Button
            {
                Text = "ĐÃ THẤY - TẮT THÔNG BÁO",
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) => {
                timerBlink.Stop();
                this.Close();
            };

            pnlBottom.Controls.Add(btnOk);
            this.Controls.Add(lblInfo);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(lblTitle);
        }
    }
}