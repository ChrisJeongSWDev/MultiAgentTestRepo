using VEOImagingSolutionSharp;
using System;
using System.Collections.Generic; // Added for List<>
using System.Drawing;
using System.Linq;                 // Added for Enumerable
using System.Threading.Tasks;
using System.Windows.Forms;
using VIEWORKS_AFTS.Interfaces;

namespace VIEWORKS_AFTS
{
    public partial class AFLogicTestForm : BaseLogicForm, IAFLogicModule
    {
        // AF 특화 상수 및 변수
        private const int SUB_PIXEL_SCALE_BIT = 4;
        private const int SUB_PIXEL_SCALE = 1 << SUB_PIXEL_SCALE_BIT;
        private ushort m_HaarFilterSizeFixed = 0;

        private byte[] m_AFProfileBuffer;
        private byte[] m_AFProfileBufferUpscale;
        private short[] m_HaarScore;
        private double[] m_dHaarScore;
        private double[] m_HaarScore_XPlane;
        private double[] m_AFProfileBufferArrayView;
        private double[] m_dProfileUpscaleBuffer;

        private int m_nCurrentFrameToken = -1;

        public ScopeViewControl m_HaarScopeView;
        public ScopeViewControl m_AlgoScopeView;
        public ScottPlotLibrary[] m_ScottPlotHandle = new ScottPlotLibrary[2];
        private const int HAAR_FILTER = 0;
        private const int AF_ZPOS = 1;

        private VIEWERForm m_AFImageViewForm;

        // 분석 모드 상태
        private int  m_nAFDataSetScrollIndex = -1;   // >=0: 비디오 데이터셋 모드, -1: 아님
        private bool m_bAFSingleImageReady   = false; // true: 단일 이미지 AF 버퍼 유효

        // AF Run Analysis 결과 (btnRunAnalysis_Click에서 채워지고 btnSaveAFResult_Click에서 CSV로 내보냄)
        private struct AFAnalysisResult
        {
            public int ImageIndex; public int PeakLevel; public int MaxHaarPos;
            public double MaxHaarValue; public double PeakPosition; public double ZRealPoS;
            // FPGA가 출력하는 Diff 원시값(정수). ZRealPoS = AFDiffFPGA * 1000.0f / 16384.0f.
            // ZOffset / 2048 sentinel 이 적용되지 않은 순수 원시값이다.
            public int AFDiffFPGA;
            public bool HaarValid; // false = frame acquire failed OR peakLevel <= threshold
        }
        private List<AFAnalysisResult> m_AFAnalysisResults;

        // 마지막으로 성공한 데이터셋 로드의 프레임 수. 분석 루프는 스크롤바 Maximum 대신
        // 이 값을 기준으로 돈다. 스크롤바는 UI 상태일 뿐이라, 로드 실패 등으로 실제 데이터셋과
        // 어긋나면 존재하지도 않는 프레임을 훑게 된다.
        private int m_nLoadedDataSetFrameCount = 0;

        // ── Phase 5: high-precision perf tracking for this form's OWN ring consumption path ──────
        // (AcquireMatFrame/ReleaseMatFrame in the live-mode branch of OnImageLoaded) -- an
        // independent vantage point for verifying the 300MB/s generator pipeline
        // (ImagingLogicForm.InitHighThroughputGenerator) doesn't starve/stall OTHER ring consumers
        // while it is running. NOTE: whether/how often OnImageLoaded itself fires during a
        // continuous 20Hz generator session is wired in BaseLogicForm, outside this phase's
        // write_scope (ImagingLogicForm.cs / AFLogicTestForm.cs only) -- this instrumentation is
        // additive and harmless regardless of that cadence, but its cadence during a live session
        // was not independently confirmed here.
        private long m_LiveAcquireAttemptCount;
        private long m_LiveAcquireFailureCount;
        private double m_LiveLastAcquireLatencyMs;
        private readonly object m_LivePerfLock = new object();

        /// <summary>Point-in-time snapshot returned by GetLiveAcquirePerfSnapshot().</summary>
        public struct LiveAcquirePerfSnapshot
        {
            public long AttemptCount;
            public long FailureCount;
            public double LastAcquireLatencyMs;
        }

        public AFLogicTestForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            NeutralizeStretchAnchors();
            LayoutWideControls();
            // Deferred via BeginInvoke: toggling AutoScroll while still inside OnLoad — i.e. while this
            // embedded (TopLevel=false) form's control/handle creation is still unwinding as part of
            // ShowModule()'s Controls.Add/Show() call — caused the app to silently vanish (no exception
            // dialog, no WER entry) during reproduction, consistent with AutoScroll's style change
            // forcing a handle recreation while control creation was still in progress. Posting it to
            // run after OnLoad (and the current layout pass) fully unwinds avoids that reentrancy.
            this.BeginInvoke(new Action(ResetAutoScrollExtent));
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            LayoutWideControls();
        }

        // WinForms' AutoScroll tracks a "widest/tallest child ever seen" high-water mark for its
        // scroll extent. InitializeComponent's own PerformLayout call runs (and bakes that extent in)
        // while the wide controls still have their Designer-declared 5559px-wide bounds (and
        // TextBox_BackDataBufferIndex.Left=5503) — i.e. before NeutralizeStretchAnchors/
        // LayoutWideControls ever get a chance to shrink them to the real ClientSize. That extent
        // does not shrink back down on its own afterward, which produces a spurious horizontal
        // AutoScroll scrollbar (confirmed via reproduction: dragging it reveals a large empty region
        // with no real content) even though every visible control now fits within ClientSize.Width.
        // Toggling AutoScroll off/on forces WinForms to discard that cached extent and recompute
        // strictly from the controls' current (correct) bounds. This only needs to run once, from
        // OnLoad: the poisoned extent is solely a one-time InitializeComponent artifact, and from
        // then on LayoutWideControls() keeps every wide control's Width within ClientSize.Width, so
        // the extent never grows stale again.
        //
        // IMPORTANT: do NOT call this from OnClientSizeChanged. Toggling AutoScroll changes
        // ClientSize as scrollbars appear/disappear, which re-enters OnClientSizeChanged; calling
        // this method from there recurses (each re-entry toggles AutoScroll again, changing
        // ClientSize again, ...) until a StackOverflowException silently kills the process — this
        // was hit and confirmed during reproduction (process vanished with no exception dialog and no
        // Windows Error Reporting entry, consistent with an uncatchable stack-overflow fast-fail).
        // The re-entrancy guard below is defense in depth in case any other caller is added later.
        private bool m_bInResetAutoScrollExtent = false;

        private void ResetAutoScrollExtent()
        {
            if (m_bInResetAutoScrollExtent) return;
            m_bInResetAutoScrollExtent = true;
            try
            {
                bool wasAutoScroll = this.AutoScroll;
                this.AutoScroll = false;
                this.AutoScroll = wasAutoScroll;
            }
            finally
            {
                m_bInResetAutoScrollExtent = false;
            }
        }

        // The Designer bakes a large negative right-anchor margin because these controls are 5559px wide
        // while the design-time ClientSize is (17,17). Drop the Right anchor so the explicit width
        // management in LayoutWideControls is authoritative and survives every parent resize.
        private void NeutralizeStretchAnchors()
        {
            VIEWPanel.Anchor                    = AnchorStyles.Top | AnchorStyles.Left;
            AF_HaarFilter_Response_Panel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            AFAlgorithm_Panel.Anchor            = AnchorStyles.Top | AnchorStyles.Left;
            hScrollBar_BackDataBuffer.Anchor    = AnchorStyles.Top | AnchorStyles.Left;
            TextBox_BackDataBufferIndex.Anchor  = AnchorStyles.Top | AnchorStyles.Left;
        }

        private void LayoutWideControls()
        {
            // Defensive: OnClientSizeChanged can fire during InitializeComponent before fields settle.
            if (AFAlgorithm_Panel == null || AF_HaarFilter_Response_Panel == null) return;
            if (this.ClientSize.Width <= 1) return;

            const int left = 34;       // matches Designer Location.X for all wide controls
            const int rightPad = 20;   // small breathing room at the right edge
            int contentWidth = Math.Max(50, this.ClientSize.Width - left - rightPad);

            // 1. AF Laser Viewer: 절반 크기로 축소 (115px)
            VIEWPanel.Left   = left;
            VIEWPanel.Top    = 195;
            VIEWPanel.Width  = contentWidth;
            VIEWPanel.Height = 115;

            // 2. hScrollBar & Index TextBox (버퍼 슬라이더 스트립)
            const int gap = 5;
            int idxWidth    = TextBox_BackDataBufferIndex.Width; // 90 per Designer
            int scrollWidth = Math.Max(50, contentWidth - idxWidth - gap);

            hScrollBar_BackDataBuffer.Left   = left;
            hScrollBar_BackDataBuffer.Top    = 314;
            hScrollBar_BackDataBuffer.Width  = scrollWidth;
            hScrollBar_BackDataBuffer.Height = 21;

            TextBox_BackDataBufferIndex.Left   = left + scrollWidth + gap;
            TextBox_BackDataBufferIndex.Top    = 312;
            TextBox_BackDataBufferIndex.Height = 24;

            // 3. Haar Scope & Algorithm Scope (각각 세로 305px로 대폭 확대, 총 높이 958px로 스크롤 없음)
            AF_HaarFilter_Response_Panel.Left   = left;
            AF_HaarFilter_Response_Panel.Top    = 340;
            AF_HaarFilter_Response_Panel.Width  = contentWidth;
            AF_HaarFilter_Response_Panel.Height = 305;

            AFAlgorithm_Panel.Left   = left;
            AFAlgorithm_Panel.Top    = AF_HaarFilter_Response_Panel.Bottom + 8; // 653
            AFAlgorithm_Panel.Width  = contentWidth;
            AFAlgorithm_Panel.Height = 305; // Bottom = 958px (1019px 클라이언트 영역 내 완벽 Fit)
        }

        // --- ILogicModule 구현 ---

        public override bool InitializeModule()
        {
            // 1. 부모의 공통 초기화 수행 (DLL 로드 등)
            if (!base.InitializeModule()) return false;

            // VIEWERForm 폼 임베딩 초기화
            m_AFImageViewForm = new VIEWERForm();
            m_AFImageViewForm.TopLevel = false;
            m_AFImageViewForm.FormBorderStyle = FormBorderStyle.None;
            m_AFImageViewForm.Dock = DockStyle.Fill;
            VIEWPanel.Controls.Clear();
            VIEWPanel.Controls.Add(m_AFImageViewForm);
            m_AFImageViewForm.Show();

            // UI에서 파라미터 파싱
            if (ushort.TryParse(TextBox_LaserWidthFilter.Text, out ushort nFilterSize))
                m_HaarFilterSizeFixed = nFilterSize;

            int nCOEA = 0;
            if (int.TryParse(TextBox_COEA.Text, out int parsedCOEA)) nCOEA = parsedCOEA;

            int nCOEB =0;
            if (int.TryParse(TextBox_COEB.Text, out int parsedCOEB)) nCOEB = parsedCOEB;

            int nPeakThreshold = 0;
            if (int.TryParse(TextBox_PeakThreshold.Text, out int parsedPeak)) nPeakThreshold = parsedPeak;

            // 2. AF 전용 라이브러리 초기화 (파라미터 추가 반영)
            m_VwOpenCVUtil.InitializeAFLib(nCOEA, nCOEB, nPeakThreshold, m_HaarFilterSizeFixed);

            // UI 초기값 설정 (UI 값이 비어있을 경우를 대비)
            TextBox_LaserWidthFilter.Text = m_HaarFilterSizeFixed.ToString();
            TextBox_COEA.Text = nCOEA.ToString();
            TextBox_COEB.Text = nCOEB.ToString();
            TextBox_PeakThreshold.Text = nPeakThreshold.ToString();

            // 3. 스코프(그래프) 초기화
            InitHaarFilterScope();
            InitAFZPoSScope();

            return true;
        }

        public override void ClearData()
        {
            TextBox_MaxHaarScore.Clear();
            TextBox_MaxHaarPosition.Clear();
            TextBox_PeakPosition.Clear();
            TextBox_AF_Diff_FPGA.Clear();
            TextBox_ZRealPoS.Clear();
            m_ScottPlotHandle[HAAR_FILTER]?.ClearTail(0);
            m_ScottPlotHandle[AF_ZPOS]?.ClearTail(0);
        }

        public override void DeinitializeModule()
        {
            if (m_bDeinitialized) return;

            if (m_AFImageViewForm != null)
            {
                if (!m_AFImageViewForm.IsDisposed)
                {
                    // Close() first so GRABBER_IMAGEVIEW_FormClosing fires and stops
                    // _renderDriver/_uiPollingDriver and any background worker thread before the
                    // form is torn down. Close() on a Form normally disposes it too, but guard
                    // IsDisposed again before the explicit Dispose() call below to avoid a
                    // double-dispose.
                    m_AFImageViewForm.Close();
                    if (!m_AFImageViewForm.IsDisposed)
                        m_AFImageViewForm.Dispose();
                }
                m_AFImageViewForm = null;
            }
            base.DeinitializeModule();
        }

        public override void LoadImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var res = m_VwOpenCVUtil.LoadAFLaserSingleImageFromFile(filePath);
            if (res != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS)
            {
                MessageBox.Show($"이미지 로드 실패: {res}");
                return;
            }

            m_nAFDataSetScrollIndex = -1;
            m_bAFSingleImageReady   = true;
            OnImageLoaded();
        }

        public override void SetInputImage(IntPtr pBuffer, int width, int height, int stride, PixelFormat format)
        {
            m_bAFSingleImageReady = false;
            m_nAFDataSetScrollIndex = -1;
            base.SetInputImage(pBuffer, width, height, stride, format);
        }

        protected override void OnImageLoaded()
        {
            if (m_AFImageViewForm == null || m_AFImageViewForm.IsDisposed) return;

            CvMatView view;

            if (m_nAFDataSetScrollIndex >= 0)
            {
                if (m_VwOpenCVUtil.GetAFLaserVideoFrame(m_nAFDataSetScrollIndex, out view)
                        == VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS && view.data != IntPtr.Zero)
                    m_AFImageViewForm.Push(view);
                return;
            }

            if (m_bAFSingleImageReady)
            {
                if (m_VwOpenCVUtil.GetAFLaserSingleImage(out view)
                        == VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS && view.data != IntPtr.Zero)
                    m_AFImageViewForm.Push(view);
                return;
            }

            // live 모드 — 최신 공용 ring frame 표시 (스캔 중일 때)
            int token = -1;
            // [Phase 5] Timed via Stopwatch so live-mode ring consumption can be independently
            // verified while ImagingLogicForm's high-throughput generator is streaming -- see
            // GetLiveAcquirePerfSnapshot(). Purely additive: the success/failure branches below are
            // unchanged from before this instrumentation was added.
            long acquireStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            var acquireRes = m_VwOpenCVUtil.AcquireMatFrame(out view, out token);
            double acquireLatencyMs = (System.Diagnostics.Stopwatch.GetTimestamp() - acquireStartTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            System.Threading.Interlocked.Increment(ref m_LiveAcquireAttemptCount);
            lock (m_LivePerfLock) { m_LiveLastAcquireLatencyMs = acquireLatencyMs; }

            if (acquireRes == VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS && view.data != IntPtr.Zero)
            {
                m_AFImageViewForm.Push(view, token);
                m_VwOpenCVUtil.ReleaseMatFrame(token);
            }
            else
            {
                System.Threading.Interlocked.Increment(ref m_LiveAcquireFailureCount);
            }
        }

        /// <summary>
        /// Phase 5: snapshot of this form's own AcquireMatFrame call counters (live-mode branch of
        /// OnImageLoaded only -- see that method). Safe to poll from any thread (e.g. a future
        /// diagnostics UI) while a generator session is running.
        /// </summary>
        public LiveAcquirePerfSnapshot GetLiveAcquirePerfSnapshot()
        {
            lock (m_LivePerfLock)
            {
                return new LiveAcquirePerfSnapshot
                {
                    AttemptCount = System.Threading.Interlocked.Read(ref m_LiveAcquireAttemptCount),
                    FailureCount = System.Threading.Interlocked.Read(ref m_LiveAcquireFailureCount),
                    LastAcquireLatencyMs = m_LiveLastAcquireLatencyMs
                };
            }
        }

        // --- IAFLogicModule 구현 ---

        public void RunAFTest()
        {
            VEOImagingSolutionSharp.CvMatView view = new VEOImagingSolutionSharp.CvMatView();

            if (m_nAFDataSetScrollIndex >= 0)
            {
                if (m_nCurrentFrameToken >= 0)
                {
                    m_VwOpenCVUtil.ReleaseMatFrame(m_nCurrentFrameToken);
                    m_nCurrentFrameToken = -1;
                }
                // 비디오 데이터셋 모드 — 직접 index 접근, acquire/release 불필요
                var getRes = m_VwOpenCVUtil.GetAFLaserVideoFrame(m_nAFDataSetScrollIndex, out view);
                if (getRes != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS || view.data == IntPtr.Zero)
                {
                    MessageBox.Show($"AF 비디오 프레임 획득 실패 (index={m_nAFDataSetScrollIndex}, result={getRes})");
                    return;
                }
            }
            else if (m_bAFSingleImageReady)
            {
                if (m_nCurrentFrameToken >= 0)
                {
                    m_VwOpenCVUtil.ReleaseMatFrame(m_nCurrentFrameToken);
                    m_nCurrentFrameToken = -1;
                }
                // 단일 이미지 모드 — AF 전용 버퍼, acquire/release 불필요
                var getRes = m_VwOpenCVUtil.GetAFLaserSingleImage(out view);
                if (getRes != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS || view.data == IntPtr.Zero)
                {
                    MessageBox.Show("AF 단일 이미지 버퍼가 비어있습니다. 이미지를 먼저 로드하세요.");
                    return;
                }
            }
            else
            {
                // live 카메라 모드 — 공용 ring (ImagingIntegratorForm 스캔 중 실시간 분석 경로 유지)
                if (m_nCurrentFrameToken >= 0)
                {
                    m_VwOpenCVUtil.ReleaseMatFrame(m_nCurrentFrameToken);
                    m_nCurrentFrameToken = -1;
                }
                var acquireRes = m_VwOpenCVUtil.AcquireMatFrame(out view, out m_nCurrentFrameToken);
                if (acquireRes != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS || view.data == IntPtr.Zero)
                {
                    MessageBox.Show("분석할 이미지가 없습니다.\n이미지를 로드하거나 카메라 스캔을 시작하세요.");
                    return;
                }
            }

            // --- 동적 해상도 대응: 이미지 크기에 따른 버퍼 재할당 ---
            int currentWidth = view.cols;
            int subPixelLength = currentWidth * SUB_PIXEL_SCALE;
            int rangeConvertParam = (currentWidth / 2) * SUB_PIXEL_SCALE;

            if (m_AFProfileBuffer == null || m_AFProfileBuffer.Length != currentWidth)
            {
                m_AFProfileBuffer = new byte[currentWidth];
                m_AFProfileBufferUpscale = new byte[subPixelLength];
                m_dProfileUpscaleBuffer = new double[subPixelLength];
                m_HaarScore = new short[subPixelLength];
                m_dHaarScore = new double[subPixelLength];
                m_HaarScore_XPlane = new double[subPixelLength];
                m_AFProfileBufferArrayView = new double[currentWidth];

                // X축 배열 초기화 (ScottPlot 용도)
                for (int i = 0; i < m_HaarScore_XPlane.Length; i++)
                    m_HaarScore_XPlane[i] = (short)(i - rangeConvertParam);

                // ScottPlot X축 스케일 업데이트
                m_HaarScopeView?.CreateScopeSpans();
            }

            // UI에서 파라미터 파싱 및 AF 라이브러리 재초기화
            if (ushort.TryParse(TextBox_LaserWidthFilter.Text, out ushort nFilterSize))
                m_HaarFilterSizeFixed = nFilterSize;

            int nCOEA = -30681;
            if (int.TryParse(TextBox_COEA.Text, out int parsedCOEA)) nCOEA = parsedCOEA;

            int nCOEB = 300;
            if (int.TryParse(TextBox_COEB.Text, out int parsedCOEB)) nCOEB = parsedCOEB;

            // Peak Threshold Window (Lower~Upper). Lower는 기존 PeakThreshold 필드를 재사용하며
            // 라이브러리 내부 lower threshold로 그대로 전달한다. Upper는 C# 측에서만 판정한다.
            int nPeakLower = 50;
            if (int.TryParse(TextBox_PeakThreshold.Text, out int parsedPeak)) nPeakLower = parsedPeak;

            int nPeakUpper = 255;
            if (int.TryParse(TextBox_PeakUpper.Text, out int parsedPeakUpper)) nPeakUpper = parsedPeakUpper;

            m_VwOpenCVUtil.InitializeAFLib(nCOEA, nCOEB, nPeakLower, m_HaarFilterSizeFixed);

            ushort haarFilterSizeSubPix = (ushort)(m_HaarFilterSizeFixed << SUB_PIXEL_SCALE_BIT);

            var algResult = m_VwOpenCVUtil.ProfileVerticalSum(view.data, (ushort)currentWidth, (ushort)view.rows, m_AFProfileBuffer);
            
            // --- Peak Threshold 미달 처리 ---
            if (algResult == VEOImagingSolutionSharp.API_RESULT.RESULT_PEAK_LOW_THRESHOLD)
            {
                TextBox_PeakLevel.BackColor = Color.Red;
                TextBox_PeakLevel.ForeColor = Color.White;
                // 기존 MessageBox.Show 및 ClearData() 호출은 제거, 처리 계속
            }
            else
            {
                // 정상 시 복구 (다크 테마 기본 배경색)
                TextBox_PeakLevel.BackColor = Color.FromArgb(0x1f, 0x1f, 0x1f);
                TextBox_PeakLevel.ForeColor = Color.White;
            }

            m_VwOpenCVUtil.UpscaleProfile(m_AFProfileBuffer, (ushort)currentWidth, m_AFProfileBufferUpscale);          
            m_VwOpenCVUtil.CalculateHaarScore(m_AFProfileBufferUpscale, (ushort)subPixelLength, haarFilterSizeSubPix, m_HaarScore);
            m_dHaarScore = m_VwOpenCVUtil.ToDoubleArray(m_HaarScore);
            
            ushort nLinePos_BeforeConverted = 0;
            m_VwOpenCVUtil.MaxHaarScorePosition(m_HaarScore, (ushort)subPixelLength, out nLinePos_BeforeConverted);
            short nLinePos = (short)(nLinePos_BeforeConverted - rangeConvertParam);

            float fPeakPosition = 0, fZRealPos = 0;
            m_VwOpenCVUtil.GetDetectedPeakPosition(nLinePos, (ushort)currentWidth, out fPeakPosition);
            m_VwOpenCVUtil.GetZRealPosition(nLinePos, out fZRealPos);

            // FPGA가 출력하는 Diff 원시값. ZRealPos 는 이 값을 (v / 16384.0f) * 1000.0f 로 환산한 것이며,
            // 여기서는 ZOffset / 2048 sentinel 을 적용하지 않은 원시값을 그대로 표시한다.
            int nAFDiffFPGA = 0;
            m_VwOpenCVUtil.GetZDiffPosition(nLinePos, out nAFDiffFPGA);

            // ZOffset 적용: 입력값이 유효하면 최종 계산된 ZRealPos에 가산하여 출력
            double zOffset = 0;
            double.TryParse(TextBox_ZOffset.Text, out zOffset);
            double dZRealPos = fZRealPos + zOffset;

            // Peak Level 계산 (Profile Buffer에서 최대값 추출)
            byte peakLevel = 0;
            for (int i = 0; i < m_AFProfileBuffer.Length; i++)
            {
                if (m_AFProfileBuffer[i] > peakLevel) peakLevel = m_AFProfileBuffer[i];
            }

            // Peak Window(Lower~Upper) 판정: peak가 범위를 벗어나면(너무 어둡거나 너무 밝으면)
            // 변위를 신뢰할 수 없으므로 2048 예외처리 엔딩 (예: Upper=120인데 peak=253 -> 2048).
            bool bPeakInWindow = (peakLevel >= nPeakLower && peakLevel <= nPeakUpper);
            if (!bPeakInWindow)
            {
                dZRealPos = 2048.0;
                TextBox_PeakLevel.BackColor = Color.Red;   // 범위 이탈 시각 표시 (Upper 초과 포함)
                TextBox_PeakLevel.ForeColor = Color.White;
            }

            m_AFProfileBufferArrayView = m_VwOpenCVUtil.ToDoubleArray(m_AFProfileBuffer);

            if (m_AFImageViewForm != null && !m_AFImageViewForm.IsDisposed)
            {
                m_AFImageViewForm.CheckGuideLine = true;
                m_AFImageViewForm.CheckDetectedLine = true;
                m_AFImageViewForm.UpdatePeakPoS((int)fPeakPosition);
                m_AFImageViewForm.UpdatePeakLevel(peakLevel);
                m_AFImageViewForm.Push(view, m_nCurrentFrameToken);
            }

            TextBox_PeakLevel.Text = peakLevel.ToString();
            TextBox_MaxHaarScore.Text = string.Format("{0:0.00}", m_dHaarScore[nLinePos_BeforeConverted]);
            TextBox_MaxHaarPosition.Text = string.Format("{0}", nLinePos);
            TextBox_PeakPosition.Text = string.Format("{0:0.00}", fPeakPosition);
            TextBox_AF_Diff_FPGA.Text = nAFDiffFPGA.ToString();
            TextBox_ZRealPoS.Text = string.Format("{0:0.00}", dZRealPos);

            if (m_HaarScopeView != null)
            {
                m_HaarScopeView.UpdateChannelData("Haar_Response", m_HaarScore_XPlane, m_dHaarScore, refresh: false);
                if (m_AFProfileBufferUpscale != null)
                {
                    if (m_dProfileUpscaleBuffer == null || m_dProfileUpscaleBuffer.Length != m_AFProfileBufferUpscale.Length)
                        m_dProfileUpscaleBuffer = new double[m_AFProfileBufferUpscale.Length];

                    for (int p = 0; p < m_AFProfileBufferUpscale.Length; p++)
                        m_dProfileUpscaleBuffer[p] = m_AFProfileBufferUpscale[p];

                    m_HaarScopeView.UpdateChannelData("1D_Profile_VerticalSum", m_HaarScore_XPlane, m_dProfileUpscaleBuffer, refresh: false);
                }
                m_HaarScopeView.RefreshPlots(autoScale: false);
            }
        }

        public void ToggleSimulation(bool start) { /* 구현 필요 시 추가 */ }

        // --- 내부 버튼 이벤트 (Designer 참조) ---

        private void btnLoadImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Bitmap Image (*.bmp)|*.bmp";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.LoadImage(openFileDialog.FileName);
            }
        }

        private void btnRunAFTest_Click(object sender, EventArgs e)
        {
            this.RunAFTest();
        }

        private void LoadAFCalibrationParam_btn_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV Report (*.csv)|*.csv";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(openFileDialog.FileName);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length < 2) continue;

                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        switch (key)
                        {
                            case "COEF_A":
                                TextBox_COEA.Text = value;
                                break;
                            case "COEF_B":
                                TextBox_COEB.Text = value;
                                break;
                            case "LaserWidthFilter":
                                TextBox_LaserWidthFilter.Text = value;
                                break;
                            case "PeakThreshold": // 하위호환: 기존 리포트의 PeakThreshold = Peak Lower
                            case "PeakLower":
                                TextBox_PeakThreshold.Text = value;
                                break;
                            case "PeakUpper":
                                TextBox_PeakUpper.Text = value;
                                break;
                            case "PeakCount":
                                TextBox_PeakCount.Text = value;
                                break;
                            case "PeakIndex":
                                TextBox_PeakIndex.Text = value;
                                break;
                        }
                    }
                    MessageBox.Show("Calibration Report 로드 및 UI 반영이 완료되었습니다.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Report 로드 중 오류 발생: " + ex.Message);
                }
            }
        }

        private void btnLoadVideo_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog o = new OpenFileDialog
            {
                Title = "Select AF Laser Video or RAW Frame for Extraction",
                Filter = "AF DataSet (*.mp4;*.avi;*.raw)|*.mp4;*.avi;*.raw|Video (*.mp4;*.avi)|*.mp4;*.avi|RAW Frames (*.raw)|*.raw"
            })
            {
                if (o.ShowDialog() != DialogResult.OK) return;

                // .raw 를 고르면 "그 파일이 들어있는 폴더"를 통째로 임포트한다. 폴더 선택 UI를 따로 두지 않고
                // 파일 다이얼로그를 재사용하므로, 사용자는 대상 폴더 안의 아무 .raw 파일이나 지정하면 된다.
                if (string.Equals(System.IO.Path.GetExtension(o.FileName), ".raw", StringComparison.OrdinalIgnoreCase))
                    LoadRawImageDataSet(o.FileName);
                else
                    LoadVideoDataSet(o.FileName);
            }
        }

        private void LoadVideoDataSet(string videoPath)
        {
            // 비동기 작업 중 UI 멈춤 방지를 위해 Task.Run 사용
            Task.Run(() =>
            {
                try
                {
                    int loadedCount = 0;
                    var res = m_VwOpenCVUtil.LoadAFLaserVideoDataSet(videoPath, out loadedCount);
                    if (res != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS || loadedCount <= 0)
                    {
                        throw new Exception($"비디오 로드에 실패했습니다. (Result: {res})");
                    }

                    this.Invoke(new Action(() => ApplyDataSetLoaded(
                        loadedCount, $"Loaded {loadedCount} frames to AF Video Dataset successfully.")));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => MessageBox.Show($"DataSet Load Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        // RAW 프레임 폴더 임포트.
        // 선택한 파일에서 폴더와 접두어를 뽑아 "<접두어><N>.raw" 를 N=0 부터 결번 직전까지 읽는다.
        // 0번부터 연속으로 읽기 때문에 스크롤 인덱스 i 와 파일 번호 i 가 항상 1:1로 대응한다.
        private void LoadRawImageDataSet(string selectedRawPath)
        {
            string folder = System.IO.Path.GetDirectoryName(selectedRawPath);
            string ext    = System.IO.Path.GetExtension(selectedRawPath);              // ".raw"
            string name   = System.IO.Path.GetFileNameWithoutExtension(selectedRawPath);

            // "AFCAM_RAW_IMAGE_123" -> 접두어 "AFCAM_RAW_IMAGE_" (뒤쪽 연속 숫자를 잘라낸다)
            int cut = name.Length;
            while (cut > 0 && char.IsDigit(name[cut - 1])) cut--;
            if (cut == name.Length)
            {
                MessageBox.Show("파일명이 '<접두어><번호>.raw' 형식이 아닙니다.\n예: AFCAM_RAW_IMAGE_0.raw",
                                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string prefix = name.Substring(0, cut);

            // 0번이 있어야 인덱스 대응이 성립하므로 먼저 확인한다.
            string firstPath = System.IO.Path.Combine(folder, prefix + "0" + ext);
            if (!System.IO.File.Exists(firstPath))
            {
                MessageBox.Show($"{prefix}0{ext} 를 찾을 수 없습니다.\nRAW 데이터셋은 0번부터 시작해야 합니다.",
                                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 결번 직전까지의 프레임 수를 미리 세어 다이얼로그에 보여준다.
            int detected = 0;
            while (System.IO.File.Exists(System.IO.Path.Combine(folder, prefix + detected.ToString() + ext)))
                detected++;

            long fileSize = new System.IO.FileInfo(firstPath).Length;

            // 폴더 전체 파일 수와 다르면 중간 결번이 있다는 뜻 -> 로드 후 안내에 사용
            int totalInFolder = System.IO.Directory.GetFiles(folder, prefix + "*" + ext).Length;

            int width, height;
            using (var opt = new RawImportOptionsForm(fileSize, detected, prefix))
            {
                if (opt.ShowDialog(this) != DialogResult.OK) return;
                width  = opt.FrameWidth;
                height = opt.FrameHeight;
            }

            Task.Run(() =>
            {
                try
                {
                    int loadedCount = 0;
                    var res = m_VwOpenCVUtil.LoadAFLaserRawImageDataSet(folder, prefix, ext, 0, width, height, out loadedCount);
                    if (res != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS || loadedCount <= 0)
                    {
                        throw new Exception($"RAW 폴더 로드에 실패했습니다. (Result: {res}, Loaded: {loadedCount})\n"
                                          + $"지정한 해상도 {width}x{height} 와 파일 크기가 맞는지 확인해주세요.");
                    }

                    string msg = $"Loaded {loadedCount} frames from RAW folder. ({prefix}0 ~ {prefix}{loadedCount - 1}, {width}x{height})";
                    if (totalInFolder > loadedCount)
                    {
                        msg += $"\n\n{prefix}{loadedCount}{ext} 없음 — 해당 번호에서 중단했습니다."
                             + $"\n(폴더에는 {totalInFolder}개가 있으나 연속 구간만 로드)";
                    }

                    this.Invoke(new Action(() => ApplyDataSetLoaded(loadedCount, msg)));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => MessageBox.Show($"DataSet Load Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        // 비디오 / RAW 두 경로가 공유하는 로드 후 UI 갱신 (UI 스레드에서 호출할 것).
        private void ApplyDataSetLoaded(int loadedCount, string message)
        {
            m_bAFSingleImageReady      = false;
            m_nAFDataSetScrollIndex    = -1; // 스크롤 전까지는 미결정
            m_nLoadedDataSetFrameCount = loadedCount;

            hScrollBar_BackDataBuffer.Minimum     = 0;
            hScrollBar_BackDataBuffer.SmallChange = 1;
            hScrollBar_BackDataBuffer.LargeChange = 1;   // so the last frame (== Maximum) is reachable
            hScrollBar_BackDataBuffer.Maximum     = loadedCount > 0 ? loadedCount - 1 : 0;
            hScrollBar_BackDataBuffer.Value       = 0;
            hScrollBar_BackDataBuffer.Visible     = true;
            TextBox_BackDataBufferIndex.Text      = $"1 / {loadedCount}";
            TextBox_BackDataBufferIndex.Visible   = true;

            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 첫 프레임 강제 트리거
            if (loadedCount > 0)
                hScrollBar_BackDataBuffer_Scroll(this, new ScrollEventArgs(ScrollEventType.EndScroll, 0));
        }
        private void btnRunAnalysis_Click(object sender, EventArgs e)
        {
            // 0. BackDataBuffer가 비어있는지 확인
            if (hScrollBar_BackDataBuffer.Maximum <= 0)
            {
                MessageBox.Show("분석할 프레임이 없습니다. 먼저 데이터셋(Video 또는 RAW 폴더)을 로드해주세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Peak Threshold Window 파싱 (Lower / Upper). Lower~Upper 범위 안의 peak만 변위 계산.
            if (!int.TryParse(TextBox_PeakThreshold.Text, out int nPeakLower))
            {
                MessageBox.Show("유효한 Peak Lower 값을 입력해주세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!int.TryParse(TextBox_PeakUpper.Text, out int nPeakUpper))
            {
                MessageBox.Show("유효한 Peak Upper 값을 입력해주세요.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (nPeakUpper < nPeakLower)
            {
                MessageBox.Show("Peak Upper는 Peak Lower보다 크거나 같아야 합니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Analysis Exposure Mode 선택 읽기 (UI 스레드에서 캡처, 백그라운드 스레드로 안전 전달)
            // groupSize = 그룹당 프레임 수: Single=1(축소 없음), Dual=2, Triple=3
            int groupSize = radioMultiExpTriple.Checked ? 3
                          : radioMultiExpDual.Checked   ? 2
                          : 1;

            // ZOffset 읽기 (UI 스레드에서 캡처). 입력이 유효하면 유효 검출 프레임의
            // 최종 ZRealPos에 가산한다. (미검출 2048 sentinel에는 적용하지 않음)
            double zOffset = 0;
            double.TryParse(TextBox_ZOffset.Text, out zOffset);

            // UI 동결 및 진행 표시 (선택 사항)
            this.Enabled = false; // UI 비활성화하여 중복 클릭 방지 (필요 시 ProgressBar 등 추가)

            Task.Run(() =>
            {
                List<double> zRealPosValues = new List<double>();
                List<AFAnalysisResult> afResults = new List<AFAnalysisResult>();
                // 실제 로드된 프레임 수 기준. 예전에는 스크롤바 Maximum 을 썼는데, 로드가 실패해서
                // 데이터셋이 비었는데 스크롤바만 이전 값으로 남아 있으면 전 프레임이 취득 실패하고
                // CSV 에 빈 행만 쌓였다.
                int totalFrames = m_nLoadedDataSetFrameCount > 0
                                ? m_nLoadedDataSetFrameCount
                                : hScrollBar_BackDataBuffer.Maximum + 1;
                int acquireFailCount = 0;

                // RunAFTest에서 사용되는 상수/변수들 미리 준비 (스레드 안전하게 복사)
                ushort haarFilterSizeFixed = m_HaarFilterSizeFixed;
                int subPixelScaleBit = SUB_PIXEL_SCALE_BIT;

                for (int i = 0; i < totalFrames; i++)
                {
                    VEOImagingSolutionSharp.CvMatView view = new VEOImagingSolutionSharp.CvMatView();
                    var getRes = m_VwOpenCVUtil.GetAFLaserVideoFrame(i, out view);
                    if (getRes != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS || view.data == IntPtr.Zero)
                    {
                        acquireFailCount++;
                        zRealPosValues.Add(2048.0);
                        afResults.Add(new AFAnalysisResult { ImageIndex = i, PeakLevel = 0, HaarValid = false, ZRealPoS = 2048.0 });
                        continue;
                    }

                    // 3. Peak Level 계산 (CvMatView 다이렉트 접근)
                    int currentWidth = view.cols;
                    byte[] currentAFProfileBuffer = new byte[currentWidth];
                    m_VwOpenCVUtil.ProfileVerticalSum(view.data, (ushort)currentWidth, (ushort)view.rows, currentAFProfileBuffer);

                    byte peakLevel = 0;
                    for (int k = 0; k < currentAFProfileBuffer.Length; k++)
                    {
                        if (currentAFProfileBuffer[k] > peakLevel) peakLevel = currentAFProfileBuffer[k];
                    }

                    // 4. Peak Window(Lower~Upper) 비교 및 ZRealPos 또는 2048 저장
                    //    peak가 [Lower, Upper] 범위 안일 때만 변위 계산, 벗어나면 2048 예외처리 엔딩
                    //    (예: Upper=120인데 peak=253인 밝은 신호 -> 2048)
                    if (peakLevel >= nPeakLower && peakLevel <= nPeakUpper)
                    {
                        // ZRealPos 계산 (RunAFTest의 해당 로직 재활용)
                        int subPixelLength = currentWidth * (1 << subPixelScaleBit); // SUB_PIXEL_SCALE_BIT 사용
                        int rangeConvertParam = (currentWidth / 2) * (1 << subPixelScaleBit);

                        byte[] currentAFProfileBufferUpscale = new byte[subPixelLength];
                        short[] currentHaarScore = new short[subPixelLength];

                        m_VwOpenCVUtil.UpscaleProfile(currentAFProfileBuffer, (ushort)currentWidth, currentAFProfileBufferUpscale);
                        m_VwOpenCVUtil.CalculateHaarScore(currentAFProfileBufferUpscale, (ushort)subPixelLength, (ushort)(haarFilterSizeFixed << subPixelScaleBit), currentHaarScore);

                        ushort nLinePos_BeforeConverted = 0;
                        m_VwOpenCVUtil.MaxHaarScorePosition(currentHaarScore, (ushort)subPixelLength, out nLinePos_BeforeConverted);
                        short nLinePos = (short)(nLinePos_BeforeConverted - rangeConvertParam);

                        float fPeakPosition = 0, fZRealPos = 0;
                        m_VwOpenCVUtil.GetDetectedPeakPosition(nLinePos, (ushort)currentWidth, out fPeakPosition);
                        m_VwOpenCVUtil.GetZRealPosition(nLinePos, out fZRealPos);

                        // FPGA Diff 원시값 (ZOffset / sentinel 미적용)
                        int nAFDiffFPGA = 0;
                        m_VwOpenCVUtil.GetZDiffPosition(nLinePos, out nAFDiffFPGA);

                        double dZRealPos = fZRealPos + zOffset; // ZOffset 적용
                        zRealPosValues.Add(dZRealPos);
                        afResults.Add(new AFAnalysisResult
                        {
                            ImageIndex = i,
                            PeakLevel = peakLevel,
                            MaxHaarPos = nLinePos,
                            MaxHaarValue = (double)currentHaarScore[nLinePos_BeforeConverted],
                            PeakPosition = fPeakPosition,
                            AFDiffFPGA = nAFDiffFPGA,
                            ZRealPoS = dZRealPos,
                            HaarValid = true
                        });
                    }
                    else
                    {
                        zRealPosValues.Add(2048.0); // Peak Threshold 이하일 경우 2048 출력
                        afResults.Add(new AFAnalysisResult { ImageIndex = i, PeakLevel = peakLevel, HaarValid = false, ZRealPoS = 2048.0 });
                    }
                }

                // 5. Multi Exposure 그룹 축소
                //    groupSize=1(Single)이면 축소 없이 그대로, Dual=2 / Triple=3이면
                //    연속 프레임을 groupSize개씩 홀짝 그룹으로 묶어 그룹 내 Max Haar Score가
                //    가장 높은 프레임만 승자로 남기고, 그 승자의 ZRealPoS를 최종값으로 사용한다.
                //    (예: 5100 프레임 + Dual -> 2550개 그룹, 각 그룹의 승자 ZRealPoS)
                List<AFAnalysisResult> finalResults = ReduceByMaxHaarScore(afResults, groupSize);

                // 6. UI 업데이트 (메인 스레드에서 Plot에 데이터 뿌리기)
                // 전 프레임 취득 실패 = 데이터셋이 비었다는 뜻. 이 상태로 결과를 남기면
                // Save AF Result 가 값이 전부 빈 CSV 를 만들어 내므로 여기서 명시적으로 중단한다.
                if (afResults.Count > 0 && acquireFailCount == afResults.Count)
                {
                    this.Invoke(new Action(() =>
                    {
                        this.Enabled = true;
                        MessageBox.Show(
                            $"프레임을 하나도 가져오지 못했습니다. ({afResults.Count}개 전부 실패)\n"
                            + "데이터셋이 비어 있거나 로드가 실패한 상태입니다. Video 또는 RAW 폴더를 다시 로드해주세요.",
                            "분석 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                    return;
                }

                this.Invoke(new Action(() =>
                {
                    m_AFAnalysisResults = finalResults;

                    // AFAlgorithm_Panel (m_ScottPlotHandle[AF_ZPOS])에 데이터 뿌리기 (그룹 축소 후 최종 ZRealPoS)
                    double[] xData = Enumerable.Range(0, finalResults.Count).Select(i => (double)i).ToArray();
                    double[] rawZ = finalResults.Select(r => r.ZRealPoS).ToArray();
                    double[] procZ = ApplyVerificationAlgorithm(rawZ);

                    if (m_AlgoScopeView != null)
                    {
                        m_AlgoScopeView.UpdateChannelData("ProcRawData", xData, rawZ, refresh: false);
                        m_AlgoScopeView.UpdateChannelData("ProcData", xData, procZ, refresh: false);
                        m_AlgoScopeView.RefreshPlots(autoScale: true);
                    }

                    // UI 복구 및 상태 메시지
                    this.Enabled = true;
                    string modeName = groupSize == 3 ? "Multi Exposure (Triple)"
                                    : groupSize == 2 ? "Multi Exposure (Dual)"
                                    : "Single Detection";
                    MessageBox.Show(
                        $"모드: {modeName}\n원본 프레임 {afResults.Count}개 -> 최종 {finalResults.Count}개 분석 완료.",
                        "분석 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            });
        }

        // Multi Exposure 그룹 축소:
        // src를 앞에서부터 groupSize개씩 연속 그룹으로 묶고, 각 그룹에서 Max Haar Score가
        // 가장 높은(= 최고 신뢰도) 프레임 하나만 승자로 남긴 리스트를 반환한다.
        //  - groupSize <= 1 : 축소 없이 원본을 그대로 반환 (Single Detection Mode).
        //  - 마지막 그룹이 groupSize보다 작으면(총 개수가 나누어떨어지지 않으면) 남은 프레임끼리만 비교.
        // 승자 선택 규칙: HaarValid == true 프레임을 우선하고(무효 프레임의 0점이 유효한 음수
        // 점수를 이기지 못하도록), 유효/무효가 같으면 MaxHaarValue가 큰 쪽을 택한다.
        private static List<AFAnalysisResult> ReduceByMaxHaarScore(List<AFAnalysisResult> src, int groupSize)
        {
            if (src == null || groupSize <= 1) return src ?? new List<AFAnalysisResult>();

            var reduced = new List<AFAnalysisResult>((src.Count + groupSize - 1) / groupSize);
            for (int i = 0; i < src.Count; i += groupSize)
            {
                int end = Math.Min(i + groupSize, src.Count);
                AFAnalysisResult best = src[i];
                for (int j = i + 1; j < end; j++)
                {
                    if (IsBetterHaar(src[j], best)) best = src[j];
                }
                reduced.Add(best);
            }
            return reduced;
        }

        // candidate가 current보다 더 좋은(더 신뢰할 만한) Haar 검출 결과이면 true.
        private static bool IsBetterHaar(AFAnalysisResult candidate, AFAnalysisResult current)
        {
            if (candidate.HaarValid != current.HaarValid) return candidate.HaarValid;
            return candidate.MaxHaarValue > current.MaxHaarValue;
        }

        private void btnSaveAFResult_Click(object sender, EventArgs e)
        {
            if (m_AFAnalysisResults == null || m_AFAnalysisResults.Count == 0)
            {
                MessageBox.Show("저장할 AF 분석 결과가 없습니다. 먼저 Run Analysis를 실행해주세요.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog { Filter = "CSV Report (*.csv)|*.csv", FileName = "AFAnalysisResult.csv" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var lines = new List<string>();

                    // Metadata / Algorithm Parameter section (same 6 keys as LoadAFCalibrationParam_btn_Click)
                    lines.Add($"COEF_A,{TextBox_COEA.Text}");
                    lines.Add($"COEF_B,{TextBox_COEB.Text}");
                    lines.Add($"LaserWidthFilter,{TextBox_LaserWidthFilter.Text}");
                    lines.Add($"PeakThreshold,{TextBox_PeakThreshold.Text}"); // = Peak Lower (하위호환 키 유지)
                    lines.Add($"PeakLower,{TextBox_PeakThreshold.Text}");
                    lines.Add($"PeakUpper,{TextBox_PeakUpper.Text}");
                    lines.Add($"PeakCount,{TextBox_PeakCount.Text}");
                    lines.Add($"PeakIndex,{TextBox_PeakIndex.Text}");
                    lines.Add($"Timestamp,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    lines.Add($"FrameCount,{m_AFAnalysisResults.Count}");

                    // Blank separator line
                    lines.Add(string.Empty);

                    // Data table
                    lines.Add("Image Index,Peak Level,Max Haar PoS,Max Haar Value,Peak Position,AF_Diff_FPGA,ZRealPoS");
                    foreach (var r in m_AFAnalysisResults)
                    {
                        string maxHaarPos = r.HaarValid ? r.MaxHaarPos.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
                        string maxHaarValue = r.HaarValid ? r.MaxHaarValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "";
                        string peakPosition = r.HaarValid ? r.PeakPosition.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "";
                        // AF_Diff_FPGA: FPGA 원시 Diff. 무효 프레임(HaarValid=false)은 계산 자체가 없으므로 공란.
                        string afDiffFPGA = r.HaarValid ? r.AFDiffFPGA.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
                        string zRealPoS = r.ZRealPoS.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

                        lines.Add($"{r.ImageIndex},{r.PeakLevel},{maxHaarPos},{maxHaarValue},{peakPosition},{afDiffFPGA},{zRealPoS}");
                    }

                    System.IO.File.WriteAllLines(sfd.FileName, lines);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("AF Result 저장 중 오류 발생: " + ex.Message);
                }
            }
        }

        // --- ScottPlot 설정 및 이벤트 핸들러 ---

        /// <summary>
        /// ProcRawData에 개발 및 검증하려는 로직을 적용하여 ProcData를 산출하는 슬롯 메서드.
        /// (추후 필터링, 이동평균, 이상치 보정 등 신규 알고리즘 로직을 자유롭게 연결)
        /// </summary>
        private double[] ApplyVerificationAlgorithm(double[] rawData)
        {
            if (rawData == null || rawData.Length == 0) return Array.Empty<double>();

            double[] processed = new double[rawData.Length];
            const int window = 3;
            for (int i = 0; i < rawData.Length; i++)
            {
                int start = Math.Max(0, i - window / 2);
                int end = Math.Min(rawData.Length - 1, i + window / 2);
                double sum = 0;
                int count = 0;
                for (int k = start; k <= end; k++)
                {
                    if (!double.IsNaN(rawData[k]) && !double.IsInfinity(rawData[k]) && rawData[k] < 2000.0)
                    {
                        sum += rawData[k];
                        count++;
                    }
                }
                processed[i] = count > 0 ? (sum / count) : rawData[i];
            }
            return processed;
        }

        private void InitHaarFilterScope()
        {
            AF_HaarFilter_Response_Panel.Controls.Clear();
            m_HaarScopeView = new ScopeViewControl { Dock = DockStyle.Fill };
            AF_HaarFilter_Response_Panel.Controls.Add(m_HaarScopeView);

            m_HaarScopeView.InitialScope(
                xLabel: "Pixel (Subpixel)",
                yLeftLabel: "Haar Filter Response",
                yRightLabel: "1D Profile (Vertical Sum)"
            );

            m_HaarScopeView.RegisterChannel("Haar_Response", "Haar Response", Color.OrangeRed, useRightAxis: false, unitX: "px", unitY: "");
            m_HaarScopeView.RegisterChannel("1D_Profile_VerticalSum", "1D Profile (Vertical Sum)", Color.YellowGreen, useRightAxis: true, unitX: "px", unitY: "");
            m_HaarScopeView.SetRightAxisLimits(0, 255);

            m_ScottPlotHandle[HAAR_FILTER] = m_HaarScopeView.m_ScottPlotHandle;
        }

        private void InitAFZPoSScope()
        {
            AFAlgorithm_Panel.Controls.Clear();
            m_AlgoScopeView = new ScopeViewControl { Dock = DockStyle.Fill };
            AFAlgorithm_Panel.Controls.Add(m_AlgoScopeView);

            m_AlgoScopeView.InitialScope(
                xLabel: "Frame / Image Index",
                yLeftLabel: "Z-Position (um)"
            );

            m_AlgoScopeView.RegisterChannel("ProcRawData", "ProcRawData (Raw Sensing)", Color.Aqua, useRightAxis: false, unitX: "Frame", unitY: "µm");
            m_AlgoScopeView.RegisterChannel("ProcData", "ProcData (Algorithm Applied)", Color.OrangeRed, useRightAxis: false, unitX: "Frame", unitY: "µm");

            m_ScottPlotHandle[AF_ZPOS] = m_AlgoScopeView.m_ScottPlotHandle;
        }

        private string BuildHaarSpanAnnotationText()
        {
            return BuildGenericSpanAnnotationText(HAAR_FILTER, "Pixel");
        }

        private string BuildZPosSpanAnnotationText()
        {
            return BuildGenericSpanAnnotationText(AF_ZPOS, "Frame");
        }

        private string BuildGenericSpanAnnotationText(int plotIndex, string xUnit)
        {
            var hSpan = m_ScottPlotHandle[plotIndex].HSpan;
            if (hSpan == null)
                return "";

            double xMin = Math.Min(hSpan.X1, hSpan.X2);
            double xMax = Math.Max(hSpan.X1, hSpan.X2);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"X Span: {xMin:F1} ~ {xMax:F1} ({xMax - xMin:F1} {xUnit})");

            var vLeft = m_ScottPlotHandle[plotIndex].VSpanLeft;
            if (vLeft != null)
            {
                double yL1 = Math.Min(vLeft.Y1, vLeft.Y2);
                double yL2 = Math.Max(vLeft.Y1, vLeft.Y2);
                sb.AppendLine($"Y Span: {yL1:F1} ~ {yL2:F1} ({yL2 - yL1:F1})");
            }

            // 해당 구간(Channel 0)의 통계 데이터 추출
            if (m_ScottPlotHandle[plotIndex].TryGetSpanMetricsForChannel(0, out var metrics))
            {
                if (metrics.IdxMax >= metrics.IdxMin)
                {
                    sb.AppendLine("--- Data Stats ---");
                    sb.AppendLine($"  Min: {metrics.YMin:F2}, Max: {metrics.YMax:F2}");
                    sb.AppendLine($"  Avg: {metrics.YMean:F2}, Delta: {metrics.YDelta:F2}");
                }
                else
                {
                    sb.AppendLine("Not enough data in X range.");
                }
            }
            else
            {
                 sb.AppendLine("No data in current span.");
            }

            return sb.ToString();
        }

        private void hScrollBar_BackDataBuffer_Scroll(object sender, ScrollEventArgs e)
        {
            int frameIndex = e.NewValue;
            TextBox_BackDataBufferIndex.Text = $"{frameIndex + 1} / {hScrollBar_BackDataBuffer.Maximum + 1}";

            m_nAFDataSetScrollIndex = frameIndex;
            m_bAFSingleImageReady   = false; // 비디오 모드로 전환
            this.RunAFTest();
        }

    }
}
