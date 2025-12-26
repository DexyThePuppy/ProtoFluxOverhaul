using System;
using FrooxEngine.ProtoFlux;
using HarmonyLib;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	[HarmonyPatch(typeof(ProtoFluxTool))]
	public class ProtoFluxTool_SpawnNodeSounds_Patch
	{
		// Patch for node creation (SpawnNode with Type)
		[HarmonyPatch("SpawnNode", new Type[] { typeof(Type), typeof(Action<ProtoFluxNode>) })]
		[HarmonyPostfix]
		public static void SpawnNode_Type_Postfix(ProtoFluxTool __instance, Type nodeType, Action<ProtoFluxNode> setup, ProtoFluxNode __result)
		{
			try
			{
				// Skip if disabled or no node sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(NODE_SOUNDS))
				{
					Logger.LogNode("Create", "Node create sound skipped: Mod or node sounds disabled");
					return;
				}

				// Only play sound if node was successfully created and we have permission
				if (__result != null && __result.Slot != null && HasPermission(__result))
				{
					Logger.LogNode("Create", $"Playing node create sound (type) at position {__result.Slot.GlobalPosition}");
					ProtoFluxSounds.OnNodeCreated(__instance.World, __result.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogNode("Create", $"Node create sound skipped (type): Node={__result != null}, Slot={__result?.Slot != null}, HasPermission={__result != null && HasPermission(__result)}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in node create sound (type)", e, Logger.LogCategory.Node);
			}
		}

		// Patch for node creation (SpawnNode generic)
		[HarmonyPatch("SpawnNode", new Type[] { typeof(Action<ProtoFluxNode>) })]
		[HarmonyPostfix]
		public static void SpawnNode_Generic_Postfix(ProtoFluxTool __instance, Action<ProtoFluxNode> setup, ProtoFluxNode __result)
		{
			try
			{
				// Skip if disabled or no node sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(NODE_SOUNDS))
				{
					Logger.LogNode("Create", "Node create sound skipped: Mod or node sounds disabled");
					return;
				}

				// Only play sound if node was successfully created and we have permission
				if (__result != null && __result.Slot != null && HasPermission(__result))
				{
					Logger.LogNode("Create", $"Playing node create sound (generic) at position {__result.Slot.GlobalPosition}");
					ProtoFluxSounds.OnNodeCreated(__instance.World, __result.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogNode("Create", $"Node create sound skipped (generic): Node={__result != null}, Slot={__result?.Slot != null}, HasPermission={__result != null && HasPermission(__result)}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in node create sound (generic)", e, Logger.LogCategory.Node);
			}
		}
	}
}

