#pragma once

#include <opencv2/opencv.hpp>
#include <opencv2/highgui.hpp>
#include <iostream>
#include <atlstr.h>
#include <string>
#include <vector>
#include <queue>
#include <list>
#include <unordered_map>
#include <mutex>
#include <condition_variable>
#include <thread>
#include <atomic>
#include <cstdint>
#include <windows.h>
#include "VwSolutionSDKEventLog.h"

using namespace cv;
using namespace std;

// AF ALGORITHM CONST VALUES

#define HAAR_FILTER_SIZE_FIXED	6			//, MIN 2 : MAX 63
#define SUB_PIXEL_SCALE_BIT		4			//
#define MAX_LINE_NUM			4			//
#define LINE_NUM_BIT			0			//
#define COE_DET_BIT				13			//
#define PEAKTHRESHOLD			20			//
#define OUTPUT_MAX				16383

enum class PixelFormat
{
    // Mono
    Mono8,
    Mono10,
    Mono12,

    // RGB
    RGB24,     // 8bit 3
    RGB32,     // 8bit 4

    // Bayer 8bit
    BayerRG8,
    BayerBG8,
    BayerGB8,
    BayerGR8,

    // Bayer 10/12bit ( 16bit   )
    BayerRG10,
    BayerRG12,
    BayerBG10,
    BayerBG12,
    BayerGB10,
    BayerGB12,
    BayerGR10,
    BayerGR12,
};

// ============================================================================
// Frame Source Abstraction (Phase 2/3)
// AssembleVisibleTiles RAM    ,   
// "RAM "  " MMF + LRU "      .
// ============================================================================
class IFrameSource
{
public:
    virtual ~IFrameSource() {}
    // ringIndex   .      Mat.
    // Unpinned view: the reference is only valid until the next GetFrame/AcquireFrame call on this
    // SAME source (from ANY thread) that could evict/replace ringIndex. Safe for a single-threaded,
    // short-lived lookup only. Any caller that may run concurrently with other frame-source traffic
    // -- e.g. Build3DReconImage's ReconThreadPool workers, which run after m_ringMtx is released --
    // MUST use AcquireFrame/ReleaseFrame below instead (Codex Round 3 repair: "make GetFrame either
    // pinned/owned or otherwise lifetime-safe").
    virtual const cv::Mat& GetFrame(int ringIndex) = 0;
    virtual size_t Capacity() const = 0;

    // Thread-safe pinned access, uniform across every IFrameSource implementation (promoted here
    // from an MmfFrameSource-only extension so a caller holding a single frozen IFrameSource*
    // -- e.g. one snapshotted once under m_ringMtx at the start of a Build3DReconImage batch, see
    // OpenCVUtil::SnapshotActiveFrameSourceLocked -- can pin frames through ONE polymorphic call
    // without branching on the concrete source type. That branch-and-separately-read pattern (check
    // "is this MMF?" then separately dereference the MMF pointer) is exactly the torn/unsynchronized
    // read Codex Round 3 flagged when done against live, mutable OpenCVUtil members from a pool
    // worker thread instead of a single frozen local pointer.
    // Maps/locates ringIndex if needed, pins it against eviction/teardown for the duration of use,
    // and copies its cv::Mat header into *outFrame. Returns false (outFrame left untouched) if
    // ringIndex is out of range or outFrame is null. Every call that returns true MUST be paired
    // with exactly one ReleaseFrame(ringIndex), or that slot stays pinned indefinitely.
    //   - RamFrameSource: the pin is a no-op beyond the bounds-checked copy -- RAM ring slots are
    //     retired only via OpenCVUtil's own m_ringSlotState reader-count mechanism, never via this
    //     interface, so there is nothing here to protect against.
    //   - MmfFrameSource: the pin blocks LRU eviction of ringIndex's mapping AND blocks Close() from
    //     unmapping it until ReleaseFrame is called -- see MmfFrameSource::Close() below.
    virtual bool AcquireFrame(int ringIndex, cv::Mat* outFrame) = 0;
    // Unpins ringIndex previously pinned by AcquireFrame. A ringIndex with no outstanding pin is
    // ignored.
    virtual void ReleaseFrame(int ringIndex) = 0;
};

//   : RAM  (m_OpenCVLibRingBuffer)    .
class RamFrameSource : public IFrameSource
{
public:
    explicit RamFrameSource(std::vector<cv::Mat>& ringBuffer) : m_ring(ringBuffer) {}
    const cv::Mat& GetFrame(int ringIndex) override
    {
        static const cv::Mat s_empty;
        if (ringIndex < 0 || ringIndex >= (int)m_ring.size()) return s_empty;
        return m_ring[ringIndex];
    }
    size_t Capacity() const override { return m_ring.size(); }

    // See IFrameSource::AcquireFrame. RAM slots are protected by OpenCVUtil's own
    // m_ringSlotState reader-count pinning, established by the caller before this is ever reached,
    // so there is no separate eviction/close mechanism here to guard against -- this just mirrors
    // GetFrame's bounds-checked copy under the uniform pinned-access interface.
    bool AcquireFrame(int ringIndex, cv::Mat* outFrame) override
    {
        if (!outFrame || ringIndex < 0 || ringIndex >= (int)m_ring.size()) return false;
        *outFrame = m_ring[ringIndex];
        return true;
    }
    void ReleaseFrame(int /*ringIndex*/) override { /* no-op: see AcquireFrame */ }
private:
    std::vector<cv::Mat>& m_ring;
};

//     RAW   MMF(Memory-Mapped File) ,
//   ( 32~64) LRU      .
//     (MapViewOfFile) , LRU 
//  UnmapViewOfFile   (working set) .
class MmfFrameSource : public IFrameSource
{
public:
    MmfFrameSource();
    virtual ~MmfFrameSource();

    // backingFilePath: frameCount * frameWidth * frameHeight * elemSize(cvType)
    //                    RAW  ( 0..N-1   ).
    // Calls Close() first (see below) -- if another instance's batch still holds outstanding
    // AcquireFrame pins on this object, Open() blocks until that batch fully drains before tearing
    // down the old mapping and establishing the new one (Codex Round 3 repair: "prevent close/
    // reopen until all MMF pins drain"), so a reopen can never invalidate frames a live batch is
    // still reading.
    bool Open(const std::wstring& backingFilePath, int frameCount, int frameWidth, int frameHeight, int cvType, size_t lruCapacity);
    // Blocking drain-then-close (Codex Round 3 repair: "prevent close/reopen until all MMF pins
    // drain"). Under m_mtx, first sets m_closing = true so AcquireFrame stops granting any NEW pin
    // (it returns false while a close is in progress/draining), then blocks on m_pinDrainCv until
    // every CachedFrame::pinCount in m_cachedViews has been released back to zero by its matching
    // ReleaseFrame call(s), and only THEN unmaps every cached view and closes the file/mapping
    // handles. Because Close() does not return until fully drained and unmapped, both destruction
    // and Open()'s internal Close() (a reopen) are guaranteed to never race a pinned mapping still
    // in use by a frozen-source batch (see OpenCVUtil::SnapshotActiveFrameSourceLocked). Idempotent:
    // safe to call when nothing is open or nothing is pinned (returns immediately).
    void Close();

    // IFrameSource contract. Internally locked (m_mtx) so concurrent calls never race the LRU/
    // mapping structures themselves, but the returned reference is only an unpinned view: if
    // another thread's GetFrame/AcquireFrame call evicts ringIndex under LRU pressure while the
    // first caller is still reading it, that reference dangles (UnmapViewOfFile'd out from under
    // it). Fine for a single-threaded, short-lived lookup; NOT safe to retain across any call that
    // could run on another thread (e.g. the Phase 1 ReconThreadPool workers) -- those callers MUST
    // use AcquireFrame/ReleaseFrame instead, which pin the mapping for the duration of use.
    const cv::Mat& GetFrame(int ringIndex) override;
    size_t Capacity() const override { return (size_t)m_frameCount; }

    // Thread-safe pinned access (Phase 1: closes the GetFrame use-after-unmap race under concurrent
    // recon workers). Maps ringIndex if not already cached, increments its pin count, and copies
    // its cv::Mat header into *outFrame -- while any pin is outstanding on a slot, EvictIfNeeded
    // skips it, so the backing mapping cannot be UnmapViewOfFile'd no matter how many other threads
    // call GetFrame/AcquireFrame/EvictIfNeeded concurrently. Returns false (outFrame left untouched)
    // if ringIndex is out of range, the mapping fails, or a Close() is currently in progress/
    // draining (m_closing) -- the last case lets Close()'s pin count converge to zero instead of
    // racing a caller that keeps acquiring new pins forever. Every AcquireFrame that returns true
    // MUST be paired with exactly one ReleaseFrame(ringIndex), or that slot stays pinned (never
    // evicted, and Close() blocks forever) for the remaining lifetime of this MmfFrameSource.
    bool AcquireFrame(int ringIndex, cv::Mat* outFrame) override;
    // Unpins ringIndex previously pinned by AcquireFrame. Once its pin count reaches zero the slot
    // becomes eligible for LRU eviction again, and m_pinDrainCv is notified so a Close() blocked in
    // its drain wait can re-check whether every pin has now been released. A ringIndex with no
    // outstanding pin is ignored.
    void ReleaseFrame(int ringIndex) override;

private:
    struct CachedFrame
    {
        cv::Mat view;         // Non-owning view wrapping mapped memory
        void*   mappedBase;   // Base pointer for UnmapViewOfFile
        int     pinCount = 0; // Outstanding AcquireFrame pins (guarded by m_mtx); EvictIfNeeded
                               // must skip any entry with pinCount > 0, and Close() must not unmap
                               // it until this reaches 0.
    };

    // Shared lookup/mapping path for GetFrame and AcquireFrame: maps ringIndex on first access,
    // refreshes its LRU recency, and returns the cached entry (nullptr if ringIndex is out of range
    // or mapping fails). Must be called with m_mtx already held.
    void CloseLocked(std::unique_lock<std::mutex>& lk);
    CachedFrame* GetOrMapLocked(int ringIndex);
    void Touch(int ringIndex);
    void EvictIfNeeded();

    HANDLE   m_hFile;
    HANDLE   m_hMapping;

    int      m_frameCount;
    int      m_frameWidth;
    int      m_frameHeight;
    int      m_cvType;
    size_t   m_frameBytes;
    size_t   m_lruCapacity;

    std::list<int>                                     m_lruOrder;   // front = most recently used
    std::unordered_map<int, std::list<int>::iterator>  m_lruLookup;
    std::unordered_map<int, CachedFrame>               m_cachedViews;
    std::mutex m_mtx;

    // Close()/pin-drain coordination (Codex Round 3 repair: "prevent close/reopen until all MMF
    // pins drain"). Both guarded by m_mtx. m_closing is set for the duration of a Close() call so
    // AcquireFrame refuses to grant any new pin while draining (guaranteeing the wait below
    // eventually converges); m_pinDrainCv is notified from ReleaseFrame every time a pin count
    // reaches zero, and waited on by Close() until no CachedFrame in m_cachedViews has pinCount > 0.
    bool                     m_closing = false;
    std::condition_variable  m_pinDrainCv;
};

// ============================================================================
// Ring buffer runtime observability (Phase B2). Plain POD (only uint64_t/int members) so it stays
// safe to hand across the C ABI in VEOImgSoLLibrary_API.h, mirroring CvMatView/RawPixelValue's
// layout discipline in VEOImgSoLGlobal.h. Defined once here (not re-declared inside API.h's
// extern "C" block) since VEOImgSoLLibrary_API.h already #includes this header -- a second
// definition there would be an ODR/duplicate-definition compile error, not just redundant text.
// ============================================================================
struct RingStats
{
    uint64_t publishedFrames;       // Total frames successfully published to ring
    uint64_t droppedFramesBusy;     // Frames dropped due to slot BUSY (readers holding slot)
    uint64_t droppedFramesNotInit;  // Frames dropped due to uninitialized ring buffer
    uint64_t totalAcquireCalls;     // Total AcquireLatestFrame / AcquireLatestFrameEx calls
    uint64_t totalReleaseCalls;     // Total ReleaseFrame / ReleaseFrameToken calls
    int activeReaders;              // Current active readers across all slots
};
static_assert(sizeof(RingStats) == 48, "RingStats layout must stay 8-byte aligned on x64");

class OpenCVUtil
{
public:
	OpenCVUtil(EventCallback* Pointer, void* pUserPointer);
	virtual ~OpenCVUtil(void);

#pragma region 1. Library Lifecycle
    void InitializeLibHandle();
    void DeinitializeLibHandle();
#pragma endregion

#pragma region 2. Mat Conversions
    Mat ImageLoad(const std::string& path);
    Mat SetImagePointer(int width, int height, void* pBuffer, PixelFormat pixelFormat);
	Mat SetImagePointer(int width, int height, void* pBuffer, PixelFormat pixelFormat, size_t strideBytes);
#pragma endregion

#pragma region 3. General Image Processing & Vision APIs
    API_RESULT Threshold(const CvMatView& srcView, const CvMatView& dstView, double thresh, double maxVal, int thresholdType, int* outUsedThresh);
    API_RESULT FindBoundaryYLatest(const CvMatView& src, int* outBoundaryY);
#pragma endregion

#pragma region 4. Common Ring Buffer Operations
    API_RESULT SetImageBuffer(int count, int width, int height, PixelFormat format);
    API_RESULT ClearImageBuffer();
    //  deep copy. index >= 0: explicit-slot publish (existing contract, unchanged).
    // index < 0 (e.g. -1): atomic Producer Slot Arbitration (Phase B4 repair) -- the SOLE entry
    // point for dynamic slot allocation. Under m_ringMtx, cyclically scans slots starting just
    // after m_nLastWrittenIndex for one with readers == 0 && !writing; on success claims it
    // (writing = true), copies src's pixels into it, and publishes via EndSlotWrite in this SAME
    // call -- arbitrate + copy + publish are one atomic operation, so no caller can ever observe or
    // hold a reserved-but-unpublished slot. If every slot is currently held (all readers > 0 or
    // writing), applies a non-blocking DropNewest policy: bumps the BUSY drop counter and returns
    // RESULT_LOAD_FAIL immediately WITHOUT waiting on m_ringCv, so a stalled consumer can never
    // stall the producer. There is deliberately no standalone slot-reservation export anywhere in
    // this DLL's surface -- a prior round's BeginWriteSlot() (reserve now, publish later) was
    // removed per Codex Review, since any caller that reserved a slot without publishing it would
    // permanently deadlock every future write to that slot.
    API_RESULT SetImageBufferPtr(const CvMatView* src, int index);
    // (non-owning)  : SetImageBufferPtr ()   copyTo  
    // src->data   cv::Mat   ( 0).   
    //     (lifetime)  (caller)  .
    //   / use-after-free/tearing  deep copy 
    //         src     .
    API_RESULT RegisterImageBufferView(const CvMatView* src, int index); //  (0,   )
    API_RESULT GetImageBufferPtr(int index, CvMatView* out);           // (0)
    API_RESULT SetLatestIndex(int index);                              //   index  
    // Legacy acquire/release pair. The slot guard is a per-slot reference count, so several
    // independent consumers may hold the same slot at once; every AcquireLatestFrame must be
    // matched by exactly one ReleaseFrame(index) or the slot stays write-blocked.
    API_RESULT AcquireLatestFrame(CvMatView* out, int* index);
    API_RESULT ReleaseFrame(int index);
    // Additive token-based acquire for multiple concurrent independent acquirers
    // (e.g. a 60Hz UI thread and a 20Hz image-processing thread). Ownership is identified by an
    // opaque non-zero token instead of the slot index, so one consumer's release can never drop
    // another consumer's guard, and a duplicated release is rejected instead of corrupting the
    // reference count. outIndex / outGeneration are optional (may be nullptr); outGeneration
    // returns the slot's publish generation, which lets a consumer detect that the latest frame
    // is the same one it already processed.
    API_RESULT AcquireLatestFrameEx(CvMatView* out, int* outIndex, uint64_t* outToken, uint64_t* outGeneration);
    API_RESULT ReleaseFrameToken(uint64_t token);
    // Runtime observability snapshot: published/dropped frame counters, total acquire/release call
    // volume, and current active-reader concurrency. Safe to call from any thread at any time.
    API_RESULT GetRingStats(RingStats* outStats);
    API_RESULT GetRawPixelValue(int index, int x, int y, RawPixelValue* outValue);
    API_RESULT ImageLoadToIndex(const char* path, int index, CvMatView* out);
    API_RESULT ImageLoadDepthToIndex(const char* path, int index, CvMatView* out);
    API_RESULT ImageSaveFromIndex(const char* path, int index);
    API_RESULT LoadTiffStackToRingBuffers(const char* tifFilePath, int* outLoadedFrames);
    API_RESULT AssembleVisibleTiles(const int* frameIndices, int frameCount,
                                    int frameWidth, int frameHeight,
                                    int roiX, int roiY, int roiW, int roiH,
                                    CvMatView* outView);

    // ---- 2GB+ BigTIFF    MMF/LRU    (Phase 1~3) ----
    // compression: 0=LZW(), 1=Deflate(Adobe), 2=
    API_RESULT SaveScanAsBigTiff(const char* filepath, const int* frameIndices, int frameCount, int compression);
    // MMF    RAW     .
    API_RESULT ExportFramesToRawFile(const char* filepath, const int* frameIndices, int frameCount);
    // AssembleVisibleTiles  RAM   MMF+LRU    .
    // Swaps m_frameSource under m_ringMtx only -- it never reaches into a frozen batch's already-
    // snapshotted IFrameSource* (see SnapshotActiveFrameSourceLocked below), and if the underlying
    // MmfFrameSource object is reused across calls, its Open() blocks until any prior batch's pins
    // on it have fully drained (Codex Round 3 repair; see MmfFrameSource::Open/Close).
    API_RESULT EnableDiskFrameSource(const char* backingFilePath, int frameCount, int frameWidth, int frameHeight, PixelFormat format, int lruCapacity);
    // RAM   .
    // Calls MmfFrameSource::Close(), which now blocks until every outstanding AcquireFrame pin on
    // it has been released (Codex Round 3 repair: "prevent close/reopen until all MMF pins drain"),
    // so this can never unmap a frame a concurrent, already-frozen Build3DReconImage batch is still
    // reading -- that batch simply keeps running against the pinned mapping until it releases it.
    API_RESULT DisableDiskFrameSource();
#pragma endregion

#pragma region 5. Isolated AF Buffer Operations
    API_RESULT LoadAFLaserVideoDataSet(const char* filepath, int* outFrameCount);
    // RAW 프레임 폴더 임포트. folderPath 안의 "<filePrefix><N><fileExt>" 를 N=startIndex 부터,
    // 1씩 증가시키며 존재하는 동안 읽어 m_afVideoDataSet 을 채운다(첫 결번에서 정상 종료).
    // 8bit Mono(CV_8UC1) 무헤더 덤프 전제이며, 각 파일 크기가 width*height 와 다르면 중단한다.
    API_RESULT LoadAFLaserRawImageDataSet(const char* folderPath, const char* filePrefix, const char* fileExt,
                                          int startIndex, int width, int height, int* outFrameCount);
    API_RESULT GetAFLaserVideoFrame(int index, CvMatView* outView);
    API_RESULT ClearAFLaserVideoDataSet();
    API_RESULT SetAFLaserSingleImage(const CvMatView* src);
    API_RESULT GetAFLaserSingleImage(CvMatView* outView);
    API_RESULT LoadAFLaserSingleImageFromFile(const char* filepath);
    API_RESULT BeginVideoDataSet(int expectedFrameCount, double captureFps);
    API_RESULT EndVideoDataSet(int* outFrameCount);
    API_RESULT AppendFrameToVideoDataSet(const CvMatView* src);
    API_RESULT ExportVideoDataSet(const char* filepath, double fps, const char* fourccCode, int* outWritten);
#pragma endregion

#pragma region 6. AF Algorithm Core
    API_RESULT AFAlgorithmInitialize(int nCOEA, int nCOEB, int PeakThreshold, int nHaarFilterSize);
    API_RESULT VerticalSum(const unsigned char* u8i_pImageBuffer, unsigned short u12i_nWidth, unsigned short u12i_nHeight, unsigned char* u8i_pProfile);
    API_RESULT UpScaleData(const unsigned char* u8i_pProfile, unsigned short u12i_nProfileSize, unsigned char* u8i_pUpScaledProfile);
    API_RESULT CalcHaarScore(const unsigned char* u8i_pProfile, unsigned short u15i_nProfileSize, unsigned short u12i_nHaarFilterSize, short* s16i_pHarrScore);
    API_RESULT MaxHaarScorePosition(const short* s16i_pHarrScore, unsigned short u15i_nHaarScoreSize, unsigned short* s15i_nScorePosition);
    API_RESULT GetDetectedPeakPosition(short s15i_nLinePos, unsigned short u12i_nCameraWidth, float* fPeakPosition);
    API_RESULT GetZRealPosition(short s15i_nLinePos, float* fZRealPos);
    // FPGA AF diff raw value. ZRealPos is derived from it as (value / 16384.0f) * 1000.0f,
    // so this exposes the integer the FPGA itself outputs, before the mm conversion.
    API_RESULT GetZDiffPosition(short s15i_nLinePos, int* nZDiffPos);
    API_RESULT CalculatePeakLevel(const CvMatView& src, int* outPeakLevel);
#pragma endregion

#pragma region 7. BGR24 Display & Drawing APIs
    API_RESULT ConvertToBgr24(const CvMatView* src, CvMatView* dst);
    API_RESULT ConvertBayerToRgb24(const CvMatView* src, CvMatView* dst, PixelFormat bayerFormat);
    API_RESULT DrawLineBgr24(CvMatView* view, int x1, int y1, int x2, int y2, unsigned char b, unsigned char g, unsigned char r, int thickness);
    API_RESULT DrawCrossMarkerBgr24(CvMatView* view, int x, int y, int size, unsigned char b, unsigned char g, unsigned char r, int thickness);
    API_RESULT DrawCircleBgr24(CvMatView* view, int cx, int cy, int radius, unsigned char b, unsigned char g, unsigned char r, int thickness);
#pragma endregion

#pragma region 9. Fast Template Matching & Wafer Alignment
    API_RESULT RegisterTemplatePattern(const CvMatView* templateView, int patternId);
    API_RESULT MatchTemplatePyramidFast(const CvMatView* srcView, int patternId, float minScore, int* outMatchedX, int* outMatchedY, float* outScore);
    API_RESULT ExtractAlignedSubMat(const CvMatView* srcView, int startX, int startY, int cropWidth, int cropHeight, CvMatView* outView);
    API_RESULT ReleaseTemplatePattern(int patternId);
#pragma endregion

private:
#pragma region Private Utility & Helper Methods
    VwSolutionSDKEventLog* m_EventLog;
    int PixelFormatToCvType(PixelFormat pixelFormat);
    int PixelFormatToBayerCode(PixelFormat pixelFormat, bool toRgbOrder);
    int FindBoundaryYCore(const cv::Mat& srcBgrOrGray);
    short MaxHaarScorePosition(short* s16i_pHarrScore, unsigned short u15i_nHaarScoreSize);
    int GetZPos(short s15i_nLinePos);
    float GetLineRealpixelPos(short s15i_nLinePos, unsigned short u12i_nCameraWidth);
    float GetZRealPos(short s15i_nLinePos);
    API_RESULT ImageLoadToIndexWithFlags(const char* path, int index, CvMatView* out, int imreadFlags);
    API_RESULT AcquireLatestFrameCore(CvMatView* out, int* outIndex, uint64_t* outToken, uint64_t* outGeneration);

    // ---- Ring slot write ownership (splits the global state lock from the pixel copy) ----
    // BeginSlotWriteCore takes exclusive write ownership of a slot: it waits until any in-flight
    // out-of-lock copy on that slot has finished, rejects the slot while readers hold it (BUSY),
    // then marks it as being written. Every RESULT_SUCCESS must be paired with EndSlotWrite so
    // waiters are released even when the copy throws. All *Core/*Locked helpers below require
    // m_ringMtx to be held on entry and still hold it on return.
    API_RESULT BeginSlotWriteCore(std::unique_lock<std::mutex>& lk, int index);
    void       EndSlotWrite(int index, bool published);   // takes m_ringMtx itself
    void       WaitForSlotWriteLocked(std::unique_lock<std::mutex>& lk, int index);
    void       WaitForAllSlotWritesLocked(std::unique_lock<std::mutex>& lk);
    void       ResetSlotStateLocked(size_t count);
#pragma endregion

#pragma region Private State Members: Common Ring Buffer
    // Per-slot reader guard. Replaces the former single scalar m_nReadingIndex, which could only
    // ever record ONE in-use slot: with two independent acquirers (UI 60Hz + image processing
    // 20Hz) the second Acquire overwrote the first one's guard, and the first Release cleared the
    // guard still owned by the other consumer, letting a writer tear a frame being displayed.
    struct RingSlotState
    {
        int      readers      = 0;      // outstanding Acquire count (legacy + token paths)
        int      legacyReaders = 0;     // subset released through ReleaseFrame(index)
        bool     writing      = false;  // an out-of-lock pixel copy is in flight on this slot
        uint64_t generation   = 0;      // set to m_nNextFrameId at publish time (Phase B2: exact
                                         // monotonic frame id, not an independent per-slot counter).
                                         // Assigned from both EndSlotWrite and SetLatestIndex, since
                                         // both are publish paths.
    };

    std::vector<cv::Mat> m_OpenCVLibRingBuffer;   //    index   ()
    int                  m_nLastWrittenIndex = -1; //    (AcquireLatest )
    uint64_t             m_nNextFrameId      = 1;
    std::mutex           m_ringMtx;
    // Invariant: m_ringSlotState.size() == m_OpenCVLibRingBuffer.size(); both are resized together.
    std::vector<RingSlotState>        m_ringSlotState;
    std::condition_variable           m_ringCv;          // signalled when a slot write or read ends
    uint64_t                          m_nNextReadToken = 1;  // monotonic; 0 is never a valid token
    std::unordered_map<uint64_t, int> m_readTokens;      // live token -> slot index

    // Ring observability counters (Phase B2). Atomics so hot-path increments never need m_ringMtx
    // just for bookkeeping; m_activeReaderCount is the one exception, since it must stay numerically
    // in sync with every RingSlotState::readers mutation, all of which already happen under
    // m_ringMtx in AcquireLatestFrameCore / ReleaseFrame / ReleaseFrameToken.
    std::atomic<uint64_t>             m_statPublishedFrames{ 0 };
    std::atomic<uint64_t>             m_statDroppedFramesBusy{ 0 };
    std::atomic<uint64_t>             m_statDroppedFramesNotInit{ 0 };
    std::atomic<uint64_t>             m_statTotalAcquireCalls{ 0 };
    std::atomic<uint64_t>             m_statTotalReleaseCalls{ 0 };
    int                               m_activeReaderCount = 0;   // guarded by m_ringMtx

    cv::Mat              m_assembleBufferMat;      //     Mat

    // AssembleVisibleTiles     (: RAM ).
    IFrameSource*        m_frameSource      = nullptr;
    RamFrameSource*      m_ramFrameSource   = nullptr;
    MmfFrameSource*      m_mmfFrameSource   = nullptr;
    bool                 m_bUsingDiskFrameSource = false;
#pragma endregion

#pragma region Private State Members: Isolated AF Buffers
    std::vector<cv::Mat>    m_afVideoDataSet;
    cv::Mat                 m_afSingleAnalysisMat;
    std::mutex              m_afMtx;
    double                  m_afCaptureFps    = 30.0;
#pragma endregion

#pragma region Private State Members: AF Parameters
    unsigned short			u12i_m_nHaarFilterSize;
    unsigned short			u12i_m_nMaskSize;
    int						s17i_m_nCoeA;
    short					s16i_m_nCoeB;
    unsigned char			u8i_m_PeakThreshold;
    bool					m_bValidLaser;

    unsigned char			u8i_m_nNumLine;
    unsigned char			u8i_m_nSelLineIdx;
    short					s16i_m_nMaskX1[MAX_LINE_NUM];
    short					s16i_m_nMaskX2[MAX_LINE_NUM];
    short					s15i_m_nLinePos[MAX_LINE_NUM];
#pragma endregion

    // No new data members below this point (Phase 1: Threading & Zero-Allocation Refactoring).
    // SetReconThreadCount/SetReconCancellation/GetReconProgress (region 8, above) are backed by
    // file-static state in VEOImgSoLUtil.cpp, not by OpenCVUtil members -- see the comment on those
    // declarations. This keeps sizeof(OpenCVUtil) and every member offset above byte-for-byte
    // identical to the pre-Phase-1 layout (ABI stability).
};
