using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VIEWORKS_AFTS
{
    /// <summary>
    /// RAW 프레임 폴더 임포트 시 해상도(Width x Height)를 결정하는 소형 모달 다이얼로그.
    ///
    /// RAW 덤프는 헤더가 없어 파일 크기만으로는 해상도를 특정할 수 없다(예: 18,000 byte 는
    /// 360x50 / 900x20 / 1200x15 ... 다수 조합이 가능). 따라서 사용자가 W/H 를 지정하되,
    /// 첫 파일의 실제 크기와 W*H 가 일치할 때만 OK 를 허용하고, 크기의 약수로 만들 수 있는
    /// 후보를 프리셋으로 제시해 선택을 돕는다. 8bit Mono 전제이므로 픽셀당 1 byte 로 계산한다.
    /// </summary>
    public partial class RawImportOptionsForm : Form
    {
        // 직전 입력값 기억: 같은 세션에서 여러 폴더를 연속으로 열 때 재입력을 줄인다.
        private static int s_lastWidth  = 360;
        private static int s_lastHeight = 50;

        private readonly long m_FileSizeBytes;
        private bool m_SuppressPresetEvent;

        /// <summary>확정된 프레임 폭(px).</summary>
        public int FrameWidth  { get; private set; }

        /// <summary>확정된 프레임 높이(px).</summary>
        public int FrameHeight { get; private set; }

        /// <param name="fileSizeBytes">폴더 첫 RAW 파일의 실제 크기(byte).</param>
        /// <param name="detectedFrameCount">연속 번호 probing 으로 세어 둔 프레임 수.</param>
        /// <param name="filePrefix">파일명에서 추출한 접두어(표시용).</param>
        public RawImportOptionsForm(long fileSizeBytes, int detectedFrameCount, string filePrefix)
        {
            InitializeComponent();

            m_FileSizeBytes = fileSizeBytes;

            labelFileInfo.Text = string.Format("File size : {0:N0} byte    Frames : {1:N0}", fileSizeBytes, detectedFrameCount);
            labelPrefixInfo.Text = "Pattern : " + filePrefix + "N.raw";

            BuildPresetCandidates();

            // 직전값이 이번 파일 크기와도 맞으면 그대로, 아니면 첫 후보로 시작한다.
            if ((long)s_lastWidth * s_lastHeight == fileSizeBytes)
            {
                TextBox_Width.Text  = s_lastWidth.ToString();
                TextBox_Height.Text = s_lastHeight.ToString();
            }
            else if (comboPreset.Items.Count > 0)
            {
                comboPreset.SelectedIndex = 0;
            }

            Validate_Input();
        }

        /// <summary>
        /// 파일 크기의 약수 쌍 중 라인 레이저 프로파일로 그럴듯한 것(가로 >= 세로, 세로 <= 256)만
        /// 후보로 남긴다. 세로가 큰 순으로 정렬해 흔한 형태가 위로 오게 한다.
        /// </summary>
        private void BuildPresetCandidates()
        {
            var candidates = new List<Size>();
            for (int h = 1; h <= 256; h++)
            {
                if (m_FileSizeBytes % h != 0) continue;
                long w = m_FileSizeBytes / h;
                if (w < h) continue;
                if (w > int.MaxValue) continue;
                candidates.Add(new Size((int)w, h));
            }
            candidates.Reverse(); // 세로가 큰 쪽부터

            m_SuppressPresetEvent = true;
            foreach (var c in candidates)
                comboPreset.Items.Add(string.Format("{0} x {1}", c.Width, c.Height));
            m_SuppressPresetEvent = false;
        }

        private void comboPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_SuppressPresetEvent) return;
            string sel = comboPreset.SelectedItem as string;
            if (string.IsNullOrEmpty(sel)) return;

            string[] parts = sel.Split('x');
            if (parts.Length != 2) return;

            TextBox_Width.Text  = parts[0].Trim();
            TextBox_Height.Text = parts[1].Trim();
        }

        private void Input_TextChanged(object sender, EventArgs e)
        {
            Validate_Input();
        }

        /// <summary>W*H 가 파일 크기와 정확히 일치할 때만 OK 를 허용한다.</summary>
        private void Validate_Input()
        {
            // && 단축 평가로 두 번째 TryParse 가 실행되지 않을 수 있어 명시적으로 초기화한다.
            int w = 0, h = 0;
            bool parsed = int.TryParse(TextBox_Width.Text, out w)
                       && int.TryParse(TextBox_Height.Text, out h)
                       && w > 0 && h > 0;

            if (!parsed)
            {
                labelValidation.ForeColor = Color.Firebrick;
                labelValidation.Text = "Width / Height 에 1 이상의 정수를 입력하세요.";
                btnOK.Enabled = false;
                return;
            }

            long computed = (long)w * h;
            if (computed != m_FileSizeBytes)
            {
                labelValidation.ForeColor = Color.Firebrick;
                labelValidation.Text = string.Format("{0} x {1} = {2:N0} byte  ≠  파일 크기 {3:N0} byte",
                                                     w, h, computed, m_FileSizeBytes);
                btnOK.Enabled = false;
                return;
            }

            labelValidation.ForeColor = Color.ForestGreen;
            labelValidation.Text = string.Format("{0} x {1} x 1byte = {2:N0} byte  OK", w, h, computed);
            btnOK.Enabled = true;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            int w, h;
            if (!int.TryParse(TextBox_Width.Text, out w) || !int.TryParse(TextBox_Height.Text, out h))
                return;

            FrameWidth  = w;
            FrameHeight = h;
            s_lastWidth  = w;
            s_lastHeight = h;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
