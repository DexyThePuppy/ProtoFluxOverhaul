using System;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using Renderite.Shared;

namespace ProtoFluxOverhaul;

/// <summary>
/// World-shared textures, sprites, and wire Fresnel materials, rooted under
/// __TEMP/ProtoFluxOverhaul/&lt;user&gt;. References can outlive their creating user.
/// </summary>
internal static class SharedAssets
{
	internal const string WireTextureKey = "PFO_WireTexture";
	internal const string WireMaterialKey = "PFO_WireMaterial";

	internal static int Version { get; private set; } = 1;

	internal static void BumpVersion()
	{
		Version++;
	}

	internal static Slot GetUserAssetsSlot(World world, string folder)
	{
		if (world == null || world.IsDisposed || world.IsDestroyed || world.RootSlot == null) return null;

		var tempSlot = world.RootSlot.FindChild("__TEMP") ?? world.RootSlot.AddSlot("__TEMP", false);
		var modSlot = tempSlot.FindChild("ProtoFluxOverhaul") ?? tempSlot.AddSlot("ProtoFluxOverhaul", false);
		var userName = world.LocalUser?.UserName ?? "Local";
		var userSlot = modSlot.FindChild(userName) ?? modSlot.AddSlot(userName, false);
		var folderSlot = userSlot.FindChild(folder) ?? userSlot.AddSlot(folder, false);
		// Repair folders made by older builds without replacing any referenced assets.
		// Audio uses this same path so it cannot reattach a cleaner above the visuals.
		RemoveLeaveCleanup(modSlot);
		RemoveLeaveCleanup(userSlot);
		RemoveLeaveCleanup(folderSlot);
		return folderSlot;
	}

	private static void RemoveLeaveCleanup(Slot slot)
	{
		foreach (var cleanup in slot.GetComponents<DestroyOnUserLeave>())
			cleanup.Destroy();
	}

	[HarmonyPatch(typeof(World), nameof(World.StartRunning))]
	private static class PreserveExistingAssetsPatch
	{
		private static void Postfix(World __instance)
		{
			// Run after the initial world state arrives, even if no nodes are rebuilt
			// or sounds played. Keep this lifetime repair active when styling is off.
			__instance.RunSynchronously(() =>
			{
				if (__instance.IsDisposed || __instance.IsDestroyed) return;
				try
				{
					var assets = __instance.RootSlot?.FindChild("__TEMP")?.FindChild("ProtoFluxOverhaul");
					if (assets == null) return;
					foreach (var cleanup in assets.GetComponentsInChildren<DestroyOnUserLeave>())
						cleanup.Destroy();
				}
				catch (Exception ex)
				{
					Logger.LogError("Error preserving shared assets after world load", ex, Logger.LogCategory.UI);
				}
			}, immediatellyIfPossible: true);
		}
	}

	internal static void ApplyTextureSettings(StaticTexture2D texture, Uri uri, bool clamp)
	{
		if (texture == null) return;
		var config = ProtoFluxOverhaul.Config;

		if (!EqualsUri(texture.URL.Value, uri))
			texture.URL.Value = uri;

		var fm = config.GetValue(ProtoFluxOverhaul.FILTER_MODE);
		if (texture.FilterMode.Value != fm)
			texture.FilterMode.Value = fm;

		var mip = config.GetValue(ProtoFluxOverhaul.MIPMAPS);
		if (texture.MipMaps.Value != mip)
			texture.MipMaps.Value = mip;

		var unc = config.GetValue(ProtoFluxOverhaul.UNCOMPRESSED);
		if (texture.Uncompressed.Value != unc)
			texture.Uncompressed.Value = unc;

		var crunch = config.GetValue(ProtoFluxOverhaul.CRUNCH_COMPRESSED);
		if (texture.CrunchCompressed.Value != crunch)
			texture.CrunchCompressed.Value = crunch;

		var direct = config.GetValue(ProtoFluxOverhaul.DIRECT_LOAD);
		if (texture.DirectLoad.Value != direct)
			texture.DirectLoad.Value = direct;

		var exact = config.GetValue(ProtoFluxOverhaul.FORCE_EXACT_VARIANT);
		if (texture.ForceExactVariant.Value != exact)
			texture.ForceExactVariant.Value = exact;

		var aniso = config.GetValue(ProtoFluxOverhaul.ANISOTROPIC_LEVEL);
		if (texture.AnisotropicLevel.Value != aniso)
			texture.AnisotropicLevel.Value = aniso;

		var wu = clamp ? TextureWrapMode.Clamp : config.GetValue(ProtoFluxOverhaul.WRAP_MODE_U);
		if (texture.WrapModeU.Value != wu)
			texture.WrapModeU.Value = wu;

		var wv = clamp ? TextureWrapMode.Clamp : config.GetValue(ProtoFluxOverhaul.WRAP_MODE_V);
		if (texture.WrapModeV.Value != wv)
			texture.WrapModeV.Value = wv;

		var kom = config.GetValue(ProtoFluxOverhaul.KEEP_ORIGINAL_MIPMAPS);
		if (texture.KeepOriginalMipMaps.Value != kom)
			texture.KeepOriginalMipMaps.Value = kom;

		var mmf = config.GetValue(ProtoFluxOverhaul.MIPMAP_FILTER);
		if (texture.MipMapFilter.Value != mmf)
			texture.MipMapFilter.Value = mmf;

		var read = config.GetValue(ProtoFluxOverhaul.READABLE);
		if (texture.Readable.Value != read)
			texture.Readable.Value = read;

		var format = config.GetValue(ProtoFluxOverhaul.PREFERRED_FORMAT);
		if (texture.PreferredFormat.Value != format)
			texture.PreferredFormat.Value = format;

		var profile = config.GetValue(ProtoFluxOverhaul.PREFERRED_PROFILE);
		if (texture.PreferredProfile.Value != profile)
			texture.PreferredProfile.Value = profile;

		const float pow2Align = 0.05f;
		if (MathX.Abs(texture.PowerOfTwoAlignThreshold.Value - pow2Align) > 1e-8f)
			texture.PowerOfTwoAlignThreshold.Value = pow2Align;
	}

	internal static StaticTexture2D GetDefaultTexture(World world, string key, Uri uri, bool clamp)
	{
		return GetShared<StaticTexture2D>(world, key, tex => ApplyTextureSettings(tex, uri, clamp), () => GetUserAssetsSlot(world, "Materials"));
	}

	internal static IAssetProvider<ITexture2D> BindSharedTexture(Slot owner, AssetRef<ITexture2D> target,
		string key, Uri uri, bool clamp, string textureRole)
	{
		var world = owner?.World;
		if (world == null || world.IsDisposed || world.IsDestroyed || target == null) return null;
		var fallback = GetDefaultTexture(world, key, uri, clamp);
		var source = textureRole == null ? null : UserAssetOverrides.GetTextureReference(world, textureRole);
		NativeAssetFallback.Bind(owner, target, source, fallback);
		return target.Target;
	}

	internal static SpriteProvider GetSharedSprite(World world, string key, Uri uri, bool flipHorizontal, string textureRole)
	{
		if (world == null || world.IsDisposed) return null;

		var sprite = GetShared<SpriteProvider>(world, key, sprite =>
		{
			sprite.Rect.Value = flipHorizontal
				? new Rect(1f, 0f, -1f, 1f)
				: new Rect(0f, 0f, 1f, 1f);
			sprite.Scale.Value = 1.0f;
			sprite.FixedSize.Value = 16f;
			sprite.Borders.Value = new float4(0f, 0f, 0.0001f, 0f);
		}, () => GetUserAssetsSlot(world, "Sprites"));
		// The native reference copy follows live profile edits and source removal
		// even when this mod is no longer running on a client.
		if (sprite != null)
			BindSharedTexture(sprite.Slot, sprite.Texture, key + "_Tex", uri, clamp: true, textureRole);
		return sprite;
	}

	internal static AssetRef<Material> GetSharedWireMaterialReference(World world, bool isOutput)
	{
		if (world == null || world.IsDisposed || world.IsDestroyed) return null;
		return UserAssetOverrides.GetMaterialReference(world, isOutput ? "WireOutput" : "WireInput");
	}

	internal static FresnelMaterial GetDefaultWireMaterial(World world, bool isOutput)
	{
		if (world == null || world.IsDisposed || world.IsDestroyed) return null;
		// A renderer may currently use a custom Fresnel. Only copy vanilla settings
		// into the default so removing an override really restores vanilla behavior.
		var original = world.KeyOwner("ProtoFlux_WireMaterial") as FresnelMaterial;

		var key = WireMaterialKey + (isOutput ? "_Output" : "_Input");
		var material = GetShared<FresnelMaterial>(world, key, mat =>
		{
			mat.NearColor.Value = colorX.White;
			mat.FarColor.Value = colorX.White;
			if (original != null)
			{
				mat.Sidedness.Value = original.Sidedness.Value;
				mat.UseVertexColors.Value = original.UseVertexColors.Value;
				mat.BlendMode.Value = original.BlendMode.Value;
				mat.ZWrite.Value = original.ZWrite.Value;
				mat.NearTextureScale.Value = original.NearTextureScale.Value;
				mat.FarTextureScale.Value = original.FarTextureScale.Value;
			}
			else
			{
				mat.Sidedness.Value = Sidedness.Double;
				mat.UseVertexColors.Value = true;
				mat.BlendMode.Value = BlendMode.Alpha;
				mat.ZWrite.Value = ZWrite.On;
			}


			// Animate material offsets once per direction. Driving every wire's UVOffset
			// would regenerate and upload every procedural mesh each animation frame.
			var panner = mat.Slot.GetComponentOrAttach<Panner2D>();
			var speed = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.SCROLL_SPEED);
			speed = new float2(speed.x * (isOutput ? 1f : -1f), speed.y);
			var repeat = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.SCROLL_REPEAT);
			panner.Repeat = repeat;
			panner.Speed = speed;
			if (panner.Target != mat.FarTextureOffset && !mat.FarTextureOffset.IsDriven)
				panner.Target = mat.FarTextureOffset;

			var nearCopy = mat.Slot.GetComponentOrAttach<ValueCopy<float2>>();
			if (!mat.NearTextureOffset.IsDriven || nearCopy.Target.Target == mat.NearTextureOffset)
			{
				nearCopy.Source.Target = mat.FarTextureOffset;
				nearCopy.Target.Target = mat.NearTextureOffset;
				nearCopy.WriteBack.Value = false;
			}
		}, () => GetUserAssetsSlot(world, "Materials").AddSlot(key, false));
		if (material != null)
		{
			var uri = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WIRE_TEXTURE);
			BindSharedTexture(material.Slot, material.FarTexture, WireTextureKey, uri, clamp: false, textureRole: "Wire");
			BindSharedTexture(material.Slot, material.NearTexture, WireTextureKey, uri, clamp: false, textureRole: "Wire");
		}
		return material;
	}

	internal static void RefreshExistingAssets(World world)
	{
		if (world == null || world.IsDisposed || world.IsDestroyed) return;
		var config = ProtoFluxOverhaul.Config;

		RefreshTexture(world, WireTextureKey, config.GetValue(ProtoFluxOverhaul.WIRE_TEXTURE), false);
		RefreshTexture(world, "PFO_Tex_NodeBackground", config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE));
		RefreshTexture(world, "PFO_Tex_NodeBackground_Wrap", config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE), false);
		RefreshTexture(world, "PFO_Tex_NodeHeader", config.GetValue(ProtoFluxOverhaul.NODE_BACKGROUND_HEADER_TEXTURE));
		RefreshTexture(world, "PFO_Tex_Shading", config.GetValue(ProtoFluxOverhaul.SHADING_TEXTURE));
		RefreshTexture(world, "PFO_Tex_ShadingInverted", config.GetValue(ProtoFluxOverhaul.SHADING_INVERTED_TEXTURE));

		foreach (var direction in new[] { "Input", "Output" })
		{
			RefreshTexture(world, $"PFO_Sprite_Connector_{direction}_Tex", config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE));
			RefreshTexture(world, $"PFO_Sprite_Call_{direction}_Tex", config.GetValue(direction == "Output"
				? ProtoFluxOverhaul.CALL_CONNECTOR_OUTPUT_TEXTURE : ProtoFluxOverhaul.CALL_CONNECTOR_INPUT_TEXTURE));
			RefreshTexture(world, $"PFO_Sprite_Vector1_{direction}_Tex", config.GetValue(ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE));
			RefreshTexture(world, $"PFO_Sprite_Vector2_{direction}_Tex", config.GetValue(ProtoFluxOverhaul.VECTOR_X1_CONNECTOR_TEXTURE));
			RefreshTexture(world, $"PFO_Sprite_Vector3_{direction}_Tex", config.GetValue(ProtoFluxOverhaul.VECTOR_X2_CONNECTOR_TEXTURE));
			RefreshTexture(world, $"PFO_Sprite_Vector4_{direction}_Tex", config.GetValue(ProtoFluxOverhaul.VECTOR_X3_CONNECTOR_TEXTURE));

			if (world.KeyOwner(UserKey(world, WireMaterialKey + "_" + direction)) is FresnelMaterial material && !material.IsRemoved)
				GetDefaultWireMaterial(world, direction == "Output");
		}
	}

	private static void RefreshTexture(World world, string key, Uri uri, bool clamp = true)
	{
		if (world.KeyOwner(UserKey(world, key)) is StaticTexture2D texture && !texture.IsRemoved)
			GetDefaultTexture(world, key, uri, clamp);
	}

	private static string UserKey(World world, string key)
	{
		// Engine shared keys are world-global; scope configurable assets to their creator.
		return key + "_" + (world.LocalUser?.ReferenceID.ToString() ?? "Local");
	}

	private static T GetShared<T>(World world, string sharedKey, Action<T> onCreate, Func<Slot> getRoot) where T : Component, new()
	{
		sharedKey = UserKey(world, sharedKey);
		T created = world.GetSharedComponentOrCreate(sharedKey, onCreate, Version, replaceExisting: false, updateExisting: false, getRoot);
		if (created != null && !created.IsRemoved)
			return created;

		return world.GetSharedComponentOrCreate(sharedKey, onCreate, Version, replaceExisting: true, updateExisting: false, getRoot);
	}

	private static bool EqualsUri(Uri a, Uri b)
	{
		if (ReferenceEquals(a, b)) return true;
		if (a == null || b == null) return false;
		return a.Equals(b);
	}
}
