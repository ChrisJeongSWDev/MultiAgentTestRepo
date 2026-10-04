using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScottPlot;

// 별칭 정의를 통해 Color, Font, FontStyle, Label 모호성 해결 (AFTS_SCOPEForm 패턴)
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using WinFormsLabel = System.Windows.Forms.Label;

namespace VIEWORKS_AFTS
{
    /// <summary>
    /// VEO Focus Scope (AFTS_SCOPEForm) 원본 UI 및 코드 아키텍처를 100% 동일하게 캡슐화한 종합 Scope 뷰 컨트롤.
    /// PlotPanel(캔버스), LegendContainerPanel(우측 다크 범례, ResetView, PanLock, SetSpanCenter, CustomLegend, SpanMetrics)를 포함합니다.
    /// </summary>
    public class ScopeViewControl : UserControl
    {
        // UI Layout Panels
        private Panel PlotAreaPanel;
        private Panel PlotPanel;
        private Panel LegendContainerPanel;
        private Panel LegendHeaderPanel;
        private FlowLayoutPanel LegendFlowPanel;
        private WinFormsLabel LegendTitleLabel;
        private Button ResetViewBtn;
        private Button PanLockBtn;
        private Button SetSpanCenterBtn;

        // Vieworks Exhibition 스타일 실시간 Span Inspector 패널
        private ScopeSpanMetricsPanel _spanMetricsPanel;
        private readonly ToolTip _legendToolTip = new ToolTip();

        // ScottPlot 핸들
        public ScottPlotLibrary m_ScottPlotHandle = new ScottPlotLibrary();

        // 채널 등록 및 데이터 관리
        public class ChannelInfo
        {
            public string Key;
            public string DisplayText;
            public DrawingColor Color;
            public bool UseRightAxis;
            public double[] XData;
            public double[] YData;
            public bool IsVisible = true;
            public string UnitX = "";
            public string UnitY = "";
        }

        private sealed class LegendEntry
        {
            public string Key;
            public string Text;
            public DrawingColor Color;
            public bool IsBaseline;
        }

        private readonly Dictionary<string, ChannelInfo> _channels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _plotVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private string _highlightedPlotKey;
        private string _selectedTargetKey;
        private bool _panLocked;

        private string _xLabel = "X Axis";
        private string _yLeftLabel = "Y Left";
        private string _yRightLabel = null;
        private bool _isRightAxisActive = false;

        public ScopeViewControl()
        {
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            this.SuspendLayout();
            this.BackColor = DrawingColor.FromArgb(24, 24, 26);
            this.DoubleBuffered = true;

            // 1. Root PlotAreaPanel
            PlotAreaPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DrawingColor.FromArgb(24, 24, 26)
            };

            // 2. PlotPanel (FormsPlot 캔버스 호스트)
            PlotPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DrawingColor.Black
            };

            // 3. LegendContainerPanel (우측 범례 및 Span 통계 컨테이너)
            LegendContainerPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 320,
                BackColor = DrawingColor.FromArgb(24, 24, 26)
            };

            // 3.1. LegendHeaderPanel (컨트롤 버튼 3종 및 타이틀)
            LegendHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 105,
                BackColor = DrawingColor.FromArgb(24, 24, 26)
            };

            ResetViewBtn = new Button
            {
                Text = "Reset View",
                Font = new DrawingFont("Century Gothic", 9.0f, DrawingFontStyle.Bold),
                BackColor = DrawingColor.FromArgb(59, 130, 246),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(8, 8),
                Size = new Size(300, 26),
                Cursor = Cursors.Hand
            };
            ResetViewBtn.FlatAppearance.BorderSize = 0;
            ResetViewBtn.Click += ResetViewBtn_Click;

            PanLockBtn = new Button
            {
                Text = "Pan Lock: Off",
                Font = new DrawingFont("Century Gothic", 9.0f, DrawingFontStyle.Bold),
                BackColor = DrawingColor.FromArgb(71, 85, 105),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(8, 38),
                Size = new Size(146, 26),
                Cursor = Cursors.Hand
            };
            PanLockBtn.FlatAppearance.BorderSize = 0;
            PanLockBtn.Click += PanLockBtn_Click;

            SetSpanCenterBtn = new Button
            {
                Text = "Set Span Center",
                Font = new DrawingFont("Century Gothic", 9.0f, DrawingFontStyle.Bold),
                BackColor = DrawingColor.FromArgb(59, 130, 246),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(160, 38),
                Size = new Size(148, 26),
                Cursor = Cursors.Hand
            };
            SetSpanCenterBtn.FlatAppearance.BorderSize = 0;
            SetSpanCenterBtn.Click += SetSpanCenter_Click;

            LegendTitleLabel = new WinFormsLabel
            {
                Text = "CHANNELS & SIGNALS",
                Font = new DrawingFont("Century Gothic", 9.5f, DrawingFontStyle.Bold),
                ForeColor = DrawingColor.FromArgb(148, 163, 184),
                Location = new Point(8, 72),
                AutoSize = true
            };

            LegendHeaderPanel.Controls.Add(ResetViewBtn);
            LegendHeaderPanel.Controls.Add(PanLockBtn);
            LegendHeaderPanel.Controls.Add(SetSpanCenterBtn);
            LegendHeaderPanel.Controls.Add(LegendTitleLabel);

            // 3.2. LegendFlowPanel (커스텀 범례 아이템 목록)
            LegendFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = DrawingColor.FromArgb(24, 24, 26),
                Padding = new Padding(4)
            };

            // 3.3. SpanMetricsPanel (하단 도킹)
            InitializeSpanMetricsInspector();

            LegendContainerPanel.Controls.Add(LegendFlowPanel);
            LegendContainerPanel.Controls.Add(LegendHeaderPanel);
            LegendContainerPanel.Controls.Add(_spanMetricsPanel);

            PlotAreaPanel.Controls.Add(PlotPanel);
            PlotAreaPanel.Controls.Add(LegendContainerPanel);

            this.Controls.Add(PlotAreaPanel);
            this.ResumeLayout(false);
        }

        public void InitialScope(string xLabel, string yLeftLabel, string yRightLabel = null)
        {
            _xLabel = xLabel;
            _yLeftLabel = yLeftLabel;
            _yRightLabel = yRightLabel;
            _isRightAxisActive = !string.IsNullOrEmpty(yRightLabel);

            m_ScottPlotHandle.InitialScottLibrary(PlotPanel, _xLabel, _yLeftLabel);
            if (_isRightAxisActive)
            {
                m_ScottPlotHandle.YAxisRight.Label.Text = _yRightLabel;
                m_ScottPlotHandle.YAxisRight.Label.FontSize = 12;
            }

            m_ScottPlotHandle.HideLegend();
            ConfigureSpanAnnotation();

            m_ScottPlotHandle.PlotControl.Refresh();
        }

        private void ConfigureSpanAnnotation()
        {
            m_ScottPlotHandle.DisableAnnotation();
        }

        private void InitializeSpanMetricsInspector()
        {
            if (_spanMetricsPanel == null)
            {
                _spanMetricsPanel = new ScopeSpanMetricsPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 70
                };

                _spanMetricsPanel.TargetChannelChanged += (s, targetKey) => {
                    _selectedTargetKey = targetKey;
                    RefreshLegendCardSelection();
                    UpdateSpanMetricsDisplay();
                };

                m_ScottPlotHandle.OnSpanRangeChanged += (x1, x2) => {
                    UpdateSpanMetricsDisplay();
                };
            }
        }

        public void RegisterChannel(string key, string displayText, DrawingColor defaultColor, bool useRightAxis = false, string unitX = "", string unitY = "")
        {
            var info = new ChannelInfo
            {
                Key = key,
                DisplayText = displayText,
                Color = defaultColor,
                UseRightAxis = useRightAxis,
                XData = Array.Empty<double>(),
                YData = Array.Empty<double>(),
                IsVisible = true,
                UnitX = unitX ?? "",
                UnitY = unitY ?? ""
            };
            _channels[key] = info;
            _plotVisibility[key] = true;

            RebuildLegendPanel();
        }

        public void SetRightAxisLimits(double min, double max)
        {
            m_ScottPlotHandle.SetRightAxisLimits(min, max);
        }

        public void UpdateChannelData(string key, double[] xData, double[] yData, bool refresh = true)
        {
            if (!_channels.TryGetValue(key, out var info))
                return;

            info.XData = xData ?? Array.Empty<double>();
            info.YData = yData ?? Array.Empty<double>();

            if (refresh)
            {
                RefreshPlots();
            }
        }

        public void SetVisible(string key, bool visible)
        {
            if (_channels.TryGetValue(key, out var info))
            {
                info.IsVisible = visible;
                _plotVisibility[key] = visible;
                m_ScottPlotHandle.SetPlotVisibility(key, visible);
                RebuildLegendPanel();
                m_ScottPlotHandle.PlotControl.Refresh();
            }
        }

        public void RefreshPlots(bool autoScale = false)
        {
            foreach (var kvp in _channels)
            {
                var ch = kvp.Value;
                if (ch.XData == null || ch.YData == null || ch.XData.Length == 0 || ch.YData.Length == 0)
                    continue;

                int len = Math.Min(ch.XData.Length, ch.YData.Length);
                double[] x = ch.XData.Length == len ? ch.XData : ch.XData.Take(len).ToArray();
                double[] y = ch.YData.Length == len ? ch.YData : ch.YData.Take(len).ToArray();

                m_ScottPlotHandle.AddOrUpdatePlot(ch.Key, x, y, ch.Color, ch.DisplayText, 2.0f, ch.UseRightAxis, refresh: false);

                bool visible = _plotVisibility.TryGetValue(ch.Key, out bool v) ? v : ch.IsVisible;
                m_ScottPlotHandle.SetPlotVisibility(ch.Key, visible);
            }

            if (autoScale || m_ScottPlotHandle.VSpanLeft == null)
            {
                m_ScottPlotHandle.AutoScale();
                CreateScopeSpans();
            }
            else
            {
                m_ScottPlotHandle.PlotControl.Refresh();
                UpdateSpanMetricsDisplay();
            }

            if (LegendFlowPanel.Controls.Count == 0 && _channels.Count > 0)
            {
                RebuildLegendPanel();
            }
        }

        public void CreateScopeSpans()
        {
            bool hasAnyData = _channels.Values.Any(c => c.YData != null && c.YData.Length > 0);
            if (!hasAnyData) return;

            m_ScottPlotHandle.RecreateCenterSpansFromCurrentLimits(
                spanRatioX: 0.05,
                spanRatioYLeft: 0.05,
                spanRatioYRight: 0.05,
                useRightAxisSpan: _isRightAxisActive,
                horizontalSpanColor: DrawingColor.LightBlue,
                verticalSpanLeftColor: DrawingColor.OrangeRed,
                verticalSpanRightColor: DrawingColor.YellowGreen,
                horizontalSpanAlpha: 50,
                verticalSpanLeftAlpha: 40,
                verticalSpanRightAlpha: 40);

            UpdateSpanMetricsDisplay();
        }

        private List<LegendEntry> BuildLegendEntries()
        {
            var entries = new List<LegendEntry>();
            foreach (var ch in _channels.Values)
            {
                string suffix = ch.UseRightAxis ? " [R-Axis]" : "";
                entries.Add(new LegendEntry
                {
                    Key = ch.Key,
                    Text = ch.DisplayText + suffix,
                    Color = ch.Color,
                    IsBaseline = false
                });
            }
            return entries;
        }

        public void RebuildLegendPanel()
        {
            List<LegendEntry> entries = BuildLegendEntries();
            var currentKeys = new HashSet<string>(entries.Select(en => en.Key));

            var staleKeys = _plotVisibility.Keys.Where(k => !currentKeys.Contains(k)).ToList();
            foreach (string staleKey in staleKeys)
                _plotVisibility.Remove(staleKey);

            if (_highlightedPlotKey != null && !currentKeys.Contains(_highlightedPlotKey))
                _highlightedPlotKey = null;

            if (string.IsNullOrEmpty(_selectedTargetKey) || !currentKeys.Contains(_selectedTargetKey))
            {
                _selectedTargetKey = entries.Count > 0 ? entries[0].Key : null;
            }

            LegendFlowPanel.SuspendLayout();
            LegendFlowPanel.Controls.Clear();

            if (entries.Count == 0)
            {
                WinFormsLabel emptyLabel = new WinFormsLabel
                {
                    Text = "(No profiles to display)",
                    AutoSize = true,
                    ForeColor = DrawingColor.Gray,
                    Margin = new Padding(4)
                };
                LegendFlowPanel.Controls.Add(emptyLabel);
            }
            else
            {
                foreach (LegendEntry entry in entries)
                {
                    if (!_plotVisibility.ContainsKey(entry.Key))
                        _plotVisibility[entry.Key] = true;

                    LegendFlowPanel.Controls.Add(CreateLegendItemControl(entry));
                }
            }

            LegendFlowPanel.ResumeLayout();
            UpdateSpanTargetChannels();
        }

        private Control CreateLegendItemControl(LegendEntry entry)
        {
            bool visible = _plotVisibility.TryGetValue(entry.Key, out bool v) ? v : true;
            bool isSelected = string.Equals(_selectedTargetKey, entry.Key, StringComparison.OrdinalIgnoreCase);

            Panel card = new Panel
            {
                Size = new Size(300, 32),
                Margin = new Padding(2, 2, 2, 2),
                BackColor = isSelected ? DrawingColor.FromArgb(40, 52, 70) : DrawingColor.FromArgb(30, 30, 34),
                Cursor = Cursors.Hand,
                Tag = entry.Key
            };

            CheckBox chk = new CheckBox
            {
                Checked = visible,
                Location = new Point(6, 6),
                Size = new Size(18, 18),
                Cursor = Cursors.Hand,
                Tag = entry.Key
            };

            Panel swatch = new Panel
            {
                Size = new Size(12, 12),
                Location = new Point(28, 9),
                BackColor = visible ? entry.Color : DrawingColor.FromArgb(60, 60, 65),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Tag = entry.Key
            };

            WinFormsLabel label = new WinFormsLabel
            {
                Text = entry.Text,
                AutoSize = true,
                Location = new Point(46, 7),
                ForeColor = visible ? DrawingColor.White : DrawingColor.Gray,
                Font = new DrawingFont("Segoe UI", 9.0f, DrawingFontStyle.Regular),
                Cursor = Cursors.Hand,
                Tag = entry.Key
            };

            // 1. 체크박스: 전체 재생성 없이 해당 플롯 표시/숨김만 즉각 토글
            chk.Click += (s, e) => {
                bool next = chk.Checked;
                _plotVisibility[entry.Key] = next;
                if (_channels.TryGetValue(entry.Key, out var ch))
                    ch.IsVisible = next;

                m_ScottPlotHandle.SetPlotVisibility(entry.Key, next);
                label.ForeColor = next ? DrawingColor.White : DrawingColor.Gray;
                swatch.BackColor = next ? entry.Color : DrawingColor.FromArgb(60, 60, 65);
                m_ScottPlotHandle.PlotControl.Refresh();
            };

            // 2. 스와치: 색상 선택 다이얼로그 호출
            swatch.Click += (s, e) => OpenColorPickerForEntry(entry.Key);

            // 3. 카드 및 레이블 클릭: 해당 채널을 Active Target으로 선택 및 메트릭 갱신
            Action selectTarget = () => SelectTargetChannel(entry.Key);
            card.Click += (s, e) => selectTarget();
            label.Click += (s, e) => selectTarget();

            // 마우스 호버 하이라이트
            EventHandler enterHandler = (s, e) => HighlightLegendKey(entry.Key, true);
            EventHandler leaveHandler = (s, e) => HighlightLegendKey(entry.Key, false);
            card.MouseEnter += enterHandler;
            card.MouseLeave += leaveHandler;
            label.MouseEnter += enterHandler;
            label.MouseLeave += leaveHandler;

            // 선택 상태에 따른 테두리 드로잉
            card.Paint += (s, pe) => {
                bool selected = string.Equals(_selectedTargetKey, entry.Key, StringComparison.OrdinalIgnoreCase);
                using (var pen = new Pen(selected ? DrawingColor.FromArgb(59, 130, 246) : DrawingColor.FromArgb(45, 45, 52), 1))
                {
                    pe.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            string tooltip = $"[{entry.Key}] {entry.Text}\n• Click row to select as ΔX/ΔY active target\n• Click checkbox to toggle visibility\n• Click color box to change line color";
            _legendToolTip.SetToolTip(card, tooltip);
            _legendToolTip.SetToolTip(chk, tooltip);
            _legendToolTip.SetToolTip(swatch, tooltip);
            _legendToolTip.SetToolTip(label, tooltip);

            card.Controls.Add(chk);
            card.Controls.Add(swatch);
            card.Controls.Add(label);

            return card;
        }

        public void SelectTargetChannel(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _selectedTargetKey = key;
            _spanMetricsPanel?.SelectChannel(key);
            RefreshLegendCardSelection();
            UpdateSpanMetricsDisplay();
        }

        public void RefreshLegendCardSelection()
        {
            foreach (Control c in LegendFlowPanel.Controls)
            {
                if (c is Panel p && p.Tag is string k)
                {
                    bool isSelected = string.Equals(k, _selectedTargetKey, StringComparison.OrdinalIgnoreCase);
                    p.BackColor = isSelected ? DrawingColor.FromArgb(40, 52, 70) : DrawingColor.FromArgb(30, 30, 34);
                    p.Invalidate();
                }
            }
        }

        public void ToggleLegendVisibility(string key)
        {
            bool current = _plotVisibility.TryGetValue(key, out bool v) ? v : true;
            bool next = !current;
            _plotVisibility[key] = next;

            if (_channels.TryGetValue(key, out var info))
                info.IsVisible = next;

            m_ScottPlotHandle.SetPlotVisibility(key, next);
            m_ScottPlotHandle.PlotControl.Refresh();

            RebuildLegendPanel();
        }

        private void HighlightLegendKey(string key, bool highlighted)
        {
            if (highlighted)
            {
                if (_highlightedPlotKey != null && _highlightedPlotKey != key)
                    m_ScottPlotHandle.SetPlotHighlight(_highlightedPlotKey, false);

                _highlightedPlotKey = key;
                m_ScottPlotHandle.SetPlotHighlight(key, true);
            }
            else
            {
                if (_highlightedPlotKey == key)
                {
                    m_ScottPlotHandle.SetPlotHighlight(key, false);
                    _highlightedPlotKey = null;
                }
            }
            m_ScottPlotHandle.PlotControl.Refresh();
        }

        private void OpenColorPickerForEntry(string key)
        {
            if (string.IsNullOrEmpty(key) || !_channels.TryGetValue(key, out var ch))
                return;

            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.Color = ch.Color;
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ch.Color = dlg.Color;
                    RefreshPlots(false);
                }
            }
        }

        private void UpdateSpanTargetChannels()
        {
            if (_spanMetricsPanel == null) return;

            var items = new List<ChannelComboItem>();
            foreach (var ch in _channels.Values)
            {
                string name = ch.DisplayText + (ch.UseRightAxis ? " [R]" : "");
                items.Add(new ChannelComboItem(ch.Key, name, ch.Color));
            }

            string defaultKey = !string.IsNullOrEmpty(_selectedTargetKey) ? _selectedTargetKey : (items.Count > 0 ? items[0].Key : null);
            _spanMetricsPanel.UpdateAvailableChannels(items, defaultKey);
            UpdateSpanMetricsDisplay();
        }

        private void UpdateSpanMetricsDisplay()
        {
            if (_spanMetricsPanel == null) return;

            string targetKey = _spanMetricsPanel.SelectedTargetKey;
            if (string.IsNullOrEmpty(targetKey))
            {
                _spanMetricsPanel.ClearMetrics();
                return;
            }

            string unitX = "";
            string unitY = "";
            if (_channels.TryGetValue(targetKey, out var ch))
            {
                unitX = ch.UnitX ?? "";
                unitY = ch.UnitY ?? "";
            }

            ScopeSpanMetrics metrics = m_ScottPlotHandle.CalculatePlotSpanMetrics(targetKey, unitX, unitY);
            _spanMetricsPanel.UpdateMetrics(metrics);
        }

        private void ResetViewBtn_Click(object sender, EventArgs e)
        {
            m_ScottPlotHandle.ResetView();
            CreateScopeSpans();
            m_ScottPlotHandle.PlotControl.Refresh();
        }

        private void PanLockBtn_Click(object sender, EventArgs e)
        {
            _panLocked = !_panLocked;
            m_ScottPlotHandle.SetPanLock(_panLocked);
            PanLockBtn.Text = _panLocked ? "Pan Lock: On" : "Pan Lock: Off";
            PanLockBtn.BackColor = _panLocked ? DrawingColor.FromArgb(239, 68, 68) : DrawingColor.FromArgb(71, 85, 105);
        }

        private void SetSpanCenter_Click(object sender, EventArgs e)
        {
            CreateScopeSpans();
            m_ScottPlotHandle.PlotControl.Refresh();
        }

        public void AutoScale()
        {
            m_ScottPlotHandle.AutoScale();
            CreateScopeSpans();
            m_ScottPlotHandle.PlotControl.Refresh();
        }

        public void SafeRefresh()
        {
            m_ScottPlotHandle.SafeRefresh();
            UpdateSpanMetricsDisplay();
        }
    }
}
