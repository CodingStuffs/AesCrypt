using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AesCrypt
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                Console.Error.WriteLine("Usage: enc.exe <file path>");
                return;
            }

            FileInfo inpFile = new FileInfo(args[0]);
            if (!inpFile.Exists)
            {
                Console.Error.WriteLine("File does not exist");
                return;
            }

            byte[] fileName = Encoding.UTF8.GetBytes(inpFile.FullName);

            string rootDir = Path.GetDirectoryName(args[0]);
            string randomName = Guid.NewGuid().ToString("N");
            string tempName = Path.Combine(rootDir, randomName);
            string finalHash;

            if (File.Exists(tempName))
            {
                Console.Error.WriteLine("ERROR: temporary file exists, wtf how does that even happen");
                return;
            }

            using (FileStream inp = File.Open(args[0], FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream oup = File.Create(tempName))
            using (AesManaged enc = new AesManaged())
            {
                enc.KeySize = 256;
                enc.Mode = CipherMode.CBC;

                enc.GenerateIV();
                enc.GenerateKey();

                SHA512Managed originalHash = new SHA512Managed();
                originalHash.Initialize();

                SHA512Managed cryptedHash = new SHA512Managed();
                using(CryptoStream hashStream = new CryptoStream(oup, cryptedHash, CryptoStreamMode.Write))
                using (CryptoStream cs = new CryptoStream(hashStream, enc.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    // crypted file structure: {name length x2}{full file name}{data length x8}{data}{sha512 hash of data x64}

                    byte[] lenFileName = BitConverter.GetBytes((ushort)fileName.Length);
                    cs.Write(lenFileName, 0, lenFileName.Length);

                    cs.Write(fileName, 0, fileName.Length);

                    byte[] fileSizeBits = BitConverter.GetBytes(inpFile.Length);
                    cs.Write(fileSizeBits, 0, fileSizeBits.Length);

                    byte[] data = new byte[64 * 1024];
                    int bytesRead;
                    long bytesHashed = 0;
                    do
                    {
                        // pull data from original file
                        bytesRead = inp.Read(data, 0, data.Length);

                        // send it to crypted stream
                        cs.Write(data, 0, bytesRead);

                        // also hash it for decryption verification purposes
                        bytesHashed += originalHash.TransformBlock(data, 0, bytesRead, data, 0);
                    } while (bytesRead > 0);

                    // write original hash into crypted file so we can verify it after decryption
                    originalHash.TransformFinalBlock(data, 0, 0);
                    cs.Write(originalHash.Hash, 0, originalHash.Hash.Length);
                }

                finalHash = bytesToHex(cryptedHash.Hash);

                string iv = Convert.ToBase64String(enc.IV);
                string key = Convert.ToBase64String(enc.Key);

                Console.Out.WriteLine("{0} {1} {2}", finalHash, iv, key);
            }

            File.Move(tempName, Path.Combine(rootDir, finalHash));
        }

        private static string bytesToHex(byte[] data)
        {
            StringBuilder niceHash = new StringBuilder();
            for (int i = 0; i < data.Length; ++i)
            {
                niceHash.AppendFormat("{0:X2}", data[i]);
            }
            return niceHash.ToString();
        }
    }
}