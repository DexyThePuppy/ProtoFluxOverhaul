using System;
using System.Linq;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Patch to handle initial node creation
	[HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateVisual")]
	public class ProtoFluxNodeVisual_GenerateVisual_Patch
	{
		private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");

		public static void Postfix(ProtoFluxNodeVisual __instance)
		{
			try
			{
				// Skip if disabled
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

				// Skip if instance or slot is destroyed/removed
				if (__instance == null || __instance.IsRemoved ||
				    __instance.Slot == null || __instance.Slot.IsRemoved) return;

				// Log entry for debugging regeneration issues
				var slotTag = __instance.Slot.Tag;
				var slotRefId = __instance.Slot.ReferenceID.ToString();
				var nodeName = __instance.Node?.Target?.GetType().Name ?? "Unknown";
				Logger.LogUI("GenerateVisual Entry", $"Processing node '{nodeName}', Slot={__instance.Slot.Name}, RefID={slotRefId}, Tag='{slotTag ?? "(null)"}'");

				// Skip if already styled by ProtoFluxOverhaul (prevents duplicate processing)
				if (RoundedCornersHelper.HasPFOTag(__instance.Slot))
				{
					Logger.LogUI("GenerateVisual", $"Skipping already-styled node (Tag contains ProtoFluxOverhaul)");
					return;
				}

				// Audio is now handled on-demand by ProtoFluxSounds

				// === User Permission Check ===
				if (!PermissionHelper.HasPermission(__instance)) return;

				// === Mark node as styled EARLY to prevent reprocessing if later code fails ===
				// This must happen before any code that could throw an exception
				RoundedCornersHelper.AddPFOTag(__instance.Slot);
				Logger.LogUI("Tag", $"Added ProtoFluxOverhaul tag via GenerateVisual (early)");

				// Get the node's type color for potential background use
				colorX nodeTypeColor;
				var node = __instance.Node.Target;
				if (node != null)
				{
					var nodeType = node.GetType();
					if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
					{
						// Check if it's an async update node
						bool isAsync = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
						nodeTypeColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
					}
					else
					{
						nodeTypeColor = DatatypeColorHelper.GetTypeColor(nodeType);
					}
				}
				else
				{
					nodeTypeColor = colorX.White; // fallback
				}

				// Apply rounded corners to background with header color if config is enabled
				var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
				if (bgImageRef?.Target != null)
				{
					colorX darkenedColor = nodeTypeColor * 0.5f;
					RoundedCornersHelper.ApplyRoundedCorners(bgImageRef.Target, false, darkenedColor);
				}

				// Find all connector slots in the hierarchy (skip removed/destroyed)
				var connectorSlots = __instance.Slot.GetComponentsInChildren<Image>()
					.Where(img => img != null && !img.IsRemoved &&
					              img.Slot != null && !img.Slot.IsRemoved &&
					              img.Slot.Name == "Connector")
					.ToList();

				foreach (var connectorImage in connectorSlots)
				{
					DynamicConnectorStyler.StyleConnectorImage(
						__instance,
						connectorImage,
						isOutputOverride: null,
						impulseTypeOverride: null,
						isOperationOverride: null,
						isAsyncOverride: null,
						applyRectLayout: false,
						applyWirePointAnchor: false,
						styleLabelBackground: true,
						logContext: "GenerateVisual"
					);
				}

				// === Cleanup relay node visuals ===
				// Relay nodes don't need duplicate background sprites and shading
				if (node != null)
				{
					RoundedCornersHelper.CleanupRelayNodeVisuals(__instance.Slot, node);
				}

				Logger.LogUI("GenerateVisual", $"Successfully processed node");
			}
			catch (Exception e)
			{
				Logger.LogError("Error in ProtoFluxNodeVisual_GenerateVisual_Patch", e, LogCategory.UI);
			}
		}
	}
}

