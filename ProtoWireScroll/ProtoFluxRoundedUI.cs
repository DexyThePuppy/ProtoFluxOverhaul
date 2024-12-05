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

namespace ProtoWireScroll {
    // Patch to add rounded corners to ProtoFlux node visuals
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "BuildUI")]
    public class ProtoFluxNodeVisual_BuildUI_Patch {
        private static readonly Uri ROUNDED_TEXTURE = new Uri("resdb:///3ee5c0335455c19970d877e2b80f7869539df43fccb8fc64b38e320fc44c154f.png");
        
        // ColorMyProtoFlux color settings
        private static readonly colorX NODE_CATEGORY_TEXT_LIGHT_COLOR = new colorX(0.75f);
        private static readonly colorX NODE_CATEGORY_TEXT_DARK_COLOR = new colorX(0.25f);

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
} 