using System;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Patch to make background color compatible with node status (selection, highlighting, validation)
	[HarmonyPatch(typeof(ProtoFluxNodeVisual), "UpdateNodeStatus")]
	public class ProtoFluxNodeVisual_UpdateNodeStatus_Patch
	{
		private static readonly FieldInfo bgImageField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_bgImage");
		private static readonly FieldInfo overviewBgField = AccessTools.Field(typeof(ProtoFluxNodeVisual), "_overviewBg");

		public static bool Prefix(ProtoFluxNodeVisual __instance)
		{
			try
			{
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return true;

				var bgImageRef = (SyncRef<Image>)bgImageField.GetValue(__instance);
				if (bgImageRef?.Target == null) return true;

				if (!PermissionHelper.HasPermission(__instance)) return true;

				var bgImage = bgImageRef.Target;
				// Palette (and other) drives must not be overwritten by the engine Tint write.
				if (bgImage.Tint.IsDriven)
					return false;

				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_HEADER_COLOR_FOR_BACKGROUND))
					return true;

				// Get the node's type color as base
				colorX baseColor;
				var node = __instance.Node.Target;
				if (node != null)
				{
					var nodeType = node.GetType();
					if (nodeType.IsSubclassOf(typeof(UpdateBase)) || nodeType.IsSubclassOf(typeof(UserUpdateBase)))
					{
						bool isAsync = typeof(IAsyncNodeOperation).IsAssignableFrom(nodeType);
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

				if (!bgImage.Tint.IsDriven)
				{
					bgImage.Tint.Value = finalColor;
					Logger.LogUI("UpdateNodeStatus", $"Set tint for status color: R:{finalColor.r:F2} G:{finalColor.g:F2} B:{finalColor.b:F2}");
				}
				else
				{
					Logger.LogUI("UpdateNodeStatus", "Skipped: tint is already driven");
				}

				// Also update overview background if it exists
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

