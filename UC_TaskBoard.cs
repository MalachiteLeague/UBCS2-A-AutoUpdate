using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Firebase.Database;
using Firebase.Database.Query;
using System.Reactive.Linq;
using System.Media;
using System.Linq;

namespace UBCS2_A
{
    public partial class UC_TaskBoard : UserControl
    {
        const string PASSWORD_DELETE_ALL = "9999";

        // Font chữ chuẩn
        private readonly Font _fontNormal = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        // Font cho mẫu Khẩn (To hơn chút và Đậm)
        private readonly Font _fontUrgent = new Font("Segoe UI", 11F, FontStyle.Bold);
        // Font gạch ngang (cho mẫu hủy)
        private readonly Font _fontStrikeout = new Font("Segoe UI", 9.5F, FontStyle.Strikeout | FontStyle.Bold);

        private IDisposable? _firebaseSubscription;
        private bool _isAlertShowing = false;

        private Dictionary<string, NhiemVu> _taskCache = new Dictionary<string, NhiemVu>();

        // UI Components
        private TextBox txtInputSID;
        private Button btnDeleteAll;
        private Button btnAction;

        private DataGridView dgvTodo, dgvProgress, dgvDone;
        private ContextMenuStrip ctxMenuAdmin;
        private GroupBox grpDetail;
        private TextBox txtDetailDesc;
        private Label lblDetailInfo;
        private Label lblStatus;

        private FirebaseClient? _firebase;
        private string _myArea = "";
        private NhiemVu? _currentTask = null;

        public UC_TaskBoard()
        {
            SetupUI();
            // Dọn dẹp tài nguyên Font khi đóng form
            this.Disposed += (s, e) => {
                _firebaseSubscription?.Dispose();
                _fontNormal.Dispose();
                _fontStrikeout.Dispose();
                _fontUrgent.Dispose();
            };
        }

        public void Init(FirebaseClient firebase, string myArea)
        {
            _firebaseSubscription?.Dispose(); _firebase = firebase; _myArea = myArea;
            _taskCache.Clear();
            dgvTodo.Rows.Clear(); dgvProgress.Rows.Clear(); dgvDone.Rows.Clear();
            ClearDetailPanel();
            if (btnDeleteAll != null) btnDeleteAll.Visible = (_myArea == "Tất cả (Admin)");
            ListenToFirebaseGlobal();
        }

        public List<NhiemVu> PublicSearch(string sid) => _taskCache.Values.Where(t => t.SID.ToLower().Contains(sid.ToLower())).ToList();

        public void PublicHighlight(string taskID)
        {
            void FindAndSelect(DataGridView dgv)
            {
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.Tag is NhiemVu t && t.ID == taskID)
                    {
                        dgv.ClearSelection(); row.Selected = true; dgv.CurrentCell = row.Cells[0];
                        _currentTask = t; UpdateDetailPanel(t); return;
                    }
                }
            }
            FindAndSelect(dgvTodo); FindAndSelect(dgvProgress); FindAndSelect(dgvDone);
        }

        private void SetupUI()
        {
            this.Dock = DockStyle.Fill; this.BackColor = Color.WhiteSmoke;
            Font stdFont = new Font("Segoe UI", 8.25F);

            ctxMenuAdmin = new ContextMenuStrip();
            var itemDelete = new ToolStripMenuItem("🗑 Xóa nhiệm vụ này") { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.Red };
            itemDelete.Click += ItemDelete_Click;
            ctxMenuAdmin.Items.Add(itemDelete);

            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.White };
            Label lblHD = new Label { Text = "Giao SID:", Top = 9, Left = 5, AutoSize = true, Font = new Font("Segoe UI", 8.25F, FontStyle.Bold), ForeColor = Color.DimGray };
            txtInputSID = new TextBox { Top = 7, Left = 65, Width = 120, Font = stdFont };
            txtInputSID.KeyDown += TxtInputSID_KeyDown;

            Label lblArea = new Label { Text = _myArea, Top = 9, AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 7F, FontStyle.Italic), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            lblArea.Left = this.Width - 100;
            pnlTop.Controls.AddRange(new Control[] { lblHD, txtInputSID, lblArea });

            grpDetail = new GroupBox { Dock = DockStyle.Bottom, Height = 150, Text = "Chi tiết", Font = new Font("Segoe UI", 8.25F, FontStyle.Bold), BackColor = Color.White, Padding = new Padding(5) };
            Panel pnlBottomButton = new Panel { Dock = DockStyle.Bottom, Height = 35, BackColor = Color.Transparent };

            btnDeleteAll = new Button { Text = "🔥 XÓA HẾT", Dock = DockStyle.Right, Width = 90, BackColor = Color.Maroon, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.25F, FontStyle.Bold), Cursor = Cursors.Hand };
            btnDeleteAll.Click += BtnDeleteAll_Click;

            btnAction = new Button { Text = "...", Dock = DockStyle.Fill, BackColor = Color.LightGray, Enabled = false, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnAction.Click += BtnAction_Click;

            pnlBottomButton.Controls.Add(btnDeleteAll); pnlBottomButton.Controls.Add(btnAction);

            lblStatus = new Label { Text = "", Dock = DockStyle.Bottom, Height = 20, TextAlign = ContentAlignment.MiddleCenter, Font = stdFont, ForeColor = Color.Red };
            lblDetailInfo = new Label { Text = "...", Dock = DockStyle.Top, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8F, FontStyle.Italic), ForeColor = Color.DimGray };
            txtDetailDesc = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.WhiteSmoke, Font = stdFont, ScrollBars = ScrollBars.Vertical };

            grpDetail.Controls.Add(txtDetailDesc); grpDetail.Controls.Add(lblDetailInfo); grpDetail.Controls.Add(lblStatus); grpDetail.Controls.Add(pnlBottomButton);

            TableLayoutPanel tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Color.WhiteSmoke };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label CreateHeader(string t, Color c) => new Label { Text = t, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.25F, FontStyle.Bold), BackColor = c, ForeColor = Color.White };
            tbl.Controls.Add(CreateHeader("1. CHỜ", Color.OrangeRed), 0, 0);
            tbl.Controls.Add(CreateHeader("2. LÀM", Color.DodgerBlue), 1, 0);
            tbl.Controls.Add(CreateHeader("3. XONG", Color.ForestGreen), 2, 0);

            dgvTodo = CreateDynamicGrid("todo"); dgvProgress = CreateDynamicGrid("progress"); dgvDone = CreateDynamicGrid("done");
            tbl.Controls.Add(dgvTodo, 0, 1); tbl.Controls.Add(dgvProgress, 1, 1); tbl.Controls.Add(dgvDone, 2, 1);
            this.Controls.Add(tbl); this.Controls.Add(grpDetail); this.Controls.Add(pnlTop);
        }

        private DataGridView CreateDynamicGrid(string tag)
        {
            DataGridView dgv = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, Tag = tag, BorderStyle = BorderStyle.None, ColumnHeadersVisible = false, RowTemplate = { Height = 28 }, ContextMenuStrip = ctxMenuAdmin };
            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dgv, new object[] { true });
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgv.Columns.Add("colSID", "SID"); dgv.Columns["colSID"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; dgv.Columns["colSID"].DefaultCellStyle.Font = _fontNormal;
            dgv.CellClick += Dgv_CellClick; dgv.KeyDown += Dgv_KeyDown;
            dgv.CellMouseDown += (s, e) => { if (e.Button == MouseButtons.Right && e.RowIndex >= 0) { dgv.CurrentCell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex]; dgv.Rows[e.RowIndex].Selected = true; if (dgv.Rows[e.RowIndex].Tag is NhiemVu t) { _currentTask = t; UpdateDetailPanel(t); } } };
            return dgv;
        }

        private async void TxtInputSID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(txtInputSID.Text))
            {
                e.SuppressKeyPress = true; string sid = txtInputSID.Text.Trim();
                using (var frm = new FrmGiaoViec(sid, _myArea))
                {
                    if (frm.ShowDialog() == DialogResult.OK)
                    {
                        // --- LOGIC MỚI: Thêm IsKhan vào object ---
                        var t = new NhiemVu
                        {
                            ID = DateTime.Now.Ticks.ToString(),
                            SID = sid,
                            NguoiGiao = _myArea,
                            NguoiNhan = frm.SelectedArea,
                            MoTa = frm.Description,
                            TrangThai = "New",
                            ThoiGian = DateTime.Now,
                            IsKhan = frm.IsEmergency // <-- Lấy từ Form Giao Việc
                        };
                        await _firebase!.Child("Tasks").Child(t.ID).PutAsync(t);
                        txtInputSID.Clear();
                    }
                }
            }
        }

        private void ListenToFirebaseGlobal()
        {
            _firebaseSubscription = _firebase!.Child("Tasks").AsObservable<NhiemVu>().Subscribe(d =>
            {
                try
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    this.Invoke((Action)(() => {
                        string key = d.Key;
                        if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.Delete)
                        {
                            if (_taskCache.ContainsKey(key)) { RemoveTaskFromUI(key); _taskCache.Remove(key); }
                            if (_currentTask != null && _currentTask.ID == key) ClearDetailPanel();
                            return;
                        }
                        if (d.Object != null)
                        {
                            var task = d.Object; task.ID = key; _taskCache[key] = task; RemoveTaskFromUI(key);
                            if (task.TrangThai == "New") AddToGrid(dgvTodo, task); else if (task.TrangThai == "Processing") AddToGrid(dgvProgress, task); else if (task.TrangThai == "Done") AddToGrid(dgvDone, task);
                            if (_currentTask != null && _currentTask.ID == task.ID) { _currentTask = task; UpdateDetailPanel(task); }

                            // --- Logic hiển thị Form Cảnh Báo ---
                            if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate && task.TrangThai == "New" && _myArea == task.NguoiNhan && !_isAlertShowing)
                            {
                                _isAlertShowing = true;
                                SystemSounds.Hand.Play();
                                // Truyền thêm biến IsKhan để cảnh báo biết đường đổi màu đỏ
                                try { using (var frmAlert = new FrmCanhBao(task.SID, task.MoTa, task.NguoiGiao, task.IsKhan)) { frmAlert.ShowDialog(); } } finally { _isAlertShowing = false; }
                            }
                        }
                    }));
                }
                catch { }
            });
        }

        // =========================================================================
        // HÀM QUAN TRỌNG NHẤT: XỬ LÝ GIAO DIỆN (ĐÃ SỬA THEO YÊU CẦU CỦA BẠN)
        // =========================================================================
        private void AddToGrid(DataGridView dgv, NhiemVu t)
        {
            if (dgv == null) return;

            // 1. Thêm biểu tượng tia sét "⚡" nếu là Khẩn
            string displaySID = t.IsKhan ? "⚡ " + t.SID : t.SID;

            int idx = dgv.Rows.Add(displaySID);
            var row = dgv.Rows[idx];
            row.Tag = t;

            // 2. GIỮ NGUYÊN MÀU NỀN THEO KHU VỰC (Logic cũ)
            Color baseColor = GetColorByArea(t.NguoiNhan);
            row.DefaultCellStyle.BackColor = baseColor;

            // 3. SỬA LỖI SELECT (QUAN TRỌNG): 
            // Dùng màu tối hơn của chính màu nền đó để làm màu Selection
            // -> Giúp người dùng biết mình đang chọn dòng nào mà không bị lệch tông
            row.DefaultCellStyle.SelectionBackColor = (baseColor == Color.White)
                ? Color.WhiteSmoke
                : ControlPaint.Dark(baseColor, 0.1f);

            // 4. XỬ LÝ RIÊNG CHO MẪU KHẨN (Chỉ đổi màu chữ)
            if (t.IsKhan && t.TrangThai != "Done")
            {
                row.DefaultCellStyle.ForeColor = Color.Red;         // Chữ đỏ rực
                row.DefaultCellStyle.SelectionForeColor = Color.Red;// Chọn vào vẫn đỏ
                row.DefaultCellStyle.Font = _fontUrgent;            // Chữ To & Đậm hơn
            }
            else
            {
                row.DefaultCellStyle.ForeColor = Color.Black;
                row.DefaultCellStyle.SelectionForeColor = Color.Black;
                row.DefaultCellStyle.Font = _fontNormal;
            }

            // 5. XỬ LÝ TRẠNG THÁI DONE / HỦY (Đè lên tất cả)
            if (t.TrangThai == "Done")
            {
                bool isHuy = t.MoTa.StartsWith("[HỦY]");
                Color c = isHuy ? Color.Red : Color.ForestGreen;
                row.DefaultCellStyle.ForeColor = c;
                row.DefaultCellStyle.SelectionForeColor = c;
                if (isHuy) row.DefaultCellStyle.Font = _fontStrikeout;
            }
        }
        // =========================================================================

        private void UpdateDetailPanel(NhiemVu t)
        {
            if (txtDetailDesc == null) return;
            bool isHuy = t.MoTa.StartsWith("[HỦY]");
            txtDetailDesc.Text = t.MoTa;
            txtDetailDesc.ForeColor = isHuy ? Color.Red : Color.Black;

            // Hiển thị [KHẨN] trong chi tiết
            string khanText = t.IsKhan ? "⚡ [KHẨN] " : "";
            lblDetailInfo.Text = $"{khanText}{t.NguoiGiao} -> {t.NguoiNhan} ({t.ThoiGian:HH:mm})";
            if (t.IsKhan) lblDetailInfo.ForeColor = Color.Red; else lblDetailInfo.ForeColor = Color.DimGray;

            btnAction.Enabled = true; bool isMyDuty = (_myArea == t.NguoiNhan); if (t.TrangThai == "New") { lblStatus.Text = "Chờ nhận"; lblStatus.ForeColor = Color.OrangeRed; btnAction.Text = "NHẬN VIỆC"; btnAction.BackColor = isMyDuty ? Color.Orange : Color.LightGray; btnAction.Enabled = isMyDuty; } else if (t.TrangThai == "Processing") { lblStatus.Text = "Đang làm"; lblStatus.ForeColor = Color.DodgerBlue; btnAction.Text = "HOÀN THÀNH"; btnAction.BackColor = isMyDuty ? Color.DodgerBlue : Color.LightGray; btnAction.Enabled = isMyDuty; } else { lblStatus.Text = isHuy ? "ĐÃ HỦY" : "Xong"; lblStatus.ForeColor = isHuy ? Color.Red : Color.Green; btnAction.Text = "KẾT THÚC"; btnAction.BackColor = Color.Gray; btnAction.Enabled = false; }
            if (!isMyDuty && t.TrangThai != "Done") lblStatus.Text += (_myArea == "Tất cả (Admin)") ? " (Admin)" : " (Không phải việc của bạn)";
        }

        private async void BtnAction_Click(object sender, EventArgs e) { if (_currentTask == null) return; try { if (btnAction.Text == "NHẬN VIỆC") await _firebase!.Child("Tasks").Child(_currentTask.ID).PatchAsync(new { TrangThai = "Processing" }); else if (btnAction.Text.Contains("HOÀN THÀNH")) { using (var frm = new FrmXuLy(_currentTask.MoTa)) { if (frm.ShowDialog() == DialogResult.OK) { string r = frm.KetQua; if (frm.IsThatBai) r = "[HỦY] " + r; await _firebase!.Child("Tasks").Child(_currentTask.ID).PatchAsync(new { TrangThai = "Done", MoTa = r }); } } } } catch { } }
        private void Dgv_CellClick(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && ((DataGridView)s).Rows[e.RowIndex].Tag is NhiemVu t) { _currentTask = t; UpdateDetailPanel(t); } }
        private void ClearDetailPanel() { if (txtDetailDesc != null) txtDetailDesc.Text = ""; if (lblDetailInfo != null) lblDetailInfo.Text = "..."; if (btnAction != null) { btnAction.Text = "..."; btnAction.Enabled = false; } if (lblStatus != null) lblStatus.Text = ""; _currentTask = null; }
        private async void ItemDelete_Click(object s, EventArgs e) { if (_myArea == "Khách (Chỉ xem)") { MessageBox.Show("Khách không được xóa.", "Cấm"); return; } if (_currentTask == null) return; if (MessageBox.Show("XÓA?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes) await _firebase!.Child("Tasks").Child(_currentTask.ID).DeleteAsync(); }
        private async void Dgv_KeyDown(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Delete && _myArea != "Khách (Chỉ xem)" && ((DataGridView)s).CurrentRow?.Tag is NhiemVu t) { e.Handled = true; await _firebase!.Child("Tasks").Child(t.ID).DeleteAsync(); } }
        private void RemoveTaskFromUI(string id) { RemoveRow(dgvTodo, id); RemoveRow(dgvProgress, id); RemoveRow(dgvDone, id); }
        private void RemoveRow(DataGridView dgv, string id) { if (dgv == null) return; for (int i = dgv.Rows.Count - 1; i >= 0; i--) if (dgv.Rows[i].Tag is NhiemVu t && t.ID == id) { dgv.Rows.RemoveAt(i); return; } }
        private Color GetColorByArea(string a) { if (string.IsNullOrEmpty(a)) return Color.White; string al = a.ToLower(); if (al.Contains("huyết học tầng 1")) return Color.LightSkyBlue; if (al.Contains("huyết học tầng 3")) return Color.LightGreen; if (al.Contains("sinh hóa tầng 1")) return Color.Khaki; if (al.Contains("miễn dịch tầng 1")) return Color.PeachPuff; if (al.Contains("sh-md tầng 3")) return Color.LightPink; return Color.White; }
        private async void BtnDeleteAll_Click(object sender, EventArgs e) { if (MessageBox.Show("⛔ CẢNH BÁO: Bạn muốn XÓA SẠCH toàn bộ nhiệm vụ?\nHành động này không thể hoàn tác!", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; string pass = ShowPasswordInput(); if (pass != PASSWORD_DELETE_ALL) { MessageBox.Show("Mật khẩu sai!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); return; } try { await _firebase!.Child("Tasks").DeleteAsync(); _taskCache.Clear(); dgvTodo.Rows.Clear(); dgvProgress.Rows.Clear(); dgvDone.Rows.Clear(); ClearDetailPanel(); MessageBox.Show("Đã xóa sạch nhiệm vụ!", "Thành công"); } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); } }
        private string ShowPasswordInput() { Form prompt = new Form() { Width = 300, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Bảo mật", StartPosition = FormStartPosition.CenterScreen, MinimizeBox = false, MaximizeBox = false }; Label textLabel = new Label() { Left = 20, Top = 20, Text = "Nhập mật khẩu để xóa hết:" }; TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 240, PasswordChar = '*' }; Button confirmation = new Button() { Text = "Xác nhận", Left = 160, Width = 100, Top = 80, DialogResult = DialogResult.OK, BackColor = Color.Red, ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; prompt.Controls.Add(textBox); prompt.Controls.Add(confirmation); prompt.Controls.Add(textLabel); prompt.AcceptButton = confirmation; return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : ""; }
    }
}