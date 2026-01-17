using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UBCS2_A.Models;
using UBCS2_A.Services;

namespace UBCS2_A.Helpers
{
    public class InputGroupManager
    {
        private readonly MatrixManager _matrixManager;
        private readonly FirebaseService _firebaseService;

        private readonly DataGridView _dgvInput;
        private readonly Button _btnGui;

        private readonly ComboBox _cboNguoiGui;
        private readonly ComboBox _cboNguoiNhan;
        private readonly TextBox _txtCarrier;
        private readonly ComboBox _cboLine;

        // Radio Buttons
        private readonly RadioButton _radKhac;
        private readonly RadioButton _radDen;
        private readonly RadioButton _radDo;
        private readonly RadioButton _radXanhLa;
        private readonly RadioButton _radXanhDuong;
        private readonly RadioButton _radNuocTieu;
        private readonly RadioButton _radPCD;

        // Firebase Config
        private const string NODE_SUGGEST_SENDER = "T_System/Suggestions/Senders";
        private const string NODE_SUGGEST_RECEIVER = "T_System/Suggestions/Receivers";
        private HashSet<string> _cacheSenders = new HashSet<string>();
        private HashSet<string> _cacheReceivers = new HashSet<string>();

        public InputGroupManager(
            MatrixManager matrixManager,
            FirebaseService firebaseService,
            DataGridView dgvInput,
            Button btnGui,
            ComboBox cboNguoiGui, ComboBox cboNguoiNhan,
            TextBox txtCarrier, ComboBox cboLine,
            RadioButton rKhac, RadioButton rDen, RadioButton rDo,
            RadioButton rXla, RadioButton rXdu, RadioButton rNt, RadioButton rPcd)
        {
            _matrixManager = matrixManager;
            _firebaseService = firebaseService;
            _dgvInput = dgvInput;
            _btnGui = btnGui;
            _cboNguoiGui = cboNguoiGui;
            _cboNguoiNhan = cboNguoiNhan;
            _txtCarrier = txtCarrier;
            _cboLine = cboLine;

            _radKhac = rKhac;
            _radDen = rDen;
            _radDo = rDo;
            _radXanhLa = rXla;
            _radXanhDuong = rXdu;
            _radNuocTieu = rNt;
            _radPCD = rPcd;

            Console.WriteLine("[INPUT-MGR] 🟢 Khởi tạo Manager nhập liệu (Refactored).");
            SetupUI();
            LoadSuggestionsAsync();
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

            _radKhac.Checked = true;

            SetupOneCombo(_cboNguoiGui);
            SetupOneCombo(_cboNguoiNhan);
        }

        private void SetupOneCombo(ComboBox cbo)
        {
            cbo.Items.Clear();
            cbo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cbo.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        private async void LoadSuggestionsAsync()
        {
            if (_firebaseService == null) return;
            try
            {
                var senders = await _firebaseService.GetDataAsync<Dictionary<string, bool>>(NODE_SUGGEST_SENDER);
                if (senders != null) UpdateComboList(_cboNguoiGui, _cacheSenders, senders.Keys.ToList());

                var receivers = await _firebaseService.GetDataAsync<Dictionary<string, bool>>(NODE_SUGGEST_RECEIVER);
                if (receivers != null) UpdateComboList(_cboNguoiNhan, _cacheReceivers, receivers.Keys.ToList());

                // Đăng ký sự kiện Realtime (Refactored: Sự kiện này chỉ gọi hàm logic)
                _firebaseService.OnDataChanged += Firebase_OnDataChanged;
            }
            catch (Exception ex) { Console.WriteLine($"[INPUT-SYNC-ERR] {ex.Message}"); }
        }

        private void UpdateComboList(ComboBox cbo, HashSet<string> cache, List<string> newItems)
        {
            if (cbo.InvokeRequired) { cbo.Invoke(new Action(() => UpdateComboList(cbo, cache, newItems))); return; }
            foreach (var item in newItems)
            {
                if (!string.IsNullOrWhiteSpace(item) && !cache.Contains(item))
                {
                    cache.Add(item);
                    cbo.Items.Add(item);
                }
            }
        }

        // =============================================================
        // [KHU VỰC EVENT] - CHỈ GỌI HÀM, KHÔNG CHỨA LOGIC
        // =============================================================

        private void RegisterEvents()
        {
            _dgvInput.CellEndEdit += DgvInput_CellEndEdit;
            _dgvInput.RowsAdded += (s, e) => UpdateSTT();
            _dgvInput.RowsRemoved += (s, e) => UpdateSTT();
            _btnGui.Click += BtnGui_Click;
        }

        // Sự kiện 1: Firebase thay đổi -> Gọi xử lý cập nhật
        private void Firebase_OnDataChanged(object sender, FirebaseDataEventArgs e)
        {
            ProcessRealtimeUpdate(e.Path, e.Key);
        }

        // Sự kiện 2: Sửa ô Grid -> Gọi xử lý hậu tố
        private void DgvInput_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            ProcessAutoSuffix(e.RowIndex, e.ColumnIndex);
        }

        // Sự kiện 3: Bấm nút Gửi -> Gọi quy trình gửi
        private void BtnGui_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[INPUT-SEND] 🚀 Người dùng nhấn Gửi...");

            if (!IsValidInput()) return;

            try
            {
                var packageData = CreatePackageData();
                ProcessSending(packageData);
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message);
            }
        }

        // =============================================================
        // [KHU VỰC NGHIỆP VỤ RIÊNG] - LOGIC ĐƯỢC TÁCH BIỆT
        // =============================================================

        #region Logic Xử Lý Realtime Firebase
        private void ProcessRealtimeUpdate(string path, string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (path.Contains("Suggestions"))
            {
                if (path.Contains("Senders"))
                    UpdateComboList(_cboNguoiGui, _cacheSenders, new List<string> { key });
                else if (path.Contains("Receivers"))
                    UpdateComboList(_cboNguoiNhan, _cacheReceivers, new List<string> { key });
            }
        }
        #endregion

        #region Logic Xử Lý Hậu Tố (Auto Suffix)
        private string GetCurrentSuffix()
        {
            if (_radDen.Checked) return " Đen";
            if (_radDo.Checked) return " Đỏ";
            if (_radXanhLa.Checked) return " X.Lá";
            if (_radXanhDuong.Checked) return " X.Dương";
            if (_radNuocTieu.Checked) return " NT";
            if (_radPCD.Checked) return " PCĐ";
            return "";
        }

        private void ProcessAutoSuffix(int rowIndex, int colIndex)
        {
            // Chỉ xử lý cột SID (Index 1)
            if (colIndex != 1 || rowIndex < 0) return;

            var cell = _dgvInput.Rows[rowIndex].Cells[1];
            string rawValue = cell.Value?.ToString().Trim();

            if (!string.IsNullOrEmpty(rawValue))
            {
                string suffix = GetCurrentSuffix();
                if (!string.IsNullOrEmpty(suffix) && !rawValue.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    string newValue = rawValue + suffix;
                    cell.Value = newValue;
                    Console.WriteLine($"[INPUT-SCAN] 🔫 Auto Suffix: '{rawValue}' -> '{newValue}'");
                }
            }
        }
        #endregion

        #region Logic Gửi Hàng (Validation - Creation - Sending)

        // 1. Kiểm tra lỗi
        private bool IsValidInput()
        {
            if (_dgvInput.Rows.Count <= 1 && string.IsNullOrWhiteSpace(_dgvInput.Rows[0].Cells[1].Value?.ToString()))
            {
                MessageBox.Show("Chưa có mã xét nghiệm nào được quét!", "Nhắc nhở", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        // 2. Tạo dữ liệu từ UI
        private CotDuLieuModel CreatePackageData()
        {
            var newCol = new CotDuLieuModel();
            newCol.GioGui = DateTime.Now.ToString("HH:mm dd/MM/yyyy");
            newCol.NguoiGui = _cboNguoiGui.Text.Trim();
            newCol.NguoiNhan = _cboNguoiNhan.Text.Trim();
            newCol.Carrier = _txtCarrier.Text.Trim();
            newCol.Line = _cboLine.SelectedItem?.ToString() ?? "";
            newCol.Status = 0;
            newCol.Sids = new List<string>();

            foreach (DataGridViewRow row in _dgvInput.Rows)
            {
                if (!row.IsNewRow)
                {
                    string sid = row.Cells[1].Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(sid)) newCol.Sids.Add(sid);
                }
            }
            return newCol;
        }

        // 3. Gửi đi (Matrix + Firebase Save)
        private void ProcessSending(CotDuLieuModel data)
        {
            if (_matrixManager != null)
            {
                _matrixManager.AddNewColumn(data);

                // Lưu tên người dùng vào Firebase để gợi ý lần sau
                SaveSuggestionAsync(data.NguoiGui, NODE_SUGGEST_SENDER, _cacheSenders);
                SaveSuggestionAsync(data.NguoiNhan, NODE_SUGGEST_RECEIVER, _cacheReceivers);
            }
            else
            {
                throw new Exception("Lỗi hệ thống: Không tìm thấy MatrixManager.");
            }
        }

        private async void SaveSuggestionAsync(string value, string nodePath, HashSet<string> cache)
        {
            if (string.IsNullOrWhiteSpace(value) || _firebaseService == null) return;

            if (!cache.Contains(value))
            {
                try
                {
                    await _firebaseService.UpdateDataAsync($"{nodePath}/{value}", true);
                    Console.WriteLine($"[INPUT-SAVE] 💾 Đã lưu gợi ý mới: {value}");
                    cache.Add(value);
                }
                catch (Exception ex) { Console.WriteLine($"[INPUT-SAVE-ERR] {ex.Message}"); }
            }
        }
        #endregion

        private void ClearForm()
        {
            _txtCarrier.Clear();
            _dgvInput.Rows.Clear();
            _radKhac.Checked = true;
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