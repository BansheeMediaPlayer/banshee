//
// banshee-player-video.c
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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

#include "banshee-player-video.h"

// ---------------------------------------------------------------------------
// Private Functions
// ---------------------------------------------------------------------------

#ifdef GDK_WINDOWING_X11

static gboolean
bp_video_find_xoverlay (BansheePlayer *player)
{
    GstElement *video_sink = NULL;
    GstElement *xoverlay;
    GstXOverlay *previous_xoverlay;

    previous_xoverlay = player->xoverlay;
    
    g_object_get (player->playbin, "video-sink", &video_sink, NULL);
    
    if (video_sink == NULL) {
        player->xoverlay = NULL;
        if (previous_xoverlay != NULL) {
            gst_object_unref (previous_xoverlay);
        }

        return FALSE;
    }
    
    xoverlay = GST_IS_BIN (video_sink)
        ? gst_bin_get_by_interface (GST_BIN (video_sink), GST_TYPE_X_OVERLAY)
        : video_sink;
    
    player->xoverlay = GST_IS_X_OVERLAY (xoverlay) ? GST_X_OVERLAY (xoverlay) : NULL;
    
    if (previous_xoverlay != NULL) {
        gst_object_unref (previous_xoverlay);
    }
        
    if (player->xoverlay != NULL && g_object_class_find_property (
        G_OBJECT_GET_CLASS (player->xoverlay), "force-aspect-ratio")) {
        g_object_set (G_OBJECT (player->xoverlay), "force-aspect-ratio", TRUE, NULL);
    }
    
    if (player->xoverlay != NULL && g_object_class_find_property (
        G_OBJECT_GET_CLASS (player->xoverlay), "handle-events")) {
        g_object_set (G_OBJECT (player->xoverlay), "handle-events", FALSE, NULL);
    }

    gst_object_unref (video_sink);

    return player->xoverlay != NULL;
}

#endif /* GDK_WINDOWING_X11 */

static void
bp_video_sink_element_added (GstBin *videosink, GstElement *element, BansheePlayer *player)
{
    g_return_if_fail (IS_BANSHEE_PLAYER (player));

    #ifdef GDK_WINDOWING_X11
    g_mutex_lock (player->mutex);
    bp_video_find_xoverlay (player);
    g_mutex_unlock (player->mutex);    
    #endif
}

static void
bp_video_bus_element_sync_message (GstBus *bus, GstMessage *message, BansheePlayer *player)
{
    gboolean found_xoverlay;
    
    g_return_if_fail (IS_BANSHEE_PLAYER (player));

    #ifdef GDK_WINDOWING_X11

    if (message->structure == NULL || !gst_structure_has_name (message->structure, "prepare-xwindow-id")) {
        return;
    }

    g_mutex_lock (player->mutex);
    found_xoverlay = bp_video_find_xoverlay (player);
    g_mutex_unlock (player->mutex);

    if (found_xoverlay) {
        gst_x_overlay_set_xwindow_id (player->xoverlay, GDK_WINDOW_XWINDOW (player->video_window));
    }

    #endif
}

// ---------------------------------------------------------------------------
// Internal Functions
// ---------------------------------------------------------------------------

void
_bp_video_pipeline_setup (BansheePlayer *player, GstBus *bus)
{
    GstElement *videosink;
    
    g_return_if_fail (IS_BANSHEE_PLAYER (player));
    
     if (player->video_pipeline_setup_cb != NULL) {
        videosink = player->video_pipeline_setup_cb (player, bus);
        if (videosink != NULL && GST_IS_ELEMENT (videosink)) {
            g_object_set (G_OBJECT (player->playbin), "video-sink", videosink, NULL);
            player->video_display_context_type = BP_VIDEO_DISPLAY_CONTEXT_CUSTOM;
            return;
        }
    }
    
    #ifdef GDK_WINDOWING_X11

    player->video_display_context_type = BP_VIDEO_DISPLAY_CONTEXT_GDK_WINDOW;
    
    videosink = gst_element_factory_make ("gconfvideosink", "videosink");
    if (videosink == NULL) {
        videosink = gst_element_factory_make ("ximagesink", "videosink");
        if (videosink == NULL) {
            player->video_display_context_type = BP_VIDEO_DISPLAY_CONTEXT_UNSUPPORTED;
            videosink = gst_element_factory_make ("fakesink", "videosink");
            if (videosink != NULL) {
                g_object_set (G_OBJECT (videosink), "sync", TRUE, NULL);
            }
        }
    }
    
    g_object_set (G_OBJECT (player->playbin), "video-sink", videosink, NULL);
    
    gst_bus_set_sync_handler (bus, gst_bus_sync_signal_handler, player);
    g_signal_connect (bus, "sync-message::element", G_CALLBACK (bp_video_bus_element_sync_message), player);
        
    if (GST_IS_BIN (videosink)) {
        g_signal_connect (videosink, "element-added", G_CALLBACK (bp_video_sink_element_added), player);
    }
    
    #else
    
    player->video_display_context_type = BP_VIDEO_DISPLAY_CONTEXT_UNSUPPORTED;

    #ifndef WIN32

    videosink = gst_element_factory_make ("fakesink", "videosink");
    if (videosink != NULL) {
        g_object_set (G_OBJECT (videosink), "sync", TRUE, NULL);
    }
    
    g_object_set (G_OBJECT (player->playbin), "video-sink", videosink, NULL);

    #endif
    
    #endif
}

P_INVOKE void
bp_set_video_pipeline_setup_callback (BansheePlayer *player, BansheePlayerVideoPipelineSetupCallback cb)
{
    SET_CALLBACK (video_pipeline_setup_cb);
}

// ---------------------------------------------------------------------------
// Public Functions
// ---------------------------------------------------------------------------

#ifdef GDK_WINDOWING_X11

P_INVOKE BpVideoDisplayContextType
bp_video_get_display_context_type (BansheePlayer *player)
{
    return player->video_display_context_type;
}

P_INVOKE void
bp_video_set_display_context (BansheePlayer *player, gpointer context)
{
    g_return_if_fail (IS_BANSHEE_PLAYER (player));
    
    if (bp_video_get_display_context_type (player) == BP_VIDEO_DISPLAY_CONTEXT_GDK_WINDOW) {
        player->video_window = (GdkWindow *)context;
    }
}

P_INVOKE gpointer
bp_video_get_display_context (BansheePlayer *player)
{
    g_return_val_if_fail (IS_BANSHEE_PLAYER (player), NULL);
   
    if (bp_video_get_display_context_type (player) == BP_VIDEO_DISPLAY_CONTEXT_GDK_WINDOW) {
        return player->video_window;
    }
    
    return NULL;
}

P_INVOKE void
bp_video_window_expose (BansheePlayer *player, GdkWindow *window, gboolean direct)
{
    XID window_id;
    
    g_return_if_fail (IS_BANSHEE_PLAYER (player));
    
    if (direct && player->xoverlay != NULL && GST_IS_X_OVERLAY (player->xoverlay)) {
        gst_x_overlay_expose (player->xoverlay);
        return;
    }
   
    g_mutex_lock (player->mutex);
   
    if (player->xoverlay == NULL && !bp_video_find_xoverlay (player)) {
        g_mutex_unlock (player->mutex);
        return;
    }
    
    gst_object_ref (player->xoverlay);
    g_mutex_unlock (player->mutex);

    window_id = GDK_WINDOW_XWINDOW (window);

    gst_x_overlay_set_xwindow_id (player->xoverlay, window_id);
    gst_x_overlay_expose (player->xoverlay);

    gst_object_unref (player->xoverlay);
}

#else /* GDK_WINDOWING_X11 */

P_INVOKE BpVideoDisplayContextType
bp_video_get_display_context_type (BansheePlayer *player)
{
    return player->video_display_context_type;
}

P_INVOKE void
bp_video_set_display_context (BansheePlayer *player, gpointer context)
{
}

P_INVOKE gpointer
bp_video_get_display_context (BansheePlayer *player)
{
    return NULL;
}

P_INVOKE void
bp_video_window_expose (BansheePlayer *player, GdkWindow *window, gboolean direct)
{
}

#endif /* GDK_WINDOWING_X11 */
