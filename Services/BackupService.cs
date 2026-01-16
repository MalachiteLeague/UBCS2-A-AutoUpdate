using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using UBCS2_A.Helpers;

namespace UBCS2_A.Services
{
    public class BackupService
    {
        private readonly LabDataContext _labContext;
        private readonly MatrixManager _matrixManager;
        private readonly string _saveFolder = "Thư mục lưu mẫu";
        private System.Windows.Forms.Timer _backupTimer;

        public BackupService(LabDataContext labContext, MatrixManager matrixManager)
        {
            _labContext = labContext;
            _matrixManager = matrixManager;

            if (!Directory.Exists(_saveFolder)) Directory.CreateDirectory(_saveFolder);

            // Timer backup tự động 5 phút
            _backupTimer = new System.Windows.Forms.Timer();
            _backupTimer.Interval = 5 * 60 * 1000;
            _backupTimer.Tick += (s, e) => PerformExport(isAuto: true);
        }

        public void StartAutoBackup() { _backupTimer.Start(); }
        public void StopAutoBackup() { _backupTimer.Stop(); }

        /// <summary>
        /// Hàm xuất dữ liệu.
        /// [UPDATE] Thêm tham số customFileName để hỗ trợ lệnh Xóa dữ liệu.
        /// </summary>
        public string PerformExport(bool isAuto, string customFileName = null)
        {
            try
            {
                // 1. Thu thập dữ liệu
                var allLines = new List<string>();
                allLines.Add($"BÁO CÁO DỮ LIỆU TOÀN HỆ THỐNG");
                allLines.Add($"Thời điểm: {DateTime.Now:HH:mm dd/MM/yyyy}");
                allLines.Add("");

                if (_matrixManager != null) allLines.AddRange(_matrixManager.GetExportData());
                if (_labContext != null) allLines.AddRange(_labContext.GetAllLabDataForExport());

                // 2. Xác định tên file
                string fileName;
                if (!string.IsNullOrEmpty(customFileName))
                {
                    // Nếu có tên tùy chỉnh (VD: Xóa 10-30 14-01-2025) thì dùng luôn
                    fileName = customFileName + ".csv";
                }
                else
                {
                    // Mặc định: Lưu mẫu dd-MM-yyyy
                    string dateString = DateTime.Now.ToString("dd-MM-yyyy");
                    fileName = $"Lưu mẫu {dateString}.csv";
                }

                string finalPath = Path.Combine(_saveFolder, fileName);

                // 3. Logic File Phụ (Tránh lỗi file đang mở)
                int counter = 1;
                string baseNameWithoutExt = Path.GetFileNameWithoutExtension(finalPath);

                while (IsFileLocked(finalPath))
                {
                    string tempName = $"{baseNameWithoutExt} ({counter}).csv";
                    finalPath = Path.Combine(_saveFolder, tempName);
                    counter++;
                    if (counter > 10) return null;
                }

                // 4. Ghi file
                File.WriteAllLines(finalPath, allLines, Encoding.UTF8);

                if (!isAuto) Console.WriteLine($"[BACKUP] ✅ Đã lưu file: {Path.GetFileName(finalPath)}");
                return finalPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKUP-ERR] ❌ {ex.Message}");
                return null;
            }
        }

        private bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try { using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None)) { stream.Close(); } }
            catch (IOException) { return true; }
            return false;
        }
    }
}