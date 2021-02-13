// ByteFX.Data data access components for .Net
// Copyright (C) 2002-2003  ByteFX, Inc.
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Console;

namespace UC
{
    public enum StandardHandles : int
    {
        STD_INPUT = -10,
        STD_OUTPUT = -11,
        STD_ERROR = -12,
        STD_CONIN = -999,
        STD_CONOUT = -998,
    }

    /// <summary>
    /// Summary description for API.
    /// </summary>
    public class InternalStream : Stream
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr handle, byte[] buffer, uint toRead, ref uint read, IntPtr lpOverLapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint count, ref uint written, IntPtr lpOverlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(IntPtr handle);
        [DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetStdHandle(StandardHandles handle);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetStdHandle(StandardHandles nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] EFileAccess access,
            [MarshalAs(UnmanagedType.U4)] EFileShare share,
            IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
            [MarshalAs(UnmanagedType.U4)] ECreationDisposition creationDisposition,
            [MarshalAs(UnmanagedType.U4)] EFileAttributes flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [Flags]
        enum EFileAccess : uint
        {
            //
            // Standart Section
            //

            AccessSystemSecurity = 0x1000000,   // AccessSystemAcl access type
            MaximumAllowed = 0x2000000,     // MaximumAllowed access type

            Delete = 0x10000,
            ReadControl = 0x20000,
            WriteDAC = 0x40000,
            WriteOwner = 0x80000,
            Synchronize = 0x100000,

            StandardRightsRequired = 0xF0000,
            StandardRightsRead = ReadControl,
            StandardRightsWrite = ReadControl,
            StandardRightsExecute = ReadControl,
            StandardRightsAll = 0x1F0000,
            SpecificRightsAll = 0xFFFF,

            FILE_READ_DATA = 0x0001,        // file & pipe
            FILE_LIST_DIRECTORY = 0x0001,       // directory
            FILE_WRITE_DATA = 0x0002,       // file & pipe
            FILE_ADD_FILE = 0x0002,         // directory
            FILE_APPEND_DATA = 0x0004,      // file
            FILE_ADD_SUBDIRECTORY = 0x0004,     // directory
            FILE_CREATE_PIPE_INSTANCE = 0x0004, // named pipe
            FILE_READ_EA = 0x0008,          // file & directory
            FILE_WRITE_EA = 0x0010,         // file & directory
            FILE_EXECUTE = 0x0020,          // file
            FILE_TRAVERSE = 0x0020,         // directory
            FILE_DELETE_CHILD = 0x0040,     // directory
            FILE_READ_ATTRIBUTES = 0x0080,      // all
            FILE_WRITE_ATTRIBUTES = 0x0100,     // all

            //
            // Generic Section
            //

            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,

            SPECIFIC_RIGHTS_ALL = 0x00FFFF,
            FILE_ALL_ACCESS =
            StandardRightsRequired |
            Synchronize |
            0x1FF,

            FILE_GENERIC_READ =
            StandardRightsRead |
            FILE_READ_DATA |
            FILE_READ_ATTRIBUTES |
            FILE_READ_EA |
            Synchronize,

            FILE_GENERIC_WRITE =
            StandardRightsWrite |
            FILE_WRITE_DATA |
            FILE_WRITE_ATTRIBUTES |
            FILE_WRITE_EA |
            FILE_APPEND_DATA |
            Synchronize,

            FILE_GENERIC_EXECUTE =
            StandardRightsExecute |
              FILE_READ_ATTRIBUTES |
              FILE_EXECUTE |
              Synchronize
        }

        [Flags]
        private enum EFileShare : uint
        {
            /// <summary>
            ///
            /// </summary>
            None = 0x00000000,
            /// <summary>
            /// Enables subsequent open operations on an object to request read access.
            /// Otherwise, other processes cannot open the object if they request read access.
            /// If this flag is not specified, but the object has been opened for read access, the function fails.
            /// </summary>
            Read = 0x00000001,
            /// <summary>
            /// Enables subsequent open operations on an object to request write access.
            /// Otherwise, other processes cannot open the object if they request write access.
            /// If this flag is not specified, but the object has been opened for write access, the function fails.
            /// </summary>
            Write = 0x00000002,
            /// <summary>
            /// Enables subsequent open operations on an object to request delete access.
            /// Otherwise, other processes cannot open the object if they request delete access.
            /// If this flag is not specified, but the object has been opened for delete access, the function fails.
            /// </summary>
            Delete = 0x00000004
        }

        private enum ECreationDisposition : uint
        {
            /// <summary>
            /// Creates a new file. The function fails if a specified file exists.
            /// </summary>
            New = 1,
            /// <summary>
            /// Creates a new file, always.
            /// If a file exists, the function overwrites the file, clears the existing attributes, combines the specified file attributes,
            /// and flags with FILE_ATTRIBUTE_ARCHIVE, but does not set the security descriptor that the SECURITY_ATTRIBUTES structure specifies.
            /// </summary>
            CreateAlways = 2,
            /// <summary>
            /// Opens a file. The function fails if the file does not exist.
            /// </summary>
            OpenExisting = 3,
            /// <summary>
            /// Opens a file, always.
            /// If a file does not exist, the function creates a file as if dwCreationDisposition is CREATE_NEW.
            /// </summary>
            OpenAlways = 4,
            /// <summary>
            /// Opens a file and truncates it so that its size is 0 (zero) bytes. The function fails if the file does not exist.
            /// The calling process must open the file with the GENERIC_WRITE access right.
            /// </summary>
            TruncateExisting = 5
        }

        [Flags]
        private enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out ConsoleModes lpMode);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, ConsoleModes dwMode);

        [Flags]
        private enum ConsoleModes : uint
        {
            ENABLE_PROCESSED_INPUT = 0x0001,
            ENABLE_LINE_INPUT = 0x0002,
            ENABLE_ECHO_INPUT = 0x0004,
            ENABLE_WINDOW_INPUT = 0x0008,
            ENABLE_MOUSE_INPUT = 0x0010,
            ENABLE_INSERT_MODE = 0x0020,
            ENABLE_QUICK_EDIT_MODE = 0x0040,
            ENABLE_EXTENDED_FLAGS = 0x0080,
            ENABLE_AUTO_POSITION = 0x0100,

            ENABLE_PROCESSED_OUTPUT = 0x0001,
            ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002,
            ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004,
            DISABLE_NEWLINE_AUTO_RETURN = 0x0008,
            ENABLE_LVB_GRID_WORLDWIDE = 0x0010,
            ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200,
        }


        IntPtr _handle;
        FileAccess _mode;

        public InternalStream(StandardHandles handleType)
        {
            _handle = IntPtr.Zero;
            Open(handleType);
        }

        public IntPtr Handle => _handle;

        public override string ToString()
        {
            return $"Handle: {this.Handle.ToInt64():X8}";
        }

        public void Open(StandardHandles handleType)
        {
            if (handleType == StandardHandles.STD_CONIN)
            {
                _mode = FileAccess.Read;
                
                try
                {
                    _handle = GetStdHandle(StandardHandles.STD_INPUT);
                    //_handle = CreateFile("CONIN$", EFileAccess.FILE_GENERIC_READ,
                    //    EFileShare.Read, IntPtr.Zero, ECreationDisposition.OpenExisting, 0, IntPtr.Zero);
                }
                catch
                {
                    _handle = GetStdHandle(StandardHandles.STD_INPUT);
                }

            }
            else if (handleType == StandardHandles.STD_CONOUT)
            {
                _mode = FileAccess.Write;
                try
                {
                    _handle = GetStdHandle(StandardHandles.STD_OUTPUT);
                    //if (GetConsoleWindow() != IntPtr.Zero)
                    //    _handle = CreateFile("CONOUT$", EFileAccess.FILE_GENERIC_READ | EFileAccess.FILE_GENERIC_WRITE,
                    //        EFileShare.Write, IntPtr.Zero, ECreationDisposition.OpenExisting, 0, IntPtr.Zero);
                    //else
                    //SetStdHandle(StandardHandles.STD_OUTPUT, _handle);
                    //_handle = GetStdHandle(StandardHandles.STD_OUTPUT);
                    //if (GetConsoleMode(_handle, out var cMode))
                    //SetConsoleMode(_handle, cMode | ConsoleModes.ENABLE_VIRTUAL_TERMINAL_INPUT);
                }
                catch
                {
                    _handle = GetStdHandle(StandardHandles.STD_OUTPUT);
                }
            }
            else
            {
                if (handleType == StandardHandles.STD_INPUT)
                    _mode = FileAccess.Read;
                else
                    _mode = FileAccess.Write;
                _handle = GetStdHandle(handleType);
            }
        }
        
        public override bool CanRead
        {
            get { return (_mode & FileAccess.Read) > 0; }
        }

        public override bool CanWrite
        {
            get { return (_mode & FileAccess.Write) > 0; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException("InternalStream does not support seeking"); }
        }

        public override long Position
        {
            get { throw new NotSupportedException("InternalStream does not support seeking"); }
            set { }
        }

        public override void Flush()
        {
            if (_handle == IntPtr.Zero)
                throw new ObjectDisposedException("InternalStream", "The stream has already been closed");
            FlushFileBuffers(_handle);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", "The buffer to read into cannot be null");
            if (buffer.Length < (offset + count))
                throw new ArgumentException("Buffer is not large enough to hold requested data", "buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", offset, "Offset cannot be negative");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative");
            if (!CanRead)
                throw new NotSupportedException("The stream does not support reading");
            if (_handle == IntPtr.Zero)
                throw new ObjectDisposedException("InternalStream", "The stream has already been closed");

            // first read the data into an internal buffer since ReadFile cannot read into a buf at
            // a specified offset
            uint read = 0;
            byte[] buf = new Byte[count];
            ReadFile(_handle, buf, (uint)count, ref read, IntPtr.Zero);

            for (int x = 0; x < read; x++)
            {
                buffer[offset + x] = buf[x];
            }
            return (int)read;
        }

        public override void Close()
        {
            //CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }

        public override void SetLength(long length)
        {
            throw new NotSupportedException("InternalStream doesn't support SetLength");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer", "The buffer to write into cannot be null");
            if (buffer.Length < (offset + count))
                throw new ArgumentException("Buffer does not contain amount of requested data", "buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", offset, "Offset cannot be negative");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative");
            if (!CanWrite)
                throw new NotSupportedException("The stream does not support writing");
            if (_handle == IntPtr.Zero)
                throw new ObjectDisposedException("InternalStream", "The stream has already been closed");

            // copy data to internal buffer to allow writing from a specified offset
            byte[] buf = new Byte[count];
            for (int x = 0; x < count; x++)
            {
                buf[x] = buffer[offset + x];
            }
            uint written = 0;
            bool result = WriteFile(_handle, buf, (uint)count, ref written, IntPtr.Zero);
            if (!result)
                Engine.DebugLog($"InternalStream Write Fail: {this} {count}");

            if (!result)
                throw new IOException("Writing to the stream failed");
            if (written < count)
                throw new IOException("Unable to write entire buffer to stream");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("InternalStream doesn't support seeking");
        }
    }
}
