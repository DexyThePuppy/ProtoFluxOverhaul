using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	/// <summary>Local interaction sounds with shared assets and live avatar overrides.</summary>
	public static partial class ProtoFluxSounds
	{
		public static readonly string[] SOUND_NAMES = { "Connect", "Delete", "Grab", "NodeCreate", "NodeGrab", "WireDrag" };

		public static void Initialize(World world)
		{
			if (world == null || world.IsDisposed || world.IsDestroyed) return;
			world.RunSynchronously(() =>
			{
				if (world.IsDisposed || world.IsDestroyed || world.LocalUser == null) return;
				try
				{
					// Preload on equip; first playback still waits if loading is ongoing.
					foreach (string soundName in SOUND_NAMES)
						if (IsSoundEnabled(soundName)) GetSharedAudioClip(world, soundName);
				}
				catch (Exception ex)
				{
					Logger.LogError("Error preloading interaction sounds", ex, LogCategory.Audio);
				}
			}, immediatellyIfPossible: true);
		}

		[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnEquipped))]
		private static class PreloadOnEquipPatch
		{
			private static void Postfix(ProtoFluxTool __instance)
			{
				if (!__instance.IsRemoved
					&& (__instance.ActiveHandler?.IsOwnedByLocalUser == true || __instance.IsUnderLocalUser))
					Initialize(__instance.World);
			}
		}

		public static void PlaySoundAndCleanup(World world, float3 position, string soundName)
		{
			if (world == null || world.IsDisposed || world.IsDestroyed || !IsSoundEnabled(soundName)) return;
			world.RunSynchronously(() => PlaySoundInWorld(world, position, soundName), immediatellyIfPossible: true);
		}

		private static bool IsSoundEnabled(string soundName)
		{
			var config = ProtoFluxOverhaul.Config;
			if (!config.GetValue(ProtoFluxOverhaul.ENABLED)) return false;
			if (soundName == "WireDrag" && !config.GetValue(ProtoFluxOverhaul.WIRE_DRAG_SOUNDS)) return false;
			return soundName == "NodeCreate" || soundName == "NodeGrab"
				? config.GetValue(ProtoFluxOverhaul.NODE_SOUNDS)
				: config.GetValue(ProtoFluxOverhaul.WIRE_SOUNDS);
		}

		public static void OnWireConnected(World world, float3 position) => PlaySoundAndCleanup(world, position, "Connect");
		public static void OnWireDeleted(World world, float3 position) => PlaySoundAndCleanup(world, position, "Delete");
		public static void OnWireGrabbed(World world, float3 position) => PlaySoundAndCleanup(world, position, "Grab");
		public static void OnNodeCreated(World world, float3 position) => PlaySoundAndCleanup(world, position, "NodeCreate");
		public static void OnNodeGrabbed(World world, float3 position) => PlaySoundAndCleanup(world, position, "NodeGrab");
	}
}
