using System.Net;

namespace PRN222
{
    public class Program
    {
        //static void Main(string[] args)
        //{
        //    // Chuyển đổi tên miền www.contoso.com sang các địa chỉ IP tương ứng
        //    var domainEntry = Dns.GetHostEntry("www.contoso.com");

        //    Console.WriteLine($"Host Name: {domainEntry.HostName}");

        //    // Liệt kê danh sách các địa chỉ IP của tên miền này
        //    foreach (var ip in domainEntry.AddressList)
        //    {
        //        Console.WriteLine($"IP Address: {ip}");
        //    }

        //    // Ngược lại, có thể tìm tên miền từ địa chỉ IP (Reverse DNS)
        //    var entryByAddress = Dns.GetHostEntry("127.0.0.1");
        //    Console.WriteLine($"Host Name from IP: {entryByAddress.HostName}");
        //}

        static readonly HttpClient httpClient = new HttpClient();
        static async Task Main()
        {
            string uri = "https://www.y8.com/";
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine(content);

            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }

        }
    }

}
