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

                // 1. Cập nhật quyền cho bảng Lab (Xét nghiệm)
                if (_context != null)
                {
                    _context.SetUserRole(role);
                }

                // 2. Cập nhật quyền cho Task (Giao việc) [QUAN TRỌNG: SỬA DÒNG NÀY]
                if (_taskContext != null)
                {
                    // Hàm này sẽ cập nhật _currentRole trong TaskContext
                    // Giúp Popup nhận diện đúng khu vực để hiện lên
                    _taskContext.SetCurrentUserRole(role);
                }
                // 3. Cập nhật quyền cho Chat (Tin nhắn) - [FIX] Thêm mới
                if (_chatContext != null)
                {
                    // Truyền thẳng tên vị trí cụ thể (VD: "Huyết học T1")
                    // ChatContext sẽ tự động refresh lại bảng Chat Private
                    _chatContext.SetCurrentUserRole(role);
                }
            };


            // Mặc định chọn Khách
            cboKhuVuc.SelectedIndex = cboKhuVuc.Items.Count - 1;
        }
    }
}