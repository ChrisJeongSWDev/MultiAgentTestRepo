using System;
using System.ComponentModel;
using System.Windows.Forms;
using VIEWORKS_AFTS.Interfaces;

namespace VIEWORKS_AFTS
{
    public partial class Vieworks_AFTS : Form
    {
        // 현재 활성화된 로직 모듈 (인터페이스를 통해 제어)
        private ILogicModule m_ActiveModule;

        // 개별 모듈 폼
        private AFLogicTestForm m_AFLogicTestForm;

        private System.Drawing.Point FormCursor = new System.Drawing.Point();
        private bool m_bMaximized = false;

        public Vieworks_AFTS()
        {
            InitializeComponent();
            InitializeSubModules();

            this.Text = string.Empty;
            this.ControlBox = false;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Wired here (not in the Designer) so module teardown runs in FormClosed, after
            // the framework's own Application.OpenForms close-enumeration has finished.
            this.FormClosed += Vieworks_AFTS_FormClosed;
        }

        private void InitializeSubModules()
        {
            // AF 모듈 생성 및 초기화
            m_AFLogicTestForm = new AFLogicTestForm();
            m_AFLogicTestForm.InitializeModule();

            // 버튼 이벤트 연결
            this.btnAFLogicTest.Click += btnAFLogicTest_Click;

            // 기본 모듈로 AFLogicTestForm 표시
            ShowModule(m_AFLogicTestForm);
        }

        private void ShowModule(ILogicModule module)
        {
            if (module == null || !(module is Form moduleForm)) return;

            // 기존 모듈 정리
            this.PanelMainForm.Controls.Clear();
            
            // 새 모듈 표시
            m_ActiveModule = module;
            moduleForm.TopLevel = false;
            moduleForm.Dock = DockStyle.Fill;
            this.PanelMainForm.Controls.Add(moduleForm);
            moduleForm.Show();
        }

        private void btnAFLogicTest_Click(object sender, EventArgs e)
        {
            ShowModule(m_AFLogicTestForm);
        }

        #region Form UI Control (Window Drag, Maximize, Exit etc.)

        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 1;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && m.Result == (IntPtr)HTCLIENT) m.Result = (IntPtr)HTCAPTION;
        }

        private void PanelMainBar_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
                this.Location = new System.Drawing.Point(this.Left - (FormCursor.X - e.X), this.Top - (FormCursor.Y - e.Y));
        }

        private void PanelMainBar_MouseDown(object sender, MouseEventArgs e) => FormCursor = new System.Drawing.Point(e.X, e.Y);

        private void ProgramMaximizeButton_Click(object sender, EventArgs e)
        {
            if (!m_bMaximized) { this.WindowState = FormWindowState.Maximized; m_bMaximized = true; }
            else { this.WindowState = FormWindowState.Normal; m_bMaximized = false; }
        }

        private void ProgramExitButton_Click(object sender, EventArgs e)
        {
            // Module teardown is intentionally NOT performed here anymore -- it now runs in
            // Vieworks_AFTS_FormClosed, after Close()'s own Application.OpenForms enumeration
            // has finished. Calling DeinitializeModule() here (pre-emptively, before Close())
            // used to mutate that same OpenForms collection while Application.Exit()'s
            // shutdown loop was iterating it, raising InvalidOperationException.
            this.Close();
        }
        private void ProgramMinimizeButton_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;

        private void Vieworks_AFTS_Load(object sender, EventArgs e) => this.FormBorderStyle = FormBorderStyle.None;
        private void Vieworks_AFTS_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Module teardown deliberately NOT performed here: FormClosing fires while
            // Application.OpenForms is still being enumerated by the framework's close
            // sequence, and DeinitializeModule() (viewer Close()/Dispose(), native lib
            // teardown) mutates that same collection. Moved to Vieworks_AFTS_FormClosed,
            // which runs once that enumeration has completed.
        }

        private void Vieworks_AFTS_FormClosed(object sender, FormClosedEventArgs e)
        {
            m_AFLogicTestForm?.DeinitializeModule();
        }

        private void CameraSimulator_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) { /* 시뮬레이션 종료 핸들러 */ }
        private void ImageProcessingThread_DoWork(object sender, DoWorkEventArgs e) { /* 이미지 처리 스레드 핸들러 */ }

        #endregion
    }
}
