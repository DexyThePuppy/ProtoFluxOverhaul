using System;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Shared permission check method for all patches
	// Uses the same logic as wire permission checks for consistency
	public static class PermissionHelper
	{
		/// <summary>
		/// When true, all permission checks are bypassed. Used during auto-rebuild of selected nodes
		/// where the user explicitly selected nodes via ProtoFluxTool (local visual only, safe to bypass).
		/// </summary>
		public static bool BypassPermissionChecks { get; set; } = false;

		/// <summary>
		/// Permission check for ProtoFluxNodeVisual using the same logic as wire permission checks.
		/// Allows the owner or host/admin to modify.
		/// </summary>
		public static bool HasPermission(ProtoFluxNodeVisual instance)
		{
			try
			{
				// Bypass permission checks when auto-rebuilding selected nodes
				if (BypassPermissionChecks) return true;

				if (instance == null || instance.Slot == null)
				{
					Logger.LogPermission("Check", false, "Permission check failed: instance or slot is null");
					return false;
				}

				// Get the visual's slot owner
				instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
				User slotAllocUser = instance.World.GetUserByAllocationID(user);
				Logger.LogPermission("Visual Slot", true, $"Visual slot allocation: Position={position}, UserID={user}, User={slotAllocUser?.UserName}");

				if (slotAllocUser == null || position < slotAllocUser.AllocationIDStart)
				{
					// Fallback: check the instance component ownership
					instance.ReferenceID.ExtractIDs(out ulong position1, out byte user1);
					User instanceAllocUser = instance.World.GetUserByAllocationID(user1);
					Logger.LogPermission("Instance", true, $"Instance allocation: Position={position1}, UserID={user1}, User={instanceAllocUser?.UserName}");

					// Allow the owner or host/admin to modify
					bool hasPermission = (instanceAllocUser != null &&
						position1 >= instanceAllocUser.AllocationIDStart &&
						instanceAllocUser == instance.LocalUser);

					Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={instanceAllocUser?.UserName}, IsLocalUser={instanceAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={hasPermission}");
					return hasPermission;
				}

				// Allow the owner or host/admin to modify
				bool result = slotAllocUser == instance.LocalUser;
				Logger.LogPermission("Visual Check", result, $"Permission check (visual): Owner={slotAllocUser?.UserName}, IsLocalUser={slotAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={result}");
				return result;
			}
			catch (Exception e)
			{
				// If anything goes wrong, deny permission to be safe
				Logger.LogError("Permission check error", e, LogCategory.Permission);
				return false;
			}
		}

		/// <summary>
		/// Permission check for ProtoFluxWireManager.
		/// Allows the owner or host/admin to modify.
		/// </summary>
		public static bool HasPermission(ProtoFluxWireManager instance)
		{
			try
			{
				if (instance == null || instance.Slot == null)
				{
					Logger.LogPermission("Check", false, "Permission check failed: instance or slot is null");
					return false;
				}

				// Get the wire's owner
				instance.Slot.ReferenceID.ExtractIDs(out ulong position, out byte user);
				User wirePointAllocUser = instance.World.GetUserByAllocationID(user);
				Logger.LogPermission("Wire Point", true, $"Wire point allocation: Position={position}, UserID={user}, User={wirePointAllocUser?.UserName}");

				if (wirePointAllocUser == null || position < wirePointAllocUser.AllocationIDStart)
				{
					instance.ReferenceID.ExtractIDs(out ulong position1, out byte user1);
					User instanceAllocUser = instance.World.GetUserByAllocationID(user1);
					Logger.LogPermission("Instance", true, $"Instance allocation: Position={position1}, UserID={user1}, User={instanceAllocUser?.UserName}");

					// Allow the wire owner or host/admin to modify
					bool hasPermission = (instanceAllocUser != null &&
						position1 >= instanceAllocUser.AllocationIDStart &&
						instanceAllocUser == instance.LocalUser);

					Logger.LogPermission("Instance Check", hasPermission, $"Permission check (instance): Owner={instanceAllocUser?.UserName}, IsLocalUser={instanceAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={hasPermission}");
					return hasPermission;
				}

				// Allow the wire owner or host/admin to modify
				bool result = wirePointAllocUser == instance.LocalUser;
				Logger.LogPermission("Wire Check", result, $"Permission check (wire): Owner={wirePointAllocUser?.UserName}, IsLocalUser={wirePointAllocUser == instance.LocalUser}, IsHost={instance.LocalUser.IsHost}, Result={result}");
				return result;
			}
			catch (Exception e)
			{
				// If anything goes wrong, deny permission to be safe
				Logger.LogError("Permission check error", e, LogCategory.Permission);
				return false;
			}
		}
	}
}

