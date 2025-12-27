using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace UBCS2_A
{
    public class FrmGiaoViec : Form
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedArea { get; private set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Description { get; private set; } = "";

        // --- THUỘC TÍNH MỚI ---
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsEmergency { get; private set; } = false;
        // -----------------------

        private ComboBox cbArea;
        private TextBox txtDesc;
        private CheckBox chkKhan; // Checkbox mới
        private Button btnOk;

        public FrmGiaoViec(string sid, string currentArea)
        {
            this.Text = $"Giao nhiệm vụ cho SID: {sid}";
            this.Size = new Size(400, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 10);

            Label lbl1 = new Label { Text = "Chọn nơi nhận:", Top = 20, Left = 20, AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            cbArea = new ComboBox { Top = 45, Left = 20, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
            cbArea.Items.AddRange(new string[] {
                "Sinh hóa Tầng 1", "Huyết học Tầng 1", "Miễn dịch Tầng 1",
                "Huyết học Tầng 3", "SH-MD Tầng 3"
            });
            if (cbArea.Items.Contains(currentArea)) cbArea.Items.Remove(currentArea);
            if (cbArea.Items.Count > 0) cbArea.SelectedIndex = 0;

            Label lbl2 = new Label { Text = "Mô tả / Ghi chú:", Top = 90, Left = 20, AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            txtDesc = new TextBox { Top = 115, Left = 20, Width = 340, Height = 60, Multiline = true };
            this.Shown += (s, e) => txtDesc.Focus();

            // --- CHECKBOX KHẨN ---
            chkKhan = new CheckBox
            {
                Text = "⚡ MẪU KHẨN / CẤP CỨU (STAT)",
                Top = 185,
                Left = 20,
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Red
            };

            btnOk = new Button
            {
                Text = "XÁC NHẬN GIAO",
                Top = 225,
                Left = 100,
                Width = 180,
                Height = 40,
                DialogResult = DialogResult.OK,
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btnOk.Click += (s, e) => {
                if (cbArea.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn nơi nhận!", "Thiếu thông tin");
                    this.DialogResult = DialogResult.None;
                    return;
                }
                SelectedArea = cbArea.SelectedItem.ToString();
                Description = txtDesc.Text;
                IsEmergency = chkKhan.Checked; // Lưu trạng thái
            };

            this.Controls.AddRange(new Control[] { lbl1, cbArea, lbl2, txtDesc, chkKhan, btnOk });
            this.AcceptButton = btnOk;
        }
    }
}