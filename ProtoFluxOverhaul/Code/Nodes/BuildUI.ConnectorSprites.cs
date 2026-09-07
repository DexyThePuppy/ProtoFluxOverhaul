using FrooxEngine;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Linq;
using ProtoFlux.Core;

namespace ProtoFluxOverhaul {
    public partial class ProtoFluxNodeVisual_BuildUI_Patch {
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

                string genericName = genericTypeDef.Name;
                if (genericName.StartsWith("SyncRef") || genericName.StartsWith("RelayRef") || genericName.StartsWith("RefProxy")) {
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
            Logger.LogUI("Proxy Debug", $"Found proxy components in hierarchy for slot {connectorSlot.Name}: {string.Join(", ", connectorSlot.GetComponentsInParents<Component>().Where(c => c.GetType().Name.Contains("Proxy")).Select(c => c.GetType().Name))}");
            
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
            if (impulseType.HasValue || isOperation) {
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
            
            var kind = isOutput ? "Output" : "Input";
            var uri = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE);
            return SharedAssets.GetSharedSprite(slot.World, $"PFO_Sprite_Connector_{kind}", uri, flipHorizontal: isOutput, textureRole: "Connector");
        }

        /// <summary>
        /// Creates or retrieves a shared sprite provider for the Call connector image
        /// </summary>
        public static SpriteProvider GetOrCreateSharedCallConnectorSprite(Slot slot, bool isOutput) {
            var kind = isOutput ? "Output" : "Input";
            var uri = isOutput
                ? ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CALL_CONNECTOR_OUTPUT_TEXTURE)
                : ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CALL_CONNECTOR_INPUT_TEXTURE);
            return SharedAssets.GetSharedSprite(slot.World, $"PFO_Sprite_Call_{kind}", uri, flipHorizontal: isOutput, textureRole: isOutput ? "CallOutput" : "CallInput");
        }

        /// <summary>
        /// Creates or retrieves a shared sprite provider for vector connector images
        /// </summary>
        public static SpriteProvider GetOrCreateSharedVectorConnectorSprite(Slot slot, bool isOutput, int vectorSize) {
            Uri textureUrl;
            switch (vectorSize) {
                case 1:
                    textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE);
                    break;
                case 2:
                    textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.VECTOR_X1_CONNECTOR_TEXTURE);
                    break;
                case 3:
                    textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.VECTOR_X2_CONNECTOR_TEXTURE);
                    break;
                case 4:
                    textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.VECTOR_X3_CONNECTOR_TEXTURE);
                    break;
                default:
                    textureUrl = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE);
                    break;
            }

            Logger.LogUI("Texture Selection", $"Vector size {vectorSize} -> {textureUrl}");
            var kind = isOutput ? "Output" : "Input";
            var textureRole = vectorSize >= 2 && vectorSize <= 4 ? $"Vector{vectorSize - 1}" : "Connector";
            return SharedAssets.GetSharedSprite(slot.World, $"PFO_Sprite_Vector{vectorSize}_{kind}", textureUrl, flipHorizontal: isOutput, textureRole: textureRole);
        }
    }
}

