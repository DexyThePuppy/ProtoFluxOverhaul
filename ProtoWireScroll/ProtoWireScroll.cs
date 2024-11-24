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
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
	public override string Name => "ProtoWireScroll";
	public override string Author => "Dexy";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/DexyThePuppy/ProtoWireScroll";

	// Configuration
	public static ModConfiguration? Config;
	private static readonly Dictionary<Slot, Panner2D> pannerCache = new Dictionary<Slot, Panner2D>();

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float2> SCROLL_SPEED = new("scrollSpeed", "Scroll Speed (X,Y)", () => new float2(-0.5f, 0f));
	
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<float2> SCROLL_REPEAT = new("scrollRepeat", "Scroll Repeat Interval (X,Y)", () => new float2(1f, 1f));
	
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PING_PONG = new("pingPong", "Ping Pong Animation", () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<Uri> FAR_TEXTURE = new("farTexture", "Far Texture URL", () => {
		var uri = new Uri("resdb:///5e31d9fdc3533ec5fc3c8272ec10f4b2a9c5ccae2c1f9b3cbee60337dc4f4ba4.png");
		var textureSlot = Engine.Current.WorldManager.FocusedWorld.RootSlot.AddSlot("FarTexture");
		CreateAndConfigureTexture(textureSlot, uri);
		return uri;
	});

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<Uri> NEAR_TEXTURE = new("nearTexture", "Near Texture URL", () => {
		var uri = new Uri("resdb:///5e31d9fdc3533ec5fc3c8272ec10f4b2a9c5ccae2c1f9b3cbee60337dc4f4ba4.png");
		var textureSlot = Engine.Current.WorldManager.FocusedWorld.RootSlot.AddSlot("NearTexture");
		CreateAndConfigureTexture(textureSlot, uri);
		return uri;
	});

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
			Harmony harmony = new Harmony("com.example.ProtoWireScroll");
			harmony.PatchAll();
			Msg("🐾 ProtoWireScroll successfully loaded and patched! Woof!");
		}
		catch (Exception e) {
			Error($"🐾 ProtoWireScroll failed to initialize! Error: {e.Message}");
			Error(e.StackTrace);
		}
	}

	private static void OnConfigChanged(ConfigurationChangedEvent configEvent) {
		if (Config == null) return;  // Early return if Config is null
		
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
					var farTexture = CreateAndConfigureTexture(fresnelMaterial.Slot, farTextureUrl);
					fresnelMaterial.FarTexture.Target = farTexture;
				}
				if (Config.TryGetValue(NEAR_TEXTURE, out var nearTextureUrl) && nearTextureUrl != null) {
					var nearTexture = CreateAndConfigureTexture(fresnelMaterial.Slot, nearTextureUrl);
					fresnelMaterial.NearTexture.Target = nearTexture;
				}
			}
		}
		UniLog.Log("🐾 Updated all ProtoWireScroll settings! *happy tail wag*");
	}

	//Example of how a HarmonyPatch can be formatted, Note that the following isn't a real patch and will not compile.
	[HarmonyPatch(typeof(ProtoFluxWireManager), "OnChanges")]
	class ProtoFluxWireManager_OnChanges_Patch 
	{
		static readonly string LOG_PREFIX = "[ProtoWireScroll] ";

		public static void Postfix(ProtoFluxWireManager __instance) 
		{
			var renderer = AccessTools.Field(typeof(ProtoFluxWireManager), "_renderer").GetValue(__instance) as SyncRef<MeshRenderer>;
			
			if (renderer?.Target == null)
			{
				UniLog.Error($"{LOG_PREFIX}Renderer reference or target is null! *whimpers*");
				return;
			}

			var material = renderer.Target.Material.Target;
			if (!(material is FresnelMaterial fresnelMaterial))
			{
				UniLog.Warning($"{LOG_PREFIX}Material is not FresnelMaterial! *sad puppy noises*");
				return;
			}

			// Get or create Panner2D
			if (!pannerCache.TryGetValue(fresnelMaterial.Slot, out var panner))
			{
				panner = fresnelMaterial.Slot.AttachComponent<Panner2D>();
				
				if (Config?.TryGetValue(SCROLL_SPEED, out var speed) == true) {
					panner.Speed = speed;
				}
				if (Config?.TryGetValue(SCROLL_REPEAT, out var repeat) == true) {
					panner.Repeat = repeat;
				}
				if (Config?.TryGetValue(PING_PONG, out var pingPong) == true) {
					panner.PingPong.Value = pingPong;
				}
				pannerCache[fresnelMaterial.Slot] = panner;

				// Set textures
				if (Config?.TryGetValue(FAR_TEXTURE, out var farTextureUrl) == true && farTextureUrl != null) {
					var farTexture = CreateAndConfigureTexture(fresnelMaterial.Slot, farTextureUrl);
					fresnelMaterial.FarTexture.Target = farTexture;
				}
				if (Config?.TryGetValue(NEAR_TEXTURE, out var nearTextureUrl) == true && nearTextureUrl != null) {
					var nearTexture = CreateAndConfigureTexture(fresnelMaterial.Slot, nearTextureUrl);
					fresnelMaterial.NearTexture.Target = nearTexture;
				}

				UniLog.Log($"{LOG_PREFIX}Created new Panner2D *tail wag*");
			}

			// Setup texture offset drivers if they don't exist
			if (!fresnelMaterial.FarTextureOffset.IsLinked)
			{
				panner.Target = fresnelMaterial.FarTextureOffset;  // Direct assignment to the IField
				UniLog.Log($"{LOG_PREFIX}Linked FarTextureOffset to Panner2D! Woof!");
			}

			if (!fresnelMaterial.NearTextureOffset.IsLinked)
			{
				var newNearDrive = fresnelMaterial.Slot.AttachComponent<ValueDriver<float2>>();
				newNearDrive.DriveTarget.Target = fresnelMaterial.NearTextureOffset;
				newNearDrive.ValueSource.Target = panner.Target;
				UniLog.Log($"{LOG_PREFIX}Created new NearTextureOffset driver! *excited bark*");
			}
		}
	}

	[HarmonyPatch(typeof(ProtoFluxWireManager), "Setup")]
	class ProtoFluxWireManager_Setup_Patch
	{
		static void Prefix(ProtoFluxWireManager __instance, WireType type, ref float width, ref colorX startColor, ref int atlasOffset, ref bool collider, ref bool reverseTexture)
		{
			// Only flip the texture direction for input wires
			if (type == WireType.Input)
			{
				reverseTexture = !reverseTexture;
			}
		}
	}

	private static StaticTexture2D CreateAndConfigureTexture(Slot slot, Uri uri) {
		var texture = slot.AttachComponent<StaticTexture2D>();
		texture.URL.Value = uri;
		
		// Set default values immediately
		texture.FilterMode.Value = Config?.GetValue(FILTER_MODE) ?? TextureFilterMode.Point;
		texture.MipMaps.Value = Config?.GetValue(MIPMAPS) ?? false;
		texture.Uncompressed.Value = Config?.GetValue(UNCOMPRESSED) ?? true;
		texture.CrunchCompressed.Value = Config?.GetValue(CRUNCH_COMPRESSED) ?? false;
		texture.DirectLoad.Value = Config?.GetValue(DIRECT_LOAD) ?? false;
		texture.ForceExactVariant.Value = Config?.GetValue(FORCE_EXACT_VARIANT) ?? true;
		texture.AnisotropicLevel.Value = Config?.GetValue(ANISOTROPIC_LEVEL) ?? 1;
		texture.WrapModeU.Value = Config?.GetValue(WRAP_MODE_U) ?? TextureWrapMode.Repeat;
		texture.WrapModeV.Value = Config?.GetValue(WRAP_MODE_V) ?? TextureWrapMode.Repeat;
		texture.KeepOriginalMipMaps.Value = Config?.GetValue(KEEP_ORIGINAL_MIPMAPS) ?? false;
		texture.MipMapFilter.Value = Config?.GetValue(MIPMAP_FILTER) ?? Filtering.Box;
		texture.Readable.Value = Config?.GetValue(READABLE) ?? false;
		texture.PowerOfTwoAlignThreshold.Value = 0.05f;  // Add this for proper texture alignment

		return texture;
	}
}
