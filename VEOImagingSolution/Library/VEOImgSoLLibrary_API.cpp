//#include "pch.h"
#include "VEOImgSoLLibrary_API.h"
#include "VEOImgSoLRingContext.h"
#include <memory>
#include <mutex>
#include <thread>

OpenCVUtil* _OpenCVLibHandle;
bool m_bInitialized = false;

enum class LibState { Uninitialized, Initializing, Initialized, Deinitializing };
static LibState             s_libState    = LibState::Uninitialized;
static std::recursive_mutex s_apiMtx;
VeoRingContext s_legacyCtx = {};
static std::thread::id      s_initThreadId = {};
static thread_local int     s_apiCallDepth = 0;  // per-thread API call depth

API_RESULT InitializeLibHandle(EventCallback* Pointer, void* pUserPointer)
{
	try
	{
		std::shared_ptr<OpenCVUtil> localHandle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState == LibState::Initializing)
			{
				if (std::this_thread::get_id() == s_initThreadId)
					return RESULT_SUCCESS;
				return RESULT_ERROR;
			}
			if (s_libState == LibState::Initialized)
				return RESULT_SUCCESS;
			if (s_libState == LibState::Deinitializing)
				return RESULT_ERROR;
			s_libState     = LibState::Initializing;
			s_initThreadId = std::this_thread::get_id();
			s_legacyCtx.m_spUtil     = std::make_shared<OpenCVUtil>(Pointer, pUserPointer);
			if (!s_legacyCtx.m_spUtil)
			{
				s_libState = LibState::Uninitialized;
				s_initThreadId = {};
				return RESULT_ERROR;
			}
			_OpenCVLibHandle = s_legacyCtx.m_spUtil.get();
			localHandle = s_legacyCtx.m_spUtil;
		}
		localHandle->InitializeLibHandle();
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			s_libState     = LibState::Initialized;
			m_bInitialized = true;
			s_initThreadId = {};
		}
		return RESULT_SUCCESS;
	}
	catch (...)
	{
		std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
		s_legacyCtx.m_spUtil.reset();
		_OpenCVLibHandle = nullptr;
		m_bInitialized   = false;
		s_libState       = LibState::Uninitialized;
		s_initThreadId   = {};
		return RESULT_ERROR;
	}
}

API_RESULT DeInitializeLibHandle()
{
	try
	{
		std::shared_ptr<OpenCVUtil> handleToDelete;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState == LibState::Uninitialized)
				return RESULT_SUCCESS;
			if (s_libState == LibState::Initializing || s_libState == LibState::Deinitializing)
				return RESULT_ERROR;
			// Initialized  transition to Deinitializing
			s_libState     = LibState::Deinitializing;
			m_bInitialized = false;
			handleToDelete = s_legacyCtx.m_spUtil;
			s_legacyCtx.m_spUtil.reset();
			_OpenCVLibHandle = nullptr;
		}

		// DEADLOCK GUARD: if the calling thread is currently inside an API wrapper
		// (s_apiCallDepth > 0), the quiesce spin would wait for itself  deadlock.
		// Restore state and reject the call.
		if (s_apiCallDepth > 0)
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState == LibState::Deinitializing)
			{
				s_legacyCtx.m_spUtil       = handleToDelete;
				_OpenCVLibHandle = s_legacyCtx.m_spUtil.get();
				m_bInitialized   = true;
				s_libState       = LibState::Initialized;
			}
			return RESULT_ERROR; // Re-entrant DeInit from callback: caller must not call DeInit from callback
		}

		// Quiesce: wait for all other threads' in-flight API calls to complete
		while (handleToDelete.use_count() > 1)
			std::this_thread::yield();

		handleToDelete->DeinitializeLibHandle();

		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState == LibState::Deinitializing)
				s_libState = LibState::Uninitialized;
		}
		return RESULT_SUCCESS;
	}
	catch (...)
	{
		std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
		if (s_libState == LibState::Deinitializing)
			s_libState = LibState::Uninitialized;
		return RESULT_ERROR;
	}
}

API_RESULT ImageLoad(const char* path, int index, CvMatView* out)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ImageLoadToIndex(path, index, out);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ImageLoadDepth(const char* path, int index, CvMatView* out)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ImageLoadDepthToIndex(path, index, out);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ImageSave(const char* path, int index)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ImageSaveFromIndex(path, index);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT AcquireLatestFrame(CvMatView* outView, int* outIndex)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		if (!outView || !outIndex) return RESULT_NULL_HANDLE;
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->AcquireLatestFrame(outView, outIndex);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ReleaseFrame(int index)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ReleaseFrame(index);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT AcquireLatestFrameEx(CvMatView* outView, int* outIndex, uint64_t* outToken, uint64_t* outGeneration)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		if (!outView || !outToken) return RESULT_NULL_HANDLE;
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->AcquireLatestFrameEx(outView, outIndex, outToken, outGeneration);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ReleaseFrameToken(uint64_t token)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ReleaseFrameToken(token);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT GetRingStats(RingStats* outStats)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		if (!outStats) return RESULT_NULL_HANDLE;
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetRingStats(outStats);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT GetRawPixelValue(int index, int x, int y, RawPixelValue* outValue)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		if (!outValue) return RESULT_NULL_HANDLE;
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetRawPixelValue(index, x, y, outValue);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT SetImageBuffer(int count, int width, int height, PixelFormat format)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->SetImageBuffer(count, width, height, format);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ClearImageBuffer()
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ClearImageBuffer();
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT SetImageBufferPtr(const CvMatView* src, int index)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->SetImageBufferPtr(src, index);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT RegisterImageBufferView(const CvMatView* src, int index)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->RegisterImageBufferView(src, index);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT GetImageBufferPtr(int index, CvMatView* out)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetImageBufferPtr(index, out);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT SetLatestIndex(int index)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->SetLatestIndex(index);
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT FindBoundaryY(const CvMatView* src, int* outBoundaryY)
{
	std::shared_ptr<OpenCVUtil> handle;
	{
		std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
		if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
			return RESULT_NOT_INITIALIZED;
		handle = s_legacyCtx.m_spUtil;
	}
	++s_apiCallDepth;
	struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
	return handle->FindBoundaryYLatest(*src,outBoundaryY);
}

API_RESULT Threshold(const CvMatView* srcView, const CvMatView* dstView, double thresh, double maxVal, int thresholdType, int* outUsedThresh)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		if (!srcView || !dstView) return RESULT_NULL_HANDLE;

		// outUsedThresh null (  threshold    )
		//    
		if (!srcView->data || srcView->rows <= 0 || srcView->cols <= 0) return RESULT_ERROR;
		if (!dstView->data || dstView->rows != srcView->rows || dstView->cols != srcView->cols) return RESULT_ERROR;

		// (OpenCVUtil) Threshold 
		// (  ThresholdView   Threshold     )
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->Threshold(*srcView, *dstView, thresh, maxVal, thresholdType, outUsedThresh);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}

API_RESULT LoadTiffStackToRingBuffers(const char* tifFilePath, int* outLoadedFrames)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->LoadTiffStackToRingBuffers(tifFilePath, outLoadedFrames);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT AFAlgorithmInitialize(int nCOEA, int nCOEB, int PeakThreshold, int nHaarFilterSize)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->AFAlgorithmInitialize(nCOEA, nCOEB, PeakThreshold, nHaarFilterSize);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}
API_RESULT VerticalSum(const unsigned char* u8i_pImageBuffer, unsigned short u12i_nWidth, unsigned short u12i_nHeight, unsigned char* u8i_pProfile)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->VerticalSum(u8i_pImageBuffer, u12i_nWidth, u12i_nHeight, u8i_pProfile);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}

API_RESULT UpScaleData(const unsigned char* u8i_pProfile, unsigned short u12i_nProfileSize, unsigned char* u8i_pUpScaledProfile)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->UpScaleData(u8i_pProfile, u12i_nProfileSize, u8i_pUpScaledProfile);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}

API_RESULT CalcHaarScore(const unsigned char* u8i_pProfile, unsigned short u15i_nProfileSize, unsigned short u12i_nHaarFilterSize, short* s16i_pHarrScore)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->CalcHaarScore(u8i_pProfile, u15i_nProfileSize, u12i_nHaarFilterSize, s16i_pHarrScore);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}

API_RESULT MaxHaarScorePosition(const short* s16i_pHarrScore, unsigned short u15i_nHaarScoreSize, unsigned short* s15i_nScorePosition)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->MaxHaarScorePosition(s16i_pHarrScore, u15i_nHaarScoreSize, s15i_nScorePosition);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}

API_RESULT GetDetectedPeakPosition(short s15i_nLinePos, unsigned short u12i_nCameraWidth, float* fPeakPosition)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetDetectedPeakPosition(s15i_nLinePos, u12i_nCameraWidth, fPeakPosition);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}
API_RESULT GetZDiffPosition(short s15i_nLinePos, int* nZDiffPos)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetZDiffPosition(s15i_nLinePos, nZDiffPos);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}
API_RESULT GetZRealPosition(short s15i_nLinePos, float* fZRealPos)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}

		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetZRealPosition(s15i_nLinePos, fZRealPos);
	}
	catch (...)
	{
		return RESULT_ERROR;
	}
}

API_RESULT CalculatePeakLevel(const CvMatView* src, int* outPeakLevel)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		if (!src || !outPeakLevel) return RESULT_NULL_HANDLE;
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->CalculatePeakLevel(*src, outPeakLevel);
	}
	catch (...) { return RESULT_ERROR; }
}



API_RESULT AssembleVisibleTiles(const int* frameIndices, int frameCount, int frameWidth, int frameHeight, int roiX, int roiY, int roiW, int roiH, CvMatView* outView)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->AssembleVisibleTiles(frameIndices, frameCount, frameWidth, frameHeight, roiX, roiY, roiW, roiH, outView);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT SaveScanAsBigTiff(const char* filepath, const int* frameIndices, int frameCount, int compression)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->SaveScanAsBigTiff(filepath, frameIndices, frameCount, compression);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ExportFramesToRawFile(const char* filepath, const int* frameIndices, int frameCount)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ExportFramesToRawFile(filepath, frameIndices, frameCount);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT EnableDiskFrameSource(const char* backingFilePath, int frameCount, int frameWidth, int frameHeight, PixelFormat format, int lruCapacity)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->EnableDiskFrameSource(backingFilePath, frameCount, frameWidth, frameHeight, format, lruCapacity);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT DisableDiskFrameSource()
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->DisableDiskFrameSource();
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT LoadAFLaserRawImageDataSet(const char* folderPath, const char* filePrefix, const char* fileExt,
                                      int startIndex, int width, int height, int* outFrameCount)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->LoadAFLaserRawImageDataSet(folderPath, filePrefix, fileExt, startIndex, width, height, outFrameCount);
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT LoadAFLaserVideoDataSet(const char* filepath, int* outFrameCount)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->LoadAFLaserVideoDataSet(filepath, outFrameCount);
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT GetAFLaserVideoFrame(int index, CvMatView* outView)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetAFLaserVideoFrame(index, outView);
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT ClearAFLaserVideoDataSet()
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ClearAFLaserVideoDataSet();
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT SetAFLaserSingleImage(const CvMatView* src)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->SetAFLaserSingleImage(src);
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT GetAFLaserSingleImage(CvMatView* outView)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->GetAFLaserSingleImage(outView);
	}
	catch (...) { return RESULT_ERROR; }
}
API_RESULT LoadAFLaserSingleImageFromFile(const char* filepath)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->LoadAFLaserSingleImageFromFile(filepath);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT BeginVideoDataSet(int expectedFrameCount, double captureFps)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->BeginVideoDataSet(expectedFrameCount, captureFps);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT EndVideoDataSet(int* outFrameCount)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->EndVideoDataSet(outFrameCount);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT AppendFrameToVideoDataSet(const CvMatView* src)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->AppendFrameToVideoDataSet(src);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ExportVideoDataSet(const char* filepath, double fps, const char* fourccCode, int* outWritten)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ExportVideoDataSet(filepath, fps, fourccCode, outWritten);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ConvertToBgr24(const CvMatView* src, CvMatView* dst)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ConvertToBgr24(src, dst);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT ConvertBayerToRgb24(const CvMatView* src, CvMatView* dst, PixelFormat bayerFormat)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->ConvertBayerToRgb24(src, dst, bayerFormat);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT DrawLineBgr24(CvMatView* view, int x1, int y1, int x2, int y2, unsigned char b, unsigned char g, unsigned char r, int thickness)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->DrawLineBgr24(view, x1, y1, x2, y2, b, g, r, thickness);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT DrawCrossMarkerBgr24(CvMatView* view, int x, int y, int size, unsigned char b, unsigned char g, unsigned char r, int thickness)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->DrawCrossMarkerBgr24(view, x, y, size, b, g, r, thickness);
	}
	catch (...) { return RESULT_ERROR; }
}

API_RESULT DrawCircleBgr24(CvMatView* view, int cx, int cy, int radius, unsigned char b, unsigned char g, unsigned char r, int thickness)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;
		return handle->DrawCircleBgr24(view, cx, cy, radius, b, g, r, thickness);
	}
	catch (...) { return RESULT_ERROR; }
}

#pragma region 9. Fast Template Matching & Wafer Alignment
extern "C" DLL_OPENCVLIB API_RESULT RegisterTemplatePattern(const CvMatView* templateView, int patternId)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;

		return handle->RegisterTemplatePattern(templateView, patternId);
	}
	catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
	catch (...)                   { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT MatchTemplatePyramidFast(const CvMatView* srcView, int patternId, float minScore, int* outMatchedX, int* outMatchedY, float* outScore)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;

		return handle->MatchTemplatePyramidFast(srcView, patternId, minScore, outMatchedX, outMatchedY, outScore);
	}
	catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
	catch (...)                   { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT ExtractAlignedSubMat(const CvMatView* srcView, int startX, int startY, int cropWidth, int cropHeight, CvMatView* outView)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;

		return handle->ExtractAlignedSubMat(srcView, startX, startY, cropWidth, cropHeight, outView);
	}
	catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
	catch (...)                   { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT ReleaseTemplatePattern(int patternId)
{
	try
	{
		std::shared_ptr<OpenCVUtil> handle;
		{
			std::lock_guard<std::recursive_mutex> lk(s_apiMtx);
			if (s_libState != LibState::Initialized || !s_legacyCtx.m_spUtil)
				return RESULT_NOT_INITIALIZED;
			handle = s_legacyCtx.m_spUtil;
		}
		++s_apiCallDepth;
		struct DepthGuard { ~DepthGuard() { --s_apiCallDepth; } } _guard;

		return handle->ReleaseTemplatePattern(patternId);
	}
	catch (const std::bad_alloc&) { return RESULT_OUT_OF_MEMORY; }
	catch (...)                   { return RESULT_ERROR; }
}
#pragma endregion
