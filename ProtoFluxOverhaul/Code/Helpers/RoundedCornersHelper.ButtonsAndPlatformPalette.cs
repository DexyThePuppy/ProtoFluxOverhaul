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
		public static PlatformColorPalette EnsurePlatformColorPalette(Slot root)
		{
			if (root == null) return null;
			return root.GetComponentOrAttach<PlatformColorPalette>();
		}

		private const string PFO_BUTTON_SLOT_NAME = "PFO_ButtonDrivers";

		/// <summary>
		/// Gets or creates the PFO child slot for button driver components.
		/// </summary>
		private static Slot GetOrCreateButtonPFOSlot(Slot buttonSlot)
		{
			if (buttonSlot == null) return null;
			var slot = buttonSlot.FindChild(PFO_BUTTON_SLOT_NAME) ?? buttonSlot.AddSlot(PFO_BUTTON_SLOT_NAME);
			slot.GetComponentOrAttach<IgnoreLayout>();
			return slot;
		}

		/// <summary>
		/// Attaches a new ValueCopy component to the given slot.
		/// </summary>
		private static ValueCopy<T> AttachValueCopy<T>(Slot slot)
		{
			if (slot == null) return null;
			return slot.AttachComponent<ValueCopy<T>>();
		}

		private static InteractionElement.ColorDriver EnsurePrimaryButtonColorDriver(Button button, Image bgImage)
		{
			if (button == null || bgImage == null) return null;

			// In normal engine flow, Button.OnAttach calls SetupBackgroundColor(bgImage.Tint) which adds ColorDrivers[0].
			if (button.ColorDrivers == null || button.ColorDrivers.Count == 0)
				button.SetupBackgroundColor(bgImage.Tint);

			if (button.ColorDrivers == null || button.ColorDrivers.Count == 0)
				return null;

			var driver = button.ColorDrivers[0];
			if (!driver.ColorDrive.IsLinkValid)
				driver.ColorDrive.Target = bgImage.Tint;

			return driver;
		}

		private static StaticTexture2D EnsureConfiguredTextureOnSpriteProvider(
			SpriteProvider spriteProvider,
			Uri url,
			bool clamp = true)
		{
			if (spriteProvider == null) return null;
			var texture = spriteProvider.Slot.GetComponent<StaticTexture2D>() ?? spriteProvider.Slot.AttachComponent<StaticTexture2D>();

			texture.URL.Value = url;
			texture.FilterMode.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.FILTER_MODE);
			texture.WrapModeU.Value = clamp ? TextureWrapMode.Clamp : ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WRAP_MODE_U);
			texture.WrapModeV.Value = clamp ? TextureWrapMode.Clamp : ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WRAP_MODE_V);
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
			return texture;
		}

		private static (SpriteProvider provider, float? scale, float? fixedSize) EnsureButtonSpriteUsesNodeBackground(Image bgImage)
		{
			if (bgImage == null || ProtoFluxOverhaul.Config == null) return (null, null, null);

			var existingProvider = bgImage.Sprite.Target as SpriteProvider;
			if (existingProvider == null) return (null, null, null);

			float? existingScale = existingProvider.Scale.Value;
			float? existingFixedSize = existingProvider.FixedSize.Value;

			// If the sprite provider is not under this image slot, it's likely shared (e.g. RadiantUI style).
			// Never mutate shared providers: clone onto the button slot and point the image to it.
			bool isLocalProvider = existingProvider.Slot.IsChildOf(bgImage.Slot, includeSelf: true);
			SpriteProvider providerToUse = existingProvider;

			if (!isLocalProvider)
			{
				var localSlot = bgImage.Slot.FindChild("PFO_ButtonSprite") ?? bgImage.Slot.AddSlot("PFO_ButtonSprite");
				localSlot.GetComponentOrAttach<IgnoreLayout>();

				providerToUse = localSlot.GetComponent<SpriteProvider>() ?? localSlot.AttachComponent<SpriteProvider>();

				// Copy essential sprite settings so the button keeps its existing look/scale behavior.
				providerToUse.Rect.Value = existingProvider.Rect.Value;
				providerToUse.Borders.Value = existingProvider.Borders.Value;
				providerToUse.Scale.Value = existingProvider.Scale.Value;
				providerToUse.FixedSize.Value = existingProvider.FixedSize.Value;
				// Note: PreserveAspect is an Image property, not a SpriteProvider property.
				// We intentionally do not touch bgImage.PreserveAspect here.

				bgImage.Sprite.Target = providerToUse;
			}

			// Swap the sprite texture to the node background texture (do not change scale).
			// Also ensure it has standard 9-slice borders for rounded visuals.
			providerToUse.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f);
			providerToUse.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f);

			var url = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE);
			EnsureConfiguredTextureOnSpriteProvider(providerToUse, url, clamp: true);

			// IMPORTANT: return the scale of the provider actually assigned to the image.
			// If we cloned to PFO_ButtonSprite, that's the one we want to match for the shading overlay.
			var activeProvider = bgImage.Sprite.Target as SpriteProvider;
			float? activeScale = activeProvider?.Scale.Value ?? existingScale;
			float? activeFixedSize = activeProvider?.FixedSize.Value ?? existingFixedSize;
			return (providerToUse, activeScale, activeFixedSize);
		}

		public static void ApplyProtoFluxNodeButtonTheme(
			Slot uiRoot,
			PlatformColorPalette palette,
			Image nodeBackgroundImage,
			bool usePlatformPalette,
			bool useColoredNodeBackground)
		{
			if (uiRoot == null) return;

			var buttons = uiRoot.GetComponentsInChildren<Button>();
			foreach (var button in buttons)
			{
				if (button == null) continue;

				// Ensure the background driver targets the button Image tint
				var bgImage = button.Slot.GetComponent<Image>();
				if (bgImage == null) continue;

				// === ALWAYS APPLY: Texture and Shading ===
				// Replace ONLY the underlying texture on the existing button sprite (do not attach a new sprite provider).
				// IMPORTANT: for buttons we want the shading overlay to match the sprite provider *FixedSize* (not Scale).
				var (buttonSpriteProvider, _, buttonSpriteFixedSize) = EnsureButtonSpriteUsesNodeBackground(bgImage);

				// If this button contains a TextEditor, it's effectively a text field / input widget.
				// In that case we want the inverted shading texture (same as headers/labels) for better visual contrast.
				bool invertButtonShading = button.Slot.GetComponentInChildren<TextEditor>() != null;
				EnsureShadingOverlay(
					bgImage,
					invertShading: invertButtonShading,
					isHeader: false,
					preserveOriginalColor: false,
					scaleOverride: null,
					fixedSizeOverride: buttonSpriteFixedSize,
					fixedSizeSource: buttonSpriteProvider?.FixedSize);

				// === OPTIONAL: Color Driving ===
				// Priority: Colored Node Background > PlatformColorPalette > Default
				if (useColoredNodeBackground && nodeBackgroundImage != null)
				{
					// Colored Node Background Mode: drive button BaseColor from node background tint
					var driver = EnsurePrimaryButtonColorDriver(button, bgImage);
					if (driver == null) continue;

					// Set neutral interaction colors so the BaseColor drives the overall hue
					colorX neutral = new colorX(0.80f, 0.80f, 0.80f, 1f);
					driver.SetColors(in neutral);

					// Get or create a single PFO slot for the base color ValueCopy
					var pfoSlot = GetOrCreateButtonPFOSlot(button.Slot);
					if (pfoSlot != null && !button.BaseColor.IsDriven)
					{
						var baseCopy = AttachValueCopy<colorX>(pfoSlot);
						if (baseCopy != null)
							TryLinkValueCopy(baseCopy, nodeBackgroundImage.Tint, button.BaseColor);
					}
				}
				else if (usePlatformPalette && palette != null)
				{
					// Platform Palette Mode: drive colors from palette neutrals
					var driver = EnsurePrimaryButtonColorDriver(button, bgImage);
					if (driver == null) continue;

					// Keep BaseColor neutral so driver colors apply predictably.
					if (!button.BaseColor.IsDriven)
						button.BaseColor.Value = colorX.White;

					// Get or create a single PFO slot for all button driver ValueCopy components
					var pfoSlot = GetOrCreateButtonPFOSlot(button.Slot);
					if (pfoSlot != null)
					{
						// Only set up if not already driven (prevents duplicate components)
						if (!driver.NormalColor.IsDriven)
						{
							var normalCopy = AttachValueCopy<colorX>(pfoSlot);
							if (normalCopy != null)
								TryLinkValueCopy(normalCopy, palette.Neutrals.Mid, driver.NormalColor);
						}

						if (!driver.HighlightColor.IsDriven)
						{
							var highlightCopy = AttachValueCopy<colorX>(pfoSlot);
							if (highlightCopy != null)
								TryLinkValueCopy(highlightCopy, palette.Neutrals.Light, driver.HighlightColor);
						}

						if (!driver.PressColor.IsDriven)
						{
							var pressCopy = AttachValueCopy<colorX>(pfoSlot);
							if (pressCopy != null)
								TryLinkValueCopy(pressCopy, palette.Neutrals.Dark, driver.PressColor);
						}

						if (!driver.DisabledColor.IsDriven)
						{
							var disabledCopy = AttachValueCopy<colorX>(pfoSlot);
							if (disabledCopy != null)
								TryLinkValueCopy(disabledCopy, palette.Neutrals.MidLight, driver.DisabledColor);
						}

						// Also drive label color from palette
						if (button.Label != null && !button.Label.Color.IsDriven)
						{
							var textCopy = AttachValueCopy<colorX>(pfoSlot);
							if (textCopy != null)
								TryLinkValueCopy(textCopy, palette.Neutrals.Light, button.Label.Color);
						}
					}
				}
				// Note: When neither mode is enabled, buttons keep their original colors
				// but still get the texture and shading applied
			}
		}
	}
}

