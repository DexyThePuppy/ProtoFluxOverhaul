using System.Runtime.CompilerServices;

using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;

using Renderite.Shared;

namespace ProtoFluxOverhaul
{
	public static partial class RoundedCornersHelper
	{
		private sealed class NodeMaterialOverrideState
		{
			public IAssetProvider<Material> Original;
			public IAssetProvider<Material> Applied;
		}

		private static readonly ConditionalWeakTable<Image, NodeMaterialOverrideState> NodeMaterialOverrides = new();

		private static bool IsNodeBackgroundImage(Image image)
		{
			var parent = image?.Slot?.Parent;
			// BuildUI creates the node background as its first direct Image child.
			// Later Image siblings, buttons, headers and shading use native materials.
			return parent?.Name == ProtoFluxNodeVisual.SLOT_NAME
				&& parent.GetComponent<ProtoFluxNodeVisual>() != null
				&& ReferenceEquals(parent.FindChild("Image")?.GetComponent<Image>(), image);
		}

		private static void ApplyNodeMaterialOverride(Image image)
		{
			if (image == null || image.IsRemoved || image.World == null) return;
			bool ownsBinding = NativeAssetFallback.IsDrivenByUs(image.Material);
			if (image.Material.IsDriven && !ownsBinding) return;

			bool isBackground = IsNodeBackgroundImage(image);
			var source = isBackground ? UserAssetOverrides.GetMaterialReference(image.World, "Node") : null;
			if (source != null)
			{
				var state = NodeMaterialOverrides.GetValue(image, static key => new NodeMaterialOverrideState
				{
					// A saved native binding can outlive this process's restoration state.
					Original = NativeAssetFallback.IsDrivenByUs(key.Material)
						? key.World.GetDefaultUI_ZWrite() : key.Material.Target
				});
				if (NativeAssetFallback.Bind(image.Slot, image.Material, source, image.World.GetDefaultUI_ZWrite()))
					state.Applied = image.Material.RawTarget;
			}
			else if (NodeMaterialOverrides.TryGetValue(image, out var state))
			{
				// Restore a tracked assignment when the override disappears or the
				// image is no longer the main background, without clearing authored refs.
				bool restore = ownsBinding || image.Material.RawTarget == state.Applied;
				NativeAssetFallback.Release(image.Material);
				if (restore && !image.Material.IsDriven)
					image.Material.Target = state.Original != null && !state.Original.IsRemoved ? state.Original : null;
				NodeMaterialOverrides.Remove(image);
			}
			else if (ownsBinding && NativeAssetFallback.Release(image.Material))
			{
				// Repair a saved binding that no longer targets the main background.
				image.Material.Target = isBackground ? image.World.GetDefaultUI_ZWrite() : null;
			}
		}

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
			ApplyNodeMaterialOverride(shadingImage);
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

			// Sprite provider + shared texture
			var spriteProvider = shadingSlot.GetComponentOrAttach<SpriteProvider>();
			var shadingUrl = invertShading
				? ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.SHADING_INVERTED_TEXTURE)
				: ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.SHADING_TEXTURE);
			SharedAssets.BindSharedTexture(
				spriteProvider.Slot, spriteProvider.Texture,
				invertShading ? "PFO_Tex_ShadingInverted" : "PFO_Tex_Shading",
				shadingUrl,
				clamp: true, textureRole: invertShading ? "ShadingInverted" : "Shading");
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

		private static void ApplyNodeSpriteTexture(SpriteProvider sprite, bool isHeader)
		{
			var textureUrl = ProtoFluxOverhaul.Config.GetValue(isHeader
				? ProtoFluxOverhaul.NODE_BACKGROUND_HEADER_TEXTURE : ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE);
			SharedAssets.BindSharedTexture(sprite.Slot, sprite.Texture,
				isHeader ? "PFO_Tex_NodeHeader" : "PFO_Tex_NodeBackground",
				textureUrl, clamp: true, textureRole: isHeader ? "NodeHeader" : "NodeBackground");
		}

		public static void ApplyRoundedCorners(Image image, bool isHeader = false, colorX? headerColor = null, bool preserveOriginalColor = false, float? spriteScaleOverride = null, bool invertShading = false)
		{
			// Safety check - don't process removed/destroyed components
			if (image == null || image.IsRemoved || image.Slot == null || image.Slot.IsRemoved) return;
			ApplyNodeMaterialOverride(image);

			// Store original color if we need to preserve it
			colorX originalColor = image.Tint.Value;

			// For backgrounds, check if we need to update the tint even if sprite provider exists
			if (image.Sprite.Target is SpriteProvider existingSpriteProvider)
			{
				// Reapplying styling must also pick up a changed profile. Keep shared
				// external sprites and explicitly driven texture references intact.
				if (existingSpriteProvider.Slot.IsChildOf(image.Slot, includeSelf: true)
					&& (!existingSpriteProvider.Texture.IsDriven || NativeAssetFallback.IsDrivenByUs(existingSpriteProvider.Texture)))
					ApplyNodeSpriteTexture(existingSpriteProvider, isHeader);
				// If this is a background and we have a header color and the config is enabled, update the tint
				if (!isHeader && !preserveOriginalColor && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND))
				{
					if (!TrySetColorIfUndriven(image.Tint, headerColor.Value))
						Logger.LogUI("Header Color Background Update", "Skipped tint override; existing drive detected");
					else
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

			ApplyNodeSpriteTexture(spriteProvider, isHeader);

			Logger.LogUI("Texture Setup", $"Set up texture for {(isHeader ? "header" : "background")}");

			// Configure the sprite provider based on the image settings
			spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f); // x:0 y:0 width:1 height:1
			spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f); // x:0.5 y:0 z:0 w:0
			// Default sprite scales:
			// - Label backgrounds (preserveOriginalColor): 0.03f
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
				if (!TrySetColorIfUndriven(image.Tint, originalColor))
					Logger.LogUI("Rounded Corners", "Skipped original color preservation; existing drive detected");
				else
					Logger.LogUI("Color Preserved", $"Preserved original color for connector label: R:{originalColor.r:F2} G:{originalColor.g:F2} B:{originalColor.b:F2}");
			}
			else if (!isHeader && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND))
			{
				if (!TrySetColorIfUndriven(image.Tint, headerColor.Value))
					Logger.LogUI("Rounded Corners", "Skipped header background color update; existing drive detected");
				else
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

