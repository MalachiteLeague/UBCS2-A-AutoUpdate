using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Helpers;
using UBCS2_A.Models;

namespace UBCS2_A.Services
{
    /// <summary>
    /// [CONTEXT] Quản lý dữ liệu Chat.
    /// - [KEEP] Giữ nguyên logic ID tự tăng và SendMessageReal để không báo lỗi file khác.
    /// - [KEEP] Logic ủy quyền lọc cho ChatManager.
    /// </summary>
    public class ChatContext : IDisposable
    {
        private readonly FirebaseService _firebaseService;
        private ChatManager _chatManager;

        // Dữ liệu RAM
        private List<ChatModel> _chatList = new List<ChatModel>();
        private readonly object _lock = new object();
        private int _currentMaxId = 0;

        private string _nodeName = "T_Chats";
        private const int MAX_MSG = 1000;

        private Control _invokeControl;

        public ChatContext(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
            Console.WriteLine("[CHAT-CTX] 🔵 Khởi tạo ChatContext.");
        }

        #region 1. CẤU HÌNH UI & EVENTS

        public void RegisterControls(
            DataGridView dgvAll, DataGridView dgvPrivate,
            ComboBox cboTarget, TextBox txtContent, Button btnSend)
        {
            _invokeControl = dgvAll;

            // 1. Khởi tạo Manager quản lý 2 bảng
            _chatManager = new ChatManager(dgvAll, dgvPrivate);

            // 2. Cấu hình ComboBox chọn nơi gửi
            cboTarget.Items.Clear();
            cboTarget.Items.AddRange(new string[] {
                "Toàn viện",
                "Huyết học T1",
                "Sinh hóa T1",
                "Miễn dịch T1",
                "Huyết học T3",
                "SH-MD T3",
                "Hành Chánh T1",
                "Hành Chánh T3"
            });
            cboTarget.SelectedIndex = 0; // Mặc định chọn Toàn viện

            Console.WriteLine("[CHAT-CTX] 🛠️ Đã đăng ký UI Controls & Danh sách Nơi gửi.");
        }

        // [QUAN TRỌNG] Hàm này được gọi từ Form1.Auth.cs
        // Giữ nguyên logic ủy quyền cho ChatManager xử lý bộ lọc
        public void SetCurrentUserRole(string role)
        {
            if (_chatManager != null)
            {
                Console.WriteLine($"[CHAT] 🔄 Đang cập nhật bộ lọc sang: {role}");
                _chatManager.SetCurrentUserRole(role);
            }
        }

        #endregion

        #region 2. GỬI TIN & CUỐN CHIẾU (GIỮ NGUYÊN CODE GỐC)

        public void SendMessageReal(string senderName, string target, string content)
        {
            lock (_lock)
            {
                _currentMaxId++;
                var msg = new ChatModel()
                {
                    Id = _currentMaxId,
                    ThoiGian = DateTime.Now.ToString("HH:mm"),
                    NguoiGui = senderName,
                    NoiNhan = target,
                    NoiDung = content
                };
                Console.WriteLine($"[CHAT-SEND] 📤 {senderName} -> {target}: {content}");

                _chatList.Add(msg);

                string key = $"Chat_{msg.Id}";
                _firebaseService.UpdateDataAsync($"{_nodeName}/{key}", msg);

                if (_chatList.Count > MAX_MSG)
                {
                    var oldMsg = _chatList[0];
                    _chatList.RemoveAt(0);
                    _firebaseService.DeleteDataAsync($"{_nodeName}/Chat_{oldMsg.Id}");
                }

                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (_chatManager == null) return;

            // Tạo bản sao (Snapshot) trong lock để tránh lỗi Collection Modified
            List<ChatModel> snapshot;
            lock (_lock)
            {
                snapshot = _chatList.ToList();
            }

            if (_invokeControl != null && _invokeControl.InvokeRequired)
            {
                _invokeControl.BeginInvoke(new Action(() => _chatManager.LoadData(snapshot)));
            }
            else
            {
                _chatManager.LoadData(snapshot);
            }
        }

        #endregion

        #region 3. ĐỒNG BỘ REALTIME (GIỮ NGUYÊN CODE GỐC)

        public void StartSync()
        {
            _firebaseService.OnDataChanged += Firebase_OnDataChanged;
            _firebaseService.OnItemDeleted += Firebase_OnItemDeleted;
        }

        public async Task LoadInitialDataAsync()
        {
            try
            {
                var dict = await _firebaseService.GetDataAsync<Dictionary<string, ChatModel>>(_nodeName);
                if (dict != null)
                {
                    lock (_lock)
                    {
                        _chatList.Clear();
                        _currentMaxId = 0;

                        foreach (var kvp in dict)
                        {
                            if (kvp.Key.StartsWith("Chat_") && int.TryParse(kvp.Key.Substring(5), out int id))
                            {
                                var m = kvp.Value;
                                m.Id = id;
                                _chatList.Add(m);
                                if (id > _currentMaxId) _currentMaxId = id;
                            }
                        }

                        _chatList = _chatList.OrderBy(x => x.Id).ToList();
                        while (_chatList.Count > MAX_MSG) _chatList.RemoveAt(0);

                        UpdateUI();
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[CHAT-ERR] LoadInit: {ex.Message}"); }
        }

        private void Firebase_OnDataChanged(object sender, FirebaseDataEventArgs e)
        {
            if (e.RootNode != _nodeName) return;
            if (e.Key.StartsWith("Chat_") && int.TryParse(e.Key.Substring(5), out int id))
            {
                var msg = e.ToObject<ChatModel>();
                if (msg == null) return;
                msg.Id = id;

                lock (_lock)
                {
                    if (id > _currentMaxId) _currentMaxId = id;
                    var exist = _chatList.FirstOrDefault(x => x.Id == id);
                    if (exist == null)
                    {
                        _chatList.Add(msg);
                        _chatList = _chatList.OrderBy(x => x.Id).ToList();
                    }
                    else
                    {
                        _chatList[_chatList.IndexOf(exist)] = msg;
                    }

                    while (_chatList.Count > MAX_MSG) _chatList.RemoveAt(0);
                    UpdateUI();
                }
            }
        }

        private void Firebase_OnItemDeleted(object sender, FirebaseDeleteEventArgs e)
        {
            if (e.RootNode != _nodeName) return;
            if (e.TargetId.StartsWith("Chat_") && int.TryParse(e.TargetId.Substring(5), out int id))
            {
                lock (_lock)
                {
                    var target = _chatList.FirstOrDefault(x => x.Id == id);
                    if (target != null)
                    {
                        _chatList.Remove(target);
                        UpdateUI();
                    }
                }
            }
        }

        public void Dispose()
        {
            _firebaseService.OnDataChanged -= Firebase_OnDataChanged;
            _firebaseService.OnItemDeleted -= Firebase_OnItemDeleted;
        }

        #endregion
    }
}