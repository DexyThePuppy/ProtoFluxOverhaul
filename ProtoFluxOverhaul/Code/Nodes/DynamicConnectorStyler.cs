using System;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using ProtoFlux.Core;

namespace ProtoFluxOverhaul
{
	/// <summary>
	/// Shared implementation for styling dynamically-created connector elements
	/// (e.g. connectors created after initial node BuildUI).
	/// Keeps Harmony patches minimal and behavior consistent.
	/// </summary>
	internal static class DynamicConnectorStyler
	{
		internal static void StyleConnectorImage(
			ProtoFluxNodeVisual instance,
			Image connectorImage,
			bool? isOutputOverride = null,
			ImpulseType? impulseTypeOverride = null,
			bool? isOperationOverride = null,
			bool? isAsyncOverride = null,
			bool applyRectLayout = false,
			bool applyWirePointAnchor = false,
			bool styleLabelBackground = false,
			string logContext = "Connector")
		{
			if (connectorImage == null || connectorImage.IsRemoved ||
			    connectorImage.Slot == null || connectorImage.Slot.IsRemoved) return;

			// Skip if this connector is already styled
			if (RoundedCornersHelper.HasPFOTag(connectorImage.Slot))
			{
				Logger.LogUI(logContext, "Skipping already-styled connector");
				return;
			}

			bool isOutput = isOutputOverride ?? (connectorImage.RectTransform.OffsetMin.Value.x < 0);

			// Determine type info (overrides win; otherwise infer from proxies)
			var impulseProxy = connectorImage.Slot.GetComponent<ProtoFluxImpulseProxy>();
			var operationProxy = connectorImage.Slot.GetComponent<ProtoFluxOperationProxy>();

			ImpulseType? impulseType = impulseTypeOverride ?? impulseProxy?.ImpulseType.Value;
			bool isOperation = isOperationOverride ?? (operationProxy != null);
			bool isAsync = isAsyncOverride ?? (operationProxy?.IsAsync.Value ?? false);

			// Get or create shared sprite provider with the correct type
			var spriteProvider = ProtoFluxNodeVisual_BuildUI_Patch.GetOrCreateSharedConnectorSprite(
				connectorImage.Slot,
				isOutput,
				impulseType,
				isOperation,
				isAsync
			);

			// Apply the sprite provider to the connector image
			connectorImage.Sprite.Target = spriteProvider;
			connectorImage.PreserveAspect.Value = true;
			connectorImage.FlipHorizontally.Value = false; // We handle flipping in the sprite provider

			// Palette-driven connector tint (optional)
			if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE))
			{
				var palette = RoundedCornersHelper.EnsurePlatformColorPalette(instance.Slot);
				if (palette != null)
				{
					bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
					colorX colorToMatch = RoundedCornersHelper.GetConnectorTypeColor(connectorImage.Slot) ?? connectorImage.Tint.Value;
					var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, colorToMatch);
					if (source != null)
					{
						var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
						RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint);
					}
				}
			}

			// Optionally enforce RectTransform layout (used for dynamic connectors)
			if (applyRectLayout)
			{
				if (isOutput)
				{
					connectorImage.RectTransform.SetFixedHorizontal(-16f, 0.0f, 1f);
				}
				else
				{
					connectorImage.RectTransform.SetFixedHorizontal(0.0f, 16f, 0.0f);
				}
			}

			// Optionally set wire point anchor (matches BuildUI behavior)
			if (applyWirePointAnchor)
			{
				var wirePoint = connectorImage.Slot.FindChild("<WIRE_POINT>");
				if (wirePoint != null)
				{
					var rectTransform = wirePoint.GetComponent<RectTransform>();
					if (rectTransform != null)
					{
						rectTransform.AnchorMin.Value = new float2(isOutput ? 1f : 0.0f, 0.5f);
						rectTransform.AnchorMax.Value = new float2(isOutput ? 1f : 0.0f, 0.5f);
					}
				}
			}

			// Optional: style connector label background
			if (styleLabelBackground)
			{
				var parentSlot = connectorImage.Slot.Parent;
				var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
					.FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);

				if (labelBackgroundImage != null)
				{
					bool usePalette = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE);
					float labelScale = RoundedCornersHelper.CONNECTOR_LABEL_SPRITE_SCALE;
					RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, usePalette ? false : true, labelScale);

					// Align vertical offsets with base ProtoFlux layout (only adjust Y values)
					RectTransform labelRect = labelBackgroundImage.RectTransform;
					float2 offsetMin = labelRect.OffsetMin.Value;
					float2 offsetMax = labelRect.OffsetMax.Value;
					labelRect.OffsetMin.Value = new float2(offsetMin.x, 1f);
					labelRect.OffsetMax.Value = new float2(offsetMax.x, -1f);

					// Toggle enabled status based on config
					bool backgroundsEnabled = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLE_CONNECTOR_LABEL_BACKGROUNDS);
					labelBackgroundImage.EnabledField.Value = backgroundsEnabled;

					Logger.LogUI("Connector Label Background", $"Applied header sprite to {logContext.ToLowerInvariant()} connector label background {(usePalette ? "with palette tint" : "while preserving original color")}");

					// Palette-driven label background tint + text tint (optional)
					// Use connector's current tint to find matching Sub color (mirrors existing dynamic behavior)
					if (usePalette)
					{
						var palette = RoundedCornersHelper.EnsurePlatformColorPalette(instance.Slot);
						if (palette != null)
						{
							bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
							colorX originalConnectorTint = connectorImage.Tint.Value;
							var bgSource = RoundedCornersHelper.GetLabelBackgroundTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, originalConnectorTint);
							if (bgSource != null)
							{
								var bgCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
								RoundedCornersHelper.TryLinkValueCopy(bgCopy, bgSource, labelBackgroundImage.Tint);
							}

							if (backgroundsEnabled)
							{
								var paletteTextSlot = labelBackgroundImage.Slot.FindChild("Text");
								var textComponent = paletteTextSlot?.GetComponent<Text>();
								var textSource = RoundedCornersHelper.GetLabelTextTintSource(palette);
								if (textComponent != null && textSource != null)
								{
									var textCopy = textComponent.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
									RoundedCornersHelper.TryLinkValueCopy(textCopy, textSource, textComponent.Color);
								}
							}
						}
					}

					// Find and center the text in the label (existing behavior is logging + optional tint copy)
					var textSlot = labelBackgroundImage.Slot.FindChild("Text");
					if (textSlot != null)
					{
						var textComponent = textSlot.GetComponent<Text>();
						if (textComponent != null)
						{
							Logger.LogUI("Connector Label Text", $"Set {logContext.ToLowerInvariant()} connector label text to center alignment");

							// If backgrounds are disabled, copy the image tint to the text color
							if (!backgroundsEnabled)
							{
								var valueCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
								valueCopy.Source.Target = labelBackgroundImage.Tint;
								valueCopy.Target.Target = textComponent.Color;
								valueCopy.WriteBack.Value = false;
								Logger.LogUI("Connector Label Text Color", $"Copying background tint to text color for {logContext.ToLowerInvariant()} (backgrounds disabled)");
							}
						}
					}
				}
			}

			// Mark this connector as styled by ProtoFluxOverhaul
			RoundedCornersHelper.AddPFOTag(connectorImage.Slot);
		}

		internal static void StyleDynamicConnector(
			ProtoFluxNodeVisual instance,
			UIBuilder ui,
			bool isOutput,
			ImpulseType? impulseTypeOverride = null,
			bool? isOperationOverride = null,
			bool? isAsyncOverride = null,
			bool styleLabelBackground = false,
			string logContext = "Dynamic Connector")
		{
			try
			{
				// Skip if disabled
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

				// === User Permission Check ===
				if (!PermissionHelper.HasPermission(instance)) return;

				// Find wire point slot for audio setup
				var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
				if (wirePointSlot != null)
				{
					WireHelper.CreateAudioClipsSlot(wirePointSlot);
				}

				// Find the connector image that was just created
				var connectorImage = ui.Current?.GetComponentInChildren<Image>(image => image.Slot.Name == "Connector");
				if (connectorImage == null) return;

				StyleConnectorImage(
					instance,
					connectorImage,
					isOutputOverride: isOutput,
					impulseTypeOverride: impulseTypeOverride,
					isOperationOverride: isOperationOverride,
					isAsyncOverride: isAsyncOverride,
					applyRectLayout: true,
					applyWirePointAnchor: true,
					styleLabelBackground: styleLabelBackground,
					logContext: logContext
				);

				Logger.LogUI(logContext, "Applied texture patch to newly created connector");
			}
			catch (Exception e)
			{
				Logger.LogError($"Error in {logContext} generation", e, Logger.LogCategory.UI);
			}
		}
	}
}

