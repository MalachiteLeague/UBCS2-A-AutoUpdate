using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [MANAGER] Quản lý khu vực nhập liệu (Góc trái màn hình).
    /// Nhiệm vụ: Xử lý quét mã, tự động thêm hậu tố (Suffix) và đóng gói gửi đi.
    /// </summary>
    public class InputGroupManager
    {
        // 1. Các tham chiếu UI
        private readonly MatrixManager _matrixManager; // Cầu nối để gửi dữ liệu
        private readonly DataGridView _dgvInput;
        private readonly Button _btnGui;

        private readonly TextBox _txtNguoiGui;
        private readonly TextBox _txtNguoiNhan;
        private readonly TextBox _txtCarrier;
        private readonly ComboBox _cboLine;

        // 2. Các Radio Button (Chọn loại mẫu)
        private readonly RadioButton _radKhac;
        private readonly RadioButton _radDen;
        private readonly RadioButton _radDo;
        private readonly RadioButton _radXanhLa;
        private readonly RadioButton _radXanhDuong;
        private readonly RadioButton _radNuocTieu;
        private readonly RadioButton _radPCD;

        public InputGroupManager(
            MatrixManager matrixManager,
            DataGridView dgvInput,
            Button btnGui,
            TextBox txtNguoiGui, TextBox txtNguoiNhan, TextBox txtCarrier, ComboBox cboLine,
            RadioButton rKhac, RadioButton rDen, RadioButton rDo,
            RadioButton rXla, RadioButton rXdu, RadioButton rNt, RadioButton rPcd)
        {
            _matrixManager = matrixManager;
            _dgvInput = dgvInput;
            _btnGui = btnGui;
            _txtNguoiGui = txtNguoiGui;
            _txtNguoiNhan = txtNguoiNhan;
            _txtCarrier = txtCarrier;
            _cboLine = cboLine;

            _radKhac = rKhac;
            _radDen = rDen;
            _radDo = rDo;
            _radXanhLa = rXla;
            _radXanhDuong = rXdu;
            _radNuocTieu = rNt;
            _radPCD = rPcd;

            Console.WriteLine("[INPUT-MGR] 🟢 Khởi tạo Manager quản lý nhập liệu.");
            SetupUI();
            RegisterEvents();
        }

        private void SetupUI()
        {
            _cboLine.Items.Clear();
            _cboLine.Items.AddRange(new string[] { "T1->T3", "T3->T1" });
            if (_cboLine.Items.Count > 0) _cboLine.SelectedIndex = 0;

            _dgvInput.Columns.Clear();
            _dgvInput.AutoGenerateColumns = false;
            _dgvInput.AllowUserToAddRows = true;
            _dgvInput.RowHeadersVisible = false;

            var colSTT = new DataGridViewTextBoxColumn();
            colSTT.HeaderText = "STT";
            colSTT.Width = 40;
            colSTT.ReadOnly = true;
            colSTT.DefaultCellStyle.BackColor = Color.LightGray;
            colSTT.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvInput.Columns.Add(colSTT);

            var colSID = new DataGridViewTextBoxColumn();
            colSID.HeaderText = "Mã Xét Nghiệm (SID)";
            colSID.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dgvInput.Columns.Add(colSID);

            _radKhac.Checked = true; // Mặc định

            Console.WriteLine("[INPUT-MGR] 🛠️ Đã cấu hình UI xong.");
        }

        private void RegisterEvents()
        {
            _dgvInput.CellEndEdit += DgvInput_CellEndEdit;
            _dgvInput.RowsAdded += (s, e) => UpdateSTT();
            _dgvInput.RowsRemoved += (s, e) => UpdateSTT();
            _btnGui.Click += BtnGui_Click;
        }

        // ==========================================================
        // [QUAN TRỌNG] LOGIC HẬU TỐ (SUFFIX) ĐÃ CẬP NHẬT
        // ==========================================================
        private string GetCurrentSuffix()
        {
            if (_radDen.Checked) return " Đen";       // Có khoảng trắng phía trước
            if (_radDo.Checked) return " Đỏ";
            if (_radXanhLa.Checked) return " X.Lá";
            if (_radXanhDuong.Checked) return " X.Dương";
            if (_radNuocTieu.Checked) return " NT";
            if (_radPCD.Checked) return " PCĐ";

            return ""; // _radKhac hoặc chưa chọn gì
        }

        private void DgvInput_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Chỉ xử lý cột SID (Index 1)
            if (e.ColumnIndex == 1 && e.RowIndex >= 0)
            {
                var cell = _dgvInput.Rows[e.RowIndex].Cells[1];
                string rawValue = cell.Value?.ToString().Trim();

                if (!string.IsNullOrEmpty(rawValue))
                {
                    string suffix = GetCurrentSuffix();

                    // Logic: Chỉ thêm nếu có hậu tố VÀ chuỗi chưa có hậu tố đó
                    // Sử dụng StringComparison.OrdinalIgnoreCase để không phân biệt hoa thường
                    if (!string.IsNullOrEmpty(suffix) && !rawValue.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        string newValue = rawValue + suffix;
                        cell.Value = newValue;
                        Console.WriteLine($"[INPUT-SCAN] 🔫 Auto Suffix: '{rawValue}' -> '{newValue}'");
                    }
                }
            }
        }

        private void BtnGui_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[INPUT-SEND] 🚀 Người dùng nhấn Gửi...");

            if (_dgvInput.Rows.Count <= 1 && string.IsNullOrWhiteSpace(_dgvInput.Rows[0].Cells[1].Value?.ToString()))
            {
                MessageBox.Show("Chưa có mã xét nghiệm nào được quét!", "Nhắc nhở", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var newCol = new CotDuLieuModel();
                // Format ngày giờ đầy đủ
                newCol.GioGui = DateTime.Now.ToString("HH:mm dd/MM/yyyy");
                newCol.NguoiGui = _txtNguoiGui.Text.Trim();
                newCol.NguoiNhan = _txtNguoiNhan.Text.Trim();
                newCol.Carrier = _txtCarrier.Text.Trim();
                newCol.Line = _cboLine.SelectedItem?.ToString() ?? "";
                newCol.Status = 0; // Mặc định là chưa nhận
                newCol.Sids = new List<string>();

                foreach (DataGridViewRow row in _dgvInput.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        string sid = row.Cells[1].Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(sid))
                        {
                            newCol.Sids.Add(sid);
                        }
                    }
                }

                Console.WriteLine($"[INPUT-SEND] 📦 Đóng gói {newCol.Sids.Count} mẫu. Gửi sang Matrix...");

                if (_matrixManager != null)
                {
                    _matrixManager.AddNewColumn(newCol);
                    ClearForm();
                }
                else
                {
                    Console.WriteLine("[INPUT-ERR] ❌ MatrixManager bị Null!");
                    MessageBox.Show("Lỗi hệ thống: Không tìm thấy MatrixManager.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INPUT-ERR] ❌ Lỗi khi gửi: {ex.Message}");
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message);
            }
        }

        private void ClearForm()
        {
            _txtCarrier.Clear();
            _dgvInput.Rows.Clear();
            _radKhac.Checked = true; // Reset về Khác
            _txtCarrier.Focus();
            Console.WriteLine("[INPUT-MGR] ✨ Đã dọn dẹp Form.");
        }

        private void UpdateSTT()
        {
            for (int i = 0; i < _dgvInput.Rows.Count; i++)
            {
                if (!_dgvInput.Rows[i].IsNewRow)
                    _dgvInput.Rows[i].Cells[0].Value = (i + 1).ToString();
            }
        }
    }
}