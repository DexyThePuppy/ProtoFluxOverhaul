using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using Renderite.Shared;
using ResoniteModLoader;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul : ResoniteMod {
	internal const string VERSION = "1.5.0";
	public override string Name => "ProtoFluxOverhaul";
	public override string Author => "Dexy, NepuShiro";
	public override string Version => VERSION;
	public override string Link => "https://github.com/DexyThePuppy/ProtoFluxOverhaul";

	// Configuration
	public static ModConfiguration Config;

	// ============ BASIC SETTINGS ============
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> SPACER_BASIC = new("spacerMain", "--- Main Settings ---", () => new dummy());
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> ENABLED = new("Enabled", "Should ProtoFluxOverhaul be Enabled?", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DEBUG_LOGGING = new("Enable Debug Logging", "Enable Debug Logging", () => false);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> USE_HEADER_COLOR_FOR_BACKGROUND = new("Colored Node Background", "Use Node Type Color as Background Color for Nodes", () => false);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> USE_PLATFORM_COLOR_PALETTE = new("Use PlatformColorPalette", "Attach PlatformColorPalette to each ProtoFlux node UI and drive header/background/overview Image.Tint via ValueCopy from the palette outputs", () => false);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> ENABLE_CONNECTOR_LABEL_BACKGROUNDS = new("Enable Connector Label Backgrounds", "Enable the background images on connector labels", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> AUTO_REBUILD_SELECTED_NODES = new("Auto Rebuild Selected Nodes", "When selecting ProtoFlux nodes, automatically rebuild them with ProtoFluxOverhaul styling (bypasses permission checks, not that this is only temporary and is reverted to the original behavior when the nodes are packed/unpacked).", () => false);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> ENABLE_HEADER_BACKGROUND = new("Enable Header Background", "Enable the background image on node headers", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> ENABLE_FOOTER_CATEGORY_TEXT = new("Enable Footer Category Text", "Enable the category text at the bottom of nodes", () => true);

	// ============ ANIMATION SETTINGS ============
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> SPACER_ANIMATION = new("spacerAnimation", "--- Animation Settings ---", () => new dummy());
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<float2> SCROLL_SPEED = new("Scroll Speed", "Scroll Speed (X,Y)", () => new float2(-0.5f, 0f));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<float2> SCROLL_REPEAT = new("Scroll Repeat Interval", "Scroll Repeat Interval (X,Y)", () => new float2(1f, 1f));

	// ============ TEXTURE URLS ============
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> SPACER_TEXTURES = new("spacerTextures", "--- Texture URLs ---", () => new dummy());
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> WIRE_TEXTURE = new("Wire Texture", "Wire Texture URL", () => new Uri("resdb:///75bfbcf76ebd1319013405fb2a33762aca2e4d0a0e494ecf69e20091ddf2a6de.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> CONNECTOR_INPUT_TEXTURE = new("Default Connector | Both | Texture", "Default Connector Texture URL", () => new Uri("resdb:///86f073f1eca1168ec2b36819cf99f3a6be56d1fd8c5d7b4a8165832ef9ffdaa4.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_INPUT_TEXTURE = new("Call Connector | Input | Texture", "Call Connector Input Texture URL", () => new Uri("resdb:///9b2b22316dd9775d512006995bbcdeaf0bfcebe0e7ee22034facf699b2d76d06.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> CALL_CONNECTOR_OUTPUT_TEXTURE = new("Call Connector | Output | Texture", "Call Connector Output Texture URL", () => new Uri("resdb:///52d53736e60f77c9f7856c87483d827eec7b0946310cd6aefc25b3c22bcd810e.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> VECTOR_X1_CONNECTOR_TEXTURE = new("x1 Connector | Texture", "Vector x1 Connector Texture URL", () => new Uri("resdb:///829214df5aaacecb2782da6c7b9eb4b5f45b7498a0c180ac65155afc0f92453c.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> VECTOR_X2_CONNECTOR_TEXTURE = new("x2 Connector | Texture", "Vector x2 Connector Texture URL", () => new Uri("resdb:///27105afe77edf1ef5f7105548d95cd0189f38436c26f59923c84787b2109333d.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> VECTOR_X3_CONNECTOR_TEXTURE = new("x3 Connector | Texture", "Vector x3 Connector Texture URL", () => new Uri("resdb:///69f01c5e9ad9084125cbae13d860ae4284f9adfbce0f67fb8f21431d5d659704.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_TEXTURE = new("Node Background | Texture", "Node Background Texture URL", () => new Uri("resdb:///440a5985be1b25ef7717047c0e1734ef31750cc01bccdfc976bf26b770dd2c08.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> NODE_BACKGROUND_HEADER_TEXTURE = new("Node Background Header | Texture", "Node Background Header Texture URL", () => new Uri("resdb:///440a5985be1b25ef7717047c0e1734ef31750cc01bccdfc976bf26b770dd2c08.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> SHADING_TEXTURE = new("Shading | Texture", "Shading overlay texture URL (used on node background, title, and label backgrounds)", () => new Uri("resdb:///80499be19181c3a3855718c62ab363166f1388cd29677e00aab00ec0e1c27101.png"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> SHADING_INVERTED_TEXTURE = new("Shading Inverted | Texture", "Shading overlay texture URL used for Title/Header and Label backgrounds", () => new Uri("resdb:///1daff2a96af606c5ea9147339cfdf5761eb4196d532af258a14461f391a467d1.png"));

	// ============ AUDIO SETTINGS ============
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> SPACER_AUDIO = new("spacerAudio", "--- Audio Settings ---", () => new dummy());
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> WIRE_SOUNDS = new("Wire Sounds", "Should wire interaction sounds be enabled?", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> NODE_SOUNDS = new("Node Sounds", "Should node interaction sounds be enabled?", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> GRAB_SOUND = new("Grab Sound", "Grab Sound URL", () => new Uri("resdb:///391ce0c681b24b79a0240a1fa2e4a4c06492619897c0e6e045640e71a34b7ec7.wav"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> DELETE_SOUND = new("Delete Sound", "Delete Sound URL", () => new Uri("resdb:///b0c4195cce0990b27a3525623f46787d247c530387f8bc551e50bcf0584ab28b.wav"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> CONNECT_SOUND = new("Connect Sound", "Connect Sound URL", () => new Uri("resdb:///8c63d74efcef070bf8fec2f9b1b20eecb15a499b17c64abaad225467d138d93b.wav"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> NODE_CREATE_SOUND = new("Node Create Sound", "Node Create Sound URL", () => new Uri("resdb:///8c63d74efcef070bf8fec2f9b1b20eecb15a499b17c64abaad225467d138d93b.wav"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Uri> NODE_GRAB_SOUND = new("Node Grab Sound", "Node Grab Sound URL", () => new Uri("resdb:///391ce0c681b24b79a0240a1fa2e4a4c06492619897c0e6e045640e71a34b7ec7.wav"));
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> AUDIO_VOLUME = new("Audio Volume", "Audio Volume", () => 1f);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MIN_DISTANCE = new("Audio Min Distance", "Audio Min Distance", () => 0.1f);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<float> MAX_DISTANCE = new("Audio Max Distance", "Audio Max Distance", () => 25f);

	// ============ ADVANCED TEXTURE SETTINGS ============
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<dummy> SPACER_ADVANCED = new("spacerAdvanced", "--- Advanced Texture Settings ---", () => new dummy());
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<int> ANISOTROPIC_LEVEL = new("Anisotropic Level", "Anisotropic Level", () => 16);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> MIPMAPS = new("Generate MipMaps", "Generate MipMaps", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> KEEP_ORIGINAL_MIPMAPS = new("Keep Original MipMaps", "Keep Original MipMaps", () => false);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<Filtering> MIPMAP_FILTER = new("MipMap Filter", "MipMap Filter", () => Filtering.Lanczos3);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> UNCOMPRESSED = new("Uncompressed Texture", "Uncompressed Texture", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> DIRECT_LOAD = new("Direct Load", "Direct Load", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> FORCE_EXACT_VARIANT = new("Force Exact Variant", "Force Exact Variant", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> CRUNCH_COMPRESSED = new("Crunch Compression", "Use Crunch Compression", () => false);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<TextureCompression> PREFERRED_FORMAT = new("Preferred Texture Format", "Preferred Texture Format", () => TextureCompression.BC3_Crunched);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<bool> READABLE = new("Readable Texture", "Readable Texture", () => true);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<TextureFilterMode> FILTER_MODE = new("Texture Filter Mode", "Texture Filter Mode", () => TextureFilterMode.Anisotropic);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_U = new("Texture Wrap Mode U", "Texture Wrap Mode U", () => TextureWrapMode.Repeat);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_V = new("Texture Wrap Mode V", "Texture Wrap Mode V", () => TextureWrapMode.Clamp);
	[AutoRegisterConfigKey] public static readonly ModConfigurationKey<ColorProfile> PREFERRED_PROFILE = new("Preferred Color Profile", "Preferred Color Profile", () => ColorProfile.sRGBAlpha);

	/// <summary>
	/// Shared ownership/host permission check for any component.
	/// Centralized to keep all patches consistent and reduce duplication.
	/// </summary>
	private static bool HasPermission(Component component) {
		try {
			if (component == null || component.Slot == null) {
				Logger.LogPermission("Check", false, "Permission check failed: component or slot is null");
				return false;
			}

			// Get the component's slot owner (allocation info)
			component.Slot.ReferenceID.ExtractIDs(out ulong slotPosition, out byte slotUser);
			User slotAllocUser = component.World.GetUserByAllocationID(slotUser);
			Logger.LogPermission("Slot Point", true, $"Slot allocation: Position={slotPosition}, UserID={slotUser}, User={slotAllocUser?.UserName}, Type={component.GetType().Name}");

			// If the slot allocation isn't valid, fall back to component allocation.
			if (slotAllocUser == null || slotPosition < slotAllocUser.AllocationIDStart) {
				component.ReferenceID.ExtractIDs(out ulong componentPosition, out byte componentUser);
				User componentAllocUser = component.World.GetUserByAllocationID(componentUser);
				Logger.LogPermission("Instance", true, $"Instance allocation: Position={componentPosition}, UserID={componentUser}, User={componentAllocUser?.UserName}, Type={component.GetType().Name}");

				bool hasPermission = (componentAllocUser != null &&
					componentPosition >= componentAllocUser.AllocationIDStart &&
					componentAllocUser == component.LocalUser);

				Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={componentAllocUser?.UserName}, IsLocalUser={componentAllocUser == component.LocalUser}, IsHost={component.LocalUser.IsHost}, Result={hasPermission}, Type={component.GetType().Name}");
				return hasPermission;
			}

			bool result = slotAllocUser == component.LocalUser;
			Logger.LogPermission("Slot Check", result, $"Permission check (slot): Owner={slotAllocUser?.UserName}, IsLocalUser={slotAllocUser == component.LocalUser}, IsHost={component.LocalUser.IsHost}, Result={result}, Type={component.GetType().Name}");
			return result;
		} catch (Exception e) {
			// If anything goes wrong, deny permission to be safe
			Logger.LogError("Permission check error", e, LogCategory.Permission);
			return false;
		}
	}

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
					foreach (var kvp in _pannerCache.ToList()) {
						var panner = kvp.Value;
						if (panner == null || panner.IsRemoved) {
							// Clean up stale cache entries
							_pannerCache.Remove(kvp.Key);
							continue;
						}

						// Ensure the panner is properly initialized before setting properties
						try {
							panner.Speed = Config.GetValue(SCROLL_SPEED);
							panner.Repeat = Config.GetValue(SCROLL_REPEAT);
						} catch (System.NullReferenceException) {
							// Skip this panner if it's not properly initialized
							Logger.LogWarning($"Skipping uninitialized Panner2D on {kvp.Key.Name}");
							continue;
						}

						// Panner/material/texture live on the PFO child slot
						var pfoSlot = kvp.Key;
						var fresnelMaterial = pfoSlot.GetComponent<FresnelMaterial>();
						if (fresnelMaterial != null) {
							try {
								var farTexture = GetOrCreateSharedTexture(pfoSlot, Config.GetValue(WIRE_TEXTURE));
								fresnelMaterial.FarTexture.Target = farTexture;

								var nearTexture = GetOrCreateSharedTexture(pfoSlot, Config.GetValue(WIRE_TEXTURE));
								fresnelMaterial.NearTexture.Target = nearTexture;
							} catch (Exception ex) {
								Logger.LogError($"Error updating textures for panner on slot {pfoSlot.Name}", ex, LogCategory.UI);
							}
						}
					}
				});
			}
		};
	}
}
