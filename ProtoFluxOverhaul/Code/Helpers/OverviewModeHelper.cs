using System;
using System.Runtime.CompilerServices;

using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.UIX;

using static ProtoFluxOverhaul.Logger;

namespace ProtoFluxOverhaul
{
	public static class OverviewModeHelper
	{
		private static readonly ConditionalWeakTable<ProtoFluxNodeVisual, Image> CachedOverviewImages = new ConditionalWeakTable<ProtoFluxNodeVisual, Image>();

		internal static Image GetOverviewImage(ProtoFluxNodeVisual instance)
		{
			if (instance?.Slot == null) return null;

			if (CachedOverviewImages.TryGetValue(instance, out var cached) && cached != null && !cached.IsRemoved)
				return cached;

			var found = instance.Slot.GetComponentInChildren<Image>(static img => img.Slot.Name == "Overview");

			CachedOverviewImages.Remove(instance);
			if (found != null)
				CachedOverviewImages.Add(instance, found);

			return found;
		}

		public static bool GetOverviewMode(User user)
		{
			try
			{
				if (user == null) return false;
				var settings = user.GetComponent<ProtofluxUserEditSettings>();
				return settings != null && settings.OverviewMode.Value;
			}
			catch (Exception e)
			{
				Logger.LogError("Failed to get overview mode", e, LogCategory.UI);
				return false;
			}
		}
	}
}
