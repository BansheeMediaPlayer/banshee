/* -*- Mode: csharp; tab-width: 4; c-basic-offset: 4; indent-tabs-mode: t -*- */
/***************************************************************************
 *  NotificationAreaIcon.cs
 *
 * Copyright (C) 2005 Todd Berman <tberman@off.net>
 * Copyright (C) 2005 Ed Catmur <ed@catmur.co.uk>
 * Copyright (C) 2005 Novell, Inc. (Miguel de Icaza, Aaron Bockover)
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

// NOTE: throughout IntPtr is used for the Xlib long type, as this tends to 
// have the correct width and does not require any configure checks.

using System;
using System.Runtime.InteropServices;

using Gtk;
using Gdk;

#pragma warning disable 0169

public class NotificationAreaIcon : Plug
{
    private uint stamp;
    private Orientation orientation;
    
    private int selection_atom;
    private int manager_atom;
    private int system_tray_opcode_atom;
    private int orientation_atom;
    private IntPtr manager_window;
    private FilterFunc filter;
    
    public NotificationAreaIcon (string name)
    {
        Title = name;
        Init ();
    }
    
    public NotificationAreaIcon (string name, Gdk.Screen screen)
    {
        Title = name;
        Screen = screen;
        Init ();
    }

    public uint SendMessage (uint timeout, string message)
    {
        if (manager_window == IntPtr.Zero) {
            return 0;
        }

        SendManagerMessage (SystemTrayMessage.BeginMessage, (IntPtr) Id, timeout, (uint) message.Length, ++stamp);

        gdk_error_trap_push ();
        
        for (int index = 0; index < message.Length; index += 20) {
            XClientMessageEvent ev = new XClientMessageEvent ();
            
            IntPtr display = gdk_x11_display_get_xdisplay (Display.Handle);
            
            ev.type = XEventName.ClientMessage;
            ev.window = (IntPtr) Id;
            ev.format = 8;
            ev.message_type = (IntPtr) XInternAtom (display, "_NET_SYSTEM_TRAY_MESSAGE_DATA", false);
            
            byte [] arr = System.Text.Encoding.UTF8.GetBytes (message.Substring (index));
            int len = Math.Min (arr.Length, 20);
            Marshal.Copy (arr, 0, ev.data.ptr1, len);

            XSendEvent (display, manager_window, false, EventMask.StructureNotifyMask, ref ev);
            XSync (display, false);
        }
        
        gdk_error_trap_pop ();

        return stamp;
    }

    public void CancelMessage (uint id)
    {
        if (id == 0) {
            return;
        }

        SendManagerMessage (SystemTrayMessage.CancelMessage, (IntPtr) Id, id, 0, 0);
    }

    private void Init ()
    {
        stamp = 1;
        orientation = Orientation.Horizontal;
        AddEvents ((int)EventMask.PropertyChangeMask);
        filter = new FilterFunc (ManagerFilter);
    }

    protected override void OnRealized ()
    {
        base.OnRealized ();
        Display display = Screen.Display;
        IntPtr xdisplay = gdk_x11_display_get_xdisplay (display.Handle);
        selection_atom = XInternAtom (xdisplay, "_NET_SYSTEM_TRAY_S" + Screen.Number.ToString (), false);
        manager_atom = XInternAtom (xdisplay, "MANAGER", false);
        system_tray_opcode_atom = XInternAtom (xdisplay, "_NET_SYSTEM_TRAY_OPCODE", false);
        orientation_atom = XInternAtom (xdisplay, "_NET_SYSTEM_TRAY_ORIENTATION", false);
        UpdateManagerWindow (false);
        SendDockRequest ();
        Screen.RootWindow.AddFilter (filter);
    }

    protected override void OnUnrealized ()
    {
        if (manager_window != IntPtr.Zero) {
            Gdk.Window gdkwin = Gdk.Window.ForeignNewForDisplay (Display, (uint)manager_window);
            if (gdkwin != null) {
                gdkwin.RemoveFilter (filter);
            }
        }
        
        Screen.RootWindow.RemoveFilter (filter);
        base.OnUnrealized ();
    }

    private void UpdateManagerWindow (bool dock_if_realized)
    {
        IntPtr xdisplay = gdk_x11_display_get_xdisplay (Display.Handle);

        if (manager_window != IntPtr.Zero) {
            return;
        }
        
        XGrabServer (xdisplay);

        manager_window = XGetSelectionOwner (xdisplay, selection_atom);
        if (manager_window != IntPtr.Zero) {
            XSelectInput (xdisplay, manager_window, EventMask.StructureNotifyMask | EventMask.PropertyChangeMask);
        }
        
        XUngrabServer (xdisplay);
        XFlush (xdisplay);

        if (manager_window != IntPtr.Zero) {
            Gdk.Window gdkwin = Gdk.Window.ForeignNewForDisplay (Display, (uint)manager_window);
            if (gdkwin != null) {
                gdkwin.AddFilter (filter);
            }
            
            if (dock_if_realized && IsRealized) {
                SendDockRequest ();
            }
            
            GetOrientationProperty ();
        }
    }

    private void SendDockRequest ()
    {
        SendManagerMessage (SystemTrayMessage.RequestDock, manager_window, Id, 0, 0);
    }

    private void SendManagerMessage (SystemTrayMessage message, IntPtr window, uint data1, uint data2, uint data3)
    {
        XClientMessageEvent ev = new XClientMessageEvent ();
        IntPtr display;

        ev.type = XEventName.ClientMessage;
        ev.window = window;
        ev.message_type = (IntPtr)system_tray_opcode_atom;
        ev.format = 32;
        ev.data.ptr1 = gdk_x11_get_server_time (GdkWindow.Handle);
        ev.data.ptr2 = (IntPtr)message;
        ev.data.ptr3 = (IntPtr)data1;
        ev.data.ptr4 = (IntPtr)data2;
        ev.data.ptr5 = (IntPtr)data3;

        display = gdk_x11_display_get_xdisplay (Display.Handle);
        gdk_error_trap_push ();
        XSendEvent (display, manager_window, false, EventMask.NoEventMask, ref ev);
        XSync (display, false);
        gdk_error_trap_pop ();
    }

    private FilterReturn ManagerFilter (IntPtr xevent, Event evnt)
    {
        XEvent xev = (XEvent) Marshal.PtrToStructure (xevent, typeof(XEvent));
        
        if (xev.xany.type == XEventName.ClientMessage 
                && (int) xev.xclient.message_type == manager_atom 
                && (int) xev.xclient.data.ptr2 == selection_atom) {
            UpdateManagerWindow (true);
        } else if (xev.xany.window == manager_window) {
            if (xev.xany.type == XEventName.PropertyNotify && xev.xproperty.atom == orientation_atom) {
                GetOrientationProperty();
            } else if (xev.xany.type == XEventName.DestroyNotify) {
                ManagerWindowDestroyed();
            }
        }
        
        return FilterReturn.Continue;
    }

    private void ManagerWindowDestroyed ()
    {
        if (manager_window != IntPtr.Zero) {
            Gdk.Window gdkwin = Gdk.Window.ForeignNewForDisplay (Display, (uint) manager_window);
            
            if (gdkwin != null) {
                gdkwin.RemoveFilter (filter);
            }
            
            manager_window = IntPtr.Zero;
            UpdateManagerWindow (true);
        }
    }

    private void GetOrientationProperty ()
    {
        IntPtr display;
        int type;
        int format;
        IntPtr prop_return;
        IntPtr nitems, bytes_after;
        int error, result;

        if (manager_window == IntPtr.Zero) {
            return;
        }

        display = gdk_x11_display_get_xdisplay (Display.Handle);
        
        gdk_error_trap_push ();
        type = 0;
        
        result = XGetWindowProperty (display, manager_window, orientation_atom, (IntPtr) 0, 
            (IntPtr) System.Int32.MaxValue, false, (int) XAtom.Cardinal, out type, out format, 
            out nitems, out bytes_after, out prop_return);
        
        error = gdk_error_trap_pop ();

        if (error != 0 || result != 0) {
            return;
        }

        if (type == (int) XAtom.Cardinal) {
            orientation = ((SystemTrayOrientation) Marshal.ReadInt32 (prop_return) == SystemTrayOrientation.Horz) 
                ? Orientation.Horizontal 
                : Orientation.Vertical;
        }

        if (prop_return != IntPtr.Zero) {
            XFree (prop_return);
        }
    }

    [DllImport ("gdk-x11-2.0")]
    private static extern IntPtr gdk_x11_display_get_xdisplay (IntPtr display);
    
    [DllImport ("gdk-x11-2.0")]
    private static extern IntPtr gdk_x11_get_server_time (IntPtr window);
    
    [DllImport ("gdk-x11-2.0")]
    private static extern void gdk_error_trap_push ();
    
    [DllImport ("gdk-x11-2.0")]
    private static extern int gdk_error_trap_pop ();
    
    [DllImport ("libX11", EntryPoint="XInternAtom")]
    private extern static int XInternAtom(IntPtr display, string atom_name, bool only_if_exists);
    
    [DllImport ("libX11")]
    private extern static void XGrabServer (IntPtr display);
    
    [DllImport ("libX11")]
    private extern static void XUngrabServer (IntPtr display);
    
    [DllImport ("libX11")]
    private extern static int XFlush (IntPtr display);
    
    [DllImport ("libX11")]
    private extern static int XSync (IntPtr display, bool discard);
    
    [DllImport ("libX11")]
    private extern static int XFree (IntPtr display);
    
    [DllImport ("libX11")]
    private extern static IntPtr XGetSelectionOwner (IntPtr display, int atom);
    
    [DllImport ("libX11")]
    private extern static IntPtr XSelectInput (IntPtr window, IntPtr display, EventMask mask);
    
    [DllImport ("libX11", EntryPoint="XSendEvent")]
    private extern static int XSendEvent(IntPtr display, IntPtr window, bool propagate, EventMask event_mask, 
        ref XClientMessageEvent send_event);
        
    [DllImport("libX11")]
    private extern static int XGetWindowProperty(IntPtr display, IntPtr w, int property, IntPtr long_offset, 
        IntPtr long_length, bool deleteProp, int req_type, out int actual_type_return, out int actual_format_return, 
        out IntPtr nitems_return, out IntPtr bytes_after_return, out IntPtr prop_return);

	[Flags]
	private enum EventMask {
	    NoEventMask              = 0,
	    KeyPressMask             = 1 << 0,
	    KeyReleaseMask           = 1 << 1,
	    ButtonPressMask          = 1 << 2,
	    ButtonReleaseMask        = 1 << 3,
	    EnterWindowMask          = 1 << 4,
	    LeaveWindowMask          = 1 << 5,
	    PointerMotionMask        = 1 << 6,
	    PointerMotionHintMask    = 1 << 7,
	    Button1MotionMask        = 1 << 8,
	    Button2MotionMask        = 1 << 9,
	    Button3MotionMask        = 1 << 10,
	    Button4MotionMask        = 1 << 11,
	    Button5MotionMask        = 1 << 12,
	    ButtonMotionMask         = 1 << 13,
	    KeymapStateMask          = 1 << 14,
	    ExposureMask             = 1 << 15,
	    VisibilityChangeMask     = 1 << 16,
	    StructureNotifyMask      = 1 << 17,
	    ResizeRedirectMask       = 1 << 18,
	    SubstructureNotifyMask   = 1 << 19,
	    SubstructureRedirectMask = 1 << 20,
	    FocusChangeMask          = 1 << 21,
	    PropertyChangeMask       = 1 << 22,
	    ColormapChangeMask       = 1 << 23,
	    OwnerGrabButtonMask      = 1 << 24
	}

	private enum SystemTrayMessage {
	    RequestDock = 0,
	    BeginMessage = 1,
	    CancelMessage = 2
	}

	private enum SystemTrayOrientation {
	    Horz = 0,
	    Vert = 1
	}

	private enum XEventName {
	    KeyPress                = 2,
	    KeyRelease              = 3,
	    ButtonPress             = 4,
	    ButtonRelease           = 5,
	    MotionNotify            = 6,
	    EnterNotify             = 7,
	    LeaveNotify             = 8,
	    FocusIn                 = 9,
	    FocusOut                = 10,
	    KeymapNotify            = 11,
	    Expose                  = 12,
	    GraphicsExpose          = 13,
	    NoExpose                = 14,
	    VisibilityNotify        = 15,
	    CreateNotify            = 16,
	    DestroyNotify           = 17,
	    UnmapNotify             = 18,
	    MapNotify               = 19,
	    MapRequest              = 20,
	    ReparentNotify          = 21,
	    ConfigureNotify         = 22,
	    ConfigureRequest        = 23,
	    GravityNotify           = 24,
	    ResizeRequest           = 25,
	    CirculateNotify         = 26,
	    CirculateRequest        = 27,
	    PropertyNotify          = 28,
	    SelectionClear          = 29,
	    SelectionRequest        = 30,
	    SelectionNotify         = 31,
	    ColormapNotify          = 32,
	    ClientMessage           = 33,
	    MappingNotify           = 34,
	    TimerNotify             = 100,
	    LASTEvent
	}

	private enum XAtom {
	    Cardinal                = 6,
	    LASTAtom
	}
	
	[StructLayout(LayoutKind.Explicit)]
	private struct XEvent 
	{
	    [FieldOffset(0)] public XAnyEvent            xany;
	    [FieldOffset(0)] public XPropertyEvent       xproperty;
	    [FieldOffset(0)] public XClientMessageEvent  xclient;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct XAnyEvent 
	{
	    internal XEventName    type;
	    internal IntPtr        serial;
	    internal bool          send_event;
	    internal IntPtr        display;
	    internal IntPtr        window;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct XPropertyEvent 
	{
	    internal XEventName    type;
	    internal IntPtr        serial;
	    internal bool          send_event;
	    internal IntPtr        display;
	    internal IntPtr        window;
	    internal int           atom;
	    internal IntPtr        time;
	    internal int           state;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct XClientMessageEvent 
	{
	    internal XEventName     type;
	    internal IntPtr         serial;
	    internal bool           send_event;
	    internal IntPtr         display;
	    internal IntPtr         window;
	    internal IntPtr         message_type;
	    internal int            format;
	    
	    [StructLayout(LayoutKind.Explicit)]
	    internal struct DataUnion 
	    {
	        [FieldOffset(0)]  internal IntPtr ptr1;
	        [FieldOffset(4)]  internal IntPtr ptr2;
	        [FieldOffset(8)]  internal IntPtr ptr3;
	        [FieldOffset(12)] internal IntPtr ptr4;
	        [FieldOffset(16)] internal IntPtr ptr5;
	    }
	    
	    internal DataUnion      data;
	}
}

#pragma warning restore 0169

