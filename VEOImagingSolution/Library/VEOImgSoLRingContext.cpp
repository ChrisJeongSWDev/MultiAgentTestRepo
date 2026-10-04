#include "VEOImgSoLRingContext.h"
#include "VEOImgSoLLibrary_API.h"   // for API_RESULT, DLL_OPENCVLIB (extern "C" impls below)
#include <unordered_map>
#include <atomic>
#include <chrono>

// ============================================================================
// File-scope registry state.
//
// The registry is the SOLE sound way to validate a caller-supplied opaque pointer, and registry
// membership also RETAINS the pointee's lifetime (shared_ptr-owned) -- not just its identity.
// ============================================================================
static std::mutex                          s_ctxRegMtx;
// Registry entries OWN the context via shared_ptr -- this is what makes registry admission a
// retained lifetime instead of a bare pointer membership check. Any thread that copies a
// shared_ptr out of this map (under s_ctxRegMtx) keeps the pointee alive for the rest of its own
// call, regardless of what any concurrent DestroyRingContext does afterward -- there is no window
// where a resolved-but-not-yet-locked raw pointer can be freed out from under a caller.
static std::unordered_map<VeoRingContext*, std::shared_ptr<VeoRingContext>> s_ctxRegistry;
static std::atomic<uint64_t>               s_ctxNextId{ 1 };
static thread_local VeoRingContext*        t_ctxStack[8];   // contexts this thread is currently inside
static thread_local int                    t_ctxDepth = 0;  // per-context analogue of s_apiCallDepth (which is a single
                                                            // scalar per thread and cannot distinguish contexts -- do
                                                            // not reuse it, this stack is the correct adaptation)

namespace {

// Resolves an opaque handle to a live engine. On success the returned shared_ptr keeps the engine
// alive for the whole call even if another thread starts destroying the context -- the exact
// ownership trick the legacy DeInitializeLibHandle already relies on, applied per context instead
// of once globally.
API_RESULT ResolveCtx(VEO_RING_CONTEXT h, std::shared_ptr<OpenCVUtil>& outUtil, std::shared_ptr<VeoRingContext>& outCtx)
{
    if (!h) return RESULT_NULL_HANDLE;
    std::shared_ptr<VeoRingContext> ctx;
    {
        std::lock_guard<std::mutex> reg(s_ctxRegMtx);
        auto it = s_ctxRegistry.find(h);
        if (it == s_ctxRegistry.end()) return RESULT_NULL_HANDLE;   // stale/foreign
        ctx = it->second;   // retains a real reference for the rest of this call
    }
    std::lock_guard<std::recursive_mutex> lk(ctx->m_mtx);
    if (ctx->m_magic != VeoRingContext::kMagicLive)                    return RESULT_NULL_HANDLE;
    if (ctx->m_state != VeoRingContext::State::Live || !ctx->m_spUtil) return RESULT_NOT_INITIALIZED;
    outUtil = ctx->m_spUtil;    // +1 use_count: this is what DestroyRingContext quiesces on
    outCtx  = ctx;              // caller keeps this alive for the duration of its own call too
    return RESULT_SUCCESS;
}

// Marks the calling thread as "inside" a context for the duration of one export call, so a destroy
// re-entered from that context's own event callback can be rejected instead of spinning forever.
struct CtxCallGuard
{
    bool ok;
    explicit CtxCallGuard(VeoRingContext* c) { ok = (t_ctxDepth < 8); if (ok) t_ctxStack[t_ctxDepth++] = c; }
    ~CtxCallGuard() { if (ok) --t_ctxDepth; }
};

bool ThreadIsInside(VeoRingContext* c)
{
    for (int i = 0; i < t_ctxDepth; ++i) if (t_ctxStack[i] == c) return true;
    return false;
}

} // namespace

#pragma region Context lifecycle

extern "C" DLL_OPENCVLIB API_RESULT CreateRingContext(EventCallback* pCallback, void* pUserPointer, VEO_RING_CONTEXT* outCtx)
{
    if (!outCtx) return RESULT_NULL_HANDLE;
    *outCtx = nullptr;
    try
    {
        auto ctx = std::make_shared<VeoRingContext>();
        ctx->m_magic        = VeoRingContext::kMagicLive;
        ctx->m_id           = s_ctxNextId.fetch_add(1);
        ctx->m_state        = VeoRingContext::State::Live;
        ctx->m_pCallback    = pCallback;
        ctx->m_pUserPointer = pUserPointer;
        ctx->m_spUtil       = std::make_shared<OpenCVUtil>(pCallback, pUserPointer);
        ctx->m_spUtil->InitializeLibHandle();
        { std::lock_guard<std::mutex> reg(s_ctxRegMtx); s_ctxRegistry[ctx.get()] = ctx; }
        *outCtx = ctx.get();
        return RESULT_SUCCESS;
    }
    catch (...) { return RESULT_ERROR; }
    // NOTE: if make_shared/InitializeLibHandle throws AFTER `new VeoRingContext()` but before the
    // registry insert, the partially-built context is leaked rather than freed. That is deliberate:
    // this is an allocation-failure edge case, and a cleanup path here would have to reason about a
    // half-constructed object that no other thread can see -- a subtler bug risk than the leak.
}

extern "C" DLL_OPENCVLIB API_RESULT DestroyRingContext(VEO_RING_CONTEXT h)
{
    if (!h) return RESULT_NULL_HANDLE;
    std::shared_ptr<VeoRingContext> ctx;
    {
        std::lock_guard<std::mutex> reg(s_ctxRegMtx);
        auto it = s_ctxRegistry.find(h);
        if (it == s_ctxRegistry.end()) return RESULT_NULL_HANDLE;
        ctx = it->second;   // retains a real reference for the rest of this call
    }

    // (0) Re-entrancy guard: a destroy issued from inside THIS context's own event callback would
    //     spin against its own in-flight call forever.
    if (ThreadIsInside(ctx.get())) return RESULT_ERROR;

    std::shared_ptr<OpenCVUtil> doomed;
    {
        std::lock_guard<std::recursive_mutex> lk(ctx->m_mtx);
        if (ctx->m_state != VeoRingContext::State::Live) return RESULT_ERROR;   // double / racing destroy
        ctx->m_state = VeoRingContext::State::Destroying;                       // rejects every NEW ResolveCtx
        ctx->m_destroyThread = std::this_thread::get_id();
        doomed = ctx->m_spUtil;
        ctx->m_spUtil.reset();
    }

    // (1) Quiesce IN-FLIGHT CALLS -- the legacy DeInitializeLibHandle mechanism, scoped per context.
    while (doomed.use_count() > 1)
        std::this_thread::yield();

    // (2) Quiesce BORROWED FRAMES. The legacy path does NOT do this (a known gap -- do not replicate
    //     it here). After (1), no API call is in flight, but a token holder holds no shared_ptr --
    //     OpenCVUtil::GetRingStats().activeReaders is the only observable.
    RingStats st;
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(2000);
    for (;;)
    {
        if (doomed->GetRingStats(&st) != RESULT_SUCCESS) break;
        if (st.activeReaders <= 0) break;
        if (std::chrono::steady_clock::now() >= deadline)
        {
            // Un-destroy rather than free under a live reader or leak: the caller can release its
            // frames and retry. Safe because (1) already proved no call is in flight and Destroying
            // blocked every new one.
            std::lock_guard<std::recursive_mutex> lk(ctx->m_mtx);
            ctx->m_spUtil = doomed;
            ctx->m_state  = VeoRingContext::State::Live;
            return RESULT_ERROR;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }

    doomed->DeinitializeLibHandle();   // keeps the Initialize/Deinitialize pairing (log-only today)
    doomed.reset();                    // ~OpenCVUtil: ring, event log, frame sources

    { std::lock_guard<std::mutex> reg(s_ctxRegMtx); s_ctxRegistry.erase(ctx.get()); }   // drop the registry's owning reference
    {
        std::lock_guard<std::recursive_mutex> lk(ctx->m_mtx);
        ctx->m_magic = VeoRingContext::kMagicDead;
        ctx->m_state = VeoRingContext::State::Dead;
    }
    // No explicit delete: `ctx` (this function's local shared_ptr) is now the last owning
    // reference -- the registry's copy was just erased above -- so ~VeoRingContext runs when
    // `ctx` goes out of scope at return, exactly where `delete ctx;` used to run.
    return RESULT_SUCCESS;
}

#pragma endregion

#pragma region Per-context ring operations

extern "C" DLL_OPENCVLIB API_RESULT RingContextSetImageBuffer(VEO_RING_CONTEXT h, int count, int width, int height, PixelFormat format)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->SetImageBuffer(count, width, height, format);
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextClearImageBuffer(VEO_RING_CONTEXT h)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->ClearImageBuffer();
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextSetImageBufferPtr(VEO_RING_CONTEXT h, const CvMatView* src, int index)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->SetImageBufferPtr(src, index);
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextRegisterImageBufferView(VEO_RING_CONTEXT h, const CvMatView* src, int index)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->RegisterImageBufferView(src, index);
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextSetLatestIndex(VEO_RING_CONTEXT h, int index)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->SetLatestIndex(index);
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextAcquireLatestFrameEx(VEO_RING_CONTEXT h, CvMatView* outView, int* outIndex, uint64_t* outToken, uint64_t* outGeneration)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->AcquireLatestFrameEx(outView, outIndex, outToken, outGeneration);
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextReleaseFrameToken(VEO_RING_CONTEXT h, uint64_t token)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->ReleaseFrameToken(token);
    }
    catch (...) { return RESULT_ERROR; }
}

extern "C" DLL_OPENCVLIB API_RESULT RingContextGetRingStats(VEO_RING_CONTEXT h, RingStats* outStats)
{
    try
    {
        std::shared_ptr<OpenCVUtil> util;
        std::shared_ptr<VeoRingContext> ctx;
        API_RESULT r = ResolveCtx(h, util, ctx);
        if (r != RESULT_SUCCESS) return r;
        CtxCallGuard g(ctx.get());
        if (!g.ok) return RESULT_ERROR;          // pathological nesting depth
        return util->GetRingStats(outStats);
    }
    catch (...) { return RESULT_ERROR; }
}

#pragma endregion
