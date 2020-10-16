using System;

namespace WebShot.Menu.Menus
{
    public static class NumRange
    {
        public static NumRange<TInput> From<TInput>(TInput min, TInput max) where TInput : struct, IComparable<TInput> =>
            new(min, max);

        public static NumRange<TInput> AtLeast<TInput>(TInput min) where TInput : struct, IComparable<TInput> =>
            new(min, null);

        public static NumRange<TInput> AtMost<TInput>(TInput max) where TInput : struct, IComparable<TInput> =>
            new(null, max);

        public static NumRange<TInput> Any<TInput>() where TInput : struct, IComparable<TInput> =>
            new(null, null);

        public static readonly NumRange<int> AnyInt = new(null, null);
    }

    public class NumRange<T> where T : struct, IComparable<T>
    {
        public T? Min { get; set; }
        public T? Max { get; set; }

        public NumRange(T? min = null, T? max = null)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(T val) =>
            (!Min.HasValue || val.CompareTo(Min.Value) >= 0)
            && (!Max.HasValue || val.CompareTo(Max.Value) <= 0);

        public override string ToString()
        {
            var start = Min.HasValue ? Min.ToString() : double.NegativeInfinity.ToString();
            var end = Max.HasValue ? Max.ToString() : double.PositiveInfinity.ToString();
            return $"{start} <= value <= {end}";
        }
    }
}