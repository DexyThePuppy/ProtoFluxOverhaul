using System.Runtime.CompilerServices;

namespace ProtoFluxOverhaul;

/// <summary>Skips interpolation and argument evaluation when debug logging is disabled.</summary>
[InterpolatedStringHandler]
public ref struct DebugLogMessage
{
	private DefaultInterpolatedStringHandler builder;
	internal bool Enabled { get; }

	public DebugLogMessage(int literalLength, int formattedCount, out bool shouldAppend)
	{
		Enabled = shouldAppend = Logger.IsDebugEnabled;
		builder = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
	}

	public void AppendLiteral(string value) => builder.AppendLiteral(value);
	public void AppendFormatted<T>(T value) => builder.AppendFormatted(value);
	public void AppendFormatted<T>(T value, string format) => builder.AppendFormatted(value, format);
	public void AppendFormatted<T>(T value, int alignment) => builder.AppendFormatted(value, alignment);
	public void AppendFormatted<T>(T value, int alignment, string format) => builder.AppendFormatted(value, alignment, format);
	internal string GetFormattedText() => builder.ToStringAndClear();
}
