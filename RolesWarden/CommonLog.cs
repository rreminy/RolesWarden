using Microsoft.Extensions.Logging;

namespace RolesWarden
{
    public static class CommonLog
    {
        public static void LogConstructing(ILogger logger, object obj)
        {
            logger.LogInformation("Constructing {type}", obj.GetType().Name);
        }

        public static void LogConstructed(ILogger logger, object obj)
        {
            logger.LogInformation("Constructed {type}", obj.GetType().Name);
        }

        public static void LogStarting(ILogger logger, object obj)
        {
            logger.LogInformation("Starting {type}", obj.GetType().Name);
        }

        public static void LogStarted(ILogger logger, object obj)
        {
            logger.LogInformation("Started {type}", obj.GetType().Name);
        }

        public static void LogStopping(ILogger logger, object obj)
        {
            logger.LogInformation("Stopping {type}", obj.GetType().Name);
        }

        public static void LogStopped(ILogger logger, object obj)
        {
            logger.LogInformation("Stopped {type}", obj.GetType().Name);
        }

        public static void LogDisposing(ILogger logger, object obj)
        {
            logger.LogInformation("Disposing {type}", obj.GetType().Name);
        }

        public static void LogDisposed(ILogger logger, object obj)
        {
            logger.LogInformation("Disposed {type}", obj.GetType().Name);
        }
    }
}
