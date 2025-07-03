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
        public static void ApplyRoundedCorners(Image image, bool isHeader = false) {
            // Skip if already applied
            if (image.Sprite.Target is SpriteProvider) return;

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
            spriteProvider.Scale.Value = isHeader ? 0.03f : 0.07f;  // Different scale for header vs background
            spriteProvider.FixedSize.Value = 1.00f;  // FixedSize: 1.00
            Logger.LogUI("Sprite Config", $"Configured {(isHeader ? "header" : "background")} sprite provider settings");

            // Update the image to use the sprite
            image.Sprite.Target = spriteProvider;
            
            // Preserve color and tint settings
            image.PreserveAspect.Value = true;
            Logger.LogUI("Completion", $"Successfully applied rounded corners to {(isHeader ? "header" : "background")}");
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
        /// Creates or retrieves a shared sprite provider for the connector image
        /// </summary>
        public static SpriteProvider GetOrCreateSharedConnectorSprite(Slot slot, bool isOutput, ImpulseType? impulseType = null, bool isOperation = false, bool isAsync = false) {
            // Check if this should use the Call connector
            if (ShouldUseCallConnector(impulseType, isOperation, isAsync)) {
                return GetOrCreateSharedCallConnectorSprite(slot, isOutput);
            }
            
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
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_OUTPUT_TEXTURE) : 
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

        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ProtoFluxNode node) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // Audio is now handled on-demand by ProtoFluxSounds

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

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

                // Find all connector images in the hierarchy
                var connectorSlots = ui.Root.GetComponentsInChildren<Image>()
                    .Where(img => img.Slot.Name == "Connector")
                    .ToList();

                foreach (var connectorImage in connectorSlots) {
                    // Determine if this is an output connector based on its RectTransform settings
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
                    var spriteProvider = GetOrCreateSharedConnectorSprite(connectorImage.Slot, isOutput, impulseType, isOperation, isAsync);
                    
                    // Apply the sprite provider to the connector image
                    connectorImage.Sprite.Target = spriteProvider;
                    connectorImage.PreserveAspect.Value = true;

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
                            spacerImage.Tint.Value = RadiantUI_Constants.HEADER;
                            
                            // Create Text component in a child slot
                            var spacerTextSlot = spacerSlot.AddSlot("Text");
                            var spacerText = spacerTextSlot.AttachComponent<Text>();
                            spacerText.Content.Value = node.NodeName;
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
                
                // Get the node's type color for the header
                colorX nodeTypeColor;
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
                
                // Apply the color to the header image
                image.Tint.Value = nodeTypeColor;
                
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
                
                // Calculate text color based on header image color for better contrast
                var headerColor = image.Tint.Value;
                Logger.LogUI("Header Color", $"Header image color: R:{headerColor.r:F2} G:{headerColor.g:F2} B:{headerColor.b:F2}");
                
                var brightness = (headerColor.r * 0.299f + headerColor.g * 0.587f + headerColor.b * 0.114f);
                Logger.LogUI("Brightness", $"Calculated brightness: {brightness:F2}");
                
                var textColor = brightness > 0.6f ? colorX.Black : colorX.White;
                Logger.LogUI("Text Color", $"Setting text color to: {(brightness > 0.6f ? "BLACK" : "WHITE")} based on brightness");
                
                // Set text color multiple ways to ensure it takes effect
                newText.Color.Value = textColor;
                newText.Color.ForceSet(textColor);
                newText.Size.Value = 9.00f;
                newText.AutoSizeMin.Value = 4f;
                
                // Set text content - now we always have headerText since we created it for spacer slots
                string displayText = headerText.Content.Value;
                newText.Content.Value = $"<color={(brightness > 0.6f ? "#000000" : "#FFFFFF")}><b>{displayText}</b></color>";
                
                Logger.LogUI("Text Color", $"Text color set to: R:{newText.Color.Value.r:F2} G:{newText.Color.Value.g:F2} B:{newText.Color.Value.b:F2}");
                
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
                        // Toggle Header visibility based on overview mode (opposite of overview)
                        newHeaderSlot.ActiveSelf = !overviewModeEnabled;
                        
                        // Toggle Overview slot visibility based on overview mode (same as overview)
                        overviewSlot.Slot.ActiveSelf = overviewModeEnabled;
                        Logger.LogUI("Overview Processing", $"Overview slot set to {(overviewModeEnabled ? "VISIBLE" : "HIDDEN")} based on overview mode");
                        Logger.LogUI("Header Visibility", $"Header slot set to {(!overviewModeEnabled ? "VISIBLE" : "HIDDEN")} based on overview mode");
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
                RoundedCornersHelper.ApplyRoundedCorners(image, true);

                Logger.LogUI("Completion", $"Successfully reorganized title layout using {(usingSpacerSlot ? "converted spacer slot" : "original header panel")}!");

                // Find the category text (it's the last Text component with dark gray color)
                var categoryText = ui.Root.GetComponentsInChildren<Text>()
                    .LastOrDefault(text => text.Color.Value == colorX.DarkGray);
                
                if (categoryText != null) {
                    categoryText.VerticalAlign.Value = TextVerticalAlignment.Middle;
                    categoryText.Size.Value = 8.00f;
                    categoryText.AlignmentMode.Value = AlignmentMode.LineBased;
                    categoryText.LineHeight.Value = 0.35f;
                }

                // Overview panel transparency is handled in UpdateNodeStatus patch
            }
            catch (Exception e) {
                Logger.LogError("Failed to process node visual", e, LogCategory.UI);
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

                // Apply rounded corners to background
                var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
                if (bgImageRef?.Target != null) {
                    RoundedCornersHelper.ApplyRoundedCorners(bgImageRef.Target, false);
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

                // Find wire point slot - it will be in the last created slot
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot == null) return;

                // Create AudioClips structure
                WireHelper.CreateAudioClipsSlot(wirePointSlot);
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

                // Find wire point slot - it will be in the last created slot
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot == null) return;

                // Create AudioClips structure
                WireHelper.CreateAudioClipsSlot(wirePointSlot);
            }
            catch (Exception e) {
                Logger.LogError("Error in output element generation", e, LogCategory.UI);
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
                    // Toggle Header visibility (opposite of overview mode)
                    if (titleParent != null) {
                        var header = titleParent.FindChild("Header");
                        if (header != null) {
                            header.ActiveSelf = !overviewModeEnabled;
                            Logger.LogUI("Dynamic Header Toggle", $"Header set to {(!overviewModeEnabled ? "VISIBLE" : "HIDDEN")} due to overview mode change");
                        }
                    }
                    
                    // Toggle Overview visibility (same as overview mode)
                    overviewSlot.Slot.ActiveSelf = overviewModeEnabled;
                    Logger.LogUI("Dynamic Overview Toggle", $"Overview slot set to {(overviewModeEnabled ? "VISIBLE" : "HIDDEN")} due to overview mode change");
                } else {
                    // No overview slot found, keep header visible
                    if (titleParent != null) {
                        var header = titleParent.FindChild("Header");
                        if (header != null) {
                            header.ActiveSelf = true;
                            Logger.LogUI("Dynamic Header Toggle", "No overview slot found - keeping header visible");
                        }
                    }
                }

            } catch (Exception e) {
                Logger.LogError("Error in OnChanges patch", e, LogCategory.UI);
            }
        }
    }

 
} 
