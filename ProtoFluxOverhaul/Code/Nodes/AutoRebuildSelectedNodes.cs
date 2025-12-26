using System;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	[HarmonyPatch(typeof(ProtoFluxTool))]
	public class ProtoFluxTool_AutoRebuildSelectedNodes_Patch
	{
		// Track nodes we've already rebuilt to prevent double-processing
		private static readonly HashSet<ProtoFluxNode> _rebuiltNodes = new HashSet<ProtoFluxNode>();
		private static bool _isRebuilding = false;

		// Patch selection to optionally force a rebuild of selected nodes using our styling
		[HarmonyPatch("MarkSelection")]
		[HarmonyPostfix]
		public static void MarkSelection_Postfix(ProtoFluxTool __instance)
		{
			try
			{
				// Avoid mutating visuals during secondary-press interactions.
				// This prevents invalid tool UI/tooltip state mid-frame.
				if (ProtoFluxToolInteractionGuards.IsSecondaryPressing)
				{
					Logger.LogUI("AutoRebuild", "Skipping: Secondary press in progress");
					return;
				}

				// Prevent re-entry during rebuild
				if (_isRebuilding)
				{
					Logger.LogUI("AutoRebuild", "Skipping: Already rebuilding (re-entry prevention)");
					return;
				}

				if (!Config.GetValue(ENABLED) || !Config.GetValue(AUTO_REBUILD_SELECTED_NODES))
				{
					return;
				}

				// Access the protected _selectedNodes list via reflection
				var selectedNodesField = AccessTools.Field(typeof(ProtoFluxTool), "_selectedNodes");
				var selected = selectedNodesField?.GetValue(__instance) as SyncRefList<ProtoFluxNodeVisual>;
				if (selected == null || selected.Count == 0)
				{
					Logger.LogUI("AutoRebuild", $"Skipping: No selected nodes (selected={selected != null}, count={selected?.Count ?? 0})");
					return;
				}

				Logger.LogUI("AutoRebuild", $"Processing {selected.Count} selected node(s)...");

				// Collect nodes to rebuild (can't modify list while iterating)
				// Only rebuild nodes we haven't already processed
				// NOTE: We skip strict ownership checks here because:
				// 1. ProtoFluxNodeVisual is local-only UI (not synced)
				// 2. User explicitly selected the node with the tool
				// 3. Rebuilding only affects local visual appearance
				var nodesToRebuild = new List<ProtoFluxNode>();
				foreach (var visual in selected)
				{
					if (visual == null)
					{
						Logger.LogUI("AutoRebuild", "Skipping visual: null reference");
						continue;
					}

					// Basic validity check - ensure visual is not destroyed
					if (visual.IsRemoved)
					{
						Logger.LogUI("AutoRebuild", $"Skipping visual '{visual.Slot?.Name}': Visual is removed");
						continue;
					}

					var node = visual.Node?.Target;
					if (node == null)
					{
						Logger.LogUI("AutoRebuild", $"Skipping visual '{visual.Slot?.Name}': Node is null");
						continue;
					}

					// Skip if already rebuilt recently
					if (_rebuiltNodes.Contains(node))
					{
						Logger.LogUI("AutoRebuild", $"Skipping node '{node.GetType().Name}': Already rebuilt");
						continue;
					}

					// Skip if the node was already styled by ProtoFluxOverhaul
					var slotTag = visual.Slot?.Tag;
					Logger.LogUI("AutoRebuild", $"Checking node '{node.GetType().Name}': Slot={visual.Slot?.Name}, RefID={visual.Slot?.ReferenceID}, Tag='{slotTag ?? "(null)"}'");
					if (RoundedCornersHelper.HasPFOTag(visual.Slot))
					{
						Logger.LogUI("AutoRebuild", $"Skipping node '{node.GetType().Name}': Already styled (Tag contains ProtoFluxOverhaul)");
						continue;
					}
					Logger.LogUI("AutoRebuild", $"Node '{node.GetType().Name}' will be rebuilt (tag check passed)");

					Logger.LogUI("AutoRebuild", $"Queuing node '{node.GetType().Name}' for rebuild");
					nodesToRebuild.Add(node);
				}

				if (nodesToRebuild.Count == 0)
				{
					Logger.LogUI("AutoRebuild", "No nodes queued for rebuild after filtering");
					return;
				}

				Logger.LogUI("AutoRebuild", $"Rebuilding {nodesToRebuild.Count} node(s)...");

				_isRebuilding = true;
				// Bypass permission checks during auto-rebuild since:
				// 1. User explicitly selected these nodes with ProtoFluxTool
				// 2. Visual is local-only (not synced)
				// 3. Rebuilding only affects local appearance
				PermissionHelper.BypassPermissionChecks = true;
				try
				{
					// Clear selection first (will be rebuilt with new visuals)
					selected.Clear();

					// Destroy existing visuals and recreate them using Resonite's proper method
					// This mirrors how Pack/Unpack works: RemoveVisual() then EnsureVisual()
					foreach (var node in nodesToRebuild)
					{
						_rebuiltNodes.Add(node);

						// Use Resonite's RemoveVisual extension method - properly destroys the <NODE_UI> slot
						node.RemoveVisual();
						Logger.LogUI("AutoRebuild", $"Removed visual for '{node.GetType().Name}'");

						// EnsureVisual creates a new <NODE_UI> slot and visual, triggering our patches
						var newVisual = node.EnsureVisual();
						if (newVisual != null)
						{
							// Re-add to selection and mark as selected
							selected.Add(newVisual);
							newVisual.IsSelected.Value = true;
							Logger.LogUI("AutoRebuild", $"Rebuilt node visual for '{node.GetType().Name}' via MarkSelection");
						}
					}
				}
				finally
				{
					_isRebuilding = false;
					PermissionHelper.BypassPermissionChecks = false;
					// Clear the rebuilt nodes set after a short delay to allow re-rebuilding later
					// For now, we clear immediately since we're done with this batch
					_rebuiltNodes.Clear();
				}
			}
			catch (Exception e)
			{
				_isRebuilding = false;
				PermissionHelper.BypassPermissionChecks = false;
				Logger.LogError("Error rebuilding selected nodes", e, Logger.LogCategory.UI);
			}
		}
	}
}

