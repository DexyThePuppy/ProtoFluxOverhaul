using System;
using System.Linq;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using Renderite.Shared;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Patch to make background color compatible with node status (selection, highlighting, validation)
	[HarmonyPatch(typeof(ProtoFluxNodeVisual), "UpdateNodeStatus")]
	public class ProtoFluxNodeVisual_UpdateNodeStatus_Patch
	{
		private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");

		public static bool Prefix(ProtoFluxNodeVisual __instance)
		{
			try
			{
				// Skip if disabled or not using header color for background
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return true;
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND)) return true;

				// Get background image
				var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
				if (bgImageRef?.Target == null) return true;

				// === User Permission Check ===
				if (!PermissionHelper.HasPermission(__instance)) return true;

				// Get the node's type color as base
				colorX baseColor;
				var node = __instance.Node.Target;
				if (node != null)
				{
					var nodeType = node.GetType();
					if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
					{
						bool isAsync = nodeType.GetInterfaces().Any(i => i == typeof(IAsyncNodeOperation));
						baseColor = isAsync ? DatatypeColorHelper.ASYNC_FLOW_COLOR : DatatypeColorHelper.SYNC_FLOW_COLOR;
					}
					else
					{
						baseColor = DatatypeColorHelper.GetTypeColor(nodeType);
					}

					// Darken it like we do when initially applying (preserve alpha)
					baseColor = baseColor.MulRGB(0.5f);
				}
				else
				{
					baseColor = RadiantUI_Constants.BG_COLOR; // fallback
				}

				// Apply status color lerps (same logic as original UpdateNodeStatus)
				colorX finalColor = baseColor;

				if (__instance.IsSelected.Value)
				{
					finalColor = MathX.LerpUnclamped(finalColor, colorX.Cyan, 0.5f);
				}

				if (__instance.IsHighlighted.Value)
				{
					finalColor = MathX.LerpUnclamped(finalColor, colorX.Yellow, 0.1f);
				}

				if (!__instance.IsNodeValid)
				{
					finalColor = MathX.LerpUnclamped(finalColor, colorX.Red, 0.5f);
				}

				// Update the ValueField source so the driver propagates the new color
				var bgImage = bgImageRef.Target;
				var colorField = bgImage.Slot.GetComponent<ValueField<colorX>>();
				if (colorField != null)
				{
					colorField.Value.Value = finalColor;
					Logger.LogUI("UpdateNodeStatus", $"Updated ValueField for status color: R:{finalColor.r:F2} G:{finalColor.g:F2} B:{finalColor.b:F2}");
				}
				else
				{
					// Fallback: if no ValueField exists, set directly
					if (!bgImage.Tint.IsDriven)
					{
						bgImage.Tint.Value = finalColor;
						Logger.LogUI("UpdateNodeStatus", $"Set tint directly (no driver): R:{finalColor.r:F2} G:{finalColor.g:F2} B:{finalColor.b:F2}");
					}
					else
					{
						Logger.LogUI("UpdateNodeStatus", "Skipped: tint is driven but no ValueField found");
					}
				}

				// Also update overview background if it exists
				var overviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg");
				var overviewBg = (FieldDrive<colorX>)overviewBgField.GetValue(__instance);
				if (overviewBg.IsLinkValid)
				{
					overviewBg.Target.Value = finalColor;
				}

				// Skip original method since we handled it
				return false;
			}
			catch (Exception e)
			{
				Logger.LogError("Error in UpdateNodeStatus patch", e, LogCategory.UI);
				// Let original run if we fail
				return true;
			}
		}
	}
}

