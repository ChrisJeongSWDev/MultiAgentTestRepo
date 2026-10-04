using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;

namespace VIEWORKS_AFTS
{
    public enum SpanAxis
    {
        X,
        Y
    }

    public class PlotSpanManager
    {
        private readonly FormsPlot _plotControl;

        private HorizontalSpan _xSpan;
        private VerticalSpan _ySpan;

        private AxisSpanUnderMouse _draggingSpan;

        public bool Enabled { get; set; } = true;

        public PlotSpanManager(FormsPlot plotControl)
        {
            _plotControl = plotControl;

            _plotControl.MouseDown += OnMouseDown;
            _plotControl.MouseMove += OnMouseMove;
            _plotControl.MouseUp += OnMouseUp;
        }

        public void SetXSpan(double x1, double x2)
        {
            if (_xSpan == null)
            {
                _xSpan = _plotControl.Plot.Add.HorizontalSpan(x1, x2);
                ConfigureSpan(_xSpan);
            }
            else
            {
                _xSpan.X1 = x1;
                _xSpan.X2 = x2;
            }

            _plotControl.Refresh();
        }

        public void SetYSpan(double y1, double y2)
        {
            if (_ySpan == null)
            {
                _ySpan = _plotControl.Plot.Add.VerticalSpan(y1, y2);
                ConfigureSpan(_ySpan);
            }
            else
            {
                _ySpan.Y1 = y1;
                _ySpan.Y2 = y2;
            }

            _plotControl.Refresh();
        }

        public void SetCenter(SpanAxis axis, double center, double width)
        {
            double half = width / 2.0;

            if (axis == SpanAxis.X)
                SetXSpan(center - half, center + half);
            else
                SetYSpan(center - half, center + half);
        }

        public void Clear(SpanAxis axis)
        {
            if (axis == SpanAxis.X && _xSpan != null)
            {
                _plotControl.Plot.Remove(_xSpan);
                _xSpan = null;
            }

            if (axis == SpanAxis.Y && _ySpan != null)
            {
                _plotControl.Plot.Remove(_ySpan);
                _ySpan = null;
            }

            _plotControl.Refresh();
        }

        public void ClearAll()
        {
            Clear(SpanAxis.X);
            Clear(SpanAxis.Y);
        }

        public void ResetReferences()
        {
            _xSpan = null;
            _ySpan = null;
            _draggingSpan = null;
            _plotControl.Cursor = Cursors.Default;
            _plotControl.UserInputProcessor.Enable();
        }

        private void ConfigureSpan(AxisSpan span)
        {
            span.IsDraggable = true;
            span.IsResizable = true;
            span.EnableAutoscale = false;

            span.FillStyle.Color = ScottPlot.Colors.CornflowerBlue.WithAlpha(.25);
            span.LineStyle.Color = ScottPlot.Colors.CornflowerBlue;
            span.LineStyle.Width = 2;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            _draggingSpan = GetSpanUnderMouse(e.X, e.Y);

            if (_draggingSpan != null)
                _plotControl.UserInputProcessor.Disable();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (_draggingSpan != null)
            {
                IXAxis xAxis = _draggingSpan.Span.Axes.XAxis ?? _plotControl.Plot.Axes.Bottom;
                IYAxis yAxis = _draggingSpan.Span.Axes.YAxis ?? _plotControl.Plot.Axes.Left;
                Coordinates mouseNow = _plotControl.Plot.GetCoordinates(e.X, e.Y, xAxis, yAxis);
                _draggingSpan.DragTo(mouseNow);
                _plotControl.Refresh();
                return;
            }

            var span = GetSpanUnderMouse(e.X, e.Y);

            if (span == null)
            {
                if (_plotControl.Cursor != Cursors.Default)
                    _plotControl.Cursor = Cursors.Default;
            }
            else if (span.IsResizingHorizontally)
                _plotControl.Cursor = Cursors.SizeWE;
            else if (span.IsResizingVertically)
                _plotControl.Cursor = Cursors.SizeNS;
            else
                _plotControl.Cursor = Cursors.SizeAll;
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (_draggingSpan == null)
                return;

            _draggingSpan = null;
            _plotControl.UserInputProcessor.Enable();
            _plotControl.Cursor = Cursors.Default;
            _plotControl.Refresh();
        }

        private AxisSpanUnderMouse GetSpanUnderMouse(float x, float y)
        {
            foreach (var span in _plotControl.Plot.GetPlottables<AxisSpan>().Reverse())
            {
                IXAxis xAxis = span.Axes.XAxis ?? _plotControl.Plot.Axes.Bottom;
                IYAxis yAxis = span.Axes.YAxis ?? _plotControl.Plot.Axes.Left;
                CoordinateRect rect = _plotControl.Plot.GetCoordinateRect(x, y, 10, xAxis, yAxis);
                var result = span.UnderMouse(rect);
                if (result != null)
                    return result;
            }

            return null;
        }
    }

    public sealed class PlotInfoAnnotation
    {
        private readonly FormsPlot _plotControl;
        private Annotation _annotation;

        public PlotInfoAnnotation(FormsPlot plotControl)
        {
            _plotControl = plotControl;
        }

        public void EnsureCreated()
        {
            if (_annotation != null)
                return;

            _annotation = _plotControl.Plot.Add.Annotation("", Alignment.UpperLeft);
            _annotation.IsVisible = false;
            _annotation.LabelFontSize = 12;
            _annotation.LabelFontColor = ScottPlot.Color.FromColor(System.Drawing.Color.White);
            _annotation.LabelBackgroundColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(180, 40, 40, 40));
            _annotation.LabelBorderColor = ScottPlot.Color.FromColor(System.Drawing.Color.Black);
            _annotation.LabelBorderWidth = 2;
            _annotation.LabelPadding = 6;
            _annotation.OffsetX = 10;
            _annotation.OffsetY = 10;
        }

        public void Show(string text, bool refresh = true)
        {
            EnsureCreated();
            _annotation.Text = text ?? "";
            _annotation.IsVisible = !string.IsNullOrWhiteSpace(text);

            if (refresh)
                _plotControl.Refresh();
        }

        public void Hide(bool refresh = true)
        {
            if (_annotation == null)
                return;

            _annotation.IsVisible = false;

            if (refresh)
                _plotControl.Refresh();
        }

        public void Reset()
        {
            _annotation = null;
        }
    }

    public sealed class ScottPlotLibrary
    {
        public struct SpanChannelMetrics
        {
            public double SpanXMin;
            public double SpanXMax;
            public double SpanXSpan;
            public double SpanXCenter;

            public double SpanYMin;
            public double SpanYMax;
            public double SpanYSpan;
            public double SpanYCenter;

            public int IdxMin;
            public int IdxMax;

            public double YStart;
            public double YEnd;
            public double YDelta;
            public double YDeltaAbs;

            public double YMin;
            public double YMax;
            public double YRange;
            public double YMean;
        }

        // Scope 다중 CSV 오버레이에서 사용하는 4개의 표준(정규화) 신호 식별자.
        // AF_Diff는 Left Y축(µm), AF_ZPosition/ScanMotion은 Right Y축(mm)에 매핑된다.
        public const string SignalAFZPosition = "AF_ZPosition";
        public const string SignalScanMotion = "ScanMotion";
        public const string SignalTime = "Time";
        public const string SignalAFDiff = "AF_Diff";

        private const float HighlightLineWidthMultiplier = 2.0f;

        public struct ScopePlotSummary
        {
            public string Key;
            public string Label;
            public string CanonicalSignal;
            public System.Drawing.Color Color;
            public bool IsVisible;
            public bool IsHighlighted;
            public bool IsRightAxis;
        }

        public FormsPlot ScottPlotForm { get; } = new FormsPlot() { Dock = DockStyle.Fill };
        public FormsPlot PlotControl { get { return ScottPlotForm; } }

        public PlotSpanManager Span { get; private set; }
        public PlotInfoAnnotation InfoAnnotation { get; private set; }

        public ScottPlotLibrary()
        {
            Span = new PlotSpanManager(ScottPlotForm);
            Span.Enabled = false;
            InfoAnnotation = new PlotInfoAnnotation(ScottPlotForm);
            ConfigureDefaultStyle();
            ConfigureDefaultMouse();
            ScottPlotForm.MouseDown += InternalSpanMouseDown;
            ScottPlotForm.MouseMove += InternalSpanMouseMove;
            ScottPlotForm.MouseUp += InternalSpanMouseUp;
            ScottPlotForm.MouseWheel += InternalMouseWheelZoom;
        }

        private sealed class SeriesState
        {
            public double[] X = Array.Empty<double>();
            public double[] Y = Array.Empty<double>();
            public Scatter Scatter;
            public IYAxis YAxis;
            public string Label = "";
        }

        private sealed class PlotEntry
        {
            public Scatter Scatter;
            public double[] Xs = Array.Empty<double>();
            public double[] Ys = Array.Empty<double>();
            public System.Drawing.Color BaseColor;
            public float BaseLineWidth = 2.0f;
            public string CanonicalSignal;
            public bool IsRightAxis = false;
            public bool IsVisible = true;
            public bool IsHighlighted = false;
        }

        private SeriesState[] _series = Array.Empty<SeriesState>();
        private double[] _sharedX = Array.Empty<double>();
        private int _channelCount = 0;

        private readonly Dictionary<string, PlotEntry> _plots = new Dictionary<string, PlotEntry>(StringComparer.OrdinalIgnoreCase);

        public IXAxis XAxisBottom { get { return ScottPlotForm.Plot.Axes.Bottom; } }
        public IYAxis YAxisLeft { get { return ScottPlotForm.Plot.Axes.Left; } }
        public IYAxis YAxisRight { get { return ScottPlotForm.Plot.Axes.Right; } }

        private HorizontalSpan _timeSpan;
        private VerticalSpan _valueSpanLeft;
        private VerticalSpan _valueSpanRight;

        public AxisSpanUnderMouse SpanBeingDragged = null;
        private AxisSpanUnderMouse _draggingSpanInternal = null;
        private AxisSpan _draggingAxisSpan = null;

        private System.Drawing.Color _hSpanColor = System.Drawing.Color.LightBlue;
        private byte _hSpanAlpha = 50;
        private System.Drawing.Color _vSpanLeftColor = System.Drawing.Color.OrangeRed;
        private byte _vSpanLeftAlpha = 40;
        private System.Drawing.Color _vSpanRightColor = System.Drawing.Color.YellowGreen;
        private byte _vSpanRightAlpha = 40;

        private bool _hasLockedRightAxisLimits = false;
        private double _lockedRightMin = 0;
        private double _lockedRightMax = 255;

        private MouseEventHandler _mouseDown;
        private MouseEventHandler _mouseUp;
        private MouseEventHandler _mouseMove;
        private Func<Coordinates, string> _mouseValueAnnotationTextProvider;
        private Func<string> _defaultValueAnnotationTextProvider;
        private bool _mouseValueAnnotationEnabled = false;
        private bool _keepDefaultAnnotationOnMouseLeave = true;

        private bool _panLocked = false;

        public AxisLimits DataAxisLimitLeft { get; private set; } = new AxisLimits();
        public AxisLimits DataAxisLimitRight { get; private set; } = new AxisLimits();

        public int MaxRefreshFps { get; set; } = 60;
        private long _lastRefreshTicks = 0;

        public void AttachToPanel(Panel hostPanel)
        {
            if (hostPanel == null)
                return;

            if (!hostPanel.Controls.Contains(ScottPlotForm))
                hostPanel.Controls.Add(ScottPlotForm);
        }

        public void InitialScottLibrary(Panel targetPanel, string xLabel, string yLabel, float fontSize = 12)
        {
            if (targetPanel == null)
                return;

            targetPanel.Controls.Clear();
            AttachToPanel(targetPanel);

            ScottPlotForm.Plot.Axes.Bottom.Label.Text = xLabel;
            ScottPlotForm.Plot.Axes.Bottom.Label.FontSize = fontSize;
            ScottPlotForm.Plot.Axes.Left.Label.Text = yLabel;
            ScottPlotForm.Plot.Axes.Left.Label.FontSize = fontSize;

            AutoScale();
            SafeRefresh();
        }

        public void ConfigureMousePanZoomRect()
        {
            ApplyInputProcessorResponses();
        }

        private void ApplyInputProcessorResponses()
        {
            var uip = ScottPlotForm.UserInputProcessor;
            uip.UserActionResponses.Clear();

            if (!_panLocked)
            {
                var panButton = ScottPlot.Interactivity.StandardMouseButtons.Left;
                uip.UserActionResponses.Add(new ScottPlot.Interactivity.UserActionResponses.MouseDragPan(panButton));
            }

            var zoomButton = ScottPlot.Interactivity.StandardMouseButtons.Right;
            uip.UserActionResponses.Add(new ScottPlot.Interactivity.UserActionResponses.MouseDragZoomRectangle(zoomButton));

            // 휠 스크롤로 마우스 커서 위치 기준 확대/축소 (Ctrl 누르면 X축만, Shift 누르면 Y축만)
            uip.UserActionResponses.Add(new ScottPlot.Interactivity.UserActionResponses.MouseWheelZoom(
                ScottPlot.Interactivity.StandardKeys.Control,
                ScottPlot.Interactivity.StandardKeys.Shift));
        }

        public bool IsPanLocked { get { return _panLocked; } }

        /// <summary>
        /// Pan Lock 토글: true이면 좌클릭 드래그 팬(pan)을 비활성화하고, 우클릭 확대(zoom rectangle)와
        /// 휠 확대/축소는 계속 허용한다.
        /// </summary>
        public void SetPanLock(bool locked)
        {
            _panLocked = locked;
            ApplyInputProcessorResponses();
        }

        public void ConfigureDefaultTheme(
            System.Drawing.Color figureBackground,
            System.Drawing.Color dataBackground,
            System.Drawing.Color axisColor = default(System.Drawing.Color),
            System.Drawing.Color gridColor = default(System.Drawing.Color),
            System.Drawing.Color legendBackground = default(System.Drawing.Color),
            System.Drawing.Color legendFontColor = default(System.Drawing.Color),
            System.Drawing.Color legendOutlineColor = default(System.Drawing.Color))
        {
            var plt = ScottPlotForm.Plot;

            plt.FigureBackground.Color = ScottPlot.Color.FromColor(figureBackground);
            plt.DataBackground.Color = ScottPlot.Color.FromColor(dataBackground);

            if (axisColor == default(System.Drawing.Color)) axisColor = System.Drawing.Color.White;
            if (gridColor == default(System.Drawing.Color)) gridColor = System.Drawing.Color.Gray;

            plt.Axes.Color(ScottPlot.Color.FromColor(axisColor));
            plt.Grid.MajorLineColor = ScottPlot.Color.FromColor(gridColor);

            if (legendBackground == default(System.Drawing.Color)) legendBackground = System.Drawing.Color.FromArgb(100, 0, 0, 0);
            if (legendFontColor == default(System.Drawing.Color)) legendFontColor = System.Drawing.Color.White;
            if (legendOutlineColor == default(System.Drawing.Color)) legendOutlineColor = System.Drawing.Color.Black;

            plt.Legend.BackgroundColor = ScottPlot.Color.FromColor(legendBackground);
            plt.Legend.FontColor = ScottPlot.Color.FromColor(legendFontColor);
            plt.Legend.OutlineColor = ScottPlot.Color.FromColor(legendOutlineColor);
            plt.Legend.IsVisible = true;
        }

        private void ConfigureDefaultStyle()
        {
            ConfigureDefaultTheme(
                System.Drawing.Color.FromArgb(24, 24, 24),
                System.Drawing.Color.FromArgb(31, 31, 31),
                System.Drawing.Color.FromArgb(215, 215, 215),
                System.Drawing.Color.FromArgb(64, 64, 64));

            ScottPlotForm.Plot.Axes.Right.Label.Text = "";
            ScottPlotForm.Plot.Axes.Right.Label.FontSize = 12;
            ScottPlotForm.Plot.Legend.IsVisible = false;
        }

        public void ShowLegend(Alignment alignment)
        {
            ScottPlotForm.Plot.Legend.IsVisible = true;
            ScottPlotForm.Plot.Legend.Alignment = alignment;
            SafeRefresh();
        }

        public void HideLegend()
        {
            ScottPlotForm.Plot.Legend.IsVisible = false;
            SafeRefresh();
        }

        public void ClearTail(int channelIndex)
        {
            if (channelIndex < 0 || channelIndex >= _channelCount) return;
            var s = _series[channelIndex];
            if (s != null && s.Y != null)
                Array.Clear(s.Y, 0, s.Y.Length);
        }

        public void ConfigureDefaultMouse()
        {
            ConfigureMousePanZoomRect();
        }

        public bool NeedsReinit(int xLen, int channelCount)
        {
            return _sharedX.Length != xLen || _channelCount != channelCount || _series.Length != channelCount;
        }

        public void InitNChannels(
            Panel hostPanel,
            double[] xFixed,
            string[] labels,
            System.Drawing.Color[] colors,
            IYAxis[] yAxes = null,
            float lineWidth = 2,
            int markerSize = 0,
            bool autoScaleOnce = true)
        {
            AttachToPanel(hostPanel);

            if (xFixed == null || xFixed.Length == 0)
                throw new ArgumentException("xFixed is null/empty");

            int nCh = labels == null ? 0 : labels.Length;
            if (nCh <= 0) throw new ArgumentException("labels is null/empty");
            if (colors == null || colors.Length < nCh) throw new ArgumentException("colors length mismatch");

            RemoveScattersOnly();

            _sharedX = new double[xFixed.Length];
            Array.Copy(xFixed, _sharedX, xFixed.Length);

            _series = new SeriesState[nCh];
            _channelCount = nCh;

            for (int ch = 0; ch < nCh; ch++)
            {
                var yBuf = new double[_sharedX.Length];

                var sc = ScottPlotForm.Plot.Add.Scatter(_sharedX, yBuf);
                sc.LegendText = labels[ch];
                sc.LineColor = ScottPlot.Color.FromColor(colors[ch]);
                sc.LineWidth = lineWidth;
                sc.MarkerSize = markerSize;

                var axis = (yAxes != null && ch < yAxes.Length && yAxes[ch] != null) ? yAxes[ch] : YAxisLeft;
                sc.Axes.YAxis = axis;

                var st = new SeriesState();
                st.X = _sharedX;
                st.Y = yBuf;
                st.Scatter = sc;
                st.YAxis = axis;
                st.Label = labels[ch];

                _series[ch] = st;
            }

            if (autoScaleOnce)
                AutoScale();

            SafeRefresh();
        }

        private void RemoveScattersOnly()
        {
            if (_series != null && _series.Length > 0)
            {
                for (int i = 0; i < _series.Length; i++)
                {
                    var s = _series[i];
                    if (s != null && s.Scatter != null)
                        ScottPlotForm.Plot.Remove(s.Scatter);
                }
            }

            foreach (var entry in _plots.Values.ToArray())
            {
                if (entry != null && entry.Scatter != null)
                    ScottPlotForm.Plot.Remove(entry.Scatter);
            }

            _plots.Clear();
            _series = Array.Empty<SeriesState>();
            _channelCount = 0;
        }

        public void UpdateChannelY(int channelIndex, double[] y, bool clearTail = false)
        {
            if (y == null) return;
            if (channelIndex < 0 || channelIndex >= _channelCount) return;
            if (_series == null || _series.Length == 0) return;

            var s = _series[channelIndex];
            if (s == null) return;

            int n = Math.Min(s.Y.Length, y.Length);
            Array.Copy(y, 0, s.Y, 0, n);

            if (clearTail && n < s.Y.Length)
                Array.Clear(s.Y, n, s.Y.Length - n);
        }

        public void UpdateChannels(ReadOnlySpan<double[]> ys, bool clearTail = false)
        {
            if (_series == null || _series.Length == 0) return;

            int nCh = Math.Min(_series.Length, ys.Length);
            for (int ch = 0; ch < nCh; ch++)
            {
                var y = ys[ch];
                if (y == null) continue;

                var s = _series[ch];
                if (s == null) continue;

                int n = Math.Min(s.Y.Length, y.Length);
                Array.Copy(y, 0, s.Y, 0, n);

                if (clearTail && n < s.Y.Length)
                    Array.Clear(s.Y, n, s.Y.Length - n);
            }
        }

        public void UpdateChannels(double[][] ys, bool clearTail = false)
        {
            if (ys == null) return;
            UpdateChannels((ReadOnlySpan<double[]>)ys, clearTail);
        }

        /// <summary>
        /// key로 식별되는 시리즈를 xs/ys(각 CSV/슬롯이 소유한 독립 배열)로 추가하거나 갱신한다.
        /// 채널 간 인덱스를 공유하지 않으므로 길이가 서로 달라도 잘림(truncation) 없이 각자의 길이를 그대로 사용한다.
        /// </summary>
        public void AddOrUpdatePlot(string key, double[] xs, double[] ys, System.Drawing.Color color, string label, float lineWidth = 2.0f, bool useRightAxis = false, bool refresh = true)
        {
            if (string.IsNullOrEmpty(key) || xs == null || ys == null)
                return;

            double[] safeXs;
            double[] safeYs;
            MakeSafeXY(xs, ys, out safeXs, out safeYs);

            if (_plots.TryGetValue(key, out var existing) && existing != null && existing.Scatter != null &&
                existing.Xs.Length == safeXs.Length && existing.Ys.Length == safeYs.Length)
            {
                Array.Copy(safeXs, 0, existing.Xs, 0, safeXs.Length);
                Array.Copy(safeYs, 0, existing.Ys, 0, safeYs.Length);
                existing.BaseColor = color;
                existing.BaseLineWidth = lineWidth;
                existing.IsRightAxis = useRightAxis;
                existing.Scatter.Color = ScottPlot.Color.FromColor(color);
                existing.Scatter.LegendText = label;
                existing.Scatter.MarkerSize = 3;
                existing.Scatter.LineWidth = lineWidth;
                existing.Scatter.Axes.YAxis = useRightAxis ? YAxisRight : YAxisLeft;
                existing.Scatter.IsVisible = existing.IsVisible;
            }
            else
            {
                RemovePlotInternal(key, refresh: false);

                var scatter = ScottPlotForm.Plot.Add.Scatter(safeXs, safeYs);
                scatter.Color = ScottPlot.Color.FromColor(color);
                scatter.LegendText = label;
                scatter.MarkerSize = 3;
                scatter.LineWidth = lineWidth;

                if (useRightAxis)
                {
                    scatter.Axes.YAxis = YAxisRight;
                }

                _plots[key] = new PlotEntry
                {
                    Scatter = scatter,
                    Xs = safeXs,
                    Ys = safeYs,
                    BaseColor = color,
                    BaseLineWidth = lineWidth,
                    CanonicalSignal = null,
                    IsRightAxis = useRightAxis,
                    IsVisible = true,
                    IsHighlighted = false
                };
            }

            if (refresh)
            {
                SafeRefresh();
            }
        }

        /// <summary>
        /// Scope 슬롯 전용 오버로드: canonicalSignal(AF_Diff / AF_ZPosition / ScanMotion / Time)에 따라
        /// Y축을 자동으로 결정한다. AF_Diff는 Left(µm), AF_ZPosition/ScanMotion은 Right(mm)에 매핑된다.
        /// </summary>
        public void AddOrUpdatePlot(string key, double[] xs, double[] ys, System.Drawing.Color color, string label, string canonicalSignal, float lineWidth = 2.0f, bool refresh = true)
        {
            if (string.IsNullOrEmpty(key) || xs == null || ys == null)
                return;

            double[] safeXs;
            double[] safeYs;
            MakeSafeXY(xs, ys, out safeXs, out safeYs);

            bool useRightAxis = IsRightAxisSignal(canonicalSignal);

            if (_plots.TryGetValue(key, out var existing) && existing != null && existing.Scatter != null &&
                existing.Xs.Length == safeXs.Length && existing.Ys.Length == safeYs.Length)
            {
                Array.Copy(safeXs, 0, existing.Xs, 0, safeXs.Length);
                Array.Copy(safeYs, 0, existing.Ys, 0, safeYs.Length);
                existing.BaseColor = color;
                existing.BaseLineWidth = lineWidth;
                existing.CanonicalSignal = canonicalSignal;
                existing.IsRightAxis = useRightAxis;
                existing.Scatter.Color = ScottPlot.Color.FromColor(color);
                existing.Scatter.LegendText = label;
                existing.Scatter.MarkerSize = 3;
                existing.Scatter.LineWidth = lineWidth;
                existing.Scatter.Axes.YAxis = useRightAxis ? YAxisRight : YAxisLeft;
                existing.Scatter.IsVisible = existing.IsVisible;
            }
            else
            {
                RemovePlotInternal(key, refresh: false);

                var scatter = ScottPlotForm.Plot.Add.Scatter(safeXs, safeYs);
                scatter.Color = ScottPlot.Color.FromColor(color);
                scatter.LegendText = label;
                scatter.MarkerSize = 3;
                scatter.LineWidth = lineWidth;
                scatter.Axes.YAxis = useRightAxis ? YAxisRight : YAxisLeft;

                _plots[key] = new PlotEntry
                {
                    Scatter = scatter,
                    Xs = safeXs,
                    Ys = safeYs,
                    BaseColor = color,
                    BaseLineWidth = lineWidth,
                    CanonicalSignal = canonicalSignal,
                    IsRightAxis = useRightAxis,
                    IsVisible = true,
                    IsHighlighted = false
                };
            }

            RefreshScopeAxisLabels();
            if (refresh)
            {
                SafeRefresh();
            }
        }

        private static void MakeSafeXY(double[] xs, double[] ys, out double[] safeXs, out double[] safeYs)
        {
            if (xs.Length == ys.Length)
            {
                safeXs = xs;
                safeYs = ys;
                return;
            }

            int n = Math.Min(xs.Length, ys.Length);
            safeXs = new double[n];
            safeYs = new double[n];
            Array.Copy(xs, safeXs, n);
            Array.Copy(ys, safeYs, n);
        }

        /// <summary>AF_ZPosition/ScanMotion 신호는 Right Y축(mm), 그 외(AF_Diff 등)는 Left Y축(µm)으로 매핑한다.</summary>
        public static bool IsRightAxisSignal(string canonicalSignal)
        {
            return string.Equals(canonicalSignal, SignalAFZPosition, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(canonicalSignal, SignalScanMotion, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshScopeAxisLabels()
        {
            bool hasLeftDiff = false;
            bool hasRightPos = false;

            foreach (var entry in _plots.Values)
            {
                if (entry == null)
                    continue;

                if (entry.IsRightAxis || IsRightAxisSignal(entry.CanonicalSignal))
                    hasRightPos = true;
                else
                    hasLeftDiff = true;
            }

            ScottPlotForm.Plot.Axes.Left.Label.Text = hasLeftDiff ? "AF_Diff (µm)" : "";
            ScottPlotForm.Plot.Axes.Right.Label.Text = hasRightPos ? "AF_ZPosition / ScanMotion (mm)" : "";
        }

        public void RemovePlot(string key)
        {
            RemovePlotInternal(key, refresh: true);
        }

        private void RemovePlotInternal(string key, bool refresh)
        {
            if (string.IsNullOrEmpty(key))
                return;

            PlotEntry entry;
            if (!_plots.TryGetValue(key, out entry))
                return;

            if (entry != null && entry.Scatter != null)
                ScottPlotForm.Plot.Remove(entry.Scatter);

            _plots.Remove(key);

            if (refresh)
            {
                RefreshScopeAxisLabels();
                SafeRefresh();
            }
        }

        /// <summary>데이터를 버리지 않고 개별 곡선의 표시 여부만 전환한다.</summary>
        public void SetPlotVisibility(string key, bool visible)
        {
            PlotEntry entry;
            if (string.IsNullOrEmpty(key) || !_plots.TryGetValue(key, out entry) || entry == null)
                return;

            entry.IsVisible = visible;

            if (entry.Scatter != null)
                entry.Scatter.IsVisible = visible;

            SafeRefresh();
        }

        public bool GetPlotVisibility(string key)
        {
            PlotEntry entry;
            return _plots.TryGetValue(key, out entry) && entry != null && entry.IsVisible;
        }

        /// <summary>범례 항목 호버 시 해당 곡선을 굵게 강조한다.</summary>
        public void SetPlotHighlight(string key, bool highlighted)
        {
            PlotEntry entry;
            if (string.IsNullOrEmpty(key) || !_plots.TryGetValue(key, out entry) || entry == null)
                return;

            entry.IsHighlighted = highlighted;

            if (entry.Scatter != null)
                entry.Scatter.LineWidth = highlighted
                    ? entry.BaseLineWidth * HighlightLineWidthMultiplier
                    : entry.BaseLineWidth;

            SafeRefresh();
        }

        public void ClearAllHighlights()
        {
            foreach (var key in _plots.Keys.ToArray())
                SetPlotHighlight(key, false);
        }

        /// <summary>현재 표시 중인 모든 시리즈를 기준으로 뷰를 자동 스케일한다.</summary>
        public void ResetView()
        {
            AutoScale();
            SafeRefresh();
        }

        /// <summary>
        /// 지정한 X 범위(마스터 X 베이스라인 구간)로 확대하고, 해당 구간에 포함된 데이터를 기준으로
        /// Left/Right Y축을 각각 재계산한다. 숨겨진(비표시) 시리즈와 비정상(NaN/Infinity) 좌표는
        /// 계산에서 제외한다. 유효한 범위/샘플이 없으면 예외 없이 전체 뷰를 자동 스케일한다.
        /// </summary>
        public void ZoomToBaseline(double minX, double maxX)
        {
            if (double.IsNaN(minX) || double.IsNaN(maxX) || double.IsInfinity(minX) || double.IsInfinity(maxX))
            {
                AutoScale();
                SafeRefresh();
                return;
            }

            if (minX > maxX)
            {
                double tmp = minX;
                minX = maxX;
                maxX = tmp;
            }

            if (minX == maxX)
            {
                double pad = Math.Abs(minX) * 0.05 + 1e-6;
                minX -= pad;
                maxX += pad;
            }

            double leftMin = double.PositiveInfinity, leftMax = double.NegativeInfinity;
            double rightMin = double.PositiveInfinity, rightMax = double.NegativeInfinity;
            bool hasLeft = false;
            bool hasRight = false;

            foreach (var entry in _plots.Values)
            {
                if (entry == null || !entry.IsVisible || entry.Xs == null || entry.Ys == null)
                    continue;

                bool useRight = entry.IsRightAxis || IsRightAxisSignal(entry.CanonicalSignal);
                int n = Math.Min(entry.Xs.Length, entry.Ys.Length);

                for (int i = 0; i < n; i++)
                {
                    double x = entry.Xs[i];
                    if (double.IsNaN(x) || double.IsInfinity(x))
                        continue;

                    if (x < minX || x > maxX)
                        continue;

                    double y = entry.Ys[i];
                    if (double.IsNaN(y) || double.IsInfinity(y))
                        continue;

                    if (useRight)
                    {
                        if (y < rightMin) rightMin = y;
                        if (y > rightMax) rightMax = y;
                        hasRight = true;
                    }
                    else
                    {
                        if (y < leftMin) leftMin = y;
                        if (y > leftMax) leftMax = y;
                        hasLeft = true;
                    }
                }
            }

            if (!hasLeft && !hasRight)
            {
                AutoScale();
                SafeRefresh();
                return;
            }

            ScottPlotForm.Plot.Axes.SetLimitsX(minX, maxX);

            if (hasLeft)
            {
                double pad = ComputeAxisPad(leftMin, leftMax);
                ScottPlotForm.Plot.Axes.SetLimitsY(leftMin - pad, leftMax + pad, YAxisLeft);
            }

            if (hasRight)
            {
                double pad = ComputeAxisPad(rightMin, rightMax);
                ScottPlotForm.Plot.Axes.SetLimitsY(rightMin - pad, rightMax + pad, YAxisRight);
            }

            SafeRefresh();
        }

        private static double ComputeAxisPad(double min, double max)
        {
            double range = max - min;
            if (range <= 0)
                return Math.Abs(max) * 0.05 + 1e-6;

            return range * 0.05;
        }

        public IReadOnlyCollection<string> GetPlotKeys()
        {
            return _plots.Keys.ToArray();
        }

        public bool TryGetPlotColor(string key, out System.Drawing.Color color)
        {
            PlotEntry entry;
            if (_plots.TryGetValue(key, out entry) && entry != null)
            {
                color = entry.BaseColor;
                return true;
            }

            color = System.Drawing.Color.Empty;
            return false;
        }

        /// <summary>AFTS_SCOPEForm의 인터랙티브 범례 패널 구성을 위한 현재 곡선 상태 스냅샷.</summary>
        public IReadOnlyList<ScopePlotSummary> GetPlotSummaries()
        {
            var list = new List<ScopePlotSummary>();

            foreach (var kvp in _plots)
            {
                var entry = kvp.Value;
                if (entry == null)
                    continue;

                list.Add(new ScopePlotSummary
                {
                    Key = kvp.Key,
                    Label = entry.Scatter != null ? entry.Scatter.LegendText : "",
                    CanonicalSignal = entry.CanonicalSignal,
                    Color = entry.BaseColor,
                    IsVisible = entry.IsVisible,
                    IsHighlighted = entry.IsHighlighted,
                    IsRightAxis = entry.IsRightAxis || IsRightAxisSignal(entry.CanonicalSignal)
                });
            }

            return list;
        }

        /// <summary>
        /// ScopeSlotInfo에 저장된 슬롯 하나를 그 슬롯이 소유한 NativeXData/DataY로 렌더링한다.
        /// 슬롯이 바인딩되어 있지 않으면 해당 key의 기존 곡선을 제거한다.
        /// </summary>
        public void RenderScopeSlot(string key, ScopeSlotInfo slot)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!slot.IsBound || slot.NativeXData == null || slot.DataY == null)
            {
                RemovePlot(key);
                return;
            }

            string label = BuildScopeSlotLabel(slot);
            AddOrUpdatePlot(key, slot.NativeXData, slot.DataY, slot.Color, label, slot.CanonicalSignal);
        }

        /// <summary>8개 슬롯 배열 전체를 CH1..CH8 등 대응하는 key로 일괄 렌더링한 뒤 뷰를 재계산한다.</summary>
        public void RenderScopeSlots(ScopeSlotInfo[] slots, string[] keys)
        {
            if (slots == null || keys == null)
                return;

            int n = Math.Min(slots.Length, keys.Length);
            for (int i = 0; i < n; i++)
                RenderScopeSlot(keys[i], slots[i]);

            RefreshScopeAxisLabels();
            ResetView();
        }

        private static string BuildScopeSlotLabel(ScopeSlotInfo slot)
        {
            string fileName = string.IsNullOrEmpty(slot.BoundFileName) ? "" : slot.BoundFileName;
            string signal = string.IsNullOrEmpty(slot.CanonicalSignal) ? "" : slot.CanonicalSignal;

            if (string.IsNullOrEmpty(fileName))
                return signal;

            return string.IsNullOrEmpty(signal) ? fileName : string.Format("[{0}] {1}", fileName, signal);
        }

        /// <summary>Scope 오버레이에서 사용 중인 모든 곡선(슬롯)을 제거한다. 실시간 채널(_series)은 영향받지 않는다.</summary>
        public void ClearScopePlots()
        {
            foreach (var key in _plots.Keys.ToArray())
                RemovePlotInternal(key, refresh: false);

            RefreshScopeAxisLabels();
            SafeRefresh();
        }

        public void AddOrUpdateHorizontalSpan(double start, double end, System.Drawing.Color fillColor, byte alpha = 30)
        {
            _hSpanColor = fillColor;
            _hSpanAlpha = alpha;

            if (_timeSpan == null)
            {
                _timeSpan = ScottPlotForm.Plot.Add.HorizontalSpan(start, end);
                _timeSpan.IsDraggable = true;
                _timeSpan.IsResizable = true;
                _timeSpan.EnableAutoscale = false;
            }
            else
            {
                _timeSpan.X1 = start;
                _timeSpan.X2 = end;
            }

            _timeSpan.FillColor = ScottPlot.Color.FromColor(fillColor).WithAlpha(alpha);
        }

        public void AddOrUpdateVerticalSpanLeft(double start, double end, System.Drawing.Color fillColor, byte alpha = 30)
        {
            _vSpanLeftColor = fillColor;
            _vSpanLeftAlpha = alpha;

            if (_valueSpanLeft == null)
            {
                _valueSpanLeft = ScottPlotForm.Plot.Add.VerticalSpan(start, end);
                _valueSpanLeft.IsDraggable = true;
                _valueSpanLeft.IsResizable = true;
                _valueSpanLeft.EnableAutoscale = false;
                _valueSpanLeft.Axes.YAxis = YAxisLeft;
            }
            else
            {
                _valueSpanLeft.Y1 = start;
                _valueSpanLeft.Y2 = end;
            }

            _valueSpanLeft.FillColor = ScottPlot.Color.FromColor(fillColor).WithAlpha(alpha);
        }

        public void AddOrUpdateVerticalSpanRight(double start, double end, System.Drawing.Color fillColor, byte alpha = 30)
        {
            _vSpanRightColor = fillColor;
            _vSpanRightAlpha = alpha;

            if (_valueSpanRight == null)
            {
                _valueSpanRight = ScottPlotForm.Plot.Add.VerticalSpan(start, end);
                _valueSpanRight.IsDraggable = true;
                _valueSpanRight.IsResizable = true;
                _valueSpanRight.EnableAutoscale = false;
                _valueSpanRight.Axes.YAxis = YAxisRight;
            }
            else
            {
                _valueSpanRight.Y1 = start;
                _valueSpanRight.Y2 = end;
            }

            _valueSpanRight.FillColor = ScottPlot.Color.FromColor(fillColor).WithAlpha(alpha);
        }

        public bool RecreateCenterSpansFromCurrentLimits(
            double spanRatioX = 0.05,
            double spanRatioYLeft = 0.05,
            double spanRatioYRight = 0.05,
            bool useRightAxisSpan = false,
            System.Drawing.Color horizontalSpanColor = default(System.Drawing.Color),
            System.Drawing.Color verticalSpanLeftColor = default(System.Drawing.Color),
            System.Drawing.Color verticalSpanRightColor = default(System.Drawing.Color),
            byte? horizontalSpanAlpha = null,
            byte? verticalSpanLeftAlpha = null,
            byte? verticalSpanRightAlpha = null)
        {
            if (_hasLockedRightAxisLimits)
            {
                ScottPlotForm.Plot.Axes.SetLimitsY(_lockedRightMin, _lockedRightMax, YAxisRight);
            }
            UpdateAxisLimitsCache();

            if (!DataAxisLimitLeft.IsReal || !DataAxisLimitLeft.HasArea)
                return false;

            if (horizontalSpanColor == default(System.Drawing.Color) || horizontalSpanColor.IsEmpty)
                horizontalSpanColor = _hSpanColor;

            if (verticalSpanLeftColor == default(System.Drawing.Color) || verticalSpanLeftColor.IsEmpty)
                verticalSpanLeftColor = _vSpanLeftColor;

            if (verticalSpanRightColor == default(System.Drawing.Color) || verticalSpanRightColor.IsEmpty)
                verticalSpanRightColor = _vSpanRightColor;

            byte hAlpha = horizontalSpanAlpha ?? _hSpanAlpha;
            byte vLeftAlpha = verticalSpanLeftAlpha ?? _vSpanLeftAlpha;
            byte vRightAlpha = verticalSpanRightAlpha ?? _vSpanRightAlpha;

            RemoveHorizontalSpan();
            RemoveVerticalSpans();

            double dx = DataAxisLimitLeft.HorizontalSpan * Math.Max(0, spanRatioX);
            double x1 = DataAxisLimitLeft.HorizontalCenter - dx;
            double x2 = DataAxisLimitLeft.HorizontalCenter + dx;
            AddOrUpdateHorizontalSpan(x1, x2, horizontalSpanColor, hAlpha);

            double dyL = DataAxisLimitLeft.VerticalSpan * Math.Max(0, spanRatioYLeft);
            double yL1 = DataAxisLimitLeft.VerticalCenter - dyL;
            double yL2 = DataAxisLimitLeft.VerticalCenter + dyL;
            AddOrUpdateVerticalSpanLeft(yL1, yL2, verticalSpanLeftColor, vLeftAlpha);

            if (useRightAxisSpan && DataAxisLimitRight.IsReal && DataAxisLimitRight.HasArea)
            {
                double dyR = DataAxisLimitRight.VerticalSpan * Math.Max(0, spanRatioYRight);
                double yR1 = DataAxisLimitRight.VerticalCenter - dyR;
                double yR2 = DataAxisLimitRight.VerticalCenter + dyR;
                AddOrUpdateVerticalSpanRight(yR1, yR2, verticalSpanRightColor, vRightAlpha);
            }

            SpanBeingDragged = null;
            ScottPlotForm.UserInputProcessor.Enable();

            if (_timeSpan != null)
                OnSpanRangeChanged?.Invoke(_timeSpan.X1, _timeSpan.X2);

            SafeRefresh();
            return true;
        }

        public void RemoveHorizontalSpan()
        {
            if (_timeSpan != null)
            {
                ScottPlotForm.Plot.Remove(_timeSpan);
                _timeSpan = null;
            }
        }

        public void RemoveVerticalSpans()
        {
            if (_valueSpanLeft != null)
            {
                ScottPlotForm.Plot.Remove(_valueSpanLeft);
                _valueSpanLeft = null;
            }

            if (_valueSpanRight != null)
            {
                ScottPlotForm.Plot.Remove(_valueSpanRight);
                _valueSpanRight = null;
            }
        }

        public AxisSpanUnderMouse GetSpanUnderMouse(float x, float y, float radiusPx = 10)
        {
            foreach (AxisSpan span in ScottPlotForm.Plot.GetPlottables<AxisSpan>().Reverse())
            {
                IXAxis xAxis = span.Axes.XAxis ?? XAxisBottom;
                IYAxis yAxis = span.Axes.YAxis ?? YAxisLeft;
                CoordinateRect rect = ScottPlotForm.Plot.GetCoordinateRect(x, y, radiusPx, xAxis, yAxis);
                AxisSpanUnderMouse spanUnderMouse = span.UnderMouse(rect);
                if (spanUnderMouse != null)
                    return spanUnderMouse;
            }

            return null;
        }

        public void UpdateAxisLimitsCache()
        {
            DataAxisLimitLeft = ScottPlotForm.Plot.Axes.GetLimits(XAxisBottom, YAxisLeft);
            DataAxisLimitRight = ScottPlotForm.Plot.Axes.GetLimits(XAxisBottom, YAxisRight);
        }

        public bool HandleMiddleClickAutoScaleAndRecreateSpans(
            MouseEventArgs e,
            double spanRatioX = 0.05,
            double spanRatioYLeft = 0.05,
            double spanRatioYRight = 0.05,
            byte alpha = 30,
            bool useRightAxisSpan = false,
            System.Drawing.Color horizontalSpanColor = default(System.Drawing.Color),
            System.Drawing.Color verticalSpanLeftColor = default(System.Drawing.Color),
            System.Drawing.Color verticalSpanRightColor = default(System.Drawing.Color))
        {
            if (e.Button != MouseButtons.Middle)
                return false;

            if (horizontalSpanColor == default(System.Drawing.Color) || horizontalSpanColor.IsEmpty)
                horizontalSpanColor = System.Drawing.Color.LightBlue;

            if (verticalSpanLeftColor == default(System.Drawing.Color) || verticalSpanLeftColor.IsEmpty)
                verticalSpanLeftColor = System.Drawing.Color.OrangeRed;

            if (verticalSpanRightColor == default(System.Drawing.Color) || verticalSpanRightColor.IsEmpty)
                verticalSpanRightColor = System.Drawing.Color.YellowGreen;

            AutoScale();

            return RecreateCenterSpansFromCurrentLimits(
                spanRatioX,
                spanRatioYLeft,
                spanRatioYRight,
                useRightAxisSpan,
                horizontalSpanColor,
                verticalSpanLeftColor,
                verticalSpanRightColor,
                alpha,
                alpha,
                alpha);
        }

        public void SetMouseHandlerEvent(MouseEventHandler mouseMove, MouseEventHandler mouseUp, MouseEventHandler mouseDown)
        {
            if (_mouseDown != null) ScottPlotForm.MouseDown -= _mouseDown;
            if (_mouseMove != null) ScottPlotForm.MouseMove -= _mouseMove;
            if (_mouseUp != null) ScottPlotForm.MouseUp -= _mouseUp;

            _mouseDown = mouseDown;
            _mouseMove = mouseMove;
            _mouseUp = mouseUp;

            if (_mouseDown != null) ScottPlotForm.MouseDown += _mouseDown;
            if (_mouseMove != null) ScottPlotForm.MouseMove += _mouseMove;
            if (_mouseUp != null) ScottPlotForm.MouseUp += _mouseUp;
        }

        public void EnableMouseValueAnnotation(
            Func<Coordinates, string> textProvider,
            Func<string> defaultTextProvider = null,
            bool keepDefaultOnMouseLeave = true)
        {
            _mouseValueAnnotationTextProvider = textProvider;
            _defaultValueAnnotationTextProvider = defaultTextProvider;
            _keepDefaultAnnotationOnMouseLeave = keepDefaultOnMouseLeave;

            if (!_mouseValueAnnotationEnabled)
            {
                ScottPlotForm.MouseMove += MouseValueAnnotation_MouseMove;
                ScottPlotForm.MouseLeave += MouseValueAnnotation_MouseLeave;
                _mouseValueAnnotationEnabled = true;
            }

            ShowDefaultValueAnnotation();
        }

        public void DisableMouseValueAnnotation()
        {
            if (_mouseValueAnnotationEnabled)
            {
                ScottPlotForm.MouseMove -= MouseValueAnnotation_MouseMove;
                ScottPlotForm.MouseLeave -= MouseValueAnnotation_MouseLeave;
                _mouseValueAnnotationEnabled = false;
            }

            _mouseValueAnnotationTextProvider = null;
            _defaultValueAnnotationTextProvider = null;
            InfoAnnotation.Hide();
        }

        public void ShowDefaultValueAnnotation()
        {
            if (_defaultValueAnnotationTextProvider == null)
            {
                InfoAnnotation.Hide();
                return;
            }

            string text = _defaultValueAnnotationTextProvider();
            if (string.IsNullOrWhiteSpace(text))
                InfoAnnotation.Hide();
            else
                InfoAnnotation.Show(text);
        }

        public void EnableAnnotation(Func<string> textProvider)
        {
            _defaultValueAnnotationTextProvider = textProvider;
            InfoAnnotation.EnsureCreated();
            ShowDefaultValueAnnotation();
        }

        public void DisableAnnotation()
        {
            _defaultValueAnnotationTextProvider = null;
            InfoAnnotation.Hide();
        }

        public void UpdateAnnotation()
        {
            ShowDefaultValueAnnotation();
        }

        private void MouseValueAnnotation_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseValueAnnotationTextProvider == null)
                return;

            Coordinates coord = ScottPlotForm.Plot.GetCoordinates(e.X, e.Y);
            string text = _mouseValueAnnotationTextProvider(coord);
            InfoAnnotation.Show(text, refresh: false);
            ScottPlotForm.Refresh();
        }

        private void MouseValueAnnotation_MouseLeave(object sender, EventArgs e)
        {
            if (_keepDefaultAnnotationOnMouseLeave)
                ShowDefaultValueAnnotation();
            else
                InfoAnnotation.Hide();
        }

        public void AutoScale()
        {
            ScottPlotForm.Plot.Axes.Margins();
            ScottPlotForm.Plot.Axes.AutoScale();
            if (_hasLockedRightAxisLimits)
            {
                ScottPlotForm.Plot.Axes.SetLimitsY(_lockedRightMin, _lockedRightMax, YAxisRight);
            }
            UpdateAxisLimitsCache();
        }

        public void SetRightAxisLimits(double min, double max)
        {
            _hasLockedRightAxisLimits = true;
            _lockedRightMin = min;
            _lockedRightMax = max;
            ScottPlotForm.Plot.Axes.SetLimitsY(min, max, YAxisRight);
            UpdateAxisLimitsCache();
            SafeRefresh();
        }

        public void ClearLockedRightAxisLimits()
        {
            _hasLockedRightAxisLimits = false;
        }

        public void SetAxisLimits(double xMin, double xMax, double yMin, double yMax)
        {
            ScottPlotForm.Plot.Axes.SetLimits(xMin, xMax, yMin, yMax);
            SafeRefresh();
        }

        public void Refresh()
        {
            SafeRefresh();
        }

        public void SafeRefresh()
        {
            if (ScottPlotForm.InvokeRequired)
                ScottPlotForm.BeginInvoke(new Action(() => ScottPlotForm.Refresh()));
            else
                ScottPlotForm.Refresh();
        }

        public void SafeRefreshThrottled()
        {
            long now = DateTime.UtcNow.Ticks;
            long minTicks = TimeSpan.TicksPerSecond / Math.Max(1, MaxRefreshFps);
            if (now - _lastRefreshTicks < minTicks) return;
            _lastRefreshTicks = now;
            SafeRefresh();
        }

        public void ClearChannels()
        {
            ClearChannelsInternal();
            SafeRefresh();
        }

        private void ClearChannelsInternal()
        {
            if (_series != null && _series.Length > 0)
            {
                for (int i = 0; i < _series.Length; i++)
                {
                    var s = _series[i];
                    if (s != null && s.Scatter != null)
                        ScottPlotForm.Plot.Remove(s.Scatter);
                }
            }

            _series = Array.Empty<SeriesState>();
            _sharedX = Array.Empty<double>();
            _channelCount = 0;
        }

        public void ClearAll()
        {
            _plots.Clear();
            _series = Array.Empty<SeriesState>();
            _sharedX = Array.Empty<double>();
            _channelCount = 0;
            _timeSpan = null;
            _valueSpanLeft = null;
            _valueSpanRight = null;
            SpanBeingDragged = null;
            _draggingSpanInternal = null;
            _draggingAxisSpan = null;
            _panLocked = false;
            Span.ResetReferences();
            InfoAnnotation.Reset();

            ScottPlotForm.Plot.Clear();
            ApplyInputProcessorResponses();
            SafeRefresh();
        }

        public event Action<double, double> OnSpanRangeChanged;

        public bool TryGetSpanRange(out double x1, out double x2)
        {
            if (_timeSpan == null)
            {
                x1 = double.NaN;
                x2 = double.NaN;
                return false;
            }

            x1 = Math.Min(_timeSpan.X1, _timeSpan.X2);
            x2 = Math.Max(_timeSpan.X1, _timeSpan.X2);
            return true;
        }

        /// <summary>
        /// 지정된 plotKey(Live AF_Diff, Live FPOS, CH1~CH8 등)의 실제 Xs/Ys 좌표 데이터를 기반으로
        /// 현재 Span 구간 [x1, x2] 내의 통계 메트릭을 정밀 산출한다.
        /// 균일/불균일 샘플링 및 다중 CSV 오버레이 환경에서도 실제 좌표 비교(x1 <= Xs[i] <= x2)로 정확한 통계를 보장한다.
        /// </summary>
        public ScopeSpanMetrics CalculatePlotSpanMetrics(string plotKey, string unitX = null, string unitY = null)
        {
            var m = new ScopeSpanMetrics
            {
                HasValidData = false,
                Key = plotKey,
                DisplayName = plotKey,
                UnitX = unitX ?? "",
                UnitY = unitY ?? ""
            };

            double xMin, xMax;
            if (!TryGetSpanRange(out xMin, out xMax))
                return m;

            m.X1 = xMin;
            m.X2 = xMax;
            m.DeltaX = xMax - xMin;

            double[] xs = null;
            double[] ys = null;
            string displayName = plotKey;
            bool isRightAxis = false;
            string canonicalSignal = null;

            if (!string.IsNullOrEmpty(plotKey) && _plots.TryGetValue(plotKey, out PlotEntry entry) && entry != null)
            {
                xs = entry.Xs;
                ys = entry.Ys;
                displayName = (entry.Scatter != null && !string.IsNullOrEmpty(entry.Scatter.LegendText))
                    ? entry.Scatter.LegendText
                    : plotKey;
                isRightAxis = entry.IsRightAxis || IsRightAxisSignal(entry.CanonicalSignal);
                canonicalSignal = entry.CanonicalSignal;
            }
            else if (_series != null && _series.Length > 0)
            {
                for (int ch = 0; ch < _series.Length; ch++)
                {
                    var s = _series[ch];
                    if (s != null && (string.Equals(s.Label, plotKey, StringComparison.OrdinalIgnoreCase) || plotKey == ch.ToString()))
                    {
                        xs = s.X;
                        ys = s.Y;
                        displayName = !string.IsNullOrEmpty(s.Label) ? s.Label : ("CH " + ch);
                        isRightAxis = (s.YAxis == YAxisRight);
                        break;
                    }
                }
            }

            if (xs == null || ys == null || xs.Length == 0 || ys.Length == 0)
                return m;

            m.DisplayName = displayName;

            // 축 기반 단위 자동 보정: 호출자가 unitY를 명시적으로 전달(null이 아님)한 경우 해당 값을 그대로 유지
            if (unitY != null)
            {
                m.UnitY = unitY;
            }
            else if (string.IsNullOrEmpty(m.UnitY))
            {
                m.UnitY = (isRightAxis || IsRightAxisSignal(canonicalSignal)) ? "mm" : "µm";
            }

            if (unitX != null)
            {
                m.UnitX = unitX;
            }

            int n = Math.Min(xs.Length, ys.Length);
            double ymin = double.PositiveInfinity;
            double ymax = double.NegativeInfinity;
            double firstY = double.NaN;
            double lastY = double.NaN;
            double sum = 0.0;
            int count = 0;

            for (int i = 0; i < n; i++)
            {
                double x = xs[i];
                if (double.IsNaN(x) || double.IsInfinity(x))
                    continue;

                if (x < xMin || x > xMax)
                    continue;

                double y = ys[i];
                if (double.IsNaN(y) || double.IsInfinity(y))
                    continue;

                if (double.IsNaN(firstY))
                    firstY = y;
                lastY = y;

                if (y < ymin) ymin = y;
                if (y > ymax) ymax = y;
                sum += y;
                count++;
            }

            if (count > 0)
            {
                m.HasValidData = true;
                m.YStart = firstY;
                m.YEnd = lastY;
                m.YMin = ymin;
                m.YMax = ymax;
                m.DeltaY = lastY - firstY;
                m.PeakToValley = ymax - ymin;
                m.YMean = sum / count;
                m.SampleCount = count;
            }

            // Active Axis Vertical Span Cursor Metrics
            VerticalSpan activeVSpan = isRightAxis ? (_valueSpanRight ?? _valueSpanLeft) : (_valueSpanLeft ?? _valueSpanRight);
            if (activeVSpan != null)
            {
                double vy1 = activeVSpan.Y1;
                double vy2 = activeVSpan.Y2;
                if (!double.IsNaN(vy1) && !double.IsNaN(vy2) && !double.IsInfinity(vy1) && !double.IsInfinity(vy2))
                {
                    m.HasYSpan = true;
                    m.YSpan1 = Math.Min(vy1, vy2);
                    m.YSpan2 = Math.Max(vy1, vy2);
                    m.DeltaYSpan = m.YSpan2 - m.YSpan1;
                }
            }

            return m;
        }

        public bool TryGetSpanMetricsForChannel(int channelIndex, out SpanChannelMetrics m)
        {
            m = new SpanChannelMetrics();

            if (_timeSpan == null) return false;
            if (_series == null || _series.Length == 0) return false;
            if (channelIndex < 0 || channelIndex >= _series.Length) return false;

            double x1 = Math.Min(_timeSpan.X1, _timeSpan.X2);
            double x2 = Math.Max(_timeSpan.X1, _timeSpan.X2);

            m.SpanXMin = x1;
            m.SpanXMax = x2;
            m.SpanXSpan = x2 - x1;
            m.SpanXCenter = 0.5 * (x1 + x2);

            if (_valueSpanLeft != null)
            {
                double y1 = Math.Min(_valueSpanLeft.Y1, _valueSpanLeft.Y2);
                double y2 = Math.Max(_valueSpanLeft.Y1, _valueSpanLeft.Y2);

                m.SpanYMin = y1;
                m.SpanYMax = y2;
                m.SpanYSpan = y2 - y1;
                m.SpanYCenter = 0.5 * (y1 + y2);
            }

            var s = _series[channelIndex];
            if (s == null || s.Y == null || s.Y.Length == 0) return false;

            int i1 = (int)Math.Ceiling(x1);
            int i2 = (int)Math.Floor(x2);

            if (i1 < 0) i1 = 0;
            if (i2 >= s.Y.Length) i2 = s.Y.Length - 1;

            if (i2 < i1) return false;

            m.IdxMin = i1;
            m.IdxMax = i2;

            m.YStart = s.Y[i1];
            m.YEnd = s.Y[i2];
            m.YDelta = m.YEnd - m.YStart;
            m.YDeltaAbs = Math.Abs(m.YDelta);

            double ymin = double.PositiveInfinity;
            double ymax = double.NegativeInfinity;
            double sum = 0;
            int cnt = 0;

            for (int i = i1; i <= i2; i++)
            {
                double v = s.Y[i];
                if (v < ymin) ymin = v;
                if (v > ymax) ymax = v;
                sum += v;
                cnt++;
            }

            m.YMin = ymin;
            m.YMax = ymax;
            m.YRange = ymax - ymin;
            m.YMean = (cnt > 0) ? (sum / cnt) : 0;

            return true;
        }

        private void InternalSpanMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                bool useRightAxisSpan = _valueSpanRight != null;

                AutoScale();
                RecreateCenterSpansFromCurrentLimits(
                    spanRatioX: 0.05,
                    spanRatioYLeft: 0.05,
                    spanRatioYRight: 0.05,
                    useRightAxisSpan: useRightAxisSpan,
                    horizontalSpanColor: _hSpanColor,
                    verticalSpanLeftColor: _vSpanLeftColor,
                    verticalSpanRightColor: _vSpanRightColor,
                    horizontalSpanAlpha: _hSpanAlpha,
                    verticalSpanLeftAlpha: _vSpanLeftAlpha,
                    verticalSpanRightAlpha: _vSpanRightAlpha);

                _draggingSpanInternal = null;
                ScottPlotForm.UserInputProcessor.Enable();
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            _draggingSpanInternal = GetSpanUnderMouse(e.X, e.Y);
            if (_draggingSpanInternal != null)
            {
                _draggingAxisSpan = _draggingSpanInternal.Span ?? GetAxisSpanUnderMouse(e.X, e.Y);
                ScottPlotForm.UserInputProcessor.Disable();
            }
        }

        private void InternalSpanMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingSpanInternal == null)
            {
                var span = GetSpanUnderMouse(e.X, e.Y);
                if (span == null)
                {
                    if (ScottPlotForm.Cursor != Cursors.Default)
                        ScottPlotForm.Cursor = Cursors.Default;
                }
                else if (span.IsResizingHorizontally)
                    ScottPlotForm.Cursor = Cursors.SizeWE;
                else if (span.IsResizingVertically)
                    ScottPlotForm.Cursor = Cursors.SizeNS;
                else
                    ScottPlotForm.Cursor = Cursors.SizeAll;
                return;
            }

            IXAxis targetXAxis = (_draggingAxisSpan != null && _draggingAxisSpan.Axes.XAxis != null)
                ? _draggingAxisSpan.Axes.XAxis
                : XAxisBottom;
            IYAxis targetYAxis = (_draggingAxisSpan != null && _draggingAxisSpan.Axes.YAxis != null)
                ? _draggingAxisSpan.Axes.YAxis
                : YAxisLeft;

            Coordinates coord = ScottPlotForm.Plot.GetCoordinates(e.X, e.Y, targetXAxis, targetYAxis);
            _draggingSpanInternal.DragTo(coord);

            if (_draggingAxisSpan == _valueSpanLeft)
                SyncVerticalSpans(drivenByLeft: true);
            else if (_draggingAxisSpan == _valueSpanRight)
                SyncVerticalSpans(drivenByLeft: false);

            OnSpanRangeChanged?.Invoke(_timeSpan != null ? _timeSpan.X1 : 0, _timeSpan != null ? _timeSpan.X2 : 0);

            ShowDefaultValueAnnotation();
            SafeRefreshThrottled();
        }

        private void InternalSpanMouseUp(object sender, MouseEventArgs e)
        {
            if (_draggingSpanInternal == null)
            {
                // 우클릭 드래그로 확대(Zoom Rectangle)를 마친 경우에도 Span은 현재 위치를 유지한다.
                return;
            }

            _draggingSpanInternal = null;
            _draggingAxisSpan = null;
            ScottPlotForm.UserInputProcessor.Enable();
            ScottPlotForm.Cursor = Cursors.Default;

            OnSpanRangeChanged?.Invoke(_timeSpan != null ? _timeSpan.X1 : 0, _timeSpan != null ? _timeSpan.X2 : 0);

            ShowDefaultValueAnnotation();
            SafeRefresh();
        }

        private void InternalMouseWheelZoom(object sender, MouseEventArgs e)
        {
            // 스팬을 드래그하는 중에 들어온 휠 입력은 재정렬이 드래그와 충돌하므로 무시한다.
            if (_draggingSpanInternal != null)
                return;

            // 휠 줌/드래그 시에도 Span은 자동 재정렬하지 않고 현재 위치를 유지한다.
        }

        private void RecenterSpansToCurrentView()
        {
            if (_timeSpan == null && _valueSpanLeft == null && _valueSpanRight == null)
                return;

            bool useRightAxisSpan = _valueSpanRight != null;

            RecreateCenterSpansFromCurrentLimits(
                spanRatioX: 0.05,
                spanRatioYLeft: 0.05,
                spanRatioYRight: 0.05,
                useRightAxisSpan: useRightAxisSpan,
                horizontalSpanColor: _hSpanColor,
                verticalSpanLeftColor: _vSpanLeftColor,
                verticalSpanRightColor: _vSpanRightColor,
                horizontalSpanAlpha: _hSpanAlpha,
                verticalSpanLeftAlpha: _vSpanLeftAlpha,
                verticalSpanRightAlpha: _vSpanRightAlpha);

            ShowDefaultValueAnnotation();
        }

        private void SyncVerticalSpans(bool drivenByLeft)
        {
            if (_valueSpanLeft == null || _valueSpanRight == null)
                return;

            var leftLimits = ScottPlotForm.Plot.Axes.GetLimits(XAxisBottom, YAxisLeft);
            var rightLimits = ScottPlotForm.Plot.Axes.GetLimits(XAxisBottom, YAxisRight);

            if (!leftLimits.IsReal || !rightLimits.IsReal)
                return;

            if (drivenByLeft)
            {
                double leftRange = leftLimits.VerticalSpan;
                if (leftRange == 0) return;

                double centerFrac = ((_valueSpanLeft.Y1 + _valueSpanLeft.Y2) / 2.0 - leftLimits.Bottom) / leftRange;
                centerFrac = Math.Max(0.0, Math.Min(1.0, centerFrac));
                double widthFrac = Math.Abs(_valueSpanLeft.Y2 - _valueSpanLeft.Y1) / leftRange;

                double rightRange = rightLimits.VerticalSpan;
                double newCenter = rightLimits.Bottom + centerFrac * rightRange;
                double newHalf = widthFrac * rightRange / 2.0;
                _valueSpanRight.Y1 = newCenter - newHalf;
                _valueSpanRight.Y2 = newCenter + newHalf;
            }
            else
            {
                double rightRange = rightLimits.VerticalSpan;
                if (rightRange == 0) return;

                double centerFrac = ((_valueSpanRight.Y1 + _valueSpanRight.Y2) / 2.0 - rightLimits.Bottom) / rightRange;
                centerFrac = Math.Max(0.0, Math.Min(1.0, centerFrac));
                double widthFrac = Math.Abs(_valueSpanRight.Y2 - _valueSpanRight.Y1) / rightRange;

                double leftRange = leftLimits.VerticalSpan;
                double newCenter = leftLimits.Bottom + centerFrac * leftRange;
                double newHalf = widthFrac * leftRange / 2.0;
                _valueSpanLeft.Y1 = newCenter - newHalf;
                _valueSpanLeft.Y2 = newCenter + newHalf;
            }
        }

        private AxisSpan GetAxisSpanUnderMouse(float x, float y, float radiusPx = 10)
        {
            foreach (AxisSpan span in ScottPlotForm.Plot.GetPlottables<AxisSpan>().Reverse())
            {
                IXAxis xAxis = span.Axes.XAxis ?? XAxisBottom;
                IYAxis yAxis = span.Axes.YAxis ?? YAxisLeft;
                CoordinateRect rect = ScottPlotForm.Plot.GetCoordinateRect(x, y, radiusPx, xAxis, yAxis);
                if (span.UnderMouse(rect) != null)
                    return span;
            }
            return null;
        }

        public HorizontalSpan HSpan => _timeSpan;
        public VerticalSpan VSpanLeft => _valueSpanLeft;
        public VerticalSpan VSpanRight => _valueSpanRight;
    }
}
