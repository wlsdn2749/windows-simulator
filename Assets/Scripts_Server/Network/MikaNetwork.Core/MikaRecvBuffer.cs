using System;

namespace MikaNetwork
{
    public class MikaRecvBuffer
    {
        private byte[] _buffer;
        private int _readOffset;
        private int _writeOffset;

        public int ReadableBytes => _writeOffset - _readOffset;
        public int WritableBytes => _buffer.Length - _writeOffset;


        public MikaRecvBuffer(int size = 64 * 1024)
        {
            _buffer = new byte[size]; // 나중에 MemoryPool?
        }

        public Memory<byte> GetWritableMemory(int sizeHint)
        {
            EnsureWritableSpace(sizeHint);
            return _buffer.AsMemory(_writeOffset, sizeHint); // (sizeHint)만큼 넘겨도 상관없음
        }

        public ReadOnlySpan<byte> GetReadableSpan()
        {
            return _buffer.AsSpan(_readOffset, ReadableBytes);
        }

        public void AdvanceRead(int count)
        {
            _readOffset += count;

            if (_readOffset >= _writeOffset)
            {
                _readOffset = 0;
                _writeOffset = 0;
            }
        }

        public void AdvanceWrite(int count)
        {
            _writeOffset += count;
        }

        private void EnsureWritableSpace(int required)
        {
            if (WritableBytes >= required)
                return;

            int readableBytes = ReadableBytes;
            int requiredBufferSize = readableBytes + required; // 남아있는 사이즈 + 들어가야할 Max 사이즈
            int newSize = _buffer.Length;

            while (newSize < requiredBufferSize)
            {
                newSize *= 2;
            }

            byte[] newBuffer = new byte[newSize];
            Buffer.BlockCopy(_buffer, _readOffset, newBuffer, 0, readableBytes);

            _buffer = newBuffer;
            _readOffset = 0;
            _writeOffset = readableBytes;

        }

    }
}
