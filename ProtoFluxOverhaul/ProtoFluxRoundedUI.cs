using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using Elements.Core;
using System;
using System.Collections.Generic;
using Elements.Assets;
using System.Linq;
using System.Reflection;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using FrooxEngine.ProtoFlux.CoreNodes;
using Renderite.Shared;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul {
    // Shared permission check method for all patches
    public static class PermissionHelper {
        public static bool HasPermission(ProtoFluxNodeVisual instance) {
            if (instance == null || instance.World == null) {
                return false;
            }

            // Check the visual component's node first
            var node = instance.Node.Target;
            if (node != null) {
                // Primary check - Check the node's slot ownership
                node.Slot.ReferenceID.ExtractIDs(out ulong nodePosition, out byte nodeUser);
                User nodeSlotAllocUser = instance.World.GetUserByAllocationID(nodeUser);

                // Check if the node slot belongs to the local user
                if (nodeSlotAllocUser == null || nodePosition < nodeSlotAllocUser.AllocationIDStart) {
                    // Secondary check - Check the node component ownership
                    node.ReferenceID.ExtractIDs(out ulong nodeCompPosition, out byte nodeCompUser);
                    User nodeComponentAllocUser = instance.World.GetUserByAllocationID(nodeCompUser);

                    // If neither the slot nor component belong to the local user, check group nodes
                    if (nodeComponentAllocUser == null || 
                        nodeCompPosition < nodeComponentAllocUser.AllocationIDStart || 
                        nodeComponentAllocUser != instance.LocalUser) {
                        
                        // Additional check for node group ownership through its nodes
                        var group = node.Group;
                        if (group != null) {
                            // Check if any node in the group belongs to a different user
                            foreach (var groupNode in group.Nodes) {
                                if (groupNode == null) continue;
                                
                                groupNode.ReferenceID.ExtractIDs(out ulong groupNodePosition, out byte groupNodeUser);
                                User groupNodeAllocUser = instance.World.GetUserByAllocationID(groupNodeUser);

                                if (groupNodeAllocUser != null && 
                                    groupNodePosition >= groupNodeAllocUser.AllocationIDStart && 
                                    groupNodeAllocUser != instance.LocalUser) {
                                    return false;
                                }
                            }
                        }
                    }
                }
                else if (nodeSlotAllocUser != instance.LocalUser) {
                    return false;
                }
            }

            // Then check the visual component's own ownership
            instance.ReferenceID.ExtractIDs(out ulong position, out byte user);
            User visualAllocUser = instance.World.GetUserByAllocationID(user);

            if (visualAllocUser == null || position < visualAllocUser.AllocationIDStart) {
                // Check the slot ownership as fallback
                instance.Slot.ReferenceID.ExtractIDs(out ulong slotPosition, out byte slotUser);
                User slotAllocUser = instance.World.GetUserByAllocationID(slotUser);

                if (slotAllocUser == null || 
                    slotPosition < slotAllocUser.AllocationIDStart || 
                    slotAllocUser != instance.LocalUser) {
                    return false;
                }
                return true;
            }

            if (visualAllocUser != instance.LocalUser) {
                return false;
            }

            return true;
        }

        public static bool HasPermission(ProtoFluxWireManager instance) {
            try {
                if (instance == null || instance.World == null) {
                    return false;
                }

                // Skip permission check if we're not the authority
                if (!instance.World.IsAuthority) return false;

                // Get the wire's owner
                instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
                User wirePointAllocUser = instance.World.GetUserByAllocationID(user);
                
                if (wirePointAllocUser == null || position < wirePointAllocUser.AllocationIDStart) {
                    instance.ReferenceID.ExtractIDs(out ulong position1, out byte user1);
                    User instanceAllocUser = instance.World.GetUserByAllocationID(user1);
                    
                    // Allow the wire owner or admins to modify
                    return (instanceAllocUser != null && 
                           position1 >= instanceAllocUser.AllocationIDStart &&
                           (instanceAllocUser == instance.LocalUser || instance.LocalUser.IsHost));
                }
                
                // Allow the wire owner or admins to modify
                return wirePointAllocUser == instance.LocalUser || instance.LocalUser.IsHost;
            }
            catch (Exception) {
                // If anything goes wrong, deny permission to be safe
                return false;
            }
        }
    }

    // Helper class for overview mode access
    public static class OverviewModeHelper {
        public static bool GetOverviewMode(User user) {
            try {
                // First, try to get overview mode from any active ProtoFluxTool
                var activeTools = user.GetActiveTools();
                foreach (var tool in activeTools) {
                    if (tool is ProtoFluxTool protoFluxTool) {
                        // Use reflection to access the protected OverviewMode property
                        var overviewModeProperty = typeof(ProtoFluxTool).GetProperty("OverviewMode", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (overviewModeProperty != null) {
                            return (bool)overviewModeProperty.GetValue(protoFluxTool);
                        }
                    }
                }
                
                // Fallback: Access ProtofluxUserEditSettings directly (original method)
                var settings = user.GetComponent<ProtofluxUserEditSettings>();
                return settings != null && settings.OverviewMode.Value;
            }
            catch (Exception e) {
                Logger.LogError("Failed to get overview mode", e, LogCategory.UI);
                // Fallback to settings approach
                var settings = user.GetComponent<ProtofluxUserEditSettings>();
                return settings != null && settings.OverviewMode.Value;
            }
        }
    }

    // Helper class for shared functionality
    public static class RoundedCornersHelper {
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
            if (impulseProxy != null) {
                return impulseProxy.ImpulseType.Value.GetImpulseColor().MulRGB(1.5f);
            }

            // Check for operation proxy (flow input)
            var operationProxy = connectorSlot.GetComponent<ProtoFluxOperationProxy>();
            if (operationProxy != null) {
                return DatatypeColorHelper.GetOperationColor(operationProxy.IsAsync.Value).MulRGB(1.5f);
            }

            // Check for input proxy (value input)
            var inputProxy = connectorSlot.GetComponent<ProtoFluxInputProxy>();
            if (inputProxy != null && inputProxy.InputType.Value != null) {
                return inputProxy.InputType.Value.GetTypeColor().MulRGB(1.5f);
            }

            // Check for output proxy (value output)
            var outputProxy = connectorSlot.GetComponent<ProtoFluxOutputProxy>();
            if (outputProxy != null && outputProxy.OutputType.Value != null) {
                return outputProxy.OutputType.Value.GetTypeColor().MulRGB(1.5f);
            }

            // Check for reference proxy
            var refProxy = connectorSlot.GetComponent<ProtoFluxReferenceProxy>();
            if (refProxy != null && refProxy.ValueType.Value != null) {
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

        public static bool TryLinkValueCopy<T>(ValueCopy<T> copy, IField<T> sourceField, IField<T> targetField)
        {
            if (copy == null || sourceField == null || targetField == null)
                return false;

            // Avoid stacking multiple drives on the same field
            if (targetField.IsDriven)
                return false;

            copy.Source.Target = sourceField;
            copy.Target.Target = targetField;
            copy.WriteBack.Value = false;
            return true;
        }

        public static bool TryLinkValueDriver(ValueDriver<colorX> driver, IField<colorX> targetField, IValue<colorX> sourceValue)
        {
            if (driver == null || targetField == null || sourceValue == null)
                return false;

            if (targetField.IsDriven)
                return false;

            if (driver.DriveTarget.IsLinkValid)
            {
                if (driver.DriveTarget.Target == targetField && driver.ValueSource.Target == sourceValue)
                    return true;
                return false;
            }

            driver.DriveTarget.Target = targetField;
            driver.ValueSource.Target = sourceValue;
            return true;
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
            if (hostImage == null || hostImage.Slot == null) return;
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

        public static void ApplyRoundedCorners(Image image, bool isHeader = false, colorX? headerColor = null, bool preserveOriginalColor = false, float? spriteScaleOverride = null, bool invertShading = false) {
            // Store original color if we need to preserve it
            colorX originalColor = image.Tint.Value;
            
            // For backgrounds, check if we need to update the tint even if sprite provider exists
            if (image.Sprite.Target is SpriteProvider existingSpriteProvider) {
                // If this is a background and we have a header color and the config is enabled, update the tint
                if (!isHeader && !preserveOriginalColor && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
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
            texture.URL.Value = isHeader ? 
                ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_HEADER_TEXTURE) :
                ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE);
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
            spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f);  // x:0 y:0 width:1 height:1
            spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f);  // x:0.5 y:0 z:0 w:0
            // Default sprite scales:
            // - Label backgrounds (preserveOriginalColor): 0.02f
            // - Header: 0.05f
            // - Background: 0.09f
            float defaultScale = preserveOriginalColor ? 0.03f : (isHeader ? 0.05f : 0.09f);
            spriteProvider.Scale.Value = spriteScaleOverride ?? defaultScale;
            spriteProvider.FixedSize.Value = 1.00f;  // FixedSize: 1.00
            Logger.LogUI("Sprite Config", $"Configured {(isHeader ? "header" : "background")} sprite provider settings");

            // Update the image to use the sprite
            image.Sprite.Target = spriteProvider;
            
            // Apply color logic
            if (preserveOriginalColor) {
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
            } else if (!isHeader && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
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

    // Helper class for wire-related functionality
    public static class WireHelper {
        public static void CreateAudioClipsSlot(Slot wirePointSlot) {
            if (wirePointSlot == null || wirePointSlot.World == null) return;

            // Initialize sounds using ProtoFluxSounds
            ProtoFluxSounds.Initialize(wirePointSlot.World);
        }

        public static void FindAndSetupWirePoints(Slot rootSlot) {
            if (rootSlot == null) return;

            // Find the Overlapping Layout section
            var overlappingLayout = rootSlot.FindChild("Overlapping Layout");
            if (overlappingLayout == null) return;

            // Check Inputs & Operations section
            var inputsAndOperations = overlappingLayout.FindChild("Inputs & Operations");
            if (inputsAndOperations != null) {
                foreach (var connectorSlot in inputsAndOperations.GetComponentsInChildren<Slot>()) {
                    if (connectorSlot.Name == "Connector") {
                        var wirePoint = connectorSlot.FindChild("<WIRE_POINT>");
                        if (wirePoint != null) {
                            Logger.LogWire("Setup", $"Found input wire point in {connectorSlot.Parent?.Name ?? "unknown"}");
                            CreateAudioClipsSlot(wirePoint);
                        }
                    }
                }
            }

            // Check Outputs & Impulses section
            var outputsAndImpulses = overlappingLayout.FindChild("Outputs & Impulses");
            if (outputsAndImpulses != null) {
                foreach (var connectorSlot in outputsAndImpulses.GetComponentsInChildren<Slot>()) {
                    if (connectorSlot.Name == "Connector") {
                        var wirePoint = connectorSlot.FindChild("<WIRE_POINT>");
                        if (wirePoint != null) {
                            Logger.LogWire("Setup", $"Found output wire point in {connectorSlot.Parent?.Name ?? "unknown"}");
                            CreateAudioClipsSlot(wirePoint);
                        }
                    }
                }
            }
        }
    }

    // Patch to add rounded corners to ProtoFlux node visuals
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "BuildUI")]
    public class ProtoFluxNodeVisual_BuildUI_Patch {
        // ColorMyProtoFlux color settings
        private static readonly colorX NODE_CATEGORY_TEXT_LIGHT_COLOR = new colorX(0.75f);
        private static readonly colorX NODE_CATEGORY_TEXT_DARK_COLOR = new colorX(0.25f);

        // Cache for shared sprite provider
        private static readonly Dictionary<(Slot, bool), SpriteProvider> connectorSpriteCache = new Dictionary<(Slot, bool), SpriteProvider>();

        private static Dictionary<(Slot, bool), SpriteProvider> callConnectorSpriteCache = new Dictionary<(Slot, bool), SpriteProvider>();

        // Cache for vector connector sprite providers (int = vector size: 2, 3, 4)
        private static readonly Dictionary<(Slot, bool, int), SpriteProvider> vectorConnectorSpriteCache = new Dictionary<(Slot, bool, int), SpriteProvider>();

        /// <summary>
        /// Determines if a connector should use the Call sprite based on its type
        /// </summary>
        private static bool ShouldUseCallConnector(ImpulseType? impulseType, bool isOperation = false, bool isAsync = false) {
            // If it's any ImpulseType, use the flow connector
            if (impulseType.HasValue) {
                return true;
            }
            
            // For operations, check if it's a flow connector
            if (isOperation) {
                return true; // Operations use flow connectors
            }
            
            return false;
        }

        /// <summary>
        /// Determines if a type should be treated as a reference type for connector texture
        /// </summary>
        private static bool IsReferenceType(System.Type type) {
            if (type == null) return false;

            // Logging to understand type detection
            Logger.LogUI("Reference Detection", $"Analyzing type: {type.FullName}");

            // Universal reference type detection strategies

            // 1. Check for known reference-related interfaces and base classes
            if (typeof(IWorldElement).IsAssignableFrom(type) ||
                typeof(Component).IsAssignableFrom(type) ||
                typeof(INode).IsAssignableFrom(type)) {
                Logger.LogUI("Reference Detection", $"Type matches known reference interfaces: {type.FullName}");
                return true;
            }

            // 2. Check for generic reference types
            if (type.IsGenericType) {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                // Common reference-like generic patterns
                string[] referenceGenericPatterns = {
                    "SyncRef`1", "RelayRef`1", "RefProxy`1", 
                    "SyncRef", "RelayRef", "RefProxy"
                };

                if (referenceGenericPatterns.Any(pattern => 
                    genericTypeDef.Name.StartsWith(pattern))) {
                    Logger.LogUI("Reference Detection", $"Type matches reference generic pattern: {type.FullName}");
                    return true;
                }
            }

            // 3. Check for ProtoFlux-specific reference proxies
            if (typeof(ProtoFluxRefProxy).IsAssignableFrom(type)) {
                Logger.LogUI("Reference Detection", $"Type is a ProtoFlux reference proxy: {type.FullName}");
                return true;
            }

            // 4. Reflection-based heuristics for reference-like types
            try {
                // Look for properties/methods typical of reference types
                var targetProperty = type.GetProperty("Target");
                var referenceProperty = type.GetProperty("Reference");
                var nodeProperty = type.GetProperty("Node");

                if ((targetProperty != null && !targetProperty.PropertyType.IsPrimitive) ||
                    (referenceProperty != null && !referenceProperty.PropertyType.IsPrimitive) ||
                    (nodeProperty != null && !nodeProperty.PropertyType.IsPrimitive)) {
                    Logger.LogUI("Reference Detection", $"Type has reference-like properties: {type.FullName}");
                    return true;
                }
            }
            catch (Exception ex) {
                // Log any reflection errors, but don't prevent detection
                Logger.LogUI("Reference Detection", $"Reflection error analyzing {type.FullName}: {ex.Message}");
            }

            // 5. Last resort: check if type is not a value type and not a primitive
            if (!type.IsValueType && type != typeof(string) && !type.IsPrimitive) {
                Logger.LogUI("Reference Detection", $"Type is non-value, non-primitive type: {type.FullName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the connector dimension using official ProtoFlux data type analysis.
        /// Uses the same logic as Resonite's official GetTypeConnectorSprite method.
        /// </summary>
        private static int GetConnectorDimensionFromProxy(Slot connectorSlot) {
            // Add debugging to see what proxies we find
            var allComponents = connectorSlot.GetComponentsInParents<Component>().Where(c => c.GetType().Name.Contains("Proxy")).ToList();
            Logger.LogUI("Proxy Debug", $"Found proxy components in hierarchy for slot {connectorSlot.Name}: {string.Join(", ", allComponents.Select(c => c.GetType().Name))}");
            
            // Check for Impulse/Operation connectors first - these should NOT use vector textures
            var impulseProxy = connectorSlot.GetComponent<ProtoFluxImpulseProxy>();
            var operationProxy = connectorSlot.GetComponent<ProtoFluxOperationProxy>();
            if (impulseProxy != null || operationProxy != null) {
                return -1; // Signal that this is an Impulse connector, not a vector
            }
            
            // Look for Input/Output proxy components that contain the actual type information
            var inputProxy = connectorSlot.GetComponent<ProtoFluxInputProxy>();
            if (inputProxy != null && inputProxy.InputType.Value != null) {
                Logger.LogUI("Input Proxy", $"Found InputProxy with type {inputProxy.InputType.Value.Name} on slot {connectorSlot.Name}");
                
                // Universal reference type detection
                if (IsReferenceType(inputProxy.InputType.Value)) {
                    Logger.LogUI("Reference Detection", $"Reference type detected: {inputProxy.InputType.Value.Name} on slot {connectorSlot.Name}");
                    return -2; // Use regular connector texture
                }
                
                int dimension = GetDimensionFromType(inputProxy.InputType.Value);
                Logger.LogUI("Connector Dimension", $"Input connector for type {inputProxy.InputType.Value.Name} resolved to dimension {dimension}");
                return dimension;
            }
            
            var outputProxy = connectorSlot.GetComponent<ProtoFluxOutputProxy>();
            if (outputProxy != null && outputProxy.OutputType.Value != null) {
                Logger.LogUI("Output Proxy", $"Found OutputProxy with type {outputProxy.OutputType.Value.Name} on slot {connectorSlot.Name}");
                
                // Universal reference type detection for output proxies
                if (IsReferenceType(outputProxy.OutputType.Value)) {
                    Logger.LogUI("Reference Detection", $"Reference type detected: {outputProxy.OutputType.Value.Name} on slot {connectorSlot.Name}");
                    return -2; // Use regular connector texture
                }
                
                int dimension = GetDimensionFromType(outputProxy.OutputType.Value);
                Logger.LogUI("Connector Dimension", $"Output connector for type {outputProxy.OutputType.Value.Name} resolved to dimension {dimension}");
                return dimension;
            }
            
            // Check for Reference connectors - these should NEVER use vector textures
            var refProxy = connectorSlot.GetComponentInParents<ProtoFluxRefProxy>();
            var referenceProxy = connectorSlot.GetComponentInParents<ProtoFluxReferenceProxy>();
            var globalRefProxy = connectorSlot.GetComponentInParents<ProtoFluxGlobalRefProxy>();
            if (refProxy != null || referenceProxy != null || globalRefProxy != null) {
                Logger.LogUI("Reference Connector", $"Reference connector detected in slot {connectorSlot.Name} (RefProxy: {refProxy != null}, ReferenceProxy: {referenceProxy != null}, GlobalRefProxy: {globalRefProxy != null}), using regular texture");
                return -2; // Signal that this is a Reference connector, should use regular texture
            }
            
            return 0; // No type information found
        }
        
        /// <summary>
        /// Uses the exact same logic as Resonite's official GetTypeConnectorSprite method
        /// to determine connector dimension from a System.Type
        /// </summary>
        private static int GetDimensionFromType(System.Type type) {
            // This mirrors the official logic from DatatypeColorHelper.GetTypeConnectorSprite
            if (typeof(IVector).IsAssignableFrom(type)) {
                int dimensions = type.GetVectorDimensions(); // Returns 2, 3, or 4
                Logger.LogUI("Vector Detection", $"Type {type.Name} is vector with {dimensions} dimensions");
                return dimensions;
            }
            Logger.LogUI("Vector Detection", $"Type {type.Name} is NOT a vector, using dimension 1");
            return 1; // All non-vector types use Dim1
        }

        /// <summary>
        /// Creates or retrieves a shared sprite provider for the connector image
        /// </summary>
        public static SpriteProvider GetOrCreateSharedConnectorSprite(Slot slot, bool isOutput, ImpulseType? impulseType = null, bool isOperation = false, bool isAsync = false) {
            // Check if this should use the Call connector
            if (ShouldUseCallConnector(impulseType, isOperation, isAsync)) {
                return GetOrCreateSharedCallConnectorSprite(slot, isOutput);
            }
            
            // Check if this should use a Vector connector using official ProtoFlux type data
            int connectorDimension = GetConnectorDimensionFromProxy(slot);
            if (connectorDimension > 0) {
                Logger.LogUI("Vector Connector", $"Using official connector Dim{connectorDimension} for connector in slot {slot.Name} (isOutput: {isOutput})");
                return GetOrCreateSharedVectorConnectorSprite(slot, isOutput, connectorDimension);
            }
            // If connectorDimension is -1, it means this is an Impulse connector, so skip vector logic
            // If connectorDimension is -2, it means this is a Reference connector, so skip vector logic
            
            var cacheKey = (slot, isOutput);
            
            // Check cache first
            if (connectorSpriteCache.TryGetValue(cacheKey, out var cachedProvider)) {
                return cachedProvider;
            }

            // Create organized hierarchy under __TEMP
            var tempSlot = slot.World.RootSlot.FindChild("__TEMP") ?? slot.World.RootSlot.AddSlot("__TEMP", false);
            var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
            var userSlot = modSlot.FindChild(slot.LocalUser.UserName) ?? modSlot.AddSlot(slot.LocalUser.UserName, false);
            var spritesSlot = userSlot.FindChild("Sprites") ?? userSlot.AddSlot("Sprites", false);
            var spriteSlot = spritesSlot.FindChild(isOutput ? "Output" : "Input") ?? 
                            spritesSlot.AddSlot(isOutput ? "Output" : "Input", false);

            // Create sprite provider
            SpriteProvider spriteProvider = spriteSlot.GetComponentOrAttach<SpriteProvider>();

            // Ensure cleanup when user leaves
            userSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;

            // Set up the texture if not already set
            if (spriteProvider.Texture.Target == null) {
                var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
                texture.URL.Value = isOutput ? 
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE) : 
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE);
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
                spriteProvider.Rect.Value = !isOutput ? 
                    new Rect(0f, 0f, 1f, 1f) :    // Inputs (left) normal orientation
                    new Rect(1f, 0f, -1f, 1f);    // Outputs (right) flipped
                spriteProvider.Scale.Value = 1.0f;
                spriteProvider.FixedSize.Value = 16f; // Match the RectTransform width
                spriteProvider.Borders.Value = new float4(0f, 0f, 0.0001f, 0f); // x=0, y=0, z=0.01, w=0
            }

            // Cache the provider
            connectorSpriteCache[cacheKey] = spriteProvider;

            return spriteProvider;
        }

        /// <summary>
        /// Creates or retrieves a shared sprite provider for the Call connector image
        /// </summary>
        public static SpriteProvider GetOrCreateSharedCallConnectorSprite(Slot slot, bool isOutput) {
            var cacheKey = (slot, isOutput);
            
            // Check cache first
            if (callConnectorSpriteCache.TryGetValue(cacheKey, out var cachedProvider)) {
                return cachedProvider;
            }

            // Create organized hierarchy under __TEMP
            var tempSlot = slot.World.RootSlot.FindChild("__TEMP") ?? slot.World.RootSlot.AddSlot("__TEMP", false);
            var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
            var userSlot = modSlot.FindChild(slot.LocalUser.UserName) ?? modSlot.AddSlot(slot.LocalUser.UserName, false);
            var spritesSlot = userSlot.FindChild("Sprites") ?? userSlot.AddSlot("Sprites", false);
            var spriteSlot = spritesSlot.FindChild(isOutput ? "CallOutput" : "CallInput") ?? 
                            spritesSlot.AddSlot(isOutput ? "CallOutput" : "CallInput", false);

            // Create sprite provider
            SpriteProvider spriteProvider = spriteSlot.GetComponentOrAttach<SpriteProvider>();

            // Ensure cleanup when user leaves
            userSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;

            // Set up the texture if not already set
            if (spriteProvider.Texture.Target == null) {
                var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
                texture.URL.Value = isOutput ? 
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CALL_CONNECTOR_OUTPUT_TEXTURE) : 
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CALL_CONNECTOR_INPUT_TEXTURE);
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
                spriteProvider.Rect.Value = !isOutput ? 
                    new Rect(0f, 0f, 1f, 1f) :    // Inputs (left) normal orientation
                    new Rect(1f, 0f, -1f, 1f);    // Outputs (right) flipped
                spriteProvider.Scale.Value = 1.0f;
                spriteProvider.FixedSize.Value = 16f; // Match the RectTransform width
                spriteProvider.Borders.Value = new float4(0f, 0f, 0.0001f, 0f); // x=0, y=0, z=0.01, w=0
            }

            // Cache the provider
            callConnectorSpriteCache[cacheKey] = spriteProvider;

            return spriteProvider;
        }

        /// <summary>
        /// Creates or retrieves a shared sprite provider for vector connector images
        /// </summary>
        public static SpriteProvider GetOrCreateSharedVectorConnectorSprite(Slot slot, bool isOutput, int vectorSize) {
            var cacheKey = (slot, isOutput, vectorSize);
            
            // Check cache first
            if (vectorConnectorSpriteCache.TryGetValue(cacheKey, out var cachedProvider)) {
                return cachedProvider;
            }

            // Create organized hierarchy under __TEMP
            var tempSlot = slot.World.RootSlot.FindChild("__TEMP") ?? slot.World.RootSlot.AddSlot("__TEMP", false);
            var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
            var userSlot = modSlot.FindChild(slot.LocalUser.UserName) ?? modSlot.AddSlot(slot.LocalUser.UserName, false);
            var spritesSlot = userSlot.FindChild("Sprites") ?? userSlot.AddSlot("Sprites", false);
            var spriteSlot = spritesSlot.FindChild($"Vector{vectorSize}{(isOutput ? "Output" : "Input")}") ?? 
                            spritesSlot.AddSlot($"Vector{vectorSize}{(isOutput ? "Output" : "Input")}", false);

            // Create sprite provider
            SpriteProvider spriteProvider = spriteSlot.GetComponentOrAttach<SpriteProvider>();

            // Ensure cleanup when user leaves
            userSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;

            // Set up the texture if not already set
            if (spriteProvider.Texture.Target == null) {
                var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
                
                // Get the appropriate texture URL based on vector size
                Uri textureUrl;
                switch (vectorSize) {
                    case 1:
                        textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE);
                        Logger.LogUI("Texture Selection", $"Vector size {vectorSize} -> Using X1 texture: {textureUrl}");
                        break;
                    case 2:
                        textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.VECTOR_X1_CONNECTOR_TEXTURE);
                        Logger.LogUI("Texture Selection", $"Vector size {vectorSize} -> Using X2 texture: {textureUrl}");
                        break;
                    case 3:
                        textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.VECTOR_X2_CONNECTOR_TEXTURE);
                        Logger.LogUI("Texture Selection", $"Vector size {vectorSize} -> Using X3 texture: {textureUrl}");
                        break;
                    case 4:
                        textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.VECTOR_X3_CONNECTOR_TEXTURE);
                        Logger.LogUI("Texture Selection", $"Vector size {vectorSize} -> Using X4 texture: {textureUrl}");
                        break;
                    default:
                        // Fallback to regular connector texture
                        textureUrl = isOutput ? 
                            ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE) : 
                            ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE);
                        Logger.LogUI("Texture Selection", $"Vector size {vectorSize} (fallback) -> Using regular texture: {textureUrl}");
                        break;
                }
                
                texture.URL.Value = textureUrl;
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
                spriteProvider.Rect.Value = !isOutput ? 
                    new Rect(0f, 0f, 1f, 1f) :    // Inputs (left) normal orientation
                    new Rect(1f, 0f, -1f, 1f);    // Outputs (right) flipped
                spriteProvider.Scale.Value = 1.0f;
                spriteProvider.FixedSize.Value = 16f; // Match the RectTransform width
                spriteProvider.Borders.Value = new float4(0f, 0f, 0.0001f, 0f); // x=0, y=0, z=0.01, w=0
            }

            // Cache the provider
            vectorConnectorSpriteCache[cacheKey] = spriteProvider;

            return spriteProvider;
        }

        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ProtoFluxNode node) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // Audio is now handled on-demand by ProtoFluxSounds

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // === Remove rich text formatting tags from engine's node names only ===
                // ProtoFlux nodes like Dot, Cross, Transpose use <br> and <size=X%> for formatting
                // We strip these to show names on a single line with uniform text size
                // IMPORTANT: Only target the engine's original node name texts, not mod-created text
                string originalNodeName = node.NodeName;
                if (originalNodeName != null && (originalNodeName.Contains("<br>") || originalNodeName.Contains("<size="))) {
                    // Find text components that contain the original node name (engine-created)
                    var textComponents = ui.Root.GetComponentsInChildren<Text>();
                    foreach (var text in textComponents) {
                        // Only process if the content matches the original node name exactly
                        if (text.Content.Value == originalNodeName) {
                            var content = text.Content.Value;
                            content = content.Replace("<br>", " ");
                            // Remove <size=X%> tags using regex pattern
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"<size=[^>]*>", "");
                            content = content.Replace("</size>", "");
                            text.Content.Value = content;
                        }
                    }
                }

                // Find and setup all wire points in the hierarchy
                WireHelper.FindAndSetupWirePoints(ui.Root);

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

                // Find all connector images in the hierarchy
                var connectorSlots = ui.Root.GetComponentsInChildren<Image>()
                    .Where(img => img.Slot.Name == "Connector")
                    .ToList();

                foreach (var connectorImage in connectorSlots) {
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
                    }
                    else if (operationProxy != null) {
                        isOperation = true;
                        isAsync = operationProxy.IsAsync.Value;
                        originalTypeColor = DatatypeColorHelper.GetOperationColor(isAsync).MulRGB(1.5f);
                    }
                    else if (inputProxy != null && inputProxy.InputType.Value != null) {
                        originalTypeColor = inputProxy.InputType.Value.GetTypeColor().MulRGB(1.5f);
                    }
                    else if (outputProxy != null && outputProxy.OutputType.Value != null) {
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
                }

                // Get the background image using reflection
                var bgImageRef = (SyncRef<Image>)AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage").GetValue(__instance);
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
                        
                        // === Inner driver: handles IsHighlighted (Dark vs Mid) ===
                        // This drives an intermediate field that becomes the "base" for the outer driver
                        var highlightColorField = pfoSlot.GetComponentOrAttach<ValueField<colorX>>();
                        
                        var highlightDriver = pfoSlot.GetComponentOrAttach<BooleanValueDriver<colorX>>();
                        highlightDriver.TargetField.Target = highlightColorField.Value;
                        
                        // FalseValue: Dark (normal state)
                        if (!highlightDriver.FalseValue.IsDriven) {
                            var darkCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
                            RoundedCornersHelper.TryLinkValueCopy(darkCopy, palette.Neutrals.Dark, highlightDriver.FalseValue);
                        }
                        
                        // TrueValue: Mid (highlighted state)
                        if (!highlightDriver.TrueValue.IsDriven) {
                            var midCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
                            RoundedCornersHelper.TryLinkValueCopy(midCopy, palette.Neutrals.Mid, highlightDriver.TrueValue);
                        }
                        
                        // Drive State from IsHighlighted
                        if (!highlightDriver.State.IsDriven) {
                            var highlightStateCopy = pfoSlot.AttachComponent<ValueCopy<bool>>();
                            highlightStateCopy.Source.Target = __instance.IsHighlighted;
                            highlightStateCopy.Target.Target = highlightDriver.State;
                        }
                        
                        // === Outer driver: handles IsSelected (base vs MidLight) ===
                        // This drives the actual bgImage.Tint
                        var selectDriver = pfoSlot.AttachComponent<BooleanValueDriver<colorX>>();
                        selectDriver.TargetField.Target = bgImage.Tint;
                        
                        // FalseValue: copy from highlight driver result (Dark or Mid)
                        if (!selectDriver.FalseValue.IsDriven) {
                            var baseCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
                            baseCopy.Source.Target = highlightColorField.Value;
                            baseCopy.Target.Target = selectDriver.FalseValue;
                        }
                        
                        // TrueValue: MidLight (selected state - highest priority)
                        if (!selectDriver.TrueValue.IsDriven) {
                            var midLightCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();
                            RoundedCornersHelper.TryLinkValueCopy(midLightCopy, palette.Neutrals.MidLight, selectDriver.TrueValue);
                        }
                        
                        // Drive State from IsSelected
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

                // Find the header panel (it's the first Image with HEADER color)
                var headerPanel = ui.Root.GetComponentsInChildren<Image>()
                    .FirstOrDefault(img => img.Tint.Value == RadiantUI_Constants.HEADER);

                // Check if we found a header panel or need to look for spacer slot
                bool hasHeader = headerPanel != null;
                bool usingSpacerSlot = false;
                Slot spacerSlot = null;
                Text headerText = null;

                if (hasHeader) {
                    Logger.LogUI("Header Processing", $"Found header panel for node type: {node.GetType().Name}");
                    // Get the text component that's a sibling
                    headerText = headerPanel.Slot.Parent.GetComponentInChildren<Text>();
                    if (headerText == null) {
                        Logger.LogUI("Header Processing", "Header panel found but no text component - skipping TitleParent creation");
                        return;
                    }
                } else {
                    Logger.LogUI("Header Processing", $"No header panel found for node type: {node.GetType().Name} - looking for spacer slot");
                    
                    // Look for empty spacer slot (first child with no components except RectTransform and LayoutElement)
                    foreach (var child in ui.Root.Children) {
                        var components = child.GetComponents<Component>();
                        // Check if it's a spacer slot (only has basic layout components)
                        if (components.Count() <= 3 && // RectTransform, LayoutElement, and maybe one more
                            child.GetComponent<RectTransform>() != null &&
                            child.GetComponent<LayoutElement>() != null &&
                            child.GetComponent<Image>() == null &&
                            child.GetComponent<Text>() == null) {
                            spacerSlot = child;
                            usingSpacerSlot = true;
                            Logger.LogUI("Header Processing", $"Found spacer slot for node type: {node.GetType().Name}");
                            
                            // Create the missing header components in the spacer slot
                            Logger.LogUI("Header Creation", "Creating Image and Text components for spacer slot");
                            
                            // Create Image component
                            var spacerImage = spacerSlot.AttachComponent<Image>();
                            if (usePlatformPalette && palette != null) {
                                var spacerCopy = spacerImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                if (!RoundedCornersHelper.TryLinkValueCopy(spacerCopy, palette.Dark.Cyan, spacerImage.Tint)) {
                                    Logger.LogUI("Header Creation", "Skipped spacer palette tint copy; existing drive detected");
                                }
                            } else {
                                // Drive the spacer image tint to prevent changes over time
                                var spacerColorField = spacerImage.Slot.GetComponentOrAttach<ValueField<colorX>>();
                                spacerColorField.Value.Value = RadiantUI_Constants.HEADER;
                                var spacerColorDriver = spacerImage.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                                
                                // Only link if the target is not already linked
                                if (!RoundedCornersHelper.TryLinkValueDriver(spacerColorDriver, spacerImage.Tint, spacerColorField.Value))
                                {
                                    Logger.LogUI("Header Creation", "Skipped spacer tint override; existing drive detected");
                                }
                            }
                            
                            // Create Text component in a child slot
                            var spacerTextSlot = spacerSlot.AddSlot("Text");
                            var spacerText = spacerTextSlot.AttachComponent<Text>();
                            var spacerNodeName = node.NodeName?.Replace("<br>", " ") ?? "";
                            spacerNodeName = System.Text.RegularExpressions.Regex.Replace(spacerNodeName, @"<size=[^>]*>", "");
                            spacerNodeName = spacerNodeName.Replace("</size>", "");
                            spacerText.Content.Value = spacerNodeName;
                            spacerText.HorizontalAlign.Value = TextHorizontalAlignment.Center;
                            spacerText.VerticalAlign.Value = TextVerticalAlignment.Middle;
                            spacerText.Size.Value = 64f;
                            spacerText.Color.Value = colorX.White;
                            
                            // Set up the text's RectTransform to fill the parent
                            var spacerTextRect = spacerText.RectTransform;
                            spacerTextRect.AnchorMin.Value = new float2(0f, 0f);
                            spacerTextRect.AnchorMax.Value = new float2(1f, 1f);
                            spacerTextRect.OffsetMin.Value = new float2(0f, 0f);
                            spacerTextRect.OffsetMax.Value = new float2(0f, 0f);
                            
                            // Now we can treat this as a regular header
                            headerPanel = spacerImage;
                            headerText = spacerText;
                            hasHeader = true;
                            
                            Logger.LogUI("Header Creation", "Successfully created Image and Text components for spacer slot");
                            break;
                        }
                    }
                    
                    if (!usingSpacerSlot) {
                        Logger.LogUI("Header Processing", $"No spacer slot found for node type: {node.GetType().Name} - skipping TitleParent creation");
                        return;
                    }
                }

                // Create TitleParent with OrderOffset -1
                var titleParentSlot = ui.Root.AddSlot("TitleParent");
                titleParentSlot.OrderOffset = -1;

                // Add RectTransform to parent
                var titleParentRect = titleParentSlot.AttachComponent<RectTransform>();
                
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
                if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
                {
                    Logger.LogUI("Node Type", $"Found Update node of type: {nodeType.Name}");
                    // Check if it's an async update node
                    bool isAsync = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
                    nodeTypeColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
                    Logger.LogUI("Node Color", $"Setting Update node color to {(isAsync ? "ASYNC" : "SYNC")} flow color");
                }
                else 
                {
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
                    // Drive the color to the header image to prevent changes over time
                    var headerTintField = image.Slot.GetComponentOrAttach<ValueField<colorX>>();
                    headerTintField.Value.Value = nodeTypeColor;
                    var headerTintDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                    
                    // Only link if the target is not already linked
                    if (!RoundedCornersHelper.TryLinkValueDriver(headerTintDriver, image.Tint, headerTintField.Value))
                    {
                        Logger.LogUI("Header Tint", "Skipped header tint override; existing drive detected");
                    }
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
                
                // Store display text for later use
                string displayText = headerText.Content.Value;
                
                // Get the node type name with generics for header
                var headerNodeType = node.GetType();
                string fullTypeName = headerNodeType.GetNiceName();
                string baseTypeName = fullTypeName;
                string headerGenericPart = "";
                
                // Handle two conventions: C# generics and underscore naming
                if (headerNodeType.IsGenericType) {
                    // Handle C# generic types like ValueMulMulti<T>
                    var genericArgs = headerNodeType.GetGenericArguments();
                    if (genericArgs.Length > 0) {
                        // Format generic arguments like <float2> or <int, bool>
                        var genericNames = genericArgs.Select(t => t.GetNiceName()).ToArray();
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
                
                // Full type name with proper generic formatting
                string headerNodeTypeName = baseTypeName + headerGenericPart;
                
                // Check if the display text already contains the generic type (e.g., "float2 Input" already has "float2")
                // If so, don't add the generic part to avoid duplication like "float2 Input<float2>"
                bool displayAlreadyHasGeneric = false;
                if (!string.IsNullOrEmpty(headerGenericPart) && headerNodeType.IsGenericType) {
                    var genericArgs = headerNodeType.GetGenericArguments();
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
                
                // Initial text settings (color will be set after header tint is finalized)
                newText.Size.Value = 10.5f;
                newText.AutoSizeMin.Value = 4f;
                
                // Copy RectTransform settings from original header (now unified since we created components for spacer)
                var newHeaderRect = newHeaderSlot.AttachComponent<RectTransform>();
                var originalRect = headerPanel.Slot.GetComponent<RectTransform>();
                if (originalRect != null) {
                    newHeaderRect.AnchorMin.Value = originalRect.AnchorMin.Value;
                    newHeaderRect.AnchorMax.Value = originalRect.AnchorMax.Value;
                    newHeaderRect.OffsetMin.Value = originalRect.OffsetMin.Value;
                    newHeaderRect.OffsetMax.Value = originalRect.OffsetMax.Value;
                }
                
                // Disable the original header and text (works for both original headers and created spacer headers)
                headerPanel.Slot.ActiveSelf = false;
                headerText.Slot.ActiveSelf = false;
                
                // Handle Header/Overview visibility based on overview mode
                var overviewSlot = ui.Root.GetComponentsInChildren<Image>()
                    .FirstOrDefault(img => img.Slot.Name == "Overview");
                
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
                            var headerActiveDriver = newHeaderSlot.GetComponentOrAttach<BooleanValueDriver<bool>>();
                            headerActiveDriver.TargetField.Target = newHeaderSlot.ActiveSelf_Field;
                            headerActiveDriver.TrueValue.Value = true;
                            headerActiveDriver.FalseValue.Value = baseHeaderVisible;

                            var headerHoverCopy = newHeaderSlot.GetComponentOrAttach<ValueCopy<bool>>();
                            headerHoverCopy.Source.Target = overviewHoverArea.IsHovering;
                            headerHoverCopy.Target.Target = headerActiveDriver.State;
                            headerHoverCopy.WriteBack.Value = false;

                            // Overview ActiveSelf driver: hovering => false, not-hovering => baseOverviewVisible
                            var overviewActiveDriver = overviewSlot.Slot.GetComponentOrAttach<BooleanValueDriver<bool>>();
                            overviewActiveDriver.TargetField.Target = overviewSlot.Slot.ActiveSelf_Field;
                            overviewActiveDriver.TrueValue.Value = false;
                            overviewActiveDriver.FalseValue.Value = baseOverviewVisible;

                            var overviewHoverCopy = overviewSlot.Slot.GetComponentOrAttach<ValueCopy<bool>>();
                            overviewHoverCopy.Source.Target = overviewHoverArea.IsHovering;
                            overviewHoverCopy.Target.Target = overviewActiveDriver.State;
                            overviewHoverCopy.WriteBack.Value = false;

                            Logger.LogUI("Hover Overview Override", $"Installed hover override drivers (base: header={(baseHeaderVisible ? "VISIBLE" : "HIDDEN")}, overview={(baseOverviewVisible ? "VISIBLE" : "HIDDEN")})");
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
                    
                    Logger.LogUI("Hover Feature", $"Added hover-to-show-full-name using <NODE_UI> HoverArea: '{displayText}' → '{headerNodeTypeName}'");
                } else {
                    Logger.LogUI("Hover Feature", "WARNING: Could not find NodeHoverArea on ProtoFluxNodeVisual");
                }

                // Apply rounded corners to the background with header color if config is enabled
                var backgroundImageRef = (SyncRef<Image>)AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage").GetValue(__instance);
                if (backgroundImageRef?.Target != null) {
                    RoundedCornersHelper.ApplyRoundedCorners(backgroundImageRef.Target, false, headerTintColorForContrast);
                }

                // Apply custom RectTransform offsets to the Overview mode panel (no sprite override)
                try {
                    var overviewImage = ui.Root.GetComponentsInChildren<Image>()
                        .FirstOrDefault(img => img.Slot.Name == "Overview");

                    if (overviewImage != null) {
                        if (usePlatformPalette && palette != null) {
                            // Overview tint is typically driven by ProtoFluxNodeVisual's internal _overviewBg FieldDrive<colorX>.
                            // Setting the drive target value is the correct way to override it (ValueCopy would be rejected due to Tint.IsDriven).
                            var overviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg");
                            var overviewBgDrive = (FieldDrive<colorX>)overviewBgField.GetValue(__instance);
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
                }
                catch (Exception e) {
                    Logger.LogError("Failed to update Overview panel rect", e, LogCategory.UI);
                }

                // Apply node background sprite to connector label backgrounds (always applied by default)
                var connectorImages = ui.Root.GetComponentsInChildren<Image>()
                    .Where(img => img.Slot.Name == "Connector")
                    .ToList();

                foreach (var connectorImage in connectorImages) {
                    // Find the label background image (sibling Image component that's not the Connector)
                    var parentSlot = connectorImage.Slot.Parent;
                    var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                        .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                    
                    if (labelBackgroundImage != null) {
                        // Apply the header sprite provider for connector labels.
                        // If palette mode is enabled, do NOT preserve original color (it would create a ValueDriver and block palette tint).
                        float labelScale = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_LABEL_SPRITE_SCALE);
                        RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, usePlatformPalette ? false : true, labelScale);

                        // Align vertical offsets with base ProtoFlux layout (only adjust Y values)
                        RectTransform labelRect = labelBackgroundImage.RectTransform;
                        float2 offsetMin = labelRect.OffsetMin.Value;
                        float2 offsetMax = labelRect.OffsetMax.Value;
                        labelRect.OffsetMin.Value = new float2(offsetMin.x, 1f);
                        labelRect.OffsetMax.Value = new float2(offsetMax.x, -1f);

                        // Toggle enabled status based on config
                        bool backgroundsEnabled = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLE_CONNECTOR_LABEL_BACKGROUNDS);
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

                Logger.LogUI("Completion", $"Successfully reorganized title layout using {(usingSpacerSlot ? "converted spacer slot" : "original header panel")}!");

                // Find the category text (it's the last Text component with dark gray color)
                var categoryText = ui.Root.GetComponentsInChildren<Text>()
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
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                        // Colored Node Background mode: use header tint color
                        var categoryColorField = categoryText.Slot.GetComponentOrAttach<ValueField<colorX>>();
                        categoryColorField.Value.Value = headerTintColorForContrast;
                        var categoryColorDriver = categoryText.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                        
                        if (!RoundedCornersHelper.TryLinkValueDriver(categoryColorDriver, categoryText.Color, categoryColorField.Value))
                        {
                            Logger.LogUI("Category Color", "Skipped category color override; existing drive detected");
                        }
                        Logger.LogUI("Category Color", $"Applied node type color to category text: R:{nodeTypeColor.r:F2} G:{nodeTypeColor.g:F2} B:{nodeTypeColor.b:F2}");
                    }
                    else if (usePlatformPalette && palette != null) {
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
            catch (Exception e) {
                Logger.LogError("Failed to process node visual", e, LogCategory.UI);
            }
        }
    }

    // Patch ImpulseDisplay content so its "timeline" bar matches the title/header sprite + shading overlay.
    // NOTE: ImpulseDisplay lives in ProtoFluxBindings in many installs; don't hard-reference its type at compile time.
    [HarmonyPatch]
    public class ImpulseDisplay_BuildContentUI_Patch
    {
        private const string IMPULSE_DISPLAY_TYPE =
            "FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.ImpulseDisplay";

        public static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName(IMPULSE_DISPLAY_TYPE);
            return t == null ? null : AccessTools.Method(t, "BuildContentUI");
        }

        public static void Postfix(object __instance, ProtoFluxNodeVisual visual, UIBuilder ui)
        {
            try
            {
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;
                if (__instance == null || visual == null || ui == null) return;

                // Respect the same ownership/permission behavior as other node UI patches
                if (!PermissionHelper.HasPermission(visual)) return;

                var timelineRootField = AccessTools.Field(__instance.GetType(), "_timelineRoot");
                var timelineRef = timelineRootField?.GetValue(__instance) as SyncRef<Slot>;
                var timelineSlot = timelineRef?.Target;
                if (timelineSlot == null) return;

                var timelineImage = timelineSlot.GetComponent<Image>();
                if (timelineImage == null) return;

                // Ensure the timeline image is masked (useful when using rounded sprites + moving indicators)
                // Mask.OnAttach will ensure a Graphic exists; we already have Image, but this is safe.
                var mask = timelineSlot.GetComponentOrAttach<Mask>();
                mask.ShowMaskGraphic.Value = true;

                // Use the same sprite family as the title/header, including inverted shading
                RoundedCornersHelper.ApplyRoundedCorners(timelineImage, isHeader: true, invertShading: true);

                // Optional: drive timeline tint from PlatformColorPalette (match the default MID tint intent)
                if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE))
                {
                    var palette = RoundedCornersHelper.EnsurePlatformColorPalette(ui.Root);
                    if (palette != null)
                    {
                        var tintCopy = timelineImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                        if (!RoundedCornersHelper.TryLinkValueCopy(tintCopy, palette.Neutrals.Mid, timelineImage.Tint))
                        {
                            Logger.LogUI("PlatformColorPalette", "Skipped ImpulseDisplay timeline tint copy; existing drive detected");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Error in ImpulseDisplay_BuildContentUI_Patch", e, LogCategory.UI);
            }
        }
    }

    // Patch to handle initial node creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateVisual")]
    public class ProtoFluxNodeVisual_GenerateVisual_Patch {
        private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");

        public static void Postfix(ProtoFluxNodeVisual __instance) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // Audio is now handled on-demand by ProtoFluxSounds

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Get the node's type color for potential background use
                colorX nodeTypeColor;
                var node = __instance.Node.Target;
                if (node != null) {
                    var nodeType = node.GetType();
                    if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
                    {
                        // Check if it's an async update node
                        bool isAsync = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
                        nodeTypeColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
                    }
                    else 
                    {
                        nodeTypeColor = DatatypeColorHelper.GetTypeColor(nodeType);
                    }
                } else {
                    nodeTypeColor = colorX.White; // fallback
                }

                // Apply rounded corners to background with header color if config is enabled
                var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
                if (bgImageRef?.Target != null) {
                    colorX darkenedColor = nodeTypeColor * 0.5f;
                    RoundedCornersHelper.ApplyRoundedCorners(bgImageRef.Target, false, darkenedColor);
                }

                // Find all connector slots in the hierarchy
                var connectorSlots = __instance.Slot.GetComponentsInChildren<Image>()
                    .Where(img => img.Slot.Name == "Connector")
                    .ToList();

                foreach (var connectorImage in connectorSlots) {
                    // Determine if this is an output connector based on its position
                    bool isOutput = connectorImage.RectTransform.OffsetMin.Value.x < 0;
                    
                    // Check for ImpulseType by looking for ImpulseProxy or OperationProxy
                    var impulseProxy = connectorImage.Slot.GetComponent<ProtoFluxImpulseProxy>();
                    var operationProxy = connectorImage.Slot.GetComponent<ProtoFluxOperationProxy>();
                    
                    ImpulseType? impulseType = null;
                    bool isOperation = false;
                    bool isAsync = false;
                    
                    if (impulseProxy != null) {
                        impulseType = impulseProxy.ImpulseType.Value;
                    }
                    else if (operationProxy != null) {
                        isOperation = true;
                        isAsync = operationProxy.IsAsync.Value;
                    }
                    
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
                    // Get type color from proxy (more reliable than image tint)
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE)) {
                        var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                        if (palette != null) {
                            bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                            colorX colorToMatch = RoundedCornersHelper.GetConnectorTypeColor(connectorImage.Slot) ?? connectorImage.Tint.Value;
                            var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, colorToMatch);
                            if (source != null) {
                                var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint);
                            }
                        }
                    }
                    
                    // Apply node background sprite to connector label background (always applied by default)
                    // Find the label background image (sibling Image component that's not the Connector)
                    var parentSlot = connectorImage.Slot.Parent;
                    var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                        .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                    
                    if (labelBackgroundImage != null) {
                        bool usePalette = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE);
                        float labelScale = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_LABEL_SPRITE_SCALE);
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

                        Logger.LogUI("Connector Label Background", $"Applied header sprite to connector label background in GenerateVisual {(usePalette ? "with palette tint" : "while preserving original color")}");

                        // Palette-driven label background tint + text tint (optional)
                        // Use connector's original tint to find matching Sub color
                        if (usePalette) {
                            var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                            if (palette != null) {
                                bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                                colorX originalConnectorTint = connectorImage.Tint.Value;
                                var bgSource = RoundedCornersHelper.GetLabelBackgroundTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, originalConnectorTint);
                                if (bgSource != null) {
                                    var bgCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                    RoundedCornersHelper.TryLinkValueCopy(bgCopy, bgSource, labelBackgroundImage.Tint);
                                }

                                if (backgroundsEnabled) {
                                    var paletteTextSlot = labelBackgroundImage.Slot.FindChild("Text");
                                    var textComponent = paletteTextSlot?.GetComponent<Text>();
                                    var textSource = RoundedCornersHelper.GetLabelTextTintSource(palette);
                                    if (textComponent != null && textSource != null) {
                                        var textCopy = textComponent.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                        RoundedCornersHelper.TryLinkValueCopy(textCopy, textSource, textComponent.Color);
                                    }
                                }
                            }
                        }
                        
                        // Find and center the text in the label
                        var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                        if (textSlot != null) {
                            var textComponent = textSlot.GetComponent<Text>();
                            if (textComponent != null) {
                                textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                Logger.LogUI("Connector Label Text", $"Set connector label text to center alignment in GenerateVisual");
                                
                                // If backgrounds are disabled, copy the image tint to the text color
                                if (!backgroundsEnabled) {
                                    var valueCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                    valueCopy.Source.Target = labelBackgroundImage.Tint;
                                    valueCopy.Target.Target = textComponent.Color;
                                    valueCopy.WriteBack.Value = false;
                                    Logger.LogUI("Connector Label Text Color", $"Copying background tint to text color in GenerateVisual (backgrounds disabled)");
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Logger.LogError("Error in ProtoFluxNodeVisual_GenerateVisual_Patch", e, LogCategory.UI);
            }
        }
    }

    // Patch to handle dynamic connector creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateInputElement")]
    public class ProtoFluxNodeVisual_GenerateInputElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Find wire point slot for audio setup
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot != null) {
                    // Create AudioClips structure
                    WireHelper.CreateAudioClipsSlot(wirePointSlot);
                }

                // Find the connector image that was just created
                var connectorImage = ui.Current?.GetComponentInChildren<Image>(image => image.Slot.Name == "Connector");
                if (connectorImage != null) {
                    // This is an input connector (newly created)
                    bool isOutput = false;
                    
                    // Check for ImpulseType by looking for ImpulseProxy or OperationProxy
                    var impulseProxy = connectorImage.Slot.GetComponent<ProtoFluxImpulseProxy>();
                    var operationProxy = connectorImage.Slot.GetComponent<ProtoFluxOperationProxy>();
                    
                    ImpulseType? impulseType = null;
                    bool isOperation = false;
                    bool isAsync = false;
                    
                    if (impulseProxy != null) {
                        impulseType = impulseProxy.ImpulseType.Value;
                    }
                    else if (operationProxy != null) {
                        isOperation = true;
                        isAsync = operationProxy.IsAsync.Value;
                    }
                    
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

                    // Palette-driven connector tint (optional)
                    // Get type color from proxy (more reliable than image tint)
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE)) {
                        var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                        if (palette != null) {
                            bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                            colorX colorToMatch = RoundedCornersHelper.GetConnectorTypeColor(connectorImage.Slot) ?? connectorImage.Tint.Value;
                            var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, colorToMatch);
                            if (source != null) {
                                var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint);
                            }
                        }
                    }

                    // Set the correct RectTransform settings for input connectors
                    connectorImage.RectTransform.SetFixedHorizontal(0.0f, 16f, 0.0f);

                    // Apply node background sprite to connector label background (always applied by default)
                    // Find the label background image (sibling Image component that's not the Connector)
                    var parentSlot = connectorImage.Slot.Parent;
                    var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                        .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                    
                    if (labelBackgroundImage != null) {
                        bool usePalette = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE);
                        float labelScale = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_LABEL_SPRITE_SCALE);
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

                        Logger.LogUI("Connector Label Background", $"Applied header sprite to dynamic input connector label background {(usePalette ? "with palette tint" : "while preserving original color")}");

                        // Palette-driven label background tint + text tint (optional)
                        // Use connector's original tint to find matching Sub color
                        if (usePalette) {
                            var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                            if (palette != null) {
                                bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                                colorX originalConnectorTint = connectorImage.Tint.Value;
                                var bgSource = RoundedCornersHelper.GetLabelBackgroundTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, originalConnectorTint);
                                if (bgSource != null) {
                                    var bgCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                    RoundedCornersHelper.TryLinkValueCopy(bgCopy, bgSource, labelBackgroundImage.Tint);
                                }

                                if (backgroundsEnabled) {
                                    var paletteTextSlot = labelBackgroundImage.Slot.FindChild("Text");
                                    var textComponent = paletteTextSlot?.GetComponent<Text>();
                                    var textSource = RoundedCornersHelper.GetLabelTextTintSource(palette);
                                    if (textComponent != null && textSource != null) {
                                        var textCopy = textComponent.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                        RoundedCornersHelper.TryLinkValueCopy(textCopy, textSource, textComponent.Color);
                                    }
                                }
                            }
                        }
                        
                        // Find and center the text in the label
                        var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                        if (textSlot != null) {
                            var textComponent = textSlot.GetComponent<Text>();
                            if (textComponent != null) {
                                textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                Logger.LogUI("Connector Label Text", $"Set dynamic input connector label text to center alignment");
                                
                                // If backgrounds are disabled, copy the image tint to the text color
                                if (!backgroundsEnabled) {
                                    var valueCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                    valueCopy.Source.Target = labelBackgroundImage.Tint;
                                    valueCopy.Target.Target = textComponent.Color;
                                    valueCopy.WriteBack.Value = false;
                                    Logger.LogUI("Connector Label Text Color", $"Copying background tint to text color for dynamic input (backgrounds disabled)");
                                }
                            }
                        }
                    }

                    Logger.LogUI("Dynamic Input", $"Applied texture patch to newly created input connector");
                }
            }
            catch (Exception e) {
                Logger.LogError("Error in input element generation", e, LogCategory.UI);
            }
        }
    }

    // Additional patch to handle output connector creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateOutputElement")]
    public class ProtoFluxNodeVisual_GenerateOutputElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Find wire point slot for audio setup
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot != null) {
                    // Create AudioClips structure
                    WireHelper.CreateAudioClipsSlot(wirePointSlot);
                }

                // Find the connector image that was just created
                var connectorImage = ui.Current?.GetComponentInChildren<Image>(image => image.Slot.Name == "Connector");
                if (connectorImage != null) {
                    // This is an output connector (newly created)
                    bool isOutput = true;
                    
                    // Check for ImpulseType by looking for ImpulseProxy or OperationProxy
                    var impulseProxy = connectorImage.Slot.GetComponent<ProtoFluxImpulseProxy>();
                    var operationProxy = connectorImage.Slot.GetComponent<ProtoFluxOperationProxy>();
                    
                    ImpulseType? impulseType = null;
                    bool isOperation = false;
                    bool isAsync = false;
                    
                    if (impulseProxy != null) {
                        impulseType = impulseProxy.ImpulseType.Value;
                    }
                    else if (operationProxy != null) {
                        isOperation = true;
                        isAsync = operationProxy.IsAsync.Value;
                    }
                    
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

                    // Palette-driven connector tint (optional)
                    // Get type color from proxy (more reliable than image tint)
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE)) {
                        var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                        if (palette != null) {
                            bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                            colorX colorToMatch = RoundedCornersHelper.GetConnectorTypeColor(connectorImage.Slot) ?? connectorImage.Tint.Value;
                            var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, colorToMatch);
                            if (source != null) {
                                var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint);
                            }
                        }
                    }

                    // Set the correct RectTransform settings for output connectors
                    connectorImage.RectTransform.SetFixedHorizontal(-16f, 0.0f, 1f);

                    // Apply node background sprite to connector label background (always applied by default)
                    // Find the label background image (sibling Image component that's not the Connector)
                    var parentSlot = connectorImage.Slot.Parent;
                    var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                        .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                    
                    if (labelBackgroundImage != null) {
                        bool usePalette = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE);
                        float labelScale = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_LABEL_SPRITE_SCALE);
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

                        Logger.LogUI("Connector Label Background", $"Applied header sprite to dynamic output connector label background {(usePalette ? "with palette tint" : "while preserving original color")}");

                        // Palette-driven label background tint + text tint (optional)
                        // Use connector's original tint to find matching Sub color
                        if (usePalette) {
                            var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                            if (palette != null) {
                                bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                                colorX originalConnectorTint = connectorImage.Tint.Value;
                                var bgSource = RoundedCornersHelper.GetLabelBackgroundTintSource(palette, isOutput, impulseType, isOperation, isAsync, isReference, originalConnectorTint);
                                if (bgSource != null) {
                                    var bgCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                    RoundedCornersHelper.TryLinkValueCopy(bgCopy, bgSource, labelBackgroundImage.Tint);
                                }

                                if (backgroundsEnabled) {
                                    var paletteTextSlot = labelBackgroundImage.Slot.FindChild("Text");
                                    var textComponent = paletteTextSlot?.GetComponent<Text>();
                                    var textSource = RoundedCornersHelper.GetLabelTextTintSource(palette);
                                    if (textComponent != null && textSource != null) {
                                        var textCopy = textComponent.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                        RoundedCornersHelper.TryLinkValueCopy(textCopy, textSource, textComponent.Color);
                                    }
                                }
                            }
                        }
                        
                        // Find and center the text in the label
                        var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                        if (textSlot != null) {
                            var textComponent = textSlot.GetComponent<Text>();
                            if (textComponent != null) {
                                textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                Logger.LogUI("Connector Label Text", $"Set dynamic output connector label text to center alignment");
                                
                                // If backgrounds are disabled, copy the image tint to the text color
                                if (!backgroundsEnabled) {
                                    var valueCopy = labelBackgroundImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                    valueCopy.Source.Target = labelBackgroundImage.Tint;
                                    valueCopy.Target.Target = textComponent.Color;
                                    valueCopy.WriteBack.Value = false;
                                    Logger.LogUI("Connector Label Text Color", $"Copying background tint to text color for dynamic output (backgrounds disabled)");
                                }
                            }
                        }
                    }

                    Logger.LogUI("Dynamic Output", $"Applied texture patch to newly created output connector");
                }
            }
            catch (Exception e) {
                Logger.LogError("Error in output element generation", e, LogCategory.UI);
            }
        }
    }

    // Additional patch to handle dynamic impulse creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateImpulseElement")]
    public class ProtoFluxNodeVisual_GenerateImpulseElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ISyncRef input, string name, ImpulseType type) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Find wire point slot for audio setup
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot != null) {
                    // Create AudioClips structure
                    WireHelper.CreateAudioClipsSlot(wirePointSlot);
                }

                // Find the connector image that was just created
                var connectorImage = ui.Current?.GetComponentInChildren<Image>(image => image.Slot.Name == "Connector");
                if (connectorImage != null) {
                    // This is an impulse connector (output)
                    bool isOutput = true;
                    
                    // Get or create shared sprite provider for impulse/flow connector
                    var spriteProvider = ProtoFluxNodeVisual_BuildUI_Patch.GetOrCreateSharedConnectorSprite(
                        connectorImage.Slot, 
                        isOutput, 
                        type, 
                        false, 
                        false
                    );
                    
                    // Apply the sprite provider to the connector image
                    connectorImage.Sprite.Target = spriteProvider;
                    connectorImage.PreserveAspect.Value = true;

                    // Palette-driven connector tint (optional)
                    // Get type color from proxy (more reliable than image tint)
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE)) {
                        var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                        if (palette != null) {
                            bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                            colorX colorToMatch = RoundedCornersHelper.GetConnectorTypeColor(connectorImage.Slot) ?? connectorImage.Tint.Value;
                            var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, type, false, false, isReference, colorToMatch);
                            if (source != null) {
                                var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint);
                            }
                        }
                    }

                    // Set the correct RectTransform settings for impulse connectors (outputs)
                    connectorImage.RectTransform.SetFixedHorizontal(-16f, 0.0f, 1f);

                    Logger.LogUI("Dynamic Impulse", $"Applied texture patch to newly created impulse connector");
                }
            }
            catch (Exception e) {
                Logger.LogError("Error in impulse element generation", e, LogCategory.UI);
            }
        }
    }

    // Additional patch to handle dynamic operation creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateOperationElement")]
    public class ProtoFluxNodeVisual_GenerateOperationElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, INodeOperation operation, string name, bool isAsync) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Find wire point slot for audio setup
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot != null) {
                    // Create AudioClips structure
                    WireHelper.CreateAudioClipsSlot(wirePointSlot);
                }

                // Find the connector image that was just created
                var connectorImage = ui.Current?.GetComponentInChildren<Image>(image => image.Slot.Name == "Connector");
                if (connectorImage != null) {
                    // This is an operation connector (input)
                    bool isOutput = false;
                    
                    // Get or create shared sprite provider for operation/flow connector
                    var spriteProvider = ProtoFluxNodeVisual_BuildUI_Patch.GetOrCreateSharedConnectorSprite(
                        connectorImage.Slot, 
                        isOutput, 
                        null, 
                        true, 
                        isAsync
                    );
                    
                    // Apply the sprite provider to the connector image
                    connectorImage.Sprite.Target = spriteProvider;
                    connectorImage.PreserveAspect.Value = true;

                    // Palette-driven connector tint (optional)
                    // Get type color from proxy (more reliable than image tint)
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE)) {
                        var palette = RoundedCornersHelper.EnsurePlatformColorPalette(__instance.Slot);
                        if (palette != null) {
                            bool isReference = RoundedCornersHelper.IsReferenceConnector(connectorImage.Slot);
                            colorX colorToMatch = RoundedCornersHelper.GetConnectorTypeColor(connectorImage.Slot) ?? connectorImage.Tint.Value;
                            var source = RoundedCornersHelper.GetConnectorTintSource(palette, isOutput, null, true, isAsync, isReference, colorToMatch);
                            if (source != null) {
                                var tintCopy = connectorImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
                                RoundedCornersHelper.TryLinkValueCopy(tintCopy, source, connectorImage.Tint);
                            }
                        }
                    }

                    // Set the correct RectTransform settings for operation connectors (inputs)
                    connectorImage.RectTransform.SetFixedHorizontal(0.0f, 16f, 0.0f);

                    Logger.LogUI("Dynamic Operation", $"Applied texture patch to newly created operation connector");
                }
            }
            catch (Exception e) {
                Logger.LogError("Error in operation element generation", e, LogCategory.UI);
            }
        }
    }

    // Patch to handle dynamic overview mode changes
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "OnChanges")]
    public class ProtoFluxNodeVisual_OnChanges_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // Skip if instance is null or world is not available
                if (__instance == null || __instance.World == null) return;

                // Skip if we don't own this node
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Get the user's overview mode setting using ProtoFluxTool
                if (__instance.LocalUser == null) return;
                
                bool overviewModeEnabled = OverviewModeHelper.GetOverviewMode(__instance.LocalUser);

                // Find our custom TitleParent>Header slot and Overview slot
                var titleParent = __instance.Slot.FindChild("TitleParent");
                var overviewSlot = __instance.Slot.GetComponentsInChildren<Image>()
                    .FirstOrDefault(img => img.Slot.Name == "Overview");
                
                // Only toggle header visibility if there's an overview slot
                if (overviewSlot != null) {
                    bool baseHeaderVisible = !overviewModeEnabled;
                    bool baseOverviewVisible = overviewModeEnabled;

                    // Update header visibility: if driven by our hover driver, update its FalseValue; otherwise set directly
                    if (titleParent != null) {
                        var header = titleParent.FindChild("Header");
                        if (header != null) {
                            var headerDriver = header.GetComponent<BooleanValueDriver<bool>>();
                            if (headerDriver != null && headerDriver.TargetField.Target == header.ActiveSelf_Field) {
                                headerDriver.FalseValue.Value = baseHeaderVisible;
                                headerDriver.TrueValue.Value = true;
                            } else {
                                header.ActiveSelf = baseHeaderVisible;
                            }
                        }
                    }

                    // Update overview visibility: if driven by our hover driver, update its FalseValue; otherwise set directly
                    var overviewDriver = overviewSlot.Slot.GetComponent<BooleanValueDriver<bool>>();
                    if (overviewDriver != null && overviewDriver.TargetField.Target == overviewSlot.Slot.ActiveSelf_Field) {
                        overviewDriver.FalseValue.Value = baseOverviewVisible;
                        overviewDriver.TrueValue.Value = false;
                    } else {
                        overviewSlot.Slot.ActiveSelf = baseOverviewVisible;
                    }
                } else {
                    // No overview slot found, keep header visible
                    if (titleParent != null) {
                        var header = titleParent.FindChild("Header");
                        if (header != null) {
                            header.ActiveSelf = true;
                        }
                    }
                }

            } catch (Exception e) {
                Logger.LogError("Error in OnChanges patch", e, LogCategory.UI);
            }
        }
    }

    // Patch to make background color compatible with node status (selection, highlighting, validation)
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "UpdateNodeStatus")]
    public class ProtoFluxNodeVisual_UpdateNodeStatus_Patch {
        private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");

        public static bool Prefix(ProtoFluxNodeVisual __instance) {
            try {
                // Skip if disabled or not using header color for background
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return true;
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) return true;

                // Get background image
                var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
                if (bgImageRef?.Target == null) return true;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return true;

                // Get the node's type color as base
                colorX baseColor;
                var node = __instance.Node.Target;
                if (node != null) {
                    var nodeType = node.GetType();
                    if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
                    {
                        bool isAsync = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
                        baseColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
                    }
                    else 
                    {
                        baseColor = DatatypeColorHelper.GetTypeColor(nodeType);
                    }
                    // Darken it like we do when initially applying (preserve alpha)
                    baseColor = baseColor.MulRGB(0.5f);
                } else {
                    baseColor = RadiantUI_Constants.BG_COLOR; // fallback
                }

                // Apply status color lerps (same logic as original UpdateNodeStatus)
                colorX finalColor = baseColor;
                
                if (__instance.IsSelected.Value) {
                    finalColor = MathX.LerpUnclamped(finalColor, colorX.Cyan, 0.5f);
                }
                
                if (__instance.IsHighlighted.Value) {
                    finalColor = MathX.LerpUnclamped(finalColor, colorX.Yellow, 0.1f);
                }
                
                if (!__instance.IsNodeValid) {
                    finalColor = MathX.LerpUnclamped(finalColor, colorX.Red, 0.5f);
                }

                // Update the ValueField source so the driver propagates the new color
                var bgImage = bgImageRef.Target;
                var colorField = bgImage.Slot.GetComponent<ValueField<colorX>>();
                if (colorField != null) {
                    colorField.Value.Value = finalColor;
                    Logger.LogUI("UpdateNodeStatus", $"Updated ValueField for status color: R:{finalColor.r:F2} G:{finalColor.g:F2} B:{finalColor.b:F2}");
                } else {
                    // Fallback: if no ValueField exists, set directly
                    if (!bgImage.Tint.IsDriven) {
                        bgImage.Tint.Value = finalColor;
                        Logger.LogUI("UpdateNodeStatus", $"Set tint directly (no driver): R:{finalColor.r:F2} G:{finalColor.g:F2} B:{finalColor.b:F2}");
                    } else {
                        Logger.LogUI("UpdateNodeStatus", "Skipped: tint is driven but no ValueField found");
                    }
                }

                // Also update overview background if it exists
                var overviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg");
                var overviewBg = (FieldDrive<colorX>)overviewBgField.GetValue(__instance);
                if (overviewBg.IsLinkValid) {
                    overviewBg.Target.Value = finalColor;
                }

                // Skip original method since we handled it
                return false;
            } catch (Exception e) {
                Logger.LogError("Error in UpdateNodeStatus patch", e, LogCategory.UI);
                // Let original run if we fail
                return true;
            }
        }
    }

 
} 
