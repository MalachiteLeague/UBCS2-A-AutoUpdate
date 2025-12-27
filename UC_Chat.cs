using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Firebase.Database;
using Firebase.Database.Query;
using System.Reactive.Linq;

namespace UBCS2_A
{
    public partial class UC_Chat : UserControl
    {
        private FirebaseClient? _firebase;
        private List<Chat> _chatData = new List<Chat>();

        // 1. Tinh chỉnh Font: Size 9 nhìn sẽ sắc nét và gọn hơn
        private readonly Font fontBold = new Font("Segoe UI", 9F, FontStyle.Bold);
        private readonly Font fontNormal = new Font("Segoe UI", 9F, FontStyle.Regular);

        public UC_Chat()
        {
            InitializeComponent();
            SetupGrid();
        }

        private void SetupGrid()
        {
            // --- CẤU HÌNH GIAO DIỆN CHAT ---
            dgvChat.VirtualMode = true;
            dgvChat.ReadOnly = true;
            dgvChat.AllowUserToAddRows = false;
            dgvChat.AllowUserToDeleteRows = false;
            dgvChat.AllowUserToResizeRows = false;
            dgvChat.RowHeadersVisible = false;
            dgvChat.ColumnHeadersVisible = false;

            // Làm sạch viền
            dgvChat.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dgvChat.BackgroundColor = Color.White;
            dgvChat.BorderStyle = BorderStyle.None; // Xóa viền ngoài cùng

            // Tắt chức năng chọn dòng (để không hiện màu xanh khó chịu)
            // Tuy nhiên ta vẫn dùng CellFormatting để ép màu về trắng nếu lỡ có chọn
            dgvChat.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Tự động giãn dòng & Căn lề
            dgvChat.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgvChat.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Padding: Trên/Dưới 2px, Trái/Phải 5px cho thoáng
            dgvChat.DefaultCellStyle.Padding = new Padding(5, 2, 5, 2);

            dgvChat.Columns.Clear();
            dgvChat.Columns.Add("colContent", "Nội dung");
            dgvChat.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // DoubleBuffer chống nháy
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dgvChat, new object[] { true });

            dgvChat.CellValueNeeded += DgvChat_CellValueNeeded;
            dgvChat.CellFormatting += DgvChat_CellFormatting;

            // Khi click vào thì bỏ chọn ngay lập tức để tránh hiện viền focus
            dgvChat.SelectionChanged += (s, e) => dgvChat.ClearSelection();
        }

        public void InitChat(FirebaseClient fb)
        {
            _firebase = fb;
            ListentoChatChanges();
        }

        private void ListentoChatChanges()
        {
            if (_firebase == null) return;

            _firebase.Child("Chat")
                     .OrderByKey()
                     .LimitToLast(50)
                     .AsObservable<Chat>()
                     .Subscribe(d =>
                     {
                         try
                         {
                             if (this.IsDisposed || !this.IsHandleCreated) return;

                             this.Invoke((MethodInvoker)(() =>
                             {
                                 string key = d.Key;

                                 if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.InsertOrUpdate)
                                 {
                                     if (d.Object != null && !_chatData.Any(x => x.Time == d.Object.Time && x.Message == d.Object.Message))
                                     {
                                         _chatData.Add(d.Object);
                                     }
                                 }
                                 else if (d.EventType == Firebase.Database.Streaming.FirebaseEventType.Delete)
                                 {
                                     var itemToRemove = _chatData.FirstOrDefault(x => x.Time == d.Object.Time && x.Message == d.Object.Message);
                                     if (itemToRemove != null) _chatData.Remove(itemToRemove);
                                 }

                                 dgvChat.RowCount = _chatData.Count;
                                 if (dgvChat.RowCount > 0)
                                     dgvChat.FirstDisplayedScrollingRowIndex = dgvChat.RowCount - 1;

                                 dgvChat.Invalidate();
                             }));
                         }
                         catch { }
                     });
        }

        private void DgvChat_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _chatData.Count)
            {
                var item = _chatData[e.RowIndex];
                if (item != null)
                {
                    e.Value = $"{item.Time:HH:mm}: {item.Sender}: {item.Message}";
                }
                else
                {
                    e.Value = "";
                }
            }
        }

        // --- SỰ KIỆN QUAN TRỌNG: TÔ MÀU & XÓA HIGHLIGHT ---
        private void DgvChat_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _chatData.Count)
            {
                var item = _chatData[e.RowIndex];
                if (item == null) return;

                // 1. Lấy màu chữ
                Color textColor;
                try { textColor = Color.FromName(item.UserColor); } catch { textColor = Color.Black; }

                // 2. Thiết lập màu chữ bình thường
                e.CellStyle.ForeColor = textColor;

                // 3. KỸ THUẬT XÓA MÀU CHỌN (HIGHLIGHT):
                // Khi dòng được chọn (Selected), ta ép màu nó giống hệt lúc chưa chọn
                e.CellStyle.SelectionForeColor = textColor;   // Chữ vẫn giữ màu cũ
                e.CellStyle.SelectionBackColor = Color.White; // Nền vẫn trắng (thay vì xanh)

                // 4. Set Font
                e.CellStyle.Font = item.IsBold ? fontBold : fontNormal;
            }
        }

        private async void txtChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (!string.IsNullOrEmpty(txtChatInput.Text) && _firebase != null)
                {
                    string sName = string.IsNullOrWhiteSpace(txtUserName.Text) ? "Guest" : txtUserName.Text;
                    string selectedColor = cbColor.SelectedItem?.ToString() ?? "Black";
                    bool isBold = chkBold.Checked;

                    var newChat = new Chat
                    {
                        Sender = sName,
                        Message = txtChatInput.Text,
                        Time = DateTime.Now,
                        UserColor = selectedColor,
                        IsBold = isBold
                    };

                    await _firebase.Child("Chat").PostAsync(newChat);
                    txtChatInput.Clear();

                    if (_chatData.Count > 1500)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var allMessages = await _firebase.Child("Chat").OnceAsync<Chat>();
                                if (allMessages.Count > 1000)
                                {
                                    var toDelete = allMessages.OrderBy(x => x.Object.Time).Take(allMessages.Count - 1000).ToList();
                                    foreach (var node in toDelete)
                                    {
                                        await _firebase.Child("Chat").Child(node.Key).DeleteAsync();
                                    }
                                }
                            }
                            catch { }
                        });
                    }
                }
            }
        }
    }
}