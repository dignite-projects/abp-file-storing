using System.Security.Cryptography;
using System.Text;

namespace System.IO;

public static class StreamExtensions
{
    public static string Md5(this Stream stream)
    {
        var md5 = MD5.Create();
        md5.ComputeHash(stream);
        var hash = md5.Hash!;
        md5.Clear();

        var sb = new StringBuilder(32);
        for (var i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("X2"));
        }

        if (stream.Position > 0)
        {
            stream.Position = 0;
        }

        return sb.ToString();
    }
}
