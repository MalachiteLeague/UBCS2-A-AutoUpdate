using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UBCS2_A.Services
{
    /// <summary>
    /// Service chịu trách nhiệm giao tiếp trực tiếp với Firebase REST API.
    /// Bao gồm các thao tác CRUD và lắng nghe luồng sự kiện Realtime (SSE).
    /// </summary>
    public class FirebaseService : IDisposable
    {
        // Các hằng số cấu hình kết nối
        private const string AUTH_PARAM = "auth";
        private const string STREAM_ACCEPT_HEADER = "text/event-stream";
        private const string MEDIA_TYPE_JSON = "application/json";

        private readonly string _baseUrl;
        private readonly string _secret;

        // HttpClient dùng chung (static) để tối ưu socket
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite) };

        private CancellationTokenSource _cancelTokenSource;
        private readonly object _serviceLock = new object();

        // Events để báo ra bên ngoài
        public event EventHandler<FirebaseDataEventArgs> OnDataChanged;   // Khi dữ liệu thay đổi
        public event EventHandler<FirebaseDeleteEventArgs> OnItemDeleted; // Khi dữ liệu bị xóa
        public event Action<string, Color> OnStatusChanged;               // Trạng thái kết nối

        public FirebaseService(string url, string secret)
        {
            _baseUrl = url.TrimEnd('/');
            _secret = secret;
            Console.WriteLine($"[FIREBASE] Service initialized with URL: {_baseUrl}");
        }

        /// <summary>
        /// Tải toàn bộ dữ liệu từ một Node (GET).
        /// Thường dùng khi khởi động app để nạp dữ liệu cũ.
        /// </summary>
        public async Task<T> GetDataAsync<T>(string path)
        {
            try
            {
                string url = BuildUrl(path);
                Console.WriteLine($"[FIREBASE-GET] Requesting: {url}");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[FIREBASE-GET] Response ({json.Length} chars): {json}");

                if (json == "null") return default(T);
                if (typeof(T) == typeof(string)) return (T)(object)json;

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREBASE-ERR] GET Failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cập nhật hoặc ghi đè dữ liệu tại một đường dẫn cụ thể (PUT).
        /// Dùng khi người dùng sửa cell trên Grid.
        /// </summary>
        public async Task UpdateDataAsync<T>(string path, T data)
        {
            try
            {
                string url = BuildUrl(path);
                string json = JsonConvert.SerializeObject(data);
                Console.WriteLine($"[FIREBASE-PUT] Sending to {path}: {json}");

                var content = new StringContent(json, Encoding.UTF8, MEDIA_TYPE_JSON);
                var response = await _httpClient.PutAsync(url, content);
                response.EnsureSuccessStatusCode();

                Console.WriteLine($"[FIREBASE-PUT] Success!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREBASE-ERR] PUT Failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Bắt đầu lắng nghe sự thay đổi dữ liệu theo thời gian thực (Server-Sent Events).
        /// Chạy trên một luồng (Thread) riêng biệt để không chặn UI.
        /// </summary>
        public void StartListening()
        {
            Stop(); // Dừng luồng cũ nếu có
            _cancelTokenSource = new CancellationTokenSource();
            var token = _cancelTokenSource.Token;

            Console.WriteLine("[FIREBASE-STREAM] Starting background listener...");
            Task.Run(async () => await ListenToStreamAsync(token), token);
        }

        /// <summary>
        /// Logic cốt lõi của việc lắng nghe Realtime.
        /// Duy trì kết nối HTTP dài hạn và đọc từng dòng sự kiện gửi về.
        /// </summary>
        private async Task ListenToStreamAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    OnStatusChanged?.Invoke("🟡 Connecting...", Color.Orange);
                    var requestUrl = $"{_baseUrl}/.json?{AUTH_PARAM}={_secret}";
                    Console.WriteLine($"[FIREBASE-STREAM] Connecting to: {requestUrl}");

                    var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    request.Headers.Add("Accept", STREAM_ACCEPT_HEADER);

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream))
                        {
                            OnStatusChanged?.Invoke("🟢 Live Connected", Color.Green);
                            Console.WriteLine("[FIREBASE-STREAM] Connected! Waiting for data...");

                            while (!reader.EndOfStream && !token.IsCancellationRequested)
                            {
                                string line = await reader.ReadLineAsync();
                                // Debug dữ liệu thô nhận được
                                if (!string.IsNullOrWhiteSpace(line))
                                    Console.WriteLine($"[FIREBASE-STREAM-RAW] {line}");

                                ProcessStreamLine(line);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Console.WriteLine($"[FIREBASE-STREAM-ERR] {ex.Message}. Retrying in 3s...");
                        OnStatusChanged?.Invoke("🔴 Disconnected", Color.Red);
                        await Task.Delay(3000, token);
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý một dòng dữ liệu thô từ stream (dạng "data: {...}")
        /// và chuyển nó thành JSON Object để xử lý logic.
        /// </summary>
        private void ProcessStreamLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) return;
            string json = line.Substring(6).Trim();
            if (json == "null") return; // Gói tin keep-alive

            try
            {
                var packet = JObject.Parse(json);
                string path = packet["path"]?.ToString() ?? "";
                JToken data = packet["data"];

                lock (_serviceLock) HandleDataLogic(path, data);
            }
            catch (Exception ex) { Console.WriteLine($"[PARSE ERROR] {ex.Message}"); }
        }

        /// <summary>
        /// Phân tích dữ liệu JSON để quyết định đó là sự kiện Sửa (Put) hay Xóa (Delete),
        /// sau đó bắn Event tương ứng ra cho SyncCoordinator xử lý.
        /// </summary>
        private void HandleDataLogic(string path, JToken data)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // CASE 1: XÓA (Delete) - Data trả về là null
            if (data == null || data.Type == JTokenType.Null)
            {
                Console.WriteLine($"[FIREBASE-LOGIC] DELETE Detected on {path}");
                if (segments.Length >= 2)
                {
                    OnItemDeleted?.Invoke(this, new FirebaseDeleteEventArgs { RootNode = segments[0], TargetId = segments[1], FullPath = path });
                }
                else if (segments.Length == 1) // Xóa cả bảng
                {
                    OnItemDeleted?.Invoke(this, new FirebaseDeleteEventArgs { RootNode = segments[0], TargetId = "ALL", FullPath = path });
                }
                return;
            }

            // CASE 2: CẬP NHẬT (Update/Put)
            Console.WriteLine($"[FIREBASE-LOGIC] UPDATE Detected on {path}");
            string rootNode = segments.Length > 0 ? segments[0] : "Root";
            string key = segments.Length > 1 ? segments[1] : (data is JObject ? "Batch" : "Unknown");

            OnDataChanged?.Invoke(this, new FirebaseDataEventArgs
            {
                Path = path,
                RootNode = rootNode,
                Key = key,
                Data = data
            });
        }

        // --- Helper Methods ---
        private string BuildUrl(string path) => $"{_baseUrl}/{path.Trim('/')}.json?{AUTH_PARAM}={_secret}";

        public void Stop()
        {
            _cancelTokenSource?.Cancel();
            OnStatusChanged?.Invoke("⚪ Stopped", Color.Gray);
        }

        public void Dispose()
        {
            Stop();
            _cancelTokenSource?.Dispose();
        }

        // Các hàm Add/Patch/Delete giữ nguyên khung sườn (ít dùng trong dự án này nhưng cần cho đủ bộ)
        /// <summary>
        /// Thêm mới dữ liệu (PUSH). Firebase sẽ tự sinh Key ngẫu nhiên.
        /// </summary>
        public async Task<string> AddDataAsync<T>(string path, T data)
        {
            try
            {
                string url = BuildUrl(path);
                string json = JsonConvert.SerializeObject(data);
                Console.WriteLine($"[FIREBASE-POST] Adding to {path}: {json}"); // LOG DEBUG

                var content = new StringContent(json, Encoding.UTF8, MEDIA_TYPE_JSON);
                // POST: Gửi yêu cầu thêm mới
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                // Firebase trả về JSON dạng: { "name": "-KeyTuSinhRa123" }
                string responseBody = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(responseBody);
                string newKey = result["name"]?.ToString();

                Console.WriteLine($"[FIREBASE-POST] Success! New Key: {newKey}"); // LOG DEBUG
                return newKey;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREBASE-ERR] POST Failed: {ex.Message}"); // LOG DEBUG
                throw;
            }
        }
        /// <summary>
        /// Cập nhật một phần dữ liệu (PATCH). Chỉ sửa các trường được gửi lên, giữ nguyên các trường khác.
        /// </summary>
        public async Task PatchDataAsync(string path, object data)
        {
            try
            {
                string url = BuildUrl(path);
                string json = JsonConvert.SerializeObject(data);
                Console.WriteLine($"[FIREBASE-PATCH] Patching {path}: {json}"); // LOG DEBUG

                // HttpClient mặc định đời cũ không có PatchAsync, nên dùng SendAsync
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = new StringContent(json, Encoding.UTF8, MEDIA_TYPE_JSON)
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                Console.WriteLine($"[FIREBASE-PATCH] Success!"); // LOG DEBUG
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREBASE-ERR] PATCH Failed: {ex.Message}"); // LOG DEBUG
                throw;
            }
        }
        /// <summary>
        /// Xóa dữ liệu tại đường dẫn (DELETE)
        /// </summary>
        public async Task DeleteDataAsync(string path)
        {
            try
            {
                string url = BuildUrl(path);
                Console.WriteLine($"[FIREBASE-DELETE] Requesting Delete: {url}"); // LOG DEBUG

                var response = await _httpClient.DeleteAsync(url);
                response.EnsureSuccessStatusCode();

                Console.WriteLine($"[FIREBASE-DELETE] Success! Node {path} has been removed."); // LOG DEBUG
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIREBASE-ERR] DELETE Failed: {ex.Message}"); // LOG DEBUG
                throw;
            }
        }
    }

    // Các class chứa dữ liệu sự kiện
    public class FirebaseDataEventArgs : EventArgs
    {
        public string Path { get; set; }
        public string RootNode { get; set; }
        public string Key { get; set; }
        public JToken Data { get; set; }
        public T ToObject<T>() => Data.ToObject<T>();
    }

    public class FirebaseDeleteEventArgs : EventArgs
    {
        public string RootNode { get; set; }
        public string TargetId { get; set; }
        public string FullPath { get; set; }
    }
}