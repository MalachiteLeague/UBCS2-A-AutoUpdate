using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Helpers;
using UBCS2_A.Models;

namespace UBCS2_A
{
    public partial class Form1
    {
        private SearchManager _searchManager;

        private void SetupSearchSystem()
        {
            if (dgvSearch != null && txtSearch != null)
            {
                _searchManager = new SearchManager(dgvSearch);
                _searchManager.OnResultSelected += HandleSearchResultSelection;
                txtSearch.KeyDown += TxtSearch_KeyDown;
            }
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string keyword = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(keyword)) return;

                var finalResults = new List<SearchResultModel>();

                // 1. Tìm trong Logistics
                if (_matrixManager != null)
                    finalResults.AddRange(_matrixManager.FindSidWithLocation(keyword));

                // 2. Tìm trong Lab (9 bảng)
                if (_context != null)
                    finalResults.AddRange(_context.GetSearchResultsWithLocation(keyword));

                if (finalResults.Count == 0)
                {
                    finalResults.Add(new SearchResultModel()
                    {
                        DisplayText = $"Không tìm thấy '{keyword}'",
                        BackColor = Color.WhiteSmoke
                    });
                }

                _searchManager.ShowResults(finalResults);
                e.SuppressKeyPress = true;
                txtSearch.SelectAll();
            }
        }

        private void HandleSearchResultSelection(SearchResultModel result)
        {
            if (result == null || result.TargetGrid == null) return;

            var grid = result.TargetGrid;

            // =========================================================================
            // [MỚI] SỬ DỤNG EXTENSION METHOD - CODE CỰC GỌN!
            // =========================================================================
            // Thay vì viết vòng lặp while dài dòng, ta chỉ cần gọi:
            grid.FocusOnTab();
            // =========================================================================

            // Xử lý cuộn và tô màu (Logic nghiệp vụ giữ nguyên)
            try
            {
                if (result.RowIndex >= 0 && result.RowIndex < grid.RowCount)
                {
                    grid.ClearSelection();

                    // 1. Cuộn màn hình đến dòng đó
                    grid.FirstDisplayedScrollingRowIndex = result.RowIndex;

                    // 2. Đặt con trỏ vào đúng ô SID (Cột 1)
                    if (result.ColIndex >= 0 && result.ColIndex < grid.ColumnCount)
                    {
                        grid.CurrentCell = grid.Rows[result.RowIndex].Cells[result.ColIndex];
                        grid.Rows[result.RowIndex].Cells[result.ColIndex].Selected = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NAV-ERR] Lỗi điều hướng: {ex.Message}");
            }
        }
    }
}