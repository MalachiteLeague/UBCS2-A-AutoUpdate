using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Helpers;
using UBCS2_A.Models;
using Newtonsoft.Json.Linq;

namespace UBCS2_A.Services
{
    public class TaskContext : IDisposable
    {
        private readonly FirebaseService _firebaseService;
        private GridManager<TaskModel> _gridManager;
        private DataGridView _dgv;

        private List<TaskModel> _taskList = new List<TaskModel>();
        private List<TaskModel> _displayList = new List<TaskModel>();

        private readonly object _lock = new object();
        private int _currentMaxId = 0;
        private string _nodeName = "T_Tasks";
        private const int MAX_KEEP_TASKS = 200;
        private string _currentRole = "Khách";
        private bool _isInitialLoadComplete = false;
        private readonly HashSet<string> _restrictedAreas = new HashSet<string>()
        {
            "Huyết học T1", "Sinh hóa T1", "Miễn dịch T1",
            "Huyết học T3", "SH-MD T3", "Hành Chánh T1", "Hành Chánh T3"
        };

        public TaskContext(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        public void RegisterTaskTable(DataGridView dgv, string nodeName, int maxRows = 200)
        {
            _nodeName = nodeName;
            _dgv = dgv;
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Clear();
            dgv.BackgroundColor = Color.White;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToResizeRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.ColumnHeadersVisible = false;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;

            var colTask = new DataGridViewTextBoxColumn();
            colTask.HeaderText = "Nhiệm Vụ";
            colTask.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colTask.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colTask.DefaultCellStyle.Padding = new Padding(10, 8, 10, 8);
            colTask.DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            dgv.Columns.Add(colTask);

            _gridManager = new GridManager<TaskModel>(dgv, maxRows);

            _gridManager.OnGetValue = (rowIndex, model, colIndex) =>
            {
                // Nếu là dòng rỗng -> Trả về chuỗi rỗng
                if (model == null || (model.Id == 0 && string.IsNullOrEmpty(model.SID)))
                    return "";

                string statusIcon = "⭕";
                if (model.TrangThai == 2) statusIcon = "⚡";
                else if (model.TrangThai == 1) statusIcon = "✅";

                string line1 = $"{statusIcon}  [{model.KhuVucNhan}]  SID: {model.SID}";
                string line2 = $"      ➥ {model.NoiDung}";
                return line1 + "\r\n" + line2;
            };

            dgv.KeyDown += Dgv_KeyDown;
            dgv.CellFormatting += Dgv_CellFormatting;
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
        }

        // =========================================================================
        // [PHẦN XỬ LÝ SỰ KIỆN]
        // =========================================================================

        public void StartSync()
        {
            _firebaseService.OnDataChanged += Firebase_OnDataChanged;
            _firebaseService.OnItemDeleted += Firebase_OnItemDeleted;
        }

        private void Firebase_OnDataChanged(object sender, FirebaseDataEventArgs e)
        {
            // [CÁCH 1] BẮT LỆNH HỆ THỐNG (SYSTEM COMMAND)
            // Nếu nhận được lệnh "BACKUP_WIPE" -> Tự động xóa trắng Task luôn
            if (e.Path.Contains("System") || e.Key == "Command")
            {
                string dataStr = e.Data?.ToString() ?? "";
                if (dataStr.Contains("BACKUP_WIPE"))
                {
                    Console.WriteLine("[TASK-SYNC] 🧹 Nhận lệnh hệ thống: XÓA SẠCH!");
                    ClearAllLocalData();
                    return;
                }
            }

            // [CÁCH 2] BẮT SỰ KIỆN XÓA BẢNG TRUYỀN THỐNG
            bool isDeleteAll = (e.Path.Contains(_nodeName) && (e.Data == null || e.Data.Type == JTokenType.Null));
            if (isDeleteAll)
            {
                Console.WriteLine($"[TASK-SYNC] 🧹 Server đã xóa bảng {_nodeName}!");
                ClearAllLocalData();
                return;
            }

            // Xử lý dữ liệu con (Task_X)
            if (!e.Path.Contains(_nodeName) && e.RootNode != _nodeName) return;

            string checkString = (!string.IsNullOrEmpty(e.Key) ? e.Key : e.Path);
            if (checkString.Contains("Task_"))
            {
                string idPart = checkString.Substring(checkString.LastIndexOf("Task_") + 5);
                if (int.TryParse(idPart, out int id))
                {
                    var newTask = e.ToObject<TaskModel>();
                    if (newTask == null) // Data null -> Xóa dòng
                    {
                        HandleSingleItemDelete(id);
                        return;
                    }
                    newTask.Id = id;
                    if (_dgv.InvokeRequired) _dgv.BeginInvoke(new Action(() => ProcessSync(newTask, id)));
                    else ProcessSync(newTask, id);
                }
            }
        }

        private void Firebase_OnItemDeleted(object sender, FirebaseDeleteEventArgs e)
        {
            // Bắt sự kiện DELETE node cha
            if (e.TargetId == _nodeName || (e.RootNode == _nodeName && string.IsNullOrEmpty(e.TargetId)))
            {
                Console.WriteLine($"[TASK-SYNC] 🧹 Server đã DELETE bảng {_nodeName}!");
                ClearAllLocalData();
                return;
            }

            if (e.RootNode != _nodeName) return;

            if (e.TargetId.StartsWith("Task_") && int.TryParse(e.TargetId.Substring(5), out int id))
            {
                HandleSingleItemDelete(id);
            }
        }

        // [QUAN TRỌNG] Hàm xóa sạch dữ liệu và ép Grid trắng
        private void ClearAllLocalData()
        {
            lock (_lock)
            {
                _taskList.Clear();
                _currentMaxId = 0;

                // 1. Xóa trong GridManager (quan trọng để reset các dòng ảo)
                _gridManager?.ClearAll();

                // 2. Refresh lại logic hiển thị
                RefreshGrid();
            }

            // 3. Ép UI vẽ lại ngay lập tức
            if (_dgv.IsHandleCreated) _dgv.BeginInvoke((Action)(() => _dgv.Invalidate()));
        }

        // ... (Các hàm logic cũ bên dưới GIỮ NGUYÊN) ...

        public async Task LoadInitialDataAsync()
        {
            try
            {
                var dict = await _firebaseService.GetDataAsync<Dictionary<string, TaskModel>>(_nodeName);
                lock (_lock)
                {
                    _taskList.Clear();
                    _currentMaxId = 0;
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            if (kvp.Key.StartsWith("Task_") && int.TryParse(kvp.Key.Substring(5), out int id))
                            {
                                var t = kvp.Value; t.Id = id;
                                _taskList.Add(t);
                                if (id > _currentMaxId) _currentMaxId = id;
                            }
                        }
                    }
                    _taskList = _taskList.OrderBy(t => t.Id).ToList();
                    RefreshGrid();
                    _isInitialLoadComplete = true;
                }
            }
            catch (Exception ex) { Console.WriteLine($"[TASK-ERR] {ex.Message}"); }
        }

        private void HandleSingleItemDelete(int id)
        {
            lock (_lock)
            {
                var target = _taskList.FirstOrDefault(t => t.Id == id);
                if (target != null)
                {
                    _taskList.Remove(target);
                    RefreshGrid();
                }
            }
        }

        private void ProcessSync(TaskModel newTask, int id)
        {
            lock (_lock)
            {
                if (id > _currentMaxId) _currentMaxId = id;
                var existing = _taskList.FirstOrDefault(t => t.Id == id);
                bool isNew = (existing == null);

                if (isNew)
                {
                    _taskList.Add(newTask);
                    _taskList = _taskList.OrderBy(t => t.Id).ToList();

                    if (_isInitialLoadComplete && newTask.TrangThai == 0 &&
                        string.Equals(newTask.KhuVucNhan, _currentRole, StringComparison.OrdinalIgnoreCase))
                    {
                        ShowFlashPopup(newTask);
                    }
                }
                else
                {
                    int idx = _taskList.IndexOf(existing);
                    _taskList[idx] = newTask;
                }

                while (_taskList.Count > MAX_KEEP_TASKS) _taskList.RemoveAt(0);
                RefreshGrid();
            }
        }

        private void ShowFlashPopup(TaskModel task)
        {
            var popup = new FlashTaskForm(task);
            popup.OnConfirm += (t) => {
                t.TrangThai = 2; // Received
                _firebaseService.UpdateDataAsync($"{_nodeName}/Task_{t.Id}", t);
                RefreshGrid();
            };
            popup.Show();
        }

        // [Các hàm giao diện cũ giữ nguyên]
        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _dgv.SelectedRows.Count > 0)
            {
                var tasksToDelete = new List<TaskModel>();
                foreach (DataGridViewRow row in _dgv.SelectedRows)
                {
                    if (row.Index >= 0 && row.Index < _displayList.Count)
                        tasksToDelete.Add(_displayList[row.Index]);
                }
                DeleteTasks(tasksToDelete);
                e.Handled = true;
            }
        }

        private void DeleteTasks(List<TaskModel> tasks)
        {
            lock (_lock)
            {
                foreach (var task in tasks)
                {
                    _firebaseService.DeleteDataAsync($"{_nodeName}/Task_{task.Id}");
                    var itemInMaster = _taskList.FirstOrDefault(t => t.Id == task.Id);
                    if (itemInMaster != null) _taskList.Remove(itemInMaster);
                }
                RefreshGrid();
            }
        }

        public void AddNewTask(TaskModel newTask)
        {
            lock (_lock)
            {
                _currentMaxId++;
                newTask.Id = _currentMaxId;
                newTask.TrangThai = 0;
                _taskList.Add(newTask);
                _firebaseService.UpdateDataAsync($"{_nodeName}/Task_{newTask.Id}", newTask);
                if (_taskList.Count > MAX_KEEP_TASKS)
                {
                    var oldTask = _taskList[0];
                    _taskList.RemoveAt(0);
                    _firebaseService.DeleteDataAsync($"{_nodeName}/Task_{oldTask.Id}");
                }
                RefreshGrid();
            }
        }

        public void SetCurrentUserRole(string role)
        {
            _currentRole = role;
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            lock (_lock)
            {
                bool isRestricted = _restrictedAreas.Contains(_currentRole);
                _displayList.Clear();
                foreach (var task in _taskList)
                {
                    if (isRestricted)
                    {
                        if (string.Equals(task.KhuVucNhan, _currentRole, StringComparison.OrdinalIgnoreCase))
                            _displayList.Add(task);
                    }
                    else _displayList.Add(task);
                }
                var dict = new Dictionary<int, TaskModel>();
                for (int i = 0; i < _displayList.Count; i++) dict[i] = _displayList[i];
                _gridManager.LoadFullData(dict);
            }
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _displayList.Count)
            {
                var task = _displayList[e.RowIndex];
                if (task.TrangThai == 1) // Done
                {
                    e.CellStyle.BackColor = Color.FromArgb(220, 255, 220);
                    e.CellStyle.ForeColor = Color.DarkGreen;
                }
                else if (task.TrangThai == 2) // Received
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 250, 205);
                    e.CellStyle.ForeColor = Color.FromArgb(139, 69, 19);
                }
                else // New
                {
                    e.CellStyle.BackColor = Color.White;
                    e.CellStyle.ForeColor = Color.Black;
                }
            }
        }

        private void Dgv_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _displayList.Count)
            {
                var task = _displayList[e.RowIndex];
                int nextStatus = (task.TrangThai == 0) ? 2 : (task.TrangThai == 2 ? 1 : 0);
                string msg = (nextStatus == 2) ? "Đã Nhận" : (nextStatus == 1 ? "Hoàn Thành" : "Làm Lại");

                if (MessageBox.Show($"Chuyển trạng thái sang: {msg}?", "Cập nhật", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    task.TrangThai = nextStatus;
                    _firebaseService.UpdateDataAsync($"{_nodeName}/Task_{task.Id}", task);
                    RefreshGrid();
                }
            }
        }

        public void Dispose()
        {
            _firebaseService.OnDataChanged -= Firebase_OnDataChanged;
            _firebaseService.OnItemDeleted -= Firebase_OnItemDeleted;
        }
    }
}