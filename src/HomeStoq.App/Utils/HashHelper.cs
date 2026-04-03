using System.Security.Cryptography;
using System.Text;

namespace HomeStoq.App.Utils;

public static class HashHelper
{
    public static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
