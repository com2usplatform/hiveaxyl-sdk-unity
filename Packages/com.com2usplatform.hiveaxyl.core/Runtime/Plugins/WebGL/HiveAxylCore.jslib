// Copyright (c) Com2uS Platform Corp. All rights reserved.
//
// Unity WebGL jslib bridge for Hive Axyl Core.
// Entry points mirror the C# JslibProtocol constants.
//
// Reverse callbacks (async responses and native events) are delivered to the
// C# trampolines via dynCall_vii(fnPtr, arg1, arg2). String arguments are
// handled by IL2CPP marshaling on the way in (C#→JS) and by _malloc/_free on
// the way out (JS→C# callback arguments).
//
// The TypeScript receiver is accessed at call time via Module['HiveAxylBridge'].
// It must be assigned before HiveBootstrap.Initialize() is called.

var HiveAxylJslib = {

  // ── Internal state ────────────────────────────────────────────────────────

  $HiveAxylState: {
    asyncFnPtr: 0,
    eventFnPtr: 0
  },

  // ── SetCallbacks ──────────────────────────────────────────────────────────

  HiveAxyl_Jslib_SetCallbacks__deps: ['$HiveAxylState'],
  HiveAxyl_Jslib_SetCallbacks: function (asyncFnPtr, eventFnPtr) {
    HiveAxylState.asyncFnPtr = asyncFnPtr;
    HiveAxylState.eventFnPtr = eventFnPtr;

    // Wire the event dispatch function into the TypeScript receiver so that
    // plugin adapters can push events back to the C# event bus. Dispose()
    // clears callbacks by calling this with (0, 0); that teardown call must
    // not resurrect the bridge's shutdown state.
    var bridge = Module['HiveAxylBridge'];
    if (bridge && eventFnPtr !== 0) {
      bridge.setEventCallback(function (eventName, jsonPayload) {
        var fnPtr = HiveAxylState.eventFnPtr;
        if (fnPtr === 0) return;

        var nameLen    = lengthBytesUTF8(eventName)   + 1;
        var payloadLen = lengthBytesUTF8(jsonPayload) + 1;
        var namePtr    = _malloc(nameLen);
        var payloadPtr = _malloc(payloadLen);

        stringToUTF8(eventName,   namePtr,    nameLen);
        stringToUTF8(jsonPayload, payloadPtr, payloadLen);

        try {
          dynCall_vii(fnPtr, namePtr, payloadPtr);
        } finally {
          _free(namePtr);
          _free(payloadPtr);
        }
      });
    }
  },

  // ── CallSync ──────────────────────────────────────────────────────────────
  // Returns a heap-allocated UTF-8 string. The C# caller is responsible for
  // freeing it via HiveAxyl_Jslib_FreeString.

  HiveAxyl_Jslib_CallSync__deps: ['$HiveAxylState'],
  HiveAxyl_Jslib_CallSync: function (pluginNamePtr, methodNamePtr, payloadPtr) {
    var pluginName = UTF8ToString(pluginNamePtr);
    var methodName = UTF8ToString(methodNamePtr);
    var payload    = UTF8ToString(payloadPtr);

    var result;
    var bridge = Module['HiveAxylBridge'];

    if (!bridge) {
      result = JSON.stringify({
        error: { code: 'UNIMPLEMENTED', message: 'HiveAxylBridge is not registered.' }
      });
    } else {
      try {
        result = bridge.callSync(pluginName, methodName, payload);
      } catch (e) {
        result = JSON.stringify({
          error: { code: 'INTERNAL', message: String(e && e.constructor ? e.constructor.name : e) }
        });
      }
    }

    var len = lengthBytesUTF8(result) + 1;
    var buf = _malloc(len);
    stringToUTF8(result, buf, len);
    return buf;
  },

  // ── CallAsync ─────────────────────────────────────────────────────────────
  // Fire-and-forget: the adapter's onComplete callback delivers the response
  // via dynCall_vii back into the C# AsyncResponseTrampoline.

  HiveAxyl_Jslib_CallAsync__deps: ['$HiveAxylState'],
  HiveAxyl_Jslib_CallAsync: function (pluginNamePtr, methodNamePtr, enrichedPayloadPtr) {
    var pluginName      = UTF8ToString(pluginNamePtr);
    var methodName      = UTF8ToString(methodNamePtr);
    var enrichedPayload = UTF8ToString(enrichedPayloadPtr);

    // Capture at call time so that Shutdown between dispatch and completion
    // does not corrupt state for in-flight calls.
    var asyncFnPtr = HiveAxylState.asyncFnPtr;

    var callbackId = 0;
    try { callbackId = JSON.parse(enrichedPayload)['_callbackId'] | 0; } catch (_) {}

    function deliverResponse(responseJson) {
      if (asyncFnPtr === 0) return;

      var len = lengthBytesUTF8(responseJson) + 1;
      var ptr = _malloc(len);
      stringToUTF8(responseJson, ptr, len);

      try {
        dynCall_vii(asyncFnPtr, callbackId, ptr);
      } finally {
        _free(ptr);
      }
    }

    var bridge = Module['HiveAxylBridge'];
    if (!bridge) {
      deliverResponse(JSON.stringify({
        error: { code: 'UNIMPLEMENTED', message: 'HiveAxylBridge is not registered.' }
      }));
      return;
    }

    try {
      bridge.callAsync(pluginName, methodName, enrichedPayload, deliverResponse);
    } catch (e) {
      deliverResponse(JSON.stringify({
        error: { code: 'INTERNAL', message: String(e && e.constructor ? e.constructor.name : e) }
      }));
    }
  },

  // ── Cancel ────────────────────────────────────────────────────────────────
  // Best-effort interrupt of an in-flight async call, keyed by the same
  // callbackId the matching CallAsync carried. Routes to the TypeScript
  // receiver's cancel(), which forwards to the plugin's cancel hook so a
  // long-running operation (e.g. a popup awaiting an OAuth redirect) is
  // released. The managed task is already completed as cancelled by
  // NativeBridge; a late response the interrupted adapter still delivers is
  // dropped by the correlation manager.

  HiveAxyl_Jslib_Cancel: function (pluginNamePtr, callbackId) {
    var bridge = Module['HiveAxylBridge'];
    if (!bridge || typeof bridge.cancel !== 'function') return;

    try {
      bridge.cancel(UTF8ToString(pluginNamePtr), callbackId);
    } catch (e) {
      // Cancel has no response channel; a receiver failure must not escape
      // into the wasm caller.
    }
  },

  // ── FreeString ────────────────────────────────────────────────────────────
  // Frees a heap-allocated string returned by HiveAxyl_Jslib_CallSync.

  HiveAxyl_Jslib_FreeString: function (ptr) {
    _free(ptr);
  },

  // ── Shutdown ──────────────────────────────────────────────────────────────

  HiveAxyl_Jslib_Shutdown__deps: ['$HiveAxylState'],
  HiveAxyl_Jslib_Shutdown: function () {
    HiveAxylState.asyncFnPtr = 0;
    HiveAxylState.eventFnPtr = 0;

    var bridge = Module['HiveAxylBridge'];
    if (bridge && typeof bridge.shutdown === 'function') {
      bridge.shutdown();
    }
  }
};

autoAddDeps(HiveAxylJslib, '$HiveAxylState');
mergeInto(LibraryManager.library, HiveAxylJslib);
