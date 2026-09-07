using System.Collections.Generic;
using FrooxEngine;
using Renderite.Shared;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	// Internal organization slot name for all per-wire mod components
	private const string PfoWireSlotName = "PFO_WireOverhaul";

	private static readonly Dictionary<MeshRenderer, IAssetProvider<Material>> _materialCache = new Dictionary<MeshRenderer, IAssetProvider<Material>>();

	/// <summary>
	/// Last wire visual state applied on a PFO slot (avoids redundant sync writes; OnChanges is transform-hot).
	/// </summary>
	private struct WireVisualAppliedState
	{
		public bool IsOutput;
		public int AssetVersion;
		public int OverrideRevision;
	}

	private static readonly Dictionary<Slot, WireVisualAppliedState> _wireVisualApplied = new Dictionary<Slot, WireVisualAppliedState>();

	/// <summary>
	/// Gets or creates the child slot for all ProtoFluxOverhaul components on a wire.
	/// </summary>
	private static Slot GetOrCreatePfoSlot(Slot wireSlot)
	{
		if (wireSlot == null) return null;
		return wireSlot.FindChild(PfoWireSlotName) ?? wireSlot.AddSlot(PfoWireSlotName);
	}

	/// <summary>
	/// Finds the child slot for all ProtoFluxOverhaul components on a wire (does not create it).
	/// </summary>
	private static Slot FindPfoSlot(Slot wireSlot)
	{
		if (wireSlot == null) return null;
		return wireSlot.FindChild(PfoWireSlotName);
	}

	private static bool TryWireVisualFastPath(
		Slot pfoSlot,
		MeshRenderer renderer,
		StripeWireMesh stripeMesh,
		bool isOutput,
		int overrideRevision)
	{
		if (pfoSlot == null || renderer == null || stripeMesh == null)
			return false;

		if (!_wireVisualApplied.TryGetValue(pfoSlot, out var applied))
			return false;

		if (applied.IsOutput != isOutput || applied.AssetVersion != SharedAssets.Version || applied.OverrideRevision != overrideRevision)
			return false;
		if (stripeMesh.Profile.Value != ColorProfile.sRGB)
			return false;
		if (!_materialCache.TryGetValue(renderer, out var material) || material == null || material.IsRemoved)
			return false;

		return renderer.Material.Target == material;
	}

	private static void RememberWireVisualApplied(Slot pfoSlot, bool isOutput, int overrideRevision)
	{
		if (pfoSlot == null) return;
		_wireVisualApplied[pfoSlot] = new WireVisualAppliedState
		{
			IsOutput = isOutput,
			AssetVersion = SharedAssets.Version,
			OverrideRevision = overrideRevision,
		};
	}
}

