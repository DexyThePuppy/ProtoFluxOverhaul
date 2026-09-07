using System;
using System.Collections.Generic;

using FrooxEngine;

using HarmonyLib;

using ResoniteModLoader;

using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul : ResoniteMod {
	internal const string VERSION = "1.5.2";
	public override string Name => "ProtoFluxOverhaul";
	public override string Author => "Dexy, NepuShiro";
	public override string Version => VERSION;
	public override string Link => "https://github.com/DexyThePuppy/ProtoFluxOverhaul";

	private static void ApplySharedAssetConfig(World world) {
		if (world == null || world.IsDisposed || world.IsDestroyed) return;

		try {
			SharedAssets.RefreshExistingAssets(world);
		} catch (Exception ex) {
			Logger.LogError("Error updating shared visual assets", ex, LogCategory.UI);
		}
	}

	public override void OnEngineInit() {
		Config = GetConfiguration();
		// This is an action, not a saved preference; never replay it on startup.
		if (Config.GetValue(CREATE_AVATAR_OVERRIDES))
			Config.Set(CREATE_AVATAR_OVERRIDES, false, "PFO.ResetAction");
		Config.Save(true);

		Harmony harmony = new Harmony("com.Dexy.ProtoFluxOverhaul");
		harmony.PatchAll();

		// Always log startup regardless of debug settings
		ResoniteMod.Msg("[ProtoFluxOverhaul] Mod loaded successfully - Harmony patches applied");
		Logger.LogUI("Startup", "ProtoFluxOverhaul successfully loaded and patched");

		Config.OnThisConfigurationChanged += (k) => {
			if (k.Key == CREATE_AVATAR_OVERRIDES) {
				if (!Config.GetValue(CREATE_AVATAR_OVERRIDES)) return;
				Config.Set(CREATE_AVATAR_OVERRIDES, false, "PFO.ResetAction");
				Config.Save();
				var world = Engine.Current?.WorldManager?.FocusedWorld;
				if (world == null || world.IsDisposed || world.IsDestroyed) {
					ResoniteMod.Warn("Join a world and equip an avatar before creating asset overrides.");
					return;
				}
				world.RunSynchronously(() => AvatarMaterialProfile.Create(world));
				return;
			}
			bool visualAssetsChanged = AssetConfiguration.AffectsVisualAssets(k.Key);
			if (!visualAssetsChanged && !AssetConfiguration.AffectsAudioAssets(k.Key)) return;
			if (visualAssetsChanged) SharedAssets.BumpVersion();
			UserAssetOverrides.InvalidateConfiguration();

			var worlds = new HashSet<World>();
			try {
				if (Engine.Current?.WorldManager != null) {
					foreach (var world in Engine.Current.WorldManager.Worlds) {
						if (world != null && !world.IsDisposed && !world.IsDestroyed)
							worlds.Add(world);
					}
				}
			} catch (Exception e) {
				Logger.LogError("Failed to enumerate worlds for config update", e, LogCategory.UI);
			}

			foreach (var world in worlds) {
				if (world == null || world.IsDisposed || world.IsDestroyed) continue;
				world.RunSynchronously(() => {
					if (world.IsDisposed || world.IsDestroyed) return;
					if (visualAssetsChanged) ApplySharedAssetConfig(world);
					UserAssetOverrides.Refresh(world);
				});
			}
		};
	}
}
