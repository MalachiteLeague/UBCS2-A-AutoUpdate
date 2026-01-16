using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [MANAGER] Quản lý giao diện Tìm kiếm (Global Search UI).
    /// - Hiển thị kết quả dạng danh sách.
    /// - Bắt sự kiện Click/Enter để báo cho Form1 thực hiện điều hướng.
    /// </summary>
    public class SearchManager
    {
        private readonly DataGridView _dgv;

        // Danh sách kết quả hiện tại (Lưu trong RAM để truy xuất khi click)
        private List<SearchResultModel> _currentResults = new List<SearchResultModel>();

        // Sự kiện bắn ra ngoài Form1 khi người dùng chọn 1 dòng
        public event Action<SearchResultModel> OnResultSelected;

        public SearchManager(DataGridView dgv)
        {
            _dgv = dgv;
            Console.WriteLine("[SEARCH-MGR] 🟢 Khởi tạo Search Manager (Smart Navigation).");
            SetupGrid();
        }

        /// <summary>
        /// Cấu hình DataGridView cho bảng tìm kiếm.
        /// </summary>
        private void SetupGrid()
        {
            Console.WriteLine("[SEARCH-MGR] 🛠️ Đang cấu hình Grid Search...");
            _dgv.AutoGenerateColumns = false;
            _dgv.Columns.Clear();
            _dgv.BackgroundColor = Color.White;
            _dgv.RowHeadersVisible = false;

            // [QUAN TRỌNG] Tắt tính năng user tự thêm dòng
            _dgv.AllowUserToAddRows = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.AllowUserToResizeRows = false;

            // Chọn nguyên dòng nhưng ẩn màu xanh mặc định (xử lý ở CellFormatting)
            _dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;

            // Tự động giãn chiều cao dòng theo nội dung text
            _dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            _dgv.ColumnHeadersVisible = false; // Ẩn tiêu đề cột cho gọn
            _dgv.ReadOnly = true; // Chỉ xem

            // --- TẠO CỘT KẾT QUẢ ---
            var colResult = new DataGridViewTextBoxColumn()
            {
                Name = "colResult",
                HeaderText = "Kết Quả",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill // Giãn hết chiều ngang
            };

            // Format: Tự xuống dòng, Canh lề nhỏ (Compact), Font 9
            colResult.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colResult.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);
            colResult.DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Regular);

            _dgv.Columns.Add(colResult);

            // Đăng ký sự kiện
            _dgv.CellFormatting += Dgv_CellFormatting;
            _dgv.CellClick += Dgv_CellClick; // [NEW] Bắt sự kiện click chuột
            _dgv.KeyDown += Dgv_KeyDown;     // [NEW] Bắt sự kiện phím Enter

            Console.WriteLine("[SEARCH-MGR] ✅ Cấu hình xong UI.");
        }

        /// <summary>
        /// Xử lý tô màu nền dựa trên loại kết quả.
        /// </summary>
        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _currentResults.Count)
            {
                var item = _currentResults[e.RowIndex];
                e.CellStyle.BackColor = item.BackColor;

                // [TRICK] Set màu Selection giống màu nền để ẩn hiệu ứng "bôi xanh" khi click
                e.CellStyle.SelectionBackColor = item.BackColor;
                e.CellStyle.SelectionForeColor = Color.Black;
            }
        }

        /// <summary>
        /// Xử lý khi click chuột vào dòng kết quả -> Bắn sự kiện điều hướng.
        /// </summary>
        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _currentResults.Count)
            {
                var item = _currentResults[e.RowIndex];
                Console.WriteLine($"[SEARCH-UI] 🖱️ User clicked row {e.RowIndex}. Navigating to {item.TargetGrid?.Name}...");
                OnResultSelected?.Invoke(item);
            }
        }

        /// <summary>
        /// Xử lý khi bấm Enter trên dòng kết quả -> Bắn sự kiện điều hướng.
        /// </summary>
        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && _dgv.CurrentRow != null)
            {
                int index = _dgv.CurrentRow.Index;
                if (index >= 0 && index < _currentResults.Count)
                {
                    Console.WriteLine($"[SEARCH-UI] ⌨️ User pressed Enter on row {index}. Navigating...");
                    OnResultSelected?.Invoke(_currentResults[index]);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Hiển thị danh sách kết quả lên Grid.
        /// </summary>
        public void ShowResults(List<SearchResultModel> results)
        {
            _currentResults = results;

            // Xóa cũ nạp mới
            _dgv.Rows.Clear();

            if (_currentResults.Count > 0)
            {
                foreach (var item in _currentResults)
                {
                    _dgv.Rows.Add(item.DisplayText);
                }

                // Bỏ chọn dòng đầu tiên để tránh xanh lè
                _dgv.ClearSelection();
            }

            Console.WriteLine($"[SEARCH-UI] 🎨 Đã hiển thị {_currentResults.Count} dòng kết quả.");

            // Cuộn lên đầu để xem kết quả quan trọng nhất
            if (_dgv.RowCount > 0)
                _dgv.FirstDisplayedScrollingRowIndex = 0;
        }
    }
}