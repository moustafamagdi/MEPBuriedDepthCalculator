using System;
using System.IO;
using System.Text;

namespace MEPBuriedDepthCalculator.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();
        private bool _debugMode;

        public string LogFilePath => _logFilePath;

        public FileLogger(bool debugMode = false)
        {
            _debugMode = debugMode;
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (string.IsNullOrEmpty(desktopPath))
                {
                    desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
                }
                if (!Directory.Exists(desktopPath))
                {
                    Directory.CreateDirectory(desktopPath);
                }
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(desktopPath, $"MEPBuriedDepthCalculator_{timestamp}.log");
                
                WriteToFile($"[INFO] [Initialization] FileLogger initialized at {_logFilePath}. Version: {Constants.Version}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize FileLogger: {ex.Message}");
                _logFilePath = Path.Combine(Path.GetTempPath(), $"MEPBuriedDepthCalculator_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            }
        }

        public void SetDebugMode(bool debugMode)
        {
            _debugMode = debugMode;
        }

        public void Info(string operation, string message)
        {
            LogMessage("INFO", operation, message);
        }

        public void Debug(string operation, string message)
        {
            if (_debugMode)
            {
                LogMessage("DEBUG", operation, message);
            }
        }

        public void Warning(string operation, string message)
        {
            LogMessage("WARNING", operation, message);
        }

        public void Error(string operation, string message, Exception ex = null, long? elementId = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            if (elementId.HasValue)
            {
                sb.AppendLine($"ElementId: {elementId.Value}");
            }
            if (ex != null)
            {
                sb.AppendLine($"ExceptionType: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
            }
            LogMessage("ERROR", operation, sb.ToString().TrimEnd());
        }

        public void Fatal(string operation, string message, Exception ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            if (ex != null)
            {
                sb.AppendLine($"ExceptionType: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
            }
            LogMessage("FATAL", operation, sb.ToString().TrimEnd());
        }

        private void WriteToFile(string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logging failed: {ex.Message}");
            }
        }

        private void LogMessage(string level, string operation, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{timestamp}] [{level}] [{operation}] {message}";
            WriteToFile(line);
        }
    }
}
