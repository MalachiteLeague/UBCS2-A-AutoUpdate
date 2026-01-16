using System.Threading.Tasks;
using System.Windows.Forms;
using UBCS2_A.Services;

namespace UBCS2_A
{
    public partial class Form1
    {
        private ChatContext _chatContext;

        private void SetupChatSystem(FirebaseService firebase)
        {
            _chatContext = new ChatContext(firebase);

            if (dgvChatAll != null && dgvChatPrivate != null)
            {
                _chatContext.RegisterControls(dgvChatAll, dgvChatPrivate, cboChatTarget, txtChatContent, btnChatSend);

                // Đăng ký sự kiện UI cho Chat
                if (btnChatSend != null) btnChatSend.Click += (s, e) => HandleSendChat();
                if (txtChatContent != null)
                {
                    txtChatContent.KeyDown += (s, e) => {
                        if (e.KeyCode == Keys.Enter)
                        {
                            HandleSendChat();
                            e.SuppressKeyPress = true;
                        }
                    };
                }
            }
        }

        private void HandleSendChat()
        {
            string currentSender = cboKhuVuc.SelectedItem?.ToString() ?? "Khách";
            string target = cboChatTarget.SelectedItem?.ToString() ?? "Toàn viện";
            string content = txtChatContent.Text.Trim();

            if (!string.IsNullOrEmpty(content))
            {
                _chatContext.SendMessageReal(currentSender, target, content);
                txtChatContent.Clear();
                txtChatContent.Focus();
            }
        }

        private async Task StartChatSyncAsync()
        {
            if (_chatContext != null)
            {
                _chatContext.StartSync();
                await _chatContext.LoadInitialDataAsync();
            }
        }
    }
}