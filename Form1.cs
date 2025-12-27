using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Firebase.Database;
using Firebase.Database.Query;
using Excel = Microsoft.Office.Interop.Excel;
using System.IO;

namespace UBCS2_A
{
    public partial class Form1 : Form
    {
        #region 1. Khai báo biến và Cấu hình

        // Mật khẩu Admin
        const string PASSWORD_RESET = "9999";

        FirebaseClient? firebase;
        List<DataGridView> allGrids = new List<DataGridView>();
        bool isSyncing = false;
        bool isViewingHistory = false;
        System.Windows.Forms.Timer autoExportTimer = new System.Windows.Forms.Timer();

        // KHO DỮ LIỆU CHÍNH (Dùng Dictionary để tối ưu RAM)
        Dictionary<string, Dictionary<int, LuuMau>> virtualData = new Dictionary<string, Dictionary<int, LuuMau>>();

        // UI Code-Behind
        ComboBox cbKhuVuc = new ComboBox();
        Button btnResetDB = new Button(); // Nút đỏ góc trên

        public Form1()
        {
            InitializeComponent();

            // Kết nối Firebase
            firebase = new FirebaseClient("https://ubcs2-d311c-default-rtdb.asia-southeast1.firebasedatabase.app/",
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult("hD7JBy7P5hOCUqZnKuseumfp6u8yVBDrcfzeRDLT") });

            // Gom nhóm 9 bảng dữ liệu
            AddGridIfExist("DataCTMT1"); AddGridIfExist("DataDMT1"); AddGridIfExist("DataGST1");
            AddGridIfExist("DataSHT1"); AddGridIfExist("DataMDT1"); AddGridIfExist("DataCTMT3");
            AddGridIfExist("DataDMT3"); AddGridIfExist("DataGST3"); AddGridIfExist("DataSHMDT3");

            this.Load += async (s, e) => {
                SetupUI_KhuVuc();
                SetupGridsVirtualProperty();

                // 1. Khởi tạo TaskBoard
                if (uC_TaskBoard1 == null)
                {
                    uC_TaskBoard1 = new UC_TaskBoard { Dock = DockStyle.Fill };
                    if (this.Controls.ContainsKey("pnTaskContainer")) this.Controls["pnTaskContainer"].Controls.Add(uC_TaskBoard1);
                    else { uC_TaskBoard1.Location = new Point(400, 60); uC_TaskBoard1.Size = new Size(600, 400); this.Controls.Add(uC_TaskBoard1); uC_TaskBoard1.BringToFront(); }
                }

                string currentArea = cbKhuVuc.Items.Count > 0 ? cbKhuVuc.Items[0].ToString() : "Khách";
                ApplyPhanQuyen(currentArea);

                if (firebase != null) uC_TaskBoard1.Init(firebase, currentArea);

                // 2. Khởi tạo Chat (ĐÃ BẬT)
                if (uC_Chat1 != null && firebase != null)
                {
                    uC_Chat1.InitChat(firebase);
                }

                // 3. Tải dữ liệu bảng
                await InitData();
                SetupAutoExportTimer();
            };
        }

        void AddGridIfExist(string name) { var c = this.Controls.Find(name, true).FirstOrDefault(); if (c is DataGridView dgv) allGrids.Add(dgv); }

        void SetupUI_KhuVuc()
        {
            // Setup ComboBox chọn khu vực
            cbKhuVuc.Parent = this; cbKhuVuc.Width = 180; cbKhuVuc.DropDownStyle = ComboBoxStyle.DropDownList;
            cbKhuVuc.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            cbKhuVuc.Location = new Point(this.ClientSize.Width - cbKhuVuc.Width - 10, 10);
            cbKhuVuc.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cbKhuVuc.Items.AddRange(new string[] { "Tất cả (Admin)", "Khách (Chỉ xem)", "Sinh hóa Tầng 1", "Huyết học Tầng 1", "Miễn dịch Tầng 1", "Huyết học Tầng 3", "SH-MD Tầng 3" });

            // Setup Nút Reset Toàn Bộ (Góc trên)
            btnResetDB.Parent = this; btnResetDB.Text = "🔥 RESET DATA";
            btnResetDB.Font = new Font("Segoe UI", 9, FontStyle.Bold); btnResetDB.BackColor = Color.Maroon; btnResetDB.ForeColor = Color.White; btnResetDB.FlatStyle = FlatStyle.Flat; btnResetDB.Size = new Size(110, 29);
            btnResetDB.Location = new Point(cbKhuVuc.Left - btnResetDB.Width - 10, 10); btnResetDB.Anchor = AnchorStyles.Top | AnchorStyles.Right; btnResetDB.Cursor = Cursors.Hand;
            btnResetDB.Click += BtnResetDB_Click;

            cbKhuVuc.SelectedIndexChanged += (s, e) => {
                string selectedArea = cbKhuVuc.SelectedItem.ToString();
                ApplyPhanQuyen(selectedArea);
                if (uC_TaskBoard1 != null && firebase != null) uC_TaskBoard1.Init(firebase, selectedArea);
            };
            cbKhuVuc.SelectedIndex = 0; cbKhuVuc.BringToFront(); btnResetDB.BringToFront();
        }

        private string ShowPasswordInput() { Form prompt = new Form() { Width = 300, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Xác thực bảo mật", StartPosition = FormStartPosition.CenterScreen, MinimizeBox = false, MaximizeBox = false }; Label textLabel = new Label() { Left = 20, Top = 20, Text = "Nhập mật khẩu Admin để xóa:" }; TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 240, PasswordChar = '*' }; Button confirmation = new Button() { Text = "Xác nhận", Left = 160, Width = 100, Top = 80, DialogResult = DialogResult.OK, BackColor = Color.Red, ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; prompt.Controls.Add(textBox); prompt.Controls.Add(confirmation); prompt.Controls.Add(textLabel); prompt.AcceptButton = confirmation; return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : ""; }

        // ==========================================================================================
        // KHU VỰC XỬ LÝ XÓA DỮ LIỆU & KHÔI PHỤC (ĐÃ UPDATE CHUẨN LOGIC)
        // ==========================================================================================

        // 1. NÚT RESET TOÀN BỘ (Góc trên) -> Xóa HẾT (Format Factory)
        private async void BtnResetDB_Click(object sender, EventArgs e)
        {
            if (cbKhuVuc.SelectedItem.ToString() != "Tất cả (Admin)") return;

            if (MessageBox.Show("⛔ CẢNH BÁO NGUY HIỂM: Bạn đang chọn RESET TOÀN BỘ HỆ THỐNG!\n\n- Xóa 9 bảng dữ liệu.\n- Xóa toàn bộ Lịch sử.\n- Xóa toàn bộ Nhiệm vụ (Tasks).\n- Xóa toàn bộ tin nhắn Chat.\n\nHành động này KHÔNG THỂ hoàn tác!",
                "Cảnh báo Admin", MessageBoxButtons.YesNo, MessageBoxIcon.Error) != DialogResult.Yes) return;

            string inputPass = ShowPasswordInput();
            if (inputPass != PASSWORD_RESET) { MessageBox.Show("Mật khẩu sai!", "Bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Stop); return; }

            this.Cursor = Cursors.WaitCursor;
            try
            {
                var tasks = new List<Task>();
                // Xóa các phần phụ
                tasks.Add(firebase!.Child("History").DeleteAsync());
                tasks.Add(firebase!.Child("Tasks").DeleteAsync());
                tasks.Add(firebase!.Child("Chat").DeleteAsync());

                // Xóa 9 bảng dữ liệu chính
                foreach (var g in allGrids) tasks.Add(firebase.Child(g.Name).DeleteAsync());

                await Task.WhenAll(tasks);

                // Xóa RAM
                foreach (var g in allGrids)
                {
                    if (virtualData.ContainsKey(g.Name)) virtualData[g.Name].Clear();
                    g.Invalidate();
                }

                // Reset giao diện UserControls
                if (uC_TaskBoard1 != null) uC_TaskBoard1.Init(firebase!, "Tất cả (Admin)");
                if (uC_Chat1 != null) uC_Chat1.InitChat(firebase!); // Reset chat

                MessageBox.Show("Hệ thống đã được Format sạch sẽ!", "Thành công");
            }
            catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
            finally { this.Cursor = Cursors.Default; }
        }

        // 2. NÚT XÓA DỮ LIỆU BẢNG (Trong khung Sao lưu) -> CHỈ XÓA 9 BẢNG (CÓ BACKUP)
        public async void btnClearAll_Click(object sender, EventArgs e)
        {
            if (cbKhuVuc.SelectedItem.ToString() != "Tất cả (Admin)")
            {
                MessageBox.Show("Chỉ Admin mới được xóa dữ liệu bảng!", "Cấm", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            if (MessageBox.Show("Hệ thống sẽ:\n1. Tự động LƯU LỊCH SỬ hiện tại.\n2. XÓA TRẮNG 9 bảng dữ liệu.\n\n(Lịch sử, Task và Chat vẫn được giữ nguyên). Bạn chắc chắn?",
                "Xác nhận xóa bảng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            string inputPass = ShowPasswordInput();
            if (inputPass != PASSWORD_RESET) { MessageBox.Show("Mật khẩu sai!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            this.Cursor = Cursors.WaitCursor;
            try
            {
                // BƯỚC 1: BACKUP TRƯỚC KHI XÓA
                await BackupHistoryToFirebase();

                // BƯỚC 2: XÓA 9 BẢNG TRÊN FIREBASE
                var tasks = new List<Task>();
                foreach (var g in allGrids)
                {
                    tasks.Add(firebase!.Child(g.Name).DeleteAsync());
                }
                await Task.WhenAll(tasks);

                // BƯỚC 3: XÓA RAM
                foreach (var g in allGrids)
                {
                    if (virtualData.ContainsKey(g.Name)) virtualData[g.Name].Clear();
                    g.Invalidate();
                }

                MessageBox.Show("Đã lưu lịch sử và xóa trắng 9 bảng dữ liệu!", "Thành công");
            }
            catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
            finally { this.Cursor = Cursors.Default; }
        }

        // 3. NÚT BACKUP (THỦ CÔNG)
        public async void btnBackupNow_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Backup ngay?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                await BackupHistoryToFirebase();
                MessageBox.Show("Backup thành công!");
            }
        }

        // 4. HÀM BACKUP HISTORY (Logic lõi: Chuyển int key -> string key để lưu Dictionary JSON)
        async Task BackupHistoryToFirebase()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                foreach (var entry in virtualData)
                {
                    // Chuyển đổi Key int (0,1,2...) thành String ("1","2","3"...) để Firebase hiểu là Map
                    var map = entry.Value.ToDictionary(k => (k.Key + 1).ToString(), v => v.Value);
                    await firebase!.Child("History").Child(timestamp).Child(entry.Key).PutAsync(map);
                }
                // Xóa bớt bản backup cũ (Giữ lại 3 bản gần nhất)
                var allBackups = await firebase!.Child("History").OrderByKey().OnceAsync<object>();
                if (allBackups.Count > 3) foreach (var item in allBackups.Take(allBackups.Count - 3)) await firebase!.Child("History").Child(item.Key).DeleteAsync();
            }
            catch { }
        }

        // 5. NÚT TẢI LỊCH SỬ
        public async void btnLoadHistory_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menuHistory = new ContextMenuStrip();
            menuHistory.Items.Add("Đang tải...", null);
            menuHistory.Show((Control)sender, new Point(0, ((Control)sender).Height));
            try
            {
                var snapshots = await firebase!.Child("History").OrderByKey().LimitToLast(3).OnceAsync<object>();
                menuHistory.Items.Clear();
                if (snapshots.Count == 0) menuHistory.Items.Add("Chưa có lịch sử.");
                else foreach (var snap in snapshots.Reverse())
                    {
                        string keyTS = snap.Key;
                        string display = keyTS;
                        try { DateTime dt = DateTime.ParseExact(keyTS, "yyyyMMdd_HHmmss", null); display = "Bản lưu: " + dt.ToString("dd/MM/yyyy HH:mm:ss"); } catch { }
                        menuHistory.Items.Add(display, null, async (s, ev) => { await LoadHistoryDate(keyTS); });
                    }
                menuHistory.Show((Control)sender, new Point(0, ((Control)sender).Height));
            }
            catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
        }

        // 6. HÀM ĐỌC LỊCH SỬ TỪ FIREBASE (Đã sửa lỗi ép kiểu List/Dictionary)
        async Task LoadHistoryDate(string keyTimestamp)
        {
            isSyncing = true;
            isViewingHistory = true;
            this.Text = $"ĐANG XEM LỊCH SỬ: {keyTimestamp}";

            try
            {
                foreach (var g in allGrids)
                {
                    // 1. Xóa sạch dữ liệu trên RAM để chuẩn bị nạp mới
                    virtualData[g.Name] = new Dictionary<int, LuuMau>();

                    // --- GIẢI PHÁP HYBRID: THỬ LIST TRƯỚC, NẾU LỖI THÌ THỬ DICTIONARY ---
                    try
                    {
                        // CÁCH 1: Thử đọc dạng LIST (Vì Firebase hay tự chuyển "1","2","3" thành mảng)
                        // Lưu ý: List có thể chứa null (vì index 0 thường bị null) nên dùng LuuMau?
                        var listData = await firebase!
                            .Child("History")
                            .Child(keyTimestamp)
                            .Child(g.Name)
                            .OnceSingleAsync<List<LuuMau?>>();

                        if (listData != null)
                        {
                            foreach (var item in listData)
                            {
                                if (item == null) continue; // Bỏ qua phần tử null (thường là index 0)

                                int idx = item.STT - 1;
                                if (idx >= 0 && idx < 1000)
                                {
                                    virtualData[g.Name][idx] = new LuuMau { STT = item.STT, SID = item.SID ?? "" };
                                }
                            }
                        }
                    }
                    catch
                    {
                        // CÁCH 2: Nếu Cách 1 lỗi, thử đọc dạng DICTIONARY (Dữ liệu thưa thớt)
                        try
                        {
                            var dictData = await firebase!
                                .Child("History")
                                .Child(keyTimestamp)
                                .Child(g.Name)
                                .OnceSingleAsync<Dictionary<string, LuuMau>>();

                            if (dictData != null)
                            {
                                foreach (var kvp in dictData)
                                {
                                    var item = kvp.Value;
                                    if (item != null)
                                    {
                                        int idx = item.STT - 1;
                                        if (idx >= 0 && idx < 1000)
                                        {
                                            virtualData[g.Name][idx] = new LuuMau { STT = item.STT, SID = item.SID ?? "" };
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Nếu cả 2 cách đều lỗi thì coi như bảng đó trống */ }
                    }

                    // Vẽ lại giao diện
                    g.Invalidate();
                }
                MessageBox.Show($"Đã tải bản lưu: {keyTimestamp}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải lịch sử: " + ex.Message);
                isViewingHistory = false;
                this.Text = "Quản Lý Mẫu";
            }
            finally
            {
                isSyncing = false;
            }
        }


        // 7. NÚT KHÔI PHỤC (Restore từ History lên Live)
        public async void btnRestore_Click(object sender, EventArgs e)
        {
            if (!isViewingHistory) { MessageBox.Show("Hãy tải Lịch sử trước."); return; }

            if (MessageBox.Show("GHI ĐÈ dữ liệu hiện tại bằng bản lịch sử này?", "Cảnh báo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    isSyncing = true;
                    var tasks = new List<Task>();
                    foreach (var g in allGrids)
                    {
                        if (virtualData.ContainsKey(g.Name))
                        {
                            var dataToRestore = virtualData[g.Name];
                            tasks.Add(Task.Run(async () => {
                                await firebase!.Child(g.Name).DeleteAsync();
                                // Chuyển Key int sang string để đúng format Dictionary
                                if (dataToRestore.Count > 0)
                                {
                                    var map = dataToRestore.ToDictionary(k => (k.Key + 1).ToString(), v => v.Value);
                                    await firebase.Child(g.Name).PutAsync(map);
                                }
                            }));
                        }
                    }
                    await Task.WhenAll(tasks);
                    MessageBox.Show("Khôi phục thành công!");
                    btnBackToPresent_Click(sender, e);
                }
                catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
                finally { isSyncing = false; }
            }
        }

        // 8. NÚT VỀ HIỆN TẠI
        public async void btnBackToPresent_Click(object sender, EventArgs e)
        {
            if (!isViewingHistory) return;

            this.Cursor = Cursors.WaitCursor;
            isViewingHistory = false;
            this.Text = "Quản Lý Mẫu";
            ApplyPhanQuyen(cbKhuVuc.SelectedItem?.ToString() ?? "Khách");
            await InitData();
            this.Cursor = Cursors.Default;
            MessageBox.Show("Đã trở về hiện tại.");
        }

        // ==========================================================================================

        void ApplyPhanQuyen(string tenKhuVuc)
        {
            var luatPhanQuyen = new Dictionary<string, string[]> { { "Sinh hóa Tầng 1", new[] { "DataSHT1" } }, { "Huyết học Tầng 1", new[] { "DataCTMT1", "DataDMT1", "DataGST1" } }, { "Miễn dịch Tầng 1", new[] { "DataMDT1" } }, { "Huyết học Tầng 3", new[] { "DataCTMT3", "DataDMT3", "DataGST3" } }, { "SH-MD Tầng 3", new[] { "DataSHMDT3" } } };
            foreach (var g in allGrids) { bool duocPhepSua = (tenKhuVuc == "Tất cả (Admin)") || (luatPhanQuyen.ContainsKey(tenKhuVuc) && luatPhanQuyen[tenKhuVuc].Contains(g.Name)); if (tenKhuVuc == "Khách (Chỉ xem)") duocPhepSua = false; g.ReadOnly = !duocPhepSua; g.DefaultCellStyle.BackColor = duocPhepSua ? Color.White : Color.FromArgb(240, 240, 240); g.DefaultCellStyle.ForeColor = duocPhepSua ? Color.Black : Color.DimGray; }
            btnResetDB.Visible = (tenKhuVuc == "Tất cả (Admin)");
        }

        private void SetupGridsVirtualProperty()
        {
            foreach (var g in allGrids) { g.Tag = g.Name; g.VirtualMode = true; g.AllowUserToAddRows = false; typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(g, true); g.CellValueNeeded += DataGridView_CellValueNeeded; g.CellValuePushed += DataGridView_CellValuePushed; g.CellFormatting += (s, e) => { if (e.ColumnIndex == 1 && e.Value != null && !string.IsNullOrEmpty(e.Value.ToString())) { if (s is DataGridView grid && virtualData.ContainsKey(grid.Name)) { string currentSid = e.Value.ToString(); int count = virtualData[grid.Name].Values.Count(x => x.SID == currentSid); if (count > 1) e.CellStyle.BackColor = Color.Pink; else if (isViewingHistory) e.CellStyle.BackColor = Color.LightYellow; } } }; }
        }
        #endregion

        #region 2. Xử lý Dữ liệu
        async Task InitData() { isSyncing = true; try { foreach (var g in allGrids) { virtualData[g.Name] = new Dictionary<int, LuuMau>(); g.RowCount = 1000; } var loadTasks = allGrids.Select(g => LoadGridVirtual(g, g.Name)).ToList(); await Task.WhenAll(loadTasks); foreach (var g in allGrids) firebase!.Child(g.Name).AsObservable<LuuMau>().Subscribe(d => { UpdateGridVirtualRealtime(g, d.Key, d.Object); }); } catch (Exception ex) { MessageBox.Show("Lỗi khởi tạo: " + ex.Message); } finally { isSyncing = false; } }
        async Task LoadGridVirtual(DataGridView g, string node) { try { var data = await firebase!.Child(node).OnceAsync<LuuMau>(); if (data != null) { foreach (var d in data) { if (d?.Object != null) { int idx = d.Object.STT - 1; if (idx >= 0 && idx < 1000) { if (!virtualData[g.Name].ContainsKey(idx)) virtualData[g.Name][idx] = new LuuMau { STT = d.Object.STT }; virtualData[g.Name][idx].SID = d.Object.SID ?? ""; } } } } g.Invoke((Action)(() => g.Invalidate())); } catch { } }
        private void DataGridView_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e) { if (sender is DataGridView g && virtualData.ContainsKey(g.Name)) { if (virtualData[g.Name].ContainsKey(e.RowIndex)) { var rowData = virtualData[g.Name][e.RowIndex]; if (e.ColumnIndex == 0) e.Value = rowData.STT; else if (e.ColumnIndex == 1) e.Value = rowData.SID; } else { if (e.ColumnIndex == 0) e.Value = e.RowIndex + 1; else if (e.ColumnIndex == 1) e.Value = ""; } } }
        private async void DataGridView_CellValuePushed(object sender, DataGridViewCellValueEventArgs e) { if (isViewingHistory) { MessageBox.Show("Đang xem Lịch sử. Cấm sửa!"); if (sender is DataGridView g) g.InvalidateRow(e.RowIndex); return; } if (sender is DataGridView grid && grid.ReadOnly) return; if (sender is DataGridView gridRef && e.ColumnIndex == 1 && virtualData.ContainsKey(gridRef.Name)) { string newVal = e.Value?.ToString() ?? ""; int rowIndex = e.RowIndex; int stt = rowIndex + 1; if (!virtualData[gridRef.Name].ContainsKey(rowIndex)) { virtualData[gridRef.Name][rowIndex] = new LuuMau { STT = stt, SID = "" }; } virtualData[gridRef.Name][rowIndex].SID = newVal; gridRef.Invalidate(); try { await firebase!.Child(gridRef.Name).Child(stt.ToString()).PutAsync(new LuuMau { STT = stt, SID = newVal }); } catch { } } }
        void UpdateGridVirtualRealtime(DataGridView g, string key, LuuMau? d) { if (isSyncing || isViewingHistory) return; if (int.TryParse(key, out int stt) && virtualData.ContainsKey(g.Name)) { int idx = stt - 1; if (idx >= 0 && idx < 1000) { if (!virtualData[g.Name].ContainsKey(idx)) virtualData[g.Name][idx] = new LuuMau { STT = stt }; virtualData[g.Name][idx].SID = d?.SID ?? ""; g.Invoke((Action)(() => g.InvalidateRow(idx))); } } else if (d == null && string.IsNullOrEmpty(key)) { if (virtualData.ContainsKey(g.Name)) { virtualData[g.Name].Clear(); g.Invoke((Action)(() => g.Invalidate())); } } }
        #endregion

        #region 3. TÌM KIẾM
        public void txtSearchSID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; lstResults.Items.Clear(); string f = txtSearchSID.Text.ToLower().Trim(); if (string.IsNullOrEmpty(f)) return;

                // 1. Tìm trong Grid
                foreach (var gName in virtualData.Keys) { var g = allGrids.FirstOrDefault(x => x.Name == gName); if (g == null) continue; foreach (var item in virtualData[gName].Values) { if (item.SID.ToLower().Contains(f)) lstResults.Items.Add(new SearchResult { DisplayText = $"[{gName}] Dòng {item.STT}: {item.SID}", TargetGrid = g, TargetIndex = item.STT - 1 }); } }

                // 2. Tìm trong TaskBoard
                var tasksFound = uC_TaskBoard1.PublicSearch(f); foreach (var task in tasksFound) { lstResults.Items.Add(new SearchResult { DisplayText = $"[NHIỆM VỤ] {task.TrangThai}: {task.SID} ({task.MoTa})", TargetGrid = null, IsTaskBoardItem = true, TaskID = task.ID }); }

                if (lstResults.Items.Count > 0) lstResults.SelectedIndex = 0;
            }
        }

        public void lstResults_SelectedIndexChanged(object sender, EventArgs e) { if (lstResults.SelectedItem is SearchResult r) { if (r.IsTaskBoardItem) { uC_TaskBoard1.PublicHighlight(r.TaskID); uC_TaskBoard1.Focus(); } else if (r.TargetGrid != null) { Control p = r.TargetGrid.Parent; while (p != null) { if (p is TabPage tp && tp.Parent is TabControl tc) tc.SelectedTab = tp; p = p.Parent; } r.TargetGrid.BeginInvoke((Action)(delegate { try { r.TargetGrid.Focus(); r.TargetGrid.CurrentCell = r.TargetGrid.Rows[r.TargetIndex].Cells[1]; r.TargetGrid.Rows[r.TargetIndex].Selected = true; int sIdx = r.TargetIndex - 5; r.TargetGrid.FirstDisplayedScrollingRowIndex = sIdx < 0 ? 0 : sIdx; } catch { } })); } } }

        void SetupAutoExportTimer() { autoExportTimer.Interval = 60000; autoExportTimer.Tick += async (s, e) => { if (DateTime.Now.Hour == 0 && DateTime.Now.Minute == 0) { string path = Path.Combine(Application.StartupPath, "AutoExport"); if (!Directory.Exists(path)) Directory.CreateDirectory(path); ExportToExcel(Path.Combine(path, $"LuuMau_{DateTime.Now:dd-MM-yyyy}.xlsx"), false); await BackupHistoryToFirebase(); } }; autoExportTimer.Start(); }
        public void btnExport_Click(object sender, EventArgs e) { SaveFileDialog s = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"LuuMau_{DateTime.Now:dd-MM-yyyy}" }; if (s.ShowDialog() == DialogResult.OK) ExportToExcel(s.FileName, true); }
        void ExportToExcel(string fP, bool m) { Excel.Application? a = null; Excel.Workbook? b = null; Excel.Worksheet? w = null; try { a = new Excel.Application(); a.DisplayAlerts = false; b = a.Workbooks.Add(); int i = 1; foreach (var kv in virtualData) { if (i <= b.Sheets.Count) w = (Excel.Worksheet)b.Sheets[i]; else w = (Excel.Worksheet)b.Sheets.Add(After: b.Sheets[b.Sheets.Count]); w.Name = kv.Key; var sortedData = kv.Value.OrderBy(x => x.Key).ToList(); object[,] d = new object[sortedData.Count + 1, 2]; d[0, 0] = "STT"; d[0, 1] = "SID"; for (int r = 0; r < sortedData.Count; r++) { d[r + 1, 0] = sortedData[r].Value.STT; d[r + 1, 1] = sortedData[r].Value.SID; } w.Range["A1", $"B{sortedData.Count + 1}"].Value = d; i++; } while (b.Sheets.Count > virtualData.Count) ((Excel.Worksheet)b.Sheets[virtualData.Count + 1]).Delete(); b.SaveAs(fP); if (m) MessageBox.Show("Xuất file thành công!"); } catch (Exception ex) { if (m) MessageBox.Show("Lỗi: " + ex.Message); } finally { if (w != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(w); if (b != null) { b.Close(); System.Runtime.InteropServices.Marshal.ReleaseComObject(b); } if (a != null) { a.Quit(); System.Runtime.InteropServices.Marshal.ReleaseComObject(a); } GC.Collect(); } }
        #endregion

        // Stub Designer
        public void DataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e) { }
    }

    public class LuuMau { public int STT { get; set; } public string SID { get; set; } = ""; }
    public class SearchResult { public string DisplayText { get; set; } = ""; public DataGridView? TargetGrid { get; set; } public int TargetIndex { get; set; } public bool IsTaskBoardItem { get; set; } = false; public string TaskID { get; set; } = ""; public override string ToString() => DisplayText; }
    public class NhiemVu
    {
        public string ID { get; set; }
        public string SID { get; set; }
        public string NguoiGiao { get; set; }
        public string NguoiNhan { get; set; }
        public string MoTa { get; set; }
        public string TrangThai { get; set; }
        public DateTime ThoiGian { get; set; }

        // --- ĐÃ THÊM THUỘC TÍNH NÀY ---
        public bool IsKhan { get; set; } = false;
    }
}