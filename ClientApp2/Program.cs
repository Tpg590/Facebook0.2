using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ClientApp
{
    internal class Program
    {
        private static string myName = "You";

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            string serverIP = "26.155.50.108";
            int serverPort = 13000;

            try
            {
                Console.WriteLine("Đang kết nối tới Server Chat...");
                TcpClient client = new TcpClient(serverIP, serverPort);
                NetworkStream stream = client.GetStream();

                // Kích hoạt Task nhận tin nhắn chạy ngầm (Bất đồng bộ)
                _ = ReceiveMessagesAsync(stream);

                // Chờ một chút để Server gửi tên định danh về trước khi bắt đầu gõ
                await Task.Delay(500);

                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($" CHÀO MỪNG BẠN ĐẾN VỚI PHÒNG CHAT! TÊN BẠN LÀ: {myName}");
                Console.WriteLine(" HƯỚNG DẪN CHAT:");
                Console.WriteLine(" 1. Gửi tin nhắn chữ riêng: Nhập cú pháp [TênClient:Nội dung]");
                Console.WriteLine("    Ví dụ: Client2:Alo nghe rõ trả lời");
                Console.WriteLine(" 2. GỬI ẢNH RIÊNG: Nhập cú pháp [IMAGE:TênClient:Đường dẫn file ảnh]");
                Console.WriteLine("    Ví dụ: IMAGE:Client2:C:\\Pictures\\cat.png");
                Console.WriteLine(" 3. Nhắn tin chung cho cả phòng: Cứ gõ chữ bình thường rồi Enter.");
                Console.WriteLine(" 4. Gõ 'exit' để thoát.");
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

                    // KHỞI CHẠY TÍNH NĂNG GỬI ẢNH
                    if (message.StartsWith("IMAGE:", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string[] parts = message.Split(':');
                            if (parts.Length >= 3)
                            {
                                string targetClient = parts[1].Trim();
                                // Lấy phần còn lại làm đường dẫn file đề phòng đường dẫn chứa dấu ':' (Ví dụ C:\...)
                                string filePath = message.Substring(message.IndexOf(parts[2])).Trim();

                                if (!File.Exists(filePath))
                                {
                                    Console.WriteLine("[Hệ thống] Thất bại: Đường dẫn ảnh không chính xác hoặc file không tồn tại!");
                                    continue;
                                }

                                // Tiến hành nén ảnh thành mảng byte thô
                                byte[] imageBytes = await File.ReadAllBytesAsync(filePath);
                                int imageSize = imageBytes.Length;

                                // Bước 1: Gửi chuỗi lệnh header báo kích thước ảnh lên Server
                                string headerCmd = $"SEND_IMAGE:{targetClient}:{imageSize}";
                                byte[] headerBytes = Encoding.UTF8.GetBytes(headerCmd);
                                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

                                // Chờ chút xíu để tránh nghẽn luồng truyền tải dữ liệu mạng nội bộ công cộng
                                await Task.Delay(150);

                                // Bước 2: Bơm trực tiếp luồng byte của tệp tin lên Server
                                await stream.WriteAsync(imageBytes, 0, imageBytes.Length);
                                Console.WriteLine($"-> Đã gửi thành công tệp ảnh ({imageSize} bytes) đến người nhận: {targetClient}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Lỗi gửi tệp tin ảnh]: {ex.Message}");
                        }
                        continue;
                    }

                    // Gửi dữ liệu text lên Server thông thường
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
            // Tăng kích thước bộ đệm lên 128KB để nhận phân đoạn byte ảnh hoàn chỉnh
            byte[] buffer = new byte[131072];
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

                    // HOẠT ĐỘNG: KHI ĐƯỢC MÁY KHÁC BẮN ẢNH SANG
                    if (response.StartsWith("RECEIVE_IMAGE_START:"))
                    {
                        string[] parts = response.Split(':');
                        string senderClient = parts[1];
                        int expectedImageSize = int.Parse(parts[2]);

                        Console.WriteLine($"\n[Tin Nhắn Ảnh] Đang nhận tệp ảnh từ {senderClient} ({expectedImageSize} bytes)...");

                        // Tạo mảng byte mới để hứng vừa khít dung lượng tệp tin ảnh thật
                        byte[] imageData = new byte[expectedImageSize];
                        int totalBytesRead = 0;

                        while (totalBytesRead < expectedImageSize)
                        {
                            int read = await stream.ReadAsync(imageData, totalBytesRead, expectedImageSize - totalBytesRead);
                            if (read == 0) break;
                            totalBytesRead += read;
                        }

                        // Lưu tệp tin ảnh nhận được vào thư mục hiện tại chứa phần mềm (.exe) kèm mốc thời gian
                        string fileExtension = ".png"; // File mặc định lưu dạng ảnh phổ biến
                        string savedFileName = $"Received_{DateTime.Now:yyyyMMdd_HHmmss}{fileExtension}";
                        string fullSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, savedFileName);

                        await File.WriteAllBytesAsync(fullSavePath, imageData);
                        Console.WriteLine($"[Hệ thống] Đã nhận xong! Lưu tại: {fullSavePath}");

                        // TỰ ĐỘNG BẬT ẢNH TRÊN MÀN HÌNH MÁY BÊN NHẬN NGAY LẬP TỨC
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullSavePath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Hệ thống] Đã nhận nhưng không thể kích hoạt tự hiển thị ảnh: {ex.Message}");
                        }
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