using System;
using System.Windows.Forms;

namespace UBCS2_A
{
    public partial class Form1
    {
        // [QUAN TRỌNG] Đã xóa AuthorizationManager _authManager;

        private void SetupAuthorization()
        {
            if (cboKhuVuc == null) return;

            // Nạp danh sách quyền
            cboKhuVuc.Items.Clear();
            cboKhuVuc.Items.AddRange(new string[] {
                "Admin",
                "Hành Chánh T1", "Hành Chánh T3",
                "Huyết học T1", "Sinh hóa T1", "Miễn dịch T1",
                "Huyết học T3", "SH-MD T3",
                "Khách"
            });

            // Sự kiện đổi quyền
            cboKhuVuc.SelectedIndexChanged += (s, e) =>
            {
                string role = cboKhuVuc.SelectedItem?.ToString() ?? "Khách";

                // [GỌI THẲNG VÀO LAB CONTEXT] 
                // Không cần qua trung gian nữa!
                if (_context != null)
                {
                    _context.SetUserRole(role);
                }

                // Cập nhật role cho Task (nếu có logic riêng)
                // if (_taskContext != null) _taskContext.SetRole(role);
            };

            // Mặc định chọn Khách
            cboKhuVuc.SelectedIndex = cboKhuVuc.Items.Count - 1;
        }
    }
}