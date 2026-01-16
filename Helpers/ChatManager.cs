using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using UBCS2_A.Models;

namespace UBCS2_A.Helpers
{
    /// <summary>
    /// [MANAGER] Quản lý giao diện Chat.
    /// - [UPDATE] Font size = 9 (Nhỏ gọn).
    /// - [KEEP] Viết tắt tên (HH.T1, SH.T1...).
    /// - [KEEP] Logic tách biệt T1/T3.
    /// </summary>
    public class ChatManager
    {
        // --- UI CONTROLS ---
        private readonly DataGridView _dgvAll;
        private readonly DataGridView _dgvPrivate;

        // --- LOGIC HELPERS ---
        private GridManager<ChatModel> _gridMgrAll;
        private GridManager<ChatModel> _gridMgrPrivate;

        // --- DATA ---
        private List<ChatModel> _allChats = new List<ChatModel>();
        private List<ChatModel> _filteredChats = new List<ChatModel>();

        // --- STATE ---
        private string _currentUserRole = "Khách";

        public ChatManager(DataGridView dgvAll, DataGridView dgvPrivate)
        {
            _dgvAll = dgvAll;
            _dgvPrivate = dgvPrivate;

            Console.WriteLine("[CHAT-MGR] 🟢 Khởi tạo Chat Manager (Small Font).");
            SetupGrid(_dgvAll);
            SetupGrid(_dgvPrivate);
        }

        #region 1. CẤU HÌNH GIAO DIỆN (SIZE 9)

        private void SetupGrid(DataGridView dgv)
        {
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Clear();
            dgv.BackgroundColor = Color.White;
            dgv.RowHeadersVisible = false;

            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;

            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            dgv.ColumnHeadersVisible = false;

            // Cột Nội Dung
            var colMessage = new DataGridViewTextBoxColumn()
            {
                Name = "colMessage",
                HeaderText = "Nội Dung",
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            colMessage.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Giữ padding nhỏ để tiết kiệm diện tích
            colMessage.DefaultCellStyle.Padding = new Padding(2, 1, 2, 1);

            // [UPDATE] Giảm Font size xuống 9
            colMessage.DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Regular);

            dgv.Columns.Add(colMessage);

            dgv.CellFormatting += Dgv_CellFormatting;
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            var sourceList = (dgv == _dgvAll) ? _allChats : _filteredChats;

            if (e.RowIndex >= 0 && e.RowIndex < sourceList.Count)
            {
                var chat = sourceList[e.RowIndex];

                // Tô màu nền
                Color bgColor = Color.WhiteSmoke;

                if (string.Equals(chat.NoiNhan, "Toàn viện", StringComparison.OrdinalIgnoreCase))
                    bgColor = Color.White;
                else if (chat.NoiNhan.Contains("Huyết học")) bgColor = Color.MistyRose;
                else if (chat.NoiNhan.Contains("Sinh hóa")) bgColor = Color.Honeydew;
                else if (chat.NoiNhan.Contains("Miễn dịch")) bgColor = Color.AliceBlue;
                else if (chat.NoiNhan.Contains("Hành Chánh")) bgColor = Color.LemonChiffon;

                e.CellStyle.BackColor = bgColor;
                e.CellStyle.SelectionBackColor = bgColor;
                e.CellStyle.SelectionForeColor = Color.Black;
            }
        }

        #endregion

        #region 2. XỬ LÝ DỮ LIỆU & FORMATTING

        public void LoadData(List<ChatModel> allChats)
        {
            _allChats = allChats;

            // --- Grid Tổng ---
            if (_gridMgrAll == null)
            {
                _gridMgrAll = new GridManager<ChatModel>(_dgvAll, 1000);
                _gridMgrAll.OnGetValue = (r, m, c) => GetFormattedString(m);
            }

            var dictAll = new Dictionary<int, ChatModel>();
            for (int i = 0; i < _allChats.Count; i++) dictAll[i] = _allChats[i];

            _gridMgrAll.LoadFullData(dictAll);
            _dgvAll.RowCount = _allChats.Count;
            if (_allChats.Count > 0) _dgvAll.FirstDisplayedScrollingRowIndex = _allChats.Count - 1;

            // --- Grid Riêng ---
            ApplyFilter();
        }

        public void SetCurrentUserRole(string role)
        {
            _currentUserRole = role;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _filteredChats.Clear();

            foreach (var chat in _allChats)
            {
                // Loại bỏ "Toàn viện"
                bool isGlobal = string.Equals(chat.NoiNhan, "Toàn viện", StringComparison.OrdinalIgnoreCase);
                if (isGlobal) continue;

                // Logic riêng biệt T1/T3
                bool isForMe = string.Equals(chat.NoiNhan, _currentUserRole, StringComparison.OrdinalIgnoreCase);
                bool isFromMe = string.Equals(chat.NguoiGui, _currentUserRole, StringComparison.OrdinalIgnoreCase);

                if (isForMe || isFromMe)
                {
                    _filteredChats.Add(chat);
                }
            }

            if (_gridMgrPrivate == null)
            {
                _gridMgrPrivate = new GridManager<ChatModel>(_dgvPrivate, 1000);
                _gridMgrPrivate.OnGetValue = (r, m, c) => GetFormattedString(m);
            }

            var dictFilter = new Dictionary<int, ChatModel>();
            for (int i = 0; i < _filteredChats.Count; i++) dictFilter[i] = _filteredChats[i];

            _gridMgrPrivate.LoadFullData(dictFilter);
            _dgvPrivate.RowCount = _filteredChats.Count;

            if (_filteredChats.Count > 0) _dgvPrivate.FirstDisplayedScrollingRowIndex = _filteredChats.Count - 1;
        }

        private string GetFormattedString(ChatModel m)
        {
            if (m == null || string.IsNullOrEmpty(m.NguoiGui)) return "";

            string shortSender = AbbreviateName(m.NguoiGui);
            string shortTarget = AbbreviateName(m.NoiNhan);

            return $"[{m.ThoiGian}] {shortSender}➔{shortTarget}: {m.NoiDung}";
        }

        private string AbbreviateName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";

            if (fullName.Equals("Toàn viện", StringComparison.OrdinalIgnoreCase)) return "ALL";
            if (fullName.Equals("Khách", StringComparison.OrdinalIgnoreCase)) return "Guest";

            string shortName = fullName
                .Replace("Huyết học", "HH")
                .Replace("Sinh hóa", "SH")
                .Replace("Miễn dịch", "MD")
                .Replace("Hành Chánh", "HC")
                .Replace("SH-MD", "SHMD")
                .Replace(" ", ".");

            return shortName;
        }

        #endregion
    }
}