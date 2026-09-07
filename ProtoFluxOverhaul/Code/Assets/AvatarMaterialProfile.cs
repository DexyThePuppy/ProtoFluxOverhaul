using System;
using System.Collections.Generic;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.UIX;
using Renderite.Shared;
using ResoniteModLoader;

namespace ProtoFluxOverhaul;

/// <summary>Creates missing asset overrides on the equipped avatar, on the world thread.</summary>
internal static class AvatarMaterialProfile
{
	private const string ProfileName = "ProtoFluxOverhaul";
	private static readonly string[] Roles = { "Wire", "WireInput", "WireOutput", "Node" };

	internal static void Create(World world)
	{
		if (world == null || world.IsDisposed || world.IsDestroyed)
		{
			ResoniteMod.Warn("[ProtoFluxOverhaul] Open a world and equip an avatar before creating overrides.");
			return;
		}

		var userRoot = world.LocalUser?.Root;
		var avatar = userRoot?.GetRegisteredComponent<AvatarObjectSlot>(slot =>
			slot.Node.Value == BodyNode.Root && slot.Equipped.Target != null)?.Equipped.Target.Slot;
		if (avatar == null || avatar.IsRemoved || userRoot.Slot.IsRemoved || !avatar.IsChildOf(userRoot.Slot))
		{
			ResoniteMod.Warn("[ProtoFluxOverhaul] No equipped avatar was found. Equip an avatar and try Create Avatar Overrides again.");
			return;
		}

		var profile = avatar.FindChild(ProfileName);
		var materialRoot = profile?.FindChild("Materials");
		bool directRoles = false;
		if (profile != null && materialRoot == null)
		{
			foreach (string role in Roles)
				if (FindRole(profile, role) != null) directRoles = true;
			if (directRoles) materialRoot = profile;
		}

		var textureRoot = profile?.FindChild("Textures");
		var soundRoot = profile?.FindChild("Sounds");
		var missing = new List<string>();
		var missingTextures = new List<string>();
		var missingSounds = new List<string>();
		int inheritedDirections = 0;
		bool hasGenericWire = HasAsset<Material>(FindRole(materialRoot, "Wire"));
		foreach (string role in Roles)
		{
			if (HasAsset<Material>(FindRole(materialRoot, role))) continue;
			// Keep the authored generic Wire active instead of shadowing it with
			// newly generated direction-specific defaults.
			if (hasGenericWire && (role == "WireInput" || role == "WireOutput"))
			{
				inheritedDirections++;
				continue;
			}
			missing.Add(role);
		}
		foreach (var definition in AssetOverrideDefinitions.Textures)
			if (!HasAsset<ITexture2D>(textureRoot?.FindChild(definition.Role)))
				missingTextures.Add(definition.Role);
		foreach (var definition in AssetOverrideDefinitions.Sounds)
			if (!HasAsset<AudioClip>(soundRoot?.FindChild(definition.Role)))
				missingSounds.Add(definition.Role);

		if (missing.Count + missingTextures.Count + missingSounds.Count == 0)
		{
			UserAssetOverrides.Refresh(world);
			ResoniteMod.Msg("[ProtoFluxOverhaul] All avatar material, texture, and sound overrides are already available. Existing assets were preserved." + InheritedWireNotice(inheritedDirections));
			ReportWireTextureLinks(materialRoot, textureRoot);
			ReportShadowing(userRoot.Slot, profile);
			return;
		}

		Slot staging = null;
		Slot importContainer = null;
		Slot importedDependencies = null;
		var addedRoles = new List<Slot>();
		var addedComponents = new List<Component>();
		int addedMaterials = 0;
		int addedTextures = 0;
		int addedSounds = 0;
		bool completed = false;
		try
		{
			staging = world.RootSlot.AddSlot("PFO_AvatarAssetProfile_Staging", false);
			var stagedMaterials = staging.AddSlot("Materials");
			var stagedTextures = staging.AddSlot("Textures");
			var stagedSounds = staging.AddSlot("Sounds");
			foreach (var definition in AssetOverrideDefinitions.Textures)
			{
				if (!missingTextures.Contains(definition.Role)) continue;
				var texture = stagedTextures.AddSlot(definition.Role).AttachComponent<StaticTexture2D>();
				SharedAssets.ApplyTextureSettings(texture, ProtoFluxOverhaul.Config.GetValue(definition.ConfigKey), definition.Clamp);
			}
			foreach (var definition in AssetOverrideDefinitions.Sounds)
			{
				if (!missingSounds.Contains(definition.Role)) continue;
				var clip = stagedSounds.AddSlot(definition.Role).AttachComponent<StaticAudioClip>();
				clip.URL.Value = ProtoFluxOverhaul.Config.GetValue(definition.ConfigKey);
			}
			var stagedWireTexture = stagedTextures.FindChild("Wire")?.GetComponent<IAssetProvider<ITexture2D>>()
				?? textureRoot?.FindChild("Wire")?.GetComponent<IAssetProvider<ITexture2D>>();
			foreach (string role in missing)
			{
				var roleSlot = stagedMaterials.AddSlot(role);
				if (role == "Node")
					StageNode(world, roleSlot);
				else
					StageWire(world, roleSlot, role != "WireInput", stagedWireTexture);
			}

			// Collect referenced textures without copying the engine's shared asset slots.
			var graph = staging.SaveObject(DependencyHandling.CollectAssets, saveNonPersistent: true);
			if (world.IsDisposed || world.IsDestroyed || avatar.IsRemoved || userRoot.IsRemoved
				|| userRoot.Slot.IsRemoved || !avatar.IsChildOf(userRoot.Slot))
				throw new InvalidOperationException("The equipped avatar is no longer available.");

			profile ??= avatar.AddSlot(ProfileName);
			importContainer = profile.AddSlot("PFO_AssetImport_" + Guid.NewGuid().ToString("N"), false);
			var importedProfile = importContainer.AddSlot("Assets", false);
			importedDependencies = importContainer.AddSlot("Dependencies");
			importedProfile.LoadObject(graph.Root, record: null, assetsRoot: importedDependencies);
			importedProfile.Name = "Assets";
			var importedMaterials = importedProfile.FindChild("Materials");
			var importedTextures = importedProfile.FindChild("Textures");
			var importedSounds = importedProfile.FindChild("Sounds");

			// Validate the entire import before moving any roles into the existing profile.
			ValidateRoles<Material>(importedMaterials, missing);
			ValidateRoles<ITexture2D>(importedTextures, missingTextures);
			ValidateRoles<AudioClip>(importedSounds, missingSounds);

			materialRoot ??= directRoles ? profile : profile.FindChild("Materials") ?? profile.AddSlot("Materials");
			textureRoot ??= profile.FindChild("Textures") ?? profile.AddSlot("Textures");
			soundRoot ??= profile.FindChild("Sounds") ?? profile.AddSlot("Sounds");
			var dependencyRoot = profile.FindChild("Dependencies") ?? profile.AddSlot("Dependencies");
			importedDependencies.Name = "GeneratedAssets_" + Guid.NewGuid().ToString("N");
			importedDependencies.PersistentSelf = true;
			importedDependencies.SetParent(dependencyRoot, keepGlobalTransform: false);
			if (importedDependencies.Parent != dependencyRoot)
				throw new InvalidOperationException("The avatar's dependencies folder could not accept the copied assets.");

			foreach (string role in missingTextures)
			{
				var existingRole = textureRoot.FindChild(role);
				if (HasAsset<ITexture2D>(existingRole)) continue;
				InstallRole<ITexture2D>(textureRoot, existingRole, importedTextures.FindChild(role), addedRoles, addedComponents);
				addedTextures++;
			}
			foreach (string role in missingSounds)
			{
				var existingRole = soundRoot.FindChild(role);
				if (HasAsset<AudioClip>(existingRole)) continue;
				InstallRole<AudioClip>(soundRoot, existingRole, importedSounds.FindChild(role), addedRoles, addedComponents);
				addedSounds++;
			}
			var wireTexture = textureRoot.FindChild("Wire")?.GetComponent<IAssetProvider<ITexture2D>>();
			if (wireTexture == null || wireTexture.IsRemoved)
				throw new InvalidOperationException("The avatar's Wire texture is unavailable.");
			foreach (string role in missing)
			{
				var existingRole = FindRole(materialRoot, role);
				if (HasAsset<Material>(existingRole)) continue;
				var material = InstallRole<Material>(materialRoot, existingRole, importedMaterials.FindChild(role), addedRoles, addedComponents);
				if (role != "Node")
				{
					// Empty role slots use DuplicateComponents, so references to staged
					// texture providers must be rebound to the finished avatar role.
					var wire = material as FresnelMaterial
						?? throw new InvalidOperationException($"The generated {role} material has an unexpected type.");
					wire.NearTexture.Target = wireTexture;
					wire.FarTexture.Target = wireTexture;
				}
				addedMaterials++;
			}

			completed = true;
		}
		catch (Exception ex)
		{
			ResoniteMod.Warn($"[ProtoFluxOverhaul] Could not create avatar overrides: {ex.Message}");
			Logger.LogError("Error creating avatar asset overrides", ex, Logger.LogCategory.UI);
		}
		finally
		{
			if (!completed)
			{
				foreach (var component in addedComponents)
					if (!component.IsRemoved) component.Destroy();
				foreach (var roleSlot in addedRoles)
					if (!roleSlot.IsRemoved) roleSlot.Destroy();
				if (importedDependencies != null && !importedDependencies.IsRemoved)
					importedDependencies.Destroy();
			}
			if (importContainer != null && !importContainer.IsRemoved) importContainer.Destroy();
			if (staging != null && !staging.IsRemoved) staging.Destroy();
		}

		if (completed && !world.IsDisposed && !world.IsDestroyed && !avatar.IsRemoved)
		{
			// Import only the finished profile: temporary containers have been removed.
			UserAssetOverrides.Refresh(world);
			ResoniteMod.Msg($"[ProtoFluxOverhaul] Added {addedMaterials} material, {addedTextures} texture, and {addedSounds} sound overrides to avatar '{avatar.Name}' under {ProfileName}. Existing overrides were preserved. Save the avatar to keep this setup." + InheritedWireNotice(inheritedDirections));
			ReportWireTextureLinks(materialRoot, textureRoot);
			ReportShadowing(userRoot.Slot, profile);
		}
	}

	private static void StageNode(World world, Slot target)
	{
		var material = world.GetDefaultUI_ZWrite() as UI_UnlitMaterial;
		if (material == null || material.IsRemoved)
			throw new InvalidOperationException("The default node background material is unavailable.");

		// ProtoFlux builds its first background Image with zwrite:true. Copy that
		// complete native template, including depth offsets, stencil and queue settings.
		// UIX supplies the sprite texture and Image tint when mapping this material.
		target.DuplicateComponents(new List<Component> { material }, breakExternalReferences: false);
	}

	private static void StageWire(World world, Slot target, bool isOutput, IAssetProvider<ITexture2D> wireTexture)
	{
		var material = SharedAssets.GetDefaultWireMaterial(world, isOutput);
		if (material == null || material.IsRemoved)
			throw new InvalidOperationException("The default wire material is unavailable.");

		// Copy the material and its drivers in one operation so their internal links
		// point to the copies. Keep external textures until CollectAssets imports them.
		var components = new List<Component> { material };
		foreach (var panner in material.Slot.GetComponents<Panner2D>())
			if (panner.Target == material.FarTextureOffset) components.Add(panner);
		foreach (var copy in material.Slot.GetComponents<ValueCopy<float2>>())
			if (copy.Source.Target == material.FarTextureOffset && copy.Target.Target == material.NearTextureOffset)
				components.Add(copy);
		target.DuplicateComponents(components, breakExternalReferences: false);
		var stagedMaterial = target.GetComponent<FresnelMaterial>();
		stagedMaterial.NearTexture.Target = wireTexture;
		stagedMaterial.FarTexture.Target = wireTexture;
	}

	private static void ValidateRoles<T>(Slot root, IEnumerable<string> roles) where T : class, IAsset
	{
		foreach (string role in roles)
			if (!HasAsset<T>(root?.FindChild(role)))
				throw new InvalidOperationException($"The {role} {typeof(T).Name} override could not be copied.");
	}

	private static IAssetProvider<T> InstallRole<T>(Slot root, Slot existingRole, Slot importedRole,
		List<Slot> addedRoles, List<Component> addedComponents) where T : class, IAsset
	{
		if (existingRole == null)
		{
			importedRole.PersistentSelf = true;
			importedRole.SetParent(root, keepGlobalTransform: false);
			if (importedRole.Parent != root)
				throw new InvalidOperationException($"The avatar's {root.Name} folder could not accept the {importedRole.Name} override.");
			addedRoles.Add(importedRole);
			existingRole = importedRole;
		}
		else
		{
			// Preserve the empty role's slot, children, and unrelated components.
			// Duplicate linked drivers and their provider together, and track even
			// partially created components so a failed operation can roll them back.
			var before = new HashSet<Component>(existingRole.GetComponents<Component>());
			try
			{
				existingRole.DuplicateComponents(new List<Component>(importedRole.GetComponents<Component>()), breakExternalReferences: false);
			}
			finally
			{
				foreach (var component in existingRole.GetComponents<Component>())
					if (!before.Contains(component)) addedComponents.Add(component);
			}
		}
		var provider = existingRole.GetComponent<IAssetProvider<T>>();
		if (provider == null || provider.IsRemoved)
			throw new InvalidOperationException($"The {existingRole.Name} override could not be installed.");
		return provider;
	}

	private static Slot FindRole(Slot root, string role)
	{
		var roleSlot = root?.FindChild(role);
		if (roleSlot == null && (role == "WireInput" || role == "WireOutput"))
			roleSlot = root?.FindChild("PFO_WireMaterial_" + (role == "WireInput" ? "Input" : "Output"));
		return roleSlot;
	}

	private static bool HasAsset<T>(Slot role) where T : class, IAsset
	{
		var provider = role?.GetComponent<IAssetProvider<T>>();
		return provider != null && !provider.IsRemoved;
	}

	private static void ReportWireTextureLinks(Slot materialRoot, Slot textureRoot)
	{
		var wireTexture = textureRoot?.FindChild("Wire")?.GetComponent<IAssetProvider<ITexture2D>>();
		if (wireTexture == null) return;
		foreach (string role in Roles)
		{
			if (role == "Node") continue;
			var material = FindRole(materialRoot, role)?.GetComponent<IAssetProvider<Material>>();
			if (material == null) continue;
			if (material is FresnelMaterial fresnel
				&& fresnel.NearTexture.Target == wireTexture && fresnel.FarTexture.Target == wireTexture) continue;
			ResoniteMod.Msg("[ProtoFluxOverhaul] Existing wire materials keep their current texture references. To use the profile's Textures/Wire override, link their NearTexture and FarTexture fields (or the equivalent texture fields for a custom shader) to that provider.");
			return;
		}
	}

	private static string InheritedWireNotice(int count)
	{
		return count == 0 ? "" : $" {count} wire direction(s) continue using the existing generic Wire override.";
	}

	private static void ReportShadowing(Slot userRoot, Slot avatarProfile)
	{
		var directProfile = userRoot.FindChild(ProfileName);
		if (directProfile != null && directProfile != avatarProfile)
			ResoniteMod.Warn("[ProtoFluxOverhaul] A ProtoFluxOverhaul folder directly under the user root takes priority over this avatar profile. That existing folder was left unchanged.");
	}
}
