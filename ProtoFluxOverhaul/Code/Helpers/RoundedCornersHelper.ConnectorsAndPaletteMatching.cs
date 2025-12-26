using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using ProtoFlux.Core;
using static ProtoFluxOverhaul.Logger;

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

			// Normalize the original color if any channel exceeds 1.0 (due to MulRGB(1.5f) in wire colors)
			float maxChannel = MathX.Max(originalColor.r, MathX.Max(originalColor.g, originalColor.b));
			colorX normalizedOriginal = maxChannel > 1f
				? new colorX(originalColor.r / maxChannel, originalColor.g / maxChannel, originalColor.b / maxChannel, originalColor.a)
				: originalColor;

			// Build list of candidate colors using RadiantUI_Constants for matching (always available)
			// and palette fields for ValueCopy driving (to support per-node customization)
			var candidates = new (colorX color, IField<colorX> field)[]
			{
				// Neutrals - use RadiantUI_Constants for color matching
				(RadiantUI_Constants.Neutrals.DARK, palette.Neutrals.Dark),
				(RadiantUI_Constants.Neutrals.MID, palette.Neutrals.Mid),
				(RadiantUI_Constants.Neutrals.MIDLIGHT, palette.Neutrals.MidLight),
				(RadiantUI_Constants.Neutrals.LIGHT, palette.Neutrals.Light),
				// Hero colors (brightest)
				(RadiantUI_Constants.Hero.YELLOW, palette.Hero.Yellow),
				(RadiantUI_Constants.Hero.GREEN, palette.Hero.Green),
				(RadiantUI_Constants.Hero.RED, palette.Hero.Red),
				(RadiantUI_Constants.Hero.PURPLE, palette.Hero.Purple),
				(RadiantUI_Constants.Hero.CYAN, palette.Hero.Cyan),
				(RadiantUI_Constants.Hero.ORANGE, palette.Hero.Orange),
				// Mid colors
				(RadiantUI_Constants.MidLight.YELLOW, palette.Mid.Yellow),
				(RadiantUI_Constants.MidLight.GREEN, palette.Mid.Green),
				(RadiantUI_Constants.MidLight.RED, palette.Mid.Red),
				(RadiantUI_Constants.MidLight.PURPLE, palette.Mid.Purple),
				(RadiantUI_Constants.MidLight.CYAN, palette.Mid.Cyan),
				(RadiantUI_Constants.MidLight.ORANGE, palette.Mid.Orange),
				// Sub colors
				(RadiantUI_Constants.Sub.YELLOW, palette.Sub.Yellow),
				(RadiantUI_Constants.Sub.GREEN, palette.Sub.Green),
				(RadiantUI_Constants.Sub.RED, palette.Sub.Red),
				(RadiantUI_Constants.Sub.PURPLE, palette.Sub.Purple),
				(RadiantUI_Constants.Sub.CYAN, palette.Sub.Cyan),
				(RadiantUI_Constants.Sub.ORANGE, palette.Sub.Orange),
				// Dark colors
				(RadiantUI_Constants.Dark.YELLOW, palette.Dark.Yellow),
				(RadiantUI_Constants.Dark.GREEN, palette.Dark.Green),
				(RadiantUI_Constants.Dark.RED, palette.Dark.Red),
				(RadiantUI_Constants.Dark.PURPLE, palette.Dark.Purple),
				(RadiantUI_Constants.Dark.CYAN, palette.Dark.Cyan),
				(RadiantUI_Constants.Dark.ORANGE, palette.Dark.Orange),
			};

			IField<colorX> closestField = null;
			colorX closestConstant = originalColor;
			float closestDistSq = float.MaxValue;

			foreach (var (candidateColor, field) in candidates)
			{
				float3 d = normalizedOriginal.rgb - candidateColor.rgb;
				float distSq = d.x * d.x + d.y * d.y + d.z * d.z;
				if (distSq < closestDistSq)
				{
					closestDistSq = distSq;
					closestField = field;
					closestConstant = candidateColor;
				}
			}

			return (closestField, closestConstant);
		}

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

			// Normalize the original color if any channel exceeds 1.0
			float maxChannel = MathX.Max(originalColor.r, MathX.Max(originalColor.g, originalColor.b));
			colorX normalizedOriginal = maxChannel > 1f
				? new colorX(originalColor.r / maxChannel, originalColor.g / maxChannel, originalColor.b / maxChannel, originalColor.a)
				: originalColor;

			// Build list of candidate colors from all palette shades using RadiantUI_Constants for matching
			// Compare against all shades to find the color family, then return corresponding Sub version from palette
			var candidates = new (colorX color, IField<colorX> subField)[]
			{
				// Neutrals -> use Mid neutral for Sub equivalent
				(RadiantUI_Constants.Neutrals.DARK, palette.Neutrals.Mid),
				(RadiantUI_Constants.Neutrals.MID, palette.Neutrals.Mid),
				(RadiantUI_Constants.Neutrals.MIDLIGHT, palette.Neutrals.Mid),
				(RadiantUI_Constants.Neutrals.LIGHT, palette.Neutrals.Mid),
				// Hero colors -> Sub versions
				(RadiantUI_Constants.Hero.YELLOW, palette.Sub.Yellow),
				(RadiantUI_Constants.Hero.GREEN, palette.Sub.Green),
				(RadiantUI_Constants.Hero.RED, palette.Sub.Red),
				(RadiantUI_Constants.Hero.PURPLE, palette.Sub.Purple),
				(RadiantUI_Constants.Hero.CYAN, palette.Sub.Cyan),
				(RadiantUI_Constants.Hero.ORANGE, palette.Sub.Orange),
				// Mid colors -> Sub versions
				(RadiantUI_Constants.MidLight.YELLOW, palette.Sub.Yellow),
				(RadiantUI_Constants.MidLight.GREEN, palette.Sub.Green),
				(RadiantUI_Constants.MidLight.RED, palette.Sub.Red),
				(RadiantUI_Constants.MidLight.PURPLE, palette.Sub.Purple),
				(RadiantUI_Constants.MidLight.CYAN, palette.Sub.Cyan),
				(RadiantUI_Constants.MidLight.ORANGE, palette.Sub.Orange),
				// Sub colors -> same Sub versions
				(RadiantUI_Constants.Sub.YELLOW, palette.Sub.Yellow),
				(RadiantUI_Constants.Sub.GREEN, palette.Sub.Green),
				(RadiantUI_Constants.Sub.RED, palette.Sub.Red),
				(RadiantUI_Constants.Sub.PURPLE, palette.Sub.Purple),
				(RadiantUI_Constants.Sub.CYAN, palette.Sub.Cyan),
				(RadiantUI_Constants.Sub.ORANGE, palette.Sub.Orange),
				// Dark colors -> Sub versions (slightly brighter than dark)
				(RadiantUI_Constants.Dark.YELLOW, palette.Sub.Yellow),
				(RadiantUI_Constants.Dark.GREEN, palette.Sub.Green),
				(RadiantUI_Constants.Dark.RED, palette.Sub.Red),
				(RadiantUI_Constants.Dark.PURPLE, palette.Sub.Purple),
				(RadiantUI_Constants.Dark.CYAN, palette.Sub.Cyan),
				(RadiantUI_Constants.Dark.ORANGE, palette.Sub.Orange),
			};

			IField<colorX> closestField = palette.Sub.Cyan; // Default
			float closestDistSq = float.MaxValue;

			foreach (var (color, subField) in candidates)
			{
				float3 d = normalizedOriginal.rgb - color.rgb;
				float distSq = d.x * d.x + d.y * d.y + d.z * d.z;
				if (distSq < closestDistSq)
				{
					closestDistSq = distSq;
					closestField = subField;
				}
			}

			return closestField;
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

