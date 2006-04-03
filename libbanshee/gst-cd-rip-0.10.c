/***************************************************************************
 *  gst-cd-rip-0.10.c
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

#ifdef HAVE_CONFIG_H
#  include "config.h"
#endif

#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>
#include <string.h>

#include <glib.h>
#include <glib/gi18n.h>
#include <glib/gstdio.h>

#include <gst/gst.h>
#include <gst/tag/tag.h>

#include "gst-misc.h"

typedef struct GstCdRipper GstCdRipper;

typedef void (* GstCdRipperProgressCallback) (GstCdRipper *ripper, gint seconds, gpointer user_info);
typedef void (* GstCdRipperFinishedCallback) (GstCdRipper *ripper);
typedef void (* GstCdRipperErrorCallback) (GstCdRipper *ripper, const gchar *error, const gchar *debug);

struct GstCdRipper {
    gboolean is_ripping;
    guint iterate_timeout_id;
    
    gchar *device;
    gint paranoia_mode;
    const gchar *output_uri;
    gchar *encoder_pipeline;
    
    GstElement *pipeline;
    GstElement *cdparanoia;
    GstElement *encoder;
    GstElement *filesink;
    
    GstFormat track_format;
    
    GstCdRipperProgressCallback progress_cb;
    GstCdRipperFinishedCallback finished_cb;
    GstCdRipperErrorCallback error_cb;
};

// private methods

static void
gst_cd_ripper_raise_error(GstCdRipper *ripper, const gchar *error, const gchar *debug)
{
    g_return_if_fail(ripper != NULL);
    g_return_if_fail(ripper->error_cb != NULL);
    
    if(ripper->error_cb != NULL) {
        ripper->error_cb(ripper, error, debug);
    }
}

static gboolean
gst_cd_ripper_gvfs_allow_overwrite_cb(GstElement *element, gpointer filename,
    gpointer user_data)
{
    return TRUE;
}

static gboolean
gst_cd_ripper_iterate_timeout(GstCdRipper *ripper)
{
    GstFormat format = GST_FORMAT_TIME;
    GstState state;
    gint64 position;
    
    g_return_val_if_fail(ripper != NULL, FALSE);

    gst_element_get_state(ripper->pipeline, &state, NULL, 0);
    if(state != GST_STATE_PLAYING) {
        return TRUE;
    }

    if(!gst_element_query_position(ripper->cdparanoia, &format, &position)) {
        return TRUE;
    }
    
    if(ripper->progress_cb != NULL) {
        ripper->progress_cb(ripper, position / GST_SECOND, NULL);
    }

    return TRUE;
}

static void
gst_cd_ripper_start_iterate_timeout(GstCdRipper *ripper)
{
    g_return_if_fail(ripper != NULL);

    if(ripper->iterate_timeout_id != 0) {
        return;
    }
    
    ripper->iterate_timeout_id = g_timeout_add(200, 
        (GSourceFunc)gst_cd_ripper_iterate_timeout, ripper);
}

static void
gst_cd_ripper_stop_iterate_timeout(GstCdRipper *ripper)
{
    g_return_if_fail(ripper != NULL);
    
    if(ripper->iterate_timeout_id == 0) {
        return;
    }
    
    g_source_remove(ripper->iterate_timeout_id);
    ripper->iterate_timeout_id = 0;
}

static gboolean
gst_cd_ripper_bus_callback(GstBus *bus, GstMessage *message, gpointer data)
{
    GstCdRipper *ripper = (GstCdRipper *)data;

    g_return_val_if_fail(ripper != NULL, FALSE);

    switch(GST_MESSAGE_TYPE(message)) {
        case GST_MESSAGE_ERROR: {
            GError *error;
            gchar *debug;
            
            if(ripper->error_cb != NULL) {
                gst_message_parse_error(message, &error, &debug);
                gst_cd_ripper_raise_error(ripper, error->message, debug);
                g_error_free(error);
                g_free(debug);
            }
            
            ripper->is_ripping = FALSE;
            gst_cd_ripper_stop_iterate_timeout(ripper);
            
            break;
        }        
        case GST_MESSAGE_EOS:
            gst_element_set_state(GST_ELEMENT(ripper->pipeline), GST_STATE_NULL);
            g_object_unref(G_OBJECT(ripper->pipeline));
            
            ripper->is_ripping = FALSE;
            gst_cd_ripper_stop_iterate_timeout(ripper);
            
            if(ripper->finished_cb != NULL) {
                ripper->finished_cb(ripper);
            }
            break;
        default:
            break;
    }
    
    return TRUE;
}

static GstElement *
gst_cd_ripper_build_encoder(const gchar *encoder_pipeline)
{
    GstElement *encoder = NULL;
    gchar *pipeline = NULL;
    GError *error = NULL;
    
    // FIXME: ugly
    if(strstr(encoder_pipeline, "vorbisenc") != NULL 
        && strstr(encoder_pipeline, "oggmux") == NULL) {
        g_warning("vorbisenc added without oggmux, attempting to insert oggmux element in pipeline");
        pipeline = g_strdup_printf("audioconvert ! %s ! oggmux", encoder_pipeline);
    } else if((strstr(encoder_pipeline, "lame") != NULL || strstr(encoder_pipeline, "xingenc") != NULL) &&
        strstr(encoder_pipeline, "id3mux") == NULL) {
        const gchar *muxer_names [] = { "taglibid3mux", "id3mux", NULL};
        gint i;
        
        for(i = 0; muxer_names[i] != NULL; i++) {
            GstElementFactory *id3mux = gst_element_factory_find(muxer_names[i]);
            if(id3mux == NULL) {
                continue;
            }
            
            g_warning("MP3 encoder added without %s, attempting to insert %s element in pipeline",
                muxer_names[i], muxer_names[i]);
            pipeline = g_strdup_printf("audioconvert ! %s ! %s", encoder_pipeline, muxer_names[i]);
            gst_object_unref(GST_OBJECT(id3mux));
            break;
        }
    }
    
    if(pipeline == NULL) {
        pipeline = g_strdup_printf("audioconvert ! %s", encoder_pipeline);
    }
    
    encoder = gst_parse_bin_from_description(pipeline, TRUE, &error);
    g_free(pipeline);
    
    if(error != NULL) {
        return NULL;
    }
    
    return encoder;
}    

static gboolean
gst_cd_ripper_build_pipeline(GstCdRipper *ripper)
{
    GstElement *queue;
    
    g_return_val_if_fail(ripper != NULL, FALSE);
        
    ripper->pipeline = gst_pipeline_new("pipeline");
    if(ripper->pipeline == NULL) {
        gst_cd_ripper_raise_error(ripper, _("Could not create pipeline"), NULL);
        return FALSE;
    }

    ripper->cdparanoia = gst_element_factory_make("cdparanoiasrc", "cdparanoia");
    if(ripper->cdparanoia == NULL) {
        gst_cd_ripper_raise_error(ripper, _("Could not initialize cdparanoia"), NULL);
        return FALSE;
    }
  
    g_object_set(G_OBJECT(ripper->cdparanoia), "device", ripper->device, NULL);
    g_object_set(G_OBJECT(ripper->cdparanoia), "paranoia-mode", ripper->paranoia_mode, NULL);
    
    ripper->track_format = gst_format_get_by_nick("track");
    
    ripper->encoder = gst_cd_ripper_build_encoder(ripper->encoder_pipeline);
    if(ripper->encoder == NULL) {
        gst_cd_ripper_raise_error(ripper, _("Could not create encoder pipeline"), NULL);
        return FALSE;
    }
    
    queue = gst_element_factory_make("queue", "queue");
    if(queue == NULL) {
        gst_cd_ripper_raise_error(ripper, _("Could not create queue plugin"), NULL);
        return FALSE;
    }
    
    g_object_set(G_OBJECT(queue), "max-size-time", 120 * GST_SECOND, NULL);
    
    ripper->filesink = gst_element_factory_make("gnomevfssink", "gnomevfssink");
    if(ripper->filesink == NULL) {
        gst_cd_ripper_raise_error(ripper, _("Could not create GNOME VFS output plugin"), NULL);
        return FALSE;
    }
    
    g_signal_connect(G_OBJECT(ripper->filesink), "allow-overwrite",
        G_CALLBACK(gst_cd_ripper_gvfs_allow_overwrite_cb), ripper);
    
    gst_bin_add_many(GST_BIN(ripper->pipeline),
        ripper->cdparanoia,
        queue,
        ripper->encoder,
        ripper->filesink,
        NULL);
        
    if(!gst_element_link_many(ripper->cdparanoia, queue, ripper->encoder, ripper->filesink, NULL)) {
        gst_cd_ripper_raise_error(ripper, _("Could not link pipeline elements"), NULL);
        return FALSE;
    }

    gst_bus_add_watch(gst_pipeline_get_bus(GST_PIPELINE(ripper->pipeline)), 
        gst_cd_ripper_bus_callback, ripper);

    return TRUE;
}

// public methods

GstCdRipper *
gst_cd_ripper_new(gchar *device, gint paranoia_mode, gchar *encoder_pipeline)
{
    GstCdRipper *ripper = g_new0(GstCdRipper, 1);
    
    if(ripper == NULL) {
        return NULL;
    }
    
    gstreamer_initialize();
        
    ripper->device = g_strdup(device);
    ripper->paranoia_mode = paranoia_mode;
    ripper->encoder_pipeline = g_strdup(encoder_pipeline);
    
    ripper->pipeline = NULL;
    ripper->cdparanoia = NULL;
    ripper->encoder = NULL;
    ripper->filesink = NULL;
    
    ripper->track_format = 0;
    
    ripper->progress_cb = NULL;
    ripper->error_cb = NULL;
    ripper->finished_cb = NULL;
    
    return ripper;
}

void
gst_cd_ripper_free(GstCdRipper *ripper)
{
    g_return_if_fail(ripper != NULL);
    
    gst_cd_ripper_stop_iterate_timeout(ripper);
    
    if(GST_IS_ELEMENT(ripper->pipeline)) {
        gst_element_set_state(GST_ELEMENT(ripper->pipeline), GST_STATE_NULL);
        gst_object_unref(GST_OBJECT(ripper->pipeline));
    }
    
    g_free(ripper->device);
    g_free(ripper->encoder_pipeline);
    
    g_free(ripper);
}

gboolean
gst_cd_ripper_rip_track(GstCdRipper *ripper, gchar *uri, gint track_number, 
    gchar *md_artist, gchar *md_album, gchar *md_title, gchar *md_genre,
    gint md_track_number, gint md_track_count, gpointer user_info)
{
    GstIterator *bin_iterator;
    GstElement *bin_element;
    gboolean can_tag = FALSE;
    gboolean iterate_done = FALSE;

    g_return_val_if_fail(ripper != NULL, FALSE);

    if(!gst_cd_ripper_build_pipeline(ripper)) {
        return FALSE;
    }
    
    // initialize the pipeline, set the sink output location
    gst_element_set_state(ripper->filesink, GST_STATE_NULL);
    g_object_set(G_OBJECT(ripper->filesink), "location", uri, NULL);
    
    // find an element to do the tagging and set tag data
    bin_iterator = gst_bin_iterate_all_by_interface(GST_BIN(ripper->encoder), GST_TYPE_TAG_SETTER);
    while(!iterate_done) {
        switch(gst_iterator_next(bin_iterator, (gpointer)&bin_element)) {
            case GST_ITERATOR_OK:
                gst_tag_setter_add_tags(GST_TAG_SETTER(bin_element),
                    GST_TAG_MERGE_REPLACE_ALL,
                    GST_TAG_TITLE,  md_title,
                    GST_TAG_ARTIST, md_artist,
                    GST_TAG_ALBUM,  md_album,
                    GST_TAG_TRACK_NUMBER, md_track_number,
                    GST_TAG_TRACK_COUNT,  md_track_count,
                    GST_TAG_ENCODER, _("Banshee"),
                    GST_TAG_ENCODER_VERSION, VERSION,
                    NULL);
                    
                if(md_genre && strlen(md_genre) == 0) {
                    gst_tag_setter_add_tags(GST_TAG_SETTER(bin_element),
                        GST_TAG_MERGE_APPEND,
                        GST_TAG_GENRE, md_genre,
                        NULL);
                }
        
                can_tag = TRUE;    
                gst_object_unref(bin_element);
                break;
            case GST_ITERATOR_RESYNC:
                gst_iterator_resync(bin_iterator);
                break;
            default:
                iterate_done = TRUE;
                break;
        }
    }
    
    gst_iterator_free(bin_iterator);
    
    if(!can_tag) {
        g_warning(_("Encoding element does not support tagging!"));
    }
    
    // start the ripping
    g_object_set(G_OBJECT(ripper->cdparanoia), "track", track_number, NULL);
    
    gst_element_set_state(ripper->pipeline, GST_STATE_PLAYING);
    gst_cd_ripper_start_iterate_timeout(ripper);
    
    return TRUE;
}

void 
gst_cd_ripper_cancel(GstCdRipper *ripper)
{
    g_return_if_fail(ripper != NULL);
    gst_cd_ripper_stop_iterate_timeout(ripper);
    
    if(GST_IS_ELEMENT(ripper->pipeline)) {
        gst_element_set_state(GST_ELEMENT(ripper->pipeline), GST_STATE_NULL);
        gst_object_unref(GST_OBJECT(ripper->pipeline));
    }
    
    g_remove(ripper->output_uri);
}

void
gst_cd_ripper_set_progress_callback(GstCdRipper *ripper, 
    GstCdRipperProgressCallback cb)
{
    g_return_if_fail(ripper != NULL);
    ripper->progress_cb = cb;
}

void
gst_cd_ripper_set_finished_callback(GstCdRipper *ripper, 
    GstCdRipperFinishedCallback cb)
{
    g_return_if_fail(ripper != NULL);
    ripper->finished_cb = cb;
}

void
gst_cd_ripper_set_error_callback(GstCdRipper *ripper, 
    GstCdRipperErrorCallback cb)
{
    g_return_if_fail(ripper != NULL);
    ripper->error_cb = cb;
}

gboolean
gst_cd_ripper_get_is_ripping(GstCdRipper *ripper)
{
    g_return_val_if_fail(ripper != NULL, FALSE);
    return ripper->is_ripping;
}
