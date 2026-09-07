using System.Linq;
using System.Text.RegularExpressions;

using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;

using ProtoFlux.Runtimes.Execution.Nodes.Actions;

using Renderite.Shared;

namespace ProtoFluxOverhaul {
	public partial class ProtoFluxNodeVisual_BuildUI_Patch {
		private static string SanitizeNodeName(string name) =>
			Regex.Replace(name.Replace("<br>", " "), @"<size=[^>]*>", "").Replace("</size>", "");

		private static colorX BuildTitle(ProtoFluxNodeVisual __instance, Slot root, ProtoFluxNode node, string originalNodeName,
			PlatformColorPalette palette, bool usePlatformPalette, bool useHeaderBackgroundColor, Image overviewSlot) {
			// The engine only emits an empty spacer for forced-overview nodes such as
			// Mul_Float_Float3. Build their title from NodeName even if no spacer is found.
			string displayText = string.IsNullOrWhiteSpace(originalNodeName)
				? node.GetType().GetNiceName()
				: originalNodeName;
			displayText = SanitizeNodeName(displayText);

			// Header panels are direct children of the node layout. Restrict the search
			// so a connector label with a matching tint cannot be mistaken for the title.
			Image headerPanel = null;
			Text headerText = null;
			foreach (var child in root.Children) {
				var candidate = child.GetComponent<Image>();
				if (candidate == null || candidate.Tint.Value != RadiantUI_Constants.HEADER) continue;
				var candidateText = child.GetComponentInChildren<Text>();
				if (candidateText == null) continue;
				headerPanel = candidate;
				headerText = candidateText;
				break;
			}
			Slot spacerSlot = null;
			if (headerPanel == null && node.OverrideOverviewMode == true) {
				spacerSlot = root.Children.FirstOrDefault(child =>
					child.ChildrenCount == 0 &&
					child.GetComponent<RectTransform>() != null &&
					child.GetComponent<LayoutElement>() != null &&
					child.GetComponent<Component>(static component =>
						component is not RectTransform && component is not LayoutElement) == null);
			}

			// Create TitleParent with OrderOffset -1
			var titleParentSlot = root.AddSlot("TitleParent");
			titleParentSlot.OrderOffset = -1;

			// Add RectTransform to parent
			titleParentSlot.AttachComponent<RectTransform>();

			// Add overlapping layout to parent with exact settings
			var overlappingLayout = titleParentSlot.AttachComponent<OverlappingLayout>();
			overlappingLayout.PaddingTop.Value = 5.5f;
			overlappingLayout.PaddingRight.Value = 5.5f;
			overlappingLayout.PaddingBottom.Value = 2.5f;
			overlappingLayout.PaddingLeft.Value = 5.5f;
			overlappingLayout.HorizontalAlign.Value = LayoutHorizontalAlignment.Center;
			overlappingLayout.VerticalAlign.Value = LayoutVerticalAlignment.Middle;
			overlappingLayout.ForceExpandWidth.Value = true;
			overlappingLayout.ForceExpandHeight.Value = true;

			// Add LayoutElement with exact settings from image
			var layoutElement = titleParentSlot.AttachComponent<LayoutElement>();
			layoutElement.MinWidth.Value = -1;
			layoutElement.PreferredWidth.Value = -1;
			layoutElement.FlexibleWidth.Value = -1;
			layoutElement.MinHeight.Value = 24;
			layoutElement.PreferredHeight.Value = -1;
			layoutElement.FlexibleHeight.Value = -1;
			layoutElement.Area.Value = -1;
			layoutElement.Priority.Value = 1;

			// Create a copy of the header panel under TitleParent
			var newHeaderSlot = titleParentSlot.AddSlot("Header");
			newHeaderSlot.ActiveSelf = true;
			var image = newHeaderSlot.AttachComponent<Image>();

			// Header tint: either palette-driven (mapped from node type color) or per-node type color (existing behavior)
			colorX headerTintColorForContrast;
			colorX nodeTypeColor;

			// Get the node's type color for the header (always compute so palette mode can map it)
			var nodeType = node.GetType();
			if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase))) {
				Logger.LogUI("Node Type", $"Found Update node of type: {nodeType.Name}");
				// Check if it's an async update node
				bool isAsync = typeof(IAsyncNodeOperation).IsAssignableFrom(nodeType);
				nodeTypeColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
				Logger.LogUI("Node Color", $"Setting Update node color to {(isAsync ? "ASYNC" : "SYNC")} flow color");
			} else {
				nodeTypeColor = DatatypeColorHelper.GetTypeColor(nodeType);
			}
			Logger.LogUI("Node Color", $"Node type color: R:{nodeTypeColor.r:F2} G:{nodeTypeColor.g:F2} B:{nodeTypeColor.b:F2}");

			if (usePlatformPalette && palette != null) {
				// Map the type color to the closest palette color (checks all shades, not just neutrals)
				// Returns both the palette field (for dynamic driving) and the matched constant (for reliable contrast)
				var (headerSource, matchedConstant) = RoundedCornersHelper.FindClosestPaletteFieldWithConstant(palette, nodeTypeColor);

				var headerCopy = image.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
				if (headerSource != null) {
					if (!RoundedCornersHelper.TryLinkValueCopy(headerCopy, headerSource, image.Tint)) {
						Logger.LogUI("Header Tint", "Skipped header palette tint copy; existing drive detected");
					} else {
						Logger.LogUI("Header Tint", $"Linked header tint to palette field (matched constant: R:{matchedConstant.r:F2} G:{matchedConstant.g:F2} B:{matchedConstant.b:F2})");
					}
				} else {
					// Fallback: set the tint directly if palette field not found
					image.Tint.Value = matchedConstant;
					Logger.LogUI("Header Tint", "Palette header source missing; applied matched constant directly");
				}

				// Use the matched RadiantUI_Constants color for contrast calculation
				// This is reliable even if the palette fields haven't synced yet
				headerTintColorForContrast = matchedConstant;
			} else {
				if (!RoundedCornersHelper.TrySetColorIfUndriven(image.Tint, nodeTypeColor))
					Logger.LogUI("Header Tint", "Skipped header tint override; existing drive detected");
				headerTintColorForContrast = nodeTypeColor;
			}

			// Create a copy of the text under the new header
			var newTextSlot = newHeaderSlot.AddSlot("Text");
			newTextSlot.ActiveSelf = true;
			var newText = newTextSlot.AttachComponent<Text>();
			var textRect = newText.RectTransform;

			// Set the anchors to stretch horizontally and vertically
			textRect.AnchorMin.Value = new float2(0.028f, 0.098f);  // x:0.028 y:0.098
			textRect.AnchorMax.Value = new float2(0.97f, 0.9f);     // x:0.97 y:0.9

			// Apply text settings
			newText.Size.Value = 64.00f;
			newText.HorizontalAlign.Value = TextHorizontalAlignment.Center;
			newText.VerticalAlign.Value = TextVerticalAlignment.Middle;
			newText.AlignmentMode.Value = AlignmentMode.Geometric;
			newText.LineHeight.Value = 0.80f;
			newText.AutoSizeMin.Value = 8;
			newText.AutoSizeMax.Value = 64;
			newText.HorizontalAutoSize.Value = true;
			newText.VerticalAutoSize.Value = true;
			newText.ParseRichText.Value = true;

			var (baseTypeName, headerGenericPart, displayGenericPartPlain) = FormatHeaderNames(node.GetType(), displayText);

			// Initial text settings (color will be set after header tint is finalized)
			newText.Size.Value = 10.5f;
			newText.AutoSizeMin.Value = 4f;

			// Reuse the original layout when available; otherwise the new header stretches to TitleParent.
			var newHeaderRect = image.RectTransform;
			var originalRect = headerPanel?.RectTransform ?? spacerSlot?.GetComponent<RectTransform>();
			if (originalRect != null) {
				newHeaderRect.AnchorMin.Value = originalRect.AnchorMin.Value;
				newHeaderRect.AnchorMax.Value = originalRect.AnchorMax.Value;
				newHeaderRect.OffsetMin.Value = originalRect.OffsetMin.Value;
				newHeaderRect.OffsetMax.Value = originalRect.OffsetMax.Value;
			}

			// Remove the original title or spacer from layout only after its replacement exists.
			if (headerPanel != null) headerPanel.Slot.ActiveSelf = false;
			if (headerText != null) headerText.Slot.ActiveSelf = false;
			if (spacerSlot != null) spacerSlot.ActiveSelf = false;

			ConfigureTitleVisibility(__instance, newHeaderSlot, overviewSlot);

			// Apply rounded corners to the new header
			// Header uses inverted shading; connector labels use header-style sprite but normal shading.
			RoundedCornersHelper.ApplyRoundedCorners(image, true, invertShading: true);

			// Toggle header background based on config
			bool headerBackgroundEnabled = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLE_HEADER_BACKGROUND);
			image.EnabledField.Value = headerBackgroundEnabled;
			Logger.LogUI("Header Background", $"Header background {(headerBackgroundEnabled ? "enabled" : "disabled")}");

			// Determine the correct background color for contrast calculation
			// When header background is visible, use header tint; when hidden, use node background color
			colorX contrastColor;
			if (headerBackgroundEnabled) {
				// Header background is visible - contrast against header tint
				contrastColor = headerTintColorForContrast;
			} else {
				// Header background is hidden - contrast against node background
				if (useHeaderBackgroundColor) {
					// Node background uses header tint color
					contrastColor = headerTintColorForContrast;
				} else if (usePlatformPalette) {
					// Node background uses palette dark color - use constant for reliable contrast
					contrastColor = RadiantUI_Constants.Neutrals.DARK;
				} else {
					// Node background uses default BG color
					contrastColor = RadiantUI_Constants.BG_COLOR;
				}
			}
			float luminance = 0.2126f * contrastColor.r + 0.7152f * contrastColor.g + 0.0722f * contrastColor.b;
			colorX finalTextColor = luminance > 0.5f ? colorX.Black : colorX.White;
			Logger.LogUI("Text Color", $"Contrast bg: R:{contrastColor.r:F2} G:{contrastColor.g:F2} B:{contrastColor.b:F2}, Luminance: {luminance:F3} -> {(finalTextColor == colorX.Black ? "BLACK" : "WHITE")}");

			// Set text content based on whether header background is enabled
			string shortNameText;
			string fullNameText;

			if (headerBackgroundEnabled) {
				// Use rich text color tag for contrast when background is visible
				string textHex = finalTextColor == colorX.Black ? "#000000" : "#FFFFFF";
				shortNameText = $"<color={textHex}><b> {displayText}</b><size=80%>{displayGenericPartPlain}</size></color>";
				fullNameText = $"<color={textHex}><b>{baseTypeName}</b><size=80%>{headerGenericPart}</size></color>";
			} else {
				// No color tag - let the Color field (driven by ValueCopy) control the color
				shortNameText = $"<b>{displayText}</b><size=80%> {displayGenericPartPlain}</size>";
				fullNameText = $"<b>{baseTypeName}</b><size=80%>{headerGenericPart}</size>";

				// Maintain readability even when background is hidden by using the chosen contrast color
				newText.Color.Value = finalTextColor;
				newText.Color.ForceSet(finalTextColor);
				Logger.LogUI("Header Text Color", $"Applied contrast text color with hidden header background: {(finalTextColor == colorX.Black ? "BLACK" : "WHITE")}");
			}

			// Set initial text content
			newText.Content.Value = shortNameText;

			// === Hover to Show Full Name Feature ===
			// Use the existing HoverArea from the <NODE_UI> slot
			var nodeHoverArea = __instance.NodeHoverArea;

			if (nodeHoverArea != null) {
				// Create ValueCopy to copy hover state to the driver
				var hoverStateCopy = newHeaderSlot.AttachComponent<ValueCopy<bool>>();
				hoverStateCopy.Source.Target = nodeHoverArea.IsHovering;

				// Create BooleanValueDriver to switch between short and full names
				var nameDriver = newHeaderSlot.AttachComponent<BooleanValueDriver<string>>();
				hoverStateCopy.Target.Target = nameDriver.State;
				// Not hovering (false) = short name, Hovering (true) = full name
				nameDriver.FalseValue.Value = shortNameText;
				nameDriver.TrueValue.Value = fullNameText;
				nameDriver.TargetField.Target = newText.Content;

				Logger.LogUI("Hover Feature", $"Added hover-to-show-full-name using <NODE_UI> HoverArea: '{displayText}' → '{baseTypeName}{headerGenericPart}'");
			} else {
				Logger.LogUI("Hover Feature", "WARNING: Could not find NodeHoverArea on ProtoFluxNodeVisual");
			}

			return headerTintColorForContrast;
		}

		private static (string baseName, string genericPart, string displayGenericPart) FormatHeaderNames(System.Type headerNodeType, string displayText) {
			// Get the node type name with generics for header
			var genericArgs = headerNodeType.IsGenericType ? headerNodeType.GetGenericArguments() : System.Type.EmptyTypes;
			string fullTypeName = headerNodeType.GetNiceName();
			string baseTypeName = fullTypeName;
			string headerGenericPart = "";

			// Handle two conventions: C# generics and underscore naming
			if (headerNodeType.IsGenericType) {
				// Handle C# generic types like ValueMulMulti<T>
				if (genericArgs.Length > 0) {
					// Format generic arguments like <float2> or <int, bool>
					var genericNames = genericArgs.Select(t => t.GetNiceName());
					headerGenericPart = $"<{string.Join(", ", genericNames)}>";

					// GetNiceName() may already include generics (e.g., "ValueInput<float2>")
					// Extract just the base name by removing everything from the first '<'
					int genericBracketIndex = fullTypeName.IndexOf('<');
					if (genericBracketIndex > 0) {
						baseTypeName = fullTypeName.Substring(0, genericBracketIndex);
					}

					// Also remove underscore suffix if it exists (e.g., ValueMulMulti_Float2 -> ValueMulMulti)
					int underscoreIndex = baseTypeName.LastIndexOf('_');
					if (underscoreIndex > 0) {
						baseTypeName = baseTypeName.Substring(0, underscoreIndex);
					}
				}
			} else {
				// Handle underscore naming convention like AvgMulti_Float2 (not C# generics)
				int underscoreIndex = fullTypeName.LastIndexOf('_');
				if (underscoreIndex > 0) {
					baseTypeName = fullTypeName.Substring(0, underscoreIndex);
					string typeSuffix = fullTypeName.Substring(underscoreIndex + 1);
					headerGenericPart = $"<{typeSuffix}>";
				}
			}

			// Check if the display text already contains the generic type (e.g., "float2 Input" already has "float2")
			// If so, don't add the generic part to avoid duplication like "float2 Input<float2>"
			bool displayAlreadyHasGeneric = false;
			if (!string.IsNullOrEmpty(headerGenericPart) && headerNodeType.IsGenericType) {
				foreach (var arg in genericArgs) {
					string argName = arg.GetNiceName();
					if (displayText.Contains(argName)) {
						displayAlreadyHasGeneric = true;
						break;
					}
				}
			}

			// If display already has the generic type, clear the generic part to avoid duplication
			string displayGenericPart = displayAlreadyHasGeneric ? "" : headerGenericPart;

			// Create version without angle brackets for short name display (e.g., " float2" instead of "<float2>")
			string displayGenericPartPlain = "";
			if (!string.IsNullOrEmpty(displayGenericPart)) {
				// Remove < and > and add a space prefix
				displayGenericPartPlain = " " + displayGenericPart.Trim('<', '>');
			}

			return (baseTypeName, headerGenericPart, displayGenericPartPlain);
		}

		private static void ConfigureTitleVisibility(ProtoFluxNodeVisual __instance, Slot newHeaderSlot, Image overviewSlot) {
			// Handle Header/Overview visibility based on overview mode
			if (__instance.LocalUser != null) {
				bool overviewModeEnabled = OverviewModeHelper.GetOverviewMode(__instance.LocalUser);

				// Only toggle header visibility if there's an overview slot
				if (overviewSlot != null) {
					// Base visibility (without hover override)
					bool baseHeaderVisible = !overviewModeEnabled;
					bool baseOverviewVisible = overviewModeEnabled;

					// Hover override: while hovered, force Overview hidden (and Header visible)
					var overviewHoverArea = __instance.NodeHoverArea;
					if (overviewHoverArea != null) {
						// Header ActiveSelf driver: hovering => true, not-hovering => baseHeaderVisible
						// Only install if not already driven (prevents LinkBase warnings on rebuild/unpack).
						if (!newHeaderSlot.ActiveSelf_Field.IsDriven) {
							var headerActiveDriver = newHeaderSlot.GetComponentOrAttach<BooleanValueDriver<bool>>();
							headerActiveDriver.TargetField.Target = newHeaderSlot.ActiveSelf_Field;
							headerActiveDriver.TrueValue.Value = true;
							headerActiveDriver.FalseValue.Value = baseHeaderVisible;

							var headerHoverCopy = newHeaderSlot.GetComponentOrAttach<ValueCopy<bool>>();
							headerHoverCopy.Source.Target = overviewHoverArea.IsHovering;
							headerHoverCopy.Target.Target = headerActiveDriver.State;
							headerHoverCopy.WriteBack.Value = false;
						} else {
							newHeaderSlot.ActiveSelf = baseHeaderVisible;
							Logger.LogUI("Hover Overview Override", "Skipped header hover override: Header ActiveSelf is already driven");
						}

						// Overview ActiveSelf driver: hovering => false, not-hovering => baseOverviewVisible
						// NOTE: ProtoFluxNodeVisual internally drives Overview visibility (_overviewVisual FieldDrive<bool>).
						// Attempting to link another driver to ActiveSelf triggers "already linked... Use ForceLink()" warnings.
						if (!overviewSlot.Slot.ActiveSelf_Field.IsDriven) {
							var overviewActiveDriver = overviewSlot.Slot.GetComponentOrAttach<BooleanValueDriver<bool>>();
							overviewActiveDriver.TargetField.Target = overviewSlot.Slot.ActiveSelf_Field;
							overviewActiveDriver.TrueValue.Value = false;
							overviewActiveDriver.FalseValue.Value = baseOverviewVisible;

							var overviewHoverCopy = overviewSlot.Slot.GetComponentOrAttach<ValueCopy<bool>>();
							overviewHoverCopy.Source.Target = overviewHoverArea.IsHovering;
							overviewHoverCopy.Target.Target = overviewActiveDriver.State;
							overviewHoverCopy.WriteBack.Value = false;
						} else {
							// Keep base behavior; hover override is skipped to avoid fighting the engine drive.
							overviewSlot.Slot.ActiveSelf = baseOverviewVisible;
							Logger.LogUI("Hover Overview Override", "Skipped overview hover override: Overview ActiveSelf is already driven by engine");
						}

						Logger.LogUI("Hover Overview Override", $"Hover override install attempted (base: header={(baseHeaderVisible ? "VISIBLE" : "HIDDEN")}, overview={(baseOverviewVisible ? "VISIBLE" : "HIDDEN")})");
					} else {
						// No hover area found, fall back to static behavior
						newHeaderSlot.ActiveSelf = baseHeaderVisible;
						overviewSlot.Slot.ActiveSelf = baseOverviewVisible;
						Logger.LogUI("Overview Processing", $"Overview slot set to {(overviewModeEnabled ? "VISIBLE" : "HIDDEN")} based on overview mode");
						Logger.LogUI("Header Visibility", $"Header slot set to {(!overviewModeEnabled ? "VISIBLE" : "HIDDEN")} based on overview mode");
					}
				} else {
					// No overview slot found, keep header visible
					newHeaderSlot.ActiveSelf = true;
					Logger.LogUI("Header Visibility", "No overview slot found - keeping header visible");
				}
			} else {
				// Fallback: if no user found, keep header visible and overview hidden
				newHeaderSlot.ActiveSelf = true;
				if (overviewSlot != null) {
					overviewSlot.Slot.ActiveSelf = false;
				}
				Logger.LogUI("Header Visibility", "No user found - defaulting to header visible, overview hidden");
			}

		}
	}
}
