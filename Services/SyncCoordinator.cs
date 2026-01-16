using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UBCS2_A.Helpers;

namespace UBCS2_A.Services
{
    /// <summary>
    /// Lớp trung gian điều phối việc đồng bộ dữ liệu.
    /// Nhiệm vụ:
    /// 1. Nghe sự kiện từ Grid -> Đẩy lên Firebase.
    /// 2. Nghe sự kiện từ Firebase -> Cập nhật lại Grid.
    /// 3. Mapping giữa dòng (Row Index) và khóa (Firebase Key).
    /// </summary>
    public class SyncCoordinator<T> : IDisposable where T : class, new()
    {
        private readonly GridManager<T> _grid;
        private readonly FirebaseService _firebase;
        private readonly string _firebaseNodeName; // Tên bảng (VD: Table1)
        private readonly Func<int, T, string> _mapRowToKey; // Hàm đổi Row -> Key
        private readonly Func<string, int> _mapKeyToRow;    // Hàm đổi Key -> Row
                                                            // Thêm 1 biến Func để kiểm tra điều kiện xóa
        private readonly Func<T, bool> _checkIfEmpty;

        // Cập nhật Constructor để nhận hàm kiểm tra này
        public SyncCoordinator(GridManager<T> grid, FirebaseService firebase, string nodeName,
                               Func<int, T, string> mapRowToKey,
                               Func<string, int> mapKeyToRow,
                               Func<T, bool> checkIfEmpty = null) // <--- THÊM THAM SỐ NÀY
        {
            _grid = grid;
            _firebase = firebase;
            _firebaseNodeName = nodeName;
            _mapRowToKey = mapRowToKey;
            _mapKeyToRow = mapKeyToRow;
            _checkIfEmpty = checkIfEmpty; // Lưu lại
        }

        /// <summary>
        /// Kích hoạt việc lắng nghe 2 chiều (Grid <-> Firebase).
        /// </summary>
        public void StartSync()
        {
            Console.WriteLine($"[SYNC] Started for Node: {_firebaseNodeName}");
            _grid.OnUserChangedData += Grid_OnUserChangedData;
            _firebase.OnDataChanged += Firebase_OnDataChanged;
            _firebase.OnItemDeleted += Firebase_OnItemDeleted;
        }

        /// <summary>
        /// Xử lý khi Người dùng sửa dữ liệu trên Grid.
        /// Đẩy dữ liệu đó lên Firebase (Hàm Async).
        /// </summary>
        // Sửa hàm xử lý khi Grid thay đổi
        private async void Grid_OnUserChangedData(int rowIndex, T data)
        {
            try
            {
                string key = _mapRowToKey(rowIndex, data);
                if (string.IsNullOrEmpty(key)) return;

                // KIỂM TRA: Nếu có hàm check rỗng VÀ dữ liệu thỏa mãn là rỗng
                if (_checkIfEmpty != null && _checkIfEmpty(data))
                {
                    Console.WriteLine($"[SYNC] Data empty -> DELETING Key: {key}");
                    // Xóa hẳn node trên Firebase
                    await _firebase.DeleteDataAsync($"{_firebaseNodeName}/{key}");
                }
                else
                {
                    Console.WriteLine($"[SYNC] Data valid -> UPDATING Key: {key}");
                    // Cập nhật như cũ
                    await _firebase.UpdateDataAsync($"{_firebaseNodeName}/{key}", data);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[SYNC-ERR] {ex.Message}"); }
        }

        /// <summary>
        /// Xử lý khi Firebase báo có dữ liệu thay đổi (Realtime).
        /// Tìm dòng tương ứng trên Grid và cập nhật.
        /// </summary>
        private void Firebase_OnDataChanged(object sender, FirebaseDataEventArgs e)
        {
            if (e.RootNode != _firebaseNodeName) return; // Không phải bảng này thì bỏ qua

            int rowIndex = _mapKeyToRow(e.Key);
            Console.WriteLine($"[SYNC] Firebase Changed (Key {e.Key}) -> Mapping to Row {rowIndex}");

            if (rowIndex >= 0) _grid.UpdateSingleRow(rowIndex, e.Data);
            else Console.WriteLine($"[SYNC-WARN] Ignored Key {e.Key} (Out of range or invalid)");
        }

        /// <summary>
        /// Xử lý khi Firebase báo có dữ liệu bị xóa.
        /// </summary>
        private void Firebase_OnItemDeleted(object sender, FirebaseDeleteEventArgs e)
        {
            if (e.RootNode != _firebaseNodeName) return;
            Console.WriteLine($"[SYNC] Firebase Deleted ID: {e.TargetId}");

            if (e.TargetId == "ALL") _grid.ClearAll(); // Xóa sạch bảng
            else
            {
                // Xóa 1 dòng (Thực tế là reset dòng đó về rỗng)
                int rowIndex = _mapKeyToRow(e.TargetId);
                if (rowIndex >= 0) _grid.UpdateSingleRow(rowIndex, JToken.FromObject(new T()));
            }
        }

        /// <summary>
        /// Tải dữ liệu ban đầu khi mới mở App.
        /// Xử lý cả 2 trường hợp Firebase trả về: Array [] hoặc Dictionary {}.
        /// </summary>
        public async Task LoadInitialData()
        {
            try
            {
                Console.WriteLine($"[SYNC] Loading Initial Data for {_firebaseNodeName}...");
                string json = await _firebase.GetDataAsync<string>(_firebaseNodeName);

                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    Console.WriteLine("[SYNC] Data is empty/null on server.");
                    return;
                }

                var dataForGrid = new Dictionary<int, T>();
                int count = 0;

                // Firebase có thể trả về Mảng [...] hoặc Object {...} tùy vào Key là số liên tục hay chuỗi
                if (json.Trim().StartsWith("["))
                {
                    // Logic xử lý dạng Mảng
                    Console.WriteLine("[SYNC] Detected Array format");
                    var listData = JsonConvert.DeserializeObject<List<T>>(json);
                    if (listData != null)
                    {
                        for (int i = 0; i < listData.Count; i++)
                        {
                            if (listData[i] != null)
                            {
                                int rowIndex = _mapKeyToRow(i.ToString());
                                if (rowIndex == -1) rowIndex = i; // Fallback
                                if (rowIndex >= 0) dataForGrid[rowIndex] = listData[i];
                                count++;
                            }
                        }
                    }
                }
                else
                {
                    // Logic xử lý dạng Object/Dictionary
                    Console.WriteLine("[SYNC] Detected Dictionary/Object format");
                    var dictData = JsonConvert.DeserializeObject<Dictionary<string, T>>(json);
                    if (dictData != null)
                    {
                        foreach (var kvp in dictData)
                        {
                            int rowIndex = _mapKeyToRow(kvp.Key);
                            if (rowIndex >= 0) dataForGrid[rowIndex] = kvp.Value;
                            count++;
                        }
                    }
                }

                Console.WriteLine($"[SYNC] Loaded {count} records into Grid.");
                if (dataForGrid.Count > 0) _grid.LoadFullData(dataForGrid);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC-ERR] LoadInitialData: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Console.WriteLine($"[SYNC] Disposing coordinator for {_firebaseNodeName}");
            _grid.OnUserChangedData -= Grid_OnUserChangedData;
            _firebase.OnDataChanged -= Firebase_OnDataChanged;
            _firebase.OnItemDeleted -= Firebase_OnItemDeleted;
        }
    }
}