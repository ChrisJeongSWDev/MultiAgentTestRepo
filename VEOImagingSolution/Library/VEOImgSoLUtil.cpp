//#include "pch.h"
#include "VEOImgSoLUtil.h"

#include <condition_variable>
#include <fstream>
#include <string>

namespace
{
    static std::atomic<int>        s_activeBatchCount{ 0 };
    static std::condition_variable s_sourceDrainCv;
    static std::mutex              s_sourceDrainMtx;
}


#include <limits>
#include <cmath>

OpenCVUtil::OpenCVUtil(EventCallback* Pointer, void* pUserPointer)
{
    m_EventLog = NULL;
    if (m_EventLog == NULL)
    {
        m_EventLog = new VwSolutionSDKEventLog(Pointer, pUserPointer);
    }

    //    RAM (m_OpenCVLibRingBuffer)  RamFrameSource.
    m_ramFrameSource = new RamFrameSource(m_OpenCVLibRingBuffer);
    m_frameSource = m_ramFrameSource;
    m_mmfFrameSource = nullptr;
    m_bUsingDiskFrameSource = false;
}

OpenCVUtil::~OpenCVUtil(void)
{
    if (m_EventLog != NULL)
    {
        delete m_EventLog;
        m_EventLog = NULL;
    }

    if (m_mmfFrameSource != nullptr)
    {
        delete m_mmfFrameSource;
        m_mmfFrameSource = nullptr;
    }
    if (m_ramFrameSource != nullptr)
    {
        delete m_ramFrameSource;
        m_ramFrameSource = nullptr;
    }
    m_frameSource = nullptr;
}

int OpenCVUtil::PixelFormatToCvType(PixelFormat pixelFormat)
{
    switch (pixelFormat)
    {
        //  Mono 
    case PixelFormat::Mono8:
        return CV_8UC1;

    case PixelFormat::Mono10:
    case PixelFormat::Mono12:
        return CV_16UC1;

        //  RGB 
    case PixelFormat::RGB24:
        return CV_8UC3;

    case PixelFormat::RGB32:
        return CV_8UC4;

        //  Bayer 8bit 
    case PixelFormat::BayerRG8:
    case PixelFormat::BayerBG8:
    case PixelFormat::BayerGB8:
    case PixelFormat::BayerGR8:
        return CV_8UC1;

        //  Bayer 10/12bit (16bit ) 
    case PixelFormat::BayerRG10:
    case PixelFormat::BayerRG12:
    case PixelFormat::BayerBG10:
    case PixelFormat::BayerBG12:
    case PixelFormat::BayerGB10:
    case PixelFormat::BayerGB12:
    case PixelFormat::BayerGR10:
    case PixelFormat::BayerGR12:
        return CV_16UC1;

    default:
        return -1;
    }
}

struct CachedTemplate {
    cv::Mat original;    // 1/1
    cv::Mat pyramid4;    // 1/4
    cv::Mat pyramid8;    // 1/8
};

static std::mutex s_templateMtx;
static std::unordered_map<int, CachedTemplate> s_templateCache;

void OpenCVUtil::InitializeLibHandle()
{
    //  SetImageBuffer(count[,w,h,fmt])    (  ).
    m_EventLog->Addlog(string_format("[OPENCV_C++ LIB]LIB Handle has been initialized!- [InitializeLibHandle]\n "), SDK_LOGEVENT);
}

void OpenCVUtil::DeinitializeLibHandle()
{
    {
        std::lock_guard<std::mutex> lk(s_templateMtx);
        s_templateCache.clear();
    }
    m_EventLog->Addlog(string_format("[OPENCV_C++ LIB]LIB Handle has been deinitialized!- [InitializeLibHandle]\n "), SDK_LOGEVENT);
}

Mat OpenCVUtil::ImageLoad(const std::string& path)
{
    cv::Mat img = cv::imread(path, cv::IMREAD_ANYCOLOR | cv::IMREAD_ANYDEPTH);
    return img;
}

Mat OpenCVUtil::SetImagePointer(int width, int height, void* pBuffer, PixelFormat pixelFormat)
{
    if (!pBuffer || width <= 0 || height <= 0)
        return cv::Mat();

    int cvType = PixelFormatToCvType(pixelFormat);
    if (cvType < 0)
        return cv::Mat();

    // pBuffer   ""  (stride width*elemSize )
    return cv::Mat(height, width, cvType, pBuffer);
}

Mat OpenCVUtil::SetImagePointer(int width, int height, void* pBuffer, PixelFormat pixelFormat, size_t strideBytes)
{
    if (!pBuffer || width <= 0 || height <= 0)
        return Mat();

    int cvType = PixelFormatToCvType(pixelFormat);
    if (cvType < 0)
        return Mat();

    // strideBytes  (  )
    const size_t minStride = (size_t)width * (size_t)CV_ELEM_SIZE(cvType);
    if (strideBytes < minStride)
        return Mat(); //  stride

    return Mat(height, width, cvType, pBuffer, strideBytes);
}



API_RESULT OpenCVUtil::Threshold(const CvMatView& srcView,const CvMatView& dstView,double thresh, double maxVal, int thresholdType,int* outUsedThresh)
{
    try
    {
        if (outUsedThresh) *outUsedThresh = -1;
        if (!srcView.data || srcView.rows <= 0 || srcView.cols <= 0) return RESULT_ERROR;
        if (!dstView.data || dstView.rows != srcView.rows || dstView.cols != srcView.cols) return RESULT_ERROR;

        cv::Mat srcMat(srcView.rows, srcView.cols, srcView.type, srcView.data, (size_t)srcView.step);
        cv::Mat dstMat(dstView.rows, dstView.cols, dstView.type, dstView.data, (size_t)dstView.step);

        cv::Mat gray;
        if (srcMat.channels() == 1) gray = srcMat;
        else cv::cvtColor(srcMat, gray, cv::COLOR_BGR2GRAY);

        // gray dstMat /   ( dst CV_8UC1 )
        double used = cv::threshold(gray, dstMat, thresh, maxVal, thresholdType);
        if (outUsedThresh) *outUsedThresh = (int)std::lround(used);

        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

API_RESULT OpenCVUtil::FindBoundaryYLatest(const CvMatView& src, int* outBoundaryY)
{
    try
    {
        if (!outBoundaryY) return RESULT_NULL_HANDLE;
        *outBoundaryY = -1;

        if (!src.data || src.rows <= 0 || src.cols <= 0)
            return RESULT_ERROR;

        cv::Mat m(src.rows, src.cols, src.type, src.data, (size_t)src.step);

        *outBoundaryY = FindBoundaryYCore(m); //  static 
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

int OpenCVUtil::FindBoundaryYCore(const cv::Mat& srcBgrOrGray)
{
    try
    {
        if (srcBgrOrGray.empty() || srcBgrOrGray.rows < 100 || srcBgrOrGray.cols < 100 || srcBgrOrGray.depth() != CV_8U)
            return -1;

        cv::Mat gray;
        if (srcBgrOrGray.channels() == 1)
            gray = srcBgrOrGray;
        else if (srcBgrOrGray.channels() == 3)
            cv::cvtColor(srcBgrOrGray, gray, cv::COLOR_BGR2GRAY);
        else if (srcBgrOrGray.channels() == 4)
            cv::cvtColor(srcBgrOrGray, gray, cv::COLOR_BGRA2GRAY);
        else
            return -1;

        // ?체 ?이 ??단 ?역(최? 15000? ?색
        int maxSearchH = (std::min)(15000, gray.rows - 100);
        if (maxSearchH <= 100)
            maxSearchH = gray.rows;

        // 중앙 ±1200 ?? ROI 지??(?이???형 ??최상???역)
        int sampleStartX = (std::max)(0, gray.cols / 2 - 1200);
        int sampleEndX = (std::min)(gray.cols, gray.cols / 2 + 1200);
        int sampleWidth = sampleEndX - sampleStartX;
        if (sampleWidth <= 0) return -1;

        cv::Rect roiRect(sampleStartX, 0, sampleWidth, maxSearchH);
        cv::Mat centerRoi = gray(roiRect);

        // 1. ?별 ?균 밝기 ?로?일 계산 (REDUCE_AVG)
        cv::Mat rowProfile;
        cv::reduce(centerRoi, rowProfile, 1, cv::REDUCE_AVG, CV_32F);

        // 2. ?단 50줄의 배경 ?이??벨 측정
        int bgLines = (std::min)(50, maxSearchH);
        float bgSum = 0.0f;
        for (int y = 0; y < bgLines; y++)
        {
            bgSum += rowProfile.at<float>(y, 0);
        }
        float bgLevel = bgSum / bgLines;
        float threshold = (std::max)(20.0f, bgLevel + 12.0f);

        // 3. First Transition: ?계값을 8?인 ?상 ?속 초과?는 ???검?
        int consecutiveCount = 0;
        int candidateY = -1;

        for (int y = 0; y < maxSearchH; y++)
        {
            float val = rowProfile.at<float>(y, 0);
            if (val >= threshold)
            {
                if (consecutiveCount == 0) candidateY = y;
                consecutiveCount++;
                if (consecutiveCount >= 8)
                {
                    return candidateY; // Exact dynamic first transition
                }
            }
            else
            {
                consecutiveCount = 0;
                candidateY = -1;
            }
        }

        return -1; // No reliable boundary detected
    }
    catch (...)
    {
        return -1;
    }
}


API_RESULT OpenCVUtil::AFAlgorithmInitialize(int nCOEA,int nCOEB,int PeakThreshold, int nHaarFilterSize)
{
    //user setting parameter, example
    u12i_m_nHaarFilterSize = (nHaarFilterSize << SUB_PIXEL_SCALE_BIT);		// nHaarFilterSize * 16
    u12i_m_nMaskSize = (nHaarFilterSize << (SUB_PIXEL_SCALE_BIT + 1));		// nHaarFilterSize * 32
    u8i_m_nNumLine = (1 << LINE_NUM_BIT);
    u8i_m_PeakThreshold = PeakThreshold;
    u8i_m_nSelLineIdx = 0;
    m_bValidLaser = false;

    s17i_m_nCoeA = nCOEA;
    s16i_m_nCoeB = nCOEB;

    ////////////////////////////////////////////////

    for (int k = 0; k < MAX_LINE_NUM; k++)
    {
        s16i_m_nMaskX1[k] = 0;
        s16i_m_nMaskX2[k] = 0;
    }
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::VerticalSum(const unsigned char* u8i_pImageBuffer, unsigned short u12i_nWidth, unsigned short u12i_nHeight, unsigned char* u8i_pProfile)
{
    unsigned char nPeakVal = 0;
    for (unsigned short x = 0; x < u12i_nWidth; x++) // for ..  ..??    ..?
    {
        unsigned int u24i_nIdx = x;
        unsigned int u20i_nSum = 0;
        for (int y = 0; y < u12i_nHeight; y++) 
        {
            u20i_nSum += u8i_pImageBuffer[u24i_nIdx];
            u24i_nIdx += u12i_nWidth;
        }
        u8i_pProfile[x] = (unsigned char)(u20i_nSum / u12i_nHeight);

        //peak value 
        if (u8i_pProfile[x] > nPeakVal)
        {
            nPeakVal = u8i_pProfile[x];
        }
    }

    if (nPeakVal < u8i_m_PeakThreshold) 
    {
        return RESULT_PEAK_LOW_THRESHOLD;
    }
    else 
    {
        return RESULT_SUCCESS;
    }
}

API_RESULT OpenCVUtil::CalculatePeakLevel(const CvMatView& src, int* outPeakLevel)
{
    try
    {
        if (!outPeakLevel) return RESULT_NULL_HANDLE;
        *outPeakLevel = 0;

        if (!src.data || src.rows <= 0 || src.cols <= 0)
            return RESULT_ERROR;

        int width = src.cols;
        int height = src.rows;
        int stride = (int)src.step;
        int maxMean = 0;

        // Mono   (CV_8UC1 = 8, CV_16UC1 = 10/12/16)
        if (src.type == CV_16UC1)
        {
            unsigned short* pBase16 = (unsigned short*)src.data;
            int stride16 = stride / 2;
            for (int x = 0; x < width; x++)
            {
                unsigned int sum = 0;
                for (int y = 0; y < height; y++)
                {
                    sum += pBase16[y * stride16 + x];
                }
                int mean = (int)(sum / height);
                if (mean > maxMean) maxMean = mean;
            }
        }
        else if (src.type == CV_8UC1)
        {
            unsigned char* pBase8 = (unsigned char*)src.data;
            for (int x = 0; x < width; x++)
            {
                unsigned int sum = 0;
                for (int y = 0; y < height; y++)
                {
                    sum += pBase8[y * stride + x];
                }
                int mean = (int)(sum / height);
                if (mean > maxMean) maxMean = mean;
            }
        }
        else
        {
            //     
            return RESULT_ERROR;
        }

        *outPeakLevel = maxMean;
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

API_RESULT OpenCVUtil::UpScaleData(const unsigned char* u8i_pProfile, unsigned short u12i_nProfileSize, unsigned char* u8i_pUpScaledProfile)
{
    unsigned char u8i_nSubPixelScale = (1 << SUB_PIXEL_SCALE_BIT); // SubPixel  . 16 Level SubPixel Sampling. 
    unsigned short u15i_nUpScaledProfileSize = u12i_nProfileSize << SUB_PIXEL_SCALE_BIT; // Raw Data -> SUB PIXEL SCALE BIT  . 

    for (unsigned short x = 0; x < u15i_nUpScaledProfileSize; x++) // SUB  PIXEL 
    {

        unsigned char u4i_nW2 = x % u8i_nSubPixelScale;
        unsigned char u4i_nW1 = u8i_nSubPixelScale - u4i_nW2;

        unsigned short u12i_x1 = x >> SUB_PIXEL_SCALE_BIT;
        unsigned short u12i_x2 = u12i_x1 + 1;

        if (u12i_x2 >= u12i_nProfileSize) // Buffer Overrun .
            u12i_x2 = u12i_nProfileSize - 1;

        unsigned short u13i_Interpolated = u4i_nW1 * (unsigned short)u8i_pProfile[u12i_x1] + u4i_nW2 * (unsigned short)u8i_pProfile[u12i_x2];

        u8i_pUpScaledProfile[x] = (u13i_Interpolated >> SUB_PIXEL_SCALE_BIT);

    }
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::CalcHaarScore(const unsigned char* u8i_pProfile, unsigned short u15i_nProfileSize, unsigned short u12i_nHaarFilterSize, short* s16i_pHarrScore)
{
    //pixel level search //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // (20220804)
    short s13i_nIdxA, s13i_nIdxB, s13i_nIdxC, s13i_nIdxD, s13i_nIdxE;

    // (20220804)
    //u12i_nHaarFilterSize : 2 ~ 64 * 16 = 1024
    //u12i_nHaarFilterSize_x2 : 2048  < unsigned 14bit
    //u12i_nHaarFilterSize_x3 : 3072  < unsigned 14bit
    unsigned short u12i_nHaarFilterSize_x2 = u12i_nHaarFilterSize * 2;
    unsigned short u12i_nHaarFilterSize_x3 = u12i_nHaarFilterSize * 3;
    short s12i_nDeltaSum;

    // (20220804)
    //s20i_nScore, : -524288 ~ 524287 = 256 * 1024 * 2 (max of u12i_nHaarFilterSize = 1024) 
    int s20i_nScore;

    short s12i_nEnd = u15i_nProfileSize - u12i_nHaarFilterSize_x3;

    s20i_nScore = 0;

    s13i_nIdxA = -u12i_nHaarFilterSize;
    s13i_nIdxB = 0;
    s13i_nIdxC = u12i_nHaarFilterSize;
    s13i_nIdxD = u12i_nHaarFilterSize_x2;
    s13i_nIdxE = u12i_nHaarFilterSize_x3;


    memset(s16i_pHarrScore, 0, sizeof(short) * u15i_nProfileSize);

    for (unsigned short p = 0; p < s12i_nEnd; p++) {

        //initialize s16i_nScore
        if (p < u12i_nHaarFilterSize) 
        {

            s20i_nScore -= u8i_pProfile[s13i_nIdxB];
            s20i_nScore += u8i_pProfile[s13i_nIdxC];
            s20i_nScore += u8i_pProfile[s13i_nIdxD];
            s20i_nScore -= u8i_pProfile[s13i_nIdxE];

            //update s16i_nScore
        }
        else {

            s12i_nDeltaSum = u8i_pProfile[s13i_nIdxD];
            s12i_nDeltaSum -= u8i_pProfile[s13i_nIdxB];
            s12i_nDeltaSum <<= 1;
            s12i_nDeltaSum += u8i_pProfile[s13i_nIdxA];
            s12i_nDeltaSum -= u8i_pProfile[s13i_nIdxE];
            s20i_nScore += s12i_nDeltaSum;
        }

        //  (20220804)
        s16i_pHarrScore[s13i_nIdxC] = (s20i_nScore >> SUB_PIXEL_SCALE_BIT);

        s13i_nIdxA++;
        s13i_nIdxB++;
        s13i_nIdxC++;
        s13i_nIdxD++;
        s13i_nIdxE++;
    }
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::MaxHaarScorePosition(const short* s16i_pHarrScore, unsigned short u15i_nHaarScoreSize, unsigned short* s15i_nScorePosition)
{
    try
    {
        if (s15i_nScorePosition == nullptr)
            return RESULT_ERROR;

        *s15i_nScorePosition = 0;

        if (s16i_pHarrScore == nullptr)
            return RESULT_ERROR;

        if (u15i_nHaarScoreSize == 0)
            return RESULT_ERROR;

        //     ( short: 0~size-1 )
        //       const_cast 
        const short pos = this->MaxHaarScorePosition(const_cast<short*>(s16i_pHarrScore),u15i_nHaarScoreSize);

        // : /   ( 0 )
        if (pos < 0 || (unsigned short)pos >= u15i_nHaarScoreSize)
            return RESULT_ERROR;

        *s15i_nScorePosition = (unsigned short)pos;
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

API_RESULT OpenCVUtil::GetDetectedPeakPosition(short s15i_nLinePos, unsigned short u12i_nCameraWidth, float* fPeakPosition)
{
    try
    {
        const float Value = this->GetLineRealpixelPos(s15i_nLinePos, u12i_nCameraWidth);
        *fPeakPosition = (float)Value;
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}
API_RESULT OpenCVUtil::GetZDiffPosition(short s15i_nLinePos, int* nZDiffPos)
{
    try
    {
        const int Value = this->GetZPos(s15i_nLinePos);
        *nZDiffPos = Value;
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}
API_RESULT OpenCVUtil::GetZRealPosition(short s15i_nLinePos, float* fZRealPos)
{
    try
    {
        const float Value = this->GetZRealPos(s15i_nLinePos);
        *fZRealPos = (float)Value;
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

short OpenCVUtil::MaxHaarScorePosition(short* s16i_pHarrScore, unsigned short u15i_nHaarScoreSize)
{

    //pixel level search //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    short s16i_nMaxScore = 0;
    unsigned short u15i_MaxScorePos = 0;

    for (unsigned short p = 0; p < u15i_nHaarScoreSize; p++) 
    {

        if (!(p > s16i_m_nMaskX1[0] && p < s16i_m_nMaskX2[0])
            && !(p > s16i_m_nMaskX1[1] && p < s16i_m_nMaskX2[1])
            && !(p > s16i_m_nMaskX1[2] && p < s16i_m_nMaskX2[2])) 
        {

            if (s16i_pHarrScore[p] > s16i_nMaxScore) 
            {
                s16i_nMaxScore = s16i_pHarrScore[p];
                u15i_MaxScorePos = p;
            }

        }
    }
    //s15i_nLinePos : 0 ~ sensor size H * 16
    short s15i_nLinePos = u15i_MaxScorePos;

    return s15i_nLinePos;
}


float OpenCVUtil::GetLineRealpixelPos(short s15i_nLinePos, unsigned short u12i_nCameraWidth)
{
    return (((float)s15i_nLinePos / (1 << SUB_PIXEL_SCALE_BIT)) + (u12i_nCameraWidth >> 1));
};

int OpenCVUtil::GetZPos(short s15i_nLinePos)
{
    return ((((int)s17i_m_nCoeA * s15i_nLinePos) >> COE_DET_BIT) + s16i_m_nCoeB);
};


//    s15i_nLinePos [-16384, 16383] ->  Z  (mm)
float OpenCVUtil::GetZRealPos(short s15i_nLinePos)
{
    return (((float)GetZPos(s15i_nLinePos) / 16384.0f) * 1000.f);
};

// ============================================================================
// Ring slot write-ownership helpers.
//
// The global m_ringMtx protects ring *state* (slot vector, latest index, reader guards). It must
// NOT be held across a multi-megabyte pixel copy, otherwise a single 15MB frame publish stalls
// every Acquire/Release/read on every other slot for the duration of the memcpy. The helpers
// below let a writer claim one slot under the lock, drop the lock for the copy, and then publish
// the result under the lock again, while readers of that same slot still observe the exact same
// "never see a half-written slot" guarantee the old whole-copy-under-lock code provided.
// ============================================================================

// Requires m_ringMtx held via lk. On RESULT_SUCCESS the slot is marked writing and the caller
// MUST pair it with EndSlotWrite(index, ...).
API_RESULT OpenCVUtil::BeginSlotWriteCore(std::unique_lock<std::mutex>& lk, int index)
{
    // Same result ordering the callers had before the split: NOT_INITIALIZED, then ERROR, then BUSY.
    if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;
    if (index < 0) return RESULT_ERROR;

    // Serialise writers on the same slot: wait out any in-flight out-of-lock copy.
    m_ringCv.wait(lk, [this, index] {
        return index >= (int)m_ringSlotState.size() || !m_ringSlotState[index].writing;
    });

    // Re-validate after the wait: the ring may have been rebuilt while we were blocked.
    if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;
    if (index >= (int)m_ringSlotState.size()) return RESULT_ERROR;

    // A slot that any consumer still holds must not be overwritten (tearing prevention).
    // This is now a reference count, so it stays BUSY until the LAST holder releases it.
    if (m_ringSlotState[index].readers > 0) return RESULT_LOAD_FAIL; // BUSY

    m_ringSlotState[index].writing = true;
    return RESULT_SUCCESS;
}

// Releases write ownership. published == true bumps the slot generation and makes the slot the
// latest frame. Takes m_ringMtx itself, so the caller must not hold it.
void OpenCVUtil::EndSlotWrite(int index, bool published)
{
    {
        std::lock_guard<std::mutex> lk(m_ringMtx);
        if (index >= 0 && index < (int)m_ringSlotState.size())
        {
            m_ringSlotState[index].writing = false;
            if (published)
            {
                // Only publish if the ring still holds real pixels: the buffer may have been
                // cleared or resized while the copy ran outside the lock.
                if (index < (int)m_OpenCVLibRingBuffer.size() && !m_OpenCVLibRingBuffer[index].empty())
                {
                    m_nLastWrittenIndex = index;
                    // outGeneration in AcquireLatestFrameEx must return the exact monotonic frame
                    // id (Phase B2 requirement), so the per-slot generation IS m_nNextFrameId at
                    // publish time rather than an independent per-slot counter.
                    m_ringSlotState[index].generation = m_nNextFrameId;
                    ++m_nNextFrameId;
                    m_statPublishedFrames.fetch_add(1, std::memory_order_relaxed);
                }
            }
        }
    }
    m_ringCv.notify_all();
}

// Requires m_ringMtx held via lk. Blocks until the given slot has no out-of-lock copy in flight,
// so pixel readers keep the serialisation the single global lock used to give them.
void OpenCVUtil::WaitForSlotWriteLocked(std::unique_lock<std::mutex>& lk, int index)
{
    if (index < 0) return;
    m_ringCv.wait(lk, [this, index] {
        return index >= (int)m_ringSlotState.size() || !m_ringSlotState[index].writing;
    });
}

// Requires m_ringMtx held via lk. Blocks until no slot has an out-of-lock copy in flight. Used by
// whole-ring operations, which then keep the lock and so also block any NEW write from starting.
void OpenCVUtil::WaitForAllSlotWritesLocked(std::unique_lock<std::mutex>& lk)
{
    m_ringCv.wait(lk, [this] {
        for (size_t i = 0; i < m_ringSlotState.size(); ++i)
            if (m_ringSlotState[i].writing) return false;
        return true;
    });
}

// Requires m_ringMtx held. Rebuilds the guard state to match a ring of `count` slots and drops all
// live tokens. m_nNextReadToken is deliberately NOT reset so a token issued before the rebuild can
// never be mistaken for a token issued after it.
void OpenCVUtil::ResetSlotStateLocked(size_t count)
{
    m_ringSlotState.assign(count, RingSlotState());
    m_readTokens.clear();
    // Phase B2 repair: every RingSlotState::readers this reset just zeroed had been counted into
    // m_activeReaderCount by AcquireLatestFrameCore, but the readers that held them are never coming
    // back to ReleaseFrame/ReleaseFrameToken after a ring rebuild (their slot index/token is now
    // meaningless). Without this, GetRingStats would report stale, permanently-nonzero concurrency.
    m_activeReaderCount = 0;
}

API_RESULT OpenCVUtil::SetImageBuffer(int count, int width, int height, PixelFormat format)
{
    try
    {
        if (count <= 0) return RESULT_ERROR;

        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForAllSlotWritesLocked(lk);   // never rebuild the ring under an in-flight pixel copy
        m_OpenCVLibRingBuffer.clear();
        m_OpenCVLibRingBuffer.resize((size_t)count);   // lazy:  
        ResetSlotStateLocked((size_t)count);
        m_nLastWrittenIndex = -1;
        m_nNextFrameId = 1;

        // eager: width>0      ( announce /    )
        if (width > 0 && height > 0)
        {
            int cvType = PixelFormatToCvType(format);
            if (cvType < 0) return RESULT_ERROR;
            for (int i = 0; i < count; ++i)
                m_OpenCVLibRingBuffer[i].create(height, width, cvType);
        }

        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SetImageBuffer count=%d (%dx%d)\n", count, width, height), SDK_LOGEVENT);
        return RESULT_SUCCESS;
    }
    catch (const std::bad_alloc&)
    {
        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SetImageBuffer: out of memory (bad_alloc) count=%d (%dx%d)\n", count, width, height), SDK_LOGEVENT);
        return RESULT_OUT_OF_MEMORY;
    }
    catch (const cv::Exception& e)
    {
        if (e.code == cv::Error::StsNoMem)
        {
            if (m_EventLog)
                m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SetImageBuffer: out of memory (cv::Exception StsNoMem) count=%d (%dx%d)\n", count, width, height), SDK_LOGEVENT);
            return RESULT_OUT_OF_MEMORY;
        }
        return RESULT_ERROR;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ClearImageBuffer()
{
    try
    {
        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForAllSlotWritesLocked(lk);   // never tear down the ring under an in-flight pixel copy
        m_OpenCVLibRingBuffer.clear();
        ResetSlotStateLocked(0);
        m_nLastWrittenIndex = -1;
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::SetImageBufferPtr(const CvMatView* src, int index)
{
    try
    {
        if (!src || !src->data) return RESULT_NULL_HANDLE;
        if (src->rows <= 0 || src->cols <= 0) return RESULT_ERROR;

        // (/)    Mat  copyTo   ,   
        cv::Mat wrapped((int)src->rows, (int)src->cols, (int)src->type, src->data, (size_t)src->step);

        cv::Mat dstHandle;   // shallow handle sharing the slot's pixel buffer; copy target outside the lock
        int writeIndex = index;   // resolved slot: either the caller's explicit index, or the one arbitration picks

        // ---- Stage 1: under m_ringMtx. Slot selection, guard check and state update only. ----
        {
            std::unique_lock<std::mutex> lk(m_ringMtx);

            if (index < 0)
            {
                // Producer Slot Arbitration (Phase B4 repair): SetImageBufferPtr(src, -1) is the
                // sole, atomic entry point for dynamic slot allocation -- this whole branch runs
                // under the SAME lock acquisition as the geometry setup just below, so there is no
                // window where a claimed-but-unpublished slot is visible outside this call.
                // Deliberately does NOT call BeginSlotWriteCore: that helper blocks on m_ringCv
                // until a slot frees up, but a producer must never block here (Codex Review
                // non-blocking DropNewest requirement) -- if every slot is currently held, drop this
                // frame immediately instead of waiting for one to free up.
                if (m_OpenCVLibRingBuffer.empty())
                {
                    m_statDroppedFramesNotInit.fetch_add(1, std::memory_order_relaxed);
                    return RESULT_NOT_INITIALIZED;
                }

                const int slotCount = (int)m_ringSlotState.size();
                int found = -1;
                for (int step = 1; step <= slotCount; ++step)
                {
                    int candidate = (m_nLastWrittenIndex + step) % slotCount;
                    RingSlotState& st = m_ringSlotState[candidate];
                    if (st.readers == 0 && !st.writing)
                    {
                        st.writing = true;
                        found = candidate;
                        break;
                    }
                }

                if (found < 0)
                {
                    // Every slot is held by an active reader or another in-flight write.
                    // Non-blocking DropNewest: drop this frame instead of waiting on m_ringCv, so a
                    // stalled consumer can never stall the producer.
                    m_statDroppedFramesBusy.fetch_add(1, std::memory_order_relaxed);
                    return RESULT_LOAD_FAIL;   // BUSY
                }
                writeIndex = found;
            }
            else
            {
                API_RESULT begin = BeginSlotWriteCore(lk, writeIndex);
                if (begin != RESULT_SUCCESS)
                {
                    if (begin == RESULT_NOT_INITIALIZED) m_statDroppedFramesNotInit.fetch_add(1, std::memory_order_relaxed);
                    else if (begin == RESULT_LOAD_FAIL)  m_statDroppedFramesBusy.fetch_add(1, std::memory_order_relaxed);
                    return begin;   // NOT_INITIALIZED / ERROR / LOAD_FAIL(BUSY)
                }
            }

            // copyTo reallocates its destination when the geometry differs, which would break the
            // aliasing with the slot. So match the destination geometry HERE, under the lock (a
            // cheap header/alloc operation), and carry out a shallow handle that shares the same
            // pixel buffer. Because the geometry then matches exactly, the copy below can no
            // longer reallocate and writes straight into the slot's buffer. The handle also keeps
            // an owning slot buffer alive for the whole copy even if the slot is reassigned.
            try
            {
                cv::Mat& slot = m_OpenCVLibRingBuffer[writeIndex];
                if (slot.empty() || slot.rows != wrapped.rows || slot.cols != wrapped.cols || slot.type() != wrapped.type())
                    slot.create(wrapped.rows, wrapped.cols, wrapped.type());
                dstHandle = slot;
            }
            catch (...)
            {
                // Allocation failed. Hand write ownership back before unwinding, otherwise the
                // slot would stay marked writing forever and block every later write to it.
                lk.unlock();
                EndSlotWrite(writeIndex, false);
                return RESULT_ERROR;
            }
        }

        // ---- Stage 2: NO lock held. The 15MB-class pixel copy runs here, so Acquire/Release and
        // every operation on the other slots proceed concurrently instead of stalling behind it.
        // deep copy:   VEO          
        // (: VEOImgSoLUtil.h " deep copy")
        bool copied = false;
        try
        {
            wrapped.copyTo(dstHandle);
            copied = true;
        }
        catch (...) { copied = false; }   // EndSlotWrite below must still run, or writers/waiters stall

        // ---- Stage 3: under m_ringMtx. Release write ownership and publish the new frame. ----
        EndSlotWrite(writeIndex, copied);
        return copied ? RESULT_SUCCESS : RESULT_ERROR;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::RegisterImageBufferView(const CvMatView* src, int index)
{
    try
    {
        if (!src || !src->data) return RESULT_NULL_HANDLE;

        //  (shallow) / (non-owning)   SetImageBufferPtr deep copy .
        // copyTo   src->data   cv::Mat   ( 0).
        //       (src->data)     ,
        //   / use-after-free/tearing . (: VEOImgSoLUtil.h)
        // Built before the lock: this header only wraps caller memory, and a bad geometry/type
        // must throw here rather than between BeginSlotWriteCore and EndSlotWrite.
        cv::Mat view((int)src->rows, (int)src->cols, (int)src->type, src->data, (size_t)src->step);

        std::unique_lock<std::mutex> lk(m_ringMtx);
        API_RESULT begin = BeginSlotWriteCore(lk, index);
        if (begin != RESULT_SUCCESS)
        {
            if (begin == RESULT_NOT_INITIALIZED) m_statDroppedFramesNotInit.fetch_add(1, std::memory_order_relaxed);
            else if (begin == RESULT_LOAD_FAIL)  m_statDroppedFramesBusy.fetch_add(1, std::memory_order_relaxed);
            return begin;   // NOT_INITIALIZED / ERROR / LOAD_FAIL(BUSY)
        }

        // Header-only assignment, so there is no large copy to move out of the lock here.
        m_OpenCVLibRingBuffer[index] = view;

        lk.unlock();               // EndSlotWrite reacquires m_ringMtx itself
        EndSlotWrite(index, true); // publishes: generation = frame id, latest index, frame id++

        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}


API_RESULT OpenCVUtil::GetImageBufferPtr(int index, CvMatView* out)
{
    try
    {
        if (!out) return RESULT_NULL_HANDLE;
        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForSlotWriteLocked(lk, index);   // don't hand out a header for a slot mid-copy
        if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;
        if (index < 0 || index >= (int)m_OpenCVLibRingBuffer.size()) return RESULT_ERROR;

        const cv::Mat& m = m_OpenCVLibRingBuffer[index];
        if (m.empty()) return RESULT_LOAD_FAIL;
        out->rows = m.rows;
        out->cols = m.cols;
        out->type = m.type();
        out->step = (int64_t)m.step;
        out->data = m.data;
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::SetLatestIndex(int index)
{
    try
    {
        std::unique_lock<std::mutex> lk(m_ringMtx);
        if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;
        if (index < 0 || index >= (int)m_OpenCVLibRingBuffer.size()) return RESULT_ERROR;
        // A slot whose pixel copy is still running outside the lock is not publishable yet.
        if (index < (int)m_ringSlotState.size() && m_ringSlotState[index].writing) return RESULT_LOAD_FAIL;
        if (m_OpenCVLibRingBuffer[index].empty()) return RESULT_LOAD_FAIL;
        m_nLastWrittenIndex = index;
        // Phase B2 repair: SetLatestIndex is a publish path exactly like EndSlotWrite(index, true) --
        // it marks an already-filled slot as the newest frame -- so it must assign the same
        // generation-equals-frame-id convention, or AcquireLatestFrameEx's outGeneration would return
        // this slot's stale prior generation instead of the id this publish actually represents.
        if (index < (int)m_ringSlotState.size()) m_ringSlotState[index].generation = m_nNextFrameId;
        m_nNextFrameId++;
        m_statPublishedFrames.fetch_add(1, std::memory_order_relaxed);
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

// Shared acquire path. outToken == nullptr selects the legacy contract, where the guard is
// released by ReleaseFrame(index); otherwise a token is issued and only ReleaseFrameToken(token)
// drops it. Either way the guard itself is a per-slot reference count, so concurrent acquirers
// stack instead of overwriting one another.
API_RESULT OpenCVUtil::AcquireLatestFrameCore(CvMatView* out, int* outIndex, uint64_t* outToken, uint64_t* outGeneration)
{
    std::unique_lock<std::mutex> lk(m_ringMtx);
    m_statTotalAcquireCalls.fetch_add(1, std::memory_order_relaxed);

    // If the slot currently flagged as latest is mid-copy, wait for it. The old code held
    // m_ringMtx across the whole copy, so callers blocked here anyway; this keeps that
    // observable behaviour while no longer blocking work on the other slots.
    m_ringCv.wait(lk, [this] {
        int i = m_nLastWrittenIndex;
        return i < 0 || i >= (int)m_ringSlotState.size() || !m_ringSlotState[i].writing;
    });

    if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;

    int i = m_nLastWrittenIndex;
    if (i < 0 || i >= (int)m_OpenCVLibRingBuffer.size() || m_OpenCVLibRingBuffer[i].empty())
        return RESULT_LOAD_FAIL;
    if (i >= (int)m_ringSlotState.size()) return RESULT_LOAD_FAIL;

    const cv::Mat& m = m_OpenCVLibRingBuffer[i];
    out->rows = m.rows;
    out->cols = m.cols;
    out->type = m.type();
    out->step = (int64_t)m.step;
    out->data = m.data;

    RingSlotState& st = m_ringSlotState[i];
    if (outToken)
    {
        // Register the token BEFORE taking the reference, so a throwing map insert cannot leave
        // behind a guard that no release will ever be able to drop.
        uint64_t token = m_nNextReadToken;
        m_readTokens[token] = i;
        ++m_nNextReadToken;
        *outToken = token;
        ++st.readers;                   // /    (per-slot refcount)
        ++m_activeReaderCount;
    }
    else
    {
        ++st.readers;
        ++st.legacyReaders;             // released through ReleaseFrame(index)
        ++m_activeReaderCount;
    }

    if (outIndex)      *outIndex = i;
    if (outGeneration) *outGeneration = st.generation;
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::AcquireLatestFrame(CvMatView* out, int* index)
{
    try
    {
        if (!out || !index) return RESULT_NULL_HANDLE;
        return AcquireLatestFrameCore(out, index, nullptr, nullptr);
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::AcquireLatestFrameEx(CvMatView* out, int* outIndex, uint64_t* outToken, uint64_t* outGeneration)
{
    try
    {
        if (!out || !outToken) return RESULT_NULL_HANDLE;
        *outToken = 0;
        if (outGeneration) *outGeneration = 0;
        return AcquireLatestFrameCore(out, outIndex, outToken, outGeneration);
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ReleaseFrame(int index)
{
    try
    {
        bool released = false;
        {
            std::lock_guard<std::mutex> lk(m_ringMtx);
            m_statTotalReleaseCalls.fetch_add(1, std::memory_order_relaxed);
            if (index >= 0 && index < (int)m_ringSlotState.size())
            {
                RingSlotState& st = m_ringSlotState[index];
                // Only drop a guard taken through the legacy path. A token holder's guard is
                // untouchable from here, so one consumer releasing its own frame can never
                // expose a slot another consumer is still reading.
                if (st.legacyReaders > 0)
                {
                    --st.legacyReaders;
                    if (st.readers > 0) { --st.readers; --m_activeReaderCount; }
                    released = true;
                }
            }
        }
        if (released) m_ringCv.notify_all();
        // Releasing an index nobody holds stays a harmless no-op, as before.
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ReleaseFrameToken(uint64_t token)
{
    try
    {
        if (token == 0) return RESULT_NULL_HANDLE;

        {
            std::lock_guard<std::mutex> lk(m_ringMtx);
            m_statTotalReleaseCalls.fetch_add(1, std::memory_order_relaxed);
            std::unordered_map<uint64_t, int>::iterator it = m_readTokens.find(token);
            // Unknown token: a double release or a token from before a ring rebuild. Rejected
            // rather than applied, so it cannot decrement someone else's reference count.
            if (it == m_readTokens.end()) return RESULT_ERROR;

            int index = it->second;
            m_readTokens.erase(it);
            if (index >= 0 && index < (int)m_ringSlotState.size() && m_ringSlotState[index].readers > 0)
            {
                --m_ringSlotState[index].readers;
                --m_activeReaderCount;
            }
        }
        m_ringCv.notify_all();
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

// Runtime observability snapshot (Phase B2). The four call/publish counters are atomics updated
// from their own call sites without needing m_ringMtx; activeReaders mirrors the sum of every
// slot's RingSlotState::readers, which is only ever mutated under m_ringMtx, so it is read back
// under the same lock here for a consistent snapshot.
API_RESULT OpenCVUtil::GetRingStats(RingStats* outStats)
{
    try
    {
        if (!outStats) return RESULT_NULL_HANDLE;

        std::lock_guard<std::mutex> lk(m_ringMtx);
        outStats->publishedFrames      = m_statPublishedFrames.load(std::memory_order_relaxed);
        outStats->droppedFramesBusy    = m_statDroppedFramesBusy.load(std::memory_order_relaxed);
        outStats->droppedFramesNotInit = m_statDroppedFramesNotInit.load(std::memory_order_relaxed);
        outStats->totalAcquireCalls    = m_statTotalAcquireCalls.load(std::memory_order_relaxed);
        outStats->totalReleaseCalls    = m_statTotalReleaseCalls.load(std::memory_order_relaxed);
        outStats->activeReaders        = m_activeReaderCount;

        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::GetRawPixelValue(int index, int x, int y, RawPixelValue* outValue)
{
    try
    {
        if (!outValue) return RESULT_NULL_HANDLE;

        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForSlotWriteLocked(lk, index);   // don't sample pixels from a slot mid-copy

        if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;

        if (index < 0 || index >= (int)m_OpenCVLibRingBuffer.size())
            return RESULT_ERROR;

        const cv::Mat& mat = m_OpenCVLibRingBuffer[index];
        if (mat.empty())
            return RESULT_LOAD_FAIL;

        if (x < 0 || x >= mat.cols || y < 0 || y >= mat.rows)
            return RESULT_ERROR;

        int type = mat.type();
        int depth = mat.depth();
        int channels = mat.channels();

        outValue->format = type;
        outValue->channels = channels;
        outValue->value0 = 0;
        outValue->value1 = 0;
        outValue->value2 = 0;
        outValue->value3 = 0;

        if (depth == CV_8U)
        {
            outValue->maxValue = 255;
            if (channels == 1)
            {
                outValue->value0 = mat.at<uint8_t>(y, x);
            }
            else if (channels == 3)
            {
                cv::Vec3b val = mat.at<cv::Vec3b>(y, x);
                outValue->value0 = val[0]; // B
                outValue->value1 = val[1]; // G
                outValue->value2 = val[2]; // R
            }
            else if (channels == 4)
            {
                cv::Vec4b val = mat.at<cv::Vec4b>(y, x);
                outValue->value0 = val[0]; // B
                outValue->value1 = val[1]; // G
                outValue->value2 = val[2]; // R
                outValue->value3 = val[3]; // A
            }
        }
        else if (depth == CV_16U)
        {
            outValue->maxValue = 65535;
            if (channels == 1)
            {
                outValue->value0 = mat.at<uint16_t>(y, x);
            }
            else if (channels == 3)
            {
                cv::Vec3w val = mat.at<cv::Vec3w>(y, x);
                outValue->value0 = val[0]; // B
                outValue->value1 = val[1]; // G
                outValue->value2 = val[2]; // R
            }
            else if (channels == 4)
            {
                cv::Vec4w val = mat.at<cv::Vec4w>(y, x);
                outValue->value0 = val[0]; // B
                outValue->value1 = val[1]; // G
                outValue->value2 = val[2]; // R
                outValue->value3 = val[3]; // A
            }
        }
        else
        {
            outValue->maxValue = 1;
            if (channels == 1)
            {
                outValue->value0 = (uint32_t)mat.at<float>(y, x);
            }
        }

        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

// ====== OpenCVLib RingBuffer  I/O &  ======

API_RESULT OpenCVUtil::ImageLoadToIndexWithFlags(const char* path, int index, CvMatView* out, int imreadFlags)
{
    try
    {
        if (!path) return RESULT_NULL_HANDLE;
        cv::Mat img = cv::imread(path, imreadFlags);
        if (img.empty()) return RESULT_LOAD_FAIL;

        // imread already allocated outside the lock, so only the slot handover is serialised.
        std::unique_lock<std::mutex> lk(m_ringMtx);
        API_RESULT begin = BeginSlotWriteCore(lk, index);
        if (begin != RESULT_SUCCESS)
        {
            if (begin == RESULT_NOT_INITIALIZED) m_statDroppedFramesNotInit.fetch_add(1, std::memory_order_relaxed);
            else if (begin == RESULT_LOAD_FAIL)  m_statDroppedFramesBusy.fetch_add(1, std::memory_order_relaxed);
            return begin;   // NOT_INITIALIZED / ERROR / LOAD_FAIL(BUSY)
        }

        m_OpenCVLibRingBuffer[index] = img;   //  (imread  )

        if (out)
        {
            const cv::Mat& m = m_OpenCVLibRingBuffer[index];
            out->rows = m.rows; out->cols = m.cols; out->type = m.type();
            out->step = (int64_t)m.step; out->data = m.data;
        }

        lk.unlock();               // EndSlotWrite reacquires m_ringMtx itself
        EndSlotWrite(index, true); // publishes: generation = frame id, latest index, frame id++
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ImageLoadToIndex(const char* path, int index, CvMatView* out)
{
    return ImageLoadToIndexWithFlags(path, index, out, cv::IMREAD_COLOR);
}

API_RESULT OpenCVUtil::ImageLoadDepthToIndex(const char* path, int index, CvMatView* out)
{
    return ImageLoadToIndexWithFlags(path, index, out, cv::IMREAD_ANYDEPTH | cv::IMREAD_ANYCOLOR);
}

API_RESULT OpenCVUtil::ImageSaveFromIndex(const char* path, int index)
{
    try
    {
        if (!path) return RESULT_NULL_HANDLE;
        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForSlotWriteLocked(lk, index);   // don't encode a slot mid-copy
        if (m_OpenCVLibRingBuffer.empty()) return RESULT_NOT_INITIALIZED;
        if (index < 0 || index >= (int)m_OpenCVLibRingBuffer.size()) return RESULT_ERROR;
        const cv::Mat& m = m_OpenCVLibRingBuffer[index];
        if (m.empty()) return RESULT_LOAD_FAIL;
        return cv::imwrite(std::string(path), m) ? RESULT_SUCCESS : RESULT_ERROR;
    }
    catch (...) { return RESULT_ERROR; }
}



// ============================================================================
// Load Multi-page TIFF Dataset to Ring Buffers
// ============================================================================
API_RESULT OpenCVUtil::LoadTiffStackToRingBuffers(const char* tifFilePath, int* outLoadedFrames)
{
    try
    {
        if (!tifFilePath || !outLoadedFrames) return RESULT_NULL_HANDLE;
        *outLoadedFrames = 0;

        std::string fp(tifFilePath);
        std::vector<cv::Mat> pages;
        bool ok = cv::imreadmulti(fp, pages, cv::IMREAD_UNCHANGED);
        if (!ok || pages.empty())
        {
            if (m_EventLog)
                m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] LoadTiffStackToRingBuffers FAIL: %s\n", tifFilePath), SDK_LOGEVENT);
            return RESULT_LOAD_FAIL;
        }

        int frameCount = (int)pages.size();
        {
            std::unique_lock<std::mutex> lk(m_ringMtx);
            WaitForAllSlotWritesLocked(lk);

            // Drain active batches before resizing ring buffers
            while (s_activeBatchCount.load(std::memory_order_acquire) > 0)
            {
                s_sourceDrainCv.wait_for(lk, std::chrono::milliseconds(10));
            }

            if ((int)m_OpenCVLibRingBuffer.size() < frameCount)
            {
                m_OpenCVLibRingBuffer.resize(frameCount);
                m_ringSlotState.resize(frameCount);
            }

            for (int i = 0; i < frameCount; ++i)
            {
                cv::Mat& page = pages[i];
                if (page.empty()) continue;

                if (page.channels() == 3)
                {
                    cv::cvtColor(page, page, cv::COLOR_BGR2GRAY);
                }
                else if (page.channels() == 4)
                {
                    cv::cvtColor(page, page, cv::COLOR_BGRA2GRAY);
                }

                page.copyTo(m_OpenCVLibRingBuffer[i]);
            }
        }

        *outLoadedFrames = frameCount;
        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] LoadTiffStackToRingBuffers OK: %d frame(s) loaded from %s\n", frameCount, tifFilePath), SDK_LOGEVENT);

        return RESULT_SUCCESS;
    }
    catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
    catch (...) { return RESULT_ERROR; }
}


API_RESULT OpenCVUtil::AssembleVisibleTiles(const int* frameIndices, int frameCount,
                                            int frameWidth, int frameHeight,
                                            int roiX, int roiY, int roiW, int roiH,
                                            CvMatView* outView)
{
    try
    {
        if (!frameIndices || frameCount <= 0 || !outView) return RESULT_NULL_HANDLE;
        if (roiW <= 0 || roiH <= 0) return RESULT_ERROR;

        std::unique_lock<std::mutex> lk(m_ringMtx);
        // Whole-ring read: drain in-flight out-of-lock copies first, then keep m_ringMtx for the
        // rest of the composite so no new slot write can start. Same guarantee as before the split.
        WaitForAllSlotWritesLocked(lk);
        if (!m_frameSource || m_frameSource->Capacity() == 0) return RESULT_NOT_INITIALIZED;

        // 1.        (type) 
        int startFrame = roiY / frameHeight;
        int endFrame = (roiY + roiH - 1) / frameHeight;
        startFrame = (std::max)(0, startFrame);
        endFrame = (std::min)(frameCount - 1, endFrame);

        int cvType = CV_8UC1; // Grayscale 
        for (int i = startFrame; i <= endFrame; ++i)
        {
            int ringIndex = frameIndices[i];
            const cv::Mat& m = m_frameSource->GetFrame(ringIndex);
            if (!m.empty())
            {
                cvType = m.type();
                break;
            }
        }

        // 2.     0 
        if (m_assembleBufferMat.cols != roiW || m_assembleBufferMat.rows != roiH || m_assembleBufferMat.type() != cvType)
        {
            m_assembleBufferMat.create(roiH, roiW, cvType);
        }
        m_assembleBufferMat.setTo(cv::Scalar::all(0));

        // 3.     
        for (int i = startFrame; i <= endFrame; ++i)
        {
            int ringIndex = frameIndices[i];
            const cv::Mat& srcMat = m_frameSource->GetFrame(ringIndex);
            if (srcMat.empty()) continue;

            int frameTop = i * frameHeight;
            int frameBottom = frameTop + frameHeight;

            int interTop = (std::max)(roiY, frameTop);
            int interBottom = (std::min)(roiY + roiH, frameBottom);
            if (interBottom <= interTop) continue;

            int interLeft = (std::max)(0, roiX);
            int interRight = (std::min)(frameWidth, roiX + roiW);
            if (interRight <= interLeft) continue;

            int localX = interLeft;
            int localY = interTop - frameTop;
            int localW = interRight - interLeft;
            int localH = interBottom - interTop;

            int destX = interLeft - roiX;
            int destY = interTop - roiY;

            cv::Rect srcRect(localX, localY, localW, localH);
            srcRect &= cv::Rect(0, 0, srcMat.cols, srcMat.rows);
            if (srcRect.width <= 0 || srcRect.height <= 0) continue;

            cv::Rect destRect(destX, destY, srcRect.width, srcRect.height);
            destRect &= cv::Rect(0, 0, m_assembleBufferMat.cols, m_assembleBufferMat.rows);
            if (destRect.width <= 0 || destRect.height <= 0) continue;

            cv::Mat srcRoi = srcMat(srcRect);
            cv::Mat destRoi = m_assembleBufferMat(destRect);
            srcRoi.copyTo(destRoi);
        }

        // 4.  
        outView->cols = m_assembleBufferMat.cols;
        outView->rows = m_assembleBufferMat.rows;
        outView->type = m_assembleBufferMat.type();
        outView->step = (int64_t)m_assembleBufferMat.step;
        outView->data = m_assembleBufferMat.data;

        return RESULT_SUCCESS;
    }
    catch (const std::bad_alloc&)
    {
        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] AssembleVisibleTiles: out of memory (bad_alloc) roi=%dx%d\n", roiW, roiH), SDK_LOGEVENT);
        return RESULT_OUT_OF_MEMORY;
    }
    catch (const cv::Exception& e)
    {
        if (e.code == cv::Error::StsNoMem)
        {
            if (m_EventLog)
                m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] AssembleVisibleTiles: out of memory (cv::Exception StsNoMem) roi=%dx%d\n", roiW, roiH), SDK_LOGEVENT);
            return RESULT_OUT_OF_MEMORY;
        }
        return RESULT_ERROR;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

// ============================================================================
// Stitched TIFF export with automatic size-based chunking (Phase 1 revision)
// Frames are vertically concatenated (cv::vconcat) into a single-page TIFF
// instead of one multi-page TIFF. If the accumulated uncompressed size would
// exceed the ~1.8 GB safety boundary, the output is split across multiple
// files ("<name>_partN<ext>") so every emitted file stays safely below the
// 32-bit TIFF offset ceiling.
// ============================================================================
API_RESULT OpenCVUtil::SaveScanAsBigTiff(const char* filepath, const int* frameIndices, int frameCount, int compression)
{
    try
    {
        if (!filepath || !frameIndices || frameCount <= 0) return RESULT_NULL_HANDLE;

        std::vector<cv::Mat> pages;
        {
            std::unique_lock<std::mutex> lk(m_ringMtx);
            WaitForAllSlotWritesLocked(lk);   // no half-copied slot may enter the page list
            if (!m_frameSource || m_frameSource->Capacity() == 0) return RESULT_NOT_INITIALIZED;

            pages.reserve((size_t)frameCount);
            for (int i = 0; i < frameCount; ++i)
            {
                const cv::Mat& m = m_frameSource->GetFrame(frameIndices[i]);
                if (m.empty()) continue;
                pages.push_back(m); // shallow copy: shares the underlying buffer, no pixel copy
            }
        }
        if (pages.empty()) return RESULT_LOAD_FAIL;

        // compression: 0=LZW(default), 1=Deflate(Adobe), 2=none
        int tiffCompressionTag = 5;        // COMPRESSION_LZW
        if (compression == 1)      tiffCompressionTag = 8; // COMPRESSION_ADOBE_DEFLATE
        else if (compression == 2) tiffCompressionTag = 1; // COMPRESSION_NONE
        std::vector<int> params = { cv::IMWRITE_TIFF_COMPRESSION, tiffCompressionTag };

        // Estimate raw uncompressed bytes per frame from the first valid frame and use
        // it to decide how many frames may share one output file under the 1.8 GB
        // boundary. Conservative by design: actual files use compression on top of this.
        const uint64_t kChunkByteLimit = 1800000000ULL;

        // Reject the whole call before any stitching or write if a page is
        // incompatible with pages[0] (cols/type), or if any single page alone
        // would exceed kChunkByteLimit (framesPerChunk below would otherwise
        // clamp to 1 and hand an oversized frame to cv::imwrite as classic TIFF).
        for (size_t i = 0; i < pages.size(); ++i)
        {
            if (pages[i].cols != pages[0].cols || pages[i].type() != pages[0].type())
            {
                if (m_EventLog)
                    m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SaveScanAsBigTiff: incompatible frame %d (cols=%d type=%d, expected cols=%d type=%d)\n",
                        (int)i, pages[i].cols, pages[i].type(), pages[0].cols, pages[0].type()), SDK_LOGEVENT);
                return RESULT_ERROR;
            }

            uint64_t frameBytes = (uint64_t)pages[i].total() * (uint64_t)pages[i].elemSize();
            if (frameBytes > kChunkByteLimit)
            {
                if (m_EventLog)
                    m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SaveScanAsBigTiff: frame %d size %llu exceeds limit %llu\n",
                        (int)i, frameBytes, kChunkByteLimit), SDK_LOGEVENT);
                return RESULT_ERROR;
            }
        }

        uint64_t perFrameBytes = (uint64_t)pages[0].total() * (uint64_t)pages[0].elemSize();
        size_t framesPerChunk = pages.size();
        if (perFrameBytes > 0)
        {
            uint64_t computed = kChunkByteLimit / perFrameBytes;
            framesPerChunk = computed > 0 ? (size_t)computed : 1;
        }
        size_t chunkCount = (pages.size() + framesPerChunk - 1) / framesPerChunk;

        // Split filepath into base + extension so chunk suffixes can be inserted as
        // "<base>_partN<ext>" (find_last_of("/\\") guards against dots in directory names).
        std::string fp(filepath);
        std::string base = fp;
        std::string ext = ".tif";
        size_t dotPos = fp.find_last_of('.');
        size_t slashPos = fp.find_last_of("/\\");
        if (dotPos != std::string::npos && (slashPos == std::string::npos || dotPos > slashPos))
        {
            base = fp.substr(0, dotPos);
            ext = fp.substr(dotPos);
        }

        for (size_t chunkIdx = 0; chunkIdx < chunkCount; ++chunkIdx)
        {
            size_t begin = chunkIdx * framesPerChunk;
            size_t end = (std::min)(begin + framesPerChunk, pages.size());

            std::vector<cv::Mat> group(pages.begin() + begin, pages.begin() + end);
            std::string outPath = (chunkCount == 1) ? fp : (base + "_part" + std::to_string(chunkIdx + 1) + ext);
            bool ok = cv::imwritemulti(outPath, group, params);

            if (m_EventLog)
                m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SaveScanAsBigTiff: %s chunk=%d/%d frames=%d path=%s compression=%d\n",
                    ok ? "OK" : "FAIL", (int)(chunkIdx + 1), (int)chunkCount, (int)(end - begin), outPath.c_str(), tiffCompressionTag), SDK_LOGEVENT);

            if (!ok) return RESULT_ERROR;
        }

        return RESULT_SUCCESS;
    }
    catch (const std::bad_alloc&)
    {
        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] SaveScanAsBigTiff: out of memory (bad_alloc)\n"), SDK_LOGEVENT);
        return RESULT_OUT_OF_MEMORY;
    }
    catch (const cv::Exception& e)
    {
        if (e.code == cv::Error::StsNoMem) return RESULT_OUT_OF_MEMORY;
        return RESULT_ERROR;
    }
    catch (...) { return RESULT_ERROR; }
}

// ============================================================================
// MMF    RAW    (Phase 2 )
// EnableDiskFrameSource    .  0..frameCount-1
// frameWidth*frameHeight*elemSize(type)      .
// ============================================================================
API_RESULT OpenCVUtil::ExportFramesToRawFile(const char* filepath, const int* frameIndices, int frameCount)
{
    try
    {
        if (!filepath || !frameIndices || frameCount <= 0) return RESULT_NULL_HANDLE;

        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForAllSlotWritesLocked(lk);   // no half-copied slot may be dumped to the backing file
        if (!m_frameSource || m_frameSource->Capacity() == 0) return RESULT_NOT_INITIALIZED;

        FILE* fp = nullptr;
        errno_t err = fopen_s(&fp, filepath, "wb");
        if (err != 0 || !fp) return RESULT_ERROR;

        bool ok = true;
        for (int i = 0; i < frameCount && ok; ++i)
        {
            const cv::Mat& m = m_frameSource->GetFrame(frameIndices[i]);
            if (m.empty()) { ok = false; break; }

            //   (step == cols*elemSize)      .
            size_t rowBytes = (size_t)m.cols * m.elemSize();
            for (int r = 0; r < m.rows && ok; ++r)
            {
                if (fwrite(m.ptr(r), 1, rowBytes, fp) != rowBytes) ok = false;
            }
        }

        fclose(fp);

        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] ExportFramesToRawFile: %s frames=%d\n", ok ? "OK" : "FAIL", frameCount), SDK_LOGEVENT);

        return ok ? RESULT_SUCCESS : RESULT_ERROR;
    }
    catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
    catch (const cv::Exception& e)
    {
        if (e.code == cv::Error::StsNoMem) return RESULT_OUT_OF_MEMORY;
        return RESULT_ERROR;
    }
    catch (...) { return RESULT_ERROR; }
}

// ============================================================================
// AssembleVisibleTiles    RAM  <-> MMF/LRU   .
// ============================================================================
API_RESULT OpenCVUtil::EnableDiskFrameSource(const char* backingFilePath, int frameCount, int frameWidth, int frameHeight, PixelFormat format, int lruCapacity)
{
    try
    {
        if (!backingFilePath || frameCount <= 0 || frameWidth <= 0 || frameHeight <= 0) return RESULT_NULL_HANDLE;

        int cvType = PixelFormatToCvType(format);
        if (cvType < 0) return RESULT_ERROR;

        std::string narrowPath(backingFilePath);
        std::wstring widePath(narrowPath.begin(), narrowPath.end());

        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForAllSlotWritesLocked(lk);

        // Wait for any in-flight batch to drain before swapping frame source
        while (s_activeBatchCount.load(std::memory_order_acquire) > 0)
        {
            s_sourceDrainCv.wait_for(lk, std::chrono::milliseconds(10));
        }

        if (!m_mmfFrameSource) m_mmfFrameSource = new MmfFrameSource();

        size_t capacity = (size_t)((lruCapacity > 0) ? lruCapacity : 32);
        if (!m_mmfFrameSource->Open(widePath, frameCount, frameWidth, frameHeight, cvType, capacity))
            return RESULT_LOAD_FAIL;

        m_frameSource = m_mmfFrameSource;
        m_bUsingDiskFrameSource = true;

        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] EnableDiskFrameSource: %s frames=%d (%dx%d)\n",
                narrowPath.c_str(), frameCount, frameWidth, frameHeight), SDK_LOGEVENT);

        return RESULT_SUCCESS;
    }
    catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::DisableDiskFrameSource()
{
    try
    {
        std::unique_lock<std::mutex> lk(m_ringMtx);
        WaitForAllSlotWritesLocked(lk);

        // Wait for any in-flight batch to drain before closing disk source
        while (s_activeBatchCount.load(std::memory_order_acquire) > 0)
        {
            s_sourceDrainCv.wait_for(lk, std::chrono::milliseconds(10));
        }

        if (m_mmfFrameSource) m_mmfFrameSource->Close();
        m_frameSource = m_ramFrameSource;
        m_bUsingDiskFrameSource = false;

        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] DisableDiskFrameSource: reverted to RAM ring buffer\n"), SDK_LOGEVENT);

        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}


// ============================================================================
// MmfFrameSource: MMF(Memory-Mapped File) + LRU   
// ============================================================================
MmfFrameSource::MmfFrameSource()
    : m_hFile(nullptr), m_hMapping(nullptr),
      m_frameCount(0), m_frameWidth(0), m_frameHeight(0), m_cvType(CV_8UC1),
      m_frameBytes(0), m_lruCapacity(32), m_closing(false)
{
}

MmfFrameSource::~MmfFrameSource()
{
    Close();
}

void MmfFrameSource::CloseLocked(std::unique_lock<std::mutex>& lk)
{
    m_closing = true;

    // Block until all outstanding AcquireFrame pins drain back to zero
    m_pinDrainCv.wait(lk, [&] {
        for (const auto& kv : m_cachedViews)
        {
            if (kv.second.pinCount > 0) return false;
        }
        return true;
    });

    for (auto& kv : m_cachedViews)
    {
        if (kv.second.mappedBase) UnmapViewOfFile(kv.second.mappedBase);
    }
    m_cachedViews.clear();
    m_lruOrder.clear();
    m_lruLookup.clear();

    if (m_hMapping) { CloseHandle(m_hMapping); m_hMapping = nullptr; }
    if (m_hFile)    { CloseHandle(m_hFile);    m_hFile = nullptr; }

    m_frameCount = 0;
    m_closing = false;
}

void MmfFrameSource::Close()
{
    std::unique_lock<std::mutex> lk(m_mtx);
    CloseLocked(lk);
}

bool MmfFrameSource::Open(const std::wstring& backingFilePath, int frameCount, int frameWidth, int frameHeight, int cvType, size_t lruCapacity)
{
    std::unique_lock<std::mutex> lk(m_mtx);
    CloseLocked(lk);

    m_closing = false;
    m_frameCount  = frameCount;
    m_frameWidth  = frameWidth;
    m_frameHeight = frameHeight;
    m_cvType      = cvType;
    m_frameBytes  = (size_t)CV_ELEM_SIZE(cvType) * (size_t)frameWidth * (size_t)frameHeight;
    m_lruCapacity = (lruCapacity > 0) ? lruCapacity : 32;

    m_hFile = CreateFileW(backingFilePath.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL,
                           OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (m_hFile == INVALID_HANDLE_VALUE) { m_hFile = nullptr; return false; }

    LARGE_INTEGER fileSize{};
    if (!GetFileSizeEx(m_hFile, &fileSize)) { CloseLocked(lk); return false; }

    uint64_t requiredBytes = (uint64_t)m_frameBytes * (uint64_t)m_frameCount;
    if ((uint64_t)fileSize.QuadPart < requiredBytes) { CloseLocked(lk); return false; }

    m_hMapping = CreateFileMappingW(m_hFile, NULL, PAGE_READONLY, 0, 0, NULL);
    if (m_hMapping == nullptr) { CloseLocked(lk); return false; }

    return true;
}

void MmfFrameSource::Touch(int ringIndex)
{
    auto it = m_lruLookup.find(ringIndex);
    if (it != m_lruLookup.end())
        m_lruOrder.erase(it->second);

    m_lruOrder.push_front(ringIndex);
    m_lruLookup[ringIndex] = m_lruOrder.begin();
}

void MmfFrameSource::EvictIfNeeded()
{
    while (m_lruOrder.size() > m_lruCapacity)
    {
        bool foundVictim = false;
        int victim = -1;

        for (auto rit = m_lruOrder.rbegin(); rit != m_lruOrder.rend(); ++rit)
        {
            int idx = *rit;
             // Protect current newly mapped frame from self-eviction
            auto cvIt = m_cachedViews.find(idx);
            if (cvIt != m_cachedViews.end() && cvIt->second.pinCount == 0)
            {
                victim = idx;
                foundVictim = true;
                break;
            }
        }

        if (!foundVictim) break;

        auto lookupIt = m_lruLookup.find(victim);
        if (lookupIt != m_lruLookup.end())
        {
            m_lruOrder.erase(lookupIt->second);
            m_lruLookup.erase(lookupIt);
        }

        auto cvIt = m_cachedViews.find(victim);
        if (cvIt != m_cachedViews.end())
        {
            if (cvIt->second.mappedBase) UnmapViewOfFile(cvIt->second.mappedBase);
            m_cachedViews.erase(cvIt);
        }
    }
}

MmfFrameSource::CachedFrame* MmfFrameSource::GetOrMapLocked(int ringIndex)
{
    if (ringIndex < 0 || ringIndex >= m_frameCount || !m_hMapping) return nullptr;

    auto found = m_cachedViews.find(ringIndex);
    if (found != m_cachedViews.end())
    {
        Touch(ringIndex);
        return &found->second;
    }

    SYSTEM_INFO si{};
    GetSystemInfo(&si);
    DWORD gran = si.dwAllocationGranularity;

    uint64_t frameOffset = (uint64_t)ringIndex * (uint64_t)m_frameBytes;
    uint64_t mapStart    = (frameOffset / gran) * gran;
    DWORD delta          = (DWORD)(frameOffset - mapStart);
    size_t mapSize       = (size_t)delta + m_frameBytes;

    DWORD offsetHigh = (DWORD)(mapStart >> 32);
    DWORD offsetLow  = (DWORD)(mapStart & 0xFFFFFFFF);

    void* ptr = MapViewOfFile(m_hMapping, FILE_MAP_READ, offsetHigh, offsetLow, mapSize);
    if (!ptr) return nullptr;

    unsigned char* framePtr = (unsigned char*)ptr + delta;
    cv::Mat view(m_frameHeight, m_frameWidth, m_cvType, framePtr);

    CachedFrame cf;
    cf.mappedBase = ptr;
    cf.view       = view;
    cf.pinCount   = 0;

    m_cachedViews[ringIndex] = cf;
    Touch(ringIndex);
    EvictIfNeeded(); // Protect ringIndex from self-eviction

    return &m_cachedViews[ringIndex];
}

const cv::Mat& MmfFrameSource::GetFrame(int ringIndex)
{
    static const cv::Mat s_empty;
    std::lock_guard<std::mutex> lk(m_mtx);
    CachedFrame* cf = GetOrMapLocked(ringIndex);
    return cf ? cf->view : s_empty;
}

bool MmfFrameSource::AcquireFrame(int ringIndex, cv::Mat* outFrame)
{
    if (!outFrame) return false;
    std::lock_guard<std::mutex> lk(m_mtx);
    if (m_closing) return false;

    CachedFrame* cf = GetOrMapLocked(ringIndex);
    if (!cf) return false;

    cf->pinCount++;
    *outFrame = cf->view;
    return true;
}

void MmfFrameSource::ReleaseFrame(int ringIndex)
{
    std::lock_guard<std::mutex> lk(m_mtx);
    auto found = m_cachedViews.find(ringIndex);
    if (found != m_cachedViews.end())
    {
        if (found->second.pinCount > 0)
        {
            found->second.pinCount--;
            if (found->second.pinCount == 0 && m_closing)
            {
                m_pinDrainCv.notify_all();
            }
        }
    }
}


API_RESULT OpenCVUtil::LoadAFLaserRawImageDataSet(const char* folderPath, const char* filePrefix, const char* fileExt,
                                                  int startIndex, int width, int height, int* outFrameCount)
{
    try
    {
        if (!folderPath || !filePrefix || !fileExt || !outFrameCount) return RESULT_NULL_HANDLE;
        if (width <= 0 || height <= 0 || startIndex < 0) return RESULT_ERROR;

        *outFrameCount = 0;

        const std::streamsize expectedBytes = (std::streamsize)width * (std::streamsize)height; // 8bit Mono
        std::string dir(folderPath);
        if (!dir.empty())
        {
            const char tail = dir[dir.size() - 1];
            if (tail != '\\' && tail != '/') dir += '\\';
        }

        // 기존 ?이?셋? ??로드가 ?공???에 교체?다. 먼? clear() ?버리면 로드가 중간??        // ?패?을 ???전 ?이?셋(?? 직전??로드??mp4)까? ?라지?데, UI ????실??모른 ?        // ?전 ?레???? 그???고 ?어 ?후 모든 ?레??취득??조용???패?다.
        std::vector<cv::Mat> staged;

        bool sizeMismatch = false;
        for (int n = startIndex; ; ++n)
        {
            const std::string path = dir + filePrefix + std::to_string(n) + fileExt;

            std::ifstream fs(path.c_str(), std::ios::binary);
            if (!fs.is_open()) break;   // 결번 = ?상 종료 지??

            fs.seekg(0, std::ios::end);
            const std::streamsize actualBytes = fs.tellg();
            if (actualBytes != expectedBytes) { sizeMismatch = true; break; }
            fs.seekg(0, std::ios::beg);

            // ?로 ?당???일 채널 Mat ? ?속 메모리이므???번에 ?어 ?는??
            cv::Mat frame(height, width, CV_8UC1);
            fs.read(reinterpret_cast<char*>(frame.data), expectedBytes);
            if (fs.gcount() != expectedBytes) { sizeMismatch = true; break; }

            staged.push_back(frame);
        }

        // ?기 불일치는 ?상???입???상 ?일????패??린?? ??경우 기존 ?이?셋??        // 그???어 UI ?태(?레?????크?범위)? ?제 ?이?? ?긋?? ?게 ?다.
        if (sizeMismatch || staged.empty())
        {
            *outFrameCount = 0;
            return RESULT_LOAD_FAIL;
        }

        std::lock_guard<std::mutex> lk(m_afMtx);
        m_afVideoDataSet.swap(staged);
        *outFrameCount = (int)m_afVideoDataSet.size();
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::LoadAFLaserVideoDataSet(const char* filepath, int* outFrameCount)
{
    try
    {
        if (!filepath || !outFrameCount) return RESULT_NULL_HANDLE;
        cv::VideoCapture cap(filepath);
        if (!cap.isOpened()) return RESULT_LOAD_FAIL;
        int frameCount = (int)cap.get(cv::CAP_PROP_FRAME_COUNT);
        if (frameCount <= 0) return RESULT_ERROR;

        std::lock_guard<std::mutex> lk(m_afMtx);
        m_afVideoDataSet.clear();
        if (frameCount > 0)
            m_afVideoDataSet.reserve(frameCount);

        cv::Mat bgrFrame, grayFrame;
        while (cap.read(bgrFrame))
        {
            if (bgrFrame.empty()) break;
            if (bgrFrame.channels() > 1)
                cv::cvtColor(bgrFrame, grayFrame, cv::COLOR_BGR2GRAY);
            else
                bgrFrame.copyTo(grayFrame);
            m_afVideoDataSet.push_back(grayFrame.clone());
        }
        *outFrameCount = (int)m_afVideoDataSet.size();
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::GetAFLaserVideoFrame(int index, CvMatView* outView)
{
    if (!outView) return RESULT_NULL_HANDLE;
    std::lock_guard<std::mutex> lk(m_afMtx);
    if (index < 0 || index >= (int)m_afVideoDataSet.size()) return RESULT_ERROR;
    const cv::Mat& m = m_afVideoDataSet[index];
    if (m.empty()) return RESULT_ERROR;
    outView->rows = m.rows;
    outView->cols = m.cols;
    outView->type = m.type();
    outView->step = (int64_t)m.step;
    outView->data = m.data;
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::ClearAFLaserVideoDataSet()
{
    std::lock_guard<std::mutex> lk(m_afMtx);
    m_afVideoDataSet.clear();
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::SetAFLaserSingleImage(const CvMatView* src)
{
    if (!src || src->data == nullptr) return RESULT_NULL_HANDLE;
    if (src->cols <= 0 || src->rows <= 0) return RESULT_ERROR;
    try
    {
        cv::Mat wrapper(src->rows, src->cols, src->type,
                        src->data, (size_t)src->step);
        std::lock_guard<std::mutex> lk(m_afMtx);
        if (wrapper.channels() > 1)
            cv::cvtColor(wrapper, m_afSingleAnalysisMat, cv::COLOR_BGR2GRAY);
        else
            wrapper.copyTo(m_afSingleAnalysisMat);
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::GetAFLaserSingleImage(CvMatView* outView)
{
    if (!outView) return RESULT_NULL_HANDLE;
    std::lock_guard<std::mutex> lk(m_afMtx);
    if (m_afSingleAnalysisMat.empty()) return RESULT_NOT_INITIALIZED;
    outView->rows = m_afSingleAnalysisMat.rows;
    outView->cols = m_afSingleAnalysisMat.cols;
    outView->type = m_afSingleAnalysisMat.type();
    outView->step = (int64_t)m_afSingleAnalysisMat.step;
    outView->data = m_afSingleAnalysisMat.data;
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::LoadAFLaserSingleImageFromFile(const char* filepath)
{
    try
    {
        if (!filepath) return RESULT_NULL_HANDLE;
        cv::Mat img = cv::imread(filepath, cv::IMREAD_GRAYSCALE);
        if (img.empty()) return RESULT_LOAD_FAIL;
        std::lock_guard<std::mutex> lk(m_afMtx);
        m_afSingleAnalysisMat = img.clone();
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}


API_RESULT OpenCVUtil::BeginVideoDataSet(int expectedFrameCount, double captureFps)
{
    //     . Writer      AppendFrameToVideoDataSet .
    std::lock_guard<std::mutex> lk(m_afMtx);
    m_afVideoDataSet.clear();
    if (expectedFrameCount > 0)
        m_afVideoDataSet.reserve(expectedFrameCount);
    m_afCaptureFps = (captureFps > 0.0) ? captureFps : 30.0;
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::EndVideoDataSet(int* outFrameCount)
{
    std::lock_guard<std::mutex> lk(m_afMtx);
    if (outFrameCount) *outFrameCount = (int)m_afVideoDataSet.size();
    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::AppendFrameToVideoDataSet(const CvMatView* src)
{
    if (!src || src->data == nullptr) return RESULT_NULL_HANDLE;
    if (src->cols <= 0 || src->rows <= 0) return RESULT_ERROR;
    try
    {
        cv::Mat wrapper(src->rows, src->cols, src->type, src->data, (size_t)src->step);
        std::lock_guard<std::mutex> lk(m_afMtx);
        if (wrapper.channels() > 1)
        {
            cv::Mat gray;
            cv::cvtColor(wrapper, gray, cv::COLOR_BGR2GRAY);
            m_afVideoDataSet.push_back(gray);
        }
        else
        {
            m_afVideoDataSet.push_back(wrapper.clone());
        }
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ExportVideoDataSet(const char* filepath, double fps,
                                          const char* fourccCode, int* outWritten)
{
    try
    {
        if (!filepath) return RESULT_NULL_HANDLE;
        std::lock_guard<std::mutex> lk(m_afMtx);
        if (m_afVideoDataSet.empty()) return RESULT_ERROR;

        const cv::Mat& first = m_afVideoDataSet[0];
        if (first.empty()) return RESULT_ERROR;
        cv::Size frameSize(first.cols, first.rows);

        int fourcc;
        if (fourccCode && strlen(fourccCode) >= 4)
        {
            fourcc = cv::VideoWriter::fourcc(fourccCode[0], fourccCode[1],
                                             fourccCode[2], fourccCode[3]);
        }
        else
        {
            std::string fp(filepath);
            std::string ext;
            size_t dot = fp.find_last_of('.');
            if (dot != std::string::npos) ext = fp.substr(dot + 1);
            for (auto& c : ext) c = (char)tolower((unsigned char)c);
            fourcc = (ext == "avi") ? cv::VideoWriter::fourcc('M', 'J', 'P', 'G')
                                    : cv::VideoWriter::fourcc('m', 'p', '4', 'v');
        }
        if (fps <= 0.0) fps = m_afCaptureFps;

        bool isColor = (first.channels() > 1);
        cv::VideoWriter writer(filepath, fourcc, fps, frameSize, isColor);
        if (!writer.isOpened())
        {
            isColor = true;
            writer.open(filepath, fourcc, fps, frameSize, true);
            if (!writer.isOpened()) return RESULT_ERROR;
        }

        int written = 0;
        cv::Mat tmp;
        for (const auto& f : m_afVideoDataSet)
        {
            if (f.empty()) continue;
            const cv::Mat* w = &f;
            if (f.size() != frameSize) { cv::resize(f, tmp, frameSize); w = &tmp; }
            if (isColor && w->channels() == 1)
            {
                cv::cvtColor(*w, tmp, cv::COLOR_GRAY2BGR);
                writer.write(tmp);
            }
            else
            {
                writer.write(*w);
            }
            ++written;
        }
        writer.release();
        if (outWritten) *outWritten = written;
        return (written > 0) ? RESULT_SUCCESS : RESULT_ERROR;
    }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ConvertToBgr24(const CvMatView* src, CvMatView* dst)
{
    try
    {
        if (!src || !dst) return RESULT_ERROR;
        if (!src->data || !dst->data) return RESULT_ERROR;
        if (src->rows <= 0 || src->cols <= 0) return RESULT_ERROR;
        if (dst->rows != src->rows || dst->cols != src->cols) return RESULT_ERROR;
        if (dst->type != CV_8UC3) return RESULT_ERROR;
        if (dst->step < dst->cols * 3) return RESULT_ERROR;

        cv::Mat srcMat(src->rows, src->cols, src->type, src->data, (size_t)src->step);
        cv::Mat dstMat(dst->rows, dst->cols, dst->type, dst->data, (size_t)dst->step);

        if (src->type == CV_8UC1) // Mono8
        {
            cv::cvtColor(srcMat, dstMat, cv::COLOR_GRAY2BGR);
            return RESULT_SUCCESS;
        }
        else if (src->type == CV_16UC1) // Mono10 / 12 / 16
        {
            // [display-only]      raw ( )  .
            double minVal = 0, maxVal = 0;
            cv::minMaxLoc(srcMat, &minVal, &maxVal);

            if (maxVal <= 0)
            {
                dstMat.setTo(cv::Scalar(0, 0, 0));
                return RESULT_SUCCESS;
            }

            double scale = 1.0;
            if (maxVal <= 1023) // Mono10 
            {
                scale = 255.0 / 1023.0;
            }
            else if (maxVal <= 4095) // Mono12 
            {
                scale = 255.0 / 4095.0;
            }
            else // Mono16 
            {
                scale = 255.0 / 65535.0;
            }

            cv::Mat temp8;
            srcMat.convertTo(temp8, CV_8U, scale);
            cv::cvtColor(temp8, dstMat, cv::COLOR_GRAY2BGR);
            return RESULT_SUCCESS;
        }
        else if (src->type == CV_8UC3) // BGR24
        {
            if (src->data != dst->data)
            {
                srcMat.copyTo(dstMat);
            }
            return RESULT_SUCCESS;
        }
        else
        {
            return RESULT_ERROR;
        }
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

// PixelFormat(8bit Bayer ) -> cv::cvtColor  demosaic  .
// toRgbOrder=true  RGB (*2RGB), false  BGR (*2BGR)  .
// Bayer   -1  .
int OpenCVUtil::PixelFormatToBayerCode(PixelFormat pixelFormat, bool toRgbOrder)
{
    switch (pixelFormat)
    {
    case PixelFormat::BayerRG8: return toRgbOrder ? cv::COLOR_BayerRG2RGB : cv::COLOR_BayerRG2BGR;
    case PixelFormat::BayerBG8: return toRgbOrder ? cv::COLOR_BayerBG2RGB : cv::COLOR_BayerBG2BGR;
    case PixelFormat::BayerGB8: return toRgbOrder ? cv::COLOR_BayerGB2RGB : cv::COLOR_BayerGB2BGR;
    case PixelFormat::BayerGR8: return toRgbOrder ? cv::COLOR_BayerGR2RGB : cv::COLOR_BayerGR2BGR;
    default: return -1;
    }
}

API_RESULT OpenCVUtil::ConvertBayerToRgb24(const CvMatView* src, CvMatView* dst, PixelFormat bayerFormat)
{
    try
    {
        if (!src || !dst) return RESULT_ERROR;
        if (!src->data || !dst->data) return RESULT_ERROR;
        if (src->rows <= 0 || src->cols <= 0) return RESULT_ERROR;
        if (src->type != CV_8UC1) return RESULT_ERROR;           // raw Bayer must be single-channel 8-bit
        if (dst->rows != src->rows || dst->cols != src->cols) return RESULT_ERROR;
        if (dst->type != CV_8UC3) return RESULT_ERROR;
        if (dst->step < dst->cols * 3) return RESULT_ERROR;

        // BGR   demosaic (toRgbOrder=false).    sibling
        // ConvertToBgr24   SetFrame/CHImageViewer    BGR 
        //  ,      ( 5 note 2).
        int bayerCode = PixelFormatToBayerCode(bayerFormat, /*toRgbOrder*/ false);
        if (bayerCode < 0) return RESULT_ERROR;                  // caller passed a non-Bayer PixelFormat

        cv::Mat srcMat(src->rows, src->cols, src->type, src->data, (size_t)src->step);
        cv::Mat dstMat(dst->rows, dst->cols, dst->type, dst->data, (size_t)dst->step);

        cv::cvtColor(srcMat, dstMat, bayerCode);                 // writes directly into dst's buffer  no memcpy needed
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

// [clipping]   drawing  OpenCV  drawing API(line, circle) .
//        , OpenCV   
// (clipping)    (crash)   .

API_RESULT OpenCVUtil::DrawLineBgr24(CvMatView* view, int x1, int y1, int x2, int y2, unsigned char b, unsigned char g, unsigned char r, int thickness)
{
    try
    {
        if (!view || !view->data) return RESULT_ERROR;
        if (view->rows <= 0 || view->cols <= 0) return RESULT_ERROR;
        if (view->type != CV_8UC3) return RESULT_ERROR;
        if (thickness < 1) return RESULT_ERROR;

        cv::Mat img(view->rows, view->cols, view->type, view->data, (size_t)view->step);
        cv::line(img, cv::Point(x1, y1), cv::Point(x2, y2), cv::Scalar(b, g, r), thickness);
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

API_RESULT OpenCVUtil::DrawCrossMarkerBgr24(CvMatView* view, int x, int y, int size, unsigned char b, unsigned char g, unsigned char r, int thickness)
{
    try
    {
        if (!view || !view->data) return RESULT_ERROR;
        if (view->rows <= 0 || view->cols <= 0) return RESULT_ERROR;
        if (view->type != CV_8UC3) return RESULT_ERROR;
        if (size < 1 || thickness < 1) return RESULT_ERROR;

        // size     / (total marker size) , half = size / 2 .
        int half = size / 2;
        cv::Mat img(view->rows, view->cols, view->type, view->data, (size_t)view->step);
        cv::line(img, cv::Point(x - half, y), cv::Point(x + half, y), cv::Scalar(b, g, r), thickness);
        cv::line(img, cv::Point(x, y - half), cv::Point(x, y + half), cv::Scalar(b, g, r), thickness);
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}

API_RESULT OpenCVUtil::DrawCircleBgr24(CvMatView* view, int cx, int cy, int radius, unsigned char b, unsigned char g, unsigned char r, int thickness)
{
    try
    {
        if (!view || !view->data) return RESULT_ERROR;
        if (view->rows <= 0 || view->cols <= 0) return RESULT_ERROR;
        if (view->type != CV_8UC3) return RESULT_ERROR;
        if (radius < 0 || thickness < 1) return RESULT_ERROR;

        cv::Mat img(view->rows, view->cols, view->type, view->data, (size_t)view->step);
        cv::circle(img, cv::Point(cx, cy), radius, cv::Scalar(b, g, r), thickness);
        return RESULT_SUCCESS;
    }
    catch (...)
    {
        return RESULT_ERROR;
    }
}


#pragma region 9. Fast Template Matching & Wafer Alignment
API_RESULT OpenCVUtil::RegisterTemplatePattern(const CvMatView* templateView, int patternId)
{
    if (!templateView || !templateView->data || templateView->cols <= 0 || templateView->rows <= 0)
        return RESULT_ERROR;

    try
    {
        int cvType = (templateView->type >= 0) ? templateView->type : CV_8UC1;
        cv::Mat original(templateView->rows, templateView->cols, cvType, templateView->data, (size_t)templateView->step);
        cv::Mat gray;
        if (original.channels() == 3)
            cv::cvtColor(original, gray, cv::COLOR_BGR2GRAY);
        else
            gray = original.clone();

        CachedTemplate ct;
        ct.original = gray;
        cv::resize(gray, ct.pyramid4, cv::Size(gray.cols / 4, gray.rows / 4), 0, 0, cv::INTER_AREA);
        cv::resize(gray, ct.pyramid8, cv::Size(gray.cols / 8, gray.rows / 8), 0, 0, cv::INTER_AREA);

        {
            std::lock_guard<std::mutex> lk(s_templateMtx);
            s_templateCache[patternId] = ct;
        }

        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] RegisterTemplatePattern: id=%d size=%dx%d registered with pyramids.\n", patternId, gray.cols, gray.rows), SDK_LOGEVENT);

        return RESULT_SUCCESS;
    }
    catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::MatchTemplatePyramidFast(const CvMatView* srcView, int patternId, float minScore, int* outMatchedX, int* outMatchedY, float* outScore)
{
    if (!srcView || !srcView->data || srcView->cols <= 0 || srcView->rows <= 0 || !outMatchedX || !outMatchedY || !outScore)
        return RESULT_ERROR;

    CachedTemplate ct;
    {
        std::lock_guard<std::mutex> lk(s_templateMtx);
        auto it = s_templateCache.find(patternId);
        if (it == s_templateCache.end()) return RESULT_ERROR;
        ct = it->second;
    }

    try
    {
        int cvType = (srcView->type >= 0) ? srcView->type : CV_8UC1;
        cv::Mat srcFull(srcView->rows, srcView->cols, cvType, srcView->data, (size_t)srcView->step);
        cv::Mat srcGray = srcFull;
        if (srcFull.channels() == 3)
            cv::cvtColor(srcFull, srcGray, cv::COLOR_BGR2GRAY);

        // 1. 1/8 Coarse Search (~3ms)
        cv::Mat src8;
        cv::resize(srcGray, src8, cv::Size(srcGray.cols / 8, srcGray.rows / 8), 0, 0, cv::INTER_AREA);

        cv::Mat result8;
        cv::matchTemplate(src8, ct.pyramid8, result8, cv::TM_CCOEFF_NORMED);
        double minVal8 = 0, maxVal8 = 0;
        cv::Point minLoc8, maxLoc8;
        cv::minMaxLoc(result8, &minVal8, &maxVal8, &minLoc8, &maxLoc8);

        int coarseX = maxLoc8.x * 8;
        int coarseY = maxLoc8.y * 8;

        // 2. 1/1 Fine Search in local ROI (+/- 32 px around coarse position) (~5ms)
        int margin = 32;
        int roiX = (std::max)(0, coarseX - margin);
        int roiY = (std::max)(0, coarseY - margin);
        int roiW = (std::min)(srcGray.cols - roiX, ct.original.cols + margin * 2);
        int roiH = (std::min)(srcGray.rows - roiY, ct.original.rows + margin * 2);

        if (roiW < ct.original.cols || roiH < ct.original.rows)
        {
            *outMatchedX = coarseX;
            *outMatchedY = coarseY;
            *outScore = (float)maxVal8;
            return (*outScore >= minScore) ? RESULT_SUCCESS : RESULT_MATCH_LOW_SCORE;
        }

        cv::Mat fineRoi = srcGray(cv::Rect(roiX, roiY, roiW, roiH));
        cv::Mat result1;
        cv::matchTemplate(fineRoi, ct.original, result1, cv::TM_CCOEFF_NORMED);
        double minVal1 = 0, maxVal1 = 0;
        cv::Point minLoc1, maxLoc1;
        cv::minMaxLoc(result1, &minVal1, &maxVal1, &minLoc1, &maxLoc1);

        int finalX = roiX + maxLoc1.x;
        int finalY = roiY + maxLoc1.y;
        float finalScore = (float)maxVal1;

        *outMatchedX = finalX;
        *outMatchedY = finalY;
        *outScore = finalScore;

        if (m_EventLog)
            m_EventLog->Addlog(string_format("[OPENCV_C++ LIB] MatchTemplatePyramidFast: id=%d matched at (%d, %d), score=%.4f (min=%.2f)\n", patternId, finalX, finalY, finalScore, minScore), SDK_LOGEVENT);

        return (finalScore >= minScore) ? RESULT_SUCCESS : RESULT_MATCH_LOW_SCORE;
    }
    catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
    catch (...) { return RESULT_ERROR; }
}

API_RESULT OpenCVUtil::ExtractAlignedSubMat(const CvMatView* srcView, int startX, int startY, int cropWidth, int cropHeight, CvMatView* outView)
{
    if (!srcView || !srcView->data || !outView)
        return RESULT_ERROR;
    if (startX < 0 || startY < 0 || cropWidth <= 0 || cropHeight <= 0)
        return RESULT_ERROR;
    if (startX + cropWidth > srcView->cols || startY + cropHeight > srcView->rows)
        return RESULT_ERROR;

    int cvType = (srcView->type >= 0) ? srcView->type : CV_8UC1;
    size_t elemSize = (cvType == CV_8UC3) ? 3 : 1;

    // 0-Copy Sub-Matrix Pointer Calculation
    uint8_t* basePtr = (uint8_t*)srcView->data;
    uint8_t* subPtr = basePtr + ((size_t)startY * (size_t)srcView->step) + ((size_t)startX * elemSize);

    outView->data = (void*)subPtr;
    outView->cols = cropWidth;
    outView->rows = cropHeight;
    outView->step = srcView->step;
    outView->type = cvType;

    return RESULT_SUCCESS;
}

API_RESULT OpenCVUtil::ReleaseTemplatePattern(int patternId)
{
    std::lock_guard<std::mutex> lk(s_templateMtx);
    s_templateCache.erase(patternId);
    return RESULT_SUCCESS;
}
#pragma endregion
