//
// ThumbnailToolbarManager.cs
//
// Authors:
//   Pete Johanson <peter@peterjohanson.com>
//
// Copyright (C) 2010 Pete Johanson
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Windows7Support
{
    public static class ThumbnailToolbarManager
    {
        static ThumbnailToolbarManager ()
        {
            toolbar_created_msg = RegisterWindowMessage (TASKBAR_BUTTON_CREATED_MSG);
        }


        public static void Register (IntPtr hwnd, Action<ThumbnailToolbar> toolbar_created_callback)
        {
            Win32WinProc proc = new Win32WinProc (WndProc);
            orig_winproc_dict[hwnd] = new Registration ()
            {
                ManagedProc = proc,
                OrigWinProc = SetWindowLongW (hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate (proc)),
                CreationCallback = toolbar_created_callback
            };
        }

        static Dictionary<IntPtr, ThumbnailToolbar> toolbars = new Dictionary<IntPtr, ThumbnailToolbar> ();
        static Dictionary<IntPtr, Registration> orig_winproc_dict = new Dictionary<IntPtr, Registration> ();

        private static IntPtr WndProc (IntPtr hWnd, int Msg, int wParam, int lParam)
        {
            Registration reg = null;

            if (!orig_winproc_dict.TryGetValue (hWnd, out reg))
                return IntPtr.Zero;

            if (Msg == toolbar_created_msg) {

                ThumbnailToolbar tb = new ThumbnailToolbar (hWnd);
                toolbars[hWnd] = tb;
                reg.CreationCallback (tb);

            } else if (Msg == WM_COMMAND && HiWord (wParam) == THBN_CLICKED) {
                int button_id = LoWord (wParam);
                ThumbnailToolbar tb;

                if (toolbars.TryGetValue (hWnd, out tb)) {
                    ThumbnailToolbarButton button = tb.Buttons.SingleOrDefault (b => b.Id == button_id);
                    if (button != null)
                        button.FireClicked ();
                }
            }

            return CallWindowProcW (reg.OrigWinProc, hWnd, Msg, wParam, lParam);
        }

        static int LoWord (int dw)
        {
            return dw & 0xFFFF;
        }

        static int HiWord (int dw)
        {
            return (dw >> 16) & 0xFFFF;
        }

        class Registration
        {
            public Win32WinProc ManagedProc;
            public IntPtr OrigWinProc;
            public Action<ThumbnailToolbar> CreationCallback;
        }

        #region native imports

        static readonly uint toolbar_created_msg;

        const string TASKBAR_BUTTON_CREATED_MSG = "TaskbarButtonCreated";
        const int GWL_WNDPROC = -4;
        const int WM_COMMAND = 0x0111;
        const int THBN_CLICKED = 0x1800;

        private delegate IntPtr Win32WinProc (IntPtr hWnd, int Msg, int wParam, int lParam);

        internal const string User32Library = "user32.dll";

        [DllImport (User32Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SetWindowLongW (IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport (User32Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CallWindowProcW (IntPtr proc, IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport (User32Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint RegisterWindowMessage (string lpProcName);

        #endregion
    }
}
