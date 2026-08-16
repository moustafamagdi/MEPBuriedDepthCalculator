using System;

namespace MEPBuriedDepthCalculator.Logging
{
    public interface ILogger
    {
        void Info(string operation, string message);
        void Debug(string operation, string message);
        void Warning(string operation, string message);
        void Error(string operation, string message, Exception ex = null, long? elementId = null);
        void Fatal(string operation, string message, Exception ex = null);
        string LogFilePath { get; }
    }
}
