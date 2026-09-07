using System;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Patch to handle dynamic overview mode changes
	[HarmonyPatch(typeof(ProtoFluxNodeVisual), "OnChanges")]
	public class ProtoFluxNodeVisual_OnChanges_Patch
	{

		public static void Postfix(ProtoFluxNodeVisual __instance)
		{
			try
			{
				// Skip if disabled
				if (!ProtoFluxOverhaul.Config.GetValue(ProtoFluxOverhaul.ENABLED)) return;

				// Skip if instance is null or world is not available
				if (__instance == null || __instance.World == null) return;

				// Skip if we don't own this node
				if (!PermissionHelper.HasPermission(__instance)) return;

				// Get the user's overview mode setting using ProtoFluxTool
				if (__instance.LocalUser == null) return;

				bool overviewModeEnabled = OverviewModeHelper.GetOverviewMode(__instance.LocalUser);

				// Find our custom TitleParent>Header slot and Overview slot
				var header = __instance.Slot.FindChild("TitleParent")?.FindChild("Header");
				// A skipped or failed style pass must not hide the engine's only node name.
				if (string.IsNullOrWhiteSpace(header?.GetComponentInChildren<Text>()?.Content.Value)) return;
				var overviewSlot = OverviewModeHelper.GetOverviewImage(__instance);

				// Only toggle header visibility if there's an overview slot
				if (overviewSlot != null)
				{
					bool baseHeaderVisible = !overviewModeEnabled;
					bool baseOverviewVisible = overviewModeEnabled;

					// Update the base visibility of our hover driver when present.
					var headerDriver = header.GetComponent<BooleanValueDriver<bool>>();
					if (headerDriver != null && headerDriver.TargetField.Target == header.ActiveSelf_Field)
					{
						headerDriver.FalseValue.Value = baseHeaderVisible;
						headerDriver.TrueValue.Value = true;
					}
					else
					{
						header.ActiveSelf = baseHeaderVisible;
					}

					// Update overview visibility: if driven by our hover driver, update its FalseValue; otherwise set directly
					var overviewDriver = overviewSlot.Slot.GetComponent<BooleanValueDriver<bool>>();
					if (overviewDriver != null && overviewDriver.TargetField.Target == overviewSlot.Slot.ActiveSelf_Field)
					{
						overviewDriver.FalseValue.Value = baseOverviewVisible;
						overviewDriver.TrueValue.Value = false;
					}
					else
					{
						overviewSlot.Slot.ActiveSelf = baseOverviewVisible;
					}
				}
				else
				{
					// No overview slot found, keep header visible.
					header.ActiveSelf = true;
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in OnChanges patch", e, LogCategory.UI);
			}
		}
	}
}

