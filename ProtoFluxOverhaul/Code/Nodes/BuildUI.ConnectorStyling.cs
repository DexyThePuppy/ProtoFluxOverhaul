using System.Collections.Generic;

using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;

using ProtoFlux.Core;

using Renderite.Shared;

namespace ProtoFluxOverhaul {
	public partial class ProtoFluxNodeVisual_BuildUI_Patch {
		private static List<Image> StyleConnectors(Slot root, PlatformColorPalette palette, bool usePlatformPalette) {
			// Find all connector images in the hierarchy (skip removed/destroyed)
			var connectorSlots = root.GetComponentsInChildren<Image>(static img => img != null && !img.IsRemoved &&
				img.Slot != null && !img.Slot.IsRemoved && img.Slot.Name == "Connector");

			foreach (var connectorImage in connectorSlots) {
				// Skip if connector was destroyed during processing
				if (connectorImage == null || connectorImage.IsRemoved ||
					connectorImage.Slot == null || connectorImage.Slot.IsRemoved) continue;

				// Determine if this is an output connector based on its RectTransform settings
				bool isOutput = connectorImage.RectTransform.OffsetMin.Value.x < 0;

				// Check for all proxy types to get the correct type color
				var impulseProxy = connectorImage.Slot.GetComponent<ProtoFluxImpulseProxy>();
				var operationProxy = connectorImage.Slot.GetComponent<ProtoFluxOperationProxy>();
				var inputProxy = connectorImage.Slot.GetComponent<ProtoFluxInputProxy>();
				var outputProxy = connectorImage.Slot.GetComponent<ProtoFluxOutputProxy>();

				ImpulseType? impulseType = null;
				bool isOperation = false;
				bool isAsync = false;

				// Get the original type color from the proxy
				// This is the color set by Resonite: type.GetTypeColor().MulRGB(1.5f)
				colorX? originalTypeColor = null;

				if (impulseProxy != null) {
					impulseType = impulseProxy.ImpulseType.Value;
					originalTypeColor = impulseProxy.ImpulseType.Value.GetImpulseColor().MulRGB(1.5f);
				} else if (operationProxy != null) {
					isOperation = true;
					isAsync = operationProxy.IsAsync.Value;
					originalTypeColor = DatatypeColorHelper.GetOperationColor(isAsync).MulRGB(1.5f);
				} else if (inputProxy != null && inputProxy.InputType.Value != null) {
					originalTypeColor = inputProxy.InputType.Value.GetTypeColor().MulRGB(1.5f);
				} else if (outputProxy != null && outputProxy.OutputType.Value != null) {
					originalTypeColor = outputProxy.OutputType.Value.GetTypeColor().MulRGB(1.5f);
				}

				// Get or create shared sprite provider with the correct type
				var spriteProvider = GetOrCreateSharedConnectorSprite(connectorImage.Slot, isOutput, impulseType, isOperation, isAsync);

				// Apply the sprite provider to the connector image
				connectorImage.Sprite.Target = spriteProvider;
				connectorImage.PreserveAspect.Value = true;

				// Palette-driven connector tint (optional)
				// Use the type color from the proxy (more reliable than reading image tint)
				if (usePlatformPalette && palette != null) {
					bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
					// Use type color from proxy, fallback to image tint
					colorX colorToMatch = originalTypeColor ?? connectorImage.Tint.Value;
					var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, colorToMatch);
					if (source != null) {
						var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
						if (!RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint)) {
							Logger.LogUI("PlatformColorPalette", "Skipped connector tint copy; existing drive detected");
						}
					}
				}

				// Set the correct RectTransform settings based on the original code
				if (isOutput) {
					connectorImage.RectTransform.SetFixedHorizontal(-16f, 0.0f, 1f);
				} else {
					connectorImage.RectTransform.SetFixedHorizontal(0.0f, 16f, 0.0f);
				}

				// Set the wire point anchor
				var wirePoint = connectorImage.Slot.FindChild("<WIRE_POINT>");
				if (wirePoint != null) {
					var rectTransform = wirePoint.GetComponent<RectTransform>();
					if (rectTransform != null) {
						rectTransform.AnchorMin.Value = new float2(isOutput ? 1f : 0.0f, 0.5f);
						rectTransform.AnchorMax.Value = new float2(isOutput ? 1f : 0.0f, 0.5f);
					}
				}

				// Mark this connector as styled by ProtoFluxOverhaul
				RoundedCornersHelper.AddPFOTag(connectorImage.Slot);
			}

			return connectorSlots;
		}

		private static void StyleConnectorLabels(List<Image> connectorImages, PlatformColorPalette palette, bool usePlatformPalette) {
			bool backgroundsEnabled = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLE_CONNECTOR_LABEL_BACKGROUNDS);

			foreach (var connectorImage in connectorImages) {
				// Earlier styling can remove visuals; match the validity filter used during collection.
				if (connectorImage == null || connectorImage.IsRemoved ||
					connectorImage.Slot == null || connectorImage.Slot.IsRemoved || connectorImage.Slot.Name != "Connector") continue;

				// Find the label background image (sibling Image component that's not the Connector)
				var parentSlot = connectorImage.Slot.Parent;
				var labelBackgroundImage = parentSlot?.GetComponentInChildren<Image>(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);

				if (labelBackgroundImage != null) {
					// Apply the header sprite provider for connector labels.
					// If palette mode is enabled, do NOT preserve original color (it would create a ValueDriver and block palette tint).
					float labelScale = RoundedCornersHelper.CONNECTOR_LABEL_SPRITE_SCALE;
					RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, !usePlatformPalette, labelScale);

					// Align vertical offsets with base ProtoFlux layout (only adjust Y values)
					RectTransform labelRect = labelBackgroundImage.RectTransform;
					float2 offsetMin = labelRect.OffsetMin.Value;
					float2 offsetMax = labelRect.OffsetMax.Value;
					labelRect.OffsetMin.Value = new float2(offsetMin.x, 1f);
					labelRect.OffsetMax.Value = new float2(offsetMax.x, -1f);

					// Toggle enabled status based on config
					labelBackgroundImage.EnabledField.Value = backgroundsEnabled;

					Logger.LogUI("Connector Label Background", $"Applied header sprite to connector label background {(usePlatformPalette ? "with palette tint" : "while preserving original color")}");

					// Palette-driven label background tint (optional)
					// Use connector's original tint to find matching Sub color
					if (usePlatformPalette && palette != null) {
						bool isOutput = connectorImage.RectTransform.OffsetMin.Value.x < 0;
						var impulseProxy = connectorImage.Slot.GetComponent<ProtoFluxImpulseProxy>();
						var operationProxy = connectorImage.Slot.GetComponent<ProtoFluxOperationProxy>();
						ImpulseType? impulseType = impulseProxy != null ? impulseProxy.ImpulseType.Value : (ImpulseType?)null;
						bool isOperation = operationProxy != null;
						bool isAsync = operationProxy != null && operationProxy.IsAsync.Value;
						bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
						colorX originalConnectorTint = connectorImage.Tint.Value;

						var bgSource = RoundedCornersHelper.GetLabelBackgroundTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, originalConnectorTint);
						if (bgSource != null) {
							var bgCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
							if (!RoundedCornersHelper.TryLinkValueCopy(bgCopy, bgSource, labelBackgroundImage.Tint)) {
								Logger.LogUI("PlatformColorPalette", "Skipped label background tint copy; existing drive detected");
							}
						}
					}

					// Find and center the text in the label
					var textSlot = labelBackgroundImage.Slot.FindChild("Text");
					if (textSlot != null) {
						var textComponent = textSlot.GetComponent<Text>();
						if (textComponent != null) {
							textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
							Logger.LogUI("Connector Label Text", $"Set connector label text to center alignment");

							// Palette-driven label text color when background is enabled (optional)
							if (usePlatformPalette && palette != null && backgroundsEnabled) {
								var textSource = RoundedCornersHelper.GetLabelTextTintSource(palette);
								if (textSource != null) {
									var textCopy = textComponent.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
									if (!RoundedCornersHelper.TryLinkValueCopy(textCopy, textSource, textComponent.Color)) {
										Logger.LogUI("PlatformColorPalette", "Skipped label text tint copy; existing drive detected");
									}
								}
							}

							// If backgrounds are disabled, copy the image tint to the text color
							if (!backgroundsEnabled) {
								var valueCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
								valueCopy.Source.Target = labelBackgroundImage.Tint;
								valueCopy.Target.Target = textComponent.Color;
								valueCopy.WriteBack.Value = false;
								Logger.LogUI("Connector Label Text Color", $"Copying background tint to text color (backgrounds disabled)");
							}
						}
					}
				}
			}
		}
	}
}
