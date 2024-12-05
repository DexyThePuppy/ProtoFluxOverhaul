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

namespace ProtoWireScroll {
    // Patch to add rounded corners to ProtoFlux node visuals
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "BuildUI")]
    public class ProtoFluxNodeVisual_BuildUI_Patch {
        private static readonly Uri ROUNDED_TEXTURE = new Uri("resdb:///3ee5c0335455c19970d877e2b80f7869539df43fccb8fc64b38e320fc44c154f.png");
        private static readonly Uri CONNECTOR_TEXTURE = new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxWiresThatCanScroll/refs/heads/main/ProtoWireScroll/Images/Connector.png");
        private static readonly Uri CALL_CONNECTOR_TEXTURE = new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxWiresThatCanScroll/refs/heads/main/ProtoWireScroll/Images/Connector_Call.png");
        
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
                texture.URL.Value = CONNECTOR_TEXTURE;
                texture.FilterMode.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FILTER_MODE);
                texture.WrapModeU.Value = TextureWrapMode.Clamp;
                texture.WrapModeV.Value = TextureWrapMode.Clamp;
                texture.MipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAPS);
                texture.MipMapFilter.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAP_FILTER);
                texture.AnisotropicLevel.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.ANISOTROPIC_LEVEL);
                texture.KeepOriginalMipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.KEEP_ORIGINAL_MIPMAPS);
                texture.CrunchCompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.CRUNCH_COMPRESSED);
                texture.Readable.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.READABLE);
                texture.Uncompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.UNCOMPRESSED);
                texture.DirectLoad.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.DIRECT_LOAD);
                texture.ForceExactVariant.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FORCE_EXACT_VARIANT);
                
                spriteProvider.Texture.Target = texture;
                // For outputs (right side), keep normal orientation
                // For inputs (left side), flip horizontally
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
                texture.URL.Value = CALL_CONNECTOR_TEXTURE;
                texture.FilterMode.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FILTER_MODE);
                texture.WrapModeU.Value = TextureWrapMode.Clamp;
                texture.WrapModeV.Value = TextureWrapMode.Clamp;
                texture.MipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAPS);
                texture.MipMapFilter.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAP_FILTER);
                texture.AnisotropicLevel.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.ANISOTROPIC_LEVEL);
                texture.KeepOriginalMipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.KEEP_ORIGINAL_MIPMAPS);
                texture.CrunchCompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.CRUNCH_COMPRESSED);
                texture.Readable.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.READABLE);
                texture.Uncompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.UNCOMPRESSED);
                texture.DirectLoad.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.DIRECT_LOAD);
                texture.ForceExactVariant.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FORCE_EXACT_VARIANT);
                
                spriteProvider.Texture.Target = texture;
                // For outputs (right side), keep normal orientation
                // For inputs (left side), flip horizontally
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

        // Checks if the current user has permission to modify the node visual
        // This ensures only the node's owner can modify its properties
        // Returns: True if user has permission, false otherwise
        public static bool HasPermission(ProtoFluxNodeVisual instance) {
            if (instance == null || instance.World == null) return false;

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

                return slotAllocUser != null && 
                       slotPosition >= slotAllocUser.AllocationIDStart && 
                       slotAllocUser == instance.LocalUser;
            }

            return visualAllocUser == instance.LocalUser;
        }

        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ProtoFluxNode node) {
            try {
                // Skip if disabled
                if (!ProtoWireScroll.Config.GetValue(ProtoWireScroll.ENABLED)) return;

                // === User Permission Check ===
                if (!HasPermission(__instance)) return;

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
                    overlappingLayout.PaddingTop.Value = 4f;
                    overlappingLayout.PaddingRight.Value = 4f;
                    overlappingLayout.PaddingBottom.Value = 4f;
                    overlappingLayout.PaddingLeft.Value = 4f;
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
                    var nodeTypeColor = DatatypeColorHelper.GetTypeColor(node.GetType());
                    ProtoWireScroll.Msg($"🎨 Node type color: R:{nodeTypeColor.r:F2} G:{nodeTypeColor.g:F2} B:{nodeTypeColor.b:F2}");
                    
                    // Apply the color to the header image
                    image.Tint.Value = nodeTypeColor;
                    
                    // Create a copy of the text under the new header
                    var newTextSlot = newHeaderSlot.AddSlot("Text");
                    newTextSlot.ActiveSelf = true;
                    var newText = newTextSlot.AttachComponent<Text>();
                    
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
                    ProtoWireScroll.Msg($"🖼️ Header image color: R:{headerColor.r:F2} G:{headerColor.g:F2} B:{headerColor.b:F2}");
                    
                    var brightness = (headerColor.r * 0.299f + headerColor.g * 0.587f + headerColor.b * 0.114f);
                    ProtoWireScroll.Msg($"✨ Calculated brightness: {brightness:F2}");
                    
                    var textColor = brightness > 0.6f ? colorX.Black : colorX.White;
                    ProtoWireScroll.Msg($"📝 Setting text color to: {(brightness > 0.6f ? "BLACK" : "WHITE")} based on brightness");
                    
                    // Set text color multiple ways to ensure it takes effect
                    newText.Color.Value = textColor;
                    newText.Color.ForceSet(textColor);
                    
                    // Convert color to hex based on brightness
                    newText.Content.Value = $"<color={(brightness > 0.6f ? "#000000" : "#FFFFFF")}><b>{headerText.Content.Value}</b></color>";
                    
                    ProtoWireScroll.Msg($"📝 Text color set to: R:{newText.Color.Value.r:F2} G:{newText.Color.Value.g:F2} B:{newText.Color.Value.b:F2}");
                    
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
                    ApplyRoundedCorners(image);

                    ProtoWireScroll.Msg("✅ Successfully reorganized title layout!");
                }
            }
            catch (System.Exception e) {
                ProtoWireScroll.Msg($"❌ Error in ProtoFluxNodeVisual_BuildUI_Patch: {e.Message}");
            }
        }

        private static void ApplyRoundedCorners(Image image) {
            // Skip if already applied
            if (image.Sprite.Target is SpriteProvider) return;

            ProtoWireScroll.Msg("🐾 Applying rounded corners to header panel...");

            // Create a SpriteProvider for rounded corners
            var spriteProvider = image.Slot.AttachComponent<SpriteProvider>();
            ProtoWireScroll.Msg("✅ Created SpriteProvider for header");
            
            // Set up the texture
            var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
            texture.URL.Value = ROUNDED_TEXTURE;
            texture.FilterMode.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FILTER_MODE);
            texture.WrapModeU.Value = TextureWrapMode.Clamp;
            texture.WrapModeV.Value = TextureWrapMode.Clamp;
            texture.MipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAPS);
            texture.MipMapFilter.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAP_FILTER);
            texture.AnisotropicLevel.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.ANISOTROPIC_LEVEL);
            texture.KeepOriginalMipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.KEEP_ORIGINAL_MIPMAPS);
            texture.CrunchCompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.CRUNCH_COMPRESSED);
            texture.Readable.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.READABLE);
            texture.Uncompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.UNCOMPRESSED);
            texture.DirectLoad.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.DIRECT_LOAD);
            texture.ForceExactVariant.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FORCE_EXACT_VARIANT);
            ProtoWireScroll.Msg("✅ Set up texture for header");
            
            // Configure the sprite provider based on the image settings
            spriteProvider.Texture.Target = texture;
            spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f);  // x:0 y:0 width:1 height:1
            spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f); 
            spriteProvider.Scale.Value = 0.02f;  // Scale: 0.05
            spriteProvider.FixedSize.Value = 1.00f;  // FixedSize: 1.00
            ProtoWireScroll.Msg("✅ Configured header sprite provider settings");

            // Update the image to use the sprite
            image.Sprite.Target = spriteProvider;
            
            // Preserve color and tint settings
            image.PreserveAspect.Value = true;
            ProtoWireScroll.Msg("✅ Successfully applied rounded corners to header!");
        }
    }

    // Patch to handle initial node creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateVisual")]
    public class ProtoFluxNodeVisual_GenerateVisual_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, ProtoFluxNode node) {
            try {
                // Skip if disabled
                if (!ProtoWireScroll.Config.GetValue(ProtoWireScroll.ENABLED)) return;

                // === User Permission Check ===
                if (!ProtoFluxNodeVisual_BuildUI_Patch.HasPermission(__instance)) return;

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
            }
            catch (System.Exception e) {
                ProtoWireScroll.Msg($"❌ Error in ProtoFluxNodeVisual_GenerateVisual_Patch: {e.Message}");
            }
        }
    }

    // Patch to handle dynamic connector creation
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateInputElement")]
    public class ProtoFluxNodeVisual_GenerateInputElement_Patch {
        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui) {
            try {
                // Skip if disabled
                if (!ProtoWireScroll.Config.GetValue(ProtoWireScroll.ENABLED)) return;

                // === User Permission Check ===
                if (!ProtoFluxNodeVisual_BuildUI_Patch.HasPermission(__instance)) return;

                // Find the most recently added connector
                var connectorImage = ui.Root.GetComponentsInChildren<Image>()
                    .Where(img => img.Slot.Name == "Connector")
                    .LastOrDefault();

                if (connectorImage != null) {
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
                }
            }
            catch (System.Exception e) {
                ProtoWireScroll.Msg($"❌ Error in ProtoFluxNodeVisual_GenerateInputElement_Patch: {e.Message}");
            }
        }
    }

    // Keep the OnChanges patch for the background image
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "OnChanges")]
    public class ProtoFluxNodeVisual_OnChanges_Patch {
        private static readonly Uri ROUNDED_TEXTURE = new Uri("resdb:///3ee5c0335455c19970d877e2b80f7869539df43fccb8fc64b38e320fc44c154f.png");
        private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");

        public static void Postfix(ProtoFluxNodeVisual __instance) {
            try {
                // Skip if disabled
                if (!ProtoWireScroll.Config.GetValue(ProtoWireScroll.ENABLED)) return;

                // === User Permission Check ===
                if (!ProtoFluxNodeVisual_BuildUI_Patch.HasPermission(__instance)) return;

                // Get the background image component using reflection
                var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
                var bgImage = bgImageRef?.Target;
                if (bgImage == null) return;

                // Apply rounded corners to background
                ApplyRoundedCorners(bgImage);
            }
            catch (System.Exception e) {
                ProtoWireScroll.Msg($"❌ Error in ProtoFluxNodeVisual_OnChanges_Patch: {e.Message}");
            }
        }

        private static void ApplyRoundedCorners(Image image) {
            // Skip if already applied
            if (image.Sprite.Target is SpriteProvider) return;

            ProtoWireScroll.Msg("🐾 Applying rounded corners to background...");

            // Create a SpriteProvider for rounded corners
            var spriteProvider = image.Slot.AttachComponent<SpriteProvider>();
            ProtoWireScroll.Msg("✅ Created SpriteProvider for background");
            
            // Set up the texture
            var texture = spriteProvider.Slot.AttachComponent<StaticTexture2D>();
            texture.URL.Value = ROUNDED_TEXTURE;
            texture.FilterMode.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FILTER_MODE);
            texture.WrapModeU.Value = TextureWrapMode.Clamp;
            texture.WrapModeV.Value = TextureWrapMode.Clamp;
            texture.MipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAPS);
            texture.MipMapFilter.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.MIPMAP_FILTER);
            texture.AnisotropicLevel.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.ANISOTROPIC_LEVEL);
            texture.KeepOriginalMipMaps.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.KEEP_ORIGINAL_MIPMAPS);
            texture.CrunchCompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.CRUNCH_COMPRESSED);
            texture.Readable.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.READABLE);
            texture.Uncompressed.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.UNCOMPRESSED);
            texture.DirectLoad.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.DIRECT_LOAD);
            texture.ForceExactVariant.Value = ProtoWireScroll.Config.GetValue(ProtoWireScroll.FORCE_EXACT_VARIANT);

            ProtoWireScroll.Msg("✅ Set up texture for background");
            
            // Configure the sprite provider based on the image settings
            spriteProvider.Texture.Target = texture;
            spriteProvider.Rect.Value = new Elements.Core.Rect(0f, 0f, 1f, 1f);  // x:0 y:0 width:1 height:1
            spriteProvider.Borders.Value = new float4(0.5f, 0.5f, 0.5f, 0.5f);  // x:0.5 y:0 z:0 w:0
            spriteProvider.Scale.Value = 0.03f;  // Scale: 0.05
            spriteProvider.FixedSize.Value = 1.00f;  // FixedSize: 1.00
            ProtoWireScroll.Msg("✅ Configured background sprite provider settings");

            // Update the image to use the sprite
            image.Sprite.Target = spriteProvider;
            
            // Preserve color and tint settings
            image.PreserveAspect.Value = true;
            ProtoWireScroll.Msg("✅ Successfully applied rounded corners to background!");
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
                // Skip if disabled
                if (!ProtoWireScroll.Config.GetValue(ProtoWireScroll.ENABLED)) return;

                // Get the wire color based on the atlas offset
                // Atlas offset 4 is for flow (impulse/operation) wires
                colorX wireColor = startColor;
                if (atlasOffset == 4) {
                    // This is a flow wire, use flow colors
                    wireColor = DatatypeColorHelper.SYNC_FLOW_COLOR;
                }

                // Apply the color to both start and end of wire
                __instance.StartColor.Value = wireColor;
                __instance.EndColor.Value = wireColor;

                // Also update the wire mesh colors
                if (____wireMesh?.Target != null) {
                    ____wireMesh.Target.Color0.Value = wireColor;
                    ____wireMesh.Target.Color1.Value = wireColor;
                }
            }
            catch (System.Exception e) {
                ProtoWireScroll.Msg($"❌ Error in ProtoFluxWireManager_Setup_Patch: {e.Message}");
            }
        }
    }
} 