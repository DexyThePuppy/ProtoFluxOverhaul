using System;
using System.Linq;
using System.Reflection;

using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;

using HarmonyLib;

using ProtoFlux.Runtimes.Execution.Nodes.Actions;

using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul {
	[HarmonyPatch(typeof(ProtoFluxNodeVisual), "BuildUI")]
	public partial class ProtoFluxNodeVisual_BuildUI_Patch {
		private static readonly FieldInfo BgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");
		private static readonly FieldInfo OverviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg");

		public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ProtoFluxNode node) {
			try {
				// Skip if disabled
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

				// Skip if instance or slot is destroyed/removed
				if (__instance == null || __instance.IsRemoved ||
					__instance.Slot == null || __instance.Slot.IsRemoved ||
					node == null || node.IsRemoved) return;

				// Log entry for debugging regeneration issues
				var slotTag = __instance.Slot.Tag;
				Logger.LogUI("BuildUI Entry", $"Processing node '{node.GetType().Name}', Slot={__instance.Slot.Name}, RefID={__instance.Slot.ReferenceID}, Tag='{slotTag ?? "(null)"}'");

				// Skip if already styled by ProtoFluxOverhaul (prevents duplicate processing)
				if (RoundedCornersHelper.HasPFOTag(__instance.Slot)) {
					Logger.LogUI("BuildUI", $"Skipping already-styled node '{node.GetType().Name}' (Tag contains ProtoFluxOverhaul)");
					return;
				}

				// Audio is now handled on-demand by ProtoFluxSounds

				// === User Permission Check ===
				if (!PermissionHelper.HasPermission(__instance)) return;

				// === Remove rich text formatting tags from engine's node names only ===
				// ProtoFlux nodes like Dot, Cross, Transpose use <br> and <size=X%> for formatting
				// We strip these to show names on a single line with uniform text size
				// IMPORTANT: Only target the engine's original node name texts, not mod-created text
				string originalNodeName = node.NodeName;
				if (originalNodeName != null && (originalNodeName.Contains("<br>") || originalNodeName.Contains("<size="))) {
					string sanitizedName = SanitizeNodeName(originalNodeName);
					// Find text components that contain the original node name (engine-created)
					var textComponents = ui.Root.GetComponentsInChildren<Text>();
					foreach (var text in textComponents) {
						// Only process if the content matches the original node name exactly
						if (text.Content.Value == originalNodeName) {
							text.Content.Value = sanitizedName;
						}
					}
				}

				// Special handling for Update nodes
				if (node.GetType().IsSubclassOf(typeof(UpdateBase)) || node.GetType().IsSubclassOf(typeof(UserUpdateBase))) {
					Logger.LogUI("Node Processing", "Processing Update node UI");
					// Make sure we don't interfere with global reference UI generation
					if (ui.Current.Name == "Global References") {
						Logger.LogUI("Node Processing", "Skipping UI modification for global references panel");
						return;
					}
				}

				// Optional: drive node UI colors from PlatformColorPalette (instead of per-node type color overrides)
				bool usePlatformPalette = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE);
				bool useHeaderBackgroundColor = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND);
				PlatformColorPalette palette = usePlatformPalette ? RoundedCornersHelper.EnsurePlatformColorPalette(ui.Root) : null;

				var connectorImages = StyleConnectors(ui.Root, palette, usePlatformPalette);

				// Get the background image using reflection
				var bgImageRef = (SyncRef<Image>)BgImageField.GetValue(__instance);
				var bgImage = bgImageRef?.Target;
				if (bgImage != null) {
					bgImage.Slot.OrderOffset = -2;
				}

				if (usePlatformPalette && palette != null && bgImage != null && !useHeaderBackgroundColor) {
					// Set up chained drivers for different states:
					// - Normal: Dark
					// - IsHighlighted: Mid
					// - IsSelected: MidLight (highest priority)
					if (!bgImage.Tint.IsDriven) {
						var pfoSlot = bgImage.Slot.FindChildOrAdd("PFO_BgDriver");

						// Two BooleanValueDrivers: highlight result lives on selectDriver.FalseValue
						// (not a ValueField — that is an IValueSource and leaks into grab-drop).
						var existingDrivers = pfoSlot.GetComponents<BooleanValueDriver<colorX>>();
						var highlightDriver = existingDrivers.ElementAtOrDefault(0) ?? pfoSlot.AttachComponent<BooleanValueDriver<colorX>>();
						var selectDriver = existingDrivers.ElementAtOrDefault(1) ?? pfoSlot.AttachComponent<BooleanValueDriver<colorX>>();

						highlightDriver.TargetField.Target = selectDriver.FalseValue;
						selectDriver.TargetField.Target = bgImage.Tint;

						if (!highlightDriver.FalseValue.IsDriven) {
							var darkCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
							RoundedCornersHelper.TryLinkValueCopy(darkCopy, palette.Neutrals.Dark, highlightDriver.FalseValue);
						}

						if (!highlightDriver.TrueValue.IsDriven) {
							var midCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
							RoundedCornersHelper.TryLinkValueCopy(midCopy, palette.Neutrals.Mid, highlightDriver.TrueValue);
						}

						if (!highlightDriver.State.IsDriven) {
							var highlightStateCopy = pfoSlot.AttachComponent<ValueCopy<bool>>();
							highlightStateCopy.Source.Target = __instance.IsHighlighted;
							highlightStateCopy.Target.Target = highlightDriver.State;
						}

						if (!selectDriver.TrueValue.IsDriven) {
							var midLightCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
							RoundedCornersHelper.TryLinkValueCopy(midLightCopy, palette.Neutrals.MidLight, selectDriver.TrueValue);
						}

						if (!selectDriver.State.IsDriven) {
							var selectStateCopy = pfoSlot.AttachComponent<ValueCopy<bool>>();
							selectStateCopy.Source.Target = __instance.IsSelected;
							selectStateCopy.Target.Target = selectDriver.State;
						}

						Logger.LogUI("PlatformColorPalette", "Set up selection/highlight-aware BG tint driver (Dark → Mid → MidLight)");
					} else {
						Logger.LogUI("PlatformColorPalette", "Skipped BG tint driver; existing drive detected");
					}
				}

				// Theme ProtoFlux node UI buttons:
				// - If Colored Node Background is enabled: tint buttons from the node background via ValueCopy
				// - Else if PlatformColorPalette is enabled: use palette neutrals via ValueCopy
				// - Otherwise: buttons keep original colors but still get texture/shading
				RoundedCornersHelper.ApplyProtoFluxNodeButtonTheme(
				ui.Root,
				palette,
				bgImage,
				usePlatformPalette,
				useHeaderBackgroundColor);

				// Relays and other deliberately headerless nodes keep their compact layout.
				if (node.SupressHeaderAndFooter) return;

				var overviewImage = OverviewModeHelper.GetOverviewImage(__instance);
				var headerTintColorForContrast = BuildTitle(__instance, ui.Root, node, originalNodeName,
					palette, usePlatformPalette, useHeaderBackgroundColor, overviewImage);

				// Apply rounded corners to the background with header color if config is enabled
				if (bgImage != null) {
					RoundedCornersHelper.ApplyRoundedCorners(bgImage, false, headerTintColorForContrast);
				}

				StyleOverviewPanel(__instance, overviewImage, palette, usePlatformPalette);

				StyleConnectorLabels(connectorImages, palette, usePlatformPalette);

				Logger.LogUI("Completion", $"Created title layout for '{node.GetType().Name}'");

				StyleFooter(ui.Root, palette, usePlatformPalette, useHeaderBackgroundColor, headerTintColorForContrast);

				// === Cleanup relay node visuals ===
				// Relay nodes (ValueRelay, ObjectRelay, CallRelay, etc.) don't need the background sprite
				// and shading since the node visual patch adds those. Remove duplicates.
				RoundedCornersHelper.CleanupRelayNodeVisuals(__instance.Slot, node);

				RoundedCornersHelper.AddPFOTag(__instance.Slot);
				Logger.LogUI("Tag", $"Added ProtoFluxOverhaul tag to node '{node.GetType().Name}' after successful style pass");
				Logger.LogUI("BuildUI", $"Successfully processed node '{node.GetType().Name}'");
			} catch (Exception e) {
				Logger.LogError("Failed to process node visual", e, LogCategory.UI);
			}
		}
	}



}
