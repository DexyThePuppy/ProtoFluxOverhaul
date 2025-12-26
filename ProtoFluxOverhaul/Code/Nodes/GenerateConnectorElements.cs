using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;
using HarmonyLib;
using ProtoFlux.Core;

namespace ProtoFluxOverhaul
{
	/// <summary>
	/// Harmony patches for dynamically-created ProtoFlux connector elements.
	///
	/// Note: These must remain separate patch classes because they target different engine methods,
	/// but they can live together in one file and share a universal implementation
	/// via <see cref="DynamicConnectorStyler"/>.
	/// </summary>
	internal static class GenerateConnectorElements
	{
		[HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateInputElement")]
		internal sealed class GenerateInputElement_Patch
		{
			public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui)
			{
				DynamicConnectorStyler.StyleDynamicConnector(
					__instance,
					ui,
					isOutput: false,
					styleLabelBackground: true,
					logContext: "Dynamic Input"
				);
			}
		}

		[HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateOutputElement")]
		internal sealed class GenerateOutputElement_Patch
		{
			public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui)
			{
				DynamicConnectorStyler.StyleDynamicConnector(
					__instance,
					ui,
					isOutput: true,
					styleLabelBackground: true,
					logContext: "Dynamic Output"
				);
			}
		}

		[HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateOperationElement")]
		internal sealed class GenerateOperationElement_Patch
		{
			public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, INodeOperation operation, string name, bool isAsync)
			{
				DynamicConnectorStyler.StyleDynamicConnector(
					__instance,
					ui,
					isOutput: false,
					impulseTypeOverride: null,
					isOperationOverride: true,
					isAsyncOverride: isAsync,
					styleLabelBackground: false,
					logContext: "Dynamic Operation"
				);
			}
		}

		[HarmonyPatch(typeof(ProtoFluxNodeVisual), "GenerateImpulseElement")]
		internal sealed class GenerateImpulseElement_Patch
		{
			public static void Postfix(ProtoFluxNodeVisual __instance, UIBuilder ui, ISyncRef input, string name, ImpulseType type)
			{
				DynamicConnectorStyler.StyleDynamicConnector(
					__instance,
					ui,
					isOutput: true,
					impulseTypeOverride: type,
					isOperationOverride: false,
					isAsyncOverride: false,
					styleLabelBackground: false,
					logContext: "Dynamic Impulse"
				);
			}
		}
	}
}

