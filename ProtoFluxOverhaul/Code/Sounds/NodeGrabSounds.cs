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
		// Patch for Grab method to detect when objects are grabbed
		[HarmonyPatch("Grab")]
		[HarmonyPostfix]
		public static void Grab_Postfix(Grabbable __instance, Grabber grabber, Slot holdSlot, bool supressEvents)
		{
			try
			{
				// Skip if disabled or no node sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(NODE_SOUNDS))
				{
					Logger.LogNode("Grab", "Node grab sound skipped: Mod or node sounds disabled");
					return;
				}

				// Only handle grab events (not suppressed events)
				if (!supressEvents && __instance.IsGrabbed && HasPermission(__instance))
				{
					// Check if this grabbable belongs to a ProtoFlux node
					// The Grabbable is attached to the parent slot of ProtoFluxNodeVisual (same slot as ProtoFluxNode)
					var protoFluxNode = __instance.Slot.GetComponent<ProtoFluxNode>();
					if (protoFluxNode != null)
					{
						Logger.LogNode("Grab", $"Playing node grab sound at position {protoFluxNode.Slot.GlobalPosition} (direct ProtoFluxNode approach)");
						ProtoFluxSounds.OnNodeGrabbed(__instance.World, protoFluxNode.Slot.GlobalPosition);
					}
					else
					{
						// Fallback: check for ProtoFluxNodeVisual in case the structure is different
						var nodeVisual = __instance.Slot.FindChild("<NODE_UI>")?.GetComponent<ProtoFluxNodeVisual>();
						if (nodeVisual != null && nodeVisual.Node?.Target != null)
						{
							var node = nodeVisual.Node.Target;
							Logger.LogNode("Grab", $"Playing node grab sound at position {node.Slot.GlobalPosition} (using ProtoFluxNodeVisual approach)");
							ProtoFluxSounds.OnNodeGrabbed(__instance.World, node.Slot.GlobalPosition);
						}
						else
						{
							// Additional debugging: log what components we found
							var allComponents = __instance.Slot.GetComponents<Component>();
							var componentNames = string.Join(", ", allComponents.Select(c => c.GetType().Name));
							Logger.LogNode("Grab", $"Grabbable does not belong to a ProtoFlux node. Found components: {componentNames}");
						}
					}
				}
				else
				{
					Logger.LogNode("Grab", $"Node grab sound skipped: SupressEvents={supressEvents}, IsGrabbed={__instance.IsGrabbed}, HasPermission={HasPermission(__instance)}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in node grab sound", e, Logger.LogCategory.Node);
			}
		}
	}
}

