using System;
using ResoniteModLoader;

namespace ProtoFluxOverhaul;

/// <summary>Category logging with independent debug and permission verbosity.</summary>
public static class Logger
{
	public enum LogLevel { Debug, Info, Warning, Error }
	public enum LogCategory { General, Audio, UI, Permission, Wire, Node }

	internal static bool IsDebugEnabled => ProtoFluxOverhaul.Config?.GetValue(ProtoFluxOverhaul.DEBUG_LOGGING) == true;
	internal static bool IsPermissionLoggingEnabled => ProtoFluxOverhaul.Config?.GetValue(ProtoFluxOverhaul.VERBOSE_PERMISSION_LOGGING) == true;

	public static void DebugLog(string message, LogLevel level = LogLevel.Debug, LogCategory category = LogCategory.General)
	{
		if (!IsDebugEnabled) return;
		string text = $"[{category}] {message}";
		switch (level)
		{
			case LogLevel.Debug: ResoniteMod.Debug(text); break;
			case LogLevel.Info: ResoniteMod.Msg(text); break;
			case LogLevel.Warning: ResoniteMod.Warn(text); break;
			case LogLevel.Error: ResoniteMod.Error(text); break;
		}
	}

	public static void DebugLog(ref DebugLogMessage message, LogLevel level = LogLevel.Debug, LogCategory category = LogCategory.General)
	{
		if (message.Enabled) DebugLog(message.GetFormattedText(), level, category);
	}

	public static void LogWarning(string message, LogCategory category = LogCategory.General) =>
		DebugLog(message, LogLevel.Warning, category);

	public static void LogError(string message, Exception exception, LogCategory category = LogCategory.General)
	{
		string details = exception == null ? message
			: $"{message}\nException: {exception.Message}\nStack Trace: {exception.StackTrace}";
		ResoniteMod.Error($"[{category}] {details}");
	}

	public static void LogPermission(string context, bool result, string details)
	{
		if (IsPermissionLoggingEnabled)
			ResoniteMod.Debug($"[{LogCategory.Permission}] Permission check ({context}): {(result ? "Granted" : "Denied")}\nDetails: {details}");
	}

	public static void LogPermissionException(string message, Exception exception)
	{
		ResoniteMod.Warn($"[ProtoFluxOverhaul][{LogCategory.Permission}] {message}{(exception == null ? "" : ": " + exception.Message)}");
		if (IsDebugEnabled && exception != null)
			ResoniteMod.Error($"[ProtoFluxOverhaul][{LogCategory.Permission}] {exception}");
	}

	public static void LogAudio(string operation, string details) => LogOperation(LogCategory.Audio, operation, details);
	public static void LogUI(string operation, string details) => LogOperation(LogCategory.UI, operation, details);
	public static void LogWire(string operation, string details) => LogOperation(LogCategory.Wire, operation, details);
	public static void LogNode(string operation, string details) => LogOperation(LogCategory.Node, operation, details);

	public static void LogAudio(string operation, ref DebugLogMessage details) => LogOperation(LogCategory.Audio, operation, ref details);
	public static void LogUI(string operation, ref DebugLogMessage details) => LogOperation(LogCategory.UI, operation, ref details);
	public static void LogWire(string operation, ref DebugLogMessage details) => LogOperation(LogCategory.Wire, operation, ref details);
	public static void LogNode(string operation, ref DebugLogMessage details) => LogOperation(LogCategory.Node, operation, ref details);

	private static void LogOperation(LogCategory category, string operation, string details)
	{
		if (IsDebugEnabled)
			ResoniteMod.Debug($"[{category}] {category} operation ({operation}): {details}");
	}

	private static void LogOperation(LogCategory category, string operation, ref DebugLogMessage details)
	{
		if (details.Enabled) LogOperation(category, operation, details.GetFormattedText());
	}
}
