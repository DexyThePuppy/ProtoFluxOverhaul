using System;
using System.Linq;
using System.Reflection;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Helper class for shared functionality
	public static partial class RoundedCornersHelper
	{
		/// <summary>
		/// Tag name used to mark nodes that have been styled by ProtoFluxOverhaul.
		/// Added to the node's slot to identify already-patched nodes.
		/// </summary>
		public const string PFO_TAG_NAME = "ProtoFluxOverhaul";

		/// <summary>
		/// Scale of the rounded sprite used for connector label backgrounds.
		/// Label / Title / Background == 0.03 : 0.05 : 0.09
		/// </summary>
		public const float CONNECTOR_LABEL_SPRITE_SCALE = 0.03f;

		/// <summary>
		/// Checks if a slot has been tagged as styled by ProtoFluxOverhaul.
		/// Uses Contains instead of exact match in case the tag has other content.
		/// </summary>
		public static bool HasPFOTag(Slot slot)
		{
			if (slot == null) return false;
			var tag = slot.Tag;
			bool hasTag = !string.IsNullOrEmpty(tag) && tag.Contains(PFO_TAG_NAME);
			if (hasTag)
			{
				Logger.LogUI("Tag Check", $"Slot '{slot.Name}' has PFO tag: '{tag}'");
			}
			return hasTag;
		}

		/// <summary>
		/// Adds the ProtoFluxOverhaul tag to a slot to mark it as styled.
		/// </summary>
		public static void AddPFOTag(Slot slot)
		{
			if (slot == null) return;
			slot.Tag = PFO_TAG_NAME;
		}

		/// <summary>
		/// Checks if a ProtoFlux node is a relay/passthrough node.
		/// Relay nodes have IsPassthrough = true or are specific relay types.
		/// </summary>
		public static bool IsRelayNode(ProtoFluxNode node)
		{
			if (node == null) return false;

			var nodeType = node.GetType();

			// Check for IsPassthrough property (ValueRelay, ObjectRelay, etc.)
			var isPassthroughProp = nodeType.GetProperty("IsPassthrough", BindingFlags.Public | BindingFlags.Instance);
			if (isPassthroughProp != null)
			{
				try
				{
					var value = isPassthroughProp.GetValue(node);
					if (value is bool isPassthrough && isPassthrough)
					{
						return true;
					}
				}
				catch { }
			}

			// Check for IsOperationPassthrough method (CallRelay, ContinuationRelay, AsyncCallRelay)
			var isOpPassthroughMethod = nodeType.GetMethod("IsOperationPassthrough", BindingFlags.Public | BindingFlags.Instance);
			if (isOpPassthroughMethod != null)
			{
				try
				{
					// These nodes are passthrough if the method exists and returns true for index 0
					var result = isOpPassthroughMethod.Invoke(node, new object[] { 0 });
					if (result is bool isPassthrough && isPassthrough)
					{
						return true;
					}
				}
				catch { }
			}

			// Fallback: check by type name for known relay types
			string typeName = nodeType.Name;
			return typeName.Contains("Relay") ||
			       typeName == "ContinuouslyChangingValueRelay`1" ||
			       typeName == "ContinuouslyChangingObjectRelay`1";
		}

		/// <summary>
		/// Cleans up relay node visuals by removing the background Image sprite and Shading child slot.
		/// This prevents duplicate sprites since the node visual patch adds these.
		/// </summary>
		public static void CleanupRelayNodeVisuals(Slot nodeUISlot, ProtoFluxNode node)
		{
			if (nodeUISlot == null || nodeUISlot.IsRemoved || node == null || node.IsRemoved) return;
			if (!IsRelayNode(node)) return;

			Logger.LogUI("Relay Cleanup", $"Cleaning up relay node visuals for '{node.GetType().Name}'");

			// Find the background Image (the _bgImage from ProtoFluxNodeVisual)
			// It's typically the first Image in the node UI with BG_COLOR tint
			var bgImage = nodeUISlot.GetComponentsInChildren<Image>()
				.FirstOrDefault(img => img != null && !img.IsRemoved &&
				                       img.Slot != null && !img.Slot.IsRemoved &&
				                       img.Slot.Name != "Connector" &&
				                       img.Slot.Name != "Shading" &&
				                       img.Slot.Name != "Overview" &&
				                       img.Slot.Name != "Header");

			if (bgImage != null && !bgImage.IsRemoved && bgImage.Slot != null && !bgImage.Slot.IsRemoved)
			{
				// Remove the sprite reference
				if (bgImage.Sprite.Target != null)
				{
					bgImage.Sprite.Target = null;
					Logger.LogUI("Relay Cleanup", "Removed sprite from background Image");
				}

				// Find and destroy the Shading child slot
				var shadingSlot = bgImage.Slot.FindChild("Shading");
				if (shadingSlot != null && !shadingSlot.IsRemoved)
				{
					shadingSlot.Destroy();
					Logger.LogUI("Relay Cleanup", "Destroyed Shading child slot");
				}
			}
		}
	}
}

