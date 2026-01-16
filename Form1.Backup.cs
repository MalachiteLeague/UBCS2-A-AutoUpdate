using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Services;

namespace UBCS2_A
{
    // Đây là một phần của Form1, chuyên xử lý Backup & Xóa
    public partial class Form1
    {
        private BackupService _backupService;

        private void SetupBackupSystem()
        {
            // 1. Khởi tạo Service (Logic)
            if (_matrixManager != null && _context != null)
            {
                _backupService = new BackupService(_context, _matrixManager);
                _backupService.StartAutoBackup();
            }

            // 2. Sự kiện nút "Xuất Dữ Liệu" (Thủ công)
            if (btnExport != null)
            {
                btnExport.Click += (s, e) =>
                {
                    string path = _backupService?.PerformExport(isAuto: false);
                    if (!string.IsNullOrEmpty(path))
                    {
                        MessageBox.Show($"Xuất file thành công:\n{path}", "Thông báo");
                        try { Process.Start("explorer.exe", "/select," + path); } catch { }
                    }
                };
            }
            // Kiểm tra xem nút có tồn tại không (đề phòng bạn chưa đặt tên đúng)
            if (btnClearData != null)
            {
                // Gắn hàm BtnClearData_Click vào sự kiện Click của nút
                btnClearData.Click += BtnClearData_Click;
            }
        }

        // --- CÁC HÀM XỬ LÝ LỆNH HỆ THỐNG (COMMAND) ---

        private void SetupSystemCommands(FirebaseService firebase)
        {
            // Đăng ký sự kiện lắng nghe thay đổi dữ liệu
            firebase.OnDataChanged += (s, e) =>
            {
                // 1. LOG DEBUG: Xem máy con thực sự nhận được cái gì
                // Console.WriteLine($"[SYSTEM-DEBUG] Path: {e.Path} | Key: {e.Key} | Data: {e.Data}");

                try
                {
                    // 2. PHÂN TÍCH LINH HOẠT (Chấp nhận nhiều trường hợp đường dẫn)
                    // Trường hợp A: Thay đổi trực tiếp tại node Command (/System/Command)
                    // Trường hợp B: Thay đổi tại node cha System (/System)

                    Newtonsoft.Json.Linq.JToken commandData = null;

                    // Kiểm tra xem dữ liệu trả về có chứa từ khóa quan trọng không
                    if (e.Path.Contains("Command") || e.Path.Contains("System"))
                    {
                        // Nếu path là /System/Command -> Data chính là Command
                        if (e.Path.EndsWith("Command") || e.Key == "Command")
                        {
                            commandData = e.Data;
                        }
                        // Nếu path là /System -> Data chứa Command bên trong
                        else if (e.Data["Command"] != null)
                        {
                            commandData = e.Data["Command"];
                        }
                    }

                    // 3. XỬ LÝ LỆNH
                    if (commandData != null)
                    {
                        // Chuyển về Dictionary để lấy Action
                        var cmd = commandData.ToObject<Dictionary<string, string>>();

                        if (cmd != null && cmd.ContainsKey("Action") && cmd["Action"] == "BACKUP_WIPE")
                        {
                            string timeStr = cmd.ContainsKey("Time") ? cmd["Time"] : DateTime.Now.ToString("HH-mm dd-MM-yyyy");

                            Console.WriteLine($"[SYSTEM] ⚠️ Máy trạm đã nhận lệnh XÓA từ Server! Thời gian: {timeStr}");

                            // Gọi hàm xử lý backup (đã có Invoke bên trong để an toàn luồng)
                            HandleBackupCommand(timeStr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Bắt lỗi để biết tại sao không chạy
                    Console.WriteLine($"[SYSTEM-ERR] Lỗi xử lý lệnh Command: {ex.Message}");
                }
            };
        }

        private void HandleBackupCommand(string timeStr)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => HandleBackupCommand(timeStr))); return; }

            string safeTime = timeStr.Replace(":", "h");
            string fileName = $"Xóa {safeTime}";

            Console.WriteLine($"[SYSTEM] ⚠️ Nhận lệnh Backup khẩn cấp: {fileName}");
            _backupService?.PerformExport(isAuto: true, customFileName: fileName);
        }

        // --- SỰ KIỆN NÚT "XÓA DỮ LIỆU" ---

        private async void BtnClearData_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "CẢNH BÁO NGUY HIỂM!\n\n" +
                "Hành động này sẽ:\n" +
                "1. Yêu cầu TẤT CẢ các máy trạm sao lưu dữ liệu.\n" +
                "2. Sau 10 giây, sẽ XÓA SẠCH toàn bộ 9 bảng xét nghiệm, Tasks và Giao nhận.\n\n" +
                "Bạn có chắc chắn muốn tiếp tục không?",
                "Xác nhận Xóa Hệ Thống",
                MessageBoxButtons.YesNo, MessageBoxIcon.Error);

            if (confirm != DialogResult.Yes) return;

            try
            {
                // Gửi lệnh Backup
                string timeStamp = DateTime.Now.ToString("HH:mm dd-MM-yyyy");
                var cmd = new Dictionary<string, string> { { "Action", "BACKUP_WIPE" }, { "Time", timeStamp } };

                var firebase = new FirebaseService(FIREBASE_URL, FIREBASE_SECRET);
                await firebase.UpdateDataAsync("System/Command", cmd);

                Console.WriteLine("[CLEAR-OP] ⏳ Đã gửi lệnh. Đang chờ 10 giây...");

                // Khóa nút để tránh bấm nhiều lần
                if (sender is Button btn) btn.Enabled = false;

                await Task.Delay(10000); // Đợi 10s

                Console.WriteLine("[CLEAR-OP] 🗑️ Bắt đầu xóa dữ liệu...");
                string[] nodesToDelete = new string[]
                {
                    "T1_HuyetHoc_CongThucMau", "T1_HuyetHoc_DongMau", "T1_HuyetHoc_GiamSat", "T1_SinhHoa", "T1_MienDich",
                    "T3_HuyetHoc_CongThucMau", "T3_HuyetHoc_DongMau", "T3_HuyetHoc_GiamSat", "T3_SinhHoa_MienDich",
                    "T_Tasks", "T_Logistics_Matrix"
                };

                foreach (var node in nodesToDelete) await firebase.DeleteDataAsync(node);
                await firebase.DeleteDataAsync("System/Command");

                MessageBox.Show("Đã xóa toàn bộ dữ liệu thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (sender is Button btn) btn.Enabled = true;
            }
        }
    }
}