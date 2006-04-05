/***************************************************************************
 *  gst-transcode-0.10.c
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
 
#include <gst/gst.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>
#include <glib.h>
#include <glib/gi18n.h>
#include <glib/gstdio.h>

#include <libgnomevfs/gnome-vfs.h>

#include "gst-misc.h"

typedef struct GstTranscoder GstTranscoder;

typedef void (* GstTranscoderProgressCallback) (GstTranscoder *transcoder, gdouble progress);
typedef void (* GstTranscoderFinishedCallback) (GstTranscoder *transcoder);
typedef void (* GstTranscoderErrorCallback) (GstTranscoder *transcoder, const gchar *error, const gchar *debug);

struct GstTranscoder {
    gboolean is_transcoding;
    guint iterate_timeout_id;
    GstElement *pipeline;
    GstElement *sink_bin;
    GstElement *conv_elem;
    const gchar *output_uri;
    GstTranscoderProgressCallback progress_cb;
    GstTranscoderFinishedCallback finished_cb;
    GstTranscoderErrorCallback error_cb;
};

// private methods

static void
gst_transcoder_raise_error(GstTranscoder *transcoder, const gchar *error, const gchar *debug)
{
    g_return_if_fail(transcoder != NULL);
    g_return_if_fail(transcoder->error_cb != NULL);
    
    transcoder->error_cb(transcoder, error, debug);
}

static gboolean
gst_transcoder_gvfs_allow_overwrite_cb(GstElement *element, gpointer filename,
    gpointer user_data)
{
    return TRUE;
}

static gboolean
gst_transcoder_iterate_timeout(GstTranscoder *transcoder)
{
    GstFormat format = GST_FORMAT_TIME;
    gint64 position;
    gint64 duration;

    g_return_val_if_fail(transcoder != NULL, FALSE);
    
    if(!gst_element_query_duration(transcoder->pipeline, &format, &duration) ||
        !gst_element_query_position(transcoder->conv_elem, &format, &position)) {
        return TRUE;
    }
    
    if(transcoder->progress_cb != NULL) {
        transcoder->progress_cb(transcoder, (double)position / (double)duration);
    }
    
    return TRUE;
}

static void
gst_transcoder_start_iterate_timeout(GstTranscoder *transcoder)
{
    g_return_if_fail(transcoder != NULL);

    if(transcoder->iterate_timeout_id != 0) {
        return;
    }
    
    transcoder->iterate_timeout_id = g_timeout_add(200, 
        (GSourceFunc)gst_transcoder_iterate_timeout, transcoder);
}

static void
gst_transcoder_stop_iterate_timeout(GstTranscoder *transcoder)
{
    g_return_if_fail(transcoder != NULL);
    
    if(transcoder->iterate_timeout_id == 0) {
        return;
    }
    
    g_source_remove(transcoder->iterate_timeout_id);
    transcoder->iterate_timeout_id = 0;
}

static gboolean
gst_transcoder_bus_callback(GstBus *bus, GstMessage *message, gpointer data)
{
    GnomeVFSFileInfo fileinfo;
    GstTranscoder *transcoder = (GstTranscoder *)data;

    g_return_val_if_fail(transcoder != NULL, FALSE);

    switch(GST_MESSAGE_TYPE(message)) {
        case GST_MESSAGE_ERROR: {
            GError *error;
            gchar *debug;
            
            transcoder->is_transcoding = FALSE;
            gst_transcoder_stop_iterate_timeout(transcoder);
            
            if(transcoder->error_cb != NULL) {
                gst_message_parse_error(message, &error, &debug);
                gst_transcoder_raise_error(transcoder, error->message, debug);
                g_error_free(error);
                g_free(debug);
            }
            
            break;
        }        
        case GST_MESSAGE_EOS:
            gst_element_set_state(GST_ELEMENT(transcoder->pipeline), GST_STATE_NULL);
            g_object_unref(G_OBJECT(transcoder->pipeline));
            
            transcoder->is_transcoding = FALSE;
            gst_transcoder_stop_iterate_timeout(transcoder);

            if(gnome_vfs_get_file_info(transcoder->output_uri, &fileinfo, 
                GNOME_VFS_FILE_INFO_DEFAULT) == GNOME_VFS_OK) {
                if(fileinfo.size < 100) {
                    gst_transcoder_raise_error(transcoder, 
                        _("No decoder could be found for source format."), NULL);
                    g_remove(transcoder->output_uri);
                    break;
                }
            } else {
                gst_transcoder_raise_error(transcoder, _("Could not stat encoded file"), NULL);
                break;
            }
            
            if(transcoder->finished_cb != NULL) {
                transcoder->finished_cb(transcoder);
            }
            break;
        default:
            break;
    }
    
    return TRUE;
}

static GstElement *
gst_transcoder_build_encoder(const gchar *encoder_pipeline)
{
    GstElement *encoder = NULL;
    gchar *pipeline;
    GError *error = NULL;
    
    pipeline = g_strdup_printf("%s", encoder_pipeline);
    encoder = gst_parse_bin_from_description(pipeline, TRUE, &error);
    g_free(pipeline);
    
    if(error != NULL) {
        return NULL;
    }
    
    return encoder;
}    

static void
gst_transcoder_new_decoded_pad(GstElement *decodebin, GstPad *pad, 
    gboolean last, gpointer data)
{
    GstCaps *caps;
    GstStructure *str;
    GstPad *audiopad;
    GstTranscoder *transcoder = (GstTranscoder *)data;

    g_return_if_fail(transcoder != NULL);

    audiopad = gst_element_get_pad(transcoder->sink_bin, "sink");
    
    if(GST_PAD_IS_LINKED(audiopad)) {
        g_object_unref(audiopad);
        return;
    }

    caps = gst_pad_get_caps(pad);
    str = gst_caps_get_structure(caps, 0);
    
    if(!g_strrstr(gst_structure_get_name(str), "audio")) {
        gst_caps_unref(caps);
        gst_object_unref(audiopad);
        return;
    }
   
    gst_caps_unref(caps);
    gst_pad_link(pad, audiopad);
}

static gboolean
gst_transcoder_create_pipeline(GstTranscoder *transcoder, 
    const char *input_file, const char *output_file, 
    const gchar *encoder_pipeline)
{
    GstElement *source_elem;
    GstElement *decoder_elem;
    GstElement *encoder_elem;
    GstElement *sink_elem;
    GstElement *conv_elem;
    GstPad *encoder_pad;

    if(transcoder == NULL) {
        return FALSE;
    }
    
    transcoder->pipeline = gst_pipeline_new("pipeline");

    source_elem = gst_element_factory_make("gnomevfssrc", "source");
    if(source_elem == NULL) {
        gst_transcoder_raise_error(transcoder, _("Could not create 'gnomevfssrc' plugin"), NULL);
        return FALSE;
    }

    decoder_elem = gst_element_factory_make("decodebin", "decodebin");
    if(decoder_elem == NULL) {
        gst_transcoder_raise_error(transcoder, _("Could not create 'decodebin' plugin"), NULL);
        return FALSE;
    }
    
    sink_elem = gst_element_factory_make("gnomevfssink", "sink");
    if(sink_elem == NULL) {
        gst_transcoder_raise_error(transcoder, _("Could not create 'gnomevfssink' plugin"), NULL);
        return FALSE;
    }
    
    transcoder->sink_bin = gst_bin_new("sinkbin");
    if(transcoder->sink_bin == NULL) {
        gst_transcoder_raise_error(transcoder, _("Could not create 'sinkben' plugin"), NULL);
        return FALSE;
    }
    
    conv_elem = gst_element_factory_make("audioconvert", "audioconvert");
    if(conv_elem == NULL) {
        gst_transcoder_raise_error(transcoder, _("Could not create 'audioconvert' plugin"), NULL);
        return FALSE;
    }
    
    encoder_elem = gst_transcoder_build_encoder(encoder_pipeline);
    if(encoder_elem == NULL) {
         gst_transcoder_raise_error(transcoder, _("Could not create encoding pipeline"), encoder_pipeline);
         return FALSE;
    }

    encoder_pad = gst_element_get_pad(conv_elem, "sink");
    if(encoder_pad == NULL) {
        gst_transcoder_raise_error(transcoder, _("Could not get sink pad from encoder"), NULL);
        return FALSE;
    }
    
    gst_bin_add_many(GST_BIN(transcoder->sink_bin), conv_elem, encoder_elem, sink_elem, NULL);
    gst_element_link_many(conv_elem, encoder_elem, sink_elem, NULL);
    
    gst_element_add_pad(transcoder->sink_bin, gst_ghost_pad_new("sink", encoder_pad));
    gst_object_unref(encoder_pad);
    
    g_signal_connect(G_OBJECT(sink_elem), "allow-overwrite",
        G_CALLBACK(gst_transcoder_gvfs_allow_overwrite_cb), transcoder);
    
    gst_bin_add_many(GST_BIN(transcoder->pipeline), source_elem, decoder_elem, 
        transcoder->sink_bin, NULL);
        
    gst_element_link(source_elem, decoder_elem);

    g_object_set(source_elem, "location", input_file, NULL);
    g_object_set(sink_elem, "location", output_file, NULL);

    g_signal_connect(decoder_elem, "new-decoded-pad", 
        G_CALLBACK(gst_transcoder_new_decoded_pad), transcoder);

    gst_bus_add_watch(gst_pipeline_get_bus(GST_PIPELINE(transcoder->pipeline)), 
        gst_transcoder_bus_callback, transcoder);
        
    transcoder->conv_elem = conv_elem;
    
    return TRUE;
}

// public methods

GstTranscoder *
gst_transcoder_new()
{
    GstTranscoder *transcoder;
    
    gstreamer_initialize();
    
    transcoder = g_new0(GstTranscoder, 1);
    transcoder->is_transcoding = FALSE;
    transcoder->pipeline = NULL;
    transcoder->sink_bin = NULL;
    transcoder->conv_elem = NULL;
    transcoder->output_uri = NULL;
    transcoder->progress_cb = NULL;
    transcoder->error_cb = NULL;
    transcoder->finished_cb = NULL;
    transcoder->iterate_timeout_id = 0;
    
    return transcoder;
}

void
gst_transcoder_free(GstTranscoder *transcoder)
{
    g_return_if_fail(transcoder != NULL);
    
    g_object_unref(G_OBJECT(transcoder->pipeline));
    gst_transcoder_stop_iterate_timeout(transcoder);
    
    if(transcoder->output_uri != NULL) {
        g_free(transcoder->output_uri);
        transcoder->output_uri = NULL;
    }
    
    g_free(transcoder);
    transcoder = NULL;
}

void 
gst_transcoder_transcode(GstTranscoder *transcoder, const gchar *input_uri, 
    const gchar *output_uri, const gchar *encoder_pipeline)
{
    g_return_if_fail(transcoder != NULL);
    
    if(transcoder->is_transcoding) {
        return;
    }
    
    if(!gst_transcoder_create_pipeline(transcoder, input_uri, output_uri, encoder_pipeline)) {
        gst_transcoder_raise_error(transcoder, _("Could not construct pipeline"), NULL); 
        return;
    }
    
    if(transcoder->output_uri != NULL) {
        g_free(transcoder->output_uri);
    }
    
    transcoder->output_uri = g_strdup(output_uri);
    transcoder->is_transcoding = TRUE;
    
    gst_element_set_state(GST_ELEMENT(transcoder->pipeline), GST_STATE_PLAYING);
    gst_transcoder_start_iterate_timeout(transcoder);
}

void 
gst_transcoder_cancel(GstTranscoder *transcoder)
{
    g_return_if_fail(transcoder != NULL);
    gst_transcoder_stop_iterate_timeout(transcoder);
    
    transcoder->is_transcoding = FALSE;
    
    if(GST_IS_ELEMENT(transcoder->pipeline)) {
        gst_element_set_state(GST_ELEMENT(transcoder->pipeline), GST_STATE_NULL);
        gst_object_unref(GST_OBJECT(transcoder->pipeline));
    }
    
    g_remove(transcoder->output_uri);
}

void
gst_transcoder_set_progress_callback(GstTranscoder *transcoder, 
    GstTranscoderProgressCallback cb)
{
    g_return_if_fail(transcoder != NULL);
    transcoder->progress_cb = cb;
}

void
gst_transcoder_set_finished_callback(GstTranscoder *transcoder, 
    GstTranscoderFinishedCallback cb)
{
    g_return_if_fail(transcoder != NULL);
    transcoder->finished_cb = cb;
}

void
gst_transcoder_set_error_callback(GstTranscoder *transcoder, 
    GstTranscoderErrorCallback cb)
{
    g_return_if_fail(transcoder != NULL);
    transcoder->error_cb = cb;
}

gboolean
gst_transcoder_get_is_transcoding(GstTranscoder *transcoder)
{
    g_return_val_if_fail(transcoder != NULL, FALSE);
    return transcoder->is_transcoding;
}
