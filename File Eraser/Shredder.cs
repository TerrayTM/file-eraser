using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace File_Eraser
{
    static class Shredder
    {
        static RNGCryptoServiceProvider CSPRNG = new RNGCryptoServiceProvider();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(string FileName, uint DesiredAccess, uint ShareMode, IntPtr SecurityAttributes, uint CreationDesposition, uint FlagsAndAttributes, IntPtr HandleTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr FileHandle, byte[] Buffer, uint BytesToWrite, ref uint BytesWritten, IntPtr Overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(IntPtr FileHandle);
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetFilePointerEx")]
        private static extern bool SetFilePointer(IntPtr FileHandle, ulong DistanceToMove, [Out, Optional] IntPtr NewFilePointer, uint MoveMethod);
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetFileSizeEx")]
        private static extern bool GetFileSize(IntPtr FileHanle, out ulong FileSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr ObjectHandle);

        const uint DesiredAccess = 0x10000000;
        const uint ShareMode = 0x00000000;
        const uint CreationDisposition = 3;
        const uint Flags = 0x00000080 | 0x04000000;
        const uint FileStart = 0;
        const uint FileCurrent = 1;
        const uint FileEnd = 2;

        public static bool GutmannOverwriteMethod(string FileName)
        {
            try
            {
                if (!File.Exists(FileName))
                {
                    return false;
                }
                File.SetAttributes(FileName, FileAttributes.Normal);
                IntPtr FileHandle = CreateFile(FileName, DesiredAccess, ShareMode, IntPtr.Zero, CreationDisposition, Flags, IntPtr.Zero);
                if (FileHandle.ToInt32() == -1)
                {
                    return false;
                }
                if (!SetFilePointer(FileHandle, 0, IntPtr.Zero, FileStart))
                {
                    return false;
                }
                bool Success = true;
                try
                {
                    for (int i = 0; i < 4; ++i)
                    {
                        Success = WipeFile(FileHandle, null) && Success;
                    }
                    byte[][] OverwriteData = new byte[][]
                    {
                        new byte[] {85, 85, 85},
                        new byte[] {170, 170, 170},
                        new byte[] {146, 73, 36},
                        new byte[] {73, 36, 146},
                        new byte[] {36, 146, 73},
                        new byte[] {0, 0, 0},
                        new byte[] {17, 17, 17},
                        new byte[] {34, 34, 34},
                        new byte[] {51, 51, 51},
                        new byte[] {68, 68, 68},
                        new byte[] {85, 85, 85},
                        new byte[] {102, 102, 102},
                        new byte[] {119, 119, 119},
                        new byte[] {136, 136, 136},
                        new byte[] {153, 153, 153},
                        new byte[] {170, 170, 170},
                        new byte[] {187, 187, 187},
                        new byte[] {204, 204, 204},
                        new byte[] {221, 221, 221},
                        new byte[] {238, 238, 238},
                        new byte[] {255, 255, 255},
                        new byte[] {146, 73, 36},
                        new byte[] {73, 36, 146},
                        new byte[] {36, 146, 73},
                        new byte[] {109, 182, 219},
                        new byte[] {182, 219, 109},
                        new byte[] {219, 109, 182}
                    };
                    for (int i = 0; i < OverwriteData.Length; ++i)
                    {
                        Success = WipeFile(FileHandle, OverwriteData[i]) && Success;
                    }
                    for (int i = 0; i < 4; ++i)
                    {
                        Success = WipeFile(FileHandle, null) && Success;
                    }
                }
                catch
                {
                    throw new Exception(Marshal.GetLastWin32Error().ToString());
                }
                finally
                {
                    CloseHandle(FileHandle);
                }
                return Success;
            }
            catch
            {
                throw new Exception(Marshal.GetLastWin32Error().ToString());
            }
        }

        private static void FillBuffer(byte[] TheBuffer, byte[] TheContent)
        {
            if (TheContent == null)
            {
                CSPRNG.GetBytes(TheBuffer);
            }
            else
            {
                int Index = -1;
                for (int i = 0; i < TheBuffer.Length; ++i)
                {
                    TheBuffer[i] = TheContent[++Index % TheContent.Length];
                }
            }
        }

        private static bool WipeFile(IntPtr FileHandle, byte[] TheContent)
        {
            try
            {
                ulong FileSize = 0;
                uint BytesWritten = 0;
                if (!GetFileSize(FileHandle, out FileSize))
                {
                    return false;
                }
                if (FileSize > 4096)
                {
                    ulong Current = FileSize;
                    ulong Distance = 0;
                    ulong WrittenSize = 0;
                    do
                    {
                        if (Current > 4096)
                        {
                            Current -= 4096;
                            byte[] FileBuffer = new byte[4096];
                            FillBuffer(FileBuffer, TheContent);
                            if (!SetFilePointer(FileHandle, Distance, IntPtr.Zero, FileStart))
                            {
                                return false;
                            }
                            if (!WriteFile(FileHandle, FileBuffer, 4096, ref BytesWritten, IntPtr.Zero))
                            {
                                return false;
                            }
                            if (BytesWritten != 4096)
                            {
                                return false;
                            }
                            Distance += 4096;
                            WrittenSize += 4096;
                        }
                        else
                        {
                            byte[] FileBuffer = new byte[Current];
                            FillBuffer(FileBuffer, TheContent);
                            if (!SetFilePointer(FileHandle, Distance, IntPtr.Zero, FileStart))
                            {
                                return false;
                            }
                            if (!WriteFile(FileHandle, FileBuffer, (uint)Current, ref BytesWritten, IntPtr.Zero))
                            {
                                return false;
                            }
                            if (BytesWritten != (uint)Current)
                            {
                                return false;
                            }
                            WrittenSize += Current;
                            Current = 0;
                        }
                    }
                    while (Current != 0);
                    SetFilePointer(FileHandle, 0, IntPtr.Zero, FileStart);
                    if (FileSize != WrittenSize)
                    {
                        return false;
                    }
                }
                else
                {
                    byte[] FileBuffer = new byte[FileSize];
                    FillBuffer(FileBuffer, TheContent);
                    if (!WriteFile(FileHandle, FileBuffer, (uint)FileSize, ref BytesWritten, IntPtr.Zero))
                    {
                        return false;
                    }
                    if (BytesWritten != (uint)FileSize)
                    {
                        return false;
                    }
                }
                if (!FlushFileBuffers(FileHandle))
                {
                    return false;
                }
                return true;
            }
            catch
            {
                throw new Exception(Marshal.GetLastWin32Error().ToString());
            }
        }
    }
}