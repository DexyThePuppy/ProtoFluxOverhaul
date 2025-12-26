using System;
using Elements.Assets;
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
		private static float ColorDistanceSq(in colorX a, in colorX b)
		{
			// Euclidean distance squared in RGB space
			float3 d = a.rgb - b.rgb;
			return d.x * d.x + d.y * d.y + d.z * d.z;
		}

		/// <summary>
		/// Finds the closest matching palette field for the given original color.
		/// Returns the IField from PlatformColorPalette so it can be used with ValueCopy for dynamic driving.
		///
		/// Note: Wire colors from proxies are multiplied by 1.5 (e.g., SYNC_FLOW becomes (1.05, 1.5, 1.5)).
		/// We normalize bright colors before comparison to ensure correct hue matching.
		///
		/// Includes all palette shades: Neutrals, Hero, Mid, Sub, and Dark.
		/// Uses RadiantUI_Constants for color matching (always available, even before PlatformColorPalette.OnStart runs).
		/// </summary>
		private static IField<colorX> FindClosestPaletteField(PlatformColorPalette palette, in colorX originalColor)
		{
			if (palette == null) return null;

			// Normalize the original color if any channel exceeds 1.0 (due to MulRGB(1.5f) in wire colors)
			// This ensures we match based on hue rather than brightness
			float maxChannel = MathX.Max(originalColor.r, MathX.Max(originalColor.g, originalColor.b));
			colorX normalizedOriginal = maxChannel > 1f
				? new colorX(originalColor.r / maxChannel, originalColor.g / maxChannel, originalColor.b / maxChannel, originalColor.a)
				: originalColor;

			// Build list of candidate colors using RadiantUI_Constants (static, always available)
			// paired with their corresponding palette fields for ValueCopy driving
			var candidates = new (colorX constantColor, IField<colorX> paletteField)[]
			{
				// Neutrals
				(RadiantUI_Constants.Neutrals.DARK, palette.Neutrals.Dark),
				(RadiantUI_Constants.Neutrals.MID, palette.Neutrals.Mid),
				(RadiantUI_Constants.Neutrals.MIDLIGHT, palette.Neutrals.MidLight),
				(RadiantUI_Constants.Neutrals.LIGHT, palette.Neutrals.Light),
				// Hero colors (brightest)
				(RadiantUI_Constants.Hero.YELLOW, palette.Hero.Yellow),
				(RadiantUI_Constants.Hero.GREEN, palette.Hero.Green),
				(RadiantUI_Constants.Hero.RED, palette.Hero.Red),
				(RadiantUI_Constants.Hero.PURPLE, palette.Hero.Purple),
				(RadiantUI_Constants.Hero.CYAN, palette.Hero.Cyan),
				(RadiantUI_Constants.Hero.ORANGE, palette.Hero.Orange),
				// Mid colors
				(RadiantUI_Constants.MidLight.YELLOW, palette.Mid.Yellow),
				(RadiantUI_Constants.MidLight.GREEN, palette.Mid.Green),
				(RadiantUI_Constants.MidLight.RED, palette.Mid.Red),
				(RadiantUI_Constants.MidLight.PURPLE, palette.Mid.Purple),
				(RadiantUI_Constants.MidLight.CYAN, palette.Mid.Cyan),
				(RadiantUI_Constants.MidLight.ORANGE, palette.Mid.Orange),
				// Sub colors
				(RadiantUI_Constants.Sub.YELLOW, palette.Sub.Yellow),
				(RadiantUI_Constants.Sub.GREEN, palette.Sub.Green),
				(RadiantUI_Constants.Sub.RED, palette.Sub.Red),
				(RadiantUI_Constants.Sub.PURPLE, palette.Sub.Purple),
				(RadiantUI_Constants.Sub.CYAN, palette.Sub.Cyan),
				(RadiantUI_Constants.Sub.ORANGE, palette.Sub.Orange),
				// Dark colors
				(RadiantUI_Constants.Dark.YELLOW, palette.Dark.Yellow),
				(RadiantUI_Constants.Dark.GREEN, palette.Dark.Green),
				(RadiantUI_Constants.Dark.RED, palette.Dark.Red),
				(RadiantUI_Constants.Dark.PURPLE, palette.Dark.Purple),
				(RadiantUI_Constants.Dark.CYAN, palette.Dark.Cyan),
				(RadiantUI_Constants.Dark.ORANGE, palette.Dark.Orange),
			};

			IField<colorX> closestField = null;
			float closestDistSq = float.MaxValue;

			foreach (var (constantColor, paletteField) in candidates)
			{
				float distSq = ColorDistanceSq(in normalizedOriginal, in constantColor);
				if (distSq < closestDistSq)
				{
					closestDistSq = distSq;
					closestField = paletteField;
				}
			}

			return closestField;
		}

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
				if (!HasPermission(__instance))
				{
					// Skip silently for unauthorized wires to reduce log spam
					return;
				}

				// Get or create the PFO child slot for all additional mod components
				var pfoSlot = GetOrCreatePfoSlot(__instance.Slot);
				if (pfoSlot == null) return;

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
							var wireMesh = ____wireMesh.Target;

							// Get the original wire colors (set by engine from connector type colors)
							// These are used to find the closest matching palette field
							// IMPORTANT: Read these BEFORE any ValueCopy is set up, otherwise we'd get the palette color
							colorX originalStartColor = __instance.StartColor.Value;
							colorX originalEndColor = __instance.EndColor.Value;

							// Find the closest matching palette FIELD for each end of the wire.
							// This preserves the gradient and correctly maps any type color
							// (float, float2, int, string, etc.) to its nearest palette equivalent.
							var startField = FindClosestPaletteField(palette, in originalStartColor);
							var endField = FindClosestPaletteField(palette, in originalEndColor);

							// Set up ValueCopy components to drive wire colors from palette fields
							// This creates a dynamic link - wire colors will update when palette changes
							// All ValueCopy components are attached to the PFO child slot
							if (startField != null)
							{
								TryLinkWireColorCopy(pfoSlot, startField, __instance.StartColor);
								// Also drive the mesh color for immediate visual update
								if (wireMesh != null)
									TryLinkWireColorCopy(pfoSlot, startField, wireMesh.Color0);
							}

							if (endField != null)
							{
								TryLinkWireColorCopy(pfoSlot, endField, __instance.EndColor);
								// Also drive the mesh color for immediate visual update
								if (wireMesh != null)
									TryLinkWireColorCopy(pfoSlot, endField, wireMesh.Color1);
							}
						}
					}
				}

				// === Material Setup ===
				var renderer = ____renderer?.Target;
				if (renderer == null) return;

				if (!_materialCache.TryGetValue(renderer, out var fresnelMaterial) || fresnelMaterial == null || fresnelMaterial.IsRemoved)
				{
					var originalMaterial = renderer.Material.Target as FresnelMaterial;
					if (originalMaterial == null)
					{
						return;
					}

					// Check if a custom material already exists on the PFO slot (for multiplayer safety)
					var existingMaterial = pfoSlot.GetComponent<FresnelMaterial>();
					if (existingMaterial != null)
					{
						// Use the existing custom material instead of creating a new one
						_materialCache[renderer] = existingMaterial;
						fresnelMaterial = existingMaterial;
					}
					else
					{
						// Create a new material on the PFO slot
						var newMaterial = pfoSlot.AttachComponent<FresnelMaterial>();
						newMaterial.NearColor.Value = colorX.White;
						newMaterial.FarColor.Value = colorX.White;
						newMaterial.Sidedness.Value = originalMaterial.Sidedness.Value;
						newMaterial.UseVertexColors.Value = originalMaterial.UseVertexColors.Value;
						newMaterial.BlendMode.Value = originalMaterial.BlendMode.Value;
						newMaterial.ZWrite.Value = originalMaterial.ZWrite.Value;
						newMaterial.NearTextureScale.Value = originalMaterial.NearTextureScale.Value;
						newMaterial.NearTextureOffset.Value = originalMaterial.NearTextureOffset.Value;
						newMaterial.FarTextureScale.Value = originalMaterial.FarTextureScale.Value;
						newMaterial.FarTextureOffset.Value = originalMaterial.FarTextureOffset.Value;

						_materialCache[renderer] = newMaterial;
						fresnelMaterial = newMaterial;
					}

					// Use ReferenceCopy to drive the renderer's material from our custom material
					// This creates a dynamic link instead of direct assignment
					if (!renderer.Material.IsDriven)
					{
						// Create ReferenceField and ReferenceCopy on the PFO slot
						var materialSource = pfoSlot.GetComponentOrAttach<ReferenceField<IAssetProvider<Material>>>();
						materialSource.Reference.Target = fresnelMaterial;

						// Create ReferenceCopy to drive the renderer's material
						var materialCopy = pfoSlot.GetComponentOrAttach<ReferenceCopy<IAssetProvider<Material>>>();
						materialCopy.Source.Target = materialSource.Reference;
						materialCopy.Target.Target = renderer.Material;
						materialCopy.WriteBack.Value = false;
					}
				}

				// === Wire Mesh Setup ===
				var stripeMesh = ____wireMesh?.Target;
				if (stripeMesh != null)
				{
					stripeMesh.Profile.Value = ColorProfile.sRGB;
				}

				// === Animation Setup ===
				// Get or create Panner2D for scrolling effect on the PFO slot
				if (!_pannerCache.TryGetValue(pfoSlot, out var panner))
				{
					panner = pfoSlot.GetComponentOrAttach<Panner2D>();
					_pannerCache[pfoSlot] = panner;
				}

				try
				{
					panner.Speed = Config.GetValue(SCROLL_SPEED);
					panner.Repeat = Config.GetValue(SCROLL_REPEAT);
				}
				catch (NullReferenceException)
				{
					Logger.LogWarning($"Skipping uninitialized Panner2D in patch for {__instance.Slot.Name}");
					return;
				}

				var baseSpeed = Config.GetValue(SCROLL_SPEED);
				// The engine wire Type isn't always reliable in modded contexts; infer direction from mesh if needed.
				bool isOutputDir = false;
				TryIsOutputWire(__instance, ____wireMesh.Target, out isOutputDir);
				bool flipDirection = !isOutputDir; // inputs flip
				float directionFactor = flipDirection ? -1f : 1f;
				panner.Speed = new float2(baseSpeed.x * directionFactor, baseSpeed.y);

				var farTexture = GetOrCreateSharedTexture(pfoSlot, Config.GetValue(WIRE_TEXTURE));
				fresnelMaterial.FarTexture.Target = farTexture;

				var nearTexture = GetOrCreateSharedTexture(pfoSlot, Config.GetValue(WIRE_TEXTURE));
				fresnelMaterial.NearTexture.Target = nearTexture;

				// === Texture Offset Setup ===
				// Setup texture offset drivers if they don't exist
				if (!fresnelMaterial.FarTextureOffset.IsLinked && panner.Target == null)
				{
					panner.Target = fresnelMaterial.FarTextureOffset;
				}

				if (!fresnelMaterial.NearTextureOffset.IsLinked)
				{
					// Create a ValueDriver on the PFO slot to link NearTextureOffset to the same panner output
					ValueDriver<float2> newNearDrive = pfoSlot.GetComponentOrAttach<ValueDriver<float2>>();

					if (!newNearDrive.DriveTarget.IsLinkValid && panner.Target != null)
					{
						newNearDrive.DriveTarget.Target = fresnelMaterial.NearTextureOffset;
						newNearDrive.ValueSource.Target = panner.Target;
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Error in ProtoFluxOverhaul OnChanges patch", e, LogCategory.UI);
			}
		}
	}
}

