using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace UBCS2_A
{
    public class Chat
    {
        public string Sender { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;

        // Lưu mã màu (ví dụ: "Blue", "#FF0000")
        public string UserColor { get; set; } = "Black";

        // Lưu trạng thái in đậm
        public bool IsBold { get; set; } = false;
    }
}
