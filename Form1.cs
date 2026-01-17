using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Services;

namespace UBCS2_A
{
    public partial class Form1 : Form
    {
        // CẤU HÌNH HỆ THỐNG
        private const string FIREBASE_URL = "https://streamingub-default-rtdb.asia-southeast1.firebasedatabase.app/";
        private const string FIREBASE_SECRET = "APK7gK1JBKh2XUR4laAcTP8P9CxpZoZKBkZ9bm23";
        // [THÊM DÒNG NÀY] Khai báo biến toàn cục để các file partial (Logistics, Task...) đều dùng được
        private FirebaseService _firebaseService;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("[FORM-CORE] 🏁 KHỞI ĐỘNG HỆ THỐNG (PARTIAL CLASSES)...");

            try
            {
                // 1. KẾT NỐI SERVER
                _firebaseService = new FirebaseService(FIREBASE_URL, FIREBASE_SECRET);

                // 2. KHỞI TẠO TỪNG PHÂN HỆ
                SetupLabSystem(_firebaseService);       // Form1.Lab.cs
                SetupLogisticsSystem(_firebaseService); // Form1.Logistics.cs
                SetupTaskSystem(_firebaseService);      // Form1.Task.cs
                SetupChatSystem(_firebaseService);      // Form1.Chat.cs

                // 3. CÁC TÍNH NĂNG CHUNG
                SetupAuthorization();           // Form1.Auth.cs
                SetupSearchSystem();            // Form1.Search.cs
                                                // Setup Backup nằm bên Form1.Backup.cs
                SetupBackupSystem();
                SetupSystemCommands(_firebaseService);

                // 4. KIỂM TRA CẬP NHẬT (QUAN TRỌNG: Chạy trước hoặc song song)
                // [NEW] Thêm dòng này vào
                _ = CheckForUpdatesAsync(_firebaseService);

                // 5. KÍCH HOẠT ĐỒNG BỘ (Chạy song song)
                this.Text = "Quản lý Phòng Xét Nghiệm - Đang tải dữ liệu...";

                await Task.WhenAll(
                    StartLabSyncAsync(),
                    StartLogisticsSyncAsync(),
                    StartTaskSyncAsync(),
                    StartChatSyncAsync()
                );

                this.Text = "Quản lý Phòng Xét Nghiệm (Sẵn sàng ✅)";
                Console.WriteLine("[FORM-CORE] ✅ HỆ THỐNG SẴN SÀNG.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi động: {ex.Message}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ... (Giữ nguyên phần BackupService và OnFormClosing như cũ) ...

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _backupService?.StopAutoBackup();
            _context?.Dispose();
            _logisticsContext?.Dispose();
            _chatContext?.Dispose();
            _taskContext?.Dispose();
            base.OnFormClosing(e);
        }

    }
}