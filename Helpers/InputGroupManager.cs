using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UBCS2_A.Models;
using UBCS2_A.Services; // [QUAN TRỌNG] Cần namespace này để gọi Firebase

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [MANAGER] Quản lý khu vực nhập liệu.
    /// [UPDATE] Đã chuyển sang ComboBox và TỰ ĐỘNG LƯU tên mới lên Firebase.
    /// </summary>
    public class InputGroupManager
    {
        private readonly MatrixManager _matrixManager;
        private readonly FirebaseService _firebaseService; // [QUAN TRỌNG] Service để lưu dữ liệu

        private readonly DataGridView _dgvInput;
        private readonly Button _btnGui;

        // [UPDATE] Sử dụng ComboBox
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

        // Node lưu trữ trên Firebase
        private const string NODE_SUGGEST_SENDER = "T_System/Suggestions/Senders";
        private const string NODE_SUGGEST_RECEIVER = "T_System/Suggestions/Receivers";

        // Cache để tránh load lại những tên đã biết
        private HashSet<string> _cacheSenders = new HashSet<string>();
        private HashSet<string> _cacheReceivers = new HashSet<string>();

        public InputGroupManager(
            MatrixManager matrixManager,
            FirebaseService firebaseService, // [MỚI] Nhận service vào
            DataGridView dgvInput,
            Button btnGui,
            ComboBox cboNguoiGui, ComboBox cboNguoiNhan, // [MỚI] Nhận ComboBox
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

            Console.WriteLine("[INPUT-MGR] 🟢 Khởi tạo Manager nhập liệu (Auto-Sync).");
            SetupUI();

            // Tải danh sách tên cũ từ Firebase về
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

            // Cấu hình ComboBox gợi ý
            SetupOneCombo(_cboNguoiGui);
            SetupOneCombo(_cboNguoiNhan);
        }

        private void SetupOneCombo(ComboBox cbo)
        {
            cbo.Items.Clear();
            cbo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cbo.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        // --- PHẦN ĐỒNG BỘ FIREBASE ---
        private async void LoadSuggestionsAsync()
        {
            if (_firebaseService == null) return;
            try
            {
                var senders = await _firebaseService.GetDataAsync<Dictionary<string, bool>>(NODE_SUGGEST_SENDER);
                if (senders != null) UpdateComboList(_cboNguoiGui, _cacheSenders, senders.Keys.ToList());

                var receivers = await _firebaseService.GetDataAsync<Dictionary<string, bool>>(NODE_SUGGEST_RECEIVER);
                if (receivers != null) UpdateComboList(_cboNguoiNhan, _cacheReceivers, receivers.Keys.ToList());

                // Lắng nghe realtime (nếu máy khác nhập thì máy này cũng thấy)
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

        private void Firebase_OnDataChanged(object sender, FirebaseDataEventArgs e)
        {
            if (e.Path.Contains("Suggestions"))
            {
                string key = e.Key;
                if (string.IsNullOrEmpty(key)) return;

                if (e.Path.Contains("Senders")) UpdateComboList(_cboNguoiGui, _cacheSenders, new List<string> { key });
                else if (e.Path.Contains("Receivers")) UpdateComboList(_cboNguoiNhan, _cacheReceivers, new List<string> { key });
            }
        }

        private void RegisterEvents()
        {
            _dgvInput.CellEndEdit += DgvInput_CellEndEdit;
            _dgvInput.RowsAdded += (s, e) => UpdateSTT();
            _dgvInput.RowsRemoved += (s, e) => UpdateSTT();
            _btnGui.Click += BtnGui_Click;
        }

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

        private void DgvInput_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 1 && e.RowIndex >= 0)
            {
                var cell = _dgvInput.Rows[e.RowIndex].Cells[1];
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
                newCol.GioGui = DateTime.Now.ToString("HH:mm dd/MM/yyyy");

                // [UPDATE] Lấy Text từ ComboBox
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

                if (_matrixManager != null)
                {
                    _matrixManager.AddNewColumn(newCol);

                    // [QUAN TRỌNG] Lưu tên mới vào Firebase để lần sau gợi ý
                    SaveSuggestionAsync(newCol.NguoiGui, NODE_SUGGEST_SENDER, _cacheSenders);
                    SaveSuggestionAsync(newCol.NguoiNhan, NODE_SUGGEST_RECEIVER, _cacheReceivers);

                    ClearForm();
                }
                else
                {
                    MessageBox.Show("Lỗi hệ thống: Không tìm thấy MatrixManager.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message);
            }
        }

        // [HÀM MỚI] Thực hiện lưu lên Firebase
        private async void SaveSuggestionAsync(string value, string nodePath, HashSet<string> cache)
        {
            if (string.IsNullOrWhiteSpace(value) || _firebaseService == null) return;

            // Nếu tên này chưa có trong Cache -> Lưu lên Firebase
            if (!cache.Contains(value))
            {
                try
                {
                    // Lưu dạng: T_System/Suggestions/Senders/BacSiTuan = true
                    await _firebaseService.UpdateDataAsync($"{nodePath}/{value}", true);
                    Console.WriteLine($"[INPUT-SAVE] 💾 Đã lưu gợi ý mới: {value}");

                    // Add luôn vào cache để không lưu lại lần 2 trong phiên này
                    cache.Add(value);
                }
                catch (Exception ex) { Console.WriteLine($"[INPUT-SAVE-ERR] {ex.Message}"); }
            }
        }

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