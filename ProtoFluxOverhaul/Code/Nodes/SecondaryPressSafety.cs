using System;
using FrooxEngine.ProtoFlux;
using HarmonyLib;

namespace ProtoFluxOverhaul;

public partial class ProtoFluxOverhaul
{
	/// <summary>
	/// Small shared guard flags for tool interactions.
	/// </summary>
	internal static class ProtoFluxToolInteractionGuards
	{
		internal static volatile bool IsSecondaryPressing;
	}

	/// <summary>
	/// Resonite sometimes throws a NullReferenceException in ProtoFluxTool.OnSecondaryPress
	/// (from ValueInput&lt;T&gt;.IInput.set_BoxedValue). This patch prevents the exception from
	/// bubbling and also marks a short-lived "secondary press in progress" flag to avoid
	/// our own patches mutating visuals mid-interaction.
	/// </summary>
	[HarmonyPatch(typeof(ProtoFluxTool), "OnSecondaryPress")]
	public sealed class ProtoFluxTool_OnSecondaryPressSafety_Patch
	{
		private static bool _loggedOnce;

		[HarmonyPrefix]
		public static void Prefix()
		{
			ProtoFluxToolInteractionGuards.IsSecondaryPressing = true;
		}

		[HarmonyPostfix]
		public static void Postfix()
		{
			ProtoFluxToolInteractionGuards.IsSecondaryPressing = false;
		}

		[HarmonyFinalizer]
		public static Exception Finalizer(Exception __exception)
		{
			// Always clear the flag even if the method throws.
			ProtoFluxToolInteractionGuards.IsSecondaryPressing = false;

			if (__exception is not NullReferenceException) return __exception;

			// Only swallow the known engine-side NRE signature.
			string st = __exception.StackTrace ?? string.Empty;
			if (!st.Contains("ValueInput`1") || !st.Contains("IInput.set_BoxedValue"))
				return __exception;

			if (!_loggedOnce)
			{
				_loggedOnce = true;
				Logger.LogError(
					"Suppressed engine NullReferenceException in ProtoFluxTool.OnSecondaryPress (ValueInput<T>.IInput.set_BoxedValue).",
					__exception,
					Logger.LogCategory.UI
				);
			}

			return null; // swallow
		}
	}
}

