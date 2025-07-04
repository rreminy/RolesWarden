using System;
using System.Threading;

namespace RolesWarden.Utilities
{
    public static class TimeService
    {
        private static long s_lastUpdated;
        private static long s_timestamp;

        public static long Now
        {
            get
            {
                UpdateTimestamp();
                return Volatile.Read(ref s_timestamp);
            }
        }

        private static long GetTimestampCore()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static void UpdateTimestamp()
        {
            var lastTicks = Volatile.Read(ref s_lastUpdated);
            var currentTicks = Environment.TickCount64;
            if (lastTicks != currentTicks)
            {
                var lastTimestamp = Volatile.Read(ref s_timestamp);
                var currentTimestamp = GetTimestampCore();
                if (Interlocked.CompareExchange(ref s_timestamp, currentTimestamp, lastTimestamp) == lastTimestamp) Volatile.Write(ref s_lastUpdated, currentTicks);
            }
        }
    }
}
