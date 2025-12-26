using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;

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

		public static bool TryLinkValueDriver(ValueDriver<colorX> driver, IField<colorX> targetField, IValue<colorX> sourceValue)
		{
			if (driver == null || targetField == null || sourceValue == null)
				return false;

			if (targetField.IsDriven)
				return false;

			if (driver.DriveTarget.IsLinkValid)
			{
				if (driver.DriveTarget.Target == targetField && driver.ValueSource.Target == sourceValue)
					return true;
				return false;
			}

			driver.DriveTarget.Target = targetField;
			driver.ValueSource.Target = sourceValue;
			return true;
		}
	}
}

