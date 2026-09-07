using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	public static partial class ProtoFluxSounds
	{
		private static void CreateOneShot(World world, float3 position, string soundName)
		{
			if (world.IsDisposed || world.IsDestroyed || !IsSoundEnabled(soundName))
				return;

			Slot soundSlot = null;
			try
			{
				var parentUnder = GetSoundsSlot(world);
				if (parentUnder == null) return;
				soundSlot = parentUnder.AddLocalSlot("OneShotAudio");
				soundSlot.GlobalPosition = position;
				var player = soundSlot.AttachComponent<AudioClipPlayer>();
				BindAudioClip(world, soundName, soundSlot, player.Clip);
				player.Loop = false;
				player.Speed = 1f;
				player.Stop();
				var audioOutput = soundSlot.AttachComponent<AudioOutput>();
				audioOutput.EnabledField.OverrideForUser(world.LocalUser, value: true).Default.Value = false;
				audioOutput.Source.Target = player;
				audioOutput.AudioTypeGroup.Value = AudioTypeGroup.SoundEffect;
				audioOutput.Volume.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.AUDIO_VOLUME);
				audioOutput.Spatialize.Value = true;
				audioOutput.SpatialBlend.Value = 1f;
				audioOutput.Global.Value = false;
				audioOutput.DistanceSpace.Value = AudioDistanceSpace.Local;
				audioOutput.MinDistance.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIN_DISTANCE);
				audioOutput.MaxDistance.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MAX_DISTANCE);

				// PlayOneShot starts its clock before the native output is registered. A
				// short first sound can finish during initial setup. Keep this request
				// stopped until both the asset and output are ready, then start at zero.
				DateTime configuredAt = DateTime.UtcNow;
				world.Coroutines.StartTask(() => StartWhenReady(world, soundName, player, audioOutput, configuredAt));
			}
			catch (Exception ex)
			{
				if (soundSlot != null && !soundSlot.IsRemoved)
					soundSlot.Destroy();
				Logger.LogError($"Error playing sound {soundName}", ex, Logger.LogCategory.Audio);
			}
		}

		private static async Task StartWhenReady(World world, string soundName,
			AudioClipPlayer player, AudioOutput output, DateTime configuredAt)
		{
			const double timeoutSeconds = 15;
			long started = Stopwatch.GetTimestamp();
			bool playbackStarted = false;
			IAssetProvider<AudioClip> observedClip = null;
			bool readyOnPreviousUpdate = false;
			bool IsValid() => !world.IsDisposed && !world.IsDestroyed && !player.IsRemoved && !output.IsRemoved
				&& IsSoundEnabled(soundName);
			// IsRegistered alone only means the native wrapper exists. Wait until the
			// audio renderer has also integrated the batch containing its source/setup.
			bool IsReady()
			{
				var clip = player.Clip.Target;
				return clip != null && !clip.IsRemoved && clip.IsAssetAvailable && player.ClipLength > 0
					&& output.IsRegistered && world.Audio.Space.LatestIntegratedChangeTimestamp > configuredAt;
			}
			Action refreshBinding = () =>
			{
				if (IsValid()) BindAudioClip(world, soundName, player.Slot, player.Clip);
			};

			try
			{
				while (IsValid() && Stopwatch.GetElapsedTime(started).TotalSeconds < timeoutSeconds)
				{
					world.RunSynchronously(refreshBinding, immediatellyIfPossible: true);
					// Native selectors can temporarily be null or switch to the default
					// when an avatar disappears. Keep the request and follow the live ref.
					if (!ReferenceEquals(observedClip, player.Clip.Target))
					{
						observedClip = player.Clip.Target;
						configuredAt = DateTime.UtcNow;
						readyOnPreviousUpdate = false;
					}
					bool ready = IsReady();
					if (ready && readyOnPreviousUpdate) break;
					// Give AudioClipPlayer.OnChanges a full pass to refresh the cached
					// SyncPlayback length after this provider first becomes available.
					readyOnPreviousUpdate = ready;
					await new NextUpdate();
				}

				if (!IsValid()) return;
				if (!IsReady() || !readyOnPreviousUpdate)
				{
					var clip = player.Clip.Target;
					var source = clip is StaticAudioClip staticClip ? staticClip.URL.Value?.ToString() : clip?.GetType().Name ?? "No selected provider";
					Logger.LogError($"Timed out preparing {soundName} after {timeoutSeconds}s: {source} "
						+ $"(clip ready: {player.Clip.IsAssetAvailable}, output registered: {output.IsRegistered}, asset references: {clip?.AssetReferenceCount ?? 0})",
						null, LogCategory.Audio);
					return;
				}

				world.RunSynchronously(() =>
				{
					try
					{
						if (!IsValid())
						{
							if (!player.IsRemoved) player.Slot.Destroy();
							return;
						}
						if (!IsReady() || !ReferenceEquals(observedClip, player.Clip.Target))
						{
							world.Coroutines.StartTask(() => StartWhenReady(world, soundName, player, output, DateTime.UtcNow));
							return;
						}
						// Do not attach the cleaner while loading: its grace period would
						// otherwise destroy the stopped player before its first playback.
						var cleaner = player.Slot.AttachComponent<StoppedPlayableCleaner>();
						cleaner.Playable.Target = player;
						cleaner.GracePeriod.Value = 0.5f;
						player.Play();
						Logger.LogAudio("Playback", $"Started {soundName} after {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}ms preparation");
					}
					catch (Exception ex)
					{
						if (!player.IsRemoved) player.Slot.Destroy();
						Logger.LogError($"Error starting sound {soundName}", ex, LogCategory.Audio);
					}
				}, immediatellyIfPossible: true);
				playbackStarted = true;
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error preparing sound {soundName}", ex, LogCategory.Audio);
			}
			finally
			{
				if (!playbackStarted && !world.IsDisposed && !world.IsDestroyed)
					world.RunSynchronously(() =>
					{
						if (!player.IsRemoved) player.Slot.Destroy();
					}, immediatellyIfPossible: true);
			}
		}

		private static void PlaySoundInWorld(World world, float3 position, string soundName)
		{
			if (world.IsDisposed || world.IsDestroyed || !IsSoundEnabled(soundName)) return;

			if (Array.IndexOf(SOUND_NAMES, soundName) < 0)
			{
				Logger.DebugLog($"Unknown sound name: {soundName}", Logger.LogLevel.Error, Logger.LogCategory.Audio);
				return;
			}

			try
			{
				GetSharedAudioClip(world, soundName);
				// A newly bound native selector may not have evaluated yet. Create the
				// request even with a transiently null target; readiness handles it.
				CreateOneShot(world, position, soundName);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error playing sound {soundName}", ex, Logger.LogCategory.Audio);
			}
		}

	}
}
