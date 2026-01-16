using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [DERIVED CLASS] Grid chuyên dụng cho Xét nghiệm.
    /// Kế thừa toàn bộ tính năng của GridManager và thêm logic Tô màu trùng.
    /// </summary>
    public class LabGridManager : GridManager<MauXetNghiemModel>
    {
        // Logic riêng: Danh sách các SID bị trùng
        private HashSet<string> _duplicateKeys = new HashSet<string>();

        public LabGridManager(DataGridView dgv, int maxRows) : base(dgv, maxRows)
        {
        }

        // 1. GHI ĐÈ logic tính toán: Mỗi khi data đổi -> Tính lại danh sách trùng
        protected override void OnDataSnapshotChanged()
        {
            lock (_lock)
            {
                _duplicateKeys.Clear();
                var counts = new Dictionary<string, int>();

                foreach (var item in _dataSnapshot)
                {
                    // Logic đặc thù: Key chính là SID
                    string key = item.SID;
                    if (!string.IsNullOrEmpty(key))
                    {
                        if (counts.ContainsKey(key)) counts[key]++;
                        else counts[key] = 1;
                    }
                }

                foreach (var kvp in counts)
                {
                    if (kvp.Value > 1) _duplicateKeys.Add(kvp.Key);
                }
            }
        }

        // 2. GHI ĐÈ logic hiển thị: Nếu trùng -> Tô Vàng
        protected override void OnCustomCellFormatting(DataGridViewCellFormattingEventArgs e, MauXetNghiemModel item, int rowIndex)
        {
            string key = item.SID;
            if (!string.IsNullOrEmpty(key) && _duplicateKeys.Contains(key))
            {
                e.CellStyle.BackColor = Color.Yellow;
                e.CellStyle.ForeColor = Color.Red;
                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
            }
        }
    }
}