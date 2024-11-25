using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Collections.Generic;
using Elements.Assets;

namespace ProtoWireScroll;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class ProtoWireScroll : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.1"; //Changing the version here updates it in all locations needed
	public override string Name => "ProtoWireScroll";
	public override string Author => "Dexy";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DexyThePuppy/ProtoWireScroll";

	// Configuration
	public static ModConfiguration Config;
	private static readonly Dictionary<Slot, Panner2D> pannerCache = new Dictionary<Slot, Panner2D>();
	
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> ENABLED = new("Enabled", "Should ProtoWireScroll be Enabled?", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float2> SCROLL_SPEED = new("scrollSpeed", "Scroll Speed (X,Y)", () => new float2(-0.5f, 0f));
	
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float2> SCROLL_REPEAT = new("scrollRepeat", "Scroll Repeat Interval (X,Y)", () => new float2(1f, 1f));
	
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PING_PONG = new("pingPong", "Ping Pong Animation", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<Uri> FAR_TEXTURE = new("farTexture", "Far Texture URL", () => new Uri("resdb:///5e31d9fdc3533ec5fc3c8272ec10f4b2a9c5ccae2c1f9b3cbee60337dc4f4ba4.png"));

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<Uri> NEAR_TEXTURE = new("nearTexture", "Near Texture URL", () => new Uri("resdb:///5e31d9fdc3533ec5fc3c8272ec10f4b2a9c5ccae2c1f9b3cbee60337dc4f4ba4.png"));

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<TextureFilterMode> FILTER_MODE = new("filterMode", "Texture Filter Mode", () => TextureFilterMode.Point);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> MIPMAPS = new("mipMaps", "Generate MipMaps", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> UNCOMPRESSED = new("uncompressed", "Uncompressed Texture", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> DIRECT_LOAD = new("directLoad", "Direct Load", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> FORCE_EXACT_VARIANT = new("forceExactVariant", "Force Exact Variant", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> CRUNCH_COMPRESSED = new("crunchCompressed", "Use Crunch Compression", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_U = new("wrapModeU", "Texture Wrap Mode U", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<TextureWrapMode> WRAP_MODE_V = new("wrapModeV", "Texture Wrap Mode V", () => TextureWrapMode.Repeat);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> KEEP_ORIGINAL_MIPMAPS = new("keepOriginalMipMaps", "Keep Original MipMaps", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<Filtering> MIPMAP_FILTER = new("mipMapFilter", "MipMap Filter", () => Filtering.Box);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> READABLE = new("readable", "Readable Texture", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<int> ANISOTROPIC_LEVEL = new("anisotropicLevel", "Anisotropic Level", () => 1);

	public override void OnEngineInit() {
		Config = GetConfiguration();
		if (Config == null) {
			Error("Failed to get mod configuration! *sad puppy whimpers*");
			return;
		}

		Config.Save(true); // Save default config values
		Config.OnThisConfigurationChanged += OnConfigChanged;

		try {
			Harmony harmony = new Harmony("com.Dexy.ProtoWireScroll");
			harmony.PatchAll();
			Msg("🐾 ProtoWireScroll successfully loaded and patched! Woof!");
		}
		catch (Exception e) {
			Error($"🐾 ProtoWireScroll failed to initialize! Error: {e.Message}");
			Error(e.StackTrace);
		}
	}

	private static void OnConfigChanged(ConfigurationChangedEvent configEvent) {
		if (!Config.GetValue(ENABLED) || Config == null) return;  // Early return if Config is null or mod is Disabled
		
		foreach (var kvp in pannerCache) {
			var panner = kvp.Value;
			if (panner == null) continue;

			if (Config.TryGetValue(SCROLL_SPEED, out var speed)) {
				panner.Speed = speed;
			}
			if (Config.TryGetValue(SCROLL_REPEAT, out var repeat)) {
				panner.Repeat = repeat;
			}
			if (Config.TryGetValue(PING_PONG, out var pingPong)) {
				panner.PingPong.Value = pingPong;
			}

			// Get the FresnelMaterial
			var fresnelMaterial = kvp.Key.GetComponent<FresnelMaterial>();
			if (fresnelMaterial != null) {
				if (Config.TryGetValue(FAR_TEXTURE, out var farTextureUrl) && farTextureUrl != null) {
					var farTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, farTextureUrl);
					fresnelMaterial.FarTexture.Target = farTexture;
				}
				if (Config.TryGetValue(NEAR_TEXTURE, out var nearTextureUrl) && nearTextureUrl != null) {
					var nearTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, nearTextureUrl);
					fresnelMaterial.NearTexture.Target = nearTexture;
				}
			}
		}
		Msg("🐾 Updated all ProtoWireScroll settings! *happy tail wag*");
	}

	[HarmonyPatch(typeof(ProtoFluxWireManager), "OnChanges")]
	class ProtoFluxWireManager_OnChanges_Patch {
		public static void Postfix(ProtoFluxWireManager __instance, SyncRef<MeshRenderer> ____renderer, SyncRef<StripeWireMesh> ____wireMesh) {
			if (!Config.GetValue(ENABLED) || __instance == null) return;
			
			// Get the Allocating User
			__instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
			User allocatingUser = __instance.World.GetUserByAllocationID(user);
			
			// Don't run if the Allocating User isn't the LocalUser
			if (allocatingUser == null || position < allocatingUser.AllocationIDStart || allocatingUser != __instance.LocalUser) return;
			
			if (____renderer?.Target == null) {
				Error($"Renderer reference or target is null! *whimpers*");
				return;
			}
			
			// Get or Create the Fresnel Material
			var fresnelMaterial = GetOrCreateSharedMaterial(__instance.Slot);
			____renderer.Target.Material.Target = fresnelMaterial;

			// Get or create Panner2D
			if (!pannerCache.TryGetValue(fresnelMaterial.Slot, out var panner)) {
				panner = fresnelMaterial.Slot.GetComponentOrAttach<Panner2D>();
			
				if (Config.TryGetValue(SCROLL_SPEED, out var speed)) {
					panner.Speed = speed;
				}
				if (Config.TryGetValue(SCROLL_REPEAT, out var repeat)) {
					panner.Repeat = repeat;
				}
				if (Config.TryGetValue(PING_PONG, out var pingPong)) {
					panner.PingPong.Value = pingPong;
				}
				pannerCache[fresnelMaterial.Slot] = panner;

				// Set the textures
				if (Config.TryGetValue(FAR_TEXTURE, out var farTextureUrl) == true && farTextureUrl != null) {
					var farTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, farTextureUrl);
					fresnelMaterial.FarTexture.Target = farTexture;
				}
				if (Config.TryGetValue(NEAR_TEXTURE, out var nearTextureUrl) == true && nearTextureUrl != null) {
					var nearTexture = GetOrCreateSharedTexture(fresnelMaterial.Slot, nearTextureUrl);
					fresnelMaterial.NearTexture.Target = nearTexture;
				}

				Msg($"Created new Panner2D *tail wag*");
			}

			// Setup texture offset drivers if they don't exist
			if (!fresnelMaterial.FarTextureOffset.IsLinked) {
				panner.Target = fresnelMaterial.FarTextureOffset;  // Direct assignment to the IField
				Msg($"Linked FarTextureOffset to Panner2D! Woof!");
			}

			if (!fresnelMaterial.NearTextureOffset.IsLinked) {
				fresnelMaterial.Slot.RunSynchronously(() => {
					ValueDriver<float2> newNearDrive = fresnelMaterial.Slot.GetComponentOrAttach<ValueDriver<float2>>();
					newNearDrive.DriveTarget.Target = fresnelMaterial.NearTextureOffset;
					newNearDrive.ValueSource.Target = panner.Target;
					Msg($"Created new NearTextureOffset driver! *excited bark*");
				});
			}
			
			// Only flip the texture direction for input wires
			if (__instance.Type.Value == WireType.Input) {
				____wireMesh.Target.UVScale.Value = new float2(-1f, ProtoFluxWireManager.WIRE_ATLAS_RATIO);
			}
		}
	}

	[HarmonyPatch(typeof(ProtoFluxWireManager), "Setup")]
	class ProtoFluxWireManager_Setup_Patch {
		public static void Postfix(ProtoFluxWireManager __instance, WireType type, SyncRef<StripeWireMesh> ____wireMesh) {
			// Only flip the texture direction for input wires
			if (!Config.GetValue(ENABLED) || __instance == null) return;
			
			// Get the Allocating User
			__instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
			User allocatingUser = __instance.World.GetUserByAllocationID(user);
			
			// Don't run if the Allocating User isn't the LocalUser
			if (allocatingUser == null || position < allocatingUser.AllocationIDStart || allocatingUser != __instance.LocalUser) return;
			
			if (type == WireType.Input) {
				____wireMesh.Target.UVScale.Value = new float2(-1f, ProtoFluxWireManager.WIRE_ATLAS_RATIO);
			}
		}
	}
	
	private static FresnelMaterial GetOrCreateSharedMaterial(Slot slot) {
		// Add the new Material to __TEMP
		FresnelMaterial fresnel = slot.World.RootSlot.FindChildOrAdd("__TEMP", false).FindChildOrAdd($"{slot.LocalUser.UserName}-Scrolling-ProtofluxWire", false).GetComponentOrAttach<FresnelMaterial>();
		
		// This is from ProtoFluxWireManager.OnAttach();
		fresnel.NearColor.Value = new colorX(0.8f);
		fresnel.FarColor.Value = new colorX(1.4f);
		fresnel.Sidedness.Value = Sidedness.Double;
		fresnel.UseVertexColors.Value = true;
		fresnel.BlendMode.Value = BlendMode.Alpha;
		fresnel.ZWrite.Value = ZWrite.On;
		
		// Set the Textures
		if (Config.TryGetValue(FAR_TEXTURE, out var farTextureUrl) && farTextureUrl != null) {
			var farTexture = GetOrCreateSharedTexture(fresnel.Slot, farTextureUrl);
			fresnel.FarTexture.Target = farTexture;
		}
		if (Config.TryGetValue(NEAR_TEXTURE, out var nearTextureUrl) && nearTextureUrl != null) {
			var nearTexture = GetOrCreateSharedTexture(fresnel.Slot, nearTextureUrl);
			fresnel.NearTexture.Target = nearTexture;
		}
		
		return fresnel;
	}

	private static StaticTexture2D GetOrCreateSharedTexture(Slot slot, Uri uri) {
		// Gets the already existing Texture2D to replace the uri if needed
		StaticTexture2D texture = slot.GetComponentOrAttach<StaticTexture2D>();
		texture.URL.Value = uri;
	
		// Set default values immediately
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
		texture.PowerOfTwoAlignThreshold.Value = 0.05f;  // Add this for proper texture alignment
		
		return texture;
	}
}
