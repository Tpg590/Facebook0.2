using System.Net;

namespace DNS
{
    public class Program
    {

        static void Main(string[] args)
        {
            String domain = "google.com";
            Console.WriteLine( $"Resolving {domain}...");
            
            IPHostEntry hostEntry = Dns.GetHostEntry(domain);
            Console.WriteLine($"Host Name: {hostEntry.HostName}");
            Console.WriteLine("Danh sach cac dia chi IP: ");

            foreach (IPAddress ip in hostEntry.AddressList)
            {
                Console.WriteLine(ip.ToString());
            }
        }
    }
}
