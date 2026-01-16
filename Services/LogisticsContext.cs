using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UBCS2_A.Helpers;
using UBCS2_A.Models;

namespace UBCS2_A.Services
{
    /// <summary>
    /// [NEW CLASS] Context chuyên biệt quản lý dữ liệu Logistics (Giao nhận).
    /// Mục đích: Tách biệt hoàn toàn logic Matrix ra khỏi các bảng Xét nghiệm thường.
    /// </summary>
    public class LogisticsContext : IDisposable
    {
        // Service kết nối Firebase (Dùng chung instance với LabDataContext)
        private readonly FirebaseService _firebaseService;

        // Danh sách quản lý các bộ đồng bộ Matrix (Hiện tại chỉ có 1 bảng Giao Nhận)
        private readonly List<MatrixSyncCoordinator> _matrixCoordinators = new List<MatrixSyncCoordinator>();

        public LogisticsContext(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
            Console.WriteLine("[LOGISTICS-CTX] 🟢 Khởi tạo Context Logistics (Sẵn sàng quản lý Matrix).");
        }

        /// <summary>
        /// Đăng ký một bảng Matrix vào hệ thống quản lý của Context này.
        /// </summary>
        /// <param name="matrixMgr">Quản lý giao diện Matrix (Grid)</param>
        /// <param name="nodeName">Tên Node trên Firebase</param>
        public void RegisterMatrixTable(MatrixManager matrixMgr, string nodeName)
        {
            Console.WriteLine($"[LOGISTICS-CTX] 📝 Đang đăng ký bảng Matrix: {nodeName}...");

            // Tạo bộ điều phối đồng bộ (Sync Coordinator) riêng cho Matrix
            var coordinator = new MatrixSyncCoordinator(matrixMgr, _firebaseService, nodeName);

            // Lưu vào danh sách để quản lý (Start/Dispose sau này)
            _matrixCoordinators.Add(coordinator);

            Console.WriteLine($"[LOGISTICS-CTX] ✅ Đăng ký thành công Matrix: {nodeName}");
        }

        /// <summary>
        /// Bước 1: Kích hoạt lắng nghe sự kiện (Sync) cho các Matrix.
        /// Cần gọi hàm này TRƯỚC khi Firebase bắt đầu Stream để không bỏ lỡ dữ liệu.
        /// </summary>
        public void PrepareSync()
        {
            Console.WriteLine("[LOGISTICS-CTX] 👂 Bắt đầu kích hoạt lắng nghe sự kiện cho các bảng Matrix...");
            foreach (var coord in _matrixCoordinators)
            {
                coord.StartSync();
            }
        }

        /// <summary>
        /// Bước 2: Tải dữ liệu ban đầu cho Matrix (chạy Async).
        /// Hàm này trả về Task để Form1 có thể dùng Task.WhenAll (chạy song song).
        /// </summary>
        public async Task LoadInitialDataAsync()
        {
            Console.WriteLine("[LOGISTICS-CTX] 📥 Bắt đầu tải dữ liệu khởi tạo (Snapshot) cho Matrix...");
            var tasks = new List<Task>();

            // Gom tất cả tác vụ tải của các bảng Matrix vào list
            foreach (var coord in _matrixCoordinators)
            {
                tasks.Add(coord.LoadInitialData());
            }

            // Chờ tất cả tải xong
            await Task.WhenAll(tasks);
            Console.WriteLine("[LOGISTICS-CTX] ✅ Hoàn tất tải dữ liệu cho toàn bộ Matrix.");
        }

        /// <summary>
        /// Dọn dẹp tài nguyên khi đóng Form.
        /// </summary>
        public void Dispose()
        {
            Console.WriteLine("[LOGISTICS-CTX] 🗑️ Đang hủy (Dispose) LogisticsContext...");
            foreach (var coord in _matrixCoordinators)
            {
                coord.Dispose();
            }
            Console.WriteLine("[LOGISTICS-CTX] 🏁 Đã hủy xong.");
        }

    }
}