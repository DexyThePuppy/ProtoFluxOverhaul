using System;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

namespace ProtoFluxOverhaul;

/// <summary>Local allocation ownership checks shared by node and wire patches.</summary>
public static class PermissionHelper
{
	/// <summary>Scoped by the selected-node rebuild operation; never bypasses wire ownership.</summary>
	public static bool BypassPermissionChecks { get; set; }

	public static bool HasPermission(ProtoFluxNodeVisual instance) =>
		BypassPermissionChecks || HasPermission((Component)instance);

	public static bool HasPermission(Component component)
	{
		try
		{
			if (component?.Slot == null || component.World == null || component.LocalUser == null)
				return false;

			component.Slot.ReferenceID.ExtractIDs(out ulong position, out byte allocation);
			var owner = component.World.GetUserByAllocationID(allocation);
			string source = "Slot";
			if (owner == null || position < owner.AllocationIDStart)
			{
				component.ReferenceID.ExtractIDs(out position, out allocation);
				owner = component.World.GetUserByAllocationID(allocation);
				source = "Component";
			}

			bool allowed = owner != null && position >= owner.AllocationIDStart && owner == component.LocalUser;
			if (Logger.IsPermissionLoggingEnabled)
				Logger.LogPermission(source, allowed,
					$"Position={position}, UserID={allocation}, Owner={owner?.UserName}, Type={component.GetType().Name}");
			return allowed;
		}
		catch (Exception ex)
		{
			Logger.LogPermissionException("Permission check error", ex);
			return false;
		}
	}
}
