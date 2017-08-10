using System;
using System.Windows;

namespace BezierBorders
{
    public static class DoubleUtil
    {
        internal const double Epsilon = 2.2204460492503131e-016;
        public static bool IsZero(double value)
        {
            return Math.Abs(value) < 10.0 * Epsilon;
        }

        public static bool AreClose(double value1, double value2)
        {
            if (value1 == value2)
                return true;
            double num1 = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * Epsilon;
            double num2 = value1 - value2;
            if (-num1 < num2)
                return num1 > num2;
            return false;
        }

        public static bool LessThan(double value1, double value2)
        {
            if (value1 < value2)
                return !AreClose(value1, value2);
            return false;
        }

        public static bool GreaterThan(double value1, double value2)
        {
            if (value1 > value2)
                return !AreClose(value1, value2);
            return false;
        }

        public static double RoundLayoutValue(double value, double dpiScale)
        {
            double d;
            if (!AreClose(dpiScale, 1.0))
            {
                d = Math.Round(value * dpiScale) / dpiScale;
                if (Double.IsNaN(d) || double.IsInfinity(d) || AreClose(d, double.MaxValue))
                    d = value;
            }
            else
                d = Math.Round(value);
            return d;
        }

        public static bool IsUniform(Thickness border)
        {
            if (AreClose(border.Left, border.Top) && AreClose(border.Left, border.Right))
            {
                return AreClose(border.Left, border.Bottom);
            }
            return false;
        }

        public static bool IsBorderZero(Thickness border)
        {
            if (IsZero(border.Left) && IsZero(border.Top) && IsZero(border.Right))
            {
                return IsZero(border.Bottom);
            }
            return false;
        }
    }
}