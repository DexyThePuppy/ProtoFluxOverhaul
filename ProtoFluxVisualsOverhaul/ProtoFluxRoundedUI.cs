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

namespace ProtoFluxVisualsOverhaul {
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

    // Helper class for shared functionality
    public static class RoundedCornersHelper {
        public static void ApplyRoundedCorners(Image image, bool isHeader = false) {
            // Skip if already applied
            if (image.Sprite.Target is SpriteProvider) return;

            ProtoFluxVisualsOverhaul.Msg($"🐾 Applying rounded corners to {(isHeader ? "header" : "background")}...");

            // Create a SpriteProvider for rounded corners
            var spriteProvider = image.Slot.AttachComponent<SpriteProvider>();
            ProtoFluxVisualsOverhaul.Msg($"✅ Created SpriteProvider for {(isHeader ? "header" : "background")}");
            
            // Set up the texture
            var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
            texture.URL.Value = isHeader ? 
                ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.NODE_BACKGROUND_HEADER_TEXTURE) :
                ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.NODE_BACKGROUND_TEXTURE);
            texture.FilterMode.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.FILTER_MODE);
            texture.WrapModeU.Value = TextureWrapMode.Clamp;
            texture.WrapModeV.Value = TextureWrapMode.Clamp;
            texture.MipMaps.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIPMAPS);
            texture.MipMapFilter.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIPMAP_FILTER);
            texture.AnisotropicLevel.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ANISOTROPIC_LEVEL);
            texture.KeepOriginalMipMaps.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.KEEP_ORIGINAL_MIPMAPS);
            texture.CrunchCompressed.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CRUNCH_COMPRESSED);
            texture.Readable.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.READABLE);
            texture.Uncompressed.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.UNCOMPRESSED);
            texture.DirectLoad.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.DIRECT_LOAD);
            texture.ForceExactVariant.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.FORCE_EXACT_VARIANT);
            texture.PreferredFormat.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.PREFERRED_FORMAT);
            texture.PreferredProfile.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.PREFERRED_PROFILE);
            
            ProtoFluxVisualsOverhaul.Msg($"✅ Set up texture for {(isHeader ? "header" : "background")}");
            
            // Configure the sprite provider based on the image settings
            spriteProvider.Texture.Target = texture;
            spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f);  // x:0 y:0 width:1 height:1
            spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f);  // x:0.5 y:0 z:0 w:0
            spriteProvider.Scale.Value = isHeader ? 0.03f : 0.07f;  // Different scale for header vs background
            spriteProvider.FixedSize.Value = 1.00f;  // FixedSize: 1.00
            ProtoFluxVisualsOverhaul.Msg($"✅ Configured {(isHeader ? "header" : "background")} sprite provider settings");

            // Update the image to use the sprite
            image.Sprite.Target = spriteProvider;
            
            // Preserve color and tint settings
            image.PreserveAspect.Value = true;
            ProtoFluxVisualsOverhaul.Msg($"✅ Successfully applied rounded corners to {(isHeader ? "header" : "background")}!");
        }
    }

    // Helper class for wire-related functionality
    public static class WireHelper {
        private static readonly Dictionary<string, StaticAudioClip> sharedAudioClips = new Dictionary<string, StaticAudioClip>();

        public static void CreateAudioClipsSlot(Slot wirePointSlot) {
            if (wirePointSlot == null || wirePointSlot.World == null) return;

            // Initialize sounds if needed
            ProtoFluxSounds.Initialize(wirePointSlot.World);

            // Create AudioClips slot if it doesn't exist
            var audioClipsSlot = wirePointSlot.FindChild("AudioClips") ?? wirePointSlot.AddSlot("AudioClips");

            // Create default audio clips
            CreateAudioClip(audioClipsSlot, "Grab");
            CreateAudioClip(audioClipsSlot, "Delete");
            CreateAudioClip(audioClipsSlot, "Connect");
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
                            ProtoFluxVisualsOverhaul.Msg($"🔍 Found input wire point in {connectorSlot.Parent?.Name ?? "unknown"}");
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
                            ProtoFluxVisualsOverhaul.Msg($"🔍 Found output wire point in {connectorSlot.Parent?.Name ?? "unknown"}");
                            CreateAudioClipsSlot(wirePoint);
                        }
                    }
                }
            }
        }

        private static void CreateAudioClip(Slot parentSlot, string clipName) {
            var clipSlot = parentSlot.FindChild(clipName) ?? parentSlot.AddSlot(clipName);
            
            // Only add components if they don't exist
            if (clipSlot.GetComponent<AudioClipPlayer>() == null) {
                // Create and configure AudioOutput
                var audioOutput = clipSlot.AttachComponent<AudioOutput>();
                audioOutput.Spatialize.Value = true;
                audioOutput.MinDistance.Value = 0.1f;
                audioOutput.MaxDistance.Value = 5f;
                audioOutput.Volume.Value = 1f;

                // Create and configure AudioClipPlayer
                var player = clipSlot.AttachComponent<AudioClipPlayer>();
                
                // Get or create the shared StaticAudioClip
                var staticClip = GetOrCreateSharedStaticAudioClip(clipSlot.World, clipName);
                
                // Link the components
                player.Clip.Target = staticClip;
                audioOutput.Source.Target = player;
            }
        }

        private static StaticAudioClip GetOrCreateSharedStaticAudioClip(World world, string clipName) {
            // Check if we already have this clip cached
            if (sharedAudioClips.TryGetValue(clipName, out var existingClip)) {
                if (!existingClip.IsDestroyed) {
                    return existingClip;
                }
                sharedAudioClips.Remove(clipName);
            }

            // Create the hierarchy under _TEMP
            var tempSlot = world.RootSlot.FindChild("__TEMP") ?? world.RootSlot.AddSlot("__TEMP");
            var audioClipsSlot = tempSlot.FindChild("ProtoFluxAudioClips") ?? tempSlot.AddSlot("ProtoFluxAudioClips");
            var clipTypeSlot = audioClipsSlot.FindChild(clipName) ?? audioClipsSlot.AddSlot(clipName);

            // Create the StaticAudioClip if it doesn't exist
            var staticClip = clipTypeSlot.GetComponent<StaticAudioClip>() ?? clipTypeSlot.AttachComponent<StaticAudioClip>();

            // Base URL for the sound files from GitHub
            string baseUrl = "https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/sounds/";

            // Set the appropriate audio URL based on the clip type
            Uri audioUrl = clipName switch {
                "Grab" => new Uri(baseUrl + "FluxWireGrab.wav"),
                "Delete" => new Uri(baseUrl + "FluxWireDelete.wav"),
                "Connect" => new Uri(baseUrl + "FluxWireConnect.wav"),
                _ => null
            };

            if (audioUrl != null) {
                staticClip.URL.Value = audioUrl;
                staticClip.LoadMode.Value = AudioLoadMode.Automatic;
                staticClip.SampleRateMode.Value = SampleRateMode.Conform;
            }

            // Ensure cleanup when user leaves
            clipTypeSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = world.LocalUser;

            // Cache the clip
            sharedAudioClips[clipName] = staticClip;

            return staticClip;
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

            // Create sprite in temporary storage
            SpriteProvider spriteProvider = slot.World.RootSlot
                .FindChildOrAdd("__TEMP", false)
                .FindChildOrAdd($"{slot.LocalUser.UserName}-Connector-Sprite-{(isOutput ? "Output" : "Input")}", false)
                .GetComponentOrAttach<SpriteProvider>();

            // Ensure cleanup when user leaves
            spriteProvider.Slot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;

            // Set up the texture if not already set
            if (spriteProvider.Texture.Target == null) {
                var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
                texture.URL.Value = isOutput ? 
                    ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CONNECTOR_OUTPUT_TEXTURE) : 
                    ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CONNECTOR_INPUT_TEXTURE);
                texture.FilterMode.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.FILTER_MODE);
                texture.WrapModeU.Value = TextureWrapMode.Clamp;
                texture.WrapModeV.Value = TextureWrapMode.Clamp;
                texture.MipMaps.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIPMAPS);
                texture.MipMapFilter.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIPMAP_FILTER);
                texture.AnisotropicLevel.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ANISOTROPIC_LEVEL);
                texture.KeepOriginalMipMaps.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.KEEP_ORIGINAL_MIPMAPS);
                texture.CrunchCompressed.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CRUNCH_COMPRESSED);
                texture.Readable.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.READABLE);
                texture.Uncompressed.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.UNCOMPRESSED);
                texture.DirectLoad.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.DIRECT_LOAD);
                texture.ForceExactVariant.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.FORCE_EXACT_VARIANT);
                texture.PreferredFormat.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.PREFERRED_FORMAT);
                texture.PreferredProfile.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.PREFERRED_PROFILE);
                
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

            // Create sprite in temporary storage
            SpriteProvider spriteProvider = slot.World.RootSlot
                .FindChildOrAdd("__TEMP", false)
                .FindChildOrAdd($"{slot.LocalUser.UserName}-Call-Connector-Sprite-{(isOutput ? "Output" : "Input")}", false)
                .GetComponentOrAttach<SpriteProvider>();

            // Ensure cleanup when user leaves
            spriteProvider.Slot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;

            // Set up the texture if not already set
            if (spriteProvider.Texture.Target == null) {
                var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
                texture.URL.Value = isOutput ? 
                    ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CALL_CONNECTOR_OUTPUT_TEXTURE) : 
                    ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CALL_CONNECTOR_INPUT_TEXTURE);
                texture.FilterMode.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.FILTER_MODE);
                texture.WrapModeU.Value = TextureWrapMode.Clamp;
                texture.WrapModeV.Value = TextureWrapMode.Clamp;
                texture.MipMaps.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIPMAPS);
                texture.MipMapFilter.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIPMAP_FILTER);
                texture.AnisotropicLevel.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ANISOTROPIC_LEVEL);
                texture.KeepOriginalMipMaps.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.KEEP_ORIGINAL_MIPMAPS);
                texture.CrunchCompressed.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CRUNCH_COMPRESSED);
                texture.Readable.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.READABLE);
                texture.Uncompressed.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.UNCOMPRESSED);
                texture.DirectLoad.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.DIRECT_LOAD);
                texture.ForceExactVariant.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.FORCE_EXACT_VARIANT);
                texture.PreferredFormat.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.PREFERRED_FORMAT);
                texture.PreferredProfile.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.PREFERRED_PROFILE);
                
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
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Find and setup all wire points in the hierarchy
                WireHelper.FindAndSetupWirePoints(ui.Root);

                // Special handling for Update nodes
                if (node.GetType().IsSubclassOf(typeof(UpdateBase)) || node.GetType().IsSubclassOf(typeof(UserUpdateBase))) {
                    ProtoFluxVisualsOverhaul.Msg("🔄 Processing Update node UI...");
                    // Make sure we don't interfere with global reference UI generation
                    if (ui.Current.Name == "Global References") {
                        ProtoFluxVisualsOverhaul.Msg("⚠️ Skipping UI modification for global references panel");
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

                if (headerPanel != null) {
                    // Get the text component that's a sibling
                    var headerText = headerPanel.Slot.Parent.GetComponentInChildren<Text>();
                    if (headerText == null) return;

                    // Create TitleParent with OrderOffset -1
                    var titleParentSlot = ui.Root.AddSlot("TitleParent");
                    titleParentSlot.OrderOffset = -1;

                    // Add RectTransform to parent
                    var rectTransform = titleParentSlot.AttachComponent<RectTransform>();
                    
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
                        ProtoFluxVisualsOverhaul.Msg($"🔄 Found Update node of type: {nodeType.Name}");
                        // Check if it's an async update node
                        bool isAsync = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
                        nodeTypeColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
                        ProtoFluxVisualsOverhaul.Msg($"🎨 Setting Update node color to {(isAsync ? "ASYNC" : "SYNC")} flow color");
                    }
                    else 
                    {
                        nodeTypeColor = DatatypeColorHelper.GetTypeColor(nodeType);
                    }
                    ProtoFluxVisualsOverhaul.Msg($"🎨 Node type color: R:{nodeTypeColor.r:F2} G:{nodeTypeColor.g:F2} B:{nodeTypeColor.b:F2}");
                    
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
                    ProtoFluxVisualsOverhaul.Msg($"🖼️ Header image color: R:{headerColor.r:F2} G:{headerColor.g:F2} B:{headerColor.b:F2}");
                    
                    var brightness = (headerColor.r * 0.299f + headerColor.g * 0.587f + headerColor.b * 0.114f);
                    ProtoFluxVisualsOverhaul.Msg($"✨ Calculated brightness: {brightness:F2}");
                    
                    var textColor = brightness > 0.6f ? colorX.Black : colorX.White;
                    ProtoFluxVisualsOverhaul.Msg($"📝 Setting text color to: {(brightness > 0.6f ? "BLACK" : "WHITE")} based on brightness");
                    
                    // Set text color multiple ways to ensure it takes effect
                    newText.Color.Value = textColor;
                    newText.Color.ForceSet(textColor);
                    newText.Size.Value = 9.00f;
                    newText.AutoSizeMin.Value = 4f;
                    
                    // Convert color to hex based on brightness
                    newText.Content.Value = $"<color={(brightness > 0.6f ? "#000000" : "#FFFFFF")}><b>{headerText.Content.Value}</b></color>";
                    
                    ProtoFluxVisualsOverhaul.Msg($"📝 Text color set to: R:{newText.Color.Value.r:F2} G:{newText.Color.Value.g:F2} B:{newText.Color.Value.b:F2}");
                    
                    // Copy RectTransform settings
                    var newHeaderRect = newHeaderSlot.AttachComponent<RectTransform>();
                    var originalRect = headerPanel.Slot.GetComponent<RectTransform>();
                    if (originalRect != null) {
                        newHeaderRect.AnchorMin.Value = originalRect.AnchorMin.Value;
                        newHeaderRect.AnchorMax.Value = originalRect.AnchorMax.Value;
                        newHeaderRect.OffsetMin.Value = originalRect.OffsetMin.Value;
                        newHeaderRect.OffsetMax.Value = originalRect.OffsetMax.Value;
                    }

                    // Disable the original header and text
                    headerPanel.Slot.ActiveSelf = false;
                    headerText.Slot.ActiveSelf = false;

                    // Apply rounded corners to the new header
                    RoundedCornersHelper.ApplyRoundedCorners(image, true);

                    ProtoFluxVisualsOverhaul.Msg("✅ Successfully reorganized title layout!");
                }

                // Find the category text (it's the last Text component with dark gray color)
                var categoryText = ui.Root.GetComponentsInChildren<Text>()
                    .LastOrDefault(text => text.Color.Value == colorX.DarkGray);
                
                if (categoryText != null) {
                    categoryText.VerticalAlign.Value = TextVerticalAlignment.Middle;
                    categoryText.Size.Value = 8.00f;
                    categoryText.AlignmentMode.Value = AlignmentMode.LineBased;
                    categoryText.LineHeight.Value = 0.35f;
                }

                // Find the Overview panel
                var overviewPanel = ui.Root.GetComponentsInChildren<Image>()
                    .FirstOrDefault(img => img.Slot.Name == "Overview");

                if (overviewPanel != null && PermissionHelper.HasPermission(__instance)) {
                    // Only modify if we own the node
                    var targetNode = __instance.Node.Target;
                    if (targetNode != null) {
                        targetNode.ReferenceID.ExtractIDs(out ulong position, out byte user);
                        User nodeOwner = __instance.World.GetUserByAllocationID(user);

                        if (nodeOwner != null && position >= nodeOwner.AllocationIDStart && nodeOwner == __instance.LocalUser) {
                            // Set Overview panel to transparent
                            overviewPanel.Tint.Value = new colorX(0, 0, 0, 0); // Fully transparent
                        }
                    }
                }
            }
            catch (System.Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in ProtoFluxNodeVisual_BuildUI_Patch: {e.Message}");
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
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

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
                UniLog.Error($"Error in ProtoFluxNodeVisual_GenerateVisual_Patch: {e}");
            }
        }
    }

    // Patch to handle dynamic connector creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateInputElement")]
    public class ProtoFluxNodeVisual_GenerateInputElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

                // Find wire point slot - it will be in the last created slot
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot == null) return;

                // Create AudioClips structure
                WireHelper.CreateAudioClipsSlot(wirePointSlot);
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in input element generation: {e.Message}");
            }
        }
    }

    // Additional patch to handle output connector creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateOutputElement")]
    public class ProtoFluxNodeVisual_GenerateOutputElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

                // Find wire point slot - it will be in the last created slot
                var wirePointSlot = ui.Current?.FindChild("<WIRE_POINT>");
                if (wirePointSlot == null) return;

                // Create AudioClips structure
                WireHelper.CreateAudioClipsSlot(wirePointSlot);
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in output element generation: {e.Message}");
            }
        }
    }

    // Patch to handle Overview background color updates
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "UpdateNodeStatus")]
    public class ProtoFluxNodeVisual_UpdateNodeStatus_Patch {
        private static readonly FieldInfo overviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg");
        private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");

        public static bool Prefix(ProtoFluxNodeVisual __instance) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return true;

                // Check if this is for the local user
                if (__instance.LocalUser == null || __instance.World == null) return true;

                // Calculate the base background color
                colorX colorX1 = RadiantUI_Constants.BG_COLOR;
                if (__instance.IsSelected.Value) {
                    colorX1 = MathX.LerpUnclamped(colorX1, colorX.Cyan, 0.5f);
                }
                if (__instance.IsHighlighted.Value) {
                    colorX1 = MathX.LerpUnclamped(colorX1, colorX.Yellow, 0.1f);
                }
                if (!__instance.IsNodeValid) {
                    colorX1 = MathX.LerpUnclamped(colorX1, colorX.Red, 0.5f);
                }

                // Set background image color
                var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
                if (bgImageRef?.Target != null) {
                    bgImageRef.Target.Tint.Value = colorX1;
                }

                // Set overview background to transparent
                var overviewBgRef = (FieldDrive<colorX>)overviewBgField.GetValue(__instance);
                if (overviewBgRef?.IsLinkValid == true) {
                    overviewBgRef.Target.Value = new colorX(0, 0, 0, 0); // Fully transparent
                }

                return false; // Skip original method
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in UpdateNodeStatus patch: {e.Message}");
                return true; // Run original method on error
            }
        }
    }

    // Patch to handle Overview mode header visibility
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "OnChanges")]
    public class ProtoFluxNodeVisual_OnChanges_Patch {
        private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");
        private static readonly FieldInfo overviewVisualField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewVisual");
        private static readonly FieldInfo labelBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_labelBg");
        private static readonly FieldInfo labelTextField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_labelText");

        public static void Postfix(ProtoFluxNodeVisual __instance) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

                // Skip if instance is null or world is not available
                if (__instance == null || __instance.World == null) return;

                // Skip if we don't own this node
                if (!PermissionHelper.HasPermission(__instance)) return;

                // Get the node reference
                var node = __instance.Node.Target;
                if (node == null) return;

                // Extract IDs to check ownership
                node.ReferenceID.ExtractIDs(out ulong position, out byte user);
                User nodeOwner = __instance.World.GetUserByAllocationID(user);

                // Skip if we don't own this node
                if (nodeOwner == null || position < nodeOwner.AllocationIDStart || nodeOwner != __instance.LocalUser) return;

                // Get the overview visual field using reflection
                var overviewVisualRef = (FieldDrive<bool>)overviewVisualField.GetValue(__instance);
                var labelBgRef = (FieldDrive<bool>)labelBgField.GetValue(__instance);
                var labelTextRef = (FieldDrive<bool>)labelTextField.GetValue(__instance);

                if (!overviewVisualRef.IsLinkValid) return;

                // Check if Overview mode should be active
                var isOverviewMode = false;
                if (!__instance.IsHighlighted.Value) { // Only check settings if not highlighted
                    var settings = __instance.LocalUser?.GetComponent<ProtofluxUserEditSettings>();
                    if (settings == null) return;
                    isOverviewMode = settings.OverviewMode.Value;
                }

                // Set visibility states
                overviewVisualRef.Target.Value = isOverviewMode;
                if (labelBgRef.IsLinkValid) labelBgRef.Target.Value = !isOverviewMode;
                if (labelTextRef.IsLinkValid) labelTextRef.Target.Value = !isOverviewMode;

                // Handle TitleParent>Header visibility
                var titleParent = __instance.Slot.FindChild("TitleParent");
                if (titleParent != null) {
                    var header = titleParent.FindChild("Header");
                    if (header != null) {
                        header.ActiveSelf = !isOverviewMode;
                    }
                }

                // Handle connector label visibility
                var overlappingLayout = __instance.Slot.FindChild("Overlapping Layout");
                if (overlappingLayout != null) {
                    // Handle input labels
                    var inputsOps = overlappingLayout.FindChild("Inputs & Operations");
                    if (inputsOps != null) {
                        foreach (var group in inputsOps.Children) {
                            var imageSlot = group.FindChild("Image");
                            if (imageSlot != null) {
                                imageSlot.ActiveSelf = !isOverviewMode;
                            }
                        }
                    }

                    // Handle output labels
                    var outputsImps = overlappingLayout.FindChild("Outputs & Impulses");
                    if (outputsImps != null) {
                        foreach (var group in outputsImps.Children) {
                            var imageSlot = group.FindChild("Image");
                            if (imageSlot != null) {
                                imageSlot.ActiveSelf = !isOverviewMode;
                            }
                        }
                    }

                    // Set Overview RectTransform values
                    var overview = overlappingLayout.FindChild("Overview");
                    if (overview != null) {
                        var rectTransform = overview.GetComponent<RectTransform>();
                        if (rectTransform != null && __instance.LocalUser != null) {
                            rectTransform.AnchorMin.Value = new float2(0.05f, 0f);
                            rectTransform.AnchorMax.Value = new float2(0.95f, 0.85f);
                            rectTransform.OffsetMin.Value = new float2(16f, 0f);
                            rectTransform.OffsetMax.Value = new float2(-16f, 24f);
                            rectTransform.Pivot.Value = new float2(0.5f, 0.5f);
                        }
                    }
                }
            } catch (Exception e) {
                UniLog.Error($"Error in ProtoFluxNodeVisual_OnChanges_Patch: {e}");
            }
        }
    }

    // Patch to sync wire colors with ImpulseType colors
    [HarmonyPatch(typeof(ProtoFluxWireManager), "Setup")]
    [HarmonyPatch(new Type[] { typeof(WireType), typeof(float), typeof(colorX), typeof(int), typeof(bool), typeof(bool) })]
    public class ProtoFluxWireManager_Setup_Patch {
        public static void Postfix(
            ProtoFluxWireManager __instance, 
            WireType type,
            float width,
            colorX startColor,
            int atlasOffset,
            bool collider,
            bool reverseTexture,
            SyncRef<StripeWireMesh> ____wireMesh)
        {
            try {
                // Skip if mod is disabled or required components are missing
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED) || 
                    __instance == null || 
                    !__instance.Enabled || 
                    ____wireMesh?.Target == null ||
                    __instance.Slot == null) return;

                // Rest of the color handling code...
                colorX color1 = startColor;
                colorX color2 = startColor;

                // Check if this is an impulse/operation wire (atlas offset 4 is used for impulse/flow wires)
                if (atlasOffset == 4) {
                    var meshRenderer = ____wireMesh.Target?.Slot.GetComponent<MeshRenderer>();
                    
                    // Get colors from the wire manager's properties
                    colorX sourceColor, targetColor;
                    if (type == WireType.Output) {
                        // For output wires, we're at the start point
                        sourceColor = __instance.StartColor.Value;
                        targetColor = __instance.EndColor.Value;
                    } else {
                        // For input wires, we're at the end point
                        sourceColor = __instance.EndColor.Value;
                        targetColor = __instance.StartColor.Value;
                    }

                    if (meshRenderer?.Material.Target is FresnelMaterial fresnelMat) {
                        fresnelMat.NearColor.Value = sourceColor * 1.2f;
                        fresnelMat.FarColor.Value = sourceColor * 0.8f;
                        fresnelMat.Exponent.Value = 2.0f;
                        fresnelMat.GammaCurve.Value = 1.2f;
                    }

                    // Set gradient colors based on wire direction
                    if (type == WireType.Output) {
                        color1 = sourceColor;
                        color2 = targetColor;
                    } else {
                        color1 = targetColor;
                        color2 = sourceColor;
                    }

                    // Apply slight intensity variation for visual interest
                    color1 = color1 * 1.1f;
                    color2 = color2 * 0.9f;
                }

                // Apply the colors to create a gradient
                __instance.StartColor.Value = color1;
                __instance.EndColor.Value = color2;

                // Update the wire mesh colors
                if (____wireMesh?.Target != null) {
                    ____wireMesh.Target.Color0.Value = color1;
                    ____wireMesh.Target.Color1.Value = color2;
                }
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in ProtoFluxWireManager_Setup_Patch: {e.Message}");
            }
        }
    }

    // Patch to handle global references panel styling
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateGlobalRefs")]
    public class ProtoFluxNodeVisual_GenerateGlobalRefs_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ProtoFluxNode node) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                ProtoFluxVisualsOverhaul.Msg("🔧 Styling global references panel...");

                // Find the global references panel
                var globalRefsPanel = ui.Root.FindChild("Global References");
                if (globalRefsPanel == null) return;

                // Style the panel
                var images = globalRefsPanel.GetComponentsInChildren<Image>();
                foreach (var img in images) {
                    // Skip connectors
                    if (img.Slot.Name == "Connector") continue;

                    // Apply rounded corners to backgrounds
                    if (img.Tint.Value == RadiantUI_Constants.BG_COLOR) {
                        RoundedCornersHelper.ApplyRoundedCorners(img, false);
                    }
                }

                ProtoFluxVisualsOverhaul.Msg("✅ Global references panel styled successfully!");
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in GenerateGlobalRefs patch: {e.Message}");
            }
        }
    }

    // Patch to handle global reference element generation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual))]
    [HarmonyPatch("GenerateGlobalRefElement")]
    [HarmonyPriority(HarmonyLib.Priority.High)]
    public class ProtoFluxNodeVisual_GenerateGlobalRefElement_Patch {
        private static bool IsUpdateNodeField(ProtoFluxNode node, ISyncRef globalRef, Type referenceType) {
            if (node == null) return false;
            
            // Check if this is an Update node
            if (node.GetType().IsSubclassOf(typeof(UpdateBase)) || node.GetType().IsSubclassOf(typeof(UserUpdateBase))) {
                // Check if this is the SkipIfNull field
                var field = node.GetType().GetField("SkipIfNull");
                return field != null && field.FieldType == globalRef.GetType();
            }
            return false;
        }

        public static bool Prefix(ProtoFluxNodeVisual __instance, UIBuilder ui, Type referenceType, ISyncRef globalRef) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return true;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return true;

                var node = __instance.Node.Target;
                if (node == null) return true;

                // Special handling for Update nodes
                if (IsUpdateNodeField(node, globalRef, referenceType)) {
                    ProtoFluxVisualsOverhaul.Msg($"🎨 Processing global reference for Update node: {referenceType.Name}");
                    
                    try {
                        // Try to find an existing global value of the correct type
                        var world = __instance.World;
                        var existingGlobals = world.RootSlot.GetComponentsInChildren<FrooxEngine.ProtoFlux.GlobalValue<bool>>();
                        
                        // Try to use an existing global value first
                        foreach (var existing in existingGlobals) {
                            try {
                                if (existing.GetType() == typeof(FrooxEngine.ProtoFlux.GlobalValue<bool>)) {
                                    globalRef.TrySet(existing);
                                    ProtoFluxVisualsOverhaul.Msg("✅ Found and set existing global value");
                                    return false;
                                }
                            }
                            catch { } // Ignore if this one doesn't work
                        }

                        // If no existing value works, create a new one
                        var globalValueSlot = world.RootSlot.AddSlot("Global Value");
                        var globalValue = globalValueSlot.AttachComponent<FrooxEngine.ProtoFlux.GlobalValue<bool>>();
                        globalRef.TrySet(globalValue);
                        ProtoFluxVisualsOverhaul.Msg("✅ Created and set new global value");
                        return false;
                    }
                    catch (Exception e) {
                        ProtoFluxVisualsOverhaul.Msg($"⚠️ Error handling global value: {e.Message}");
                    }
                }

                return true; // Let original method run for non-Update nodes
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in GenerateGlobalRefElement patch: {e.Message}");
                return true; // Run original on error
            }
        }

        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, Type referenceType, ISyncRef globalRef) {
            try {
                // Skip if disabled
                if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.ENABLED)) return;

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                var node = __instance.Node.Target;
                if (node == null) return;

                // Special handling for Update nodes
                if (IsUpdateNodeField(node, globalRef, referenceType)) {
                    ProtoFluxVisualsOverhaul.Msg($"🎨 Styling global reference for Update node: {referenceType.Name}");
                    
                    // Find the most recently added global reference UI elements
                    var images = ui.Current.GetComponentsInChildren<Image>();
                    foreach (var img in images) {
                        // Skip connectors
                        if (img.Slot.Name == "Connector") continue;

                        // Apply rounded corners to backgrounds
                        if (img.Tint.Value == RadiantUI_Constants.BG_COLOR) {
                            RoundedCornersHelper.ApplyRoundedCorners(img, false);
                        }
                    }
                }
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in GenerateGlobalRefElement postfix: {e.Message}");
            }
        }
    }

    // Patch to handle global value type mismatches
    [HarmonyPatch]
    public class SyncRef_SetTarget_Patch {
        static MethodBase TargetMethod() {
            // Find the set_Target method in the ISyncRef interface implementation of SyncRef<IGlobalValueProxy<bool>>
            var syncRefType = typeof(SyncRef<>).MakeGenericType(typeof(IGlobalValueProxy<bool>));
            var interfaceMap = syncRefType.GetInterfaceMap(typeof(ISyncRef));
            return interfaceMap.TargetMethods.First(m => m.Name.Contains("set_Target"));
        }

        public static bool Prefix(object __instance, IWorldElement value) {
            try {
                if (value == null) return true;

                // Check if this is a global value with wrong type
                if (value is GlobalValue<string>) {
                    ProtoFluxVisualsOverhaul.Msg("🔄 Intercepted string global value assignment");
                    
                    // Get the world from the value since it's a WorldElement
                    var world = value.World;
                    if (world != null) {
                        // Create a new boolean global value
                        var globalValueSlot = world.RootSlot.AddSlot("Global Value");
                        var globalValue = globalValueSlot.AttachComponent<FrooxEngine.ProtoFlux.GlobalValue<bool>>();
                        
                        // Set the target to our new boolean global value
                        var setTargetMethod = __instance.GetType().GetMethod("set_Target", BindingFlags.Public | BindingFlags.Instance);
                        setTargetMethod.Invoke(__instance, new object[] { globalValue });
                        
                        ProtoFluxVisualsOverhaul.Msg("✅ Created and set new boolean global value");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e) {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error in set_Target patch: {e.Message}");
                return true;
            }
        }
    }
} 