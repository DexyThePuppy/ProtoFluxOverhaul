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
    // NOTE: PermissionHelper / OverviewModeHelper moved to separate files in Code/Nodes for modularity.
    // NOTE: RoundedCornersHelper has been split into multiple partial files:
    // - RoundedCornersHelper.TagsAndRelay.cs
    // - RoundedCornersHelper.ButtonsAndPlatformPalette.cs
    // - RoundedCornersHelper.ConnectorsAndPaletteMatching.cs
    // - RoundedCornersHelper.ValueLinking.cs
    // - RoundedCornersHelper.SpritesAndShading.cs
    //
    // This file now focuses on the BuildUI patch only.

    // Patch to add rounded corners to ProtoFlux node visuals
    [HarmonyPatch(typeof(ProtoFluxNodeVisual), "BuildUI")]
    public partial class ProtoFluxNodeVisual_BuildUI_Patch {
        // ColorMyProtoFlux color settings
        private static readonly colorX NODE_CATEGORY_TEXT_LIGHT_COLOR = new colorX(0.75f);
        private static readonly colorX NODE_CATEGORY_TEXT_DARK_COLOR = new colorX(0.25f);
        
        // NOTE: Connector sprite caches + sprite-provider helpers were moved into:
        // `ProtoFluxNodeVisual.BuildUI.ConnectorSprites.cs`

        public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ProtoFluxNode node) {
            try {
                // Skip if disabled
                if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

                // Skip if instance or slot is destroyed/removed
                if (__instance == null || __instance.IsRemoved || 
                    __instance.Slot == null || __instance.Slot.IsRemoved ||
                    node == null || node.IsRemoved) return;

                // Log entry for debugging regeneration issues
                var slotTag = __instance.Slot.Tag;
                var slotRefId = __instance.Slot.ReferenceID.ToString();
                Logger.LogUI("BuildUI Entry", $"Processing node '{node.GetType().Name}', Slot={__instance.Slot.Name}, RefID={slotRefId}, Tag='{slotTag ?? "(null)"}'");

                // Skip if already styled by ProtoFluxOverhaul (prevents duplicate processing)
                if (RoundedCornersHelper.HasPFOTag(__instance.Slot)) {
                    Logger.LogUI("BuildUI", $"Skipping already-styled node '{node.GetType().Name}' (Tag contains ProtoFluxOverhaul)");
                    return;
                }

                // Audio is now handled on-demand by ProtoFluxSounds

                // === User Permission Check ===
                if (!PermissionHelper.HasPermission(__instance)) return;

                // === Mark node as styled EARLY to prevent reprocessing if later code fails ===
                // This must happen before any code that could throw an exception
                RoundedCornersHelper.AddPFOTag(__instance.Slot);
                Logger.LogUI("Tag", $"Added ProtoFluxOverhaul tag to node '{node.GetType().Name}' (early)");

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

                // Find all connector images in the hierarchy (skip removed/destroyed)
                var connectorSlots = ui.Root.GetComponentsInChildren<Image>()
                    .Where(img => img != null && !img.IsRemoved && 
                                  img.Slot != null && !img.Slot.IsRemoved &&
                                  img.Slot.Name == "Connector")
                    .ToList();

                foreach (var connectorImage in connectorSlots) {
                    // Skip if connector was destroyed during processing
                    if (connectorImage == null || connectorImage.IsRemoved || 
                        connectorImage.Slot == null || connectorImage.Slot.IsRemoved) continue;

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

                    // Mark this connector as styled by ProtoFluxOverhaul
                    RoundedCornersHelper.AddPFOTag(connectorImage.Slot);
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
							// Only install if not already driven (prevents LinkBase warnings on rebuild/unpack).
							if (!newHeaderSlot.ActiveSelf_Field.IsDriven) {
								var headerActiveDriver = newHeaderSlot.GetComponentOrAttach<BooleanValueDriver<bool>>();
								headerActiveDriver.TargetField.Target = newHeaderSlot.ActiveSelf_Field;
								headerActiveDriver.TrueValue.Value = true;
								headerActiveDriver.FalseValue.Value = baseHeaderVisible;

								var headerHoverCopy = newHeaderSlot.GetComponentOrAttach<ValueCopy<bool>>();
								headerHoverCopy.Source.Target = overviewHoverArea.IsHovering;
								headerHoverCopy.Target.Target = headerActiveDriver.State;
								headerHoverCopy.WriteBack.Value = false;
							} else {
								newHeaderSlot.ActiveSelf = baseHeaderVisible;
								Logger.LogUI("Hover Overview Override", "Skipped header hover override: Header ActiveSelf is already driven");
							}

							// Overview ActiveSelf driver: hovering => false, not-hovering => baseOverviewVisible
							// NOTE: ProtoFluxNodeVisual internally drives Overview visibility (_overviewVisual FieldDrive<bool>).
							// Attempting to link another driver to ActiveSelf triggers "already linked... Use ForceLink()" warnings.
							if (!overviewSlot.Slot.ActiveSelf_Field.IsDriven) {
								var overviewActiveDriver = overviewSlot.Slot.GetComponentOrAttach<BooleanValueDriver<bool>>();
								overviewActiveDriver.TargetField.Target = overviewSlot.Slot.ActiveSelf_Field;
								overviewActiveDriver.TrueValue.Value = false;
								overviewActiveDriver.FalseValue.Value = baseOverviewVisible;

								var overviewHoverCopy = overviewSlot.Slot.GetComponentOrAttach<ValueCopy<bool>>();
								overviewHoverCopy.Source.Target = overviewHoverArea.IsHovering;
								overviewHoverCopy.Target.Target = overviewActiveDriver.State;
								overviewHoverCopy.WriteBack.Value = false;
							} else {
								// Keep base behavior; hover override is skipped to avoid fighting the engine drive.
								overviewSlot.Slot.ActiveSelf = baseOverviewVisible;
								Logger.LogUI("Hover Overview Override", "Skipped overview hover override: Overview ActiveSelf is already driven by engine");
							}

							Logger.LogUI("Hover Overview Override", $"Hover override install attempted (base: header={(baseHeaderVisible ? "VISIBLE" : "HIDDEN")}, overview={(baseOverviewVisible ? "VISIBLE" : "HIDDEN")})");
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
                    .Where(img => img != null && !img.IsRemoved && 
                                  img.Slot != null && !img.Slot.IsRemoved &&
                                  img.Slot.Name == "Connector")
                    .ToList();

                foreach (var connectorImage in connectorImages) {
                    // Find the label background image (sibling Image component that's not the Connector)
                    var parentSlot = connectorImage.Slot.Parent;
                    var labelBackgroundImage = parentSlot?.GetComponentsInChildren<Image>()
                        .FirstOrDefault(img => img.Slot.Name != "Connector" && img.Slot != connectorImage.Slot);
                    
                    if (labelBackgroundImage != null) {
                        // Apply the header sprite provider for connector labels.
                        // If palette mode is enabled, do NOT preserve original color (it would create a ValueDriver and block palette tint).
                        float labelScale = RoundedCornersHelper.CONNECTOR_LABEL_SPRITE_SCALE;
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

            // === Cleanup relay node visuals ===
            // Relay nodes (ValueRelay, ObjectRelay, CallRelay, etc.) don't need the background sprite
            // and shading since the node visual patch adds those. Remove duplicates.
            RoundedCornersHelper.CleanupRelayNodeVisuals(__instance.Slot, node);

            Logger.LogUI("BuildUI", $"Successfully processed node '{node.GetType().Name}'");
            }
            catch (Exception e) {
                Logger.LogError("Failed to process node visual", e, LogCategory.UI);
            }
        }
    }



} 
