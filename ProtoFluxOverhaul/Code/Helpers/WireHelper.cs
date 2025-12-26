using FrooxEngine;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Helper class for wire-related functionality
	public static class WireHelper
	{
		public static void CreateAudioClipsSlot(Slot wirePointSlot)
		{
			if (wirePointSlot == null || wirePointSlot.World == null) return;

			// Initialize sounds using ProtoFluxSounds
			ProtoFluxSounds.Initialize(wirePointSlot.World);
		}

		public static void FindAndSetupWirePoints(Slot rootSlot)
		{
			if (rootSlot == null) return;

			// Find the Overlapping Layout section
			var overlappingLayout = rootSlot.FindChild("Overlapping Layout");
			if (overlappingLayout == null) return;

			// Check Inputs & Operations section
			var inputsAndOperations = overlappingLayout.FindChild("Inputs & Operations");
			if (inputsAndOperations != null)
			{
				foreach (var connectorSlot in inputsAndOperations.GetComponentsInChildren<Slot>())
				{
					if (connectorSlot.Name == "Connector")
					{
						var wirePoint = connectorSlot.FindChild("<WIRE_POINT>");
						if (wirePoint != null)
						{
							Logger.LogWire("Setup", $"Found input wire point in {connectorSlot.Parent?.Name ?? "unknown"}");
							CreateAudioClipsSlot(wirePoint);
						}
					}
				}
			}

			// Check Outputs & Impulses section
			var outputsAndImpulses = overlappingLayout.FindChild("Outputs & Impulses");
			if (outputsAndImpulses != null)
			{
				foreach (var connectorSlot in outputsAndImpulses.GetComponentsInChildren<Slot>())
				{
					if (connectorSlot.Name == "Connector")
					{
						var wirePoint = connectorSlot.FindChild("<WIRE_POINT>");
						if (wirePoint != null)
						{
							Logger.LogWire("Setup", $"Found output wire point in {connectorSlot.Parent?.Name ?? "unknown"}");
							CreateAudioClipsSlot(wirePoint);
						}
					}
				}
			}
		}
	}
}

