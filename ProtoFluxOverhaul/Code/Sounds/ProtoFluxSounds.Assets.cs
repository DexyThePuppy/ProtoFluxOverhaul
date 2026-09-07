using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FrooxEngine;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	public static partial class ProtoFluxSounds
	{
		private sealed class WorldSoundAssets
		{
			internal User User;
			internal string UserName;
			internal Slot Root;
			internal readonly Dictionary<string, StaticAudioClip> Defaults = new();
			internal readonly Dictionary<string, AssetLoader<AudioClip>> Loaders = new();
		}

		// A completed world can be collected without a global dictionary retaining it.
		private static readonly ConditionalWeakTable<World, WorldSoundAssets> worldAssets = new();

		private static WorldSoundAssets GetWorldAssets(World world)
		{
			var assets = worldAssets.GetValue(world, static _ => new WorldSoundAssets());
			var user = world.LocalUser;
			var userName = user?.UserName;
			if (assets.User != user || assets.UserName != userName || assets.Root?.IsRemoved == true)
			{
				assets.User = user;
				assets.UserName = userName;
				assets.Root = null;
				assets.Defaults.Clear();
				assets.Loaders.Clear();
			}
			return assets;
		}

		public static Uri GetSoundUrl(string soundName)
		{
			var config = ProtoFluxOverhaul.Config;
			return soundName switch
			{
				"Connect" => config.GetValue(ProtoFluxOverhaul.CONNECT_SOUND),
				"Delete" => config.GetValue(ProtoFluxOverhaul.DELETE_SOUND),
				"Grab" => config.GetValue(ProtoFluxOverhaul.GRAB_SOUND),
				"NodeCreate" => config.GetValue(ProtoFluxOverhaul.NODE_CREATE_SOUND),
				"NodeGrab" => config.GetValue(ProtoFluxOverhaul.NODE_GRAB_SOUND),
				"WireDrag" => config.GetValue(ProtoFluxOverhaul.WIRE_DRAG_SOUND),
				_ => throw new ArgumentException($"Unknown sound name: {soundName}", nameof(soundName))
			};
		}

		/// <summary>Shared slot under __TEMP for clips and local one-shots.</summary>
		public static Slot GetSoundsSlot(World world)
		{
			if (world == null || world.IsDisposed || world.IsDestroyed) return null;
			var assets = GetWorldAssets(world);
			return assets.Root ??= SharedAssets.GetUserAssetsSlot(world, "Sounds");
		}

		public static IAssetProvider<AudioClip> GetSharedAudioClip(World world, string soundName)
		{
			if (world == null || world.IsDisposed || world.IsDestroyed) return null;
			var assets = GetWorldAssets(world);
			if (!assets.Loaders.TryGetValue(soundName, out var loader) || loader.IsRemoved)
			{
				var soundsSlot = GetSoundsSlot(world);
				if (soundsSlot == null) return null;
				var clipSlot = soundsSlot.FindChild(soundName) ?? soundsSlot.AddSlot(soundName, false);
				// Keep asset demand in our cache, never in the source avatar profile.
				loader = clipSlot.GetComponentOrAttach<AssetLoader<AudioClip>>();
				assets.Loaders[soundName] = loader;
			}
			BindAudioClip(world, soundName, loader.Slot, loader.Asset);
			return loader.Asset.Target ?? UserAssetOverrides.GetAudioClip(world, soundName)
				?? GetDefaultAudioClip(world, soundName);
		}

		/// <summary>Gets only the configured default, without consulting avatar overrides.</summary>
		public static StaticAudioClip GetDefaultAudioClip(World world, string soundName)
		{
			if (world == null || world.IsDisposed || world.IsDestroyed) return null;
			var soundUrl = GetSoundUrl(soundName);
			if (soundUrl == null) return null;
			var assets = GetWorldAssets(world);
			if (assets.Defaults.TryGetValue(soundName, out var clip) && !clip.IsRemoved && clip.URL.Value == soundUrl)
				return clip;

			try
			{
				var soundsSlot = GetSoundsSlot(world);
				if (soundsSlot == null) return null;
				var clipSlot = soundsSlot.FindChild(soundName) ?? soundsSlot.AddSlot(soundName, false);
				clip = clipSlot.AttachAudioClip(soundUrl, getExisting: true);
				if (clip == null)
				{
					Logger.LogError($"Failed to create audio clip for {soundName} with URL {soundUrl}", null, LogCategory.Audio);
					return null;
				}

				assets.Defaults[soundName] = clip;
				Logger.LogAudio("Cache", $"Created shared audio clip for {soundName} in {clipSlot.Name}");
				return clip;
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error creating shared audio clip for {soundName}", ex, LogCategory.Audio);
				return null;
			}
		}

		/// <summary>Drives a cache/player reference from the native avatar/default selector.</summary>
		internal static bool BindAudioClip(World world, string soundName, Slot owner, AssetRef<AudioClip> target)
		{
			if (world == null || world.IsDisposed || world.IsDestroyed || owner == null || owner.IsRemoved
				|| owner.World != world || target == null) return false;
			var fallback = GetDefaultAudioClip(world, soundName);
			var source = UserAssetOverrides.GetAudioClipReference(world, soundName);
			return NativeAssetFallback.Bind(owner, target, source, fallback);
		}
	}
}
