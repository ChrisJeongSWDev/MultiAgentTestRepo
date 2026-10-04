#pragma once

#include <string>
#include <vector>
#include <stdio.h>
#include <stdlib.h>
#include <tchar.h>
#include <thread>
#include <stdint.h>

typedef struct CvMatView
{
	int32_t rows;
	int32_t cols;
	int32_t type;      // OpenCV type() e.g. CV_8UC1
	int32_t _reserved; // explicit x64 alignment padding (compiler would insert this anyway before the int64 below)
	int64_t step;      // bytes per row
	void* data;        // pixel buffer pointer
};
static_assert(sizeof(CvMatView) == 32, "CvMatView layout must stay 8-byte aligned on x64");

typedef struct RawPixelValue
{
	int32_t format;
	int32_t channels;
	uint32_t value0;
	uint32_t value1;
	uint32_t value2;
	uint32_t value3;
	uint32_t maxValue;
};
static_assert(sizeof(RawPixelValue) == 28, "RawPixelValue layout changed unexpectedly");

typedef struct EVENTDATA
{
	char* _MESSAGELOG;
	int	  _MESSAGESIZE;
	int	  _LOGINDEX;
	void* _pParent;
	EVENTDATA()
	{
		_MESSAGELOG = const_cast<char*>(""); // avoid deprecated string-literal-to-char* assignment warning
		_MESSAGESIZE = 0;
		_LOGINDEX = 0; // LOGINDEX == 0: Frame Grabber. 1: Motor, 2: AF Camera.
	}
};
static_assert(sizeof(EVENTDATA) == 24, "EVENTDATA layout must stay 8-byte aligned on x64");


enum API_RESULT
{
	RESULT_SUCCESS = 0x00,
	RESULT_ERROR = 0x01,
	RESULT_NOT_INITIALIZED = 0x02,
	RESULT_NULL_HANDLE = 0x03,
	RESULT_LOAD_FAIL = 0x04,
	RESULT_PEAK_LOW_THRESHOLD = 0x05,
	RESULT_OUT_OF_MEMORY = 0x06,
	RESULT_MATCH_LOW_SCORE = 0x07,
};


template<typename ... Args>
std::string string_format(const std::string& format, Args ... args)
{
	char input[256] = "";
	// snprintf (not sprintf): bounds the write to sizeof(input) so an over-length
	// formatted result (e.g. a long %s file path) truncates instead of overflowing the buffer.
	snprintf(input, sizeof(input), format.c_str(), args ...);
	std::string strData(input);
	return strData;
}

// Opaque per-camera ring context. Only ever forward-declared in public headers, so the ABI is a
// bare pointer and no consumer can depend on the layout. Defined in VEOImgSoLRingContext.h.
struct VEO_RING_CONTEXT__;
typedef struct VEO_RING_CONTEXT__* VEO_RING_CONTEXT;
