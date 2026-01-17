using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    public class LabGridManager : GridManager<MauXetNghiemModel>
    {
        private HashSet<string> _duplicateKeys = new HashSet<string>();

        public LabGridManager(DataGridView dgv, int maxRows) : base(dgv, maxRows)
        {
        }

        protected override void OnDataSnapshotChanged()
        {
            // 1. Tính toán danh sách trùng (Logic cũ giữ nguyên)
            lock (_lock)
            {
                _duplicateKeys.Clear();
                var counts = new Dictionary<string, int>();

                foreach (var item in _dataSnapshot)
                {
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

            // 2. [FIX QUAN TRỌNG] Bắt buộc vẽ lại TOÀN BỘ lưới
            // Lý do: Để ô cũ (đã vẽ trước đó) cũng nhận biết được là mình vừa bị trùng
            if (_dgv.IsHandleCreated && !_dgv.IsDisposed)
            {
                _dgv.BeginInvoke(new Action(() => _dgv.Invalidate()));
            }
        }

        protected override void OnCustomCellFormatting(DataGridViewCellFormattingEventArgs e, MauXetNghiemModel item, int rowIndex)
        {
            // Logic tô màu (Giữ nguyên)
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