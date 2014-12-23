using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AesCrypt
{
    class Program
    {
        const int ReadBufferSize = 64 * 1024;

        static void Main(string[] args)
        {
            if (args == null || args.Length < 1 || (args[0].ToLower() != "e" && args[0].ToLower() != "d"))
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine(" To encrypt: AesCrypt.exe e <file path>");
                Console.Error.WriteLine(" To decrypt: AesCrypt.exe d <file path> <iv> <key>");
                return;
            }

            if (args[0].ToLower() == "e")
            {
                // ENCRYPTION

                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: AesCrypt.exe e <file path>");
                }

                string fileToEncrypt = args[1];
                FileInfo inpFile = new FileInfo(fileToEncrypt);
                if (!inpFile.Exists)
                {
                    Console.Error.WriteLine("File does not exist");
                    return;
                }

                byte[] fileName = Encoding.UTF8.GetBytes(inpFile.FullName);

                string rootDir = Path.GetDirectoryName(fileToEncrypt);
                string randomName = Guid.NewGuid().ToString("N");
                string tempName = Path.Combine(rootDir, randomName);
                string finalHash;

                if (File.Exists(tempName))
                {
                    Console.Error.WriteLine("ERROR: temporary file exists, wtf how does that even happen");
                    return;
                }

                using (FileStream inp = File.Open(fileToEncrypt, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                    using (CryptoStream hashStream = new CryptoStream(oup, cryptedHash, CryptoStreamMode.Write))
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

                    finalHash = Base32.ToBase32String(cryptedHash.Hash);

                    string iv = Convert.ToBase64String(enc.IV);
                    string key = Convert.ToBase64String(enc.Key);

                    Console.Out.WriteLine("{0} {1} {2}", finalHash, iv, key);
                }

                File.Move(tempName, Path.Combine(rootDir, finalHash));
            }
            else
            {
                // DECRYPTION

                if (args.Length < 4)
                {
                    Console.Error.WriteLine("Usage: AesCrypt.exe d <file path> <iv> <key>");
                    return;
                }

                FileInfo encFile = new FileInfo(args[1]);
                if (!encFile.Exists)
                {
                    Console.Error.WriteLine("File does not exist");
                    return;
                }

                byte[] iv = Convert.FromBase64String(args[2]);
                if (iv == null || iv.Length < 1)
                {
                    Console.Error.WriteLine("ERROR: invalid iv");
                    return;
                }
                byte[] key = Convert.FromBase64String(args[3]);
                if (key == null || key.Length < 1)
                {
                    Console.Error.WriteLine("ERROR: invalid key");
                    return;
                }

                using (FileStream inp = encFile.OpenRead())
                using (AesManaged aes = new AesManaged())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;

                    aes.IV = iv;
                    aes.Key = key;

                    using (CryptoStream cs = new CryptoStream(inp, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        // crypted file structure: {name length x4}{full file name}{data length x8}{data}{sha512 hash of data x64}

                        byte[] nameLengthBits = new byte[2];
                        if (cs.Read(nameLengthBits, 0, 2) != 2)
                        {
                            Console.Error.WriteLine("ERROR: Failed reading file name size");
                            return;
                        }
                        ushort nameLength = BitConverter.ToUInt16(nameLengthBits, 0);

                        byte[] originalName = new byte[nameLength];
                        if (cs.Read(originalName, 0, nameLength) != nameLength)
                        {
                            Console.Error.WriteLine("ERROR: Failed reading file name");
                            return;
                        }
                        string fileName = Encoding.UTF8.GetString(originalName);

                        byte[] dataLengthBits = new byte[8];
                        if (cs.Read(dataLengthBits, 0, dataLengthBits.Length) != dataLengthBits.Length)
                        {
                            Console.Error.WriteLine("ERROR: Failed reading data length");
                            return;
                        }
                        long dataLength = BitConverter.ToInt64(dataLengthBits, 0);

                        string outputFileName = Path.GetFileName(fileName);
                        if (File.Exists(outputFileName))
                        {
                            Console.Error.WriteLine("ERROR: '{0}' already exists, exiting", outputFileName);
                            return;
                        }

                        Console.Out.WriteLine("Decrypting '{0}', {1:N0} bytes", fileName, dataLength);
                        byte[] decryptedHash;
                        long totalRead = 0;
                        using (FileStream outputStream = new FileStream(outputFileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                        using (SHA512Managed hasher = new SHA512Managed())
                        {
                            byte[] buffer = new byte[ReadBufferSize];
                            long bytesRemaining = dataLength;
                            while (bytesRemaining > 0)
                            {
                                int readingThisRound = ReadBufferSize < bytesRemaining ? ReadBufferSize : (int)bytesRemaining;
                                int bytesRead = cs.Read(buffer, 0, readingThisRound);
                                totalRead += bytesRead;

                                // dump decrypted data to file
                                outputStream.Write(buffer, 0, bytesRead);

                                // run it through the grinder for verification later
                                int hashProgress = hasher.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                                Debug.Assert(hashProgress == bytesRead, "Hash calculation out of whack with file IO, wtf is going on");

                                bytesRemaining -= bytesRead;
                            }

                            hasher.TransformFinalBlock(buffer, 0, 0);
                            decryptedHash = hasher.Hash;
                        }

                        byte[] originalHashBits = new byte[64];
                        int wtf;
                        if ((wtf = cs.Read(originalHashBits, 0, originalHashBits.Length)) != originalHashBits.Length)
                        {
                            Console.Error.WriteLine("ERROR: Failed reading verification hash {0} vs 64", wtf);
                            return;
                        }

                        if (originalHashBits.SequenceEqual(decryptedHash))
                        {
                            Console.Out.WriteLine("Decrypted '{0}'", outputFileName);
                        }
                        else
                        {
                            Console.Out.WriteLine("Decryption FAIL");
                        }
                    }
                }
            }
        }
    }
}