#pragma once

// ============================================================================
// Per-camera ring context (multi-camera live view).
//
// This header is INTERNAL: it is deliberately NOT included from the public API header
// (VEOImgSoLLibrary_API.h), which only ever sees the opaque `VEO_RING_CONTEXT` pointer typedef
// declared in VEOImgSoLGlobal.h. Consumers therefore cannot depend on this layout, and it can be
// changed without breaking the exported ABI.
//
// Each context owns one independent OpenCVUtil engine instance, so N cameras get N fully isolated
// ring buffers instead of sharing the single process-global _OpenCVLibHandle used by the legacy
// region-1/2 exports. The legacy path uses one unregistered context, so it cannot be targeted by
// opaque-handle lifecycle operations.
// ============================================================================

#include "VEOImgSoLUtil.h"      // OpenCVUtil, PixelFormat, RingStats, EventCallback (via VwSolutionSDKEventLog.h)
#include <memory>
#include <mutex>
#include <thread>
#include <cstdint>

// The opaque handle's pointee IS this type (Vulkan-style), so the exports need no reinterpret_cast.
struct VEO_RING_CONTEXT__
{
    enum class State { Live, Destroying, Dead };
    static const uint32_t kMagicLive = 0x564F5243u;  // 'VORC'
    static const uint32_t kMagicDead = 0xDEAD0000u;

    uint32_t                     m_magic;          // kMagicLive while usable; kMagicDead after destroy
    uint64_t                     m_id;             // monotonic; log/diagnostic correlation only
    State                        m_state;          // guarded by m_mtx
    std::recursive_mutex         m_mtx;            // guards m_state/m_spUtil HANDOFF only -- never held across engine work
    std::shared_ptr<OpenCVUtil>  m_spUtil;         // the per-context engine; use_count() IS the in-flight quiesce signal
    EventCallback*               m_pCallback;      // diagnostics; the live copy lives in OpenCVUtil::m_EventLog
    void*                        m_pUserPointer;   // diagnostics; ditto
    std::thread::id              m_destroyThread;  // diagnostics
};

typedef VEO_RING_CONTEXT__ VeoRingContext;

// Owned only by the legacy lifecycle path; deliberately absent from s_ctxRegistry.
extern VeoRingContext s_legacyCtx;
