using System;
using System.Globalization;

namespace VIEWORKS_AFTS
{
    /// <summary>
    /// 수평/수직 Span 구간에 포함된 신호의 통계 메트릭 데이터 구조체.
    /// </summary>
    public struct ScopeSpanMetrics
    {
        public bool HasValidData;
        public string Key;
        public string DisplayName;
        public string UnitX;
        public string UnitY;

        public double X1;
        public double X2;
        public double DeltaX;

        public double YStart;
        public double YEnd;
        public double YMin;
        public double YMax;
        public double DeltaY;       // YEnd - YStart
        public double PeakToValley; // YMax - YMin
        public double YMean;
        public int SampleCount;

        // Active Axis Vertical Span Cursor Metrics
        public bool HasYSpan;
        public double YSpan1;
        public double YSpan2;
        public double DeltaYSpan;   // Math.Abs(YSpan2 - YSpan1)

        public static ScopeSpanMetrics Empty => new ScopeSpanMetrics
        {
            HasValidData = false,
            HasYSpan = false,
            Key = string.Empty,
            DisplayName = string.Empty,
            UnitX = string.Empty,
            UnitY = string.Empty
        };
    }

    /// <summary>
    /// ScopeSpanMetrics 구조체를 UI 라벨 텍스트로 깔끔하게 변환하는 순수 정적 포맷터.
    /// Vieworks Exhibition(ProfileLegendPanel) 다크 엔지니어링 디자인 패턴 준수.
    /// </summary>
    public static class ScopeSpanFormatter
    {
        public static string FormatDeltaX(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || double.IsNaN(m.DeltaX) || double.IsInfinity(m.DeltaX))
                return "ΔX: --";

            string unit = string.IsNullOrEmpty(m.UnitX) ? "" : " " + m.UnitX;
            double dx = Math.Abs(m.DeltaX);
            return string.Format(CultureInfo.InvariantCulture, "ΔX: {0:F3}{1}", dx, unit);
        }

        public static string FormatRangeX(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || double.IsNaN(m.X1) || double.IsNaN(m.X2) || double.IsInfinity(m.X1) || double.IsInfinity(m.X2))
                return "X Range: [ -- ~ -- ]";

            string unit = string.IsNullOrEmpty(m.UnitX) ? "" : " " + m.UnitX;
            double xMin = Math.Min(m.X1, m.X2);
            double xMax = Math.Max(m.X1, m.X2);
            return string.Format(CultureInfo.InvariantCulture, "X Range: [{0:F3} ~ {1:F3}{2}]", xMin, xMax, unit);
        }

        public static string FormatDeltaY(ScopeSpanMetrics m)
        {
            if (!m.HasValidData)
                return "ΔY: --";

            string unit = string.IsNullOrEmpty(m.UnitY) ? "" : " " + m.UnitY;

            if (m.HasYSpan && !double.IsNaN(m.DeltaYSpan) && !double.IsInfinity(m.DeltaYSpan))
            {
                double dy = Math.Abs(m.DeltaYSpan);
                return string.Format(CultureInfo.InvariantCulture, "ΔY: {0:F3}{1}", dy, unit);
            }

            if (!double.IsNaN(m.PeakToValley) && !double.IsInfinity(m.PeakToValley))
            {
                double dy = Math.Abs(m.PeakToValley);
                return string.Format(CultureInfo.InvariantCulture, "ΔY: {0:F3}{1}", dy, unit);
            }

            return "ΔY: --";
        }

        public static string FormatMinMax(ScopeSpanMetrics m)
        {
            if (!m.HasValidData)
                return "Y Range: [ -- ~ -- ]";

            string unit = string.IsNullOrEmpty(m.UnitY) ? "" : " " + m.UnitY;

            if (m.HasYSpan && !double.IsNaN(m.YSpan1) && !double.IsNaN(m.YSpan2) && !double.IsInfinity(m.YSpan1) && !double.IsInfinity(m.YSpan2))
            {
                double y1 = Math.Min(m.YSpan1, m.YSpan2);
                double y2 = Math.Max(m.YSpan1, m.YSpan2);
                return string.Format(CultureInfo.InvariantCulture, "Y Span: [{0:F3} ~ {1:F3}{2}]", y1, y2, unit);
            }

            if (!double.IsNaN(m.YMin) && !double.IsNaN(m.YMax) && !double.IsInfinity(m.YMin) && !double.IsInfinity(m.YMax))
            {
                return string.Format(CultureInfo.InvariantCulture, "Y Range: [{0:F3} ~ {1:F3}{2}]", m.YMin, m.YMax, unit);
            }

            return "Y Range: [ -- ~ -- ]";
        }

        public static string FormatMin(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || double.IsNaN(m.YMin) || double.IsInfinity(m.YMin))
                return "Min: --";

            string unit = string.IsNullOrEmpty(m.UnitY) ? "" : " " + m.UnitY;
            return string.Format(CultureInfo.InvariantCulture, "Min: {0:F3}{1}", m.YMin, unit);
        }

        public static string FormatMax(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || double.IsNaN(m.YMax) || double.IsInfinity(m.YMax))
                return "Max: --";

            string unit = string.IsNullOrEmpty(m.UnitY) ? "" : " " + m.UnitY;
            return string.Format(CultureInfo.InvariantCulture, "Max: {0:F3}{1}", m.YMax, unit);
        }

        public static string FormatMean(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || double.IsNaN(m.YMean) || double.IsInfinity(m.YMean))
                return "Mean: --";

            string unit = string.IsNullOrEmpty(m.UnitY) ? "" : " " + m.UnitY;
            return string.Format(CultureInfo.InvariantCulture, "Mean: {0:F3}{1}", m.YMean, unit);
        }

        public static string FormatSamples(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || m.SampleCount < 0)
                return "Samples: 0 pts";

            return string.Format(CultureInfo.InvariantCulture, "Samples: {0:N0} pts", m.SampleCount);
        }

        public static string FormatDeltaYEndStart(ScopeSpanMetrics m)
        {
            if (!m.HasValidData || double.IsNaN(m.DeltaY) || double.IsInfinity(m.DeltaY))
                return "ΔY (End-Start): --";

            string unit = string.IsNullOrEmpty(m.UnitY) ? "" : " " + m.UnitY;
            return string.Format(CultureInfo.InvariantCulture, "ΔY (End-Start): {0:F3}{1}", m.DeltaY, unit);
        }
    }

    public enum ScopePlotAxis
    {
        Left,
        Right
    }

    public enum ScopeXAxisMode
    {
        ScanPosition,
        TimeStep
    }

    public struct ScopeSlotInfo
    {
        public int SlotIndex;
        public bool IsBound;
        public bool IsBaseline;
        public string BoundFileName;
        public string BoundFullPath;
        public string CanonicalSignal;
        public double[] NativeXData;
        public double[] DataY;
        public System.Drawing.Color Color;
        public ScopePlotAxis AssignedAxis;
        public ScopeXAxisMode NativeXAxisMode;

        public static ScopeSlotInfo CreateEmpty(int slotIndex, System.Drawing.Color defaultColor)
        {
            return new ScopeSlotInfo
            {
                SlotIndex = slotIndex,
                IsBound = false,
                IsBaseline = false,
                BoundFileName = null,
                BoundFullPath = null,
                CanonicalSignal = null,
                NativeXData = null,
                DataY = null,
                Color = defaultColor,
                AssignedAxis = ScopePlotAxis.Left,
                NativeXAxisMode = ScopeXAxisMode.ScanPosition
            };
        }
    }

}
