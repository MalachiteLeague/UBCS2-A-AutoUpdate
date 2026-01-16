using System.Collections.Generic;

namespace UBCS2_A.Models
{
    /// <summary>
    /// [MODEL] Cấu trúc dữ liệu của một cột giao nhận (Một gói hàng).
    /// </summary>
    public class CotDuLieuModel
    {
        // [QUAN TRỌNG] ID định danh duy nhất (Ví dụ: 100, 101, 102...)
        // Dùng để tạo Key trên Firebase: Col_100, Col_101... thay vì Col_0, Col_1
        public int Id { get; set; } = 0;

        // Trạng thái: 0 = Chưa nhận (Hiện nút bấm), 1 = Đã nhận (Hiện tên)
        public int Status { get; set; } = 0;

        public string GioGui { get; set; } = "";  // VD: "10:30 12/01"
        public string GioNhan { get; set; } = ""; // VD: "14:00 12/01" (Lưu khi bấm nhận)

        public string Carrier { get; set; } = "";
        public string Line { get; set; } = "";
        public string NguoiGui { get; set; } = "";
        public string NguoiNhan { get; set; } = "";

        // Danh sách các mã xét nghiệm (SID) trong gói
        public List<string> Sids { get; set; } = new List<string>();

        public CotDuLieuModel()
        {
            Sids = new List<string>();
        }
    }
}