using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Collections.Generic;
using Elements.Assets;
using System.Linq;
using ResoniteHotReloadLib;
using FrooxEngine.UIX;
using System.Reflection;
using static ProtoFluxVisualsOverhaul.Logger;

namespace ProtoFluxVisualsOverhaul;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class ProtoFluxVisualsOverhaul : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.1.0";
	public override string Name => "ProtoFluxVisualsOverhaul";
	public override string Author => "Dexy, NepuShiro";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DexyThePuppy/ProtoFluxVisualsOverhaul";

	// Configuration
	public static ModConfiguration Config;
	public static readonly Dictionary<Slot, Panner2D> pannerCache = new Dictionary<Slot, Panner2D>();
	
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ENABLED = new("Enabled", "Should ProtoFluxVisualsOverhaul be Enabled?", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> WIRE_SOUNDS = new("WireSounds", "Should wire interaction sounds be enabled?", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float2> SCROLL_SPEED = new("scrollSpeed", "Scroll Speed (X,Y)", () => new float2(-0.5f, 0f));
	
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float2> SCROLL_REPEAT = new("scrollRepeat", "Scroll Repeat Interval (X,Y)", () => new float2(1f, 1f));
	
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> PING_PONG = new("pingPong", "Ping Pong Animation", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> FAR_TEXTURE = new("farTexture", "Far Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Texture.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NEAR_TEXTURE = new("nearTexture", "Near Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Texture.png"));	

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> ROUNDED_TEXTURE = new("roundedTexture", "Rounded Texture URL", () => new Uri("resdb:///3ee5c0335455c19970d877e2b80f7869539df43fccb8fc64b38e320fc44c154f.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECTOR_INPUT_TEXTURE = new("connectorInputTexture", "Connector Input Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Connector.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECTOR_OUTPUT_TEXTURE = new("connectorOutputTexture", "Connector Output Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Connector.png")); 

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_OUTPUT_TEXTURE = new("callConnectorOutputTexture", "Call Connector Output Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Connector_Output.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_INPUT_TEXTURE = new("callConnectorInputTexture", "Call Connector Input Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Connector_Input.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_TEXTURE = new("nodeBackgroundTexture", "Node Background Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Node_Background.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_HEADER_TEXTURE = new("nodeBackgroundHeaderTexture", "Node Background Header Texture URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/refs/heads/main/ProtoFluxVisualsOverhaul/Images/Node_Header_Background.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureFilterMode> FILTER_MODE = new("filterMode", "Texture Filter Mode", () => TextureFilterMode.Anisotropic);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> MIPMAPS = new("mipMaps", "Generate MipMaps", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> UNCOMPRESSED = new("uncompressed", "Uncompressed Texture", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DIRECT_LOAD = new("directLoad", "Direct Load", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> FORCE_EXACT_VARIANT = new("forceExactVariant", "Force Exact Variant", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> CRUNCH_COMPRESSED = new("crunchCompressed", "Use Crunch Compression", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_U = new("wrapModeU", "Texture Wrap Mode U", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_V = new("wrapModeV", "Texture Wrap Mode V", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> KEEP_ORIGINAL_MIPMAPS = new("keepOriginalMipMaps", "Keep Original MipMaps", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Filtering> MIPMAP_FILTER = new("mipMapFilter", "MipMap Filter", () => Filtering.Box);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> READABLE = new("readable", "Readable Texture", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<int> ANISOTROPIC_LEVEL = new("anisotropicLevel", "Anisotropic Level", () => 8);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureCompression> PREFERRED_FORMAT = new("preferredFormat", "Preferred Texture Format", () => TextureCompression.BC3_Crunched);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<ColorProfile> PREFERRED_PROFILE = new("preferredProfile", "Preferred Color Profile", () => ColorProfile.sRGB);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DEBUG_LOGGING = new("debugLogging", "Enable Debug Logging", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> GRAB_SOUND = new("grabSound", "Grab Sound URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/main/ProtoFluxVisualsOverhaul/sounds/FluxWireGrab.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> DELETE_SOUND = new("deleteSound", "Delete Sound URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/main/ProtoFluxVisualsOverhaul/sounds/FluxWireDelete.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECT_SOUND = new("connectSound", "Connect Sound URL", () => new Uri("https://raw.githubusercontent.com/DexyThePuppy/ProtoFluxVisualsOverhaul/main/ProtoFluxVisualsOverhaul/sounds/FluxWireConnect.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> AUDIO_VOLUME = new("audioVolume", "Audio Volume", () => 1f);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> MIN_DISTANCE = new("minDistance", "Audio Min Distance", () => 0.1f);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> MAX_DISTANCE = new("maxDistance", "Audio Max Distance", () => 25f);


	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config.Save(true);

		Harmony harmony = new Harmony("com.Dexy.ProtoFluxVisualsOverhaul");
		harmony.PatchAll();
		Logger.LogUI("Startup", "ProtoFluxVisualsOverhaul successfully loaded and patched");
		
		// Register for hot reload
		HotReloader.RegisterForHotReload(this);
		
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
				Logger.LogError("Error in ProtoFluxVisualsOverhaul OnChanges patch", e, LogCategory.UI);
			}
		}
	}

	// Harmony patch for ProtoFluxWireManager's Setup method to handle wire configuration
	// This patch ensures proper wire orientation and appearance based on wire type
	[HarmonyPatch(typeof(ProtoFluxWireManager), "Setup")]
	class ProtoFluxWireManager_Setup_Patch {        
		// UV scale constants for wire texture direction
		private const float DEFAULT_UV_SCALE = 1f;
		private const float INVERTED_UV_SCALE = -1f;

		public static void Postfix(ProtoFluxWireManager __instance, WireType type, SyncRef<StripeWireMesh> ____wireMesh, SyncRef<MeshRenderer> ____renderer) {
			try {
				// Skip if basic requirements aren't met
				if (!IsValidSetup(__instance, ____wireMesh)) return;

				// For wire direction/appearance, we don't need ownership permission
				// We only need to check if we're the one who can see/interact with the wire
				if (!__instance.Enabled || __instance.Slot == null) return;

				// Apply wire-type specific configuration
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

		// Configures wire mesh properties based on wire type
		// This should run regardless of permissions since it affects visual appearance
		private static void ConfigureWireByType(ProtoFluxWireManager instance, StripeWireMesh wireMesh, WireType type) {
			switch(type) {
				case WireType.Input:
					ConfigureInputWire(wireMesh);
					break;

				case WireType.Output:
					ConfigureOutputWire(wireMesh);
					break;

				case WireType.Reference:
					ConfigureReferenceWire(wireMesh);
					break;

				default:
					Logger.LogWire("Type", $"Unexpected wire type: {type}");
					return;
			}

			// Lock UV scale to prevent changes
			var valueCopy = instance.Slot.GetComponentOrAttach<ValueCopy<float2>>();
			valueCopy.Source.Target = wireMesh.UVScale;
			valueCopy.Target.Target = wireMesh.UVScale;
		}

		// Configures an input wire with:
		// - Inverted UV scale for correct texture direction
		// - Left-pointing tangent
		// - Input orientation for both ends
		private static void ConfigureInputWire(StripeWireMesh wireMesh) {
			wireMesh.UVScale.Value = new float2(INVERTED_UV_SCALE, ProtoFluxWireManager.WIRE_ATLAS_RATIO);
			wireMesh.Tangent0.Value = float3.Left * ProtoFluxWireManager.TANGENT_MAGNITUDE;
			wireMesh.Orientation0.Value = ProtoFluxWireManager.WIRE_ORIENTATION_INPUT;
			wireMesh.Orientation1.Value = ProtoFluxWireManager.WIRE_ORIENTATION_INPUT;
		}

		// Configures an output wire with:
		// - Default UV scale for normal texture direction
		// - Right-pointing tangent
		// - Output orientation for both ends
		private static void ConfigureOutputWire(StripeWireMesh wireMesh) {
			wireMesh.UVScale.Value = new float2(DEFAULT_UV_SCALE, ProtoFluxWireManager.WIRE_ATLAS_RATIO);
			wireMesh.Tangent0.Value = float3.Right * ProtoFluxWireManager.TANGENT_MAGNITUDE;
			wireMesh.Orientation0.Value = ProtoFluxWireManager.WIRE_ORIENTATION_OUTPUT;
			wireMesh.Orientation1.Value = ProtoFluxWireManager.WIRE_ORIENTATION_OUTPUT;
		}

		// Configures a reference wire with:
		// - Default UV scale for normal texture direction
		// - Downward-pointing tangent
		// - Reference orientation for both ends
		private static void ConfigureReferenceWire(StripeWireMesh wireMesh) {
			wireMesh.UVScale.Value = new float2(DEFAULT_UV_SCALE, ProtoFluxWireManager.WIRE_ATLAS_RATIO);
			wireMesh.Tangent0.Value = float3.Down * ProtoFluxWireManager.TANGENT_MAGNITUDE;
			wireMesh.Orientation0.Value = ProtoFluxWireManager.WIRE_ORIENTATION_REFERENCE;
			wireMesh.Orientation1.Value = ProtoFluxWireManager.WIRE_ORIENTATION_REFERENCE;
		}
	}


	/// Creates or retrieves a shared FresnelMaterial for wire rendering
	private static FresnelMaterial GetOrCreateSharedMaterial(Slot slot) {
		// Create material in temporary storage
		FresnelMaterial fresnelMaterial = slot.World.RootSlot
			.FindChildOrAdd("__TEMP", false)
			.FindChildOrAdd($"{slot.LocalUser.UserName}-Scrolling-ProtoFluxVisualsOverhaul", false)
			.GetComponentOrAttach<FresnelMaterial>();

		// Ensure cleanup when user leaves
		fresnelMaterial.Slot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = slot.LocalUser;
		
		// Configure material properties
		fresnelMaterial.NearColor.Value = new colorX(0.8f);
		fresnelMaterial.FarColor.Value = new colorX(1.4f);
		fresnelMaterial.Sidedness.Value = Sidedness.Double;
		fresnelMaterial.UseVertexColors.Value = true;
		fresnelMaterial.BlendMode.Value = BlendMode.Alpha;
		fresnelMaterial.ZWrite.Value = ZWrite.On;
		
		// Setup textures from config
		var farTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, Config.GetValue(FAR_TEXTURE));
		fresnelMaterial.FarTexture.Target = farTexture;
		
		var nearTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, Config.GetValue(NEAR_TEXTURE));
		fresnelMaterial.NearTexture.Target = nearTexture;
		
		return fresnelMaterial;
	}

	/// Creates or retrieves a shared texture with specified settings
	private static StaticTexture2D GetOrCreateSharedTexture(Slot slot, Uri uri) {
		// Get or create the texture
		StaticTexture2D texture = slot.GetComponentOrAttach<StaticTexture2D>();
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

	// Hot reload methods
	[HarmonyPatch("BeforeHotReload")]
	public static void BeforeHotReload()
	{
		// Cleanup Harmony patches
		var harmony = new Harmony("com.Dexy.ProtoFluxVisualsOverhaul");
		harmony.UnpatchAll("com.Dexy.ProtoFluxVisualsOverhaul");
		
		// Clear cached data
		pannerCache.Clear();
		Logger.LogUI("Cleanup", "Cleaned up before hot reload");
	}

	public static void OnHotReload(ResoniteMod modInstance)
	{
		// Get the config from the mod instance
		Config = modInstance.GetConfiguration();
		
		// Re-apply Harmony patches
		var harmony = new Harmony("com.Dexy.ProtoFluxVisualsOverhaul");
		harmony.PatchAll();
		
		Logger.LogUI("Reload", "Hot reload complete");
	}

	[HarmonyPatch(typeof(ProtoFluxNodeVisual), "BuildUI")]
	class ProtoFluxNodeVisual_BuildUI_Patch {
		private static bool hasPreloadedAudio = false;

		public static void Postfix(ProtoFluxNodeVisual __instance) {
			try {
				if (!Config.GetValue(ENABLED)) return;

				// Preload audio clips only once when the first node is spawned
				if (!hasPreloadedAudio) {
					Logger.LogAudio("Preload", "First node spawned, preloading audio clips");
					ProtoFluxSounds.PreloadAudioClips(__instance.World);
					hasPreloadedAudio = true;
				}

				// Get the Overlapping Layout
				var overlappingLayout = __instance.Slot.FindChild("Overlapping Layout");
				if (overlappingLayout == null) return;

				// Process Inputs & Operations
				var inputsOps = overlappingLayout.FindChild("Inputs & Operations");
				if (inputsOps != null) {
					ProcessConnectors(inputsOps);
				}

				// Process Outputs & Impulses
				var outputsImps = overlappingLayout.FindChild("Outputs & Impulses");
				if (outputsImps != null) {
					ProcessConnectors(outputsImps);
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in ProtoFluxVisualsOverhaul BuildUI patch", e, LogCategory.UI);
			}
		}

		private static void ProcessConnectors(Slot root) {
			foreach (var child in root.Children) {
				// Look for Connector slot
				var connectorSlot = child.FindChild("Connector");
				if (connectorSlot != null) {
					// Look for WIRE_POINT
					var wirePoint = connectorSlot.FindChild("<WIRE_POINT>");
					if (wirePoint != null) {
						// We no longer need to create audio clips here
						// They will be created on-demand when sounds need to be played
					}
				}

				// Recursively process children
				ProcessConnectors(child);
			}
		}

		private static string GetSlotPath(Slot slot) {
			var path = new System.Collections.Generic.List<string>();
			var current = slot;
			while (current != null) {
				path.Add(current.Name);
				current = current.Parent;
			}
			path.Reverse();
			return string.Join(" → ", path);
		}
	}

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
					
					// Allow the wire owner or admins to modify
					bool hasPermission = (instanceAllocUser != null && 
						   position1 >= instanceAllocUser.AllocationIDStart &&
						   (instanceAllocUser == instance.LocalUser || instance.LocalUser.IsHost));
					
					Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={instanceAllocUser?.UserName}, IsLocalUser={instanceAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={hasPermission}");
					return hasPermission;
				}
				
				// Allow the wire owner or admins to modify
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
					
					// Allow the wire owner or admins to modify
					bool hasPermission = (instanceAllocUser != null && 
						   position1 >= instanceAllocUser.AllocationIDStart &&
						   (instanceAllocUser == component.LocalUser || component.LocalUser.IsHost));
					
					Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={instanceAllocUser?.UserName}, IsLocalUser={instanceAllocUser == component.LocalUser}, IsHost={component.LocalUser.IsHost}, Result={hasPermission}");
					return hasPermission;
				}
				
				// Allow the wire owner or admins to modify
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
