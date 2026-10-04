using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VEOImagingSolutionSharp;

namespace VIEWORKS_AFTS
{
    public class VwOpenCVLib
    {
        private readonly object m_LockObj = new object();

        OpenCVLibSharp.EventCallback EventCallbackHandle;
        GCHandle GCEventCallbackHandle; // Delegate for event callback.
        private IntPtr m_EventCallbackPointer;

        public delegate void EventCallbackLogDelegate(EVENTDATA EvtInfo);
        public EventCallbackLogDelegate m_EventCallback;

        private bool _procBufReady = false;

        public VwOpenCVLib()
        {
            EventCallbackHandle = new OpenCVLibSharp.EventCallback(EventLogCallback);
            GCEventCallbackHandle = GCHandle.Alloc(EventCallbackHandle);
            m_EventCallbackPointer = Marshal.GetFunctionPointerForDelegate(EventCallbackHandle);
        }

        public API_RESULT InitializeLib()
        {
            return OpenCVLibSharp.InitializeLibHandle(m_EventCallbackPointer, IntPtr.Zero);
        }

        public API_RESULT DeInitializeLib()
        {
            return OpenCVLibSharp.DeInitializeLibHandle();
        }

        [System.Obsolete("Use SetImageBuffer(count, width, height, format) and SetImageBufferPtr(ref view, index) instead.")]
        public API_RESULT SetImageBuffer(int Width, int Height, IntPtr pBuffer, VEOImagingSolutionSharp.PixelFormat Format, long strideBytes)
        {
            if (!_procBufReady)
            {
                return API_RESULT.RESULT_NOT_INITIALIZED;
            }
            CvMatView view = new CvMatView
            {
                rows = Height,
                cols = Width,
                type = PixelFormatToCvType(Format),
                step = (strideBytes > 0) ? strideBytes : (long)Width * ElemSize(Format),
                data = pBuffer
            };
            return OpenCVLibSharp.SetImageBufferPtr(ref view, 0);
        }

        public API_RESULT LoadAFLaserVideoDataSet(string filepath, out int outFrameCount)
            => OpenCVLibSharp.LoadAFLaserVideoDataSet(filepath, out outFrameCount);

        // RAW 프레임 폴더 임포트. 비디오 데이터셋과 동일한 저장소(m_afVideoDataSet)를 채우므로
        // 이후 GetAFLaserVideoFrame / 스크롤 / Run Analysis 경로를 그대로 사용한다.
        public API_RESULT LoadAFLaserRawImageDataSet(
            string folderPath, string filePrefix, string fileExt,
            int startIndex, int width, int height, out int outFrameCount)
            => OpenCVLibSharp.LoadAFLaserRawImageDataSet(
                   folderPath, filePrefix, fileExt, startIndex, width, height, out outFrameCount);

        public API_RESULT GetAFLaserVideoFrame(int index, out CvMatView outView)
            => OpenCVLibSharp.GetAFLaserVideoFrame(index, out outView);

        public API_RESULT ClearAFLaserVideoDataSet()
            => OpenCVLibSharp.ClearAFLaserVideoDataSet();

        public API_RESULT SetAFLaserSingleImage(ref CvMatView src)
            => OpenCVLibSharp.SetAFLaserSingleImage(ref src);

        public API_RESULT GetAFLaserSingleImage(out CvMatView outView)
            => OpenCVLibSharp.GetAFLaserSingleImage(out outView);

        public API_RESULT LoadAFLaserSingleImageFromFile(string filepath)
            => OpenCVLibSharp.LoadAFLaserSingleImageFromFile(filepath);

        public API_RESULT AcquireMatFrame(out CvMatView outView, out int outToken)
        {
            return OpenCVLibSharp.AcquireLatestFrame(out outView, out outToken);
        }

        public API_RESULT ReleaseMatFrame(int token)
        {
            return OpenCVLibSharp.ReleaseFrame(token);
        }

        public API_RESULT InitializeAFLib(int nCOEA, int nCOEB, int nPeakThreshold, int nHaarFilterSize)
        {
            return OpenCVLibSharp.AFAlgorithmInitialize(nCOEA, nCOEB, nPeakThreshold, nHaarFilterSize);
        }

        // ====== OpenCVLib RingBuffer (v2 Index API Wrapper) ======
        public API_RESULT ImageLoad(string path, int index, out CvMatView outView)
        {
            return OpenCVLibSharp.ImageLoad(path, index, out outView);
        }

        public API_RESULT ImageLoadDepth(string path, int index, out CvMatView outView)
        {
            return OpenCVLibSharp.ImageLoadDepth(path, index, out outView);
        }

        public API_RESULT ImageSave(string path, int index)
        {
            return OpenCVLibSharp.ImageSave(path, index);
        }

        public API_RESULT SetImageBuffer(int count, int width, int height, VEOImagingSolutionSharp.PixelFormat format)
        {
            return OpenCVLibSharp.SetImageBuffer(count, width, height, format);
        }

        public API_RESULT ClearImageBuffer()
        {
            return OpenCVLibSharp.ClearImageBuffer();
        }

        public API_RESULT SetImageBufferPtr(ref CvMatView src, int index)
        {
            return OpenCVLibSharp.SetImageBufferPtr(ref src, index);
        }

        public API_RESULT RegisterImageBufferView(ref CvMatView src, int index)
        {
            return OpenCVLibSharp.RegisterImageBufferView(ref src, index);
        }

        public API_RESULT GetImageBufferPtr(int index, out CvMatView outView)
        {
            return OpenCVLibSharp.GetImageBufferPtr(index, out outView);
        }

        public API_RESULT LoadTiffStackToRingBuffers(string tifFilePath, out int outLoadedFrames)
        {
            _procBufReady = true;
            return OpenCVLibSharp.LoadTiffStackToRingBuffers(tifFilePath, out outLoadedFrames);
        }

        public API_RESULT ConvertToBgr24(ref CvMatView src, ref CvMatView dst)
        {
            return OpenCVLibSharp.ConvertToBgr24(ref src, ref dst);
        }

        public API_RESULT SetLatestIndex(int index)
        {
            return OpenCVLibSharp.SetLatestIndex(index);
        }

        public API_RESULT AcquireLatestFrame(out CvMatView outView, out int outIndex)
        {
            return OpenCVLibSharp.AcquireLatestFrame(out outView, out outIndex);
        }

        public API_RESULT ReleaseFrame(int index)
        {
            return OpenCVLibSharp.ReleaseFrame(index);
        }

        public API_RESULT AllocateBackDataPool(int count)
        {
            _procBufReady = (count >= 1);
            return OpenCVLibSharp.SetImageBuffer(count, 0, 0, VEOImagingSolutionSharp.PixelFormat.Mono8);
        }

        public API_RESULT SetBackDataFrame(int index, int width, int height, IntPtr pBuffer, VEOImagingSolutionSharp.PixelFormat pixelFormat)
        {
            CvMatView view = new CvMatView
            {
                rows = height,
                cols = width,
                type = PixelFormatToCvType(pixelFormat),
                step = (long)width * ElemSize(pixelFormat),
                data = pBuffer
            };
            return OpenCVLibSharp.SetImageBufferPtr(ref view, index);
        }

        public API_RESULT PublishBackDataFrame(int index)
        {
            return OpenCVLibSharp.SetLatestIndex(index);
        }

        public API_RESULT ClearBackDataPool()
        {
            _procBufReady = false;
            return OpenCVLibSharp.ClearImageBuffer();
        }

        public API_RESULT ProfileVerticalSum(IntPtr imageBuffer, ushort width, ushort height, byte[] profileBuffer)
        {
            if (imageBuffer == IntPtr.Zero) return API_RESULT.RESULT_ERROR;
            if (profileBuffer == null || profileBuffer.Length < width) return API_RESULT.RESULT_ERROR;
            unsafe
            {
                fixed (byte* pProfile = profileBuffer)
                {
                    return OpenCVLibSharp.VerticalSum(
                        imageBuffer,
                        width,
                        height,
                        (IntPtr)pProfile
                    );
                }
            }
        }

        public API_RESULT UpscaleProfile(byte[] profile, ushort profileSize, byte[] upscaledProfile)
        {
            if (profile == null || upscaledProfile == null)
                return API_RESULT.RESULT_ERROR;

            if (profile.Length < profileSize)
                return API_RESULT.RESULT_ERROR;

            int upSize = profileSize << 4; // SUB_PIXEL_SCALE_BIT = 4 (x16)
            if (upscaledProfile.Length < upSize)
                return API_RESULT.RESULT_ERROR;

            unsafe
            {
                fixed (byte* pProfile = profile)
                fixed (byte* pUp = upscaledProfile)
                {
                    return OpenCVLibSharp.UpScaleData(
                        (IntPtr)pProfile,
                        profileSize,
                        (IntPtr)pUp
                    );
                }
            }
        }

        public API_RESULT CalculateHaarScore(byte[] profile, ushort profileSize, ushort haarFilterSize, short[] haarScore)
        {
            if (profile == null || haarScore == null)
                return API_RESULT.RESULT_ERROR;

            if (profile.Length < profileSize)
                return API_RESULT.RESULT_ERROR;

            if (haarScore.Length < profileSize)
                return API_RESULT.RESULT_ERROR;

            unsafe
            {
                fixed (byte* pProfile = profile)
                fixed (short* pScore = haarScore)
                {
                    return OpenCVLibSharp.CalcHaarScore(
                        (IntPtr)pProfile,
                        profileSize,
                        haarFilterSize,
                        (IntPtr)pScore
                    );
                }
            }
        }

        public API_RESULT MaxHaarScorePosition(short[] harrScoreProfile, ushort profileSize, out ushort nMaxScorePosition)
        {
            if (harrScoreProfile == null)
            {
                nMaxScorePosition = 0;
                return API_RESULT.RESULT_ERROR;
            }

            if (harrScoreProfile.Length < profileSize)
            {
                nMaxScorePosition = 0;
                return API_RESULT.RESULT_ERROR;
            }

            unsafe
            {
                fixed (short* pProfile = harrScoreProfile)
                {
                    return OpenCVLibSharp.MaxHaarScorePosition(
                        (IntPtr)pProfile,
                        profileSize,
                        out nMaxScorePosition
                    );
                }
            }
        }

        public API_RESULT GetDetectedPeakPosition(short nLinePos, ushort nCameraWidth, out float fPeakPosition)
        {
            return OpenCVLibSharp.GetDetectedPeakPosition(nLinePos, nCameraWidth, out fPeakPosition);
        }

        public API_RESULT GetZRealPosition(short nLinePos, out float fZRealPos)
        {
            return OpenCVLibSharp.GetZRealPosition(nLinePos, out fZRealPos);
        }

        public API_RESULT GetZDiffPosition(short nLinePos, out int nZDiffPos)
        {
            return OpenCVLibSharp.GetZDiffPosition(nLinePos, out nZDiffPos);
        }

        public void EventLogCallback(EVENTDATA EvtInfo)
        {
            if (m_EventCallback != null)
            {
                m_EventCallback(EvtInfo);
            }
        }

        public double[] ToDoubleArray(byte[] src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var dst = new double[src.Length];
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i];
            return dst;
        }

        public double[] ToDoubleArray(short[] src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var dst = new double[src.Length];
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i];
            return dst;
        }

        /// <summary>
        /// CvMatView 포인터를 GDI+ Bitmap으로 안전하고 빠르게 변환하는 유틸리티 (Zero-Copy 및 고속 복사)
        /// </summary>
        public static Bitmap CvMatViewToBitmap(CvMatView view)
        {
            if (view.data == IntPtr.Zero || view.cols <= 0 || view.rows <= 0)
                return null;

            int channels = (view.type == 0 || view.type == 2) ? 1 : ((view.type == 16) ? 3 : 1);

            if (channels == 3)
            {
                Bitmap bmp = new Bitmap(view.cols, view.rows, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData bd = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                try
                {
                    int srcStride = (int)view.step;
                    int dstStride = bd.Stride;
                    int rowSize = view.cols * 3;

                    unsafe
                    {
                        byte* pSrc = (byte*)view.data.ToPointer();
                        byte* pDst = (byte*)bd.Scan0.ToPointer();
                        for (int y = 0; y < view.rows; y++)
                        {
                            Buffer.MemoryCopy(
                                pSrc + (long)y * srcStride,
                                pDst + (long)y * dstStride,
                                Math.Abs(dstStride),
                                rowSize);
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(bd);
                }
                return bmp;
            }
            else if (channels == 1)
            {
                Bitmap bmp = new Bitmap(view.cols, view.rows, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

                ColorPalette palette = bmp.Palette;
                for (int i = 0; i < 256; i++)
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                bmp.Palette = palette;

                BitmapData bd = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

                try
                {
                    int srcStride = (int)view.step;
                    int dstStride = bd.Stride;

                    if (view.type == 2) // CV_16UC1
                    {
                        unsafe
                        {
                            byte* pSrcBase = (byte*)view.data.ToPointer();
                            byte* pDstBase = (byte*)bd.Scan0.ToPointer();

                            for (int y = 0; y < view.rows; y++)
                            {
                                ushort* srcRow = (ushort*)(pSrcBase + (long)y * srcStride);
                                byte* dstRow = pDstBase + (long)y * dstStride;
                                for (int x = 0; x < view.cols; x++)
                                {
                                    dstRow[x] = (byte)(srcRow[x] >> 4);
                                }
                            }
                        }
                    }
                    else // CV_8UC1
                    {
                        int rowSize = view.cols;
                        unsafe
                        {
                            byte* pSrc = (byte*)view.data.ToPointer();
                            byte* pDst = (byte*)bd.Scan0.ToPointer();
                            for (int y = 0; y < view.rows; y++)
                            {
                                Buffer.MemoryCopy(
                                    pSrc + (long)y * srcStride,
                                    pDst + (long)y * dstStride,
                                    Math.Abs(dstStride),
                                    rowSize);
                            }
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(bd);
                }
                return bmp;
            }

            return null;
        }

        private static int PixelFormatToCvType(VEOImagingSolutionSharp.PixelFormat fmt)
        {
            switch (fmt)
            {
                case VEOImagingSolutionSharp.PixelFormat.Mono8: return 0;   // CV_8UC1
                case VEOImagingSolutionSharp.PixelFormat.RGB24: return 16;  // CV_8UC3
                case VEOImagingSolutionSharp.PixelFormat.RGB32: return 24;  // CV_8UC4
                case VEOImagingSolutionSharp.PixelFormat.Mono10:
                case VEOImagingSolutionSharp.PixelFormat.Mono12: return 2;  // CV_16UC1
                default: return 0;
            }
        }

        private static int ElemSize(VEOImagingSolutionSharp.PixelFormat fmt)
        {
            switch (fmt)
            {
                case VEOImagingSolutionSharp.PixelFormat.Mono8: return 1;
                case VEOImagingSolutionSharp.PixelFormat.RGB24: return 3;
                case VEOImagingSolutionSharp.PixelFormat.RGB32: return 4;
                case VEOImagingSolutionSharp.PixelFormat.Mono10:
                case VEOImagingSolutionSharp.PixelFormat.Mono12: return 2;
                default: return 1;
            }
        }
    }
}
