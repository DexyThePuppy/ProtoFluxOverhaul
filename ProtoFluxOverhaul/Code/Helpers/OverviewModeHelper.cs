using System;
using System.Reflection;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using ProtoFlux.Core;
using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	// Helper class for overview mode access
	public static class OverviewModeHelper
	{
		public static bool GetOverviewMode(User user)
		{
			try
			{
				// First, try to get overview mode from any active ProtoFluxTool
				var activeTools = user.GetActiveTools();
				foreach (var tool in activeTools)
				{
					if (tool is ProtoFluxTool protoFluxTool)
					{
						// Use reflection to access the protected OverviewMode property
						var overviewModeProperty = typeof(ProtoFluxTool).GetProperty("OverviewMode",
							BindingFlags.NonPublic | BindingFlags.Instance);
						if (overviewModeProperty != null)
						{
							return (bool)overviewModeProperty.GetValue(protoFluxTool);
						}
					}
				}

				// Fallback: Access ProtofluxUserEditSettings directly (original method)
				var settings = user.GetComponent<ProtofluxUserEditSettings>();
				return settings != null && settings.OverviewMode.Value;
			}
			catch (Exception e)
			{
				Logger.LogError("Failed to get overview mode", e, LogCategory.UI);
				// Fallback to settings approach
				var settings = user.GetComponent<ProtofluxUserEditSettings>();
				return settings != null && settings.OverviewMode.Value;
			}
		}
	}
}

