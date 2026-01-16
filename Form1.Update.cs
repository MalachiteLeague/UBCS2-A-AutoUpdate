using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Models;
using UBCS2_A.Services;

namespace UBCS2_A
{
    public partial class Form1
    {
        // Định nghĩa phiên bản hiện tại của App (Bạn nhớ sửa số này mỗi khi build bản mới)
        private const string CURRENT_VERSION = "1.0.0";

        /// <summary>
        /// Hàm kiểm tra cập nhật (Chạy ngầm).
        /// </summary>
        private async Task CheckForUpdatesAsync(FirebaseService firebase)
        {
            try
            {
                Console.WriteLine("[UPDATE] 🔄 Đang kiểm tra phiên bản mới...");

                // 1. Lấy thông tin từ node "SystemInfo" trên Firebase
                // (Dựa vào SystemInfoModel.cs bạn đã có)
                var info = await firebase.GetDataAsync<SystemInfoModel>("SystemInfo");

                if (info != null && !string.IsNullOrEmpty(info.Version))
                {
                    // 2. So sánh phiên bản
                    // (Logic đơn giản: Khác nhau là báo update. Có thể nâng cấp lên so sánh lớn hơn/nhỏ hơn sau)
                    if (info.Version != CURRENT_VERSION)
                    {
                        Console.WriteLine($"[UPDATE] 🚀 Phát hiện bản mới: {info.Version} (Hiện tại: {CURRENT_VERSION})");

                        string msg = $"Đã có phiên bản mới: {info.Version}\n" +
                                     $"Phiên bản hiện tại: {CURRENT_VERSION}\n\n" +
                                     "Bạn có muốn tải về cập nhật ngay không?";

                        // Hiện Popup hỏi người dùng (Chạy trên UI Thread)
                        if (MessageBox.Show(msg, "Cập nhật phần mềm", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            // 3. Mở trình duyệt tải về
                            if (!string.IsNullOrEmpty(info.DownloadUrl))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = info.DownloadUrl,
                                    UseShellExecute = true // Quan trọng để mở link trên trình duyệt mặc định
                                });

                                // 4. Đóng App để người dùng cài đặt
                                Application.Exit();
                            }
                            else
                            {
                                MessageBox.Show("Link tải bị lỗi. Vui lòng liên hệ Admin.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[UPDATE] ✅ Phần mềm đang là phiên bản mới nhất.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Lỗi update không nên làm crash app, chỉ log ra thôi
                Console.WriteLine($"[UPDATE-ERR] ❌ Lỗi kiểm tra cập nhật: {ex.Message}");
            }
        }
    }
}