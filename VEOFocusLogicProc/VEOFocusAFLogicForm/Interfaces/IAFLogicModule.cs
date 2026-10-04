using System;

namespace VIEWORKS_AFTS.Interfaces
{
    /// <summary>
    /// AF 로직 테스트 모듈을 위한 특화 인터페이스
    /// </summary>
    public interface IAFLogicModule : ILogicModule
    {
        /// <summary>
        /// AF 알고리즘 테스트 실행 (단일 실행)
        /// </summary>
        void RunAFTest();

        /// <summary>
        /// AF 시뮬레이션 시작/중지
        /// </summary>
        /// <param name="start">시작 여부</param>
        void ToggleSimulation(bool start);
    }
}
