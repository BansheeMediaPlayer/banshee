/***************************************************************************
 *  gst-playback-0.10.c
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
 
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <glib.h>
#include <glib/gstdio.h>

#include <gst/gst.h>

#include "gst-tagger.h"

#define IS_GST_PLAYBACK(e) (e != NULL)
#define SET_CALLBACK(cb_name) { if(engine != NULL) { engine->cb_name = cb; } }

typedef struct GstPlayback GstPlayback;

typedef void (* GstPlaybackEosCallback) (GstPlayback *engine);
typedef void (* GstPlaybackErrorCallback) (GstPlayback *engine, 
    const gchar * error, const gchar *debug);
typedef void (* GstPlaybackStateChangedCallback) (
    GstPlayback *engine, GstState old_state, 
    GstState new_state, GstState pending_state);
typedef void (* GstPlaybackIterateCallback) (GstPlayback *engine);
typedef void (* GstPlaybackBufferingCallback) (GstPlayback *engine, gint buffering_progress);

struct GstPlayback {
    GstElement *playbin;
    GstElement *audiotee;
    GstElement *audiobin;
    
    guint iterate_timeout_id;
    gchar *cdda_device;
    
    GstPlaybackEosCallback eos_cb;
    GstPlaybackErrorCallback error_cb;
    GstPlaybackStateChangedCallback state_changed_cb;
    GstPlaybackIterateCallback iterate_cb;
    GstPlaybackBufferingCallback buffering_cb;
    GstTaggerTagFoundCallback tag_found_cb;
};

// private methods

static void
gst_playback_destroy_pipeline(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    
    if(engine->playbin == NULL) {
        return;
    }
    
    if(GST_IS_ELEMENT(engine->playbin)) {
        gst_element_set_state(engine->playbin, GST_STATE_NULL);
        gst_object_unref(GST_OBJECT(engine->playbin));
    }
    
    engine->playbin = NULL;
}

static gboolean
gst_playback_bus_callback(GstBus *bus, GstMessage *message, gpointer data)
{
    GstPlayback *engine = (GstPlayback *)data;

    g_return_val_if_fail(IS_GST_PLAYBACK(engine), FALSE);

    switch(GST_MESSAGE_TYPE(message)) {
        case GST_MESSAGE_ERROR: {
            GError *error;
            gchar *debug;
            
            gst_playback_destroy_pipeline(engine);
            
            if(engine->error_cb != NULL) {
                gst_message_parse_error(message, &error, &debug);
                engine->error_cb(engine, error->message, debug);
                g_error_free(error);
                g_free(debug);
            }
            
            break;
        }        
        case GST_MESSAGE_EOS:
            if(engine->eos_cb != NULL) {
                engine->eos_cb(engine);
            }
            break;
        case GST_MESSAGE_STATE_CHANGED: {
            GstState old, new, pending;
            gst_message_parse_state_changed(message, &old, &new, &pending);
            if(engine->state_changed_cb != NULL) {
                engine->state_changed_cb(engine, old, new, pending);
            }
            break;
        }
        case GST_MESSAGE_BUFFERING: {
            const GstStructure *buffering_struct;
            gint buffering_progress = 0;
            
            buffering_struct = gst_message_get_structure(message);
            if(!gst_structure_get_int(buffering_struct, "buffer-percent", &buffering_progress)) {
                g_warning("Could not get completion percentage from BUFFERING message");
                break;
            }

            if(engine->buffering_cb != NULL) {
                engine->buffering_cb(engine, buffering_progress);
            }
        }
        case GST_MESSAGE_TAG: {
            GstTagList *tags;
            GstTaggerInvoke invoke;
            
            if(GST_MESSAGE_TYPE(message) != GST_MESSAGE_TAG) {
                break;
            }
            
            invoke.callback = engine->tag_found_cb;
            invoke.user_data = engine;
            
            gst_message_parse_tag(message, &tags);
            
            if(GST_IS_TAG_LIST(tags)) {
                gst_tag_list_foreach(tags, (GstTagForeachFunc)gst_tagger_process_tag, &invoke);
                gst_tag_list_free(tags);
            }
            break;
        }
        default:
            break;
    }
    
    return TRUE;
}

static void
gst_playback_on_notify_source_cb(GstElement *playbin, gpointer unknown, GstPlayback *engine)
{
    GstElement *source_element = NULL;
    
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    
    if(engine->cdda_device == NULL) {
        return;
    }
    
    g_object_get(playbin, "source", &source_element, NULL);
    if(source_element == NULL) {
        return;
    }
    
    if(g_object_class_find_property(G_OBJECT_GET_CLASS(source_element), "paranoia-mode") &&
        g_object_class_find_property(G_OBJECT_GET_CLASS(source_element), "device")) {
        g_object_set(source_element, "paranoia-mode", 0, NULL);
        g_object_set(source_element, "device", engine->cdda_device, NULL);
    }
    
    g_object_unref(source_element);
}

static gboolean
gst_playback_cdda_source_set_track(GstElement *playbin, guint track)
{
    static GstFormat format = 0;
    GstElement *source_element = NULL;
    GstState state;
    
    gst_element_get_state(playbin, &state, NULL, 0);
    if(state < GST_STATE_PAUSED) {
        return FALSE;
    }

    g_object_get(playbin, "source", &source_element, NULL);
    if(source_element == NULL) {
        return FALSE;
    }
    
    if(strcmp(G_OBJECT_TYPE_NAME(source_element), "GstCdParanoiaSrc") == 0) {
        if(format == 0) {
            format = gst_format_get_by_nick("track");
        }
        
        if(gst_element_seek(playbin, 1.0, format, GST_SEEK_FLAG_FLUSH,
            GST_SEEK_TYPE_SET, track - 1, GST_SEEK_TYPE_NONE, -1)) {
            g_object_unref(source_element);
            return TRUE;
        }
    }
    
    g_object_unref(source_element);
    return FALSE;
}

static gboolean 
gst_playback_construct(GstPlayback *engine)
{
    GstElement *audiosink;
    GstElement *audiosinkqueue;
    GstPad *teepad;
    
    g_return_val_if_fail(IS_GST_PLAYBACK(engine), FALSE);
    
    // create necessary elements
    engine->playbin = gst_element_factory_make("playbin", "playbin");
    g_return_val_if_fail(engine->playbin != NULL, FALSE);

    audiosink = gst_element_factory_make("gconfaudiosink", "audiosink");
    g_return_val_if_fail(audiosink != NULL, FALSE);
    
    engine->audiobin = gst_bin_new("audiobin");
    g_return_val_if_fail(engine->audiobin != NULL, FALSE);
    
    engine->audiotee = gst_element_factory_make("tee", "audiotee");
    g_return_val_if_fail(engine->audiotee != NULL, FALSE);
    
    audiosinkqueue = gst_element_factory_make("queue", "audiosinkqueue");
    g_return_val_if_fail(audiosinkqueue != NULL, FALSE);
    
    // add elements to custom audio sink
    gst_bin_add(GST_BIN(engine->audiobin), engine->audiotee);
    gst_bin_add(GST_BIN(engine->audiobin), audiosinkqueue);
    gst_bin_add(GST_BIN(engine->audiobin), audiosink);
    
    // ghost pad the audio bin
    teepad = gst_element_get_pad(engine->audiotee, "sink");
    gst_element_add_pad(engine->audiobin, gst_ghost_pad_new("sink", teepad));
    gst_object_unref(teepad);

    // link the tee/queue pads for the default
    gst_pad_link(gst_element_get_request_pad(engine->audiotee, "src0"), 
        gst_element_get_pad(audiosinkqueue, "sink"));

    // link the queue with the real audio sink
    gst_element_link(audiosinkqueue, audiosink);
    
    g_object_set(G_OBJECT(engine->playbin), "audio-sink", engine->audiobin, NULL);

    gst_bus_add_watch(gst_pipeline_get_bus(GST_PIPELINE(engine->playbin)), 
        gst_playback_bus_callback, engine);
        
    g_signal_connect(engine->playbin, "notify::source", G_CALLBACK(gst_playback_on_notify_source_cb), engine);
        
    return TRUE;
}

static gboolean
gst_playback_iterate_timeout(GstPlayback *engine)
{
    g_return_val_if_fail(IS_GST_PLAYBACK(engine), FALSE);
    
    if(engine->iterate_cb != NULL) {
        engine->iterate_cb(engine);
    }
    
    return TRUE;
}

static void
gst_playback_start_iterate_timeout(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));

    if(engine->iterate_timeout_id != 0) {
        return;
    }
    
    engine->iterate_timeout_id = g_timeout_add(200, 
        (GSourceFunc)gst_playback_iterate_timeout, engine);
}

static void
gst_playback_stop_iterate_timeout(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    
    if(engine->iterate_timeout_id == 0) {
        return;
    }
    
    g_source_remove(engine->iterate_timeout_id);
    engine->iterate_timeout_id = 0;
}

// public methods

GstPlayback *
gst_playback_new()
{
    GstPlayback *engine = g_new0(GstPlayback, 1);
    if(!gst_playback_construct(engine)) {
        g_free(engine);
        return NULL;
    }
    
    engine->eos_cb = NULL;
    engine->error_cb = NULL;
    engine->state_changed_cb = NULL;
    engine->iterate_cb = NULL;
    engine->buffering_cb = NULL;
    engine->tag_found_cb = NULL;
    
    engine->iterate_timeout_id = 0;
    engine->cdda_device = NULL;
    
    return engine;
}

void
gst_playback_free(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    
    gst_element_set_state(engine->playbin, GST_STATE_NULL);
    gst_object_unref(GST_OBJECT(engine->playbin));
    
    if(engine->cdda_device != NULL) {
        g_free(engine->cdda_device);
        engine->cdda_device = NULL;
    }
    
    g_free(engine);
    engine = NULL;
}

void
gst_playback_set_eos_callback(GstPlayback *engine, 
    GstPlaybackEosCallback cb)
{
    SET_CALLBACK(eos_cb);
}

void
gst_playback_set_error_callback(GstPlayback *engine, 
    GstPlaybackErrorCallback cb)
{
    SET_CALLBACK(error_cb);
}

void
gst_playback_set_state_changed_callback(GstPlayback *engine, 
    GstPlaybackStateChangedCallback cb)
{
    SET_CALLBACK(state_changed_cb);
}

void
gst_playback_set_iterate_callback(GstPlayback *engine, 
    GstPlaybackIterateCallback cb)
{
    SET_CALLBACK(iterate_cb);
}

void
gst_playback_set_buffering_callback(GstPlayback *engine, 
    GstPlaybackBufferingCallback cb)
{
    SET_CALLBACK(buffering_cb);
}

void
gst_playback_set_tag_found_callback(GstPlayback *engine, 
    GstTaggerTagFoundCallback cb)
{
    SET_CALLBACK(tag_found_cb);
}

void
gst_playback_open(GstPlayback *engine, const gchar *uri)
{
    GstState state;
    
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    
    if(engine->playbin == NULL && !gst_playback_construct(engine)) {
        return;
    }

    if(uri != NULL && g_str_has_prefix(uri, "cdda://")) {
        const gchar *p = g_utf8_strchr(uri, -1, '#');
        const gchar *new_cdda_device;
        
        if(p != NULL) {
            new_cdda_device = p + 1;
            
            if(engine->cdda_device == NULL) {
                engine->cdda_device = g_strdup(new_cdda_device);
            } else if(strcmp(new_cdda_device, engine->cdda_device) == 0) {
                guint track_num;
                gchar *track_str = g_strndup(uri + 7, strlen(uri) - strlen(new_cdda_device) - 9);
                track_num = atoi(track_str);
                g_free(track_str);
                
                if(gst_playback_cdda_source_set_track(engine->playbin, track_num)) {
                    return;
                }
            } else {
                if(engine->cdda_device != NULL) {
                    g_free(engine->cdda_device);
                    engine->cdda_device = NULL;
                }
            
                engine->cdda_device = g_strdup(new_cdda_device);
            }
        }
    } else if(engine->cdda_device != NULL) {
        g_free(engine->cdda_device);
        engine->cdda_device = NULL;
    }
    
    gst_element_get_state(engine->playbin, &state, NULL, 0);
    if(state >= GST_STATE_PAUSED) {
        gst_element_set_state(engine->playbin, GST_STATE_READY);
    }
    
    g_object_set(G_OBJECT(engine->playbin), "uri", uri, NULL);
}

void
gst_playback_stop(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    gst_playback_stop_iterate_timeout(engine);
    gst_element_set_state(engine->playbin, GST_STATE_PAUSED);
}

void
gst_playback_pause(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    gst_playback_stop_iterate_timeout(engine);
    gst_element_set_state(engine->playbin, GST_STATE_PAUSED);
}

void
gst_playback_play(GstPlayback *engine)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    gst_element_set_state(engine->playbin, GST_STATE_PLAYING);
    gst_playback_start_iterate_timeout(engine);
}

void
gst_playback_set_volume(GstPlayback *engine, gint volume)
{
    gdouble act_volume;
    g_return_if_fail(IS_GST_PLAYBACK(engine));
	act_volume = CLAMP(volume, 0, 100) / 100.0;
	g_object_set(G_OBJECT(engine->playbin), "volume", act_volume, NULL);
}

gint
gst_playback_get_volume(GstPlayback *engine)
{
    gdouble volume = 0.0;
    g_return_val_if_fail(IS_GST_PLAYBACK(engine), 0);
    g_object_get(engine->playbin, "volume", &volume, NULL);
    return (gint)(volume * 100.0);
}

void
gst_playback_set_position(GstPlayback *engine, guint64 time_ms)
{
    g_return_if_fail(IS_GST_PLAYBACK(engine));
    
    if(!gst_element_seek(engine->playbin, 1.0, 
        GST_FORMAT_TIME, GST_SEEK_FLAG_FLUSH,
        GST_SEEK_TYPE_SET, time_ms * GST_MSECOND, 
        GST_SEEK_TYPE_NONE, GST_CLOCK_TIME_NONE)) {
        g_warning("Could not seek in stream");
    }
}

guint64
gst_playback_get_position(GstPlayback *engine)
{
    GstFormat format = GST_FORMAT_TIME;
    gint64 position;

    g_return_val_if_fail(IS_GST_PLAYBACK(engine), 0);

    if(gst_element_query_position(engine->playbin, &format, &position)) {
        return position / 1000000;
    }
    
    return 0;
}

guint64
gst_playback_get_duration(GstPlayback *engine)
{
    GstFormat format = GST_FORMAT_TIME;
    gint64 duration;

    g_return_val_if_fail(IS_GST_PLAYBACK(engine), 0);

    if(gst_element_query_duration(engine->playbin, &format, &duration)) {
        return duration / 1000000;
    }
    
    return 0;
}

gboolean
gst_playback_can_seek(GstPlayback *engine)
{
    GstQuery *query;
    gboolean can_seek = TRUE;
    
    g_return_val_if_fail(IS_GST_PLAYBACK(engine), FALSE);
    g_return_val_if_fail(engine->playbin != NULL, FALSE);
    
    query = gst_query_new_seeking(GST_FORMAT_TIME);
    if(!gst_element_query(engine->playbin, query)) {
        // This will probably fail, 100% of the time, because it's apparently 
        // very unimplemented in GStreamer... when it's fixed
        // we will return FALSE here, and show the warning
        // g_warning("Could not query pipeline for seek ability");
        return gst_playback_get_duration(engine) > 0;
    }
    
    gst_query_parse_seeking(query, NULL, &can_seek, NULL, NULL);
    gst_query_unref(query);
    
    return can_seek;
}

gboolean
gst_playback_get_pipeline_elements(GstPlayback *engine, GstElement **playbin, GstElement **audiobin, 
    GstElement **audiotee)
{
    g_return_val_if_fail(IS_GST_PLAYBACK(engine), FALSE);
    
    *playbin = engine->playbin;
    *audiobin = engine->audiobin;
    *audiotee = engine->audiotee;
    
    return TRUE;
}
