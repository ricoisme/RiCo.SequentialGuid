using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RiCo.SequentialGuid
{
    public class SequentialGuid
    {
        private static readonly Lazy<SequentialGuid> InstanceField =
            new Lazy<SequentialGuid>(() => new SequentialGuid());

        private const int NumberOfBytes = 6;
        private const int LimitOfAByte = 256;
        private const byte Step = 1;

        private readonly long _maximumSort = (long)Math.Pow(LimitOfAByte, NumberOfBytes);
        private readonly DateTime _StartSequence;
        private readonly DateTime _EndSequence;

        private volatile int _lockFlag;
        private Guid _originalGuid;
        private long _lastSequence;

        public SequentialGuid(DateTime? endSequence = null) : this(Guid.NewGuid(), endSequence)
        {
        }

        public SequentialGuid(Guid initial, DateTime? endSequence = null)
        {
            _originalGuid = initial;
            _StartSequence = new DateTime(2019, 1, 1);
            _EndSequence = endSequence ?? new DateTime(2300, 12, 31);
        }

        public Guid Current
        {
            get
            {
                SpinWait.SpinUntil(() => Interlocked.CompareExchange(ref _lockFlag, 1, 0) == 0);
                //avoid race issues
                var guid = _originalGuid;
                _lockFlag = 0;
                return guid;
            }
        }

        internal static SequentialGuid Instance => InstanceField.Value;

        public static Guid NewGuid() => Instance.Next(DateTime.Now);

        public Guid Next(DateTime now)
        {
            if (now < _StartSequence || now >= _EndSequence)
            {
                throw new ArgumentOutOfRangeException();
            }

            var sequence = GetCurrentSequence(now);

            SpinWait.SpinUntil(() => Interlocked.CompareExchange(ref _lockFlag, 1, 0) == 0);
            //we need to do this to avoid race issues
            if (sequence <= _lastSequence)
            {
                sequence = _lastSequence + Step;
            }
            _lastSequence = sequence;

            var sequenceBytes = GetSequenceBytes(sequence);
            var totalBytes = GetGuidBytes().Concat(sequenceBytes).ToArray();
            var guidForSqlServer = ConvertToSqlServer(new Guid(totalBytes));

            _originalGuid = guidForSqlServer;
            _lockFlag = 0;
            return guidForSqlServer;
        }

        private TimeSpan TotalSize => _EndSequence - _StartSequence;

        private long GetCurrentSequence(DateTime value)
        {
            var ticksNow = value.Ticks - _StartSequence.Ticks;
            var result = ((decimal)ticksNow / TotalSize.Ticks * _maximumSort);
            return (long)result;
        }

        private IEnumerable<byte> GetSequenceBytes(long sequence)
        {
            var sequenceBytes = BitConverter.GetBytes(sequence);
            var sequenceBytesConcat = sequenceBytes.Concat(new byte[NumberOfBytes]);
            return sequenceBytesConcat.Take(NumberOfBytes).Reverse();
        }

        private IEnumerable<byte> GetGuidBytes()
        {
            if (_originalGuid == Guid.Empty)
            {
                _originalGuid = Guid.NewGuid();
            }

            return _originalGuid.ToByteArray().Take(10).ToArray();
        }

        private Guid ConvertToSqlServer(Guid guid)
        {
            var guidBytes = guid.ToByteArray();
            Array.Reverse(guidBytes);
            Array.Reverse(guidBytes, 0, 4);
            Array.Reverse(guidBytes, 4, 2);
            Array.Reverse(guidBytes, 6, 2);
            return new Guid(guidBytes);
        }
    }
}