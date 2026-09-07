using Elements.Core;

using FrooxEngine;

namespace ProtoFluxOverhaul
{
	public static partial class RoundedCornersHelper
	{
		public static bool TryLinkValueCopy<T>(ValueCopy<T> copy, IField<T> sourceField, IField<T> targetField)
		{
			if (copy == null || sourceField == null || targetField == null)
				return false;

			// Avoid stacking multiple drives on the same field
			if (targetField.IsDriven)
				return false;

			copy.Source.Target = sourceField;
			copy.Target.Target = targetField;
			copy.WriteBack.Value = false;
			return true;
		}

		/// <summary>
		/// Sets a color field without attaching <see cref="ValueField{T}"/> (an IValueSource
		/// that ProtoFlux input drop can pick up as a grab value).
		/// </summary>
		public static bool TrySetColorIfUndriven(IField<colorX> field, in colorX value)
		{
			if (field == null || field.IsDriven)
				return false;
			field.Value = value;
			return true;
		}
	}
}

