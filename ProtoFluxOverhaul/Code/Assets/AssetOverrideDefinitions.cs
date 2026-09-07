using System;
using ResoniteModLoader;

namespace ProtoFluxOverhaul;

/// <summary>Named profile entries and their configuration fallbacks.</summary>
internal static class AssetOverrideDefinitions
{
	internal static readonly (string Role, ModConfigurationKey<Uri> ConfigKey, bool Clamp)[] Textures =
	{
		("Wire", ProtoFluxOverhaul.WIRE_TEXTURE, false),
		("Connector", ProtoFluxOverhaul.CONNECTOR_INPUT_TEXTURE, true),
		("CallInput", ProtoFluxOverhaul.CALL_CONNECTOR_INPUT_TEXTURE, true),
		("CallOutput", ProtoFluxOverhaul.CALL_CONNECTOR_OUTPUT_TEXTURE, true),
		("Vector1", ProtoFluxOverhaul.VECTOR_X1_CONNECTOR_TEXTURE, true),
		("Vector2", ProtoFluxOverhaul.VECTOR_X2_CONNECTOR_TEXTURE, true),
		("Vector3", ProtoFluxOverhaul.VECTOR_X3_CONNECTOR_TEXTURE, true),
		("NodeBackground", ProtoFluxOverhaul.NODE_BACKGROUND_TEXTURE, true),
		("NodeHeader", ProtoFluxOverhaul.NODE_BACKGROUND_HEADER_TEXTURE, true),
		("Shading", ProtoFluxOverhaul.SHADING_TEXTURE, true),
		("ShadingInverted", ProtoFluxOverhaul.SHADING_INVERTED_TEXTURE, true)
	};

	internal static readonly (string Role, ModConfigurationKey<Uri> ConfigKey)[] Sounds =
	{
		("Connect", ProtoFluxOverhaul.CONNECT_SOUND),
		("Delete", ProtoFluxOverhaul.DELETE_SOUND),
		("Grab", ProtoFluxOverhaul.GRAB_SOUND),
		("NodeCreate", ProtoFluxOverhaul.NODE_CREATE_SOUND),
		("NodeGrab", ProtoFluxOverhaul.NODE_GRAB_SOUND),
		("WireDrag", ProtoFluxOverhaul.WIRE_DRAG_SOUND)
	};
}
