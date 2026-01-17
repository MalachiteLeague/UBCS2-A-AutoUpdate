using System;
using System.Collections.Generic;
using System.Linq; // Cần dùng LINQ
using System.Threading.Tasks;
using UBCS2_A.Helpers;
using UBCS2_A.Models;

namespace UBCS2_A.Services
{
    /// <summary>
    /// [COORDINATOR] Cầu nối giữa MatrixManager và FirebaseService.
    /// - Chuyển đổi Model <-> Firebase JSON.
    /// - Xử lý Push (Gửi) và Pull (Nhận).
    /// </summary>
    public class MatrixSyncCoordinator : IDisposable
    {
        private readonly MatrixManager _matrix;
        private readonly FirebaseService _firebase;
        private readonly string _nodeName; // Tên node trên Firebase (VD: T_Logistics_Matrix)

        public MatrixSyncCoordinator(MatrixManager matrix, FirebaseService firebase, string nodeName)
        {
            _matrix = matrix;
            _firebase = firebase;
            _nodeName = nodeName;
        }

        public void StartSync()
        {
            // 1. Lắng nghe từ App (MatrixManager)
            _matrix.OnColumnChanged += Matrix_OnColumnChanged; // Khi thêm/sửa
            _matrix.OnColumnDeleted += Matrix_OnColumnDeleted; // Khi xóa (vì đầy)

            // 2. Lắng nghe từ Firebase (Server)
            _firebase.OnDataChanged += Firebase_OnDataChanged; // Khi dữ liệu đổi
            _firebase.OnItemDeleted += Firebase_OnItemDeleted; // Khi dữ liệu bị xóa
        }

        // ==================================================================================
        // PHẦN PUSH: GỬI LÊN FIREBASE
        // ==================================================================================

        private async void Matrix_OnColumnChanged(CotDuLieuModel colData)
        {
            try
            {
                // Tạo Key dựa trên ID vĩnh viễn: Col_100, Col_101...
                string key = $"Col_{colData.Id}";
                Console.WriteLine($"[MATRIX-SYNC] 📤 Pushing Key: {key} (Status: {colData.Status})");

                // Gửi toàn bộ object lên Firebase
                await _firebase.UpdateDataAsync($"{_nodeName}/{key}", colData);
            }
            catch (Exception ex) { Console.WriteLine($"[ERR] Push Failed: {ex.Message}"); }
        }

        private async void Matrix_OnColumnDeleted(int colId)
        {
            try
            {
                string key = $"Col_{colId}";
                Console.WriteLine($"[MATRIX-SYNC] ✂️ Limit Reached. Deleting Old Key: {key} on Cloud.");

                // Xóa node trên Firebase
                await _firebase.DeleteDataAsync($"{_nodeName}/{key}");
            }
            catch (Exception ex) { Console.WriteLine($"[ERR] Delete Failed: {ex.Message}"); }
        }

        // ==================================================================================
        // PHẦN PULL: NHẬN VỀ TỪ FIREBASE
        // ==================================================================================

        private void Firebase_OnDataChanged(object sender, FirebaseDataEventArgs e)
        {
            if (e.RootNode != _nodeName) return;

            // Firebase trả về Key dạng: "Col_105"
            // Ta parse lấy ID = 105
            if (e.Key.StartsWith("Col_") && int.TryParse(e.Key.Substring(4), out int colId))
            {
                var colData = e.ToObject<CotDuLieuModel>();
                if (colData != null)
                {
                    // Cập nhật vào MatrixManager theo ID
                    _matrix.UpdateColumnById(colId, colData);
                }
            }
        }

        private void Firebase_OnItemDeleted(object sender, FirebaseDeleteEventArgs e)
        {
            // [FIX] Thêm logic xử lý xóa toàn bộ bảng (ALL)
            if (e.TargetId == "ALL" || e.TargetId == _nodeName || (e.RootNode == _nodeName && string.IsNullOrEmpty(e.TargetId)))
            {
                Console.WriteLine($"[MATRIX-SYNC] 🧹 Server đã DELETE bảng {_nodeName}!");
                // Gọi hàm LoadData với list rỗng để xóa sạch Grid
                _matrix.LoadData(new List<CotDuLieuModel>());
                return;
            }

            if (e.RootNode != _nodeName) return;

            // Nếu nhận được tín hiệu xóa Key "Col_100"
            if (e.TargetId.StartsWith("Col_") && int.TryParse(e.TargetId.Substring(4), out int colId))
            {
                _matrix.DeleteColumnById(colId);
            }
        }

        // ==================================================================================
        // INITIAL LOAD (TẢI LẦN ĐẦU)
        // ==================================================================================

        public async Task LoadInitialData()
        {
            try
            {
                Console.WriteLine($"[MATRIX-SYNC] 📥 Loading all data from Firebase...");
                var dict = await _firebase.GetDataAsync<Dictionary<string, CotDuLieuModel>>(_nodeName);

                if (dict != null)
                {
                    // Chuyển Dictionary -> List
                    var listData = new List<CotDuLieuModel>();
                    foreach (var kvp in dict)
                    {
                        // Parse ID từ Key (để chắc chắn khớp)
                        if (kvp.Key.StartsWith("Col_") && int.TryParse(kvp.Key.Substring(4), out int id))
                        {
                            var item = kvp.Value;
                            item.Id = id; // Gán lại ID cho chắc
                            listData.Add(item);
                        }
                    }

                    // Đẩy vào MatrixManager (Nó sẽ tự sắp xếp theo ID)
                    _matrix.LoadData(listData);
                }
                else
                {
                    Console.WriteLine("[MATRIX-SYNC] Data is empty.");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[ERR] LoadInit: {ex.Message}"); }
        }

        public void Dispose()
        {
            _matrix.OnColumnChanged -= Matrix_OnColumnChanged;
            _matrix.OnColumnDeleted -= Matrix_OnColumnDeleted;
            _firebase.OnDataChanged -= Firebase_OnDataChanged;
            _firebase.OnItemDeleted -= Firebase_OnItemDeleted;
        }
    }
}