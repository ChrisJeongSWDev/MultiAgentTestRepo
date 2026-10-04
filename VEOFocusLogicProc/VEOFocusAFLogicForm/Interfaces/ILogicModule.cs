using System;
using VEOImagingSolutionSharp;

namespace VIEWORKS_AFTS.Interfaces
{
    /// <summary>
    /// 하이브리드(C# + C++) 이미지 프로세싱을 위한 공통 로직 인터페이스
    /// </summary>
    public interface ILogicModule
    {
        // --- 1. 라이브러리 생명주기 관리 ---
        /// <summary>
        /// OpenCV 4.12 DLL 및 관련 리소스 초기화
        /// </summary>
        bool InitializeModule();

        /// <summary>
        /// 라이브러리 해제 및 메모리 정리
        /// </summary>
        void DeinitializeModule();


        // --- 2. 데이터 주입 및 C++ 호환 변환 (Input) ---
        /// <summary>
        /// 파일 경로로부터 이미지를 로드하여 C++ 연산 가능 형태로 변환
        /// </summary>
        void LoadImage(string filePath);

        /// <summary>
        /// 카메라/시뮬레이터의 포인터를 받아 C++ OpenCV(cvMat) 호환 형태로 연동
        /// </summary>
        void SetInputImage(IntPtr pBuffer, int width, int height, int stride, PixelFormat format);


        // --- 3. 결과 데이터 처리 및 출력 (Output) ---
        /// <summary>
        /// C++ 연산 결과를 C# UI(Bitmap 등)에서 이해할 수 있는 형태로 변환하여 출력/갱신
        /// </summary>
        void SyncResultToUI();

        /// <summary>
        /// 데이터 및 UI 요소 초기화
        /// </summary>
        void ClearData();
    }
}
