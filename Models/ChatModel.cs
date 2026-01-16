using System;

namespace UBCS2_A.Models
{
    /// <summary>
    /// [MODEL] Cấu trúc một tin nhắn chat.
    /// </summary>
    public class ChatModel
    {
        // ID định danh để xử lý cuốn chiếu (Chat_100, Chat_101...)
        public int Id { get; set; }

        public string ThoiGian { get; set; } // Giờ gửi
        public string NguoiGui { get; set; } // Tên người gửi (VD: Huyết học T1)
        public string NoiNhan { get; set; }  // Nơi nhận (VD: Sinh hóa T1, hoặc "Toàn viện")
        public string NoiDung { get; set; }  // Nội dung tin nhắn

        // Thuộc tính phụ trợ để tạo Key Firebase
        public string FirebaseKey => $"Chat_{Id}";
    }
}