using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;

using ProtoFlux.Core;

namespace ProtoFluxOverhaul
{
	public static partial class RoundedCornersHelper
	{
		public static bool IsReferenceConnector(Slot connectorSlot)
		{
			if (connectorSlot == null) return false;
			// Mirror the reference detection used for texture selection: reference connectors should be treated specially
			var refProxy = connectorSlot.GetComponentInParents<ProtoFluxRefProxy>();
			var referenceProxy = connectorSlot.GetComponentInParents<ProtoFluxReferenceProxy>();
			var globalRefProxy = connectorSlot.GetComponentInParents<ProtoFluxGlobalRefProxy>();
			return refProxy != null || referenceProxy != null || globalRefProxy != null;
		}

		/// <summary>
		/// Gets the original type color from a connector's proxy component.
		/// This returns the color as Resonite computes it: type.GetTypeColor().MulRGB(1.5f)
		/// Returns null if no proxy is found or type is not set.
		/// </summary>
		public static colorX? GetConnectorTypeColor(Slot connectorSlot)
		{
			if (connectorSlot == null) return null;

			// Check for impulse proxy (flow output)
			var impulseProxy = connectorSlot.GetComponent<ProtoFluxImpulseProxy>();
			if (impulseProxy != null)
			{
				return impulseProxy.ImpulseType.Value.GetImpulseColor().MulRGB(1.5f);
			}

			// Check for operation proxy (flow input)
			var operationProxy = connectorSlot.GetComponent<ProtoFluxOperationProxy>();
			if (operationProxy != null)
			{
				return DatatypeColorHelper.GetOperationColor(operationProxy.IsAsync.Value).MulRGB(1.5f);
			}

			// Check for input proxy (value input)
			var inputProxy = connectorSlot.GetComponent<ProtoFluxInputProxy>();
			if (inputProxy != null && inputProxy.InputType.Value != null)
			{
				return inputProxy.InputType.Value.GetTypeColor().MulRGB(1.5f);
			}

			// Check for output proxy (value output)
			var outputProxy = connectorSlot.GetComponent<ProtoFluxOutputProxy>();
			if (outputProxy != null && outputProxy.OutputType.Value != null)
			{
				return outputProxy.OutputType.Value.GetTypeColor().MulRGB(1.5f);
			}

			// Check for reference proxy
			var refProxy = connectorSlot.GetComponent<ProtoFluxReferenceProxy>();
			if (refProxy != null && refProxy.ValueType.Value != null)
			{
				return refProxy.ValueType.Value.GetTypeColor().MulRGB(1.5f);
			}

			return null;
		}

		/// <summary>
		/// Finds the closest matching color field from the palette to the given original color.
		/// Uses the same normalization and matching logic as wire colors.
		/// Includes all palette shades: Neutrals, Hero, Mid, Sub, and Dark.
		/// Uses RadiantUI_Constants for color matching (always available), but returns palette fields for ValueCopy driving.
		/// </summary>
		public static IField<colorX> FindClosestPaletteField(PlatformColorPalette palette, colorX originalColor)
		{
			var (field, _) = FindClosestPaletteFieldWithConstant(palette, originalColor);
			return field;
		}

		/// <summary>
		/// Finds the closest matching color field from the palette to the given original color.
		/// Also returns the matched RadiantUI_Constants color for reliable contrast calculation
		/// (palette field values may not be synced immediately after component attach).
		/// </summary>
		public static (IField<colorX> field, colorX constantColor) FindClosestPaletteFieldWithConstant(PlatformColorPalette palette, colorX originalColor)
		{
			if (palette == null) return (null, originalColor);
			int index = FindClosestPaletteIndex(originalColor);
			return index < 0 ? (null, originalColor) : (GetPaletteField(palette, index), PaletteColors[index]);
		}

		// Match against stable engine colors, then select the live field on this node's palette.
		// Keep this order: equal distances select the first candidate, including across shades.
		private static readonly colorX[] PaletteColors =
		{
			RadiantUI_Constants.Neutrals.DARK,
			RadiantUI_Constants.Neutrals.MID,
			RadiantUI_Constants.Neutrals.MIDLIGHT,
			RadiantUI_Constants.Neutrals.LIGHT,
			RadiantUI_Constants.Hero.YELLOW,
			RadiantUI_Constants.Hero.GREEN,
			RadiantUI_Constants.Hero.RED,
			RadiantUI_Constants.Hero.PURPLE,
			RadiantUI_Constants.Hero.CYAN,
			RadiantUI_Constants.Hero.ORANGE,
			RadiantUI_Constants.MidLight.YELLOW,
			RadiantUI_Constants.MidLight.GREEN,
			RadiantUI_Constants.MidLight.RED,
			RadiantUI_Constants.MidLight.PURPLE,
			RadiantUI_Constants.MidLight.CYAN,
			RadiantUI_Constants.MidLight.ORANGE,
			RadiantUI_Constants.Sub.YELLOW,
			RadiantUI_Constants.Sub.GREEN,
			RadiantUI_Constants.Sub.RED,
			RadiantUI_Constants.Sub.PURPLE,
			RadiantUI_Constants.Sub.CYAN,
			RadiantUI_Constants.Sub.ORANGE,
			RadiantUI_Constants.Dark.YELLOW,
			RadiantUI_Constants.Dark.GREEN,
			RadiantUI_Constants.Dark.RED,
			RadiantUI_Constants.Dark.PURPLE,
			RadiantUI_Constants.Dark.CYAN,
			RadiantUI_Constants.Dark.ORANGE,
		};

		private static int FindClosestPaletteIndex(colorX originalColor)
		{
			float maxChannel = MathX.Max(originalColor.r, MathX.Max(originalColor.g, originalColor.b));
			colorX normalizedOriginal = maxChannel > 1f
				? new colorX(originalColor.r / maxChannel, originalColor.g / maxChannel, originalColor.b / maxChannel, originalColor.a)
				: originalColor;

			int closestIndex = -1;
			float closestDistSq = float.MaxValue;
			for (int i = 0; i < PaletteColors.Length; i++)
			{
				float3 difference = normalizedOriginal.rgb - PaletteColors[i].rgb;
				float distanceSq = difference.x * difference.x + difference.y * difference.y + difference.z * difference.z;
				if (distanceSq < closestDistSq)
				{
					closestDistSq = distanceSq;
					closestIndex = i;
				}
			}
			return closestIndex;
		}

		private static IField<colorX> GetPaletteField(PlatformColorPalette palette, int index) => index switch
		{
			0 => palette.Neutrals.Dark,
			1 => palette.Neutrals.Mid,
			2 => palette.Neutrals.MidLight,
			3 => palette.Neutrals.Light,
			4 => palette.Hero.Yellow,
			5 => palette.Hero.Green,
			6 => palette.Hero.Red,
			7 => palette.Hero.Purple,
			8 => palette.Hero.Cyan,
			9 => palette.Hero.Orange,
			10 => palette.Mid.Yellow,
			11 => palette.Mid.Green,
			12 => palette.Mid.Red,
			13 => palette.Mid.Purple,
			14 => palette.Mid.Cyan,
			15 => palette.Mid.Orange,
			16 => palette.Sub.Yellow,
			17 => palette.Sub.Green,
			18 => palette.Sub.Red,
			19 => palette.Sub.Purple,
			20 => palette.Sub.Cyan,
			21 => palette.Sub.Orange,
			22 => palette.Dark.Yellow,
			23 => palette.Dark.Green,
			24 => palette.Dark.Red,
			25 => palette.Dark.Purple,
			26 => palette.Dark.Cyan,
			27 => palette.Dark.Orange,
			_ => null
		};

		public static IField<colorX> GetConnectorTintSource(PlatformColorPalette palette, bool isOutput, ImpulseType? impulseType, bool isOperation, bool isAsync, bool isReference, colorX? originalColor = null)
		{
			if (palette == null) return null;

			// If original color is provided, use closest match (consistent with wire coloring)
			if (originalColor.HasValue)
			{
				return FindClosestPaletteField(palette, originalColor.Value);
			}

			// Fallback to hardcoded mapping when original color is not available
			if (isReference)
				return palette.Hero.Purple;

			// Flow connectors (impulse/operation) share the same sprite family; we differentiate color slightly
			if (impulseType.HasValue)
				return palette.Hero.Yellow;

			if (isOperation)
				return isAsync ? palette.Hero.Green : palette.Hero.Purple;

			return isOutput ? palette.Hero.Cyan : palette.Hero.Orange;
		}

		/// <summary>
		/// Finds the closest matching Sub color field from the palette to the given original color.
		/// Sub colors are darker versions used for label backgrounds.
		/// Compares against all palette shades and returns the corresponding Sub color.
		/// Uses RadiantUI_Constants for color matching (always available), but returns palette fields for ValueCopy driving.
		/// </summary>
		public static IField<colorX> FindClosestSubPaletteField(PlatformColorPalette palette, colorX originalColor)
		{
			if (palette == null) return null;
			int index = FindClosestPaletteIndex(originalColor);
			if (index < 0) return palette.Sub.Cyan;
			if (index < 4) return palette.Neutrals.Mid;
			return ((index - 4) % 6) switch
			{
				0 => palette.Sub.Yellow,
				1 => palette.Sub.Green,
				2 => palette.Sub.Red,
				3 => palette.Sub.Purple,
				4 => palette.Sub.Cyan,
				5 => palette.Sub.Orange,
				_ => palette.Sub.Cyan
			};
		}

		public static IField<colorX> GetLabelBackgroundTintSource(PlatformColorPalette palette, bool isOutput, ImpulseType? impulseType, bool isOperation, bool isAsync, bool isReference, colorX? originalColor = null)
		{
			if (palette == null) return null;

			// If original color is provided, use closest match (consistent with connector coloring)
			if (originalColor.HasValue)
			{
				return FindClosestSubPaletteField(palette, originalColor.Value);
			}

			// Fallback to hardcoded mapping when original color is not available
			if (isReference)
				return palette.Sub.Purple;

			if (impulseType.HasValue)
				return palette.Sub.Yellow;

			if (isOperation)
				return isAsync ? palette.Sub.Green : palette.Sub.Purple;

			return isOutput ? palette.Sub.Cyan : palette.Sub.Orange;
		}

		public static IField<colorX> GetLabelTextTintSource(PlatformColorPalette palette)
		{
			if (palette == null) return null;
			return palette.Neutrals.Light;
		}
	}
}

