#pragma once

#include "VEOImgSoLGlobal.h"
#include "VEOImgSoLUtil.h"

#include <stdint.h>
#include <stddef.h>
#define DLL_OPENCVLIB __declspec(dllexport)

extern "C"
{
#pragma region 1. Library Lifecycle
	DLL_OPENCVLIB API_RESULT InitializeLibHandle(EventCallback* Pointer, void* pUserPointer);
	DLL_OPENCVLIB API_RESULT DeInitializeLibHandle();
#pragma endregion

#pragma region 2. Common Ring Buffer
	DLL_OPENCVLIB API_RESULT SetImageBuffer(int count, int width, int height, PixelFormat format);
	DLL_OPENCVLIB API_RESULT ClearImageBuffer();
	// index >= 0: explicit-slot publish (existing contract). index < 0 (e.g. -1): atomic Producer
	// Slot Arbitration (Phase B4 repair) -- the sole entry point for dynamic slot allocation.
	// Arbitrates a free slot (readers == 0 && !writing), copies src's pixels into it, and publishes
	// it in this ONE call; if every slot is currently held, applies a non-blocking DropNewest
	// policy (returns RESULT_LOAD_FAIL/BUSY immediately, never blocks waiting for a slot to free
	// up). There is no separate slot-reservation export in this DLL surface -- reserving a slot
	// without a matching publish in the same call would permanently deadlock every later write to
	// it, so that capability is intentionally not exposed.
	DLL_OPENCVLIB API_RESULT SetImageBufferPtr(const CvMatView* src, int index);
	DLL_OPENCVLIB API_RESULT RegisterImageBufferView(const CvMatView* src, int index);
	DLL_OPENCVLIB API_RESULT GetImageBufferPtr(int index, CvMatView* out);
	DLL_OPENCVLIB API_RESULT SetLatestIndex(int index);
	DLL_OPENCVLIB API_RESULT AcquireLatestFrame(CvMatView* outView, int* outIndex);
	DLL_OPENCVLIB API_RESULT ReleaseFrame(int index);
	// Additive token-based acquire/release for multiple concurrent independent acquirers.
	// AcquireLatestFrame/ReleaseFrame keep their existing signatures and semantics; these two are
	// a separate, exactly-matched ownership pair. outIndex and outGeneration may be NULL.
	// outGeneration is the slot's publish generation, so a consumer can tell whether the frame it
	// just acquired is the same one it already handled.
	DLL_OPENCVLIB API_RESULT AcquireLatestFrameEx(CvMatView* outView, int* outIndex, uint64_t* outToken, uint64_t* outGeneration);
	DLL_OPENCVLIB API_RESULT ReleaseFrameToken(uint64_t token);
	// Runtime observability snapshot: published/dropped frame counters, total acquire/release call
	// volume, and current active-reader concurrency. RingStats is defined in VEOImgSoLUtil.h (this
	// header already includes it above), not redeclared here -- redefining the same struct inside
	// this extern "C" block would be an ODR/duplicate-definition compile error.
	DLL_OPENCVLIB API_RESULT GetRingStats(RingStats* outStats);
	DLL_OPENCVLIB API_RESULT GetRawPixelValue(int index, int x, int y, RawPixelValue* outValue);
	
	//      ( )
	DLL_OPENCVLIB API_RESULT ImageLoad(const char* path, int index, CvMatView* out);
	DLL_OPENCVLIB API_RESULT ImageLoadDepth(const char* path, int index, CvMatView* out);
	DLL_OPENCVLIB API_RESULT ImageSave(const char* path, int index);
#pragma endregion

#pragma region 2b. Per-Camera Ring Context (multi-camera live view)
	// Additive, handle-parameterized mirror of the region-2 ring surface. Every region-1/2 export
	// above keeps its exact signature and behaviour; these never touch _OpenCVLibHandle/s_libState.
	// Scope is deliberately live-view only -- AF/dataset/MMF/QR stay global-only until a specific
	// per-camera need appears.
	DLL_OPENCVLIB API_RESULT CreateRingContext(EventCallback* pCallback, void* pUserPointer, VEO_RING_CONTEXT* outCtx);
	DLL_OPENCVLIB API_RESULT DestroyRingContext(VEO_RING_CONTEXT ctx);
	DLL_OPENCVLIB API_RESULT RingContextSetImageBuffer(VEO_RING_CONTEXT ctx, int count, int width, int height, PixelFormat format);
	DLL_OPENCVLIB API_RESULT RingContextClearImageBuffer(VEO_RING_CONTEXT ctx);
	DLL_OPENCVLIB API_RESULT RingContextSetImageBufferPtr(VEO_RING_CONTEXT ctx, const CvMatView* src, int index);
	DLL_OPENCVLIB API_RESULT RingContextRegisterImageBufferView(VEO_RING_CONTEXT ctx, const CvMatView* src, int index);
	DLL_OPENCVLIB API_RESULT RingContextSetLatestIndex(VEO_RING_CONTEXT ctx, int index);
	DLL_OPENCVLIB API_RESULT RingContextAcquireLatestFrameEx(VEO_RING_CONTEXT ctx, CvMatView* outView, int* outIndex, uint64_t* outToken, uint64_t* outGeneration);
	DLL_OPENCVLIB API_RESULT RingContextReleaseFrameToken(VEO_RING_CONTEXT ctx, uint64_t token);
	DLL_OPENCVLIB API_RESULT RingContextGetRingStats(VEO_RING_CONTEXT ctx, RingStats* outStats);
#pragma endregion

#pragma region 3. General Image Processing & Vision APIs
	DLL_OPENCVLIB API_RESULT FindBoundaryY(const CvMatView* src, int* outBoundaryY);
	DLL_OPENCVLIB API_RESULT Threshold(const CvMatView* srcView, const CvMatView* dstView, double thresh, double maxVal, int thresholdType, int* outUsedThresh);
	DLL_OPENCVLIB API_RESULT AssembleVisibleTiles(const int* frameIndices, int frameCount, int frameWidth, int frameHeight, int roiX, int roiY, int roiW, int roiH, CvMatView* outView);

	// 2GB+ BigTIFF    MMF/LRU    (Phase 1~3)
	DLL_OPENCVLIB API_RESULT SaveScanAsBigTiff(const char* filepath, const int* frameIndices, int frameCount, int compression);
	DLL_OPENCVLIB API_RESULT ExportFramesToRawFile(const char* filepath, const int* frameIndices, int frameCount);
	DLL_OPENCVLIB API_RESULT EnableDiskFrameSource(const char* backingFilePath, int frameCount, int frameWidth, int frameHeight, PixelFormat format, int lruCapacity);
	DLL_OPENCVLIB API_RESULT DisableDiskFrameSource();

#pragma endregion

#pragma region 4. AF Calculation Core
	DLL_OPENCVLIB API_RESULT AFAlgorithmInitialize(int nCOEA, int nCOEB, int PeakThreshold, int nHaarFilterSize);
	DLL_OPENCVLIB API_RESULT VerticalSum(const unsigned char* u8i_pImageBuffer, unsigned short u12i_nWidth, unsigned short u12i_nHeight, unsigned char* u8i_pProfile);
	DLL_OPENCVLIB API_RESULT UpScaleData(const unsigned char* u8i_pProfile, unsigned short u12i_nProfileSize, unsigned char* u8i_pUpScaledProfile);
	DLL_OPENCVLIB API_RESULT CalcHaarScore(const unsigned char* u8i_pProfile, unsigned short u15i_nProfileSize, unsigned short u12i_nHaarFilterSize, short* s16i_pHarrScore);
	DLL_OPENCVLIB API_RESULT MaxHaarScorePosition(const short* s16i_pHarrScore, unsigned short u15i_nHaarScoreSize, unsigned short* s15i_nScorePosition);
	DLL_OPENCVLIB API_RESULT GetDetectedPeakPosition(short s15i_nLinePos, unsigned short u12i_nCameraWidth, float* fPeakPosition);
	DLL_OPENCVLIB API_RESULT GetZRealPosition(short s15i_nLinePos, float* fZRealPos);
	// FPGA AF diff raw value (integer). ZRealPos = (value / 16384.0f) * 1000.0f.
	DLL_OPENCVLIB API_RESULT GetZDiffPosition(short s15i_nLinePos, int* nZDiffPos);
	DLL_OPENCVLIB API_RESULT CalculatePeakLevel(const CvMatView* src, int* outPeakLevel);
#pragma endregion

#pragma region 5. Isolated AF Buffer Management
	DLL_OPENCVLIB API_RESULT LoadAFLaserVideoDataSet(const char* filepath, int* outFrameCount);
	// RAW 프레임 폴더 임포트 ("<filePrefix><N><fileExt>", N = startIndex 부터 결번 전까지). 8bit Mono 전제.
	DLL_OPENCVLIB API_RESULT LoadAFLaserRawImageDataSet(const char* folderPath, const char* filePrefix, const char* fileExt,
	                                                    int startIndex, int width, int height, int* outFrameCount);
	DLL_OPENCVLIB API_RESULT GetAFLaserVideoFrame(int index, CvMatView* outView);
	DLL_OPENCVLIB API_RESULT ClearAFLaserVideoDataSet();
	DLL_OPENCVLIB API_RESULT SetAFLaserSingleImage(const CvMatView* src);
	DLL_OPENCVLIB API_RESULT GetAFLaserSingleImage(CvMatView* outView);
	DLL_OPENCVLIB API_RESULT LoadAFLaserSingleImageFromFile(const char* filepath);
	DLL_OPENCVLIB API_RESULT BeginVideoDataSet(int expectedFrameCount, double captureFps);
	DLL_OPENCVLIB API_RESULT EndVideoDataSet(int* outFrameCount);
	DLL_OPENCVLIB API_RESULT AppendFrameToVideoDataSet(const CvMatView* src);
	DLL_OPENCVLIB API_RESULT ExportVideoDataSet(const char* filepath, double fps, const char* fourccCode, int* outWritten);
#pragma endregion

#pragma region 6. BGR24 Display & Drawing APIs
	DLL_OPENCVLIB API_RESULT ConvertToBgr24(const CvMatView* src, CvMatView* dst);
	DLL_OPENCVLIB API_RESULT ConvertBayerToRgb24(const CvMatView* src, CvMatView* dst, PixelFormat bayerFormat);
	DLL_OPENCVLIB API_RESULT DrawLineBgr24(CvMatView* view, int x1, int y1, int x2, int y2, unsigned char b, unsigned char g, unsigned char r, int thickness);
	DLL_OPENCVLIB API_RESULT DrawCrossMarkerBgr24(CvMatView* view, int x, int y, int size, unsigned char b, unsigned char g, unsigned char r, int thickness);
	DLL_OPENCVLIB API_RESULT DrawCircleBgr24(CvMatView* view, int cx, int cy, int radius, unsigned char b, unsigned char g, unsigned char r, int thickness);
#pragma endregion

#pragma region 8. Multi-page TIFF Stack Loader API
	DLL_OPENCVLIB API_RESULT LoadTiffStackToRingBuffers(const char* tifFilePath, int* outLoadedFrames);
#pragma endregion

#pragma region 9. Fast Template Matching & Wafer Alignment
	DLL_OPENCVLIB API_RESULT RegisterTemplatePattern(const CvMatView* templateView, int patternId);
	DLL_OPENCVLIB API_RESULT MatchTemplatePyramidFast(const CvMatView* srcView, int patternId, float minScore, int* outMatchedX, int* outMatchedY, float* outScore);
	DLL_OPENCVLIB API_RESULT ExtractAlignedSubMat(const CvMatView* srcView, int startX, int startY, int cropWidth, int cropHeight, CvMatView* outView);
	DLL_OPENCVLIB API_RESULT ReleaseTemplatePattern(int patternId);
#pragma endregion
}
