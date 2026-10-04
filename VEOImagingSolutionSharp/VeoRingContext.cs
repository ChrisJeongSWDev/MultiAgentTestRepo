using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VEOImagingSolutionSharp
{
    public partial class OpenCVLibSharp
    {
        // ===== VEO_RING_CONTEXT: raw handle-lifecycle (IntPtr forms) =====
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT CreateRingContext(IntPtr pCallback, IntPtr pUserPointer, out IntPtr outCtx);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT DestroyRingContext(IntPtr h);

        // ===== VEO_RING_CONTEXT: operational ring ops (SafeHandle-typed) =====
        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextSetImageBuffer(VeoRingContextHandle ctx, int count, int width, int height, PixelFormat format);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextClearImageBuffer(VeoRingContextHandle ctx);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextSetImageBufferPtr(VeoRingContextHandle ctx, ref CvMatView src, int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextRegisterImageBufferView(VeoRingContextHandle ctx, ref CvMatView src, int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextSetLatestIndex(VeoRingContextHandle ctx, int index);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextAcquireLatestFrameEx(VeoRingContextHandle ctx, out CvMatView outView, out int outIndex, out ulong outToken, out ulong outGeneration);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextReleaseFrameToken(VeoRingContextHandle ctx, ulong token);

        [DllImport("VEOImagingSolution.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern API_RESULT RingContextGetRingStats(VeoRingContextHandle ctx, out RingStats outStats);
    }

    // The native side stores the raw function pointer passed at CreateRingContext time and
    // calls back into it for the lifetime of the context -- if the C# delegate object backing
    // that function pointer gets garbage-collected, the native side is holding a dangling
    // function pointer. A GCHandle (normal, not weak) rooted inside this SafeHandle is what
    // keeps the delegate alive for exactly as long as the native context can still call back
    // into it, no longer and no shorter.
    public sealed class VeoRingContextHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private GCHandle _callbackHandle;

        private VeoRingContextHandle() : base(true) { }

        // Factory: the only way to obtain a valid instance. `callback` must be the
        // OpenCVLibSharp.EventCallback delegate the caller wants the native context to
        // invoke for its lifetime -- reuse that existing delegate type verbatim (do not
        // redefine it here; the native<->managed ABI match for EVENTDATA is coincidental
        // and only holds for that exact existing delegate signature).
        public static VeoRingContextHandle Create(OpenCVLibSharp.EventCallback callback, IntPtr pUserPointer, out API_RESULT result)
        {
            GCHandle callbackHandle = GCHandle.Alloc(callback);
            IntPtr rawCtx = IntPtr.Zero;
            bool nativeCreated = false;
            try
            {
                IntPtr pCallback = Marshal.GetFunctionPointerForDelegate(callback);
                result = OpenCVLibSharp.CreateRingContext(pCallback, pUserPointer, out rawCtx);
                if (result != API_RESULT.RESULT_SUCCESS || rawCtx == IntPtr.Zero)
                {
                    // Native side never got a handle to call back through -- safe to free now.
                    callbackHandle.Free();
                    return null;
                }
                nativeCreated = true;
                VeoRingContextHandle h = new VeoRingContextHandle();
                h.SetHandle(rawCtx);
                h._callbackHandle = callbackHandle;
                return h;
            }
            catch
            {
                if (nativeCreated)
                {
                    // Native already stored the callback pointer and is alive -- unlike the "native
                    // never got a handle" case above, we cannot simply free the GCHandle here without
                    // risking a dangling callback. Try to tear the native context down first; only
                    // free the GCHandle if that succeeds -- same fail-safe rule ReleaseHandle uses,
                    // for the same reason (native may still be able to call back on failure).
                    API_RESULT destroyResult = OpenCVLibSharp.DestroyRingContext(rawCtx);
                    if (destroyResult == API_RESULT.RESULT_SUCCESS)
                    {
                        callbackHandle.Free();
                    }
                    // else: deliberately leak the GCHandle, same trade-off as ReleaseHandle.
                }
                else
                {
                    // Native was never created (or the pre-creation call itself threw) -- nothing
                    // native-side can call back through, safe to free immediately.
                    callbackHandle.Free();
                }
                throw;
            }
        }

        protected override bool ReleaseHandle()
        {
            API_RESULT result = OpenCVLibSharp.DestroyRingContext(handle);
            if (result == API_RESULT.RESULT_SUCCESS)
            {
                // Native context is fully torn down and will never call back again -- now,
                // and only now, is it safe to free the delegate's GCHandle.
                if (_callbackHandle.IsAllocated) _callbackHandle.Free();
                return true;
            }
            // RESULT_ERROR here (see DestroyRingContext's native contract) most commonly means
            // the 2-second borrowed-frame quiesce timed out and the native side "un-destroyed"
            // itself back to Live -- the context is still alive and could still call back.
            // Freeing the GCHandle in that case would be a real use-after-free risk the moment
            // the native side invokes the now-dangling function pointer. Deliberately leak the
            // GCHandle (and report failure) instead of risking that -- a Dispose-time destroy
            // failure here means the caller almost certainly failed to release its borrowed
            // frame tokens before disposing, which is a caller bug, not something this binding
            // can safely paper over. Document this trade-off, do not silently retry or free.
            return false;
        }
    }
}
