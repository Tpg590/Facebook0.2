using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ClientApp
{
    internal class Program
    {
        private static string myName = "Elon Musk";

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            string serverIP = "127.0.0.1";
            int serverPort = 13000;

            try
            {
                Console.WriteLine("Đang kết nối tới Server Chat...");
                TcpClient client = new TcpClient(serverIP, serverPort);
                NetworkStream stream = client.GetStream();

                // Kích hoạt Task nhận tin nhắn chạy ngầm (Bất đồng bộ)
                // Task này chạy độc lập, Server đẩy tin về lúc nào là in ra màn hình lúc đó
                _ = ReceiveMessagesAsync(stream);

                // Chờ một chút để Server gửi tên định danh về trước khi bắt đầu gõ
                await Task.Delay(500);

                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($" CHÀO MỪNG BẠN ĐẾN VỚI PHÒNG CHAT! TÊN BẠN LÀ: {myName}");
                Console.WriteLine(" HƯỚNG DẪN CHAT:");
                Console.WriteLine(" 1. Gửi riêng ai đó: Nhập cú pháp [TênClient:Nội dung]");
                Console.WriteLine("    Ví dụ nếu bạn là Client1 muốn nhắn Client2: Client2:Alo nghe rõ trả lời");
                Console.WriteLine(" 2. Nhắn tin chung cho cả phòng: Cứ gõ chữ bình thường rồi Enter.");
                Console.WriteLine(" 3. Gõ 'exit' để thoát.");
                Console.WriteLine("--------------------------------------------------\n");

                while (true)
                {
                    string message = Console.ReadLine();

                    if (string.IsNullOrEmpty(message)) continue;

                    if (message.Trim().ToLower() == "exit")
                    {
                        Console.WriteLine("Đang rời phòng chat...");
                        break;
                    }

                    // Gửi dữ liệu lên Server
                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                }

                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mất kết nối tới server: {ex.Message}");
            }
        }

        // LUỒNG NHẬN TIN NHẮN TỪ SERVER (Chạy song song liên tục)
        private static async Task ReceiveMessagesAsync(NetworkStream stream)
        {
            byte[] buffer = new byte[65536];
            try
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Server đóng kết nối

                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Xử lý gói tin đặc biệt thiết lập tên ban đầu từ Server
                    if (response.StartsWith("SERVER_WELCOME:"))
                    {
                        myName = response.Replace("SERVER_WELCOME:", "");
                        continue;
                    }

                    // Xử lý gói tin thông báo hệ thống thông thường
                    if (response.StartsWith("SERVER_NOTIFY:"))
                    {
                        string notifyMsg = response.Replace("SERVER_NOTIFY:", "");
                        Console.WriteLine($"\n{notifyMsg}");
                        continue;
                    }

                    // In các tin nhắn chat từ người khác ra màn hình ngay lập tức
                    Console.WriteLine($"\n{response}");
                }
            }
            catch
            {
                Console.WriteLine("\n[Hệ thống] Đã ngắt kết nối khỏi máy chủ.");
            }
        }
    }
}