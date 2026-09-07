using System;
using System.Runtime.CompilerServices;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;

namespace ProtoFluxOverhaul;

/// <summary>Local looping feedback while the length of a dragged wire changes.</summary>
internal static class WireDragSound
{
	private const float MotionDeadzone = 0.001f;
	private const float MotionRateForMaximum = 0.5f;
	private const float MinimumMovingPlaybackSpeed = 0.5f;
	private const float MaximumPlaybackSpeed = 2f;
	private const float SmoothingSeconds = 0.06f;
	private static readonly ConditionalWeakTable<ProtoFluxTool, DragState> states = new();

	private sealed class DragState
	{
		public ProtoFluxTool Tool;
		public World World;
		public Slot Wire;
		public Slot PendingWire;
		public Slot SoundSlot;
		public AudioClipPlayer Player;
		public AudioOutput Output;
		public IAssetProvider<AudioClip> Clip;
		public DateTime OutputConfiguredAt;
		public float PreviousLength;
		public float SmoothedSpeed;
		private Action updateAction;

		public void ScheduleUpdate(Slot wire)
		{
			PendingWire = wire;
			// Reuse the dispatch delegate for the lifetime of this drag.
			World.RunSynchronously(updateAction ??= RunUpdate, immediatellyIfPossible: true);
		}

		private void RunUpdate() => UpdateSafely(Tool, PendingWire);

		public void OnWorldDestroyed(World world)
		{
			// The world destroys its slots; only release our bookkeeping during teardown.
			states.Remove(Tool);
			world.WorldDestroyed -= OnWorldDestroyed;
		}
	}

	private static bool IsEnabled(ProtoFluxTool tool) =>
		ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)
		&& ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WIRE_SOUNDS)
		&& ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.WIRE_DRAG_SOUNDS)
		&& (tool.ActiveHandler?.IsOwnedByLocalUser == true || tool.IsUnderLocalUser);

	private static void Stop(ProtoFluxTool tool)
	{
		if (tool == null || !states.TryGetValue(tool, out var state)) return;
		states.Remove(tool);
		state.World.WorldDestroyed -= state.OnWorldDestroyed;
		if (!state.World.IsDisposed && !state.World.IsDestroyed)
		{
			state.World.RunSynchronously(() =>
			{
				if (state.SoundSlot != null && !state.SoundSlot.IsRemoved)
					state.SoundSlot.Destroy();
			}, immediatellyIfPossible: true);
		}
	}

	private static DragState Start(ProtoFluxTool tool, Slot wire, float length)
	{
		var world = tool.World;
		ProtoFluxSounds.GetSharedAudioClip(world, "WireDrag");
		var soundsSlot = ProtoFluxSounds.GetSoundsSlot(world);
		if (soundsSlot == null) return null;

		var soundSlot = soundsSlot.AddLocalSlot("WireDragAudio");
		var state = new DragState
		{
			Tool = tool,
			World = world,
			Wire = wire,
			PendingWire = wire,
			SoundSlot = soundSlot,
			PreviousLength = length
		};
		states.Add(tool, state);
		world.WorldDestroyed += state.OnWorldDestroyed;

		state.Output = soundSlot.AttachComponent<AudioOutput>();
		state.Output.EnabledField.OverrideForUser(world.LocalUser, value: true).Default.Value = false;
		state.Output.AudioTypeGroup.Value = AudioTypeGroup.SoundEffect;
		state.Output.DistanceSpace.Value = AudioDistanceSpace.Local;
		state.Output.SpatialBlend.Value = 1f;
		state.Output.Spatialize.Value = true;
		state.Output.Global.Value = false;
		state.Output.DopplerLevel.Value = 0f;
		state.Output.Volume.Value = 0f;
		state.Player = soundSlot.AttachComponent<AudioClipPlayer>();
		// An AssetRef consumer must exist before waiting for the clip to become available.
		ProtoFluxSounds.BindAudioClip(world, "WireDrag", soundSlot, state.Player.Clip);
		state.Clip = state.Player.Clip.Target;
		state.Player.Loop = true;
		state.Player.Speed = 0f;
		state.Output.Source.Target = state.Player;
		state.OutputConfiguredAt = DateTime.UtcNow;
		Logger.LogAudio("WireDrag", "Started local wire drag feedback");
		return state;
	}

	private static void Update(ProtoFluxTool tool, Slot wire)
	{
		if (tool.IsRemoved || !IsEnabled(tool) || wire == null || wire.IsRemoved
			|| tool.WirePoint.Target == null)
		{
			Stop(tool);
			return;
		}

		var position = tool.WirePoint.Target.GlobalPosition;
		float length = (position - wire.GlobalPosition).Magnitude;
		if (!float.IsFinite(length))
		{
			Stop(tool);
			return;
		}

		states.TryGetValue(tool, out var state);
		if (state != null && (state.Wire != wire || state.SoundSlot.IsRemoved))
		{
			Stop(tool);
			state = null;
		}
		state ??= Start(tool, wire, length);
		if (state == null) return;

		state.SoundSlot.GlobalPosition = position;
		ProtoFluxSounds.BindAudioClip(tool.World, "WireDrag", state.SoundSlot, state.Player.Clip);
		var currentClip = state.Player.Clip.Target;
		if (!ReferenceEquals(state.Clip, currentClip))
		{
			// Follow the actual driven reference, including transient nulls while the
			// native selector switches from an avatar provider to its default.
			state.Player.Stop();
			state.Player.Speed = 0f;
			state.Output.Volume.Value = 0f;
			state.Clip = currentClip;
			state.SmoothedSpeed = 0f;
			state.PreviousLength = length;
			state.OutputConfiguredAt = DateTime.UtcNow;
		}

		float delta = tool.World.Time.RawDelta;
		float userScale = MathF.Max(0.0001f, MathF.Abs(tool.LocalUser.Root?.GlobalScale ?? 1f));
		float change = MathF.Abs(length - state.PreviousLength) / userScale;
		state.PreviousLength = length;
		float speed = CalculatePlaybackSpeed(change, delta, state.SmoothedSpeed);
		if (!state.Player.Clip.IsAssetAvailable || state.Player.ClipLength <= 0 || !state.Output.IsRegistered
			|| tool.World.Audio.Space.LatestIntegratedChangeTimestamp <= state.OutputConfiguredAt)
			speed = 0f;
		state.SmoothedSpeed = speed;

		// Zero rate alone can repeatedly output one sample. Pause and mute too, while
		// retaining the playhead so movement resumes the loop rather than restarting it.
		if (speed == 0f)
		{
			if (state.Player.IsPlaying) state.Player.Pause();
			state.Player.Speed = 0f;
			state.Output.Volume.Value = 0f;
		}
		else
		{
			state.Player.Speed = speed;
			if (!state.Player.IsPlaying) state.Player.Resume();
			state.Output.Volume.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.AUDIO_VOLUME);
		}
		state.Output.MinDistance.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MIN_DISTANCE);
		state.Output.MaxDistance.Value = ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.MAX_DISTANCE);
	}

	internal static float CalculatePlaybackSpeed(float lengthChange, float delta, float previousSpeed)
	{
		// Ignore tracking jitter and discard the first sample after a long stalled frame.
		if (!float.IsFinite(lengthChange) || !float.IsFinite(delta) || delta <= 0f || delta > 0.25f)
			return 0f;
		float rate = MathF.Abs(lengthChange) / delta;
		if (!float.IsFinite(rate) || rate <= MotionDeadzone) return 0f;
		// Lift slow motion into an audible range, then ease toward the fixed 2x cap.
		float response = MathF.Sqrt(Math.Clamp((rate - MotionDeadzone) / (MotionRateForMaximum - MotionDeadzone), 0f, 1f));
		float target = MinimumMovingPlaybackSpeed + (MaximumPlaybackSpeed - MinimumMovingPlaybackSpeed) * response;
		// Keep the first moving frame audible and every frame within the fixed cap.
		float previous = float.IsFinite(previousSpeed)
			? Math.Clamp(previousSpeed, MinimumMovingPlaybackSpeed, MaximumPlaybackSpeed) : MinimumMovingPlaybackSpeed;
		float blend = 1f - MathF.Exp(-delta / SmoothingSeconds);
		return Math.Clamp(previous + (target - previous) * blend, MinimumMovingPlaybackSpeed, MaximumPlaybackSpeed);
	}

	private static void UpdateSafely(ProtoFluxTool tool, Slot wire)
	{
		try
		{
			if (!tool.World.IsDisposed && !tool.World.IsDestroyed) Update(tool, wire);
		}
		catch (Exception ex)
		{
			Stop(tool);
			Logger.LogError("Error updating wire drag sound", ex, Logger.LogCategory.Audio);
		}
	}

	private static void ScheduleFirstUpdate(ProtoFluxTool tool, Slot wire)
	{
		// The first update creates DragState. Later updates reuse its action so the
		// high-frequency Harmony postfix does not allocate a closure each frame.
		tool.World.RunSynchronously(() => UpdateSafely(tool, wire), immediatellyIfPossible: true);
	}

	[HarmonyPatch(typeof(ProtoFluxTool), "OnCommonUpdate")]
	private static class UpdatePatch
	{
		private static void Postfix(ProtoFluxTool __instance, SyncRef<Slot> ____currentTempWire)
		{
			var wire = ____currentTempWire.Target;
			states.TryGetValue(__instance, out var state);
			if (wire == null && state == null) return;
			var world = __instance.World;
			if (world == null || world.IsDisposed || world.IsDestroyed) return;
			// Vanilla updates desktop WirePoint in OnCommonUpdate, so measure afterward.
			if (state != null) state.ScheduleUpdate(wire);
			else ScheduleFirstUpdate(__instance, wire);
		}
	}

	[HarmonyPatch(typeof(ProtoFluxTool), "CleanupDraggedWire")]
	private static class CleanupPatch
	{
		private static void Prefix(ProtoFluxTool __instance) => Stop(__instance);
	}

	[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnDequipped))]
	private static class DequipPatch
	{
		private static void Prefix(ProtoFluxTool __instance) => Stop(__instance);
	}

	[HarmonyPatch(typeof(ProtoFluxTool), "OnDestroy")]
	private static class DestroyPatch
	{
		private static void Prefix(ProtoFluxTool __instance) => Stop(__instance);
	}

	[HarmonyPatch(typeof(ProtoFluxTool), "OnDispose")]
	private static class DisposePatch
	{
		private static void Prefix(ProtoFluxTool __instance) => Stop(__instance);
	}
}
