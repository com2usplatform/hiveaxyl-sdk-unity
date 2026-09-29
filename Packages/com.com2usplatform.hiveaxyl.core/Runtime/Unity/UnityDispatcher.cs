// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using UnityEngine;

namespace Hive.Axyl.Core
{
    /// <summary>
    /// Unity-specific <see cref="IDispatcher"/> implementation that dispatches actions
    /// to the main thread via <see cref="UnitySynchronizationContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class captures the Unity main-thread <see cref="UnitySynchronizationContext"/>
    /// at construction time and uses it for all dispatch operations.
    /// Must be created on the main thread (e.g., from <c>[RuntimeInitializeOnLoadMethod]</c>).
    /// </para>
    /// <para>
    /// <see cref="PostDelayed"/> is driven by the player loop, not by a thread timer: the
    /// action is re-posted to the <see cref="UnitySynchronizationContext"/> once per frame
    /// until <see cref="Time.realtimeSinceStartupAsDouble"/> passes the due time. A thread
    /// timer (<c>Task.Delay</c>, <c>System.Threading.Timer</c>) never fires on Unity WebGL,
    /// which runs on a single thread, so a delay built on one would wait forever there.
    /// Consequences of the frame-loop design: the delay is approximate (rounded up to the
    /// next frame), it counts real time so it keeps running while <c>Time.timeScale</c> is
    /// zero, and it stops advancing while frames stop (background tab, suspended app).
    /// It is meant for coarse waits such as retry backoff, not for precise timing.
    /// </para>
    /// <para>
    /// All dispatched actions are wrapped in try-catch. Exceptions are logged via
    /// <see cref="ILogger"/> at <see cref="LogLevel.Error"/> so that one failing action
    /// does not prevent remaining queued actions from executing.
    /// </para>
    /// <para>
    /// Implements <see cref="IDisposable"/> to cancel pending delayed actions on shutdown.
    /// </para>
    /// </remarks>
    public sealed class UnityDispatcher : IDispatcher, IDisposable, IDispatcherDisposalObserver
    {
        private readonly SynchronizationContext m_context;
        private readonly int m_mainThreadId;
        private readonly ILogger? m_logger;
        private int m_disposed; // 0 = alive, 1 = disposed

        /// <summary>
        /// Raised once, the first time this dispatcher is disposed, so a pending
        /// delayed wait can resolve at dispose time instead of waiting for a tick
        /// that will never come.
        /// </summary>
        public event Action? Disposed;

        /// <summary>
        /// Initializes a new <see cref="UnityDispatcher"/> instance.
        /// Must be called on the main thread so that the correct
        /// <see cref="UnitySynchronizationContext"/> is captured.
        /// </summary>
        /// <param name="logger">
        /// Optional logger for reporting exceptions thrown by dispatched actions.
        /// When <c>null</c>, exceptions are silently swallowed.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="SynchronizationContext.Current"/> is <c>null</c>,
        /// which indicates this was not called from the main thread.
        /// </exception>
        public UnityDispatcher(ILogger? logger = null)
        {
            var current = SynchronizationContext.Current;

            if (current is null)
            {
                throw new InvalidOperationException(
                    "SynchronizationContext.Current is null. " +
                    "UnityDispatcher must be created on the Unity main thread.");
            }

            m_context = current;
            m_mainThreadId = System.Environment.CurrentManagedThreadId;
            m_logger = logger;
        }

        /// <inheritdoc/>
        public bool IsMainThread => System.Environment.CurrentManagedThreadId == m_mainThreadId;

        /// <inheritdoc/>
        public void Post(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Volatile.Read(ref m_disposed) == 1)
            {
                throw new ObjectDisposedException(nameof(UnityDispatcher));
            }

            m_context.Post(_ => ExecuteSafely(action), null);
        }

        /// <inheritdoc/>
        public void PostDelayed(Action action, int delayMillis)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (delayMillis < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMillis), delayMillis,
                    "Delay must not be negative.");
            }

            if (Volatile.Read(ref m_disposed) == 1)
            {
                throw new ObjectDisposedException(nameof(UnityDispatcher));
            }

            // Arm on the main thread: reading Time is main-thread-only and PostDelayed
            // may be called from any thread.
            m_context.Post(_ => ArmDelayed(action, delayMillis), null);
        }

        /// <summary>
        /// Drops all pending delayed actions at their next tick and raises
        /// <see cref="Disposed"/> once, so a waiter that armed one can resolve at
        /// dispose time instead of a tick that will never come. Actions already queued
        /// via <see cref="Post"/> or already executing are not affected and may still
        /// run after disposal. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) == 1)
            {
                return; // already disposed
            }

            Disposed?.Invoke();
        }

        private void ArmDelayed(Action action, int delayMillis)
        {
            double due = Time.realtimeSinceStartupAsDouble + delayMillis / 1000.0;
            TickDelayed(action, due);
        }

        private void TickDelayed(Action action, double due)
        {
            if (Volatile.Read(ref m_disposed) == 1)
            {
                return; // disposed while waiting — abandon the delayed action
            }

            if (Time.realtimeSinceStartupAsDouble >= due)
            {
                ExecuteSafely(action);
                return;
            }

            // Not due yet: check again next frame. The synchronization context runs
            // posted work once per player-loop iteration, so this is one tick per frame.
            m_context.Post(_ => TickDelayed(action, due), null);
        }

        private void ExecuteSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DispatcherErrorHelper.LogActionError(m_logger, ex);
            }
        }
    }
}
