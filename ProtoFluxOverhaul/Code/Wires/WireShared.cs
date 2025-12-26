using System;
using System.Collections.Generic;
using FrooxEngine;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	// Internal organization slot name for all per-wire mod components
	private const string PfoWireSlotName = "PFO_WireOverhaul";

	private static readonly Dictionary<Slot, Panner2D> _pannerCache = new Dictionary<Slot, Panner2D>();
	private static readonly Dictionary<MeshRenderer, FresnelMaterial> _materialCache = new Dictionary<MeshRenderer, FresnelMaterial>();

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

	/// <summary>
	/// Creates or retrieves a texture with specified settings directly on the wire slot.
	/// </summary>
	private static StaticTexture2D GetOrCreateSharedTexture(Slot slot, Uri uri)
	{
		if (slot == null)
			throw new ArgumentNullException(nameof(slot));

		StaticTexture2D texture = slot.GetComponentOrAttach<StaticTexture2D>();
		texture.URL.Value = uri;

		texture.FilterMode.Value = Config.GetValue(FILTER_MODE);
		texture.MipMaps.Value = Config.GetValue(MIPMAPS);
		texture.Uncompressed.Value = Config.GetValue(UNCOMPRESSED);
		texture.CrunchCompressed.Value = Config.GetValue(CRUNCH_COMPRESSED);
		texture.DirectLoad.Value = Config.GetValue(DIRECT_LOAD);
		texture.ForceExactVariant.Value = Config.GetValue(FORCE_EXACT_VARIANT);
		texture.AnisotropicLevel.Value = Config.GetValue(ANISOTROPIC_LEVEL);
		texture.WrapModeU.Value = Config.GetValue(WRAP_MODE_U);
		texture.WrapModeV.Value = Config.GetValue(WRAP_MODE_V);
		texture.KeepOriginalMipMaps.Value = Config.GetValue(KEEP_ORIGINAL_MIPMAPS);
		texture.MipMapFilter.Value = Config.GetValue(MIPMAP_FILTER);
		texture.Readable.Value = Config.GetValue(READABLE);
		texture.PowerOfTwoAlignThreshold.Value = 0.05f;

		return texture;
	}
}

