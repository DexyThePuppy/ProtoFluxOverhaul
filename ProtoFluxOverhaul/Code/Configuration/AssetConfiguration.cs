using System.Collections.Generic;
using ResoniteModLoader;
using static ProtoFluxOverhaul.ProtoFluxOverhaul;

namespace ProtoFluxOverhaul;

/// <summary>Only changes to asset settings invalidate world asset caches.</summary>
internal static class AssetConfiguration
{
	private static readonly HashSet<ModConfigurationKey> VisualKeys = CreateVisualKeys();
	private static readonly HashSet<ModConfigurationKey> AudioKeys = CreateAudioKeys();

	internal static bool AffectsVisualAssets(ModConfigurationKey key) => key != null && VisualKeys.Contains(key);
	internal static bool AffectsAudioAssets(ModConfigurationKey key) => key != null && AudioKeys.Contains(key);

	private static HashSet<ModConfigurationKey> CreateVisualKeys()
	{
		var keys = new HashSet<ModConfigurationKey>
		{
			SCROLL_SPEED, SCROLL_REPEAT, ANISOTROPIC_LEVEL, MIPMAPS, KEEP_ORIGINAL_MIPMAPS,
			MIPMAP_FILTER, UNCOMPRESSED, DIRECT_LOAD, FORCE_EXACT_VARIANT, CRUNCH_COMPRESSED,
			PREFERRED_FORMAT, READABLE, FILTER_MODE, WRAP_MODE_U, WRAP_MODE_V, PREFERRED_PROFILE
		};
		foreach (var definition in AssetOverrideDefinitions.Textures) keys.Add(definition.ConfigKey);
		return keys;
	}

	private static HashSet<ModConfigurationKey> CreateAudioKeys()
	{
		var keys = new HashSet<ModConfigurationKey>();
		foreach (var definition in AssetOverrideDefinitions.Sounds) keys.Add(definition.ConfigKey);
		return keys;
	}
}
