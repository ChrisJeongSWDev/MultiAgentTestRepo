using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using VEOImagingSolutionSharp;

namespace VIEWORKS_AFTS
{
    public partial class VIEWERForm : Form
    {
        private Bitmap ViewerImage;
        private object _latestLock = new object();
        private CvMatView _latest;
        private bool _hasLatest = false;

        private double _X = 0;
        private double _Y = 0;
        private CvMatView ImageView;

        private bool m_bGuideLine;
        private bool m_bDetectedLine;
        private double m_dPeakPositionValue; // 실시간 피크 위치 저장용 필드
        private int _lastX = -1;
        private int _lastY = -1;

        // --- [Pull 방식] UI 일괄 갱신용 backing fields ---
        private volatile float  _latestFps     = 0f;
        private volatile int    _latestFrame   = 0;
        private volatile int    _latestPixelX  = -1;
        private volatile int    _latestPixelY  = -1;
        private volatile int    _latestIntensity = 0;
        private volatile int    _latestRawMax    = 255;  // Raw 픽셀 최대값 (Mono8=255, Mono12=4095 등)
        private volatile bool   _latestIsRaw      = false; // true: OpenCVUtil 원본 Raw 값, false: Display 8-bit
        private volatile bool   _pendingPixelInfo = false; // 픽셀 정보 갱신 요청 플래그
        private volatile bool   _pendingRender    = false; // UI 렌더 큐 중복 방지 플래그
        private volatile bool   _liveMode         = false; // true: live grabbing, false: static display/scrolling review

        // viewer가 현재 표시 중인 VEO ring token. release는 VIEWERForm 책임.
        private int _displayToken = -1;

        // 백그라운드 30Hz 렌더 드라이버: WM_TIMER/마우스 입력에 의존하지 않고 OnPaint를 강제 유발한다.
        private System.Threading.Timer _renderDriver;
        private volatile bool _renderTickInFlight = false;

        // UI 라벨 갱신 폴링 드라이버 (WinForms Timer 대체)
        // WinForms Timer(WM_TIMER)는 큐가 비었을 때만 생성되므로
        // 드래그 중 WM_MOUSEMOVE가 쏟아지면 갱신이 멈춘다.
        // System.Threading.Timer + BeginInvoke는 WM_APP으로 직접 큐에 삽입하므로
        // 큐가 차있어도 처리된다.
        private System.Threading.Timer _uiPollingDriver;
        private volatile bool _uiPollInFlight = false;

        System.Drawing.Point VerticalStartPoint;
        System.Drawing.Point VerticalEndPoint;

        System.Drawing.Point VerticalStartPoint_Left;
        System.Drawing.Point VerticalEndPoint_Left;

        System.Drawing.Point VerticalStartPoint_Right;
        System.Drawing.Point VerticalEndPoint_Right;

        public VIEWERForm()
        {
            InitializeComponent();

            // --- Responsive Layout Fix ---
            this.MinimumSize = new System.Drawing.Size(100, 100);
            this.panel1.Dock = DockStyle.Bottom;
            this.Grabber_ImageViewer.Dock = DockStyle.Fill;
            this.Grabber_ImageViewer.BringToFront();

            Grabber_ImageViewer.MouseWheel += new MouseEventHandler(Mouse_Wheel);
            Grabber_ImageViewer.MouseClick += new MouseEventHandler(Grabber_ImageViewer_MouseClick);

            // 첫 프레임부터 OpenGL 모드로 진입하게 설정
            Grabber_ImageViewer.UseOpenGLRendering = true;

            // 백그라운드 스레드가 ~60Hz로 OnPaint를 강제 유발한다.
            _renderDriver = new System.Threading.Timer(RenderDriverTick, null, 200, 16);

            if (!Grabber_ImageViewer.UseOpenGLRendering && ImageViewThread.IsBusy == false)
            {
                ImageViewThread.RunWorkerAsync();
            }

            ZoomRatioLabel.Text = string.Format("ZoomRatio:   {0}%", Grabber_ImageViewer.ZoomRatio * 100);

            // UI 라벨 폴링: 200ms 후 시작, 33ms 주기 (~30fps)
            _uiPollingDriver = new System.Threading.Timer(UiPollingTick, null, 200, 33);
        }

        private void UiPollingTick(object state)
        {
            if (_uiPollInFlight) return;
            if (!IsHandleCreated || IsDisposed) return;

            _uiPollInFlight = true;
            try
            {
                BeginInvoke(new Action(RefreshUILabels));
            }
            catch
            {
                _uiPollInFlight = false;
            }
        }

        private void RefreshUILabels()
        {
            try
            {
                if (Grabber_ImageViewer.UseOpenGLRendering)
                {
                    Grabber_ImageViewer.ShowDetectedLine  = m_bDetectedLine;
                    Grabber_ImageViewer.PeakPositionValue = m_dPeakPositionValue;
                }

                FPSText.Text   = string.Format("FPS: {0:0.00}", _latestFps);
                FrameText.Text = string.Format("FRAME: {0}", _latestFrame);

                if (_pendingPixelInfo)
                {
                    _pendingPixelInfo = false;
                    XPoSLabel.Text      = $"X: {_latestPixelX}";
                    YPoSLabel.Text      = $"Y: {_latestPixelY}";
                    IntensityLabel.Text = _latestIsRaw
                        ? $"Raw: {_latestIntensity} / {_latestRawMax}"
                        : $"Display: {_latestIntensity} / 255";
                }
            }
            finally
            {
                _uiPollInFlight = false;
            }
        }

        public CvMatView SetViewImage
        {
            get => ImageView;
            set => ImageView = value;
        }

        public bool LiveMode
        {
            get => _liveMode;
            set => _liveMode = value;
        }

        public CHLib.Controls.CHImageViewer ImageViewer => Grabber_ImageViewer;

        public bool CheckGuideLine
        {
            get => m_bGuideLine;
            set => m_bGuideLine = value;
        }

        public bool CheckDetectedLine
        {
            get => m_bDetectedLine;
            set => m_bDetectedLine = value;
        }

        public void UpdateFPS(float FPS)
        {
            _latestFps = FPS;
        }

        public void UpdateFrameAcquired(int nFrameAcquired)
        {
            _latestFrame = nFrameAcquired;
        }

        public void UpdatePeakPoS(int nPeakPoS)
        {
            m_dPeakPositionValue = nPeakPoS;
        }

        public void UpdatePeakLevel(int nPeakLevel)
        {
        }

        private void RenderDriverTick(object state)
        {
            if (_renderTickInFlight) return;
            var c = Grabber_ImageViewer;
            if (c == null || !c.IsHandleCreated || c.IsDisposed || !c.UseOpenGLRendering) return;

            _renderTickInFlight = true;
            try
            {
                c.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        if (c.IsHandleCreated && !c.IsDisposed && c.UseOpenGLRendering)
                        {
                            if (_liveMode)
                            {
                                PullLatestFrameForRender();
                            }
                            c.Invalidate();

                            // ROI/Profile 드래그 중에는 Update() 호출을 생략한다.
                            // Update()는 WM_PAINT를 UI 스레드에서 동기 실행(SwapBuffers 포함)하므로
                            // 드래그 중 60Hz로 반복되면 WM_TIMER(FPS 갱신 등)가 처리되지 못한다.
                            // 비드래그 상태에서는 기존대로 Update()를 유지하여 렌더링 타이밍을 보장한다.
                            if (!c.IsPointerDragging)
                                c.Update();
                        }
                    }
                    finally { _renderTickInFlight = false; }
                }));
            }
            catch
            {
                _renderTickInFlight = false;
            }
        }

        private void PullLatestFrameForRender()
        {
            if (!Grabber_ImageViewer.UseOpenGLRendering) return;
            try
            {
                VEOImagingSolutionSharp.CvMatView view;
                int newToken;
                if (VEOImagingSolutionSharp.OpenCVLibSharp.AcquireLatestFrame(out view, out newToken) == VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS)
                {
                    int oldToken = _displayToken;

                    // Publish-sequence gate (Finding #3 / F2): AcquireLatestFrame returns the same
                    // token already held in _displayToken when the producer (20Hz) has not published
                    // a new frame since the last render tick (60Hz fires ~3x per publish). Re-uploading
                    // in that case is a redundant GL texture upload, and releasing oldToken here would
                    // prematurely clear the reading-index guard on the slot still actively displayed.
                    // Mirrors the oldToken != token guard Push() already uses for the same reason.
                    // AcquireLatestFrame() still increments the slot's native reader count on this
                    // call even when it hands back the same token as before -- if we return here
                    // without releasing it, that increment is never balanced by a matching release,
                    // permanently leaking one reader on the slot (BeginSlotWriteCore then reports
                    // RESULT_LOAD_FAIL/BUSY for it forever).
                    if (newToken == oldToken)
                    {
                        VEOImagingSolutionSharp.OpenCVLibSharp.ReleaseFrame(newToken);
                        return;
                    }

                    _displayToken = newToken;
                    Grabber_ImageViewer.SetFrame(view.data, view.cols, view.rows, view.type, view.step);
                    if (oldToken >= 0)
                        VEOImagingSolutionSharp.OpenCVLibSharp.ReleaseFrame(oldToken);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[VIEWERForm] PullLatestFrameForRender error: " + ex.Message);
            }
        }

        public void ImageDrawByViewRate()
        {
            if (Grabber_ImageViewer.UseOpenGLRendering)
            {
                try
                {
                    if (this.InvokeRequired)
                    {
                        if (_pendingRender) return;
                        _pendingRender = true;
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                ImageDrawByViewRate();
                            }
                            finally
                            {
                                _pendingRender = false;
                            }
                        }));
                        return;
                    }

                    VEOImagingSolutionSharp.CvMatView view;
                    int newToken;
                    VEOImagingSolutionSharp.API_RESULT res = VEOImagingSolutionSharp.OpenCVLibSharp.AcquireLatestFrame(out view, out newToken);
                    if (res == VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS)
                    {
                        int oldToken = _displayToken;
                        _displayToken = newToken;
                        Grabber_ImageViewer.ShowDetectedLine = m_bDetectedLine;
                        Grabber_ImageViewer.PeakPositionValue = m_dPeakPositionValue;
                        Grabber_ImageViewer.SetFrame(view.data, view.cols, view.rows, view.type, view.step);
                        if (oldToken >= 0)
                            VEOImagingSolutionSharp.OpenCVLibSharp.ReleaseFrame(oldToken);
                    }
                }
                catch (Exception ex)
                {
                    _pendingRender = false;
                    Debug.WriteLine("Viewer OpenGL Draw Error: " + ex.Message);
                }
                return;
            }

            CvMatView frame;
            if (!TryPop(out frame)) return;
               
            try
            {
                CvMatView processedFrame = frame;
                ImageDraw(processedFrame);
            }
            catch (Exception ex) { Debug.WriteLine("Viewer Draw Error: " + ex.Message); }
        }

        private void ViewGuideLineImage(CvMatView SourceImage, out CvMatView ProcImage)
        {
            ProcImage = SourceImage;
        }

        public void ImageDraw(CvMatView SourceImage)
        {
            if (SourceImage.data == IntPtr.Zero) return;

            if (_pendingRender) return;

            ImageView = SourceImage;

            try
            {
                Bitmap safeBitmap = VwOpenCVLib.CvMatViewToBitmap(SourceImage);
                if (safeBitmap == null) return;

                if (this.IsDisposed || !this.IsHandleCreated)
                {
                    safeBitmap.Dispose();
                    return;
                }

                _pendingRender = true;
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        UpdateViewerControl(safeBitmap);
                        _pendingRender = false;
                    }));
                }
                else
                {
                    UpdateViewerControl(safeBitmap);
                    _pendingRender = false;
                }
            }
            catch (Exception ex)
            {
                _pendingRender = false;
                Debug.WriteLine("ImageDraw Bitmap Conversion Error: " + ex.Message);
            }
        }

        private void UpdateViewerControl(Bitmap newBitmap)
        {
            if (this.IsDisposed || Grabber_ImageViewer.IsDisposed)
            {
                return;
            }

            Image oldImage = Grabber_ImageViewer.Image;

            if (oldImage == newBitmap)
            {
                Grabber_ImageViewer.Invalidate();
            }
            else
            {
                Grabber_ImageViewer.Image = newBitmap;

                if (oldImage != null)
                {
                    oldImage.Dispose();
                }
            }
        }

        public CvMatView GetImageViewImage()
        {
            return ImageView;
        }

        public CvMatView GetViewportROI(out int offsetX, out int offsetY)
        {
            offsetX = 0;
            offsetY = 0;
            try
            {
                CvMatView snap = ImageView;
                if (snap.data == IntPtr.Zero) return default(CvMatView);

                double zoom = Grabber_ImageViewer.ZoomRatio;
                if (zoom <= 0) return snap;

                int x = (int)(Grabber_ImageViewer.ViewPortLocation.X / zoom);
                int y = (int)(Grabber_ImageViewer.ViewPortLocation.Y / zoom);
                int w = (int)(Grabber_ImageViewer.ClientSize.Width / zoom);
                int h = (int)(Grabber_ImageViewer.ClientSize.Height / zoom);

                x = Math.Max(0, Math.Min(x, snap.cols - 1));
                y = Math.Max(0, Math.Min(y, snap.rows - 1));
                w = Math.Max(1, Math.Min(w, snap.cols - x));
                h = Math.Max(1, Math.Min(h, snap.rows - y));

                offsetX = x;
                offsetY = y;

                int channels = 1 + (snap.type >> 3);
                int bpp = channels;
                IntPtr subPtr = new IntPtr(snap.data.ToInt64() + (long)y * snap.step + (long)x * bpp);

                CvMatView subView = new CvMatView
                {
                    rows = h,
                    cols = w,
                    type = snap.type,
                    step = snap.step,
                    data = subPtr
                };
                return subView;
            }
            catch
            {
                return default(CvMatView);
            }
        }

        public void Push(CvMatView incoming)
        {
            Push(incoming, -1);
        }

        public void Push(CvMatView incoming, int token)
        {
            if (incoming.data == IntPtr.Zero) return;
            if (Grabber_ImageViewer.UseOpenGLRendering)
            {
                if (!_liveMode)
                {
                    int oldToken = _displayToken;
                    _displayToken = token;
                    Grabber_ImageViewer.SetFrame(incoming.data, incoming.cols, incoming.rows, incoming.type, incoming.step);
                    if (oldToken >= 0 && oldToken != token)
                        VEOImagingSolutionSharp.OpenCVLibSharp.ReleaseFrame(oldToken);
                }
                return;
            }

            lock (_latestLock)
            {
                _latest = incoming;
                _hasLatest = true;
            }
        }

        public bool TryPop(out CvMatView frame)
        {
            lock (_latestLock)
            {
                frame = _latest;
                bool ret = _hasLatest;
                _hasLatest = false;
                return ret;
            }
        }

        private void ImageViewThread_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!ImageViewThread.CancellationPending)
            {
                if (Grabber_ImageViewer.UseOpenGLRendering)
                {
                    Thread.Sleep(33);
                    continue;
                }

                ImageDrawByViewRate();
                Thread.Sleep(1);
            }
            e.Cancel = true;
        }

        private void ImageViewThread_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) { }

        private void GRABBER_IMAGEVIEW_FormClosing(object sender, FormClosingEventArgs e)
        {
            _renderDriver?.Dispose();
            _renderDriver = null;
            _uiPollingDriver?.Dispose();
            _uiPollingDriver = null;
            if (ImageViewThread.IsBusy) ImageViewThread.CancelAsync();
            if (_displayToken >= 0)
            {
                VEOImagingSolutionSharp.OpenCVLibSharp.ReleaseFrame(_displayToken);
                _displayToken = -1;
            }
        }

        // [Repair round 2] Synchronously retires the currently-held display token so an
        // external teardown (e.g. ImagingLogicForm's ShallowRegister-mode teardown, which
        // calls m_ViewerForm1.Dispose() directly instead of .Close(), so
        // GRABBER_IMAGEVIEW_FormClosing above never fires) can guarantee the viewer is no
        // longer holding a token into a scan-slot buffer before that buffer is freed.
        // Mirrors the exact release logic in GRABBER_IMAGEVIEW_FormClosing above, but callable
        // on demand and synchronous (Invoke, not BeginInvoke) so the caller can rely on the
        // token being released before it proceeds to free memory.
        public void RetireDisplayToken()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action)RetireDisplayToken);
                return;
            }
            int oldToken = _displayToken;
            _displayToken = -1;
            if (oldToken >= 0)
            {
                // [Repair round 3, codex-critic finding] Don't swallow ReleaseFrame's result silently
                // -- best-effort cleanup still proceeds either way (this is NOT the "don't free on
                // failure" OWNED-SLOT REPOINT case in SimulatedGrabberPipeline.Dispose()), but a
                // failure here should at least be visible/debuggable instead of a bare `catch { }`.
                try
                {
                    VEOImagingSolutionSharp.API_RESULT res = VEOImagingSolutionSharp.OpenCVLibSharp.ReleaseFrame(oldToken);
                    if (res != VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS)
                        System.Diagnostics.Debug.WriteLine("[VIEWERForm] RetireDisplayToken: ReleaseFrame(" + oldToken + ") failed: " + res);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[VIEWERForm] RetireDisplayToken: ReleaseFrame(" + oldToken + ") threw: " + ex.Message);
                }
            }

            // [Repair round 3, codex-critic finding] Token release above only closes the token-based
            // UAF paths (e.g. GetPixelInfo). CHImageViewer.SetFrame (called from
            // PullLatestFrameForRender/ImageDrawByViewRate) separately retains the shallow pointer in
            // its own _srcData field, which releasing the token never touches -- CHImageViewer.OnPaint
            // can still upload/read that dangling _srcData after this method returns (e.g. after a
            // zoom/resize/viewport change triggers a repaint), even though live mode is now false and
            // no further acquisition will refresh it. ClearFrame() is CHImageViewer's own existing
            // public reset (resets _srcData/_srcCols/_srcRows/_srcType/_srcStep/_hasFrameSource/etc.
            // and invalidates) -- calling it here retires the control's retained frame pointer the same
            // way the block above retires the token. No null-guard on Grabber_ImageViewer: every other
            // method in this file (PullLatestFrameForRender, ImageDrawByViewRate, GetPixelInfo, etc.)
            // accesses it directly with no null check, so this mirrors the file's established pattern
            // rather than inventing a new one.
            Grabber_ImageViewer.ClearFrame();
        }

        private void GetPixelInfo(MouseEventArgs e)
        {
            try
            {
                double zoom = Grabber_ImageViewer.ZoomRatio;
                if (zoom <= 0) return;

                _X = (Grabber_ImageViewer.ViewPortLocation.X / zoom) + (e.X / zoom);
                _Y = (Grabber_ImageViewer.ViewPortLocation.Y / zoom) + (e.Y / zoom);

                int imgX = (int)Math.Floor(_X);
                int imgY = (int)Math.Floor(_Y);

                if (imgX == _lastX && imgY == _lastY) return;
                if (Grabber_ImageViewer.UseOpenGLRendering)
                {
                    int token = _displayToken;
                    if (token >= 0)
                    {
                        VEOImagingSolutionSharp.RawPixelValue rawVal = new VEOImagingSolutionSharp.RawPixelValue();
                        if (VEOImagingSolutionSharp.OpenCVLibSharp.GetRawPixelValue(token, imgX, imgY, ref rawVal) == VEOImagingSolutionSharp.API_RESULT.RESULT_SUCCESS)
                        {
                            _lastX = imgX;
                            _lastY = imgY;
                            _latestPixelX = imgX;
                            _latestPixelY = imgY;
                            _latestIntensity = rawVal.channels == 1 ? (int)rawVal.value0 : (int)rawVal.value2;
                            _latestRawMax = rawVal.maxValue > 0 ? (int)rawVal.maxValue : 255;
                            _latestIsRaw = true;
                            _pendingPixelInfo = true;
                        }
                    }
                    return;
                }

                CvMatView snap = ImageView;
                if (snap.data == IntPtr.Zero) return;
                if (imgX < 0 || imgX >= snap.cols || imgY < 0 || imgY >= snap.rows) return;

                _lastX = imgX;
                _lastY = imgY;

                byte intensity = 0;
                int channels = 1 + (snap.type >> 3);
                unsafe
                {
                    byte* pSrc = (byte*)snap.data.ToPointer();
                    byte* pRow = pSrc + (long)imgY * snap.step;
                    if (channels == 1)
                    {
                        intensity = pRow[imgX];
                    }
                    else
                    {
                        intensity = pRow[imgX * channels];
                    }
                }

                _latestPixelX    = imgX;
                _latestPixelY    = imgY;
                _latestIntensity = intensity;
                _latestRawMax    = 255;
                _latestIsRaw     = false;
                _pendingPixelInfo = true;
            }
            catch { }
        }

        private void Mouse_Wheel(object sender, MouseEventArgs e)
        {
            if (ImageView.data != IntPtr.Zero)
            {
                ZoomRatioLabel.Text = string.Format("ZoomRatio:   {0}%", (int)(Grabber_ImageViewer.ZoomRatio * 100));
            }
        }

        private void Grabber_ImageViewer_MouseMove(object sender, MouseEventArgs e)
        {
            GetPixelInfo(e);
        }

        private void Grabber_ImageViewer_MouseClick(object sender, MouseEventArgs e)
        {
            GetPixelInfo(e);
        }
    }
}
