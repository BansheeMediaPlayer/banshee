//
// banshee-player-pipeline.c
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Julien Moutte <julien@fluendo.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
// Copyright (C) 2010 Fluendo S.A.
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

#include "banshee-player-pipeline.h"
#include "banshee-player-cdda.h"
#include "banshee-player-dvd.h"
#include "banshee-player-video.h"
#include "banshee-player-equalizer.h"
#include "banshee-player-missing-elements.h"
#include "banshee-player-replaygain.h"
#include "banshee-player-vis.h"

// ---------------------------------------------------------------------------
// Private Functions
// ---------------------------------------------------------------------------

static gboolean
bp_stream_has_video (GstElement *playbin)
{
    int n_video;
    g_object_get (G_OBJECT (playbin), "n-video", &n_video, NULL);
    return n_video > 0;
}


static void
bp_pipeline_process_tag (const GstTagList *tag_list, const gchar *tag_name, BansheePlayer *player)
{
    const GValue *value;
    gint value_count;
    
    g_return_if_fail (IS_BANSHEE_PLAYER (player));

    value_count = gst_tag_list_get_tag_size (tag_list, tag_name);
    if (value_count < 1) {
        return;
    }
    
    value = gst_tag_list_get_value_index (tag_list, tag_name, 0);

    if (value != NULL && player->tag_found_cb != NULL) {
        player->tag_found_cb (player, tag_name, value);
    }
}

static void
playbin_stream_changed_cb (GstElement * element, BansheePlayer *player)
{
    GstMessage *msg;

    // We're being called from the streaming thread, so don't do anything here
    msg = gst_message_new_application (GST_OBJECT (player->playbin), gst_structure_new ("stream-changed", NULL));
    gst_element_post_message (player->playbin, msg);
}

static gboolean
bp_next_track_starting (BansheePlayer *player)
{
    gboolean has_video;

    g_return_val_if_fail (IS_BANSHEE_PLAYER (player), FALSE);
    g_return_val_if_fail (GST_IS_ELEMENT (player->playbin), FALSE);

    // FIXME: Work around BGO #602437 - gapless transition between tracks with 
    // video streams results in broken behaviour - most obviously, huge A/V
    // sync issues.
    // Will be in GStreamer 0.10.31
    has_video = bp_stream_has_video (player->playbin);
    if (player->in_gapless_transition && has_video) {
        gchar *uri;
    
        bp_debug ("[Gapless]: Aborting gapless transition to stream with video.  Triggering normal track change");
        g_object_get (G_OBJECT (player->playbin), "uri", &uri, NULL);
        gst_element_set_state (player->playbin, GST_STATE_READY);
        
        g_object_set (G_OBJECT (player->playbin), "uri", uri, NULL);
        gst_element_set_state (player->playbin, GST_STATE_PLAYING);
        g_free (uri);
        player->in_gapless_transition = FALSE;
        // The transition to playing will happen asynchronously, and will trigger
        // a second track-starting message.  Stop processing this one.
        return FALSE;
    }
    player->in_gapless_transition = FALSE;

    if (player->next_track_starting_cb != NULL) {
        bp_debug ("[gapless] Triggering track-change signal");
        player->next_track_starting_cb (player);
    }
    return FALSE;
}

static gboolean
bp_pipeline_bus_callback (GstBus *bus, GstMessage *message, gpointer userdata)
{
    BansheePlayer *player = (BansheePlayer *)userdata;

    g_return_val_if_fail (IS_BANSHEE_PLAYER (player), FALSE);
    g_return_val_if_fail (message != NULL, FALSE);
    
    switch (GST_MESSAGE_TYPE (message)) {
        case GST_MESSAGE_EOS: {
            if (player->eos_cb != NULL) {
                player->eos_cb (player);
            }
            break;
        }
            
        case GST_MESSAGE_STATE_CHANGED: {
            GstState old, new, pending;
            gst_message_parse_state_changed (message, &old, &new, &pending);
            
            _bp_missing_elements_handle_state_changed (player, old, new);
            
            if (player->state_changed_cb != NULL && GST_MESSAGE_SRC (message) == GST_OBJECT (player->playbin)) {
                player->state_changed_cb (player, old, new, pending);
            }
            break;
        }
        
        case GST_MESSAGE_BUFFERING: {
            const GstStructure *buffering_struct;
            gint buffering_progress = 0;
            
            buffering_struct = gst_message_get_structure (message);
            if (!gst_structure_get_int (buffering_struct, "buffer-percent", &buffering_progress)) {
                g_warning ("Could not get completion percentage from BUFFERING message");
                break;
            }
            
            if (buffering_progress >= 100) {
                player->buffering = FALSE;
                if (player->target_state == GST_STATE_PLAYING) {
                    gst_element_set_state (player->playbin, GST_STATE_PLAYING);
                }
            } else if (!player->buffering && player->target_state == GST_STATE_PLAYING) {
                GstState current_state;
                gst_element_get_state (player->playbin, &current_state, NULL, 0);
                if (current_state == GST_STATE_PLAYING) {
                    gst_element_set_state (player->playbin, GST_STATE_PAUSED);
                }
                player->buffering = TRUE;
            } 

            if (player->buffering_cb != NULL) {
                player->buffering_cb (player, buffering_progress);
            }
            break;
        }
        
        case GST_MESSAGE_TAG: {
            GstTagList *tags;
            
            if (GST_MESSAGE_TYPE (message) != GST_MESSAGE_TAG) {
                break;
            }
            
            gst_message_parse_tag (message, &tags);
            
            if (GST_IS_TAG_LIST (tags)) {
                gst_tag_list_foreach (tags, (GstTagForeachFunc)bp_pipeline_process_tag, player);
                gst_tag_list_free (tags);
            }
            break;
        }
    
        case GST_MESSAGE_ERROR: {
            GError *error;
            gchar *debug;
            
            _bp_pipeline_destroy (player);
            
            if (player->error_cb != NULL) {
                gst_message_parse_error (message, &error, &debug);
                player->error_cb (player, error->domain, error->code, error->message, debug);
                g_error_free (error);
                g_free (debug);
            }
            
            break;
        } 
        
        case GST_MESSAGE_ELEMENT: {
            const GstStructure *messageStruct;
            messageStruct = gst_message_get_structure (message);
            if (GST_MESSAGE_SRC (message) == GST_OBJECT (player->playbin) && gst_structure_has_name (messageStruct, "playbin2-stream-changed")) {
                bp_next_track_starting (player);
            }
            _bp_missing_elements_process_message (player, message);
            _bp_dvd_elements_process_message (player, message);
            break;
        }

        case GST_MESSAGE_APPLICATION: {
            const gchar * name;
            const GstStructure * s = gst_message_get_structure (message);
            name = gst_structure_get_name (s);
            if (name && !strcmp (name, "stream-changed")) {
                _bp_parse_stream_info (player);
            }
            break;
        }
        
        default: break;
    }
    
    return TRUE;
}

#ifdef ENABLE_GAPLESS
static void bp_about_to_finish_callback (GstElement *playbin, BansheePlayer *player)
{
    g_return_if_fail (IS_BANSHEE_PLAYER (player));
    g_return_if_fail (GST_IS_ELEMENT (playbin));

    if (bp_stream_has_video (playbin)) {
        bp_debug ("[Gapless]: Not attempting gapless transition from stream with video");
        return;
    }

    if (player->about_to_finish_cb != NULL) {
        player->in_gapless_transition = TRUE;

        bp_debug ("[Gapless] Requesting next track");
        player->about_to_finish_cb (player);
    }
}
#endif //ENABLE_GAPLESS

static void bp_volume_changed_callback (GstElement *playbin, GParamSpec *spec, BansheePlayer *player)
{
    gdouble volume;

    g_return_if_fail (IS_BANSHEE_PLAYER (player));
    g_return_if_fail (GST_IS_ELEMENT (playbin));

    g_object_get (G_OBJECT (playbin), "volume", &volume, NULL);

    player->current_volume = volume;

    if (player->volume_changed_cb != NULL) {
        player->volume_changed_cb (player, volume);
    }
}

// ---------------------------------------------------------------------------
// Internal Functions
// ---------------------------------------------------------------------------

gboolean 
_bp_pipeline_construct (BansheePlayer *player)
{
    GstBus *bus;
    GstPad *teepad;
    GstPad *pad;
    GstElement *audiosink;
    GstElement *audiosinkqueue;
    GstElement *eq_audioconvert = NULL;
    GstElement *eq_audioconvert2 = NULL;
    
    g_return_val_if_fail (IS_BANSHEE_PLAYER (player), FALSE);
    
    // Playbin is the core element that handles autoplugging (finding the right
    // source and decoder elements) based on source URI and stream content
    player->playbin = gst_element_factory_make ("playbin2", "playbin");

#ifdef ENABLE_GAPLESS
    // FIXME: Connect a proxy about-to-finish callback that will generate a next-track-starting callback.
    // This can be removed once playbin2 generates its own next-track signal.
    // bgo#584987 - this is included in >= 0.10.26
    g_signal_connect (player->playbin, "about-to-finish", G_CALLBACK (bp_about_to_finish_callback), player);
#endif //ENABLE_GAPLESS

    g_return_val_if_fail (player->playbin != NULL, FALSE);

    g_signal_connect (player->playbin, "notify::volume", G_CALLBACK (bp_volume_changed_callback), player);
    g_signal_connect (player->playbin, "video-changed", G_CALLBACK (playbin_stream_changed_cb), player);
    g_signal_connect (player->playbin, "audio-changed", G_CALLBACK (playbin_stream_changed_cb), player);
    g_signal_connect (player->playbin, "text-changed", G_CALLBACK (playbin_stream_changed_cb), player);

    // Try to find an audio sink, prefer gconf, which typically is set to auto these days,
    // fall back on auto, which should work on windows, and as a last ditch, try alsa
    audiosink = gst_element_factory_make ("gconfaudiosink", "audiosink");
    if (audiosink == NULL) {
        audiosink = gst_element_factory_make ("directsoundsink", "audiosink");
        if (audiosink != NULL) {
            g_object_set (G_OBJECT (audiosink), "volume", 1.0, NULL);
        } else {
            audiosink = gst_element_factory_make ("autoaudiosink", "audiosink");
            if (audiosink == NULL) {
                audiosink = gst_element_factory_make ("alsasink", "audiosink");
            }
        }
    }
    
    g_return_val_if_fail (audiosink != NULL, FALSE);

    // Set the profile to "music and movies" (gst-plugins-good 0.10.3)
    if (g_object_class_find_property (G_OBJECT_GET_CLASS (audiosink), "profile")) {
        g_object_set (G_OBJECT (audiosink), "profile", 1, NULL);
    }

    /* Set the audio sink to READY so it can autodetect the right sink element
     * if needed, as this allows us to correctly determine whether it has a
     * volume */
    gst_element_set_state (audiosink, GST_STATE_READY);

    // See if the audiosink has a 'volume' property.  If it does, we assume it saves and restores
    // its volume information - and that we shouldn't
    player->audiosink_has_volume = FALSE;
    if (!GST_IS_BIN (audiosink)) {
        player->audiosink_has_volume = g_object_class_find_property (G_OBJECT_GET_CLASS (audiosink), "volume") != NULL;
    } else {
        GstIterator *elem_iter = gst_bin_iterate_recurse (GST_BIN (audiosink));
        BANSHEE_GST_ITERATOR_ITERATE (elem_iter, GstElement *, element, TRUE, {
            player->audiosink_has_volume |= g_object_class_find_property (G_OBJECT_GET_CLASS (element), "volume") != NULL;
            gst_object_unref (element);
        });
    }
    bp_debug ("Audiosink has volume: %s",
        player->audiosink_has_volume ? "YES" : "NO");
        
    
    // Create a custom audio sink bin that will hold the real primary sink
    player->audiobin = gst_bin_new ("audiobin");
    g_return_val_if_fail (player->audiobin != NULL, FALSE);
    
    // Our audio sink is a tee, so plugins can attach their own pipelines
    player->audiotee = gst_element_factory_make ("tee", "audiotee");
    g_return_val_if_fail (player->audiotee != NULL, FALSE);

    // Create a volume control with low latency
    player->volume = gst_element_factory_make ("volume", NULL);
    g_return_val_if_fail (player->volume != NULL, FALSE);

// gstreamer on OS X does not call the callback upon initialization (see bgo#680917)
#ifdef __APPLE__
    // call the volume changed callback once so the volume from the pipeline is
    // set in the player object
    bp_volume_changed_callback (player->playbin, NULL, player);
#endif

    audiosinkqueue = gst_element_factory_make ("queue", "audiosinkqueue");
    g_return_val_if_fail (audiosinkqueue != NULL, FALSE);

    player->equalizer = _bp_equalizer_new (player);
    player->preamp = NULL;
    if (player->equalizer != NULL) {
        eq_audioconvert = gst_element_factory_make ("audioconvert", "audioconvert");
        eq_audioconvert2 = gst_element_factory_make ("audioconvert", "audioconvert2");
        player->preamp = gst_element_factory_make ("volume", "preamp");
    }
    
    // Add elements to custom audio sink
    gst_bin_add_many (GST_BIN (player->audiobin), player->audiotee, player->volume, audiosinkqueue, audiosink, NULL);
    
    if (player->equalizer != NULL) {
        gst_bin_add_many (GST_BIN (player->audiobin), eq_audioconvert, eq_audioconvert2, player->equalizer, player->preamp, NULL);
    }
   
    // Ghost pad the audio bin so audio is passed from the bin into the tee
    teepad = gst_element_get_pad (player->audiotee, "sink");
    gst_element_add_pad (player->audiobin, gst_ghost_pad_new ("sink", teepad));
    gst_object_unref (teepad);

    // Link the queue and the actual audio sink
    if (player->equalizer != NULL) {
        // link in equalizer, preamp and audioconvert.
        gst_element_link_many (audiosinkqueue, eq_audioconvert, player->preamp, 
            player->equalizer, eq_audioconvert2, player->volume, audiosink, NULL);
    } else {
        // link the queue with the real audio sink
        gst_element_link_many (audiosinkqueue, player->volume, audiosink, NULL);
    }
    player->before_rgvolume = player->volume;
    player->after_rgvolume = player->audiosink = audiosink;
    player->rgvolume_in_pipeline = FALSE;
    _bp_replaygain_pipeline_rebuild (player);

    _bp_vis_pipeline_setup (player);
    
    // Now that our internal audio sink is constructed, tell playbin to use it
    g_object_set (G_OBJECT (player->playbin), "audio-sink", player->audiobin, NULL);
    
    // Connect to the bus to get messages
    bus = gst_pipeline_get_bus (GST_PIPELINE (player->playbin));    
    gst_bus_add_watch (bus, bp_pipeline_bus_callback, player);

    // Link the first tee pad to the primary audio sink queue
    GstPad *sinkpad = gst_element_get_pad (audiosinkqueue, "sink");
    pad = gst_element_get_request_pad (player->audiotee, "src%d");
    g_object_set(player->audiotee, "alloc-pad", pad, NULL);
    gst_pad_link (pad, sinkpad);
    gst_object_unref (GST_OBJECT (pad));

    // Now allow specialized pipeline setups
    _bp_cdda_pipeline_setup (player);
    _bp_dvd_pipeline_setup (player);
    _bp_video_pipeline_setup (player, bus);
    _bp_dvd_find_navigation (player);

    return TRUE;
}

void
_bp_pipeline_destroy (BansheePlayer *player)
{
    g_return_if_fail (IS_BANSHEE_PLAYER (player));
    
    if (player->playbin == NULL) {
        return;
    }
    
    if (GST_IS_ELEMENT (player->playbin)) {
        player->target_state = GST_STATE_NULL;
        gst_element_set_state (player->playbin, GST_STATE_NULL);

        // The audiosink was set READY early to detect sink volume control in
        // case it is out of sync with the playbin state ensure it's in NULL now
        if (player->audiosink != NULL && GST_STATE (player->audiosink) != GST_STATE_NULL)
          gst_element_set_state (player->audiosink, GST_STATE_NULL);

        gst_object_unref (GST_OBJECT (player->playbin));
    }
    
    _bp_vis_pipeline_destroy (player);
    
    player->playbin = NULL;
}
