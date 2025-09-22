using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Collections.Generic;
using Elements.Assets;
using System.Linq;
using FrooxEngine.UIX;
using System.Reflection;
using Renderite.Shared;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul;

public class ProtoFluxOverhaul : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.4.3";
	public override string Name => "ProtoFluxOverhaul";
	public override string Author => "Dexy, NepuShiro";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DexyThePuppy/ProtoFluxOverhaul";

	// Configuration
	public static ModConfiguration Config;
	public static readonly Dictionary<Slot, Panner2D> pannerCache = new Dictionary<Slot, Panner2D>();
	
	// ============ BASIC SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_BASIC = new("spacerBasic", "--- Basic Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ENABLED = new("Enabled", "Should ProtoFluxOverhaul be Enabled?", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DEBUG_LOGGING = new("debugLogging", "Enable Debug Logging", () => false);

	// ============ ANIMATION SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_ANIMATION = new("spacerAnimation", "--- Animation Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float2> SCROLL_SPEED = new("scrollSpeed", "Scroll Speed (X,Y)", () => new float2(-0.5f, 0f));
	
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float2> SCROLL_REPEAT = new("scrollRepeat", "Scroll Repeat Interval (X,Y)", () => new float2(1f, 1f));
	
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> PING_PONG = new("pingPong", "Ping Pong Animation", () => false);

	// ============ TEXTURE URLS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_TEXTURES = new("spacerTextures", "--- Texture URLs ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> FAR_TEXTURE = new("farTexture", "Far Texture URL", () => new Uri("resdb:///87782c86f11fa977dec6545006417b4b1e79cb4dfd6d9ed1ef169259512af9d7.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NEAR_TEXTURE = new("nearTexture", "Near Texture URL", () => new Uri("resdb:///87782c86f11fa977dec6545006417b4b1e79cb4dfd6d9ed1ef169259512af9d7.png"));	

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> ROUNDED_TEXTURE = new("roundedTexture", "Rounded Texture URL", () => new Uri("resdb:///3ee5c0335455c19970d877e2b80f7869539df43fccb8fc64b38e320fc44c154f.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECTOR_INPUT_TEXTURE = new("connectorInputTexture", "Connector Input Texture URL", () => new Uri("resdb:///baff0353323659064d2692e3609025d2348998798ee6031cb777fbd4a13f4360.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECTOR_OUTPUT_TEXTURE = new("connectorOutputTexture", "Connector Output Texture URL", () => new Uri("resdb:///baff0353323659064d2692e3609025d2348998798ee6031cb777fbd4a13f4360.png")); 

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_INPUT_TEXTURE = new("callConnectorInputTexture", "Call Connector Input Texture URL", () => new Uri("resdb:///59586a2c3a1f0bab46fd6b24103fba2f00f9dd98d438d98226b5b54859b30b30.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_OUTPUT_TEXTURE = new("callConnectorOutputTexture", "Call Connector Output Texture URL", () => new Uri("resdb:///6cd7ec2bad20b57ba68fda3bb961912f42b3816965a941e968d6d2b237ad7335.png"));


	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X1_CONNECTOR_TEXTURE = new("vectorX1ConnectorTexture", "Vector x1 Connector Texture URL", () => new Uri("https://github.com/DexyThePuppy/ProtoFluxOverhaul/blob/c67ceb3280c144a82ccb74861dc14f361d8c37d5/ProtoFluxOverhaul/Images/Connector_x1.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X2_CONNECTOR_TEXTURE = new("vectorX2ConnectorTexture", "Vector x2 Connector Texture URL", () => new Uri("https://github.com/DexyThePuppy/ProtoFluxOverhaul/blob/c67ceb3280c144a82ccb74861dc14f361d8c37d5/ProtoFluxOverhaul/Images/Connector_x2.png?"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X3_CONNECTOR_TEXTURE = new("vectorX3ConnectorTexture", "Vector x3 Connector Texture URL", () => new Uri("https://github.com/DexyThePuppy/ProtoFluxOverhaul/blob/c67ceb3280c144a82ccb74861dc14f361d8c37d5/ProtoFluxOverhaul/Images/Connector_x3.png?"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X4_CONNECTOR_TEXTURE = new("vectorX4ConnectorTexture", "Vector x4 Connector Texture URL", () => new Uri("https://github.com/DexyThePuppy/ProtoFluxOverhaul/blob/c67ceb3280c144a82ccb74861dc14f361d8c37d5/ProtoFluxOverhaul/Images/Connector_x4.png?"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_TEXTURE = new("nodeBackgroundTexture", "Node Background Texture URL", () => new Uri("resdb:///f020df95cf3b923094540d50796174b884cdd2061798032a48d5975d0570d0cd.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_HEADER_TEXTURE = new("nodeBackgroundHeaderTexture", "Node Background Header Texture URL", () => new Uri("resdb:///81ee369eaf5e92513de9a3a5bf4604d619434981ce4b4a091a7777df75bc9ec2.png"));

	// ============ AUDIO SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_AUDIO = new("spacerAudio", "--- Audio Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> WIRE_SOUNDS = new("WireSounds", "Should wire interaction sounds be enabled?", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> GRAB_SOUND = new("grabSound", "Grab Sound URL", () => new Uri("resdb:///391ce0c681b24b79a0240a1fa2e4a4c06492619897c0e6e045640e71a34b7ec7.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> DELETE_SOUND = new("deleteSound", "Delete Sound URL", () => new Uri("resdb:///b0c4195cce0990b27a3525623f46787d247c530387f8bc551e50bcf0584ab28b.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECT_SOUND = new("connectSound", "Connect Sound URL", () => new Uri("resdb:///8c63d74efcef070bf8fec2f9b1b20eecb15a499b17c64abaad225467d138d93b.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> AUDIO_VOLUME = new("audioVolume", "Audio Volume", () => 1f);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> MIN_DISTANCE = new("minDistance", "Audio Min Distance", () => 0.1f);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> MAX_DISTANCE = new("maxDistance", "Audio Max Distance", () => 25f);

	// ============ ADVANCED TEXTURE SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_ADVANCED = new("spacerAdvanced", "--- Advanced Texture Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<int> ANISOTROPIC_LEVEL = new("anisotropicLevel", "Anisotropic Level", () => 8);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> MIPMAPS = new("mipMaps", "Generate MipMaps", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> KEEP_ORIGINAL_MIPMAPS = new("keepOriginalMipMaps", "Keep Original MipMaps", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Filtering> MIPMAP_FILTER = new("mipMapFilter", "MipMap Filter", () => Filtering.Box);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> UNCOMPRESSED = new("uncompressed", "Uncompressed Texture", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DIRECT_LOAD = new("directLoad", "Direct Load", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> FORCE_EXACT_VARIANT = new("forceExactVariant", "Force Exact Variant", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> CRUNCH_COMPRESSED = new("crunchCompressed", "Use Crunch Compression", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureCompression> PREFERRED_FORMAT = new("preferredFormat", "Preferred Texture Format", () => TextureCompression.BC3_Crunched);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> READABLE = new("readable", "Readable Texture", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureFilterMode> FILTER_MODE = new("filterMode", "Texture Filter Mode", () => TextureFilterMode.Bilinear);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_U = new("wrapModeU", "Texture Wrap Mode U", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_V = new("wrapModeV", "Texture Wrap Mode V", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<ColorProfile> PREFERRED_PROFILE = new("preferredProfile", "Preferred Color Profile", () => ColorProfile.sRGB);


	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config.Save(true);

		Harmony harmony = new Harmony("com.Dexy.ProtoFluxOverhaul");
		harmony.PatchAll();
		
		// Always log startup regardless of debug settings
		ResoniteMod.Msg("[ProtoFluxOverhaul] Mod loaded successfully - Harmony patches applied");
		Logger.LogUI("Startup", "ProtoFluxOverhaul successfully loaded and patched");
		
		Config.OnThisConfigurationChanged += (k) => {
			if (k.Key != ENABLED) {
				Engine.Current.GlobalCoroutineManager.StartTask(async () => {
					await default(ToWorld);
					foreach (var kvp in pannerCache) {
						var panner = kvp.Value;
						if (panner == null) continue;

						panner.Speed = Config.GetValue(SCROLL_SPEED);
						panner.Repeat = Config.GetValue(SCROLL_REPEAT);
						panner.PingPong.Value = Config.GetValue(PING_PONG);

						// Get the FresnelMaterial
						var fresnelMaterial = kvp.Key.GetComponent<FresnelMaterial>();
						if (fresnelMaterial != null) {
							var farTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, Config.GetValue(FAR_TEXTURE));
							fresnelMaterial.FarTexture.Target = farTexture;
							
							var nearTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, Config.GetValue(NEAR_TEXTURE));
							fresnelMaterial.NearTexture.Target = nearTexture;
						}
					}
				});
			} 
		};

	}

	[HarmonyPatch(typeof(ProtoFluxWireManager), "OnChanges")]
	class ProtoFluxWireManager_OnChanges_Patch {
		public static void Postfix(ProtoFluxWireManager __instance, SyncRef<MeshRenderer> ____renderer, SyncRef<StripeWireMesh> ____wireMesh) {
			try {
				// Skip if mod is disabled or required components are missing
				if (!Config.GetValue(ENABLED) || 
					__instance == null || 
					!__instance.Enabled || 
					____renderer?.Target == null || 
					____wireMesh?.Target == null ||
					__instance.Slot == null) return;
				
				// === User Permission Check ===
				// Get the AllocUser for wire point
				__instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
				User wirePointAllocUser = __instance.World.GetUserByAllocationID(user);
				
				// Only process if wire belongs to local user
				if (wirePointAllocUser == null || position < wirePointAllocUser.AllocationIDStart) {
					__instance.ReferenceID.ExtractIDs(out ulong position1, out byte user1);
					User instanceAllocUser = __instance.World.GetUserByAllocationID(user1);
					
					if (instanceAllocUser == null || position1 < instanceAllocUser.AllocationIDStart || instanceAllocUser != __instance.LocalUser) return;
				}
				else if (wirePointAllocUser != __instance.LocalUser) return;
				
				// === Material Setup ===
				// Get or create the shared Fresnel Material
				var fresnelMaterial = GetOrCreateSharedMaterial(__instance.Slot);
				if (fresnelMaterial != null) {
					____renderer.Target.Material.Target = fresnelMaterial;
				}

				// === Animation Setup ===
				// Get or create Panner2D for scrolling effect
				if (!pannerCache.TryGetValue(fresnelMaterial.Slot, out var panner)) {
					panner = fresnelMaterial.Slot.GetComponentOrAttach<Panner2D>();
				
					// Configure panner with user settings
					panner.Speed = Config.GetValue(SCROLL_SPEED);
					panner.Repeat = Config.GetValue(SCROLL_REPEAT);
					panner.PingPong.Value = Config.GetValue(PING_PONG);
					
					pannerCache[fresnelMaterial.Slot] = panner;

					// Set the textures from config
					var farTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, Config.GetValue(FAR_TEXTURE));
					fresnelMaterial.FarTexture.Target = farTexture;
					
					var nearTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, Config.GetValue(NEAR_TEXTURE));
					fresnelMaterial.NearTexture.Target = nearTexture;
				}

				// === Texture Offset Setup ===
				// Setup texture offset drivers if they don't exist
				if (!fresnelMaterial.FarTextureOffset.IsLinked) {
					panner.Target = fresnelMaterial.FarTextureOffset;
				}

				if (!fresnelMaterial.NearTextureOffset.IsLinked) {
					ValueDriver<float2> newNearDrive = fresnelMaterial.Slot.GetComponentOrAttach<ValueDriver<float2>>();
					newNearDrive.DriveTarget.Target = fresnelMaterial.NearTextureOffset;
					newNearDrive.ValueSource.Target = panner.Target;
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in ProtoFluxOverhaul OnChanges patch", e, LogCategory.UI);
			}
		}
	}

	// Harmony patch for ProtoFluxWireManager's Setup method to handle wire configuration
	// This patch ensures proper wire orientation and appearance based on wire type
	[HarmonyPatch(typeof(ProtoFluxWireManager), "Setup")]
	class ProtoFluxWireManager_Setup_Patch {        

		public static void Postfix(ProtoFluxWireManager __instance, WireType type, SyncRef<StripeWireMesh> ____wireMesh, SyncRef<MeshRenderer> ____renderer) {
			try {
				// Skip if basic requirements aren't met
				if (!IsValidSetup(__instance, ____wireMesh)) return;

				// For wire direction/appearance, we don't need ownership permission
				// We only need to check if we're the one who can see/interact with the wire
				if (!__instance.Enabled || __instance.Slot == null) return;

				// Apply wire-type specific configuration with correct atlas offset
				ConfigureWireByType(__instance, ____wireMesh.Target, type);

				// Only apply material changes if we have permission
				if (PermissionHelper.HasPermission(__instance)) {
					// Get or create the shared Fresnel Material
					var fresnelMaterial = GetOrCreateSharedMaterial(__instance.Slot);
					if (fresnelMaterial != null && ____renderer?.Target != null) {
						____renderer.Target.Material.Target = fresnelMaterial;
					}
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in ProtoFluxWireManager Setup", e, LogCategory.Wire);
			}
		}

		// Validates that all required components are present and the mod is enabled
		private static bool IsValidSetup(ProtoFluxWireManager instance, SyncRef<StripeWireMesh> wireMesh) {
			return Config.GetValue(ENABLED) && 
				   instance != null && 
				   wireMesh?.Target != null;
		}

		// Configures wire mesh properties based on wire type and uses official atlas offset
		// This should run regardless of permissions since it affects visual appearance
		private static void ConfigureWireByType(ProtoFluxWireManager instance, StripeWireMesh wireMesh, WireType type) {
			// Get the official atlas offset from the proxy component that has the type information
			int atlasOffset = GetAtlasOffsetFromProxy(instance.Slot);
			
			switch(type) {
				case WireType.Input:
					ConfigureInputWire(wireMesh, atlasOffset);
					break;

				case WireType.Output:
					ConfigureOutputWire(wireMesh, atlasOffset);
					break;

				case WireType.Reference:
					ConfigureReferenceWire(wireMesh, atlasOffset);
					break;

				default:
					Logger.LogWire("Type", $"Unexpected wire type: {type}");
					return;
			}

		// Set custom UV scale for extended wire texture atlas
		// Impulse wires use x=1, all other wires use x=-1
		bool isImpulseWire = IsImpulseWire(instance.Slot);
		float uvX = isImpulseWire ? 1f : -1f;
		wireMesh.UVScale.Value = new float2(uvX, 0.17f);
		
		Logger.LogWire("UVScale", $"Wire UV scale set to ({uvX}, 0.17) - IsImpulse: {isImpulseWire}");
		
		// Lock UV scale to prevent changes
		var valueCopy = instance.Slot.GetComponentOrAttach<ValueCopy<float2>>();
		valueCopy.Source.Target = wireMesh.UVScale;
		valueCopy.Target.Target = wireMesh.UVScale;
		}
		
		// Checks if this wire is an Impulse/Flow wire
		private static bool IsImpulseWire(Slot wireSlot) {
			var impulseProxy = wireSlot.GetComponentInParents<ProtoFluxImpulseProxy>();
			var operationProxy = wireSlot.GetComponentInParents<ProtoFluxOperationProxy>();
			return impulseProxy != null || operationProxy != null;
		}
		
		// Gets the atlas offset from proxy components and applies custom offset to skip atlas index 1
		private static int GetAtlasOffsetFromProxy(Slot wireSlot) {
			// Check for Impulse/Flow wires first - use their official AtlasIndex (4) + custom mapping
			var impulseProxy = wireSlot.GetComponentInParents<ProtoFluxImpulseProxy>();
			if (impulseProxy != null) {
				int officialOffset = impulseProxy.AtlasIndex; // Official: 4 for Impulse
				return GetCustomAtlasOffset(officialOffset);
			}
			
			var operationProxy = wireSlot.GetComponentInParents<ProtoFluxOperationProxy>();
			if (operationProxy != null) {
				int officialOffset = operationProxy.AtlasIndex; // Official: 4 for Operation
				return GetCustomAtlasOffset(officialOffset);
			}
			
			// Look for the proxy component that contains the type information
			var inputProxy = wireSlot.GetComponentInParents<ProtoFluxInputProxy>();
			if (inputProxy != null) {
				// Check if this is a reference or other non-vector type
				if (IsReferenceOrNonVectorType(inputProxy.InputType.Value)) {
					return 0; // References and non-vector types use atlas 0
				}
				int officialOffset = inputProxy.AtlasIndex; // Official: 0, 1, 2, 3 for scalar, vec2, vec3, vec4
				return GetCustomAtlasOffset(officialOffset);
			}
			
			var outputProxy = wireSlot.GetComponentInParents<ProtoFluxOutputProxy>();
			if (outputProxy != null) {
				// Check if this is a reference or other non-vector type
				if (IsReferenceOrNonVectorType(outputProxy.OutputType.Value)) {
					return 0; // References and non-vector types use atlas 0
				}
				int officialOffset = outputProxy.AtlasIndex; // Official: 0, 1, 2, 3 for scalar, vec2, vec3, vec4
				return GetCustomAtlasOffset(officialOffset);
			}
			
			// Fallback to 0 (references, etc.) if no proxy found
			return 0;
		}
		
		
		// Checks if a type is a reference or other non-vector type that should use atlas 0
		private static bool IsReferenceOrNonVectorType(System.Type type) {
			if (type == null) return true;
			
			// References (Slot, User, Component types, etc.)
			if (typeof(IWorldElement).IsAssignableFrom(type)) return true;
			if (typeof(Component).IsAssignableFrom(type)) return true;
			if (type == typeof(Slot) || type == typeof(User)) return true;
			
			// Other non-primitive types (except vectors)
			if (!type.IsPrimitive && type != typeof(string) && !typeof(IVector).IsAssignableFrom(type)) {
				return true;
			}
			
			return false;
		}
		
		// Custom atlas offset mapping to skip index 1
		private static int GetCustomAtlasOffset(int officialOffset) {
			switch (officialOffset) {
				case 0: return 1; // scalar (float, int, etc.) → atlas 1 (was 0)
				case 1: return 2; // vector2 (float2, int2, etc.) → atlas 2 (skip original 1)
				case 2: return 3; // vector3 (float3, int3, etc.) → atlas 3
				case 3: return 4; // vector4 (float4, int4, etc.) → atlas 4
				case 4: return 5; // impulse (call, operation, etc.) → atlas 5 (bottom row)
				default: return officialOffset; // fallback
			}
		}
		
		// Gets precise UV Y offset for 6-row wire texture atlas (128x128 tiles)
		// Atlas height: 6 × 128 = 768 pixels
		// UV Y = (768 - 128 × (row + 1)) / 768
		private static float GetCustomUVOffset(int atlasRow) {
			switch (atlasRow) {
				case 0: return 0.8333333f; // (768 - 128×1) / 768 = 640/768
				case 1: return 0.6666667f; // (768 - 128×2) / 768 = 512/768
				case 2: return 0.5000000f; // (768 - 128×3) / 768 = 384/768
				case 3: return 0.3333333f; // (768 - 128×4) / 768 = 256/768
				case 4: return 0.1666667f; // (768 - 128×5) / 768 = 128/768
				case 5: return 0.0000000f; // (768 - 128×6) / 768 = 0/768
				default: return 0.0f; // fallback to bottom row
			}
		}

		// Configures an input wire with:
		// - Left-pointing tangent
		// - Input orientation for both ends
		// - Official atlas offset for vector dimension
		private static void ConfigureInputWire(StripeWireMesh wireMesh, int atlasOffset) {
			wireMesh.Tangent0.Value = float3.Left * ProtoFluxWireManager.TANGENT_MAGNITUDE;
			wireMesh.Orientation0.Value = ProtoFluxWireManager.WIRE_ORIENTATION_INPUT;
			wireMesh.Orientation1.Value = ProtoFluxWireManager.WIRE_ORIENTATION_INPUT;
			
			// Apply custom atlas offset calculation for 6-row texture (128x128 tiles)
			float uvY = GetCustomUVOffset(atlasOffset);
			wireMesh.UVOffset.Value = new float2(0f, uvY);
			Logger.LogWire("Atlas", $"Input wire using atlas offset {atlasOffset} -> UV Y offset {uvY}");
		}

		// Configures an output wire with:
		// - Right-pointing tangent
		// - Output orientation for both ends
		// - Official atlas offset for vector dimension
		private static void ConfigureOutputWire(StripeWireMesh wireMesh, int atlasOffset) {
			wireMesh.Tangent0.Value = float3.Right * ProtoFluxWireManager.TANGENT_MAGNITUDE;
			wireMesh.Orientation0.Value = ProtoFluxWireManager.WIRE_ORIENTATION_OUTPUT;
			wireMesh.Orientation1.Value = ProtoFluxWireManager.WIRE_ORIENTATION_OUTPUT;
			
			// Apply custom atlas offset calculation for 6-row texture (128x128 tiles)
			float uvY = GetCustomUVOffset(atlasOffset);
			wireMesh.UVOffset.Value = new float2(0f, uvY);
			Logger.LogWire("Atlas", $"Output wire using atlas offset {atlasOffset} -> UV Y offset {uvY}");
		}

		// Configures a reference wire with:
		// - Downward-pointing tangent
		// - Reference orientation for both ends
		// - Official atlas offset for vector dimension
		private static void ConfigureReferenceWire(StripeWireMesh wireMesh, int atlasOffset) {
			wireMesh.Tangent0.Value = float3.Down * ProtoFluxWireManager.TANGENT_MAGNITUDE;
			wireMesh.Orientation0.Value = ProtoFluxWireManager.WIRE_ORIENTATION_REFERENCE;
			wireMesh.Orientation1.Value = ProtoFluxWireManager.WIRE_ORIENTATION_REFERENCE;
			
			// Apply custom atlas offset calculation for 6-row texture (128x128 tiles)
			float uvY = GetCustomUVOffset(atlasOffset);
			wireMesh.UVOffset.Value = new float2(0f, uvY);
			Logger.LogWire("Atlas", $"Reference wire using atlas offset {atlasOffset} -> UV Y offset {uvY}");
		}
	}


	/// Creates or retrieves a shared FresnelMaterial for wire rendering
	private static FresnelMaterial GetOrCreateSharedMaterial(Slot slot) {
		// Create organized hierarchy under __TEMP
		var tempSlot = slot.World.RootSlot.FindChild("__TEMP") ?? slot.World.RootSlot.AddSlot("__TEMP", false);
		var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
		var userSlot = modSlot.FindChild(slot.LocalUser.UserName) ?? modSlot.AddSlot(slot.LocalUser.UserName, false);
		var materialsSlot = userSlot.FindChild("Materials") ?? userSlot.AddSlot("Materials", false);
		var materialSlot = materialsSlot.FindChild("Wire") ?? materialsSlot.AddSlot("Wire", false);

		// Create material
		FresnelMaterial fresnelMaterial = materialSlot.GetComponentOrAttach<FresnelMaterial>();

		// Ensure cleanup when user leaves
		userSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;
		
		// Configure material properties
		fresnelMaterial.NearColor.Value = new colorX(0.8f);
		fresnelMaterial.FarColor.Value = new colorX(1.4f);
		fresnelMaterial.Sidedness.Value = Sidedness.Double;
		fresnelMaterial.UseVertexColors.Value = true;
		fresnelMaterial.BlendMode.Value = BlendMode.Alpha;
		fresnelMaterial.ZWrite.Value = ZWrite.On;
		
		// Setup textures from config
		var farTexture = GetOrCreateSharedTexture(materialSlot, Config.GetValue(FAR_TEXTURE));
		fresnelMaterial.FarTexture.Target = farTexture;
		
		var nearTexture = GetOrCreateSharedTexture(materialSlot, Config.GetValue(NEAR_TEXTURE));
		fresnelMaterial.NearTexture.Target = nearTexture;
		
		return fresnelMaterial;
	}

	/// Creates or retrieves a shared texture with specified settings
	private static StaticTexture2D GetOrCreateSharedTexture(Slot slot, Uri uri) {
		// Create organized hierarchy under __TEMP
		var tempSlot = slot.World.RootSlot.FindChild("__TEMP") ?? slot.World.RootSlot.AddSlot("__TEMP", false);
		var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
		var userSlot = modSlot.FindChild(slot.LocalUser.UserName) ?? modSlot.AddSlot(slot.LocalUser.UserName, false);
		var texturesSlot = userSlot.FindChild("Textures") ?? userSlot.AddSlot("Textures", false);
		var textureSlot = texturesSlot.FindChild("Wire") ?? texturesSlot.AddSlot("Wire", false);

		// Get or create the texture
		StaticTexture2D texture = textureSlot.GetComponentOrAttach<StaticTexture2D>();
		texture.URL.Value = uri;

		// Configure texture properties from user settings
		texture.FilterMode.Value = Config.GetValue(FILTER_MODE);
		texture.MipMaps.Value = Config.GetValue(MIPMAPS);
		texture.Uncompressed.Value = Config.GetValue(UNCOMPRESSED);
		texture.CrunchCompressed.Value = Config.GetValue(CRUNCH_COMPRESSED);
		texture.DirectLoad.Value = Config.GetValue(DIRECT_LOAD);
		texture.ForceExactVariant.Value = Config.GetValue(FORCE_EXACT_VARIANT);
		texture.AnisotropicLevel.Value = Config.GetValue(ANISOTROPIC_LEVEL);
		texture.WrapModeU.Value = Config.GetValue(WRAP_MODE_U);
		texture.WrapModeV.Value = Config.GetValue(WRAP_MODE_V);
		texture.KeepOriginalMipMaps.Value = Config.GetValue(KEEP_ORIGINAL_MIPMAPS);
		texture.MipMapFilter.Value = Config.GetValue(MIPMAP_FILTER);
		texture.Readable.Value = Config.GetValue(READABLE);
		texture.PowerOfTwoAlignThreshold.Value = 0.05f;  // For proper texture alignment
		
		return texture;
	}


	// Audio initialization is now handled on-demand by ProtoFluxSounds using engine's optimized patterns

	// Patches for wire-related events
	[HarmonyPatch(typeof(ProtoFluxWireManager))]
	public class ProtoFluxWireManager_Patches {
		// Helper method for permission checks
		private static bool HasPermission(ProtoFluxWireManager instance) {
			try {
				if (instance == null || instance.Slot == null) {
					Logger.LogPermission("Check", false, "Permission check failed: instance or slot is null");
					return false;
				}

				// Get the wire's owner
				instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
				User wirePointAllocUser = instance.World.GetUserByAllocationID(user);
				Logger.LogPermission("Wire Point", true, $"Wire point allocation: Position={position}, UserID={user}, User={wirePointAllocUser?.UserName}");
				
				if (wirePointAllocUser == null || position < wirePointAllocUser.AllocationIDStart) {
					instance.ReferenceID.ExtractIDs(out ulong position1, out byte user1);
					User instanceAllocUser = instance.World.GetUserByAllocationID(user1);
					Logger.LogPermission("Instance", true, $"Instance allocation: Position={position1}, UserID={user1}, User={instanceAllocUser?.UserName}");
					
					// Allow the wire owner to modify
					bool hasPermission = (instanceAllocUser != null && 
						   position1 >= instanceAllocUser.AllocationIDStart &&
						   (instanceAllocUser == instance.LocalUser || instance.LocalUser.IsHost));
					
					Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={instanceAllocUser?.UserName}, IsLocalUser={instanceAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={hasPermission}");
					return hasPermission;
				}
				
				// Allow the wire owner to modify
				bool result = wirePointAllocUser == instance.LocalUser || instance.LocalUser.IsHost;
				Logger.LogPermission("Wire Check", result, $"Permission check (wire): Owner={wirePointAllocUser?.UserName}, IsLocalUser={wirePointAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={result}");
				return result;
			}
			catch (Exception e) {
				// If anything goes wrong, deny permission to be safe
				Logger.LogError("Permission check error", e, LogCategory.Permission);
				return false;
			}
		}

		[HarmonyPatch("OnDestroy")]
		[HarmonyPrefix]
		public static void OnDestroy_Prefix(ProtoFluxWireManager __instance)
		{
			try
			{
				// Skip if disabled
				if (!Config.GetValue(ENABLED)) {
					Logger.LogWire("Delete", "Wire delete sound skipped: Mod disabled");
					return;
				}

				// Skip if required components are missing
				if (__instance == null || !__instance.Enabled || __instance.Slot == null) {
					Logger.LogWire("Delete", "Wire delete sound skipped: Missing components");
					return;
				}

				// Play delete sound if wire is being deleted and we have permission
				if (__instance.DeleteHighlight.Value && HasPermission(__instance))
				{
					Logger.LogWire("Delete", $"Playing wire delete sound at position {__instance.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireDeleted(__instance.World, __instance.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Delete", $"Wire delete sound skipped: DeleteHighlight={__instance.DeleteHighlight.Value}, HasPermission={__instance != null && HasPermission(__instance)}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error handling wire cleanup", e, LogCategory.Wire);
			}
		}
	}

	[HarmonyPatch(typeof(ProtoFluxTool))]
	public class ProtoFluxTool_Patches {
		// Helper method for permission checks
		private static bool HasPermission(Component component) {
			try {
				if (component == null || component.Slot == null) {
					Logger.LogPermission("Check", false, "Permission check failed: instance or slot is null");
					return false;
				}

				// Get the wire's owner
				component.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
				User wirePointAllocUser = component.World.GetUserByAllocationID(user);
				Logger.LogPermission("Wire Point", true, $"Wire point allocation: Position={position}, UserID={user}, User={wirePointAllocUser?.UserName}");
				
				if (wirePointAllocUser == null || position < wirePointAllocUser.AllocationIDStart) {
					component.ReferenceID.ExtractIDs(out ulong position1, out byte user1);
					User instanceAllocUser = component.World.GetUserByAllocationID(user1);
					Logger.LogPermission("Instance", true, $"Instance allocation: Position={position1}, UserID={user1}, User={instanceAllocUser?.UserName}");
					
					// Allow the wire owner to modify
					bool hasPermission = (instanceAllocUser != null && 
						   position1 >= instanceAllocUser.AllocationIDStart &&
						   (instanceAllocUser == component.LocalUser || component.LocalUser.IsHost));
					
					Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={instanceAllocUser?.UserName}, IsLocalUser={instanceAllocUser == component.LocalUser}, IsHost={component.LocalUser.IsHost}, Result={hasPermission}");
					return hasPermission;
				}
				
				// Allow the wire owner to modify
				bool result = wirePointAllocUser == component.LocalUser || component.LocalUser.IsHost;
				Logger.LogPermission("Wire Check", result, $"Permission check (wire): Owner={wirePointAllocUser?.UserName}, IsLocalUser={wirePointAllocUser == component.LocalUser}, IsHost={component.LocalUser.IsHost}, Result={result}");
				return result;
			}
			catch (Exception e) {
				// If anything goes wrong, deny permission to be safe
				Logger.LogError("Permission check error", e, LogCategory.Permission);
				return false;
			}
		}

		// Patch for wire grab start
		[HarmonyPatch("StartDraggingWire")]
		[HarmonyPrefix]
		public static void StartDraggingWire_Prefix(ProtoFluxTool __instance, ProtoFluxElementProxy proxy) {
			try {
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS)) {
					Logger.LogWire("Grab", "Wire grab sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				if (proxy != null && HasPermission(proxy)) {
					Logger.LogWire("Grab", $"Playing wire grab sound at position {proxy.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireGrabbed(__instance.World, proxy.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Grab", $"Wire grab sound skipped: Proxy={proxy != null}, HasPermission={proxy != null && HasPermission(proxy)}");
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in wire grab sound", e, LogCategory.Wire);
			}
		}

		// Patch for wire connection (Input-Output)
		[HarmonyPatch(typeof(ProtoFluxTool), "TryConnect", new Type[] { typeof(ProtoFluxInputProxy), typeof(ProtoFluxOutputProxy) })]
		[HarmonyPostfix]
		public static void TryConnect_InputOutput_Postfix(ProtoFluxTool __instance, ProtoFluxInputProxy input, ProtoFluxOutputProxy output) {
			try {
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS)) {
					Logger.LogWire("Connect", "Wire connect sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				if (input != null && output != null && HasPermission(input)) {
						Logger.LogWire("Connect", $"Playing wire connect sound (Input-Output) at position {input.Slot.GlobalPosition}");
						ProtoFluxSounds.OnWireConnected(__instance.World, input.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Connect", $"Wire connect sound skipped: Input={input != null}, Output={output != null}, HasPermission={input != null && HasPermission(input)}");
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in wire connect sound (Input-Output)", e, LogCategory.Wire);
			}
		}

		// Patch for wire connection (Impulse-Operation)
		[HarmonyPatch(typeof(ProtoFluxTool), "TryConnect", new Type[] { typeof(ProtoFluxImpulseProxy), typeof(ProtoFluxOperationProxy) })]
		[HarmonyPostfix]
		public static void TryConnect_ImpulseOperation_Postfix(ProtoFluxTool __instance, ProtoFluxImpulseProxy impulse, ProtoFluxOperationProxy operation) {
			try {
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS)) {
					Logger.LogWire("Connect", "Wire connect sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				if (impulse != null && operation != null && HasPermission(impulse)) {
					Logger.LogWire("Connect", $"Playing wire connect sound (Impulse-Operation) at position {impulse.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, impulse.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Connect", $"Wire connect sound skipped: Impulse={impulse != null}, Operation={operation != null}, HasPermission={impulse != null && HasPermission(impulse)}");
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in wire connect sound (Impulse-Operation)", e, LogCategory.Wire);
			}
		}

		// Patch for wire connection (Node-Input-Output)
		[HarmonyPatch(typeof(ProtoFluxTool), "TryConnect", new Type[] { typeof(ProtoFluxNode), typeof(ISyncRef), typeof(INodeOutput) })]
		[HarmonyPostfix]
		public static void TryConnect_NodeInputOutput_Postfix(ProtoFluxTool __instance, ProtoFluxNode node, ISyncRef input, INodeOutput output) {
			try {
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS)) {
					Logger.LogWire("Connect", "Wire connect sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				if (node != null && input != null && output != null && HasPermission(node)) {
					Logger.LogWire("Connect", $"Playing wire connect sound (Node-Input-Output) at position {node.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, node.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Connect", $"Wire connect sound skipped: Node={node != null}, Input={input != null}, Output={output != null}, HasPermission={node != null && HasPermission(node)}");
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in wire connect sound (Node-Input-Output)", e, LogCategory.Wire);
			}
		}

		// Patch for wire deletion
		[HarmonyPatch("OnPrimaryRelease")]
		[HarmonyPostfix]
		public static void OnPrimaryRelease_Postfix(ProtoFluxTool __instance) {
			try {
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS)) {
					Logger.LogWire("Delete", "Wire delete sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Check if we're deleting wires (using the cut line)
				if (__instance.GetType().GetField("_cutWires", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) is HashSet<ProtoFluxWireManager> cutWires && 
					cutWires.Count > 0) {
					// Play delete sound for cut wires
					foreach (var wire in cutWires) {
						if (wire != null && !wire.IsRemoved && HasPermission(wire)) {
							Logger.LogWire("Delete", $"Playing wire delete sound (cut) at position {wire.Slot.GlobalPosition}");
							ProtoFluxSounds.OnWireDeleted(__instance.World, wire.Slot.GlobalPosition);
						} else {
							Logger.LogWire("Delete", $"Wire delete sound skipped (cut): Wire={wire != null}, IsRemoved={wire?.IsRemoved}, HasPermission={wire != null && !wire.IsRemoved && HasPermission(wire)}");
						}
					}
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in wire delete sound", e, LogCategory.Wire);
			}
		}
	}
}
