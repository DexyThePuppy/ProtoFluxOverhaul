using System;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using Renderite.Shared;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	public static partial class RoundedCornersHelper
	{
		private static void EnsureShadingOverlay(
			Image hostImage,
			bool invertShading,
			bool isHeader,
			bool preserveOriginalColor,
			float? scaleOverride = null,
			float? fixedSizeOverride = null,
			IField<float> fixedSizeSource = null)
		{
			if (hostImage == null || hostImage.IsRemoved || hostImage.Slot == null || hostImage.Slot.IsRemoved) return;
			if (ProtoFluxOverhaul.Config == null) return;

			// Create (or reuse) the overlay slot
			var shadingSlot = hostImage.Slot.FindChild("Shading") ?? hostImage.Slot.AddSlot("Shading");
			shadingSlot.OrderOffset = 999;

			// Ensure it doesn't affect layout
			shadingSlot.GetComponentOrAttach<IgnoreLayout>();

			// Full-stretch rect
			var rt = shadingSlot.GetComponentOrAttach<RectTransform>();
			rt.AnchorMin.Value = float2.Zero;
			rt.AnchorMax.Value = float2.One;
			rt.OffsetMin.Value = float2.Zero;
			rt.OffsetMax.Value = float2.Zero;

			// Image
			var shadingImage = shadingSlot.GetComponentOrAttach<Image>();
			shadingImage.PreserveAspect.Value = true;
			// If we intend to control FixedSize, ensure NineSliceSizing uses FixedSize so Sprite.FixedSize is respected.
			// Otherwise keep it consistent with the host image.
			shadingImage.NineSliceSizing.Value =
				(fixedSizeOverride.HasValue || fixedSizeSource != null)
					? NineSliceSizing.FixedSize
					: hostImage.NineSliceSizing.Value;

			// Keep enabled in sync with host
			var enabledCopy = shadingSlot.GetComponentOrAttach<ValueCopy<bool>>();
			enabledCopy.Source.Target = hostImage.EnabledField;
			enabledCopy.Target.Target = shadingImage.EnabledField;
			enabledCopy.WriteBack.Value = false;

			// Sprite provider + texture
			var spriteProvider = shadingSlot.GetComponentOrAttach<SpriteProvider>();
			var texture = spriteProvider.Slot.GetComponentOrAttach<StaticTexture2D>();

			// Shading texture choice is independent of which rounded sprite style we use.
			// Connector labels use the header-style sprite (isHeader=true) but should still use normal shading.
			var shadingUrl = invertShading
				? ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.SHADING_INVERTED_TEXTURE)
				: ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.SHADING_TEXTURE);
			texture.URL.Value = shadingUrl;
			texture.FilterMode.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.FILTER_MODE);
			texture.WrapModeU.Value = TextureWrapMode.Clamp;
			texture.WrapModeV.Value = TextureWrapMode.Clamp;
			texture.MipMaps.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIPMAPS);
			texture.MipMapFilter.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIPMAP_FILTER);
			texture.AnisotropicLevel.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ANISOTROPIC_LEVEL);
			texture.KeepOriginalMipMaps.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.KEEP_ORIGINAL_MIPMAPS);
			texture.CrunchCompressed.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CRUNCH_COMPRESSED);
			texture.Readable.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.READABLE);
			texture.Uncompressed.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.UNCOMPRESSED);
			texture.DirectLoad.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.DIRECT_LOAD);
			texture.ForceExactVariant.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.FORCE_EXACT_VARIANT);
			texture.PreferredFormat.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.PREFERRED_FORMAT);
			texture.PreferredProfile.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.PREFERRED_PROFILE);

			spriteProvider.Texture.Target = texture;
			spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f);
			spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f);

			// Shading scale mapping: Label / Title / Background == 0.03 : 0.05 : 0.09 ("0.3 : 0.5 : 0.9")
			// IMPORTANT: connector label backgrounds use header-style sprite (isHeader=true) but should still use label scale.
			// So we honor the optional override when provided (e.g. CONNECTOR_LABEL_SPRITE_SCALE).
			//
			// If we're using FixedSize (e.g. buttons), the SpriteProvider.Scale should be 1.0 so the FixedSize math is stable.
			if (fixedSizeOverride.HasValue || fixedSizeSource != null)
				spriteProvider.Scale.Value = 1.0f;
			else
				spriteProvider.Scale.Value = scaleOverride ?? (preserveOriginalColor ? 0.03f : (isHeader ? 0.05f : 0.09f));

			// FixedSize matters for many default UI sprites (e.g. button backgrounds). When provided, match it.
			float resolvedFixedSize = fixedSizeOverride ?? 1.00f;
			if (fixedSizeSource != null)
			{
				var fixedCopy = shadingSlot.GetComponentOrAttach<ValueCopy<float>>();
				if (!spriteProvider.FixedSize.IsDriven || (fixedCopy.Target.IsLinkValid && fixedCopy.Target.Target == spriteProvider.FixedSize))
				{
					fixedCopy.Source.Target = fixedSizeSource;
					fixedCopy.Target.Target = spriteProvider.FixedSize;
					fixedCopy.WriteBack.Value = false;
				}
				else
				{
					spriteProvider.FixedSize.Value = resolvedFixedSize;
				}
			}
			else
			{
				spriteProvider.FixedSize.Value = resolvedFixedSize;
			}

			shadingImage.Sprite.Target = spriteProvider;
		}

		public static void ApplyRoundedCorners(Image image, bool isHeader = false, colorX? headerColor = null, bool preserveOriginalColor = false, float? spriteScaleOverride = null, bool invertShading = false)
		{
			// Safety check - don't process removed/destroyed components
			if (image == null || image.IsRemoved || image.Slot == null || image.Slot.IsRemoved) return;

			// Store original color if we need to preserve it
			colorX originalColor = image.Tint.Value;

			// For backgrounds, check if we need to update the tint even if sprite provider exists
			if (image.Sprite.Target is SpriteProvider existingSpriteProvider)
			{
				// If this is a background and we have a header color and the config is enabled, update the tint
				if (!isHeader && !preserveOriginalColor && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND))
				{
					// Drive the header color to prevent changes over time
					var headerColorField = image.Slot.GetComponentOrAttach<ValueField<colorX>>();
					headerColorField.Value.Value = headerColor.Value;
					var headerColorDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();

					// Only link if the target is not already linked
					if (!TryLinkValueDriver(headerColorDriver, image.Tint, headerColorField.Value))
					{
						Logger.LogUI("Header Color Background Update", "Skipped tint override; existing drive detected");
					}
					Logger.LogUI("Header Color Background Update", $"Updated existing background tint to header color: R:{headerColor.Value.r:F2} G:{headerColor.Value.g:F2} B:{headerColor.Value.b:F2}");
				}

				// Ensure shading overlay exists even if the rounded sprite already exists
				EnsureShadingOverlay(image, invertShading, isHeader, preserveOriginalColor, spriteScaleOverride);
				return;
			}

			Logger.LogUI("Rounded Corners", $"Applying rounded corners to {(isHeader ? "header" : "background")}");

			// Create a SpriteProvider for rounded corners
			var spriteProvider = image.Slot.AttachComponent<SpriteProvider>();
			Logger.LogUI("Sprite Provider", $"Created SpriteProvider for {(isHeader ? "header" : "background")}");

			// Set up the texture
			var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
			texture.URL.Value = isHeader
				? ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_HEADER_TEXTURE)
				: ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE);
			texture.FilterMode.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.FILTER_MODE);
			texture.WrapModeU.Value = TextureWrapMode.Clamp;
			texture.WrapModeV.Value = TextureWrapMode.Clamp;
			texture.MipMaps.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIPMAPS);
			texture.MipMapFilter.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIPMAP_FILTER);
			texture.AnisotropicLevel.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ANISOTROPIC_LEVEL);
			texture.KeepOriginalMipMaps.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.KEEP_ORIGINAL_MIPMAPS);
			texture.CrunchCompressed.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CRUNCH_COMPRESSED);
			texture.Readable.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.READABLE);
			texture.Uncompressed.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.UNCOMPRESSED);
			texture.DirectLoad.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.DIRECT_LOAD);
			texture.ForceExactVariant.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.FORCE_EXACT_VARIANT);
			texture.PreferredFormat.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.PREFERRED_FORMAT);
			texture.PreferredProfile.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.PREFERRED_PROFILE);

			Logger.LogUI("Texture Setup", $"Set up texture for {(isHeader ? "header" : "background")}");

			// Configure the sprite provider based on the image settings
			spriteProvider.Texture.Target = texture;
			spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f); // x:0 y:0 width:1 height:1
			spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f); // x:0.5 y:0 z:0 w:0
			// Default sprite scales:
			// - Label backgrounds (preserveOriginalColor): 0.02f
			// - Header: 0.05f
			// - Background: 0.09f
			float defaultScale = preserveOriginalColor ? 0.03f : (isHeader ? 0.05f : 0.09f);
			spriteProvider.Scale.Value = spriteScaleOverride ?? defaultScale;
			spriteProvider.FixedSize.Value = 1.00f; // FixedSize: 1.00
			Logger.LogUI("Sprite Config", $"Configured {(isHeader ? "header" : "background")} sprite provider settings");

			// Update the image to use the sprite
			image.Sprite.Target = spriteProvider;

			// Apply color logic
			if (preserveOriginalColor)
			{
				// Drive the original color for connector labels to prevent changes over time
				var originalColorField = image.Slot.GetComponentOrAttach<ValueField<colorX>>();
				originalColorField.Value.Value = originalColor;
				var originalColorDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();

				// Only link if the target is not already linked
				if (!TryLinkValueDriver(originalColorDriver, image.Tint, originalColorField.Value))
				{
					Logger.LogUI("Rounded Corners", "Skipped original color preservation; existing drive detected");
				}
				Logger.LogUI("Color Preserved", $"Preserved original color for connector label: R:{originalColor.r:F2} G:{originalColor.g:F2} B:{originalColor.b:F2}");
			}
			else if (!isHeader && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND))
			{
				// Drive header color to background if config option is enabled to prevent changes over time
				var headerColorField = image.Slot.GetComponentOrAttach<ValueField<colorX>>();
				headerColorField.Value.Value = headerColor.Value;
				var headerColorDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();

				// Only link if the target is not already linked
				if (!TryLinkValueDriver(headerColorDriver, image.Tint, headerColorField.Value))
				{
					Logger.LogUI("Rounded Corners", "Skipped header background color update; existing drive detected");
				}
				Logger.LogUI("Header Color Background", $"Applied header color to background: R:{headerColor.Value.r:F2} G:{headerColor.Value.g:F2} B:{headerColor.Value.b:F2}");
			}

			// Preserve color and tint settings
			image.PreserveAspect.Value = true;
			Logger.LogUI("Completion", $"Successfully applied rounded corners to {(isHeader ? "header" : "background")}");

			// Shading overlay slot (node background / title / label)
			EnsureShadingOverlay(image, invertShading, isHeader, preserveOriginalColor, spriteScaleOverride ?? defaultScale);
		}
	}
}

