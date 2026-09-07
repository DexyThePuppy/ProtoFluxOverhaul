using System.Runtime.CompilerServices;
using FrooxEngine;

namespace ProtoFluxOverhaul;

/// <summary>Native synchronized selection, retaining actual provider references.</summary>
internal static class NativeAssetFallback
{
	private static class Bindings<T> where T : class, IAsset
	{
		internal static readonly ConditionalWeakTable<AssetRef<T>, ReferenceCopy<IAssetProvider<T>>> Copies = new();
	}

	internal static AssetRef<T> Create<T>(Slot slot, IAssetProvider<T> primary,
		IAssetProvider<T> fallback, AssetRef<T> fallbackSource = null) where T : class, IAsset
	{
		var selected = slot.GetComponentOrAttach<AssetProxy<T>>();
		var choose = slot.GetComponentOrAttach<BooleanAssetDriver<T>>();
		var exists = slot.GetComponentOrAttach<ReferenceEqualityDriver<IAssetProvider<T>>>();
		// AssetRefs notify the observer when their provider is destroyed; a plain
		// SyncRef only becomes null when read and does not supply that notification.
		if (choose.TrueTarget.Target != primary) choose.TrueTarget.Target = primary;
		Bind(slot, choose.FalseTarget, fallbackSource, fallback);
		if (!choose.State.IsDriven) choose.State.Value = primary != null;
		if (!selected.AssetReference.IsDriven)
			selected.AssetReference.Target = primary ?? fallbackSource?.Target ?? fallback;
		choose.Target.Target = selected.AssetReference;
		exists.TargetReference.Target = choose.TrueTarget;
		exists.Reference.Target = null;
		exists.Invert.Value = true;
		exists.Target.Target = choose.State;
		return selected.AssetReference;
	}

	private static ReferenceCopy<IAssetProvider<T>> FindCopy<T>(AssetRef<T> target) where T : class, IAsset
	{
		if (target == null || target.IsRemoved) return null;
		if (Bindings<T>.Copies.TryGetValue(target, out var cached))
		{
			if (!cached.IsRemoved && cached.Target.Target == target) return cached;
			// A user may retarget a component. Drop our association without moving
			// or destroying their driver when the old consumer is restyled.
			Bindings<T>.Copies.Remove(target);
		}
		// Native components can survive a world reload while the C# cache cannot.
		// Recover only copies sourced from our world-owned selector hierarchy.
		if (target.ActiveLink is RefDrive<IAssetProvider<T>> link
			&& link.Parent is ReferenceCopy<IAssetProvider<T>> copy && !copy.IsRemoved
			&& copy.Target == link && copy.Target.Target == target && !copy.WriteBack.Value
			&& copy.Source.Target is AssetRef<T> source && IsSelectorReference(source))
		{
			Bindings<T>.Copies.Add(target, copy);
			return copy;
		}
		return null;
	}

	internal static bool IsSelectorReference<T>(AssetRef<T> source) where T : class, IAsset
	{
		if (source == null || source.IsRemoved || source.Parent is not AssetProxy<T> proxy || proxy.AssetReference != source)
			return false;
		for (var slot = proxy.Slot; slot != null && !slot.IsRemoved; slot = slot.Parent)
		{
			if (slot.Name == "LiveOverrides" && slot.Parent?.Parent?.Name == "ProtoFluxOverhaul"
				&& slot.Parent.Parent.Parent?.Name == "__TEMP"
				&& slot.Parent.Parent.Parent.Parent == slot.World.RootSlot) return true;
		}
		return false;
	}

	internal static bool IsDrivenByUs<T>(AssetRef<T> target) where T : class, IAsset =>
		FindCopy(target) is { } copy && target.ActiveLink == copy.Target;

	internal static bool Release<T>(AssetRef<T> target) where T : class, IAsset
	{
		var copy = FindCopy(target);
		if (copy == null) return false;
		bool owned = target.ActiveLink == copy.Target;
		Bindings<T>.Copies.Remove(target);
		if (!copy.IsRemoved) copy.Destroy();
		return owned;
	}

	internal static bool Bind<T>(Slot owner, AssetRef<T> target, AssetRef<T> source,
		IAssetProvider<T> fallback) where T : class, IAsset
	{
		if (owner == null || owner.IsRemoved || target == null || target.IsRemoved) return false;
		if (target.IsDriven && !IsDrivenByUs(target)) return false;
		if (source == null || source.IsRemoved)
		{
			Release(target);
			if (target.Target != fallback) target.Target = fallback;
			return true;
		}
		var copy = FindCopy(target);
		if (copy == null)
		{
			Bindings<T>.Copies.Remove(target);
			copy = owner.AttachComponent<ReferenceCopy<IAssetProvider<T>>>();
			Bindings<T>.Copies.Add(target, copy);
		}
		// Initialize before driving so first use need not wait for OnChanges.
		if (!target.IsDriven) target.Target = source.Target ?? fallback;
		if (copy.Source.Target != source) copy.Source.Target = source;
		if (copy.Target.Target != target) copy.Target.Target = target;
		copy.WriteBack.Value = false;
		return true;
	}
}
