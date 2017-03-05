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

namespace UC
{
    public enum StandardHandles
    {
        STD_INPUT = -10,
        STD_OUTPUT = -11,
        STD_ERROR = -12
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

        IntPtr _handle;
        FileAccess _mode;

        public InternalStream(StandardHandles handleType)
        {
            _handle = IntPtr.Zero;
            Open(handleType);
        }

        public void Open(StandardHandles handleType)
        {
            if (handleType == StandardHandles.STD_INPUT)
                _mode = FileAccess.Read;
            else
                _mode = FileAccess.Write;
            _handle = GetStdHandle(handleType);
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
