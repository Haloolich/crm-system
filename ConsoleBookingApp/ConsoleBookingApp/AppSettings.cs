using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ConsoleBookingApp
{
    public class AppSettings
    {
        public string DefaultIpAddress { get; set; }

        public string DbConnectionString { get; set; } = "server=localhost;database=booking;uid=user;pwd=password;";
        public string DbHost { get; set; } = "localhost";
        public int DbPort { get; set; } = 3306;
        public string DbName { get; set; } = "schedule_db";
        public string DbUser { get; set; } = "Admin";
        public string DbPassword { get; set; } = "admin";
        public int Port { get; set; } = 8888;

        public string ConnectionString => $"Server={DbHost};Database={DbName};Uid={DbUser};Pwd={DbPassword};Port={DbPort};";

        public AppSettings()
        {
            this.DefaultIpAddress = GetLocalIPAddress();

            if (string.IsNullOrEmpty(this.DefaultIpAddress) || this.DefaultIpAddress == "0.0.0.0")
            {
                Console.WriteLine("Попередження: Не вдалося автоматично визначити IP-адресу основної мережі. Використовується localhost.");
                this.DefaultIpAddress = "127.0.0.1";
            }

            Console.WriteLine($"Використовується IP-адреса сервера: {this.DefaultIpAddress}");
        }

        private string GetLocalIPAddress()
        {
            try
            {
                // Отримуємо всі активні мережеві інтерфейси, крім Loopback та Tunnel (VPN)
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .ToList();
                foreach (NetworkInterface ni in networkInterfaces)
                {
                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    if (ipProps.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork &&
                                                         !IPAddress.IsLoopback(g.Address)))
                    {
                        foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !IPAddress.IsLoopback(addr.Address))
                            {
                                return addr.Address.ToString();
                            }
                        }
                    }
                }
                foreach (NetworkInterface ni in networkInterfaces)
                {
                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }
                return string.Empty; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при отриманні IP-адреси: {ex.Message}");
                return string.Empty;
            }
        }
    }
}