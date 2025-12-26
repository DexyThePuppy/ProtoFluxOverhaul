using System;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Patch ImpulseDisplay content so its "timeline" bar matches the title/header sprite + shading overlay.
	// NOTE: ImpulseDisplay lives in ProtoFluxBindings in many installs; don't hard-reference its type at compile time.
	[HarmonyPatch]
	public class ImpulseDisplay_BuildContentUI_Patch
	{
		private const string IMPULSE_DISPLAY_TYPE =
			"FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.ImpulseDisplay";

		public static MethodBase TargetMethod()
		{
			var t = AccessTools.TypeByName(IMPULSE_DISPLAY_TYPE);
			return t == null ? null : AccessTools.Method(t, "BuildContentUI");
		}

		public static void Postfix(object __instance, ProtoFluxNodeVisual visual, UIBuilder ui)
		{
			try
			{
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;
				if (__instance == null || visual == null || ui == null) return;

				// Respect the same ownership/permission behavior as other node UI patches
				if (!PermissionHelper.HasPermission(visual)) return;

				var timelineRootField = AccessTools.Field(__instance.GetType(), "_timelineRoot");
				var timelineRef = timelineRootField?.GetValue(__instance) as SyncRef<Slot>;
				var timelineSlot = timelineRef?.Target;
				if (timelineSlot == null) return;

				var timelineImage = timelineSlot.GetComponent<Image>();
				if (timelineImage == null) return;

				// Ensure the timeline image is masked (useful when using rounded sprites + moving indicators)
				// Mask.OnAttach will ensure a Graphic exists; we already have Image, but this is safe.
				var mask = timelineSlot.GetComponentOrAttach<Mask>();
				mask.ShowMaskGraphic.Value = true;

				// Use the same sprite family as the title/header, including inverted shading
				RoundedCornersHelper.ApplyRoundedCorners(timelineImage, isHeader: true, invertShading: true);

				// Optional: drive timeline tint from PlatformColorPalette (match the default MID tint intent)
				if (ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.USE_PLATFORM_COLOR_PALETTE))
				{
					var palette = RoundedCornersHelper.EnsurePlatformColorPalette(ui.Root);
					if (palette != null)
					{
						var tintCopy = timelineImage.Slot.GetComponentOrAttach<ValueCopy<colorX>>();
						if (!RoundedCornersHelper.TryLinkValueCopy(tintCopy, palette.Neutrals.Mid, timelineImage.Tint))
						{
							Logger.LogUI("PlatformColorPalette", "Skipped ImpulseDisplay timeline tint copy; existing drive detected");
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in ImpulseDisplay_BuildContentUI_Patch", e, LogCategory.UI);
			}
		}
	}
}

