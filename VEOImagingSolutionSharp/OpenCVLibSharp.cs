using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VEOImagingSolutionSharp
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CvMatView
    {
        public int rows;        // int32_t
        public int cols;        // int32_t
        public int type;        // int32_t  (cv::Mat::type())
        public long step;       // int64_t  (bytes per row)
        public IntPtr data;     // void*
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawPixelValue
    {
        public int format;      // int32_t
        public int channels;    // int32_t
        public uint value0;     // uint32_t
        public uint value1;     // uint32_t
        public uint value2;     // uint32_t
        public uint value3;     // uint32_t
        public uint maxValue;   // uint32_t
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EVENTDATA
    {
        public IntPtr _MESSAGELOG;
        public int _MESSAGESIZE;
        public int _LOGINDEX;
        public IntPtr pParent;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct RingStats
    {
        public ulong publishedFrames;       // uint64_t
        public ulong droppedFramesBusy;     // uint64_t
        public ulong droppedFramesNotInit;  // uint64_t
        public ulong totalAcquireCalls;     // uint64_t
        public ulong totalReleaseCalls;     // uint64_t
        public int activeReaders;           // int32_t
        private int _padding;               // explicit 4-byte padding to match native 48-byte size
    }


    public enum API_RESULT
    {
        RESULT_SUCCESS = 0x00,
        RESULT_ERROR = 0x01,
        RESULT_NOT_INITIALIZED = 0x02,
        RESULT_NULL_HANDLE = 0x03,
        RESULT_LOAD_FAIL = 0x04,
        RESULT_PEAK_LOW_THRESHOLD = 0x05,
        RESULT_OUT_OF_MEMORY = 0x06,
    };

    public enum LOGEVENT_OPTION
    {
        SDK_LOGEVENT = 0x00,
    };

    public enum PixelFormat : int
    {
        // Mono
        Mono8 = 0,
        Mono10 = 1,
        Mono12 = 2,

        // RGB
        RGB24 = 3,
        RGB32 = 4,

        // Bayer 8bit
        BayerRG8 = 5,
        BayerBG8 = 6,
        BayerGB8 = 7,
        BayerGR8 = 8,

        // Bayer 10/12bit
        BayerRG10 = 9,
        BayerRG12 = 10,
        BayerBG10 = 11,
        BayerBG12 = 12,
        BayerGB10 = 13,
        BayerGB12 = 14,
        BayerGR10 = 15,
        BayerGR12 = 16,
    }

    public partial class OpenCVLibSharp
    {
        static OpenCVLibSharp()
        {
            System.Diagnostics.Debug.Assert(Marshal.SizeOf(typeof(CvMatView)) == 32, "CvMatView layout size must be exactly 32 bytes on x64.");
            System.Diagnostics.Debug.Assert(Marshal.SizeOf(typeof(RingStats)) == 48, "RingStats layout size must be exactly 48 bytes on x64.");
        }

        public delegate void EventCallback(EVENTDATA EvtInfo);
        public EventCallback _EventCallbackHandle;

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT InitializeLibHandle(IntPtr EventCallback, IntPtr pUserPointer);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT DeInitializeLibHandle();

        // ===== OpenCVLib RingBuffer (통합 v2) =====
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT ImageLoad(string path, int index, out CvMatView outView);

        // 원본 비트뎁스 보존(ANYDEPTH|ANYCOLOR) — 16bit 높이맵은 이 함수를 사용 (예: CV_16UC1 유지)
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT ImageLoadDepth(string path, int index, out CvMatView outView);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT ImageSave(string path, int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT SetImageBuffer(int count, int width, int height, PixelFormat format);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT ClearImageBuffer();

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT SetImageBufferPtr(ref CvMatView src, int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT RegisterImageBufferView(ref CvMatView src, int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetImageBufferPtr(int index, out CvMatView outView);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT LoadTiffStackToRingBuffers(string tifFilePath, out int outLoadedFrames);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT SetLatestIndex(int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT FindBoundaryY(ref CvMatView src, out int outBoundaryY);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT Threshold(ref CvMatView src, ref CvMatView dst, double thresh, double maxval, int thresholdType, out int outUsedThresh);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT AcquireLatestFrame(out CvMatView outView, out int outToken);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT ReleaseFrame(int token);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT AcquireLatestFrameEx(out CvMatView outView, out int outIndex, out ulong outToken, out ulong outGeneration);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT ReleaseFrameToken(ulong token);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT GetRingStats(out RingStats outStats);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetRawPixelValue(int token, int x, int y, ref RawPixelValue outValue);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT AFAlgorithmInitialize(int nCOEA,int nCOEB,int nPeakThreshold, int nHaarFilterSize);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT VerticalSum(IntPtr imageBuffer,ushort width,ushort height,IntPtr profileBuffer);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT UpScaleData(IntPtr profileBuffer, ushort profileSize, IntPtr UpScaledProfile);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT CalcHaarScore(IntPtr profileBuffer, ushort profileSize, ushort haarFilterSize, IntPtr haarScore);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT MaxHaarScorePosition(IntPtr harrScore,ushort harrScoreSize, out ushort outPos);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetDetectedPeakPosition(short nLinePos, ushort nCameraWidth, out float fPeakPosition);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetZRealPosition(short nLinePos, out float fZRealPos);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetZDiffPosition(short nLinePos, out int nZDiffPos);
        
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT CalculatePeakLevel(ref CvMatView src, out int outPeakLevel);



        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT AssembleVisibleTiles(
            int[] frameIndices, int frameCount,
            int frameWidth, int frameHeight,
            int roiX, int roiY, int roiW, int roiH,
            out CvMatView outView);

        // ===== 2GB+ BigTIFF 스트리밍 저장 및 MMF/LRU 디스크 프레임 소스 =====
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT SaveScanAsBigTiff(string filepath, int[] frameIndices, int frameCount, int compression);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT ExportFramesToRawFile(string filepath, int[] frameIndices, int frameCount);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT EnableDiskFrameSource(string backingFilePath, int frameCount, int frameWidth, int frameHeight, PixelFormat format, int lruCapacity);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT DisableDiskFrameSource();

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT LoadAFLaserVideoDataSet(string filepath, out int outFrameCount);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT LoadAFLaserRawImageDataSet(
            string folderPath, string filePrefix, string fileExt,
            int startIndex, int width, int height, out int outFrameCount);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetAFLaserVideoFrame(int index, out CvMatView outView);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT ClearAFLaserVideoDataSet();

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT SetAFLaserSingleImage(ref CvMatView src);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT GetAFLaserSingleImage(out CvMatView outView);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT LoadAFLaserSingleImageFromFile(string filepath);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT BeginVideoDataSet(int expectedFrameCount, double captureFps);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT EndVideoDataSet(out int outFrameCount);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT AppendFrameToVideoDataSet(ref CvMatView src);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern VEOImagingSolutionSharp.API_RESULT ExportVideoDataSet(string filepath, double fps, string fourccCode, out int outWritten);

        // ===== BGR24 Display & Drawing APIs =====
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT ConvertToBgr24(ref CvMatView src, ref CvMatView dst);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT DrawLineBgr24(ref CvMatView view, int x1, int y1, int x2, int y2, byte b, byte g, byte r, int thickness);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT DrawCrossMarkerBgr24(ref CvMatView view, int x, int y, int size, byte b, byte g, byte r, int thickness);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern VEOImagingSolutionSharp.API_RESULT DrawCircleBgr24(ref CvMatView view, int cx, int cy, int radius, byte b, byte g, byte r, int thickness);
    }
}
