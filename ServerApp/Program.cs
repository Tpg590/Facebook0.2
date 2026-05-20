using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    internal class Program
    {
        // Dùng ConcurrentDictionary để an toàn khi nhiều luồng (Client) thêm/xóa cùng lúc
        private static ConcurrentDictionary<string, TcpClient> _onlineClients = new ConcurrentDictionary<string, TcpClient>();

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            string host = "0.0.0.0";
            int port = 13000;

            TcpListener server = null;
            try
            {
                IPAddress localAddr = IPAddress.Parse(host);
                server = new TcpListener(localAddr, port);
                server.Start();
                Console.WriteLine("Server Chat Online đang chạy... Chờ các Client kết nối...");

                int clientCount = 0;
                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    clientCount++;

                    // Tạm thời đặt tên tự động: Client1, Client2...
                    string clientName = $"Client{clientCount}";
                    _onlineClients.TryAdd(clientName, client);

                    Console.WriteLine($"\n[KẾT NỐI] {clientName} vừa tham gia từ: {client.Client.RemoteEndPoint}");

                    // Gửi thông báo cho Client biết tên của chính nó
                    await SendToSingleClientAsync(client, $"SERVER_WELCOME:{clientName}");

                    // Thông báo cho các Client khác biết có người mới vào
                    await BroadcastMessageAsync($"SERVER_NOTIFY:[Hệ thống] {clientName} đã online.", clientName);

                    // Tách luồng xử lý riêng biệt cho Client này
                    _ = HandleClientChatAsync(client, clientName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi hệ thống: {ex.Message}");
            }
            finally
            {
                server?.Stop();
            }
        }

        private static async Task HandleClientChatAsync(TcpClient client, string currentClientName)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                // Nâng kích thước buffer lên 128KB để nhận các phân đoạn dữ liệu ảnh mượt mà hơn
                byte[] buffer = new byte[131072];
                int bytesRead;

                try
                {
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        string rawMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                        if (string.IsNullOrEmpty(rawMessage)) continue;

                        // ==========================================
                        // XỬ LÝ LOGIC CHUYỂN TIẾP ẢNH (IMAGE FORWARDING)
                        // ==========================================
                        if (rawMessage.StartsWith("SEND_IMAGE:"))
                        {
                            // Cú pháp: SEND_IMAGE:NgườiNhận:KíchThướcFile
                            string[] parts = rawMessage.Split(':');
                            if (parts.Length >= 3)
                            {
                                string targetClientName = parts[1].Trim();
                                int imageSize = int.Parse(parts[2].Trim());

                                Console.WriteLine($"[Hệ thống] {currentClientName} đang gửi dữ liệu ảnh ({imageSize} bytes) tới {targetClientName}...");

                                if (_onlineClients.TryGetValue(targetClientName, out TcpClient targetClient))
                                {
                                    // 1. Gửi tín hiệu báo trước cho Client đích khởi tạo tiến trình nhận mảng byte ảnh
                                    string alertMsg = $"RECEIVE_IMAGE_START:{currentClientName}:{imageSize}";
                                    await SendToSingleClientAsync(targetClient, alertMsg);

                                    // Nghỉ 150ms để luồng mạng bên nhận đồng bộ trạng thái, tránh dính gói tin
                                    await Task.Delay(150);

                                    // 2. Đọc trực tiếp dòng byte ảnh từ người gửi và pipe (đổ) thẳng sang luồng của người nhận
                                    int totalBytesReceived = 0;
                                    NetworkStream targetStream = targetClient.GetStream();

                                    while (totalBytesReceived < imageSize)
                                    {
                                        int read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, imageSize - totalBytesReceived));
                                        if (read == 0) break;

                                        await targetStream.WriteAsync(buffer, 0, read);
                                        totalBytesReceived += read;
                                    }
                                    Console.WriteLine($"[Hệ thống] Đã chuyển tiếp ảnh hoàn tất từ {currentClientName} sang {targetClientName}.");
                                }
                                else
                                {
                                    await SendToSingleClientAsync(client, $"[Hệ thống]: Không tìm thấy {targetClientName} để gửi ảnh.");
                                }
                            }
                            continue;
                        }

                        // ==========================================
                        // XỬ LÝ LOGIC CHAT VĂN BẢN TRUYỀN THỐNG
                        // ==========================================
                        Console.WriteLine($"[{currentClientName} gửi lên]: {rawMessage}");

                        int colonIndex = rawMessage.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string targetClientName = rawMessage.Substring(0, colonIndex).Trim();
                            string messageContent = rawMessage.Substring(colonIndex + 1).Trim();

                            // Xử lý gửi tin nhắn riêng cho 1 người
                            if (_onlineClients.TryGetValue(targetClientName, out TcpClient targetClient))
                            {
                                string formattedMsg = $"[{currentClientName} nói riêng]: {messageContent}";
                                await SendToSingleClientAsync(targetClient, formattedMsg);
                            }
                            else
                            {
                                await SendToSingleClientAsync(client, $"[Hệ thống]: Không tìm thấy {targetClientName} online.");
                            }
                        }
                        else
                        {
                            // Nếu gõ không đúng cú pháp có dấu ':', Server mặc định hiểu là gửi cho TẤT CẢ mọi người
                            string formattedMsg = $"[{currentClientName} nói chung]: {rawMessage}";
                            await BroadcastMessageAsync(formattedMsg, currentClientName);
                        }
                    }
                }
                catch (Exception)
                {
                    // Ngoại lệ xảy ra khi client tắt đột ngột
                }
                finally
                {
                    // Khi Client ngắt kết nối, xóa khỏi danh sách online
                    _onlineClients.TryRemove(currentClientName, out _);
                    Console.WriteLine($"\n[NGẮT KẾT NỐI] {currentClientName} đã rời phòng chat.");
                    await BroadcastMessageAsync($"SERVER_NOTIFY:[Hệ thống] {currentClientName} đã offline.", currentClientName);
                }
            }
        }

        // Hàm gửi tin nhắn tới duy nhất 1 Client
        private static async Task SendToSingleClientAsync(TcpClient client, string message)
        {
            try
            {
                if (client != null && client.Connected)
                {
                    NetworkStream stream = client.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi tin đơn: {ex.Message}");
            }
        }

        // Hàm gửi tin nhắn tới tất cả mọi người (trừ người gửi)
        private static async Task BroadcastMessageAsync(string message, string excludeClientName)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var kvp in _onlineClients)
            {
                if (kvp.Key != excludeClientName && kvp.Value.Connected)
                {
                    try
                    {
                        NetworkStream stream = kvp.Value.GetStream();
                        await stream.WriteAsync(data, 0, data.Length);
                    }
                    catch { /* Bỏ qua client lỗi */ }
                }
            }
        }
    }
}