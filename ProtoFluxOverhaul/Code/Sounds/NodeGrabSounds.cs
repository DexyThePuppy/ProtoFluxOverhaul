using System;
using System.Linq;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	// Patch for Grabbable component to detect node grabbing
	[HarmonyPatch(typeof(Grabbable))]
	public class Grabbable_NodeGrabSounds_Patch
	{
		[HarmonyPatch("Grab")]
		[HarmonyPostfix]
		public static void Grab_Postfix(Grabbable __instance, Grabber grabber, bool supressEvents, IGrabbable __result)
		{
			try
			{
				if (!Config.GetValue(ENABLED) || !Config.GetValue(NODE_SOUNDS))
					return;

				// IsGrabbed can already be true when a new grab is rejected. The return
				// value confirms success; the grabber identifies the user performing it.
				if (supressEvents || __result != __instance || __instance.Slot == null
					|| grabber == null || !grabber.IsUnderLocalUser)
					return;

				// Cheap path: ProtoFlux nodes expose ProtoFluxNode on the same slot as the Grabbable.
				var protoFluxNode = __instance.Slot.GetComponent<ProtoFluxNode>();
				if (protoFluxNode != null)
				{
					Logger.LogNode("Grab", $"Playing node grab sound at position {protoFluxNode.Slot.GlobalPosition} (direct ProtoFluxNode approach)");
					ProtoFluxSounds.OnNodeGrabbed(__instance.World, protoFluxNode.Slot.GlobalPosition);
					return;
				}

				var nodeUi = __instance.Slot.FindChild("<NODE_UI>");
				if (nodeUi != null)
				{
					var nodeVisual = nodeUi.GetComponent<ProtoFluxNodeVisual>();
					if (nodeVisual != null && nodeVisual.Node?.Target != null)
					{
						var node = nodeVisual.Node.Target;
						Logger.LogNode("Grab", $"Playing node grab sound at position {node.Slot.GlobalPosition} (using ProtoFluxNodeVisual approach)");
						ProtoFluxSounds.OnNodeGrabbed(__instance.World, node.Slot.GlobalPosition);
						return;
					}
				}

				if (Config.GetValue(DEBUG_LOGGING))
				{
					var allComponents = __instance.Slot.GetComponents<Component>();
					var componentNames = string.Join(", ", allComponents.Select(c => c.GetType().Name));
					Logger.LogNode("Grab", $"Grabbable does not belong to a ProtoFlux node. Found components: {componentNames}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in node grab sound", e, Logger.LogCategory.Node);
			}
		}
	}
}
