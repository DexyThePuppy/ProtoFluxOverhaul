using System;
using System.Linq;

using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;

using Renderite.Shared;

using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul {
	public partial class ProtoFluxNodeVisual_BuildUI_Patch {
		private static void StyleOverviewPanel(ProtoFluxNodeVisual __instance, Image overviewImage, PlatformColorPalette palette, bool usePlatformPalette) {
			// Apply custom RectTransform offsets to the Overview mode panel (no sprite override)
			try {
				if (overviewImage != null) {
					if (usePlatformPalette && palette != null) {
						// Overview tint is typically driven by ProtoFluxNodeVisual's internal _overviewBg FieldDrive<colorX>.
						// Setting the drive target value is the correct way to override it (ValueCopy would be rejected due to Tint.IsDriven).
						var overviewBgDrive = (FieldDrive<colorX>)OverviewBgField.GetValue(__instance);
						if (overviewBgDrive != null && overviewBgDrive.IsLinkValid) {
							overviewBgDrive.Target.Value = palette.Neutrals.Dark.Value;
						} else if (!overviewImage.Tint.IsDriven) {
							overviewImage.Tint.Value = palette.Neutrals.Dark.Value;
						} else {
							Logger.LogUI("PlatformColorPalette", "Skipped Overview palette tint; Tint is driven and _overviewBg drive was not link-valid");
						}
					}

					var overviewRect = overviewImage.RectTransform;
					if (overviewRect != null) {
						// OffsetMin: (16, -3), OffsetMax: (-16, 20)
						overviewRect.OffsetMin.Value = new float2(16f, -3f);
						overviewRect.OffsetMax.Value = new float2(-16f, 20f);
					}

					Logger.LogUI("Overview Panel", "Applied custom rect offsets to Overview panel");
				}
			} catch (Exception e) {
				Logger.LogError("Failed to update Overview panel rect", e, LogCategory.UI);
			}
		}

		private static void StyleFooter(Slot root, PlatformColorPalette palette, bool usePlatformPalette, bool useHeaderBackgroundColor, colorX headerTintColorForContrast) {
			// Find the category text (it's the last Text component with dark gray color)
			var categoryText = root.GetComponentsInChildren<Text>()
				.LastOrDefault(text => text.Color.Value == colorX.DarkGray);

			if (categoryText != null) {
				categoryText.VerticalAlign.Value = TextVerticalAlignment.Middle;
				categoryText.Size.Value = 8.00f;
				categoryText.AlignmentMode.Value = AlignmentMode.LineBased;
				categoryText.LineHeight.Value = 0.35f;

				// Toggle footer category text based on config
				bool footerEnabled = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLE_FOOTER_CATEGORY_TEXT);
				categoryText.EnabledField.Value = footerEnabled;

				// Adjust the footer's LayoutElement MinHeight based on whether text is enabled
				var footerLayoutElement = categoryText.Slot.GetComponent<LayoutElement>();
				if (footerLayoutElement != null) {
					footerLayoutElement.MinHeight.Value = footerEnabled ? 16f : 10f;
					Logger.LogUI("Footer Layout", $"Footer MinHeight set to {(footerEnabled ? "16f" : "10f")}");
				}

				Logger.LogUI("Footer Category Text", $"Footer category text {(footerEnabled ? "enabled" : "disabled")}");

				// Apply appropriate color to category text based on config
				if (useHeaderBackgroundColor) {
					if (!RoundedCornersHelper.TrySetColorIfUndriven(categoryText.Color, headerTintColorForContrast))
						Logger.LogUI("Category Color", "Skipped category color override; existing drive detected");
					else
						Logger.LogUI("Category Color", $"Applied header color to category text: R:{headerTintColorForContrast.r:F2} G:{headerTintColorForContrast.g:F2} B:{headerTintColorForContrast.b:F2}");
				} else if (usePlatformPalette && palette != null) {
					// PlatformColorPalette mode: use Light color for contrast against Dark background
					// Node background is Dark (or Mid/MidLight when highlighted/selected), so Light provides good contrast
					if (!categoryText.Color.IsDriven) {
						var categoryColorCopy = categoryText.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
						RoundedCornersHelper.TryLinkValueCopy(categoryColorCopy, palette.Neutrals.Light, categoryText.Color);
						Logger.LogUI("Category Color", "Applied palette Light color to category text for contrast");
					}
				}
			}
		}
	}
}
