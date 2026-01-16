using System;

namespace UBCS2_A.Models
{
    /// <summary>
    /// [MODEL] Cấu trúc dữ liệu của một Task giao việc.
    /// </summary>
    public class TaskModel
    {
        // [QUAN TRỌNG] ID định danh duy nhất (Ví dụ: 100, 101...)
        // Dùng để tạo Key Firebase: Task_100, Task_101 (Thay vì dựa vào số dòng)
        public int Id { get; set; } = 0;

        public string ThoiGian { get; set; } = "";
        public string KhuVucNhan { get; set; } = "";
        public string SID { get; set; } = "";
        public string NoiDung { get; set; } = "";

        // Trạng thái: 0=Mới, 1=Đang làm, 2=Xong
        public int TrangThai { get; set; } = 0;

        // Key Firebase (Optional, dùng để tham chiếu nếu cần)
        public string FirebaseKey => $"Task_{Id}";
    }
}