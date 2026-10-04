using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace VIEWORKS_AFTS
{
    /// <summary>
    /// VEO Focus Scope용 실시간 Span 메트릭 뷰어 & 인스펙터 패널.
    /// Vieworks Exhibition 다크 엔지니어링 디자인 패턴 적용.
    /// 사용자가 선택한 활성 채널의 노란색 ΔX 및 ΔY 수치만을 크고 선명하게 표출합니다 (사족 데이터 제거).
    /// </summary>
    public class ScopeSpanMetricsPanel : Panel
    {
        private readonly Label _lblTitle;
        private readonly Panel _pnlSwatch;
        private readonly Label _lblChannelName;
        private readonly Label _lblDeltaX;
        private readonly Label _lblDeltaY;

        private readonly List<ChannelComboItem> _availableChannels = new List<ChannelComboItem>();
        private readonly Dictionary<string, Color> _channelColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private string _selectedTargetKey;

        public event Action<object, string> TargetChannelChanged;

        public string SelectedTargetKey
        {
            get => _selectedTargetKey;
            set => SelectChannel(value);
        }

        public ScopeSpanMetricsPanel()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(24, 24, 26); // #18181A
            this.ForeColor = Color.White;
            this.Padding = new Padding(8, 4, 8, 4);
            this.Size = new Size(320, 70);

            // 1. Header: Title + Swatch + Active Channel Name
            _lblTitle = new Label
            {
                Text = "SPAN METRICS",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
                Location = new Point(10, 5),
                AutoSize = true
            };

            _pnlSwatch = new Panel
            {
                BackColor = Color.YellowGreen,
                Size = new Size(10, 10),
                Location = new Point(102, 7),
                BorderStyle = BorderStyle.FixedSingle
            };

            _lblChannelName = new Label
            {
                Text = "(No Channel)",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                ForeColor = Color.YellowGreen,
                Location = new Point(116, 5),
                AutoSize = true
            };

            // 2. Yellow Metrics: Only Delta X and Delta Y
            _lblDeltaX = new Label
            {
                Text = "ΔX: --",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.Gold,
                Location = new Point(10, 24),
                AutoSize = true
            };

            _lblDeltaY = new Label
            {
                Text = "ΔY: --",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.Gold,
                Location = new Point(10, 44),
                AutoSize = true
            };

            this.Controls.Add(_lblTitle);
            this.Controls.Add(_pnlSwatch);
            this.Controls.Add(_lblChannelName);
            this.Controls.Add(_lblDeltaX);
            this.Controls.Add(_lblDeltaY);
        }

        /// <summary>
        /// 활성 채널 목록을 등록하고 색상을 매핑한다.
        /// </summary>
        public void UpdateAvailableChannels(IEnumerable<ChannelComboItem> items, string defaultKey = null)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(() => UpdateAvailableChannels(items, defaultKey)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            _availableChannels.Clear();
            _channelColors.Clear();

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    _availableChannels.Add(item);
                    _channelColors[item.Key] = item.Color;
                }
            }

            if (_availableChannels.Count > 0)
            {
                // 기존 선택 유지 또는 기본값 설정
                string targetKey = _selectedTargetKey;
                if (string.IsNullOrEmpty(targetKey) || !_availableChannels.Any(c => string.Equals(c.Key, targetKey, StringComparison.OrdinalIgnoreCase)))
                {
                    targetKey = !string.IsNullOrEmpty(defaultKey) ? defaultKey : _availableChannels[0].Key;
                }
                SelectChannel(targetKey);
            }
            else
            {
                _selectedTargetKey = null;
                _pnlSwatch.BackColor = Color.Gray;
                _lblChannelName.ForeColor = Color.Gray;
                _lblChannelName.Text = "(No Channel)";
                ClearMetrics();
            }
        }

        public void SetAvailableChannels(IEnumerable<ChannelComboItem> items, string defaultKey = null)
        {
            UpdateAvailableChannels(items, defaultKey);
        }

        public void SetChannels(IEnumerable<ChannelComboItem> items, string defaultKey = null)
        {
            UpdateAvailableChannels(items, defaultKey);
        }

        public bool SelectChannel(string key)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    return (bool)this.Invoke(new Func<bool>(() => SelectChannel(key)));
                }
                catch (ObjectDisposedException) { return false; }
                catch (InvalidOperationException) { return false; }
            }

            if (string.IsNullOrEmpty(key)) return false;

            var match = _availableChannels.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                bool changed = !string.Equals(_selectedTargetKey, match.Key, StringComparison.OrdinalIgnoreCase);
                _selectedTargetKey = match.Key;
                UpdateChannelDisplay(match.Key, match.DisplayName);

                if (changed)
                {
                    TargetChannelChanged?.Invoke(this, match.Key);
                }
                return true;
            }
            return false;
        }

        private void UpdateChannelDisplay(string key, string displayName)
        {
            Color c = Color.White;
            if (!string.IsNullOrEmpty(key) && _channelColors.TryGetValue(key, out Color mappedColor))
                c = mappedColor;

            _pnlSwatch.BackColor = c;
            _lblChannelName.ForeColor = c;
            _lblChannelName.Text = displayName ?? string.Empty;
        }

        public void SetChannelColor(string key, Color color)
        {
            if (string.IsNullOrEmpty(key)) return;
            _channelColors[key] = color;

            if (string.Equals(_selectedTargetKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _pnlSwatch.BackColor = color;
                _lblChannelName.ForeColor = color;
            }
            this.Invalidate();
        }

        /// <summary>
        /// 실시간으로 계산된 Span 메트릭(ΔX, ΔY)을 UI 라벨에 반영한다.
        /// </summary>
        public void UpdateMetrics(ScopeSpanMetrics m)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(() => UpdateMetrics(m)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            if (!m.HasValidData)
            {
                ClearMetrics();
                return;
            }

            _lblDeltaX.Text = ScopeSpanFormatter.FormatDeltaX(m);
            _lblDeltaY.Text = ScopeSpanFormatter.FormatDeltaY(m);
        }

        public void ClearMetrics()
        {
            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(ClearMetrics));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            _lblDeltaX.Text = "ΔX: --";
            _lblDeltaY.Text = "ΔY: --";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 패널 외곽선 (#2D2D34 / 45, 45, 52)
            using (var pen = new Pen(Color.FromArgb(45, 45, 52), 1))
            {
                g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }

            // 헤더 하단 구분선
            using (var pen = new Pen(Color.FromArgb(38, 38, 44), 1))
            {
                g.DrawLine(pen, 10, 19, this.Width - 12, 19);
            }
        }
    }

    public sealed class ChannelComboItem
    {
        public string Key { get; }
        public string DisplayName { get; }
        public Color Color { get; }

        public ChannelComboItem(string key, string displayName, Color color)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? key ?? string.Empty;
            Color = color;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
