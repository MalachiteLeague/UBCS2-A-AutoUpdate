using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Helpers;
using UBCS2_A.Models;

namespace UBCS2_A.Services
{
    public class LabDataContext : IDisposable
    {
        private readonly FirebaseService _firebaseService;

        // Quản lý các bộ đồng bộ cho Grid thường (Row-based)
        private readonly List<SyncCoordinator<MauXetNghiemModel>> _rowCoordinators = new List<SyncCoordinator<MauXetNghiemModel>>();

        // Dictionary lưu trữ tham chiếu để phục vụ tính năng Tìm kiếm & Export & Auth
        // Lưu ý: Ta lưu dưới dạng GridManager (lớp cha) để tổng quát, nhưng thực tế sẽ chứa LabGridManager
        private readonly Dictionary<string, (GridManager<MauXetNghiemModel> Manager, DataGridView Grid)> _registeredTables
            = new Dictionary<string, (GridManager<MauXetNghiemModel>, DataGridView)>();

        public LabDataContext(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
            Console.WriteLine("[LAB-CTX] 🟢 Khởi tạo LabDataContext (Chuyên trị 9 bảng xét nghiệm).");
        }

        public void RegisterTable(DataGridView dgv, string nodeName, int maxRows = 2000)
        {
            Console.WriteLine($"[LAB-CTX] 📝 Đang đăng ký bảng xét nghiệm: {nodeName}");

            // 1. Cấu hình Grid (Giao diện)
            dgv.VirtualMode = true;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.RowHeadersVisible = false;
            dgv.ColumnHeadersHeight = 30;

            // =================================================================
            // [THAY ĐỔI] SỬ DỤNG LABGRIDMANAGER (LỚP CON)
            // =================================================================
            // Class này đã tích hợp sẵn logic Tô màu trùng & Trùng SID
            var manager = new LabGridManager(dgv, maxRows);
            // =================================================================

            // 2. Mapping hiển thị cột
            // Cột 0: STT (Tự tính dựa trên Index)
            // Cột 1: SID (Lấy từ Model)
            manager.OnGetValue = (rowIndex, model, colIndex) =>
            {
                if (colIndex == 0) return (rowIndex + 1).ToString();
                if (colIndex == 1) return model.SID;
                return "";
            };

            // 3. Mapping sửa dữ liệu (Chỉ cho sửa cột SID)
            manager.OnSetValue = (model, colIndex, value) =>
            {
                string val = value?.ToString() ?? "";
                if (colIndex == 1) model.SID = val.Trim();
            };

            // Lưu tham chiếu để dùng cho Search/Export/Auth sau này
            _registeredTables[nodeName] = (manager, dgv);

            // 4. Khởi tạo bộ đồng bộ Realtime (SyncCoordinator)
            var coord = new SyncCoordinator<MauXetNghiemModel>(
                manager,
                _firebaseService,
                nodeName,
                (row, m) => $"Row_{row}",
                (key) => int.TryParse(key.Replace("Row_", ""), out int r) ? r : -1,
                (m) => string.IsNullOrEmpty(m.SID)
            );

            _rowCoordinators.Add(coord);
        }

        /// <summary>
        /// Tìm kiếm SID trong tất cả 9 bảng, trả về vị trí chính xác để điều hướng.
        /// </summary>
        public List<SearchResultModel> GetSearchResultsWithLocation(string keyword)
        {
            var results = new List<SearchResultModel>();
            if (string.IsNullOrWhiteSpace(keyword)) return results;

            string upperKey = keyword.ToUpper();

            foreach (var kvp in _registeredTables)
            {
                var manager = kvp.Value.Manager;
                var data = manager.GetAllData(); // Lấy dữ liệu Snapshot an toàn từ Manager

                for (int i = 0; i < data.Count; i++)
                {
                    if (!string.IsNullOrEmpty(data[i].SID) && data[i].SID.ToUpper().Contains(upperKey))
                    {
                        results.Add(new SearchResultModel()
                        {
                            DisplayText = $"[LAB] {kvp.Key} - STT: {i + 1} - SID: {data[i].SID}",
                            TargetGrid = kvp.Value.Grid,
                            RowIndex = i,
                            ColIndex = 1,
                            BackColor = Color.LightYellow
                        });
                    }
                }
            }
            return results;
        }

        // =========================================================================
        // [ĐÃ SỬA TÊN HÀM] GetAllLabDataForExport (Khớp với BackupService)
        // =========================================================================
        public List<string> GetAllLabDataForExport()
        {
            var lines = new List<string>();
            lines.Add("--- DỮ LIỆU XÉT NGHIỆM (LAB) ---");
            lines.Add("Bảng,STT,SID");

            foreach (var kvp in _registeredTables)
            {
                string tableName = kvp.Key;
                var manager = kvp.Value.Manager;
                var data = manager.GetAllData();

                for (int i = 0; i < data.Count; i++)
                {
                    var r = data[i];
                    if (!string.IsNullOrEmpty(r.SID))
                    {
                        lines.Add($"{tableName},{i + 1},{r.SID}");
                    }
                }
            }
            Console.WriteLine($"[LAB-CTX] 📤 Đã trích xuất dữ liệu từ {_registeredTables.Count} bảng Lab.");
            return lines;
        }

        // =========================================================================
        // [MỚI] LOGIC PHÂN QUYỀN (Authorization) - Thay thế AuthorizationManager
        // =========================================================================

        public void SetUserRole(string roleName)
        {
            Console.WriteLine($"[LAB-CTX] 🔐 Đang áp dụng quyền: {roleName}");

            foreach (var kvp in _registeredTables)
            {
                string nodeName = kvp.Key;   // Tên bảng (VD: T1_HuyetHoc_CongThucMau)
                DataGridView dgv = kvp.Value.Grid;

                bool canEdit = CheckPermission(roleName, nodeName);
                ApplyGridState(dgv, canEdit);
            }
        }

        private bool CheckPermission(string role, string tableName)
        {
            if (role == "Admin" || role == "Hành Chánh T1" || role == "Hành Chánh T3") return true;
            if (role == "Khách") return false;

            // Logic so sánh tên (Mapping)
            // Tầng 1
            if (role == "Huyết học T1" && tableName.Contains("T1_HuyetHoc")) return true;
            if (role == "Sinh hóa T1" && tableName.Contains("T1_SinhHoa")) return true;
            if (role == "Miễn dịch T1" && tableName.Contains("T1_MienDich")) return true;

            // Tầng 3
            if (role == "Huyết học T3" && tableName.Contains("T3_HuyetHoc")) return true;

            // SH-MD T3 thường gộp chung Sinh hóa và Miễn dịch
            if (role == "SH-MD T3" && (tableName.Contains("T3_SinhHoa") || tableName.Contains("T3_MienDich"))) return true;

            return false;
        }

        private void ApplyGridState(DataGridView dgv, bool canEdit)
        {
            if (dgv.Columns.Count < 2) return;

            if (canEdit)
            {
                // CHẾ ĐỘ SỬA
                dgv.ReadOnly = false;
                dgv.Columns[1].ReadOnly = false; // Cột SID cho sửa
                dgv.Columns[1].DefaultCellStyle.BackColor = Color.White;
                dgv.Columns[1].DefaultCellStyle.ForeColor = Color.Black;
            }
            else
            {
                // CHẾ ĐỘ CHỈ XEM
                dgv.ReadOnly = true;
                dgv.Columns[1].DefaultCellStyle.BackColor = Color.LightGray;
                dgv.Columns[1].DefaultCellStyle.ForeColor = Color.DimGray;
            }
        }

        public async Task StartAllAsync()
        {
            Console.WriteLine("[LAB-CTX] 🚀 Bắt đầu quy trình khởi động cho các bảng Xét nghiệm...");

            _firebaseService.StartListening();

            foreach (var coord in _rowCoordinators)
            {
                coord.StartSync();
            }

            Console.WriteLine("[LAB-CTX] 📥 Đang tải dữ liệu Snapshot cho 9 bảng xét nghiệm...");
            var allLoadTasks = new List<Task>();

            foreach (var coord in _rowCoordinators)
            {
                allLoadTasks.Add(coord.LoadInitialData());
            }

            await Task.WhenAll(allLoadTasks);
            Console.WriteLine("[LAB-CTX] ✅ Hoàn tất tải dữ liệu 9 bảng Xét nghiệm.");
        }

        public void Dispose()
        {
            Console.WriteLine("[LAB-CTX] 🗑️ Đang hủy (Dispose) LabDataContext...");
            _firebaseService?.Dispose();
            foreach (var coord in _rowCoordinators) coord.Dispose();
            _rowCoordinators.Clear();
        }
    }
}