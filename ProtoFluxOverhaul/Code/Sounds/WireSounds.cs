using System;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	[HarmonyPatch(typeof(ProtoFluxTool))]
	public class ProtoFluxTool_WirePatches
	{
		[HarmonyPatch("StartDraggingWire")]
		[HarmonyPostfix]
		public static void StartDraggingWire_Postfix(ProtoFluxTool __instance, ProtoFluxElementProxy proxy)
		{
			try
			{
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS))
					return;

				// The engine has already started this drag. Feedback belongs to the local
				// tool user, regardless of who originally allocated the connector's slot.
				if (IsLocalSoundTool(__instance) && proxy != null && !proxy.IsRemoved && proxy.Slot != null)
				{
					Logger.LogWire("Grab", $"Playing wire grab sound at position {proxy.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireGrabbed(__instance.World, proxy.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogWire("Grab", "Wire grab sound skipped: No live proxy or local tool interaction");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in wire grab sound", e, LogCategory.Wire);
			}
		}

		// Cut selection sets DeleteHighlight; the wire OnDestroy patch plays its delete sound.
		// OnPrimaryRelease has already returned _cutWires to the pool by the time a postfix runs.
	}

	private static bool IsLocalSoundTool(ProtoFluxTool tool)
	{
		return tool != null && !tool.IsRemoved
			&& (tool.ActiveHandler?.IsOwnedByLocalUser == true || tool.IsUnderLocalUser);
	}

	/// <summary>
	/// Play connect sounds only when the engine actually connected (covers cast-menu success too).
	/// </summary>
	[HarmonyPatch(typeof(ProtoFluxNode), nameof(ProtoFluxNode.TryConnectInput))]
	public class ProtoFluxNode_TryConnectInput_Sound_Patch
	{
		public static void Postfix(ProtoFluxNode __instance, bool __result, bool undoable)
		{
			PlayConnectIfSucceeded(__instance, __result && undoable, "TryConnectInput");
		}
	}

	[HarmonyPatch(typeof(ProtoFluxNode), nameof(ProtoFluxNode.TryConnectImpulse))]
	public class ProtoFluxNode_TryConnectImpulse_Sound_Patch
	{
		public static void Postfix(ProtoFluxNode __instance, bool __result, bool undoable)
		{
			PlayConnectIfSucceeded(__instance, __result && undoable, "TryConnectImpulse");
		}
	}

	[HarmonyPatch(typeof(ProtoFluxNode), nameof(ProtoFluxNode.TryConnectReference))]
	public class ProtoFluxNode_TryConnectReference_Sound_Patch
	{
		public static void Postfix(ProtoFluxNode __instance, bool __result, bool undoable)
		{
			PlayConnectIfSucceeded(__instance, __result && undoable, "TryConnectReference");
		}
	}

	private static void PlayConnectIfSucceeded(ProtoFluxNode node, bool succeeded, string context)
	{
		try
		{
			if (!succeeded) return;
			if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS)) return;
			if (node == null || node.IsRemoved || node.Slot == null) return;
			// Vanilla calls these undoable methods from local tool actions, including the cast menu.
			// The target node can belong to another user or a saved world; allocation ownership
			// must not suppress feedback for an edit that already succeeded. Playback is local-only.

			Logger.LogWire("Connect", $"Playing wire connect sound ({context}) at position {node.Slot.GlobalPosition}");
			ProtoFluxSounds.OnWireConnected(node.World, node.Slot.GlobalPosition);
		}
		catch (Exception e)
		{
			Logger.LogError($"Error in wire connect sound ({context})", e, LogCategory.Wire);
		}
	}
}
