using System;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	[HarmonyPatch(typeof(ProtoFluxTool))]
	public class ProtoFluxTool_SpawnNodeSounds_Patch
	{
		// Every SpawnNode overload, including SpawnNode<T>, creates exactly one slot
		// here. Visual generation and rebuilding do not use this method.
		[HarmonyPatch("GenerateSlotNode", new Type[] { typeof(Type) })]
		[HarmonyPostfix]
		public static void GenerateSlotNode_Postfix(ProtoFluxTool __instance, Type type, Slot __result)
		{
			try
			{
				// Skip if disabled or no node sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(NODE_SOUNDS))
				{
					Logger.LogNode("Create", "Node create sound skipped: Mod or node sounds disabled");
					return;
				}

				if (!IsLocalSoundTool(__instance) || __result == null || __result.IsRemoved)
					return;

				var world = __instance.World;
				var nodeSlot = __result;
				// GenerateSlotNode returns before SpawnNode attaches the component. Queue
				// this until the current world action finishes, including node setup.
				world.RunSynchronously(() => PlayCreatedNode(world, nodeSlot, type),
					immediatellyIfPossible: false);
			}
			catch (Exception e)
			{
				Logger.LogError("Error scheduling node create sound", e, Logger.LogCategory.Node);
			}
		}

		private static void PlayCreatedNode(World world, Slot nodeSlot, Type nodeType)
		{
			try
			{
				if (world == null || world.IsDisposed || world.IsDestroyed
					|| nodeSlot == null || nodeSlot.IsRemoved || nodeSlot.World != world
					|| !Config.GetValue(ENABLED) || !Config.GetValue(NODE_SOUNDS))
					return;

				var node = nodeSlot.GetComponent<ProtoFluxNode>();
				// Failed attachment or a setup callback that removes the node must not
				// produce a creation sound for an empty slot.
				if (node == null || node.IsRemoved || nodeType == null || !nodeType.IsInstanceOfType(node))
					return;

				Logger.LogNode("Create", $"Playing node create sound at position {nodeSlot.GlobalPosition}");
				ProtoFluxSounds.OnNodeCreated(world, nodeSlot.GlobalPosition);
			}
			catch (Exception e)
			{
				Logger.LogError("Error in node create sound", e, Logger.LogCategory.Node);
			}
		}
	}
}
