using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Services;
using UBCS2_A.Models;
using System.Drawing; // Thư viện để vẽ giao diện Popup (Form, ProgressBar...)

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [HELPER] Quản lý tính năng tự động cập nhật (Auto-Update).
    /// - [FIX] Sửa lỗi parse version (loại bỏ hậu tố +git_hash).
    /// - Cơ chế: Tải file .tmp -> Tạo file .bat -> Tắt App -> Bat xóa App cũ & đổi tên App mới -> Bật lại.
    /// </summary>
    public class UpdateManager
    {
        private readonly FirebaseService _firebaseService;

        // Lấy tên file EXE hiện tại đang chạy (VD: UBCS2-A.exe)
        private readonly string _currentExeName = Path.GetFileName(Application.ExecutablePath);

        public UpdateManager(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        /// <summary>
        /// Hàm kiểm tra và thực hiện cập nhật (Gọi hàm này ở cuối Form1_Load).
        /// </summary>
        public async Task CheckForUpdateAsync()
        {
            Console.WriteLine("[UPDATER] 🔍 Đang kiểm tra phiên bản mới...");

            try
            {
                // 1. Lấy thông tin từ Firebase (Node: SystemInfo)
                var info = await _firebaseService.GetDataAsync<SystemInfoModel>("SystemInfo");

                if (info == null || string.IsNullOrEmpty(info.DownloadUrl))
                {
                    Console.WriteLine("[UPDATER] ⚠️ Không tìm thấy thông tin Update trên Server.");
                    return;
                }

                // 2. Lấy Version hiện tại của App
                // Trong .NET 8, chuỗi này thường có dạng: "1.0.36+ab123cde..." (Kèm mã Hash Git)
                string currentVersionStr = Application.ProductVersion;

                // [FIX QUAN TRỌNG] Cắt bỏ phần đuôi rác (+...) để tránh lỗi "Input string was not in a correct format"
                if (currentVersionStr.Contains("+"))
                {
                    currentVersionStr = currentVersionStr.Split('+')[0];
                }

                Console.WriteLine($"[UPDATER] So sánh: Máy ({currentVersionStr}) vs Server ({info.Version})");

                // 3. So sánh phiên bản
                Version currentVer = Version.Parse(currentVersionStr);
                Version newVer = Version.Parse(info.Version);

                if (newVer > currentVer)
                {
                    // CÓ BẢN MỚI -> Hỏi người dùng
                    DialogResult dr = MessageBox.Show(
                        $"Phát hiện phiên bản mới: {info.Version}\n\nBạn có muốn tải về và cập nhật ngay không?",
                        "Cập nhật phần mềm",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dr == DialogResult.Yes)
                    {
                        await PerformUpdateProcess(info.DownloadUrl);
                    }
                }
                else
                {
                    Console.WriteLine("[UPDATER] ✅ Phần mềm đang ở phiên bản mới nhất.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UPDATER-ERR] ❌ Lỗi kiểm tra update: {ex.Message}");
            }
        }

        /// <summary>
        /// Quy trình tải file, tạo script .bat và tráo đổi file.
        /// </summary>
        private async Task PerformUpdateProcess(string url)
        {
            // A. Tạo giao diện Popup tiến trình (Code-First UI)
            Form progressForm = new Form()
            {
                Text = "Đang tải bản cập nhật...",
                Size = new Size(450, 150),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false, // Không hiện nút tắt X để bắt buộc đợi
                TopMost = true
            };

            Label lblStatus = new Label()
            {
                Text = "Đang kết nối tới máy chủ...",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            ProgressBar progressBar = new ProgressBar()
            {
                Location = new Point(20, 50),
                Width = 390,
                Height = 30,
                Style = ProgressBarStyle.Continuous
            };

            progressForm.Controls.Add(lblStatus);
            progressForm.Controls.Add(progressBar);
            progressForm.Show();

            try
            {
                // B. Tải file về dạng .tmp (Tránh xung đột file đang chạy)
                string tempFilePath = Path.Combine(Application.StartupPath, "update_temp.exe");

                using (HttpClient client = new HttpClient())
                {
                    // Giả lập User-Agent để Github/Server không chặn request
                    client.DefaultRequestHeaders.Add("User-Agent", "UBCS2-Updater");
                    client.Timeout = TimeSpan.FromMinutes(5); // Tăng timeout cho mạng chậm

                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[8192]; // Đọc từng cục 8KB
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue)
                                {
                                    int progress = (int)((totalRead * 100) / totalBytes.Value);

                                    // Cập nhật giao diện (Invoke về luồng UI)
                                    progressForm.Invoke(new Action(() => {
                                        progressBar.Value = progress;
                                        double mbRead = Math.Round(totalRead / 1024.0 / 1024.0, 2);
                                        lblStatus.Text = $"Đang tải... {progress}% ({mbRead} MB)";
                                    }));
                                }
                            }
                        }
                    }
                }

                progressForm.Close(); // Tải xong thì tắt popup

                // C. Tạo file BAT để thực hiện tráo đổi (Magic happens here)
                // Kịch bản:
                // 1. Chờ 2 giây cho App chính tắt hẳn.
                // 2. Xóa file exe cũ.
                // 3. Đổi tên file update_temp.exe thành tên exe chính.
                // 4. Bật lại App mới.
                // 5. Tự xóa file bat.

                string batchScriptPath = Path.Combine(Application.StartupPath, "updater.bat");
                string script = $@"
@echo off
timeout /t 2 /nobreak > NUL
del ""{_currentExeName}""
move ""update_temp.exe"" ""{_currentExeName}""
start """" ""{_currentExeName}""
del ""%~f0""
";
                File.WriteAllText(batchScriptPath, script);

                // D. Chạy file BAT và Tự sát (Tắt App hiện tại)
                ProcessStartInfo psi = new ProcessStartInfo(batchScriptPath)
                {
                    CreateNoWindow = true,       // Chạy ngầm không hiện cửa sổ đen
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                Application.Exit(); // [QUAN TRỌNG] Phải tắt App thì lệnh 'del' bên kia mới chạy được
            }
            catch (Exception ex)
            {
                progressForm.Close();
                MessageBox.Show($"Lỗi cập nhật: {ex.Message}\nVui lòng thử lại sau.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}