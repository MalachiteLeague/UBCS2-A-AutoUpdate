using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [BASE CLASS] Quản lý DataGridView cơ bản.
    /// Nhiệm vụ: Virtual Mode, Edit, Copy/Paste, Batch Update.
    /// KHÔNG chứa logic nghiệp vụ (như tô màu trùng).
    /// </summary>
    public class GridManager<T> where T : class, new()
    {
        protected readonly DataGridView _dgv;
        protected T[] _dataSnapshot;
        protected readonly object _lock = new object();
        protected int _maxRows;

        // Batch Update
        protected bool _isBatchUpdating = false;
        protected HashSet<int> _dirtyRows = new HashSet<int>();

        // Delegates
        public Func<int, T, int, object> OnGetValue;
        public Action<T, int, object> OnSetValue;
        public event Action<int, T> OnUserChangedData;

        public GridManager(DataGridView dgv, int maxRows)
        {
            _dgv = dgv;
            _maxRows = maxRows;

            _dataSnapshot = new T[_maxRows];
            for (int i = 0; i < _maxRows; i++) _dataSnapshot[i] = new T();

            SetupBaseGrid();
        }

        private void SetupBaseGrid()
        {
            _dgv.VirtualMode = true;
            _dgv.RowCount = _maxRows;
            _dgv.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;

            EnableDoubleBuffering(_dgv);
            _dgv.DataError += (s, e) => { e.ThrowException = false; };

            _dgv.CellValueNeeded += Dgv_CellValueNeeded;
            _dgv.CellValuePushed += Dgv_CellValuePushed;
            _dgv.CellFormatting += Dgv_CellFormatting;
            _dgv.KeyDown += Dgv_KeyDown;
            // [THÊM DÒNG NÀY] Đăng ký sự kiện tự vẽ
            _dgv.CellPainting += Dgv_CellPainting;
        }

        // --- CÁC HÀM VIRTUAL (ĐỂ CON GHI ĐÈ) ---

        // Hook: Khi dữ liệu thay đổi (để con tính toán lại, VD: tính trùng)
        protected virtual void OnDataSnapshotChanged() { }

        // Hook: Khi vẽ cell (để con tô màu)
        protected virtual void OnCustomCellFormatting(DataGridViewCellFormattingEventArgs e, T item, int rowIndex) { }

        // ---------------------------------------

        public void UpdateSingleRow(int rowIndex, JToken data)
        {
            if (rowIndex < 0 || rowIndex >= _maxRows) return;
            lock (_lock)
            {
                var updatedItem = data.ToObject<T>();
                if (updatedItem != null) _dataSnapshot[rowIndex] = updatedItem;
                OnDataSnapshotChanged();
            }
            if (_dgv.IsHandleCreated && !_dgv.IsDisposed)
                _dgv.BeginInvoke((Action)(() => { if (_dgv.RowCount > rowIndex) _dgv.InvalidateRow(rowIndex); }));
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                for (int i = 0; i < _maxRows; i++) _dataSnapshot[i] = new T();
                OnDataSnapshotChanged();
            }
            if (_dgv.IsHandleCreated) _dgv.BeginInvoke((Action)(() => _dgv.Invalidate()));
        }

        public void LoadFullData(IDictionary<int, T> dataMap)
        {
            lock (_lock)
            {
                for (int i = 0; i < _maxRows; i++) _dataSnapshot[i] = new T();
                foreach (var kvp in dataMap) if (kvp.Key < _maxRows) _dataSnapshot[kvp.Key] = kvp.Value;
                OnDataSnapshotChanged();
            }
            if (_dgv.InvokeRequired) _dgv.Invoke(new Action(() => _dgv.Invalidate()));
            else _dgv.Invalidate();
        }

        // --- Event Handlers ---

        private void Dgv_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < _maxRows && OnGetValue != null)
                e.Value = OnGetValue(e.RowIndex, _dataSnapshot[e.RowIndex], e.ColumnIndex);
        }

        private void Dgv_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < _maxRows && OnSetValue != null)
            {
                OnSetValue(_dataSnapshot[e.RowIndex], e.ColumnIndex, e.Value);
                OnDataSnapshotChanged();
                _dgv.InvalidateRow(e.RowIndex);

                if (!_isBatchUpdating) OnUserChangedData?.Invoke(e.RowIndex, _dataSnapshot[e.RowIndex]);
                else lock (_lock) { _dirtyRows.Add(e.RowIndex); }
            }
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _maxRows)
            {
                // Gọi hàm ảo để lớp con tự quyết định
                OnCustomCellFormatting(e, _dataSnapshot[e.RowIndex], e.RowIndex);
            }
        }

        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V) { PasteFromClipboard(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelectedCells(); e.Handled = true; }
        }

        private void PasteFromClipboard()
        {
            try
            {
                string s = Clipboard.GetText(); if (string.IsNullOrEmpty(s)) return;
                string[] lines = s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                int startRow = _dgv.CurrentCell.RowIndex;
                int startCol = _dgv.CurrentCell.ColumnIndex;
                BeginBatchUpdate();
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] cells = lines[i].Split('\t');
                    int currentRow = startRow + i;
                    if (currentRow >= _maxRows) break;
                    for (int j = 0; j < cells.Length; j++)
                    {
                        int currentCol = startCol + j;
                        if (currentCol >= _dgv.ColumnCount) break;
                        string val = cells[j].Trim();
                        if (i == lines.Length - 1 && string.IsNullOrEmpty(val)) continue;
                        if (OnSetValue != null)
                        {
                            OnSetValue(_dataSnapshot[currentRow], currentCol, val);
                            lock (_lock) { _dirtyRows.Add(currentRow); }
                        }
                    }
                }
                EndBatchUpdate();
            }
            catch { }
        }

        private void DeleteSelectedCells()
        {
            if (_dgv.SelectedCells.Count == 0) return;
            BeginBatchUpdate();
            foreach (DataGridViewCell cell in _dgv.SelectedCells)
            {
                if (!cell.ReadOnly && OnSetValue != null)
                {
                    OnSetValue(_dataSnapshot[cell.RowIndex], cell.ColumnIndex, "");
                    lock (_lock) { _dirtyRows.Add(cell.RowIndex); }
                }
            }
            EndBatchUpdate();
        }

        public void BeginBatchUpdate() { _isBatchUpdating = true; _dirtyRows.Clear(); }

        public void EndBatchUpdate()
        {
            _isBatchUpdating = false;
            foreach (int r in _dirtyRows) OnUserChangedData?.Invoke(r, _dataSnapshot[r]);
            _dirtyRows.Clear();
            OnDataSnapshotChanged();
            _dgv.Invalidate();
        }

        public void SearchAndHighlight(Func<T, bool> predicate)
        {
            int foundIndex = -1;
            lock (_lock) { for (int i = 0; i < _maxRows; i++) if (predicate(_dataSnapshot[i])) { foundIndex = i; break; } }
            if (foundIndex != -1) _dgv.BeginInvoke((Action)(() => {
                _dgv.ClearSelection();
                _dgv.FirstDisplayedScrollingRowIndex = foundIndex;
                _dgv.Rows[foundIndex].Selected = true;
            }));
        }
        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if ((e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected)
                {
                    // [FIX QUAN TRỌNG] Ép màu chữ khi chọn về giống màu bình thường (thường là Đen)
                    // Nếu không có dòng này, nó sẽ lấy DefaultCellStyle.SelectionForeColor (mặc định là Trắng)
                    e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;

                    // 1. Vẽ nội dung (Bỏ nền xanh mặc định)
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

        public List<T> GetAllData() { lock (_lock) { return _dataSnapshot.ToList(); } }

        private void EnableDoubleBuffering(Control ctrl)
        {
            PropertyInfo pi = ctrl.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi?.SetValue(ctrl, true, null);
        }
    }
}