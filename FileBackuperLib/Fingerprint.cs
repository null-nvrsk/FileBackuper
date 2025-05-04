using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace FileBackuperLib
{
    public static class Fingerprint
    {
        public static string GetMd5Hash()
        {
            string machineName = Environment.MachineName;
            string userName = Environment.UserName;
            string macAddress = GetMacAddress();

            string rawData = machineName + userName + macAddress;
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        static string GetMacAddress()
        {
            var nic = NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            return nic?.GetPhysicalAddress().ToString() ?? "00:00:00:00:00:00";
        }

        
    }
}
