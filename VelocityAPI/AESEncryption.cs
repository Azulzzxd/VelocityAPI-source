using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

public class AESEncryption
{
    private const int KeySize = 256 /*0x0100*/;
    private const int SaltSize = 16 /*0x10*/;
    private const int NonceSize = 12;
    private const int TagSize = 16 /*0x10*/;
    private const int Iterations = 100000;

    [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
    private static extern uint BCryptOpenAlgorithmProvider(
      out IntPtr phAlgorithm,
      string pszAlgId,
      string pszImplementation,
      uint dwFlags);

    [DllImport("bcrypt.dll")]
    private static extern uint BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, uint dwFlags);

    [DllImport("bcrypt.dll")]
    private static extern uint BCryptGenerateSymmetricKey(
      IntPtr hAlgorithm,
      out IntPtr phKey,
      IntPtr pbKeyObject,
      uint cbKeyObject,
      byte[] pbSecret,
      uint cbSecret,
      uint dwFlags);

    [DllImport("bcrypt.dll")]
    private static extern uint BCryptDestroyKey(IntPtr hKey);

    [DllImport("bcrypt.dll")]
    private static extern uint BCryptEncrypt(
      IntPtr hKey,
      byte[] pbInput,
      uint cbInput,
      ref AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo,
      byte[] pbIV,
      uint cbIV,
      byte[] pbOutput,
      uint cbOutput,
      out uint pcbResult,
      uint dwFlags);

    [DllImport("bcrypt.dll")]
    private static extern uint BCryptDecrypt(
      IntPtr hKey,
      byte[] pbInput,
      uint cbInput,
      ref AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo,
      byte[] pbIV,
      uint cbIV,
      byte[] pbOutput,
      uint cbOutput,
      out uint pcbResult,
      uint dwFlags);

    [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
    private static extern uint BCryptSetProperty(
      IntPtr hObject,
      string pszProperty,
      byte[] pbInput,
      uint cbInput,
      uint dwFlags);

    public static string Encrypt(string plaintext, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(plaintext))
                throw new ArgumentException("Plaintext cannot be empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty");
            byte[] randomBytes1 = AESEncryption.GenerateRandomBytes(16 /*0x10*/);
            byte[] randomBytes2 = AESEncryption.GenerateRandomBytes(12);
            byte[] key = AESEncryption.DeriveKey(password, randomBytes1);
            byte[] tag;
            byte[] src = AESEncryption.EncryptBCrypt(Encoding.UTF8.GetBytes(plaintext), key, randomBytes2, out tag);
            byte[] numArray = new byte[randomBytes1.Length + randomBytes2.Length + src.Length + tag.Length];
            Buffer.BlockCopy((Array)randomBytes1, 0, (Array)numArray, 0, randomBytes1.Length);
            Buffer.BlockCopy((Array)randomBytes2, 0, (Array)numArray, randomBytes1.Length, randomBytes2.Length);
            Buffer.BlockCopy((Array)src, 0, (Array)numArray, randomBytes1.Length + randomBytes2.Length, src.Length);
            Buffer.BlockCopy((Array)tag, 0, (Array)numArray, randomBytes1.Length + randomBytes2.Length + src.Length, tag.Length);
            return Convert.ToBase64String(numArray);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public static string Decrypt(string ciphertext, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(ciphertext))
                throw new ArgumentException("Ciphertext cannot be empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty");
            byte[] src;
            try
            {
                src = Convert.FromBase64String(ciphertext);
            }
            catch (FormatException ex)
            {
                throw;
            }
            int num = 44;
            if (src.Length < num)
                throw new CryptographicException("Invalid ciphertext: data too short");
            byte[] numArray1 = new byte[16 /*0x10*/];
            byte[] numArray2 = new byte[12];
            int count = src.Length - 16 /*0x10*/ - 12 - 16 /*0x10*/;
            byte[] numArray3 = new byte[count];
            byte[] numArray4 = new byte[16 /*0x10*/];
            Buffer.BlockCopy((Array)src, 0, (Array)numArray1, 0, 16 /*0x10*/);
            Buffer.BlockCopy((Array)src, 16 /*0x10*/, (Array)numArray2, 0, 12);
            Buffer.BlockCopy((Array)src, 28, (Array)numArray3, 0, count);
            Buffer.BlockCopy((Array)src, 28 + count, (Array)numArray4, 0, 16 /*0x10*/);
            byte[] key = AESEncryption.DeriveKey(password, numArray1);
            return Encoding.UTF8.GetString(AESEncryption.DecryptBCrypt(numArray3, key, numArray2, numArray4));
        }
        catch (CryptographicException ex)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static byte[] EncryptBCrypt(byte[] plaintext, byte[] key, byte[] nonce, out byte[] tag)
    {
        IntPtr phAlgorithm = IntPtr.Zero;
        IntPtr phKey = IntPtr.Zero;
        GCHandle gcHandle1 = new GCHandle();
        GCHandle gcHandle2 = new GCHandle();
        tag = new byte[16 /*0x10*/];
        try
        {
            uint num1 = AESEncryption.BCryptOpenAlgorithmProvider(out phAlgorithm, "AES", (string)null, 0U);
            if (num1 != 0U)
                throw new CryptographicException($"BCryptOpenAlgorithmProvider failed: 0x{num1:X8}");
            byte[] bytes = Encoding.Unicode.GetBytes("ChainingModeGCM\0");
            uint num2 = AESEncryption.BCryptSetProperty(phAlgorithm, "ChainingMode", bytes, (uint)bytes.Length, 0U);
            if (num2 != 0U)
                throw new CryptographicException($"BCryptSetProperty failed: 0x{num2:X8}");
            uint symmetricKey = AESEncryption.BCryptGenerateSymmetricKey(phAlgorithm, out phKey, IntPtr.Zero, 0U, key, (uint)key.Length, 0U);
            if (symmetricKey != 0U)
                throw new CryptographicException($"BCryptGenerateSymmetricKey failed: 0x{symmetricKey:X8}");
            gcHandle1 = GCHandle.Alloc((object)nonce, GCHandleType.Pinned);
            gcHandle2 = GCHandle.Alloc((object)tag, GCHandleType.Pinned);
            AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo = new AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO)),
                dwInfoVersion = 1,
                pbNonce = gcHandle1.AddrOfPinnedObject(),
                cbNonce = (uint)nonce.Length,
                pbTag = gcHandle2.AddrOfPinnedObject(),
                cbTag = (uint)tag.Length
            };
            byte[] pbOutput = new byte[plaintext.Length];
            uint num3 = AESEncryption.BCryptEncrypt(phKey, plaintext, (uint)plaintext.Length, ref pPaddingInfo, (byte[])null, 0U, pbOutput, (uint)pbOutput.Length, out uint _, 0U);
            if (num3 != 0U)
                throw new CryptographicException($"BCryptEncrypt failed: 0x{num3:X8}");
            return pbOutput;
        }
        finally
        {
            if (gcHandle1.IsAllocated)
                gcHandle1.Free();
            if (gcHandle2.IsAllocated)
                gcHandle2.Free();
            if (phKey != IntPtr.Zero)
            {
                int num4 = (int)AESEncryption.BCryptDestroyKey(phKey);
            }
            if (phAlgorithm != IntPtr.Zero)
            {
                int num5 = (int)AESEncryption.BCryptCloseAlgorithmProvider(phAlgorithm, 0U);
            }
        }
    }

    private static byte[] DecryptBCrypt(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
    {
        IntPtr phAlgorithm = IntPtr.Zero;
        IntPtr phKey = IntPtr.Zero;
        GCHandle gcHandle1 = new GCHandle();
        GCHandle gcHandle2 = new GCHandle();
        try
        {
            uint num1 = AESEncryption.BCryptOpenAlgorithmProvider(out phAlgorithm, "AES", (string)null, 0U);
            if (num1 != 0U)
                throw new CryptographicException($"BCryptOpenAlgorithmProvider failed: 0x{num1:X8}");
            byte[] bytes = Encoding.Unicode.GetBytes("ChainingModeGCM\0");
            uint num2 = AESEncryption.BCryptSetProperty(phAlgorithm, "ChainingMode", bytes, (uint)bytes.Length, 0U);
            if (num2 != 0U)
                throw new CryptographicException($"BCryptSetProperty failed: 0x{num2:X8}");
            uint symmetricKey = AESEncryption.BCryptGenerateSymmetricKey(phAlgorithm, out phKey, IntPtr.Zero, 0U, key, (uint)key.Length, 0U);
            if (symmetricKey != 0U)
                throw new CryptographicException($"BCryptGenerateSymmetricKey failed: 0x{symmetricKey:X8}");
            gcHandle1 = GCHandle.Alloc((object)nonce, GCHandleType.Pinned);
            gcHandle2 = GCHandle.Alloc((object)tag, GCHandleType.Pinned);
            AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo = new AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(AESEncryption.BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO)),
                dwInfoVersion = 1,
                pbNonce = gcHandle1.AddrOfPinnedObject(),
                cbNonce = (uint)nonce.Length,
                pbTag = gcHandle2.AddrOfPinnedObject(),
                cbTag = (uint)tag.Length
            };
            byte[] pbOutput = new byte[ciphertext.Length];
            uint num3 = AESEncryption.BCryptDecrypt(phKey, ciphertext, (uint)ciphertext.Length, ref pPaddingInfo, (byte[])null, 0U, pbOutput, (uint)pbOutput.Length, out uint _, 0U);
            if (num3 != 0U)
                throw new CryptographicException($"BCryptDecrypt failed: 0x{num3:X8} - Wrong password or corrupted data");
            return pbOutput;
        }
        finally
        {
            if (gcHandle1.IsAllocated)
                gcHandle1.Free();
            if (gcHandle2.IsAllocated)
                gcHandle2.Free();
            if (phKey != IntPtr.Zero)
            {
                int num4 = (int)AESEncryption.BCryptDestroyKey(phKey);
            }
            if (phAlgorithm != IntPtr.Zero)
            {
                int num5 = (int)AESEncryption.BCryptCloseAlgorithmProvider(phAlgorithm, 0U);
            }
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return AESEncryption.PBKDF2_SHA256(Encoding.UTF8.GetBytes(password), salt, 100000, 32 /*0x20*/);
    }

    private static byte[] PBKDF2_SHA256(
      byte[] password,
      byte[] salt,
      int iterations,
      int outputBytes)
    {
        using (HMACSHA256 hmacshA256 = new HMACSHA256(password))
        {
            int count = hmacshA256.HashSize / 8;
            int num = (int)Math.Ceiling((double)outputBytes / (double)count);
            byte[] numArray1 = new byte[num * count];
            for (int index1 = 1; index1 <= num; ++index1)
            {
                byte[] numArray2 = new byte[salt.Length + 4];
                Buffer.BlockCopy((Array)salt, 0, (Array)numArray2, 0, salt.Length);
                Buffer.BlockCopy((Array)AESEncryption.GetBigEndianBytes(index1), 0, (Array)numArray2, salt.Length, 4);
                byte[] hash = hmacshA256.ComputeHash(numArray2);
                byte[] src = (byte[])hash.Clone();
                for (int index2 = 1; index2 < iterations; ++index2)
                {
                    hash = hmacshA256.ComputeHash(hash);
                    for (int index3 = 0; index3 < src.Length; ++index3)
                        src[index3] ^= hash[index3];
                }
                Buffer.BlockCopy((Array)src, 0, (Array)numArray1, (index1 - 1) * count, count);
            }
            byte[] dst = new byte[outputBytes];
            Buffer.BlockCopy((Array)numArray1, 0, (Array)dst, 0, outputBytes);
            return dst;
        }
    }

    private static byte[] GetBigEndianBytes(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse<byte>(bytes);
        return bytes;
    }

    private static byte[] GenerateRandomBytes(int length)
    {
        byte[] data = new byte[length];
        using (RNGCryptoServiceProvider cryptoServiceProvider = new RNGCryptoServiceProvider())
            cryptoServiceProvider.GetBytes(data);
        return data;
    }

    private struct BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO
    {
        public uint cbSize;
        public uint dwInfoVersion;
        public IntPtr pbNonce;
        public uint cbNonce;
        public IntPtr pbAuthData;
        public uint cbAuthData;
        public IntPtr pbTag;
        public uint cbTag;
        public IntPtr pbMacContext;
        public uint cbMacContext;
        public uint cbAAD;
        public ulong cbData;
        public uint dwFlags;
    }
}
