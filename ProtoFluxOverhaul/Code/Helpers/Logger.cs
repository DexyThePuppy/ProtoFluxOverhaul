using System;
using ResoniteModLoader;

namespace ProtoFluxOverhaul
{
    /// <summary>
    /// Handles logging for ProtoFluxOverhaul with configurable debug levels
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Log levels for different types of messages
        /// </summary>
        public enum LogLevel
        {
            Debug,      // Detailed information for debugging
            Info,       // General information about normal operation
            Warning,    // Potential issues that don't affect core functionality
            Error       // Critical issues that affect functionality
        }

        /// <summary>
        /// Log categories for different parts of the mod
        /// </summary>
        public enum LogCategory
        {
            General,    // General mod operations
            Audio,      // Audio-related operations
            UI,         // UI-related operations
            Permission, // Permission checks
            Wire,       // Wire-related operations
            Node        // Node-related operations
        }

        /// <summary>
        /// Logs a debug message if debug logging is enabled
        /// </summary>
        public static void DebugLog(string message, LogLevel level = LogLevel.Debug, LogCategory category = LogCategory.General)
        {
            if (!ProtoFluxOverhaul.Config?.GetValue(ProtoFluxOverhaul.DEBUG_LOGGING) ?? false)
                return;

            string prefix = $"[{category}] ";
            
            switch (level)
            {
                case LogLevel.Debug:
                    ResoniteMod.Debug(prefix + message);
                    break;
                case LogLevel.Info:
                    ResoniteMod.Msg(prefix + message);
                    break;
                case LogLevel.Warning:
                    ResoniteMod.Warn(prefix + message);
                    break;
                case LogLevel.Error:
                    ResoniteMod.Error(prefix + message);
                    break;
            }
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message, LogCategory category = LogCategory.General)
        {
            DebugLog(message, LogLevel.Warning, category);
        }

        /// <summary>
        /// Logs an error message with exception details
        /// </summary>
        public static void LogError(string message, Exception e, LogCategory category = LogCategory.General)
        {
            string errorMessage = e != null 
                ? $"{message}\nException: {e.Message}\nStack Trace: {e.StackTrace}"
                : message;
            DebugLog(errorMessage, LogLevel.Error, category);
        }

        /// <summary>
        /// Logs a permission check result
        /// </summary>
        public static void LogPermission(string context, bool result, string details)
        {
            DebugLog(
                $"Permission check ({context}): {(result ? "Granted" : "Denied")}\nDetails: {details}",
                LogLevel.Debug,
                LogCategory.Permission
            );
        }

        /// <summary>
        /// Logs an audio-related operation
        /// </summary>
        public static void LogAudio(string operation, string details)
        {
            DebugLog(
                $"Audio operation ({operation}): {details}",
                LogLevel.Debug,
                LogCategory.Audio
            );
        }

        /// <summary>
        /// Logs a UI-related operation
        /// </summary>
        public static void LogUI(string operation, string details)
        {
            DebugLog(
                $"UI operation ({operation}): {details}",
                LogLevel.Debug,
                LogCategory.UI
            );
        }

        /// <summary>
        /// Logs a wire-related operation
        /// </summary>
        public static void LogWire(string operation, string details)
        {
            DebugLog(
                $"Wire operation ({operation}): {details}",
                LogLevel.Debug,
                LogCategory.Wire
            );
        }

        /// <summary>
        /// Logs a node-related operation
        /// </summary>
        public static void LogNode(string operation, string details)
        {
            DebugLog(
                $"Node operation ({operation}): {details}",
                LogLevel.Debug,
                LogCategory.Node
            );
        }
    }
}
