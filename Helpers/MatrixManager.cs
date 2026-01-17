using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    public class MatrixManager
    {
        private readonly DataGridView _dgv;
        private List<CotDuLieuModel> _dataColumns;
        private readonly object _lock = new object();
        // --- CẤU HÌNH ---
        private const int HEADER_ROWS = 5;
        private const int INITIAL_SID_ROWS = 50;
        private const int MAX_SID_ROWS = 1000;
        private const int MAX_KEEP_COLS = 200;
        private int _currentSidRows = INITIAL_SID_ROWS;
        private readonly string[] _rowLabels = new string[] {
            "Thời Gian Gửi", "Carrier", "Từ -> Đến", "Người Gửi", "Người Nhận"
        };
        private int _currentMaxId = 0;
        private bool _isBatchUpdating = false;
        private HashSet<int> _dirtyColumnIds = new HashSet<int>();

        public event Action<CotDuLieuModel> OnColumnChanged;
        public event Action<int> OnColumnDeleted;

        public MatrixManager(DataGridView dgv)
        {
            _dgv = dgv ?? throw new ArgumentNullException(nameof(dgv));
            _dataColumns = new List<CotDuLieuModel>();

            Console.WriteLine($"[MATRIX-INIT] 🟢 Khởi tạo MatrixManager. Limit: {MAX_KEEP_COLS} cols.");
            SetupGrid();
            SetupClipboard();
        }

        public List<SearchResultModel> FindSidWithLocation(string keyword)
        {
            var results = new List<SearchResultModel>();
            if (string.IsNullOrWhiteSpace(keyword)) return results;

            string upperKey = keyword.ToUpper();
            lock (_lock)
            {
                for (int colIndex = 0; colIndex < _dataColumns.Count; colIndex++)
                {
                    var colData = _dataColumns[colIndex];
                    for (int sidIndex = 0; sidIndex < colData.Sids.Count; sidIndex++)
                    {
                        string sid = colData.Sids[sidIndex];
                        if (!string.IsNullOrEmpty(sid) && sid.ToUpper().Contains(upperKey))
                        {
                            string receiverInfo = "";
                            if (colData.Status == 1 && !string.IsNullOrEmpty(colData.NguoiNhan))
                            {
                                receiverInfo = $" - Nhận: {colData.NguoiNhan}";
                            }

                            string displayText = $"[GN] {colData.Line} - {colData.GioGui} - {colData.Carrier} - Gửi: {colData.NguoiGui}{receiverInfo} - {sid}";

                            var resultItem = new SearchResultModel()
                            {
                                DisplayText = displayText,
                                TargetGrid = _dgv,
                                RowIndex = HEADER_ROWS + sidIndex,
                                ColIndex = colIndex + 1,
                                BackColor = Color.Honeydew // Màu xanh lá nhạt
                            };
                            results.Add(resultItem);
                        }
                    }
                }
            }
            return results;
        }

        private void SetupGrid()
        {
            _dgv.AutoGenerateColumns = false;
            _dgv.DataSource = null;
            _dgv.Columns.Clear();

            EnableDoubleBuffering(_dgv);
            _dgv.VirtualMode = true;
            _dgv.AllowUserToAddRows = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.RowHeadersVisible = false;
            _dgv.ColumnHeadersVisible = false;
            _dgv.BackgroundColor = Color.WhiteSmoke;
            _dgv.DefaultCellStyle.BackColor = Color.White;
            _dgv.DefaultCellStyle.ForeColor = Color.Black;
            _dgv.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;

            _dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;

            _currentSidRows = INITIAL_SID_ROWS;
            _dgv.RowCount = HEADER_ROWS + _currentSidRows;

            var colLabel = new DataGridViewTextBoxColumn();
            colLabel.Name = "Col_Labels";
            colLabel.Width = 120;
            colLabel.ReadOnly = true;
            colLabel.DefaultCellStyle.BackColor = Color.LightGray;
            colLabel.DefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);
            colLabel.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _dgv.Columns.Add(colLabel);
            if (_dgv.Columns.Count > 0) _dgv.Columns[0].Frozen = true;

            _dgv.CellValueNeeded += Dgv_CellValueNeeded;
            _dgv.CellValuePushed += Dgv_CellValuePushed;
            _dgv.CellFormatting += Dgv_CellFormatting;
            _dgv.CellClick += Dgv_CellClick;
            // [THÊM DÒNG NÀY]
            _dgv.CellPainting += Dgv_CellPainting;
            Console.WriteLine("[MATRIX-MGR] 🛠️ Grid Setup Complete.");
        }

        public void AddNewColumn(CotDuLieuModel data = null, bool isFromUser = true)
        {
            if (_dgv.InvokeRequired) { _dgv.Invoke(new Action(() => AddNewColumn(data, isFromUser))); return; }
            var newCol = data ?? new CotDuLieuModel();
            if (isFromUser) { _currentMaxId++; newCol.Id = _currentMaxId; }
            else { if (newCol.Id > _currentMaxId) _currentMaxId = newCol.Id; }

            lock (_lock) { _dataColumns.Add(newCol); }

            var dgvCol = new DataGridViewTextBoxColumn();
            dgvCol.Name = $"Col_ID_{newCol.Id}";
            dgvCol.Width = 130;
            dgvCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvCol.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _dgv.Columns.Add(dgvCol);

            _dgv.FirstDisplayedScrollingColumnIndex = _dgv.Columns.Count - 1;

            if (isFromUser) OnColumnChanged?.Invoke(newCol);
            CheckAndRemoveOldColumns();
        }

        private void CheckAndRemoveOldColumns()
        {
            while (_dataColumns.Count > MAX_KEEP_COLS)
            {
                var oldCol = _dataColumns[0];
                lock (_lock) { _dataColumns.RemoveAt(0); }
                if (_dgv.Columns.Count > 1) _dgv.Columns.RemoveAt(1);
                OnColumnDeleted?.Invoke(oldCol.Id);
                Console.WriteLine($"[MATRIX-CLEAN] 🧹 Removed Old Column ID: {oldCol.Id}");
            }
        }

        public void LoadData(List<CotDuLieuModel> newData)
        {
            if (_dgv.InvokeRequired) { _dgv.Invoke(new Action(() => LoadData(newData))); return; }
            Console.WriteLine($"[MATRIX-LOAD] 📥 Loading {newData?.Count ?? 0} columns...");
            _dataColumns.Clear(); _currentMaxId = 0;
            while (_dgv.Columns.Count > 1) _dgv.Columns.RemoveAt(1);
            if (newData != null)
            {
                var sortedData = newData.OrderBy(x => x.Id).ToList();
                foreach (var col in sortedData) AddNewColumn(col, isFromUser: false);
            }
            _dgv.Invalidate();
        }

        public void UpdateColumnById(int colId, CotDuLieuModel newData)
        {
            var existingCol = _dataColumns.FirstOrDefault(c => c.Id == colId);
            if (existingCol != null)
            {
                int index = _dataColumns.IndexOf(existingCol);
                lock (_lock) { _dataColumns[index] = newData; }
                if (_dgv.IsHandleCreated) _dgv.BeginInvoke((Action)(() => _dgv.Invalidate()));
            }
            else
            {
                if (colId > _currentMaxId) AddNewColumn(newData, isFromUser: false);
            }
        }

        public void DeleteColumnById(int colId)
        {
            if (_dgv.InvokeRequired) { _dgv.Invoke(new Action(() => DeleteColumnById(colId))); return; }
            var target = _dataColumns.FirstOrDefault(c => c.Id == colId);
            if (target != null)
            {
                int index = _dataColumns.IndexOf(target);
                lock (_lock) { _dataColumns.RemoveAt(index); }
                if (_dgv.Columns.Count > index + 1) _dgv.Columns.RemoveAt(index + 1);
                Console.WriteLine($"[MATRIX-DEL] ✂️ Deleted Column ID: {colId}");
            }
        }

        private void Dgv_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                if (e.RowIndex < HEADER_ROWS) e.Value = _rowLabels[e.RowIndex];
                else e.Value = (e.RowIndex - HEADER_ROWS + 1).ToString();
                return;
            }
            int dataColIndex = e.ColumnIndex - 1;

            // [THAY ĐỔI] Thêm lock để bảo vệ khi đọc _dataColumns
            CotDuLieuModel colData = null;
            lock (_lock)
            {
                if (dataColIndex >= 0 && dataColIndex < _dataColumns.Count)
                {
                    colData = _dataColumns[dataColIndex];
                }
            }

            if (colData != null)
            {
                if (e.RowIndex == 0) e.Value = colData.GioGui;
                else if (e.RowIndex == 1) e.Value = colData.Carrier;
                else if (e.RowIndex == 2) e.Value = colData.Line;
                else if (e.RowIndex == 3) e.Value = colData.NguoiGui;
                else if (e.RowIndex == 4)
                {
                    if (colData.Status == 0) e.Value = "📦 BẤM NHẬN";
                    else
                    {
                        string timeInfo = string.IsNullOrEmpty(colData.GioNhan) ? "" : $"({colData.GioNhan})";
                        e.Value = $"{colData.NguoiNhan}\n{timeInfo}";
                    }
                }
                else
                {
                    int sidIndex = e.RowIndex - HEADER_ROWS;
                    if (sidIndex < colData.Sids.Count) e.Value = colData.Sids[sidIndex];
                    else e.Value = "";
                }
            }
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.RowIndex < HEADER_ROWS)
            {
                e.CellStyle.BackColor = Color.AliceBlue;
                e.CellStyle.Font = new Font(_dgv.Font, FontStyle.Bold);
                return;
            }

            // [THAY ĐỔI] Thêm lock bảo vệ khi truy cập _dataColumns trong CellFormatting
            if (e.RowIndex == 4 && e.ColumnIndex > 0)
            {
                int dataIndex = e.ColumnIndex - 1;
                CotDuLieuModel colData = null;
                lock (_lock)
                {
                    if (dataIndex < _dataColumns.Count) colData = _dataColumns[dataIndex];
                }

                if (colData != null)
                {
                    if (colData.Status == 0)
                    {
                        e.CellStyle.BackColor = Color.OrangeRed;
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(_dgv.Font, FontStyle.Bold);
                        e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }
                    else
                    {
                        e.CellStyle.BackColor = Color.Honeydew;
                        e.CellStyle.ForeColor = Color.DarkGreen;
                        e.CellStyle.Font = new Font("Arial", 8, FontStyle.Regular);
                    }
                }
                return;
            }

            if (e.RowIndex >= HEADER_ROWS && e.ColumnIndex > 0)
            {
                string sidValue = e.Value?.ToString();
                if (!string.IsNullOrEmpty(sidValue))
                {
                    if (sidValue.EndsWith(" Đen", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.FromArgb(64, 64, 64);
                        e.CellStyle.ForeColor = Color.White;
                    }
                    else if (sidValue.EndsWith(" Đỏ", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.LightCoral;
                        e.CellStyle.ForeColor = Color.Black;
                    }
                    else if (sidValue.EndsWith(" X.Lá", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.LightGreen;
                        e.CellStyle.ForeColor = Color.Black;
                    }
                    else if (sidValue.EndsWith(" X.Dương", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.LightSkyBlue;
                        e.CellStyle.ForeColor = Color.Black;
                    }
                    else if (sidValue.EndsWith(" NT", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.LightYellow;
                        e.CellStyle.ForeColor = Color.Black;
                    }
                    else if (sidValue.EndsWith(" PCĐ", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.Plum;
                        e.CellStyle.ForeColor = Color.Black;
                    }
                }
            }
        }

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == 4 && e.ColumnIndex > 0)
            {
                int dataIndex = e.ColumnIndex - 1;
                // [THAY ĐỔI] Thêm lock trước khi lấy dữ liệu để xử lý click
                CotDuLieuModel colData = null;
                lock (_lock)
                {
                    if (dataIndex >= 0 && dataIndex < _dataColumns.Count) colData = _dataColumns[dataIndex];
                }

                if (colData != null)
                {
                    if (colData.Status == 0)
                    {
                        var confirm = MessageBox.Show($"Xác nhận nhận gói hàng từ {colData.NguoiGui}?", "Xác nhận", MessageBoxButtons.YesNo);
                        if (confirm == DialogResult.Yes)
                        {
                            colData.Status = 1;
                            colData.GioNhan = DateTime.Now.ToString("HH:mm dd/MM");
                            OnColumnChanged?.Invoke(colData);
                            _dgv.InvalidateCell(e.ColumnIndex, e.RowIndex);
                            Console.WriteLine($"[MATRIX-CLICK] 🖱️ User confirmed receiving package {colData.Id}");
                        }
                    }
                }
            }
        }

        private void Dgv_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            CheckAndExpandRows(e.RowIndex);
            SetValueAt(e.RowIndex, e.ColumnIndex, e.Value?.ToString());
        }

        private void SetValueAt(int rowIndex, int colIndex, string value)
        {
            if (colIndex == 0) return;
            int dataColIndex = colIndex - 1;
            string newValue = value ?? "";

            // [THAY ĐỔI] Thêm lock bảo vệ khi GHI dữ liệu
            lock (_lock)
            {
                if (dataColIndex >= 0 && dataColIndex < _dataColumns.Count)
                {
                    var colData = _dataColumns[dataColIndex];
                    if (rowIndex == 0) colData.GioGui = newValue;
                    else if (rowIndex == 1) colData.Carrier = newValue;
                    else if (rowIndex == 2) colData.Line = newValue;
                    else if (rowIndex == 3) colData.NguoiGui = newValue;
                    else if (rowIndex == 4) colData.NguoiNhan = newValue;
                    else
                    {
                        int sidIndex = rowIndex - HEADER_ROWS;
                        while (colData.Sids.Count <= sidIndex) colData.Sids.Add("");
                        colData.Sids[sidIndex] = newValue;
                    }
                    if (_isBatchUpdating) _dirtyColumnIds.Add(colData.Id);
                    else OnColumnChanged?.Invoke(colData);
                }
            }
        }

        private void SetupClipboard()
        {
            _dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            _dgv.KeyDown += (s, e) => {
                if (e.Control && e.KeyCode == Keys.V) { PasteFromClipboard(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DeleteSelectedCells(); e.Handled = true; }
            };
        }

        private void PasteFromClipboard()
        {
            try
            {
                string s = Clipboard.GetText();
                if (string.IsNullOrEmpty(s)) return;
                string[] lines = s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                Console.WriteLine($"[MATRIX-PASTE] 📋 Pasting {lines.Length} rows...");
                if (_dgv.CurrentCell == null) return;
                int startRow = _dgv.CurrentCell.RowIndex;
                int startCol = _dgv.CurrentCell.ColumnIndex;
                CheckAndExpandRows(startRow + lines.Length);

                _isBatchUpdating = true; _dirtyColumnIds.Clear();
                for (int i = 0; i < lines.Length; i++)
                {
                    int r = startRow + i;
                    if (r >= _dgv.RowCount) break;
                    string[] cells = lines[i].Split('\t');
                    for (int j = 0; j < cells.Length; j++)
                    {
                        int c = startCol + j;
                        if (c >= _dgv.ColumnCount) break;
                        SetValueAt(r, c, cells[j]);
                    }
                }

                _isBatchUpdating = false;
                foreach (int id in _dirtyColumnIds)
                {
                    var col = _dataColumns.FirstOrDefault(x => x.Id == id);
                    if (col != null) OnColumnChanged?.Invoke(col);
                }
                _dgv.Invalidate();
            }
            catch (Exception ex) { Console.WriteLine($"[PASTE-ERR] {ex.Message}"); }
        }

        private void DeleteSelectedCells()
        {
            if (_dgv.SelectedCells.Count == 0) return;
            Console.WriteLine($"[MATRIX-DEL] 🗑️ Deleting {_dgv.SelectedCells.Count} cells...");

            _isBatchUpdating = true; _dirtyColumnIds.Clear();
            foreach (DataGridViewCell cell in _dgv.SelectedCells) SetValueAt(cell.RowIndex, cell.ColumnIndex, "");
            _isBatchUpdating = false;
            foreach (int id in _dirtyColumnIds)
            {
                var col = _dataColumns.FirstOrDefault(x => x.Id == id);
                if (col != null) OnColumnChanged?.Invoke(col);
            }
            _dgv.Invalidate();
        }

        public List<string> GetExportData()
        {
            var lines = new List<string>();
            lock (_lock)
            {
                lines.Add("--- DỮ LIỆU GIAO NHẬN (LOGISTICS) ---");
                lines.Add("ID,Giờ Gửi,Carrier,Tuyến,Người Gửi,Người Nhận,Trạng Thái,Danh sách SID (Cách nhau bởi |)");
                foreach (var col in _dataColumns)
                {
                    string status = col.Status == 1 ? "Đã nhận" : "Chưa nhận";
                    string sids = string.Join(" | ", col.Sids.Where(s => !string.IsNullOrEmpty(s)));
                    string safeSender = EscapeCsv(col.NguoiGui);
                    string safeReceiver = EscapeCsv(col.NguoiNhan);

                    string line = $"{col.Id},{col.GioGui},{col.Carrier},{col.Line},{safeSender},{safeReceiver},{status},{sids}";
                    lines.Add(line);
                }
            }
            Console.WriteLine($"[MATRIX-EXPORT] 📤 Đã trích xuất {_dataColumns.Count} gói hàng.");
            return lines;
        }

        private string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
            {
                input = input.Replace("\"", "\"\"");
                return $"\"{input}\"";
            }
            return input;
        }
        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if ((e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected)
                {
                    // [FIX QUAN TRỌNG] Ép màu chữ khi chọn về màu gốc
                    e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;

                    // 1. Vẽ nội dung (Trừ nền xanh)
                    e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.SelectionBackground);

                    // 2. Vẽ lớp phủ trong suốt
                    using (Brush overlay = new SolidBrush(Color.FromArgb(50, 0, 120, 215)))
                    {
                        e.Graphics.FillRectangle(overlay, e.CellBounds);
                    }

                    e.Handled = true;
                }
            }
        }

        private void CheckAndExpandRows(int r) { if (r - HEADER_ROWS >= _currentSidRows - 5) { _currentSidRows = Math.Min(_currentSidRows + 20, MAX_SID_ROWS); _dgv.RowCount = HEADER_ROWS + _currentSidRows; } }
        private void EnableDoubleBuffering(Control ctrl) { PropertyInfo pi = ctrl.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic); pi?.SetValue(ctrl, true, null); }
    }
}