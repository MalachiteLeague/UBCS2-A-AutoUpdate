using System.Drawing;
using System.Windows.Forms;

namespace UBCS2_A.Models
{
    /// <summary>
    /// [MODEL] Kết quả tìm kiếm kèm địa chỉ điều hướng.
    /// Chứa thông tin để khi Click vào sẽ biết cần nhảy đến Grid nào, Dòng nào, Cột nào.
    /// </summary>
    public class SearchResultModel
    {
        // Nội dung hiển thị trên bảng Search (VD: "[LAB] Huyết học: 12345")
        public string DisplayText { get; set; }

        // --- ĐỊA CHỈ GỐC ---
        public DataGridView TargetGrid { get; set; } // Bảng chứa dữ liệu này
        public int RowIndex { get; set; }            // Dòng số mấy
        public int ColIndex { get; set; }            // Cột số mấy (thường là cột SID)

        // Màu nền để phân loại (Logistics vs Lab)
        public Color BackColor { get; set; }
    }
}