using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ipv6Server
{
    class DataFilter
    {
        static byte[] bytes = ASCIIEncoding.ASCII.GetBytes("ZeroCool");

        public static byte[] GetBytes(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                return null;
            }

            DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateEncryptor(bytes, bytes), CryptoStreamMode.Write);

            StreamWriter writer = new StreamWriter(cryptoStream);
            writer.Write(data);
            writer.Flush();
            cryptoStream.FlushFinalBlock();
            writer.Flush();

            return memoryStream.ToArray();
        }

        public static string GetString(byte[] data, int offset, int length)
        {
            byte[] toDecrypt = new byte[length];
            Array.Copy(data, toDecrypt, length);
            if (toDecrypt.Length <= 0)
            {
                return null;
            }

            try
            {
                DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
                MemoryStream memoryStream = new MemoryStream(toDecrypt);
                CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateDecryptor(bytes, bytes), CryptoStreamMode.Read);
                StreamReader reader = new StreamReader(cryptoStream);

                return reader.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }
    }
}
