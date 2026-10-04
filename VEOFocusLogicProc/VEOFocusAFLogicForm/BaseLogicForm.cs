using VEOImagingSolutionSharp;
using System;
using System.Drawing;
using System.Windows.Forms;
using VIEWORKS_AFTS.Interfaces;

namespace VIEWORKS_AFTS
{
    /// <summary>
    /// 하이브리드(C# OpenCV 제거 + C++ OpenCV 4.12) 환경을 위한 공통 기반 폼 클래스
    /// </summary>
    public class BaseLogicForm : Form, ILogicModule
    {
        // --- 하이 레벨 연산 및 DLL 브릿지 ---
        protected VwOpenCVLib m_VwOpenCVUtil = new VwOpenCVLib();

        // --- 핵심 메모리 인덱스 (예약 index 0번 기본 사용) ---
        protected int m_nCurrentIndex = 0;

        // DeinitializeModule()이 두 번(예: 종료 버튼 경로 + FormClosed 경로) 호출되어도
        // m_VwOpenCVUtil.DeInitializeLib()가 중복 실행되지 않도록 막는 가드.
        protected bool m_bDeinitialized = false;

        public BaseLogicForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
        }

        #region 1. 라이브러리 생명주기 관리

        public virtual bool InitializeModule()
        {
            try
            {
                // C++ 전용 라이브러리(OpenCV 4.12 엔진) 초기화
                m_VwOpenCVUtil.InitializeLib();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"라이브러리 초기화 실패: {ex.Message}");
                return false;
            }
        }

        public virtual void DeinitializeModule()
        {
            if (m_bDeinitialized) return;
            m_bDeinitialized = true;

            // C++ DLL 자원 및 콜백 해제
            m_VwOpenCVUtil.DeInitializeLib();
        }

        #endregion

        #region 2. 데이터 주입 및 변환 (Input: C# -> C++ cvMat)

        public virtual void LoadImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            // 이미 초기화된 ring의 예약 index 0에 이미지 로드
            CvMatView view;
            var res = OpenCVLibSharp.ImageLoad(filePath, m_nCurrentIndex, out view);
            if (res != API_RESULT.RESULT_SUCCESS) return;

            // UI 갱신
            SyncResultToUI();
        }

        public virtual void SetInputImage(IntPtr pBuffer, int width, int height, int stride, PixelFormat format)
        {
            if (pBuffer == IntPtr.Zero) return;

            CvMatView view = new CvMatView
            {
                rows = height,
                cols = width,
                type = PixelFormatToCvType(format),
                step = stride,
                data = pBuffer
            };

            // C++ DLL 측 버퍼 포인터 동기화 (예약 index 0번에 대입)
            OpenCVLibSharp.SetImageBufferPtr(ref view, m_nCurrentIndex);

            // UI 갱신
            SyncResultToUI();
        }

        #endregion

        #region 3. 결과 데이터 처리 및 출력 (Output: C++ -> C# UI)

        public virtual void SyncResultToUI()
        {
            // 자식 클래스에서 화면 갱신 수행
            OnImageLoaded();
        }

        public virtual void ClearData()
        {
            // 링버퍼 데이터를 0으로 초기화
            CvMatView view;
            if (OpenCVLibSharp.GetImageBufferPtr(m_nCurrentIndex, out view) == API_RESULT.RESULT_SUCCESS && view.data != IntPtr.Zero)
            {
                unsafe
                {
                    byte* ptr = (byte*)view.data.ToPointer();
                    long totalBytes = view.step * view.rows;
                    for (long i = 0; i < totalBytes; i++)
                    {
                        ptr[i] = 0;
                    }
                }
            }
        }

        #endregion

        // --- 자식 클래스용 훅 ---
        protected virtual void OnImageLoaded() { }

        /// <summary>
        /// 최종 가공된 CvMatView 데이터를 C# PictureBox/Viewer에 OpenGL 직접 출력
        /// </summary>
        protected void ImageDraw_ImageViewer(CHLib.Controls.CHImageViewer viewer, CvMatView view)
        {
            if (view.data == IntPtr.Zero || view.cols <= 0 || view.rows <= 0) return;

            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(() => viewer.SetFrame(view.data, view.cols, view.rows, view.type, view.step, -1)));
            }
            else
            {
                viewer.SetFrame(view.data, view.cols, view.rows, view.type, view.step, -1);
            }
        }

        protected static int PixelFormatToCvType(PixelFormat fmt)
        {
            switch (fmt)
            {
                case PixelFormat.Mono8: return 0;   // CV_8UC1
                case PixelFormat.RGB24: return 16;  // CV_8UC3
                case PixelFormat.RGB32: return 24;  // CV_8UC4
                case PixelFormat.Mono10:
                case PixelFormat.Mono12: return 2;  // CV_16UC1
                default: return 0;
            }
        }
    }
}
