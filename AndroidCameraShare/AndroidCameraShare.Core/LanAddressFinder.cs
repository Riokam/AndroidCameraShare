using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// IPv4 домашней сети для URL и QR. Loopback и 169.254 не показываем — с другого устройства туда не зайти.
    /// </summary>
    public static class LanAddressFinder
    {
        public static string? TryGetIpv4()
        {
            List<IPAddress> addresses = [];

            foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation unicast in network.GetIPProperties().UnicastAddresses)
                {
                    addresses.Add(unicast.Address);
                }
            }

            return TryPickIpv4(addresses);
        }

        public static string? TryPickIpv4(IEnumerable<IPAddress> addresses)
        {
            foreach (IPAddress address in addresses)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(address))
                {
                    continue;
                }

                byte[] bytes = address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    continue;
                }

                return address.ToString();
            }

            return null;
        }
    }
}
