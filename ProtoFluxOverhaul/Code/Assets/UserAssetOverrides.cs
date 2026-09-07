using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Elements.Assets;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;

namespace ProtoFluxOverhaul;

/// <summary>Live avatar references with native fallbacks to world-owned assets.</summary>
internal static class UserAssetOverrides
{
	private const string ProfileName = "ProtoFluxOverhaul";
	private static readonly string[] MaterialRoles = { "Wire", "WireInput", "WireOutput", "Node" };
	private static readonly ConditionalWeakTable<World, ProfileState> States = new();
	private static int configVersion = 1;

	private sealed class ProfileState
	{
		public Slot UserRoot;
		public Slot Source;
		public Slot BindingsRoot;
		public long LastScan;
		public int Revision;
		public int ConfigVersion;
		public bool Updating;
		public bool Queued;
		public readonly Dictionary<string, IAssetProvider> Primaries = new();
		public readonly Dictionary<string, AssetRef<Material>> Materials = new();
		public readonly Dictionary<string, AssetRef<ITexture2D>> Textures = new();
		public readonly Dictionary<string, AssetRef<AudioClip>> Sounds = new();
	}

	internal static int GetRevision(World world) => GetState(world)?.Revision ?? 0;
	internal static void Refresh(World world) => GetState(world, refresh: true);
	internal static void InvalidateConfiguration() => configVersion++;
	internal static IAssetProvider<AudioClip> GetAudioClip(World world, string role) => GetAudioClipReference(world, role)?.Target;

	internal static AssetRef<Material> GetMaterialReference(World world, string role)
	{
		var state = GetState(world);
		bool hasOverride = HasPrimary(state, "Materials/" + role)
			|| (role == "WireInput" || role == "WireOutput") && HasPrimary(state, "Materials/Wire");
		return hasOverride && state.Materials.TryGetValue(role, out var reference) && !reference.IsRemoved ? reference : null;
	}

	internal static AssetRef<ITexture2D> GetTextureReference(World world, string role)
	{
		var state = GetState(world);
		return HasPrimary(state, "Textures/" + role) && state.Textures.TryGetValue(role, out var reference) && !reference.IsRemoved ? reference : null;
	}

	internal static AssetRef<AudioClip> GetAudioClipReference(World world, string role)
	{
		var state = GetState(world);
		return HasPrimary(state, "Sounds/" + role) && state.Sounds.TryGetValue(role, out var reference) && !reference.IsRemoved ? reference : null;
	}

	private static bool HasPrimary(ProfileState state, string key) =>
		state != null && state.Primaries.TryGetValue(key, out var provider) && provider != null && !provider.IsRemoved;

	private static ProfileState GetState(World world, bool refresh = false)
	{
		if (world == null || world.IsDisposed || world.IsDestroyed) return null;
		var state = States.GetValue(world, w =>
		{
			w.WorldDestroyed += OnWorldDestroyed;
			return new ProfileState();
		});
		if (state.Updating || state.Queued) return state;
		var userRoot = world.LocalUser?.Root?.Slot;
		if (!refresh && state.LastScan != 0 && state.UserRoot == userRoot
			&& state.Source?.IsRemoved != true && state.ConfigVersion == configVersion
			&& Stopwatch.GetElapsedTime(state.LastScan).TotalSeconds < 1) return state;

		state.LastScan = Stopwatch.GetTimestamp();
		state.Queued = true;
		world.RunSynchronously(() =>
		{
			try
			{
				if (world.IsDisposed || world.IsDestroyed) return;
				state.Updating = true;
				var root = world.LocalUser?.Root?.Slot;
				var source = root == null || root.IsRemoved ? null : root.FindChild(ProfileName)
					?? root.FindChild(ProfileName, matchSubstring: false, ignoreCase: false, maxDepth: -1);
				var primaries = FindProviders(source);
				bool changed = state.Source != source || state.UserRoot != root
					|| state.ConfigVersion != configVersion || !SameProviders(state.Primaries, primaries)
					|| state.BindingsRoot?.IsRemoved == true || HasRemovedBindings(state.Materials)
					|| HasRemovedBindings(state.Textures) || HasRemovedBindings(state.Sounds);
				if (!changed && !refresh) return;
				state.Source = source;
				state.UserRoot = root;
				state.Primaries.Clear();
				foreach (var pair in primaries) state.Primaries.Add(pair.Key, pair.Value);
				if (primaries.Count != 0 || state.BindingsRoot != null) UpdateBindings(world, state);
				state.ConfigVersion = configVersion;
				state.Revision++;
			}
			catch (System.Exception ex)
			{
				state.ConfigVersion = -1; // Retry a partially completed refresh on the next scan.
				Logger.LogError("Could not update live avatar asset references", ex, Logger.LogCategory.UI);
			}
			finally
			{
				state.Updating = false;
				state.Queued = false;
			}
		}, immediatellyIfPossible: true);
		return state;
	}

	private static void UpdateBindings(World world, ProfileState state)
	{
		if (state.BindingsRoot == null || state.BindingsRoot.IsRemoved)
			state.BindingsRoot = SharedAssets.GetUserAssetsSlot(world, "LiveOverrides");
		var root = state.BindingsRoot;
		// Texture selectors must exist before default wire materials request them.
		foreach (var entry in AssetOverrideDefinitions.Textures)
		{
			var primary = Primary<ITexture2D>(state, "Textures/" + entry.Role);
			if (primary == null && !state.Textures.ContainsKey(entry.Role)) continue;
			var fallback = SharedAssets.GetDefaultTexture(world, "PFO_FallbackTexture_" + entry.Role,
				ProtoFluxOverhaul.Config.GetValue(entry.ConfigKey), entry.Clamp);
			state.Textures[entry.Role] = NativeAssetFallback.Create(RoleSlot(root, "Textures", entry.Role), primary, fallback);
		}
		foreach (string role in MaterialRoles)
		{
			var primary = Primary<Material>(state, "Materials/" + role);
			bool directional = role == "WireInput" || role == "WireOutput";
			var genericPrimary = directional ? Primary<Material>(state, "Materials/Wire") : null;
			if (primary == null && genericPrimary == null && !state.Materials.ContainsKey(role)) continue;
			IAssetProvider<Material> fallback = role == "Node" ? world.GetDefaultUI_ZWrite()
				: SharedAssets.GetDefaultWireMaterial(world, role != "WireInput");
			var roleSlot = RoleSlot(root, "Materials", role);
			AssetRef<Material> genericWire = null;
			if (directional)
			{
				// Each direction must end at its own default when BOTH avatar roles
				// disappear, including on clients where this mod is not installed.
				var genericSlot = roleSlot.FindChild("GenericFallback") ?? roleSlot.AddSlot("GenericFallback", false);
				genericWire = NativeAssetFallback.Create(genericSlot, genericPrimary, fallback);
			}
			state.Materials[role] = NativeAssetFallback.Create(roleSlot, primary, fallback, genericWire);
		}
		foreach (var entry in AssetOverrideDefinitions.Sounds)
		{
			var primary = Primary<AudioClip>(state, "Sounds/" + entry.Role);
			if (primary == null && !state.Sounds.ContainsKey(entry.Role)) continue;
			var fallback = ProtoFluxSounds.GetDefaultAudioClip(world, entry.Role);
			state.Sounds[entry.Role] = NativeAssetFallback.Create(RoleSlot(root, "Sounds", entry.Role), primary, fallback);
		}
	}

	private static Slot RoleSlot(Slot root, string category, string role)
	{
		var group = root.FindChild(category) ?? root.AddSlot(category, false);
		return group.FindChild(role) ?? group.AddSlot(role, false);
	}

	private static IAssetProvider<T> Primary<T>(ProfileState state, string key) where T : class, IAsset =>
		state.Primaries.TryGetValue(key, out var provider) && !provider.IsRemoved ? provider as IAssetProvider<T> : null;

	private static Dictionary<string, IAssetProvider> FindProviders(Slot profile)
	{
		var result = new Dictionary<string, IAssetProvider>();
		if (profile == null || profile.IsRemoved) return result;
		var materials = profile.FindChild("Materials") ?? profile;
		foreach (string role in MaterialRoles)
		{
			var slot = materials.FindChild(role);
			if (slot == null && (role == "WireInput" || role == "WireOutput"))
				slot = materials.FindChild("PFO_WireMaterial_" + (role == "WireInput" ? "Input" : "Output"));
			var provider = slot?.GetComponent<IAssetProvider<Material>>();
			if (provider != null && !provider.IsRemoved) result.Add("Materials/" + role, provider);
		}
		foreach (var entry in AssetOverrideDefinitions.Textures)
			AddProvider<ITexture2D>(result, profile, "Textures", entry.Role);
		foreach (var entry in AssetOverrideDefinitions.Sounds)
			AddProvider<AudioClip>(result, profile, "Sounds", entry.Role);
		return result;
	}

	private static void AddProvider<T>(Dictionary<string, IAssetProvider> result, Slot profile, string category, string role) where T : class, IAsset
	{
		var provider = profile.FindChild(category)?.FindChild(role)?.GetComponent<IAssetProvider<T>>();
		if (provider != null && !provider.IsRemoved) result.Add(category + "/" + role, provider);
	}

	private static bool SameProviders(Dictionary<string, IAssetProvider> a, Dictionary<string, IAssetProvider> b)
	{
		if (a.Count != b.Count) return false;
		foreach (var pair in a)
			if (!b.TryGetValue(pair.Key, out var provider) || !ReferenceEquals(pair.Value, provider)) return false;
		return true;
	}

	private static bool HasRemovedBindings<T>(Dictionary<string, AssetRef<T>> bindings) where T : class, IAsset
	{
		foreach (var reference in bindings.Values)
			if (reference.IsRemoved) return true;
		return false;
	}

	private static void OnWorldDestroyed(World world)
	{
		world.WorldDestroyed -= OnWorldDestroyed;
		States.Remove(world);
	}

	[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnEquipped))]
	private static class RefreshOnEquipPatch
	{
		private static void Postfix(ProtoFluxTool __instance)
		{
			if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED) && !__instance.IsRemoved
				&& (__instance.ActiveHandler?.IsOwnedByLocalUser == true || __instance.IsUnderLocalUser))
				Refresh(__instance.World);
		}
	}
}
