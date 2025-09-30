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
	internal const string VERSION_CONSTANT = "1.4.6";
	public override string Name => "ProtoFluxOverhaul";
	public override string Author => "Dexy, NepuShiro";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DexyThePuppy/ProtoFluxOverhaul";

	// Configuration
	public static ModConfiguration Config;
	public static readonly Dictionary<Slot, Panner2D> pannerCache = new Dictionary<Slot, Panner2D>();
	public static readonly Dictionary<MeshRenderer, FresnelMaterial> materialCache = new Dictionary<MeshRenderer, FresnelMaterial>();
	
	// ============ BASIC SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_BASIC = new("spacerMain", "--- Main Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ENABLED = new("Enabled", "Should ProtoFluxOverhaul be Enabled?", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DEBUG_LOGGING = new("Enable Debug Logging", "Enable Debug Logging", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> USE_HEADER_COLOR_FOR_BACKGROUND = new("Colored Node Background", "Use Node Type Color as Background Color for Nodes", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ENABLE_CONNECTOR_LABEL_BACKGROUNDS = new("Enable Connector Label Backgrounds", "Enable the background images on connector labels", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ENABLE_HEADER_BACKGROUND = new("Enable Header Background", "Enable the background image on node headers", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ENABLE_FOOTER_CATEGORY_TEXT = new("Enable Footer Category Text", "Enable the category text at the bottom of nodes", () => true);

	// ============ ANIMATION SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_ANIMATION = new("spacerAnimation", "--- Animation Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float2> SCROLL_SPEED = new("Scroll Speed", "Scroll Speed (X,Y)", () => new float2(-0.5f, 0f));
	
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float2> SCROLL_REPEAT = new("Scroll Repeat Interval", "Scroll Repeat Interval (X,Y)", () => new float2(1f, 1f));
	

	// ============ TEXTURE URLS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_TEXTURES = new("spacerTextures", "--- Texture URLs ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> WIRE_TEXTURE = new("Wire Texture", "Wire Texture URL", () => new Uri("resdb:///75bfbcf76ebd1319013405fb2a33762aca2e4d0a0e494ecf69e20091ddf2a6de.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECTOR_INPUT_TEXTURE = new("Default Connector | Both | Texture", "Default Connector Texture URL", () => new Uri("resdb:///86f073f1eca1168ec2b36819cf99f3a6be56d1fd8c5d7b4a8165832ef9ffdaa4.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_INPUT_TEXTURE = new("Call Connector | Input | Texture", "Call Connector Input Texture URL", () => new Uri("resdb:///9b2b22316dd9775d512006995bbcdeaf0bfcebe0e7ee22034facf699b2d76d06.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_OUTPUT_TEXTURE = new("Call Connector | Output | Texture", "Call Connector Output Texture URL", () => new Uri("resdb:///52d53736e60f77c9f7856c87483d827eec7b0946310cd6aefc25b3c22bcd810e.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X1_CONNECTOR_TEXTURE = new("x1 Connector | Texture", "Vector x1 Connector Texture URL", () => new Uri("resdb:///829214df5aaacecb2782da6c7b9eb4b5f45b7498a0c180ac65155afc0f92453c.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X2_CONNECTOR_TEXTURE = new("x2 Connector | Texture", "Vector x2 Connector Texture URL", () => new Uri("resdb:///27105afe77edf1ef5f7105548d95cd0189f38436c26f59923c84787b2109333d.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> VECTOR_X3_CONNECTOR_TEXTURE = new("x3 Connector | Texture", "Vector x3 Connector Texture URL", () => new Uri("resdb:///69f01c5e9ad9084125cbae13d860ae4284f9adfbce0f67fb8f21431d5d659704.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_TEXTURE = new("Node Background | Texture", "Node Background Texture URL", () => new Uri("resdb:///f020df95cf3b923094540d50796174b884cdd2061798032a48d5975d0570d0cd.png"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_HEADER_TEXTURE = new("Node Background Header | Texture", "Node Background Header Texture URL", () => new Uri("resdb:///81ee369eaf5e92513de9a3a5bf4604d619434981ce4b4a091a7777df75bc9ec2.png"));

	// ============ AUDIO SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_AUDIO = new("spacerAudio", "--- Audio Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> WIRE_SOUNDS = new("Wire Sounds", "Should wire interaction sounds be enabled?", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> GRAB_SOUND = new("Grab Sound", "Grab Sound URL", () => new Uri("resdb:///391ce0c681b24b79a0240a1fa2e4a4c06492619897c0e6e045640e71a34b7ec7.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> DELETE_SOUND = new("Delete Sound", "Delete Sound URL", () => new Uri("resdb:///b0c4195cce0990b27a3525623f46787d247c530387f8bc551e50bcf0584ab28b.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> CONNECT_SOUND = new("Connect Sound", "Connect Sound URL", () => new Uri("resdb:///8c63d74efcef070bf8fec2f9b1b20eecb15a499b17c64abaad225467d138d93b.wav"));

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> AUDIO_VOLUME = new("Audio Volume", "Audio Volume", () => 1f);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> MIN_DISTANCE = new("Audio Min Distance", "Audio Min Distance", () => 0.1f);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> MAX_DISTANCE = new("Audio Max Distance", "Audio Max Distance", () => 25f);

	// ============ ADVANCED TEXTURE SETTINGS ============
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<dummy> SPACER_ADVANCED = new("spacerAdvanced", "--- Advanced Texture Settings ---", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<int> ANISOTROPIC_LEVEL = new("Anisotropic Level", "Anisotropic Level", () => 8);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> MIPMAPS = new("Generate MipMaps", "Generate MipMaps", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> KEEP_ORIGINAL_MIPMAPS = new("Keep Original MipMaps", "Keep Original MipMaps", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Filtering> MIPMAP_FILTER = new("MipMap Filter", "MipMap Filter", () => Filtering.Box);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> UNCOMPRESSED = new("Uncompressed Texture", "Uncompressed Texture", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DIRECT_LOAD = new("Direct Load", "Direct Load", () => false);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> FORCE_EXACT_VARIANT = new("Force Exact Variant", "Force Exact Variant", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> CRUNCH_COMPRESSED = new("Crunch Compression", "Use Crunch Compression", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureCompression> PREFERRED_FORMAT = new("Preferred Texture Format", "Preferred Texture Format", () => TextureCompression.BC3_Crunched);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> READABLE = new("Readable Texture", "Readable Texture", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureFilterMode> FILTER_MODE = new("Texture Filter Mode", "Texture Filter Mode", () => TextureFilterMode.Bilinear);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_U = new("Texture Wrap Mode U", "Texture Wrap Mode U", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_V = new("Texture Wrap Mode V", "Texture Wrap Mode V", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<ColorProfile> PREFERRED_PROFILE = new("Preferred Color Profile", "Preferred Color Profile", () => ColorProfile.sRGB);


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

						// Ensure the panner is properly initialized before setting properties
						try {
							panner.Speed = Config.GetValue(SCROLL_SPEED);
							panner.Repeat = Config.GetValue(SCROLL_REPEAT);
						} catch (System.NullReferenceException) {
							// Skip this panner if it's not properly initialized
							Logger.LogWarning($"Skipping uninitialized Panner2D on {kvp.Key.Name}");
							continue;
						}

						// Get the FresnelMaterial
						var fresnelMaterial = kvp.Key.GetComponent<FresnelMaterial>();
						if (fresnelMaterial != null) {
					var farTexture = GetOrCreateSharedTexture(kvp.Key, Config.GetValue(WIRE_TEXTURE));
						fresnelMaterial.FarTexture.Target = farTexture;
						
						var nearTexture = GetOrCreateSharedTexture(kvp.Key, Config.GetValue(WIRE_TEXTURE));
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
				if (!WirePermissionHelper.HasPermission(__instance)) {
					// Skip silently for unauthorized wires to reduce log spam
					return;
				}
				
				// === Material Setup ===
				var renderer = ____renderer?.Target;
				if (renderer == null) return;

				if (!materialCache.TryGetValue(renderer, out var fresnelMaterial) || fresnelMaterial == null || fresnelMaterial.IsRemoved) {
					var originalMaterial = renderer.Material.Target as FresnelMaterial;
					if (originalMaterial == null) {
						return;
					}

					var newMaterial = renderer.Slot.AttachComponent<FresnelMaterial>();
					newMaterial.NearColor.Value = originalMaterial.NearColor.Value;
					newMaterial.FarColor.Value = originalMaterial.FarColor.Value;
					newMaterial.Sidedness.Value = originalMaterial.Sidedness.Value;
					newMaterial.UseVertexColors.Value = originalMaterial.UseVertexColors.Value;
					newMaterial.BlendMode.Value = originalMaterial.BlendMode.Value;
					newMaterial.ZWrite.Value = originalMaterial.ZWrite.Value;
					newMaterial.NearTextureScale.Value = originalMaterial.NearTextureScale.Value;
					newMaterial.NearTextureOffset.Value = originalMaterial.NearTextureOffset.Value;
					newMaterial.FarTextureScale.Value = originalMaterial.FarTextureScale.Value;
					newMaterial.FarTextureOffset.Value = originalMaterial.FarTextureOffset.Value;

					materialCache[renderer] = newMaterial;
					fresnelMaterial = newMaterial;
					renderer.Material.Target = fresnelMaterial;
				}

				// === Animation Setup ===
				// Get or create Panner2D for scrolling effect
				if (!pannerCache.TryGetValue(__instance.Slot, out var panner)) {
					panner = __instance.Slot.GetComponentOrAttach<Panner2D>();
					pannerCache[__instance.Slot] = panner;
				}

				try {
					panner.Speed = Config.GetValue(SCROLL_SPEED);
					panner.Repeat = Config.GetValue(SCROLL_REPEAT);
				} catch (System.NullReferenceException) {
					Logger.LogWarning($"Skipping uninitialized Panner2D in patch for {__instance.Slot.Name}");
					return;
				}

				var baseSpeed = Config.GetValue(SCROLL_SPEED);
				bool flipDirection = __instance.Type.Value == WireType.Input;
				float directionFactor = flipDirection ? -1f : 1f;
				panner.Speed = new float2(baseSpeed.x * directionFactor, baseSpeed.y);

				var farTexture = GetOrCreateSharedTexture(__instance.Slot, Config.GetValue(WIRE_TEXTURE));
				fresnelMaterial.FarTexture.Target = farTexture;

				var nearTexture = GetOrCreateSharedTexture(__instance.Slot, Config.GetValue(WIRE_TEXTURE));
				fresnelMaterial.NearTexture.Target = nearTexture;

				// === Texture Offset Setup ===
				// Setup texture offset drivers if they don't exist
				if (!fresnelMaterial.FarTextureOffset.IsLinked) {
					if (panner.Target == null)
					{
						panner.Target = fresnelMaterial.FarTextureOffset;
					}
				}

				if (!fresnelMaterial.NearTextureOffset.IsLinked) {
					ValueDriver<float2> newNearDrive = fresnelMaterial.Slot.GetComponentOrAttach<ValueDriver<float2>>();
					
					if (!newNearDrive.DriveTarget.IsLinkValid)
					{
						newNearDrive.DriveTarget.Target = fresnelMaterial.NearTextureOffset;
						newNearDrive.ValueSource.Target = panner.Target;
					}
				}
			}
			catch (Exception e) {
				Logger.LogError("Error in ProtoFluxOverhaul OnChanges patch", e, LogCategory.UI);
			}
		}
	}

	/// Creates or retrieves a shared FresnelMaterial for wire rendering
	/// Creates or retrieves a texture with specified settings directly on the wire slot
	private static StaticTexture2D GetOrCreateSharedTexture(Slot slot, Uri uri) {
		StaticTexture2D texture = slot.GetComponentOrAttach<StaticTexture2D>();
		texture.URL.Value = uri;

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
		texture.PowerOfTwoAlignThreshold.Value = 0.05f;
		
		return texture;
	}


	// Audio initialization is now handled on-demand by ProtoFluxSounds using engine's optimized patterns

	// Shared wire permission helper
	public static class WirePermissionHelper {
		public static bool HasPermission(ProtoFluxWireManager instance) {
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
	}

	// Patches for wire-related events
	[HarmonyPatch(typeof(ProtoFluxWireManager))]
	public class ProtoFluxWireManager_Patches {
		[HarmonyPatch("OnDestroy")]
		[HarmonyPostfix]
		public static void OnDestroy_Postfix(ProtoFluxWireManager __instance)
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

				// Only process if this is an actual user deletion (DeleteHighlight = true)
				if (!__instance.DeleteHighlight.Value) {
					// This is a system cleanup, not a user deletion - skip silently
					return;
				}

				// Play delete sound if we have permission for user-initiated deletion
				bool hasPermission = WirePermissionHelper.HasPermission(__instance);
				if (hasPermission)
				{
					Logger.LogWire("Delete", $"Playing wire delete sound at position {__instance.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireDeleted(__instance.World, __instance.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Delete", $"Wire delete sound skipped: Insufficient permissions for user {__instance.LocalUser?.UserName}");
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
				bool hasPermission = proxy != null && HasPermission(proxy);
				if (hasPermission) {
					Logger.LogWire("Grab", $"Playing wire grab sound at position {proxy.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireGrabbed(__instance.World, proxy.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Grab", $"Wire grab sound skipped: Proxy={proxy != null}, HasPermission={hasPermission}");
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
				bool hasPermission = input != null && HasPermission(input);
				if (input != null && output != null && hasPermission) {
						Logger.LogWire("Connect", $"Playing wire connect sound (Input-Output) at position {input.Slot.GlobalPosition}");
						ProtoFluxSounds.OnWireConnected(__instance.World, input.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Connect", $"Wire connect sound skipped: Input={input != null}, Output={output != null}, HasPermission={hasPermission}");
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
				bool hasPermission = impulse != null && HasPermission(impulse);
				if (impulse != null && operation != null && hasPermission) {
					Logger.LogWire("Connect", $"Playing wire connect sound (Impulse-Operation) at position {impulse.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, impulse.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Connect", $"Wire connect sound skipped: Impulse={impulse != null}, Operation={operation != null}, HasPermission={hasPermission}");
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
				bool hasPermission = node != null && HasPermission(node);
				if (node != null && input != null && output != null && hasPermission) {
					Logger.LogWire("Connect", $"Playing wire connect sound (Node-Input-Output) at position {node.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, node.Slot.GlobalPosition);
				} else {
					Logger.LogWire("Connect", $"Wire connect sound skipped: Node={node != null}, Input={input != null}, Output={output != null}, HasPermission={hasPermission}");
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
						bool hasPermission = wire != null && !wire.IsRemoved && HasPermission(wire);
						if (hasPermission) {
							Logger.LogWire("Delete", $"Playing wire delete sound (cut) at position {wire.Slot.GlobalPosition}");
							ProtoFluxSounds.OnWireDeleted(__instance.World, wire.Slot.GlobalPosition);
						} else {
							Logger.LogWire("Delete", $"Wire delete sound skipped (cut): Wire={wire != null}, IsRemoved={wire?.IsRemoved}, HasPermission={hasPermission}");
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
