using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Describes a native modal dialog blocking the Editor's main thread.
    /// </summary>
    internal sealed class ModalDialogInfo
    {
        public bool Supported { get; set; }
        public bool Blocked { get; set; }

        /// <summary>
        /// "dialog" for a native <c>EditorUtility.DisplayDialog</c> box, whose buttons are real OS
        /// controls; "editor_window" for a Unity-drawn modal (<c>EditorWindow.ShowModal</c>), which
        /// blocks identically but paints its own buttons with IMGUI.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>Whether a specific button can actually be pressed programmatically.</summary>
        public bool Answerable { get; set; }

        public long Handle { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public List<string> Buttons { get; } = new List<string>();
    }

    /// <summary>
    /// Reads (and can answer) the native modal dialog that blocks Unity's main thread.
    ///
    /// Windows only. <c>EditorUtility.DisplayDialog</c> raises a standard Win32 dialog (class
    /// <c>#32770</c>) owned by the Editor process, whose title, body and button labels are all
    /// readable, and whose buttons are real <c>Button</c> controls that accept <c>BM_CLICK</c>.
    ///
    /// Every text read goes through <see cref="SendMessageTimeoutW"/>: <c>GetWindowText</c> on a
    /// window owned by the calling process is a synchronous <c>WM_GETTEXT</c>, which hangs when the
    /// owning thread is not pumping messages — exactly the case this probe reports on.
    /// </summary>
    internal static class ModalDialogProbe
    {
        private const string DialogWindowClass = "#32770";
        private const string ButtonWindowClass = "Button";
        private const uint WM_GETTEXT = 0x000D;
        private const uint BM_CLICK = 0x00F5;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint GW_OWNER = 4;
        private const int AnswerVerificationTimeoutMs = 2000;
        private const int TextReadTimeoutMs = 400;
        internal const int MaxCaptureDurationMs = 2500;
        private const int MaxAnswerScanDurationMs = 2500;
        private const int MaxTopLevelScan = 5000;
        private const int MaxChildScan = 200;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter, IntPtr className, IntPtr windowName);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder buffer, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeoutW(
            IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam, uint flags, uint timeoutMs, out IntPtr result);

        [DllImport("user32.dll")]
        private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // EditorLivenessProbe's [InitializeOnLoad] constructor first touches this type on the
        // Editor thread. The explicit static constructor prevents beforefieldinit from moving this
        // UnityEngine query onto the background sampler thread.
        private static readonly bool PlatformSupported;

        static ModalDialogProbe()
        {
            PlatformSupported = Application.platform == RuntimePlatform.WindowsEditor;
        }

        internal static bool IsSupported => PlatformSupported;

        /// <summary>
        /// Look for a modal dialog owned by this process. Safe to call from a background thread.
        /// </summary>
        internal static ModalDialogInfo Capture()
        {
            var info = new ModalDialogInfo { Supported = IsSupported };
            if (!info.Supported)
            {
                return info;
            }

            var captureClock = Stopwatch.StartNew();
            try
            {
                uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

                // Modal UI must be an owned window whose owner chain contains a disabled
                // window in this process. A disabled top-level window alone is insufficient:
                // the Editor can contain several windows plus unrelated disabled windows.
                var enabled = new List<IntPtr>();
                bool anyDisabled = false;
                IntPtr window = IntPtr.Zero;
                for (int i = 0; i < MaxTopLevelScan; i++)
                {
                    if (captureClock.ElapsedMilliseconds >= MaxCaptureDurationMs)
                    {
                        info.Supported = false;
                        return info;
                    }
                    window = FindWindowExW(IntPtr.Zero, window, IntPtr.Zero, IntPtr.Zero);
                    if (window == IntPtr.Zero) break;
                    GetWindowThreadProcessId(window, out uint pid);
                    if (pid != myPid || !IsWindowVisible(window)) continue;
                    if (IsWindowEnabled(window)) enabled.Add(window);
                    else anyDisabled = true;
                }
                if (!anyDisabled || enabled.Count == 0) return info;

                IntPtr modal = IntPtr.Zero;
                foreach (var candidate in enabled)
                {
                    if (captureClock.ElapsedMilliseconds >= MaxCaptureDurationMs)
                    {
                        info.Supported = false;
                        return info;
                    }
                    if (string.Equals(ClassOf(candidate), DialogWindowClass, StringComparison.Ordinal)
                        && IsOwnedByDisabledWindow(candidate, myPid))
                    {
                        modal = candidate;
                        break;
                    }
                }

                bool isNativeDialog = modal != IntPtr.Zero;
                if (!isNativeDialog)
                {
                    foreach (var candidate in enabled)
                    {
                        if (captureClock.ElapsedMilliseconds >= MaxCaptureDurationMs)
                        {
                            info.Supported = false;
                            return info;
                        }
                        if (IsOwnedByDisabledWindow(candidate, myPid))
                        {
                            modal = candidate;
                            break;
                        }
                    }
                }
                if (modal == IntPtr.Zero)
                {
                    // We have evidence of a disabled Editor window but cannot positively identify
                    // its modal owner. Preserve the blocked signal so the server requests a human
                    // inspection rather than reporting an ordinary busy state and waiting forever.
                    // The unidentified window is never programmatically answerable.
                    info.Blocked = true;
                    info.Kind = "unknown";
                    info.Answerable = false;
                    info.Title = "Unidentified modal window";
                    return info;
                }

                info.Blocked = true;
                info.Handle = modal.ToInt64();
                info.Title = TextOf(modal);
                info.Kind = isNativeDialog ? "dialog" : "editor_window";

                if (!isNativeDialog)
                {
                    // EditorWindow.ShowModal paints its buttons with IMGUI, so there are no child
                    // controls to enumerate. Measured: it keeps repainting while blocked yet ignores
                    // synthetic WM_LBUTTONDOWN/UP from both PostMessage and SendMessage, activated or
                    // not. Reported as blocked, but not answerable.
                    return info;
                }

                var bodyParts = new List<string>();
                foreach (var child in ChildrenOf(modal))
                {
                    if (captureClock.ElapsedMilliseconds >= MaxCaptureDurationMs)
                    {
                        // We did detect a native modal, but cannot safely guarantee all labels were
                        // read inside the budget. Report it as not answerable rather than guess.
                        info.Answerable = false;
                        return info;
                    }

                    string cls = ClassOf(child);
                    string text = TextOf(child);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (string.Equals(cls, ButtonWindowClass, StringComparison.Ordinal))
                    {
                        info.Buttons.Add(StripAccelerator(text));
                    }
                    else
                    {
                        bodyParts.Add(text);
                    }
                }

                info.Body = bodyParts.Count > 0 ? string.Join("\n", bodyParts.ToArray()) : null;
                info.Answerable = info.Buttons.Count > 0;
            }
            catch (Exception ex)
            {
                // A failed sample must not become a confident no-modal report.
                info.Supported = false;
                McpLog.Warn($"Modal dialog probe failed: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Press a button on the currently open modal dialog. Safe to call from a background thread.
        /// The dialog is re-identified and its title re-checked so the press cannot land on a
        /// different dialog than the caller was shown.
        /// </summary>
        private static readonly object AnswerLock = new object();

        internal static bool TryAnswer(string expectedTitle, string buttonLabel, out string error, out ModalDialogInfo observed)
        {
            // This path deliberately bypasses the main-thread command queue. Do not let two recovery
            // calls click the same transient modal, and do not block a second request until its
            // caller times out and then perform the action later.
            observed = new ModalDialogInfo
            {
                Supported = IsSupported,
                Blocked = true,
                Kind = "unknown",
                Answerable = false,
                Title = "Modal answer already in progress"
            };
            error = "Another modal-answer request is already in progress.";
            if (!System.Threading.Monitor.TryEnter(AnswerLock))
            {
                return false;
            }

            try
            {
                return TryAnswerLocked(expectedTitle, buttonLabel, out error, out observed);
            }
            finally
            {
                System.Threading.Monitor.Exit(AnswerLock);
            }
        }

        private static bool TryAnswerLocked(string expectedTitle, string buttonLabel, out string error, out ModalDialogInfo observed)
        {
            observed = Capture();
            error = null;

            if (!observed.Supported)
            {
                error = "Answering modal dialogs is only supported in the Windows Editor.";
                return false;
            }

            if (!observed.Blocked)
            {
                error = "No modal dialog is currently open in the Unity Editor.";
                return false;
            }

            if (!observed.Answerable)
            {
                error = $"Modal window '{observed.Title}' can't be answered programmatically; dismiss it in the Editor.";
                return false;
            }

            if (!string.IsNullOrEmpty(expectedTitle)
                && !string.Equals(expectedTitle, observed.Title, StringComparison.Ordinal))
            {
                error = $"Dialog changed: expected '{expectedTitle}' but '{observed.Title}' is open.";
                return false;
            }

            var answerClock = Stopwatch.StartNew();
            var handle = new IntPtr(observed.Handle);
            if (!IsWindow(handle))
            {
                error = "The dialog closed before it could be answered.";
                return false;
            }

            foreach (var child in ChildrenOf(handle))
            {
                if (answerClock.ElapsedMilliseconds >= MaxAnswerScanDurationMs)
                {
                    error = $"Timed out while looking for button '{buttonLabel}' on dialog '{observed.Title}'.";
                    return false;
                }

                if (!string.Equals(ClassOf(child), ButtonWindowClass, StringComparison.Ordinal)
                    || !IsWindowVisible(child) || !IsWindowEnabled(child))
                {
                    continue;
                }

                if (!string.Equals(StripAccelerator(TextOf(child)), buttonLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!PostMessageW(child, BM_CLICK, IntPtr.Zero, IntPtr.Zero))
                {
                    error = $"Windows rejected the click request for button '{buttonLabel}' on dialog '{observed.Title}'.";
                    return false;
                }

                // PostMessage only proves Windows queued the click. Verify the target dialog closes.
                var verifyClock = Stopwatch.StartNew();
                while (verifyClock.ElapsedMilliseconds < AnswerVerificationTimeoutMs)
                {
                    if (!IsWindow(handle)) return true;
                    System.Threading.Thread.Sleep(50);
                }
                observed = Capture();
                error = $"Windows accepted the click for '{buttonLabel}', but the original dialog remained open; no successful answer is reported.";
                return false;
            }

            error = $"Dialog '{observed.Title}' has no button labelled '{buttonLabel}'. Available: {string.Join(", ", observed.Buttons.ToArray())}";
            return false;
        }

        private static bool IsOwnedByDisabledWindow(IntPtr window, uint processId)
        {
            return HasDisabledOwner(
                window,
                processId,
                handle => GetWindow(handle, GW_OWNER),
                IsWindowEnabled,
                handle =>
                {
                    GetWindowThreadProcessId(handle, out uint ownerProcessId);
                    return ownerProcessId;
                });
        }

        /// <summary>Pure owner-chain predicate used by the probe and deterministic regression tests.</summary>
        internal static bool HasDisabledOwner(
            IntPtr window,
            uint processId,
            Func<IntPtr, IntPtr> getOwner,
            Func<IntPtr, bool> isWindowEnabled,
            Func<IntPtr, uint> getProcessId)
        {
            if (window == IntPtr.Zero || getOwner == null || isWindowEnabled == null || getProcessId == null)
                return false;
            IntPtr owner = getOwner(window);
            for (int depth = 0; owner != IntPtr.Zero && depth < 16; depth++)
            {
                if (getProcessId(owner) != processId) return false;
                if (!isWindowEnabled(owner)) return true;
                owner = getOwner(owner);
            }
            return false;
        }

        private static List<IntPtr> ChildrenOf(IntPtr parent)
        {
            var children = new List<IntPtr>();
            IntPtr child = IntPtr.Zero;
            for (int i = 0; i < MaxChildScan; i++)
            {
                child = FindWindowExW(parent, child, IntPtr.Zero, IntPtr.Zero);
                if (child == IntPtr.Zero)
                {
                    break;
                }

                children.Add(child);
            }

            return children;
        }

        private static string ClassOf(IntPtr hWnd)
        {
            var buffer = new StringBuilder(256);
            GetClassNameW(hWnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private static string TextOf(IntPtr hWnd)
        {
            var buffer = new StringBuilder(2048);
            IntPtr sent = SendMessageTimeoutW(
                hWnd, WM_GETTEXT, new IntPtr(buffer.Capacity), buffer, SMTO_ABORTIFHUNG, TextReadTimeoutMs, out _);
            return sent == IntPtr.Zero ? string.Empty : buffer.ToString();
        }

        private static string StripAccelerator(string label)
        {
            return string.IsNullOrEmpty(label) ? label : label.Replace("&", string.Empty);
        }
    }
}
