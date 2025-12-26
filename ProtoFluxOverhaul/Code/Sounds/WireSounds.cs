using System;
using System.Collections.Generic;
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
		// Patch for wire grab start
		[HarmonyPatch("StartDraggingWire")]
		[HarmonyPrefix]
		public static void StartDraggingWire_Prefix(ProtoFluxTool __instance, ProtoFluxElementProxy proxy)
		{
			try
			{
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS))
				{
					Logger.LogWire("Grab", "Wire grab sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				bool hasPermission = proxy != null && HasPermission(proxy);
				if (hasPermission)
				{
					Logger.LogWire("Grab", $"Playing wire grab sound at position {proxy.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireGrabbed(__instance.World, proxy.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogWire("Grab", $"Wire grab sound skipped: Proxy={proxy != null}, HasPermission={hasPermission}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in wire grab sound", e, LogCategory.Wire);
			}
		}

		// Patch for wire connection (Input-Output)
		[HarmonyPatch(typeof(ProtoFluxTool), "TryConnect", new Type[] { typeof(ProtoFluxInputProxy), typeof(ProtoFluxOutputProxy) })]
		[HarmonyPostfix]
		public static void TryConnect_InputOutput_Postfix(ProtoFluxTool __instance, ProtoFluxInputProxy input, ProtoFluxOutputProxy output)
		{
			try
			{
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS))
				{
					Logger.LogWire("Connect", "Wire connect sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				bool hasPermission = input != null && HasPermission(input);
				if (input != null && output != null && hasPermission)
				{
					Logger.LogWire("Connect", $"Playing wire connect sound (Input-Output) at position {input.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, input.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogWire("Connect", $"Wire connect sound skipped: Input={input != null}, Output={output != null}, HasPermission={hasPermission}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in wire connect sound (Input-Output)", e, LogCategory.Wire);
			}
		}

		// Patch for wire connection (Impulse-Operation)
		[HarmonyPatch(typeof(ProtoFluxTool), "TryConnect", new Type[] { typeof(ProtoFluxImpulseProxy), typeof(ProtoFluxOperationProxy) })]
		[HarmonyPostfix]
		public static void TryConnect_ImpulseOperation_Postfix(ProtoFluxTool __instance, ProtoFluxImpulseProxy impulse, ProtoFluxOperationProxy operation)
		{
			try
			{
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS))
				{
					Logger.LogWire("Connect", "Wire connect sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				bool hasPermission = impulse != null && HasPermission(impulse);
				if (impulse != null && operation != null && hasPermission)
				{
					Logger.LogWire("Connect", $"Playing wire connect sound (Impulse-Operation) at position {impulse.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, impulse.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogWire("Connect", $"Wire connect sound skipped: Impulse={impulse != null}, Operation={operation != null}, HasPermission={hasPermission}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in wire connect sound (Impulse-Operation)", e, LogCategory.Wire);
			}
		}

		// Patch for wire connection (Node-Input-Output)
		[HarmonyPatch(typeof(ProtoFluxTool), "TryConnect", new Type[] { typeof(ProtoFluxNode), typeof(ISyncRef), typeof(INodeOutput) })]
		[HarmonyPostfix]
		public static void TryConnect_NodeInputOutput_Postfix(ProtoFluxTool __instance, ProtoFluxNode node, ISyncRef input, INodeOutput output)
		{
			try
			{
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS))
				{
					Logger.LogWire("Connect", "Wire connect sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Only play sound if we have permission
				bool hasPermission = node != null && HasPermission(node);
				if (node != null && input != null && output != null && hasPermission)
				{
					Logger.LogWire("Connect", $"Playing wire connect sound (Node-Input-Output) at position {node.Slot.GlobalPosition}");
					ProtoFluxSounds.OnWireConnected(__instance.World, node.Slot.GlobalPosition);
				}
				else
				{
					Logger.LogWire("Connect", $"Wire connect sound skipped: Node={node != null}, Input={input != null}, Output={output != null}, HasPermission={hasPermission}");
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in wire connect sound (Node-Input-Output)", e, LogCategory.Wire);
			}
		}

		// Patch for wire deletion
		[HarmonyPatch("OnPrimaryRelease")]
		[HarmonyPostfix]
		public static void OnPrimaryRelease_Postfix(ProtoFluxTool __instance)
		{
			try
			{
				// Skip if disabled or no wire sounds
				if (!Config.GetValue(ENABLED) || !Config.GetValue(WIRE_SOUNDS))
				{
					Logger.LogWire("Delete", "Wire delete sound skipped: Mod or wire sounds disabled");
					return;
				}

				// Check if we're deleting wires (using the cut line)
				if (__instance.GetType().GetField("_cutWires", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance) is HashSet<ProtoFluxWireManager> cutWires &&
					cutWires.Count > 0)
				{
					// Play delete sound for cut wires
					foreach (var wire in cutWires)
					{
						bool hasPermission = wire != null && !wire.IsRemoved && HasPermission(wire);
						if (hasPermission)
						{
							Logger.LogWire("Delete", $"Playing wire delete sound (cut) at position {wire.Slot.GlobalPosition}");
							ProtoFluxSounds.OnWireDeleted(__instance.World, wire.Slot.GlobalPosition);
						}
						else
						{
							Logger.LogWire("Delete", $"Wire delete sound skipped (cut): Wire={wire != null}, IsRemoved={wire?.IsRemoved}, HasPermission={hasPermission}");
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in wire delete sound", e, LogCategory.Wire);
			}
		}
	}
}

