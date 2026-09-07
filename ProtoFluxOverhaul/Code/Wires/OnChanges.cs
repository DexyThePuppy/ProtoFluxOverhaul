using System;

using Elements.Core;

using FrooxEngine;
using FrooxEngine.ProtoFlux;

using HarmonyLib;

using Renderite.Shared;

using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	[HarmonyPatch(typeof(ProtoFluxWireManager), "OnChanges")]
	private class ProtoFluxWireManager_OnChanges_Patch
	{
		/// <summary>
		/// Helper to set up a ValueCopy component for driving a color field from a palette field.
		/// Attaches the ValueCopy to the PFO child slot for organization.
		/// Returns true if successfully linked, false if the target is already driven by something else.
		/// </summary>
		private static bool TryLinkWireColorCopy(Slot pfoSlot, IField<colorX> sourceField, IField<colorX> targetField)
		{
			if (pfoSlot == null || sourceField == null || targetField == null) return false;

			// Skip if already driven by another component
			if (targetField.IsDriven) return false;

			// Attach ValueCopy to the PFO slot
			var valueCopy = pfoSlot.AttachComponent<ValueCopy<colorX>>();

			// Link source and target
			valueCopy.Source.Target = sourceField;
			valueCopy.Target.Target = targetField;
			valueCopy.WriteBack.Value = false;

			return true;
		}

		private static bool TryIsOutputWire(ProtoFluxWireManager wire, StripeWireMesh wireMesh, out bool isOutput)
		{
			isOutput = false;
			if (wire == null) return false;

			// Prefer the explicit wire type if it is correctly set.
			if (wire.Type.Value == WireType.Output) { isOutput = true; return true; }
			if (wire.Type.Value == WireType.Input) { isOutput = false; return true; }

			// Fallback: infer from mesh tangent direction (engine Setup uses +/-X * TANGENT_MAGNITUDE).
			if (wireMesh != null)
			{
				try
				{
					isOutput = wireMesh.Tangent0.Value.x > 0f;
					return true;
				}
				catch { }
			}
			return false;
		}

		public static void Postfix(ProtoFluxWireManager __instance, SyncRef<MeshRenderer> ____renderer, SyncRef<StripeWireMesh> ____wireMesh)
		{
			try
			{
				// Skip if mod is disabled or required components are missing
				if (!Config.GetValue(ENABLED) ||
					__instance == null ||
					!__instance.Enabled ||
					____renderer?.Target == null ||
					____wireMesh?.Target == null ||
					__instance.Slot == null) return;

				// === User Permission Check ===
				if (!PermissionHelper.HasPermission(__instance))
				{
					// Skip silently for unauthorized wires to reduce log spam
					return;
				}

				// Get or create the PFO child slot for all additional mod components
				var pfoSlot = GetOrCreatePfoSlot(__instance.Slot);
				if (pfoSlot == null) return;

				var world = __instance.World;
				if (world == null) return;

				world.UpdateManager.NestCurrentlyUpdating((IUpdatable)__instance);
				try
				{
					// === Optional: override wire colors from PlatformColorPalette ===
					// Use ValueCopy to dynamically drive wire colors from the palette fields.
					// This ensures wire colors update automatically when the palette changes.
					if (Config.GetValue(USE_PLATFORM_COLOR_PALETTE) && !__instance.DeleteHighlight.Value)
					{
						// Skip if wire colors are already being driven by our ValueCopy components
						// (OnChanges is called repeatedly; we only need to set up once)
						if (__instance.StartColor.IsDriven || __instance.EndColor.IsDriven)
						{
							// Already set up - nothing to do
						}
						else
						{
							// Attach PlatformColorPalette to the PFO child slot
							var palette = pfoSlot.GetComponentOrAttach<PlatformColorPalette>();
							if (palette != null)
							{
								// Get the original wire colors (set by engine from connector type colors)
								// These are used to find the closest matching palette field
								// IMPORTANT: Read these BEFORE any ValueCopy is set up, otherwise we'd get the palette color
								colorX originalStartColor = __instance.StartColor.Value;
								colorX originalEndColor = __instance.EndColor.Value;

								// Find the closest matching palette FIELD for each end of the wire.
								// This preserves the gradient and correctly maps any type color
								// (float, float2, int, string, etc.) to its nearest palette equivalent.
								var startField = RoundedCornersHelper.FindClosestPaletteField(palette, originalStartColor);
								var endField = RoundedCornersHelper.FindClosestPaletteField(palette, originalEndColor);

								// Drive only StartColor/EndColor. Engine OnChanges copies those onto mesh Color0/Color1.
								if (startField != null)
									TryLinkWireColorCopy(pfoSlot, startField, __instance.StartColor);

								if (endField != null)
									TryLinkWireColorCopy(pfoSlot, endField, __instance.EndColor);
							}
						}
					}

					// === Material Setup ===
					var renderer = ____renderer?.Target;
					if (renderer == null) return;
					var stripeMesh = ____wireMesh?.Target;
					TryIsOutputWire(__instance, stripeMesh, out bool isOutputDir);
					var overrideRevision = UserAssetOverrides.GetRevision(world);

					if (TryWireVisualFastPath(pfoSlot, renderer, stripeMesh, isOutputDir, overrideRevision))
						return;

					var selectedMaterial = SharedAssets.GetSharedWireMaterialReference(world, isOutputDir);
					var material = selectedMaterial?.Target ?? SharedAssets.GetDefaultWireMaterial(world, isOutputDir);
					if (material == null)
						return;

					_materialCache[renderer] = material;

					SyncRef<IAssetProvider<Material>> materialReference = selectedMaterial;
					if (materialReference == null)
					{
						var materialSource = pfoSlot.GetComponentOrAttach<ReferenceField<IAssetProvider<Material>>>();
						if (materialSource.Reference.Target != material)
							materialSource.Reference.Target = material;
						materialReference = materialSource.Reference;
					}

					var materialCopy = pfoSlot.GetComponent<ReferenceCopy<IAssetProvider<Material>>>(copy =>
						copy.Target.Target == renderer.Material
						&& (NativeAssetFallback.IsSelectorReference(copy.Source.Target as AssetRef<Material>)
							|| copy.Source.Target?.Parent is ReferenceField<IAssetProvider<Material>> legacy
								&& legacy.Slot == pfoSlot && legacy.Reference == copy.Source.Target));
					if (!renderer.Material.IsDriven || materialCopy != null && renderer.Material.ActiveLink == materialCopy.Target)
					{
						materialCopy ??= pfoSlot.AttachComponent<ReferenceCopy<IAssetProvider<Material>>>();
						if (!renderer.Material.IsDriven) renderer.Material.Target = material;
						// The shared selector switches to its world-owned backup natively
						// if the avatar/provider is removed, including on unmodded clients.
						materialCopy.Source.Target = materialReference;
						materialCopy.Target.Target = renderer.Material;
						materialCopy.WriteBack.Value = false;
					}

					if (stripeMesh != null && stripeMesh.Profile.Value != ColorProfile.sRGB)
						stripeMesh.Profile.Value = ColorProfile.sRGB;

					// Migrate the per-wire animation component left by earlier versions.
					var oldPanner = pfoSlot.GetComponent<Panner2D>();
					if (oldPanner != null && !oldPanner.IsRemoved)
						oldPanner.Destroy();

					RememberWireVisualApplied(pfoSlot, isOutputDir, overrideRevision);
				}
				finally
				{
					world.UpdateManager.PopCurrentlyUpdating((IUpdatable)__instance);
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in ProtoFluxOverhaul OnChanges patch", e, LogCategory.UI);
			}
		}
	}
}
