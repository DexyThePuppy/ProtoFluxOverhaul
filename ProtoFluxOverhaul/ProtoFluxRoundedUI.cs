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
        public static void ApplyRoundedCorners(Image image, bool isHeader = false, colorX? headerColor = null, bool preserveOriginalColor = false) {
            // Store original color if we need to preserve it
            colorX originalColor = image.Tint.Value;
            
            // For backgrounds, check if we need to update the tint even if sprite provider exists
            if (image.Sprite.Target is SpriteProvider existingSpriteProvider) {
                // If this is a background and we have a header color and the config is enabled, update the tint
                if (!isHeader && !preserveOriginalColor && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                    // Drive the header color to prevent changes over time
                    var headerColorVariable = image.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                    headerColorVariable.Value.Value = headerColor.Value;
                    var headerColorDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                    headerColorDriver.DriveTarget.Target = image.Tint;
                    headerColorDriver.ValueSource.Target = headerColorVariable.Value;
                    Logger.LogUI("Header Color Background Update", $"Updated existing background tint to header color: R:{headerColor.Value.r:F2} G:{headerColor.Value.g:F2} B:{headerColor.Value.b:F2}");
                }
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
            spriteProvider.Scale.Value = isHeader ? 0.03f : 0.07f;  // Different scale for header vs background
            spriteProvider.FixedSize.Value = 1.00f;  // FixedSize: 1.00
            Logger.LogUI("Sprite Config", $"Configured {(isHeader ? "header" : "background")} sprite provider settings");

            // Update the image to use the sprite
            image.Sprite.Target = spriteProvider;
            
            // Apply color logic
            if (preserveOriginalColor) {
                // Drive the original color for connector labels to prevent changes over time
                var originalColorVariable = image.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                originalColorVariable.Value.Value = originalColor;
                var originalColorDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                originalColorDriver.DriveTarget.Target = image.Tint;
                originalColorDriver.ValueSource.Target = originalColorVariable.Value;
                Logger.LogUI("Color Preserved", $"Preserved original color for connector label: R:{originalColor.r:F2} G:{originalColor.g:F2} B:{originalColor.b:F2}");
            } else if (!isHeader && headerColor.HasValue && ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                // Drive header color to background if config option is enabled to prevent changes over time
                var headerColorVariable = image.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                headerColorVariable.Value.Value = headerColor.Value;
                var headerColorDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                headerColorDriver.DriveTarget.Target = image.Tint;
                headerColorDriver.ValueSource.Target = headerColorVariable.Value;
                Logger.LogUI("Header Color Background", $"Applied header color to background: R:{headerColor.Value.r:F2} G:{headerColor.Value.g:F2} B:{headerColor.Value.b:F2}");
            }
            
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
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_OUTPUT_TEXTURE) : 
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_OUTPUT_TEXTURE);
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
                    ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CALL_CONNECTOR_OUTPUT_TEXTURE);
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
                        textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_OUTPUT_TEXTURE);
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
                            ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_OUTPUT_TEXTURE) : 
                            ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_OUTPUT_TEXTURE);
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
                            // Drive the spacer image tint to prevent changes over time
                            var spacerColorVariable = spacerImage.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                            spacerColorVariable.Value.Value = RadiantUI_Constants.HEADER;
                            var spacerColorDriver = spacerImage.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                            spacerColorDriver.DriveTarget.Target = spacerImage.Tint;
                            spacerColorDriver.ValueSource.Target = spacerColorVariable.Value;
                            
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
                
                // Drive the color to the header image to prevent changes over time
                var headerTintVariable = image.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                headerTintVariable.Value.Value = nodeTypeColor;
                var headerTintDriver = image.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                headerTintDriver.DriveTarget.Target = image.Tint;
                headerTintDriver.ValueSource.Target = headerTintVariable.Value;
                
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

                // Apply rounded corners to the background with header color if config is enabled
                var backgroundImageRef = (SyncRef<Image>)AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage").GetValue(__instance);
                if (backgroundImageRef?.Target != null) {
                    RoundedCornersHelper.ApplyRoundedCorners(backgroundImageRef.Target, false, nodeTypeColor);
                }

                // Apply node background sprite to connector label backgrounds if config is enabled
                if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                    var connectorImages = ui.Root.GetComponentsInChildren<Image>()
                        .Where(img => img.Slot.Name == "Connector")
                        .ToList();

                    foreach (var connectorImage in connectorImages) {
                        // Find the label background image (sibling Image component that's not the Connector)
                        var parentSlot = connectorImage.Slot.Parent;
                        var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                            .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                        
                        if (labelBackgroundImage != null) {
                            // Apply the header sprite provider for connector labels but preserve original color
                            RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, true);
                            Logger.LogUI("Connector Label Background", $"Applied header sprite to connector label background while preserving original color");
                            
                            // Find and center the text in the label
                            var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                            if (textSlot != null) {
                                var textComponent = textSlot.GetComponent<Text>();
                                if (textComponent != null) {
                                    textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                    Logger.LogUI("Connector Label Text", $"Set connector label text to center alignment");
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
                    
                    // Apply node type color to category text if config is enabled
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                        // Drive the category text color to prevent changes over time
                        var categoryColorVariable = categoryText.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                        categoryColorVariable.Value.Value = nodeTypeColor; // Use full brightness node type color
                        var categoryColorDriver = categoryText.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                        categoryColorDriver.DriveTarget.Target = categoryText.Color;
                        categoryColorDriver.ValueSource.Target = categoryColorVariable.Value;
                        Logger.LogUI("Category Color", $"Applied node type color to category text: R:{nodeTypeColor.r:F2} G:{nodeTypeColor.g:F2} B:{nodeTypeColor.b:F2}");
                    }
                }
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
                    RoundedCornersHelper.ApplyRoundedCorners(bgImageRef.Target, false, nodeTypeColor);
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
                    
                    // Apply node background sprite to connector label background if config is enabled
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                        // Find the label background image (sibling Image component that's not the Connector)
                        var parentSlot = connectorImage.Slot.Parent;
                        var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                            .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                        
                        if (labelBackgroundImage != null) {
                            // Apply the header sprite provider for connector labels but preserve original color
                            RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, true);
                            Logger.LogUI("Connector Label Background", $"Applied header sprite to connector label background in GenerateVisual while preserving original color");
                            
                            // Find and center the text in the label
                            var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                            if (textSlot != null) {
                                var textComponent = textSlot.GetComponent<Text>();
                                if (textComponent != null) {
                                    textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                    Logger.LogUI("Connector Label Text", $"Set connector label text to center alignment in GenerateVisual");
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

                    // Set the correct RectTransform settings for input connectors
                    connectorImage.RectTransform.SetFixedHorizontal(0.0f, 16f, 0.0f);

                    // Apply node background sprite to connector label background if config is enabled
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                        var node = __instance.Node.Target;
                        if (node != null) {
                            colorX nodeTypeColor;
                            var nodeType = node.GetType();
                            if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
                            {
                                bool isAsyncNode = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
                                nodeTypeColor = isAsyncNode ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
                            }
                            else 
                            {
                                nodeTypeColor = DatatypeColorHelper.GetTypeColor(nodeType);
                            }

                            // Find the label background image (sibling Image component that's not the Connector)
                            var parentSlot = connectorImage.Slot.Parent;
                            var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                                .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                            
                            if (labelBackgroundImage != null) {
                                // Apply the header sprite provider for connector labels but preserve original color
                                RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, true);
                                Logger.LogUI("Connector Label Background", $"Applied header sprite to dynamic input connector label background while preserving original color");
                                
                                // Find and center the text in the label
                                var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                                if (textSlot != null) {
                                    var textComponent = textSlot.GetComponent<Text>();
                                    if (textComponent != null) {
                                        textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                        Logger.LogUI("Connector Label Text", $"Set dynamic input connector label text to center alignment");
                                    }
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

                    // Set the correct RectTransform settings for output connectors
                    connectorImage.RectTransform.SetFixedHorizontal(-16f, 0.0f, 1f);

                    // Apply node background sprite to connector label background if config is enabled
                    if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                        var node = __instance.Node.Target;
                        if (node != null) {
                            colorX nodeTypeColor;
                            var nodeType = node.GetType();
                            if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
                            {
                                bool isAsyncNode = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
                                nodeTypeColor = isAsyncNode ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
                            }
                            else 
                            {
                                nodeTypeColor = DatatypeColorHelper.GetTypeColor(nodeType);
                            }

                            // Find the label background image (sibling Image component that's not the Connector)
                            var parentSlot = connectorImage.Slot.Parent;
                            var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                                .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                            
                            if (labelBackgroundImage != null) {
                                // Apply the header sprite provider for connector labels but preserve original color
                                RoundedCornersHelper.ApplyRoundedCorners(labelBackgroundImage, true, null, true);
                                Logger.LogUI("Connector Label Background", $"Applied header sprite to dynamic output connector label background while preserving original color");
                                
                                // Find and center the text in the label
                                var textSlot = labelBackgroundImage.Slot.FindChild("Text");
                                if (textSlot != null) {
                                    var textComponent = textSlot.GetComponent<Text>();
                                    if (textComponent != null) {
                                        textComponent.VerticalAlign.Value = TextVerticalAlignment.Middle;
                                        Logger.LogUI("Connector Label Text", $"Set dynamic output connector label text to center alignment");
                                    }
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

    // Patch to override UpdateNodeStatus to respect header color for background
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "UpdateNodeStatus")]
    public class ProtoFluxNodeVisual_UpdateNodeStatus_Patch {
        public static bool Prefix(ProtoFluxNodeVisual __instance) {
            try {
                // If our mod is disabled or the config option is disabled, let the original method run
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED) || 
                    !ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) {
                    return true; // Continue with original method
                }

                // Skip if we don't own this node
                if (!PermissionHelper.HasPermission(__instance)) {
                    return true; // Continue with original method
                }

                // Get the background image using reflection
                var bgImageRef = (SyncRef<Image>)AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage").GetValue(__instance);
                if (bgImageRef?.Target == null) {
                    return true; // Continue with original method
                }

                // Get the node's type color
                var node = __instance.Node.Target;
                if (node == null) {
                    return true; // Continue with original method
                }

                colorX nodeTypeColor;
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

                // Replicate the original UpdateNodeStatus logic but with our header color as the base
                colorX finalColor = new colorX(nodeTypeColor.r * 0.35f, nodeTypeColor.g * 0.35f, nodeTypeColor.b * 0.35f, nodeTypeColor.a); // Start with header color at 50% brightness (darker) but preserve alpha
                
                if (__instance.IsSelected.Value)
                {
                    finalColor = MathX.LerpUnclamped(finalColor, colorX.Cyan, 0.5f);
                }
                if (__instance.IsHighlighted.Value)
                {
                    finalColor = MathX.LerpUnclamped(finalColor, colorX.Yellow, 0.1f);
                }
                if (!__instance.IsNodeValid)
                {
                    finalColor = MathX.LerpUnclamped(finalColor, colorX.Red, 0.5f);
                }
                
                // Drive the final color to prevent changes over time
                var bgColorVariable = bgImageRef.Target.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                bgColorVariable.Value.Value = finalColor;
                var bgColorDriver = bgImageRef.Target.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                bgColorDriver.DriveTarget.Target = bgImageRef.Target.Tint;
                bgColorDriver.ValueSource.Target = bgColorVariable.Value;
                
                // Handle overview background if it exists
                var overviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg").GetValue(__instance);
                if (overviewBgField is FieldDrive<colorX> overviewBg && overviewBg.IsLinkValid)
                {
                    var overviewColorVariable = __instance.Slot.GetComponentOrAttach<DynamicValueVariable<colorX>>();
                    overviewColorVariable.Value.Value = finalColor;
                    var overviewColorDriver = __instance.Slot.GetComponentOrAttach<ValueDriver<colorX>>();
                    overviewColorDriver.DriveTarget.Target = overviewBg.Target;
                    overviewColorDriver.ValueSource.Target = overviewColorVariable.Value;
                }
                
                Logger.LogUI("Background Color Override", $"Applied custom UpdateNodeStatus with header color base: R:{finalColor.r:F2} G:{finalColor.g:F2} B:{finalColor.b:F2}");
                
                return false; // Skip the original method
            }
            catch (Exception e) {
                Logger.LogError("Error in UpdateNodeStatus patch", e, LogCategory.UI);
                return true; // Continue with original method on error
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
                        }
                    }
                    
                    // Toggle Overview visibility (same as overview mode)
                    overviewSlot.Slot.ActiveSelf = overviewModeEnabled;
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

 
} 
