/*
 * Clutter-GStreamer.
 *
 * GStreamer integration library for Clutter.
 *
 * clutter-gst-video-sink.h - Gstreamer Video Sink that renders to a
 *                            Clutter Texture.
 *
 * Authored by Jonathan Matthew  <jonathan@kaolin.wh9.net>,
 *             Chris Lord        <chris@openedhand.com>
 *
 * Copyright (C) 2007,2008 OpenedHand
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the
 * Free Software Foundation, Inc., 59 Temple Place - Suite 330,
 * Boston, MA 02111-1307, USA.
 */

/**
 * SECTION:clutter-gst-video-sink
 * @short_description: GStreamer video sink
 *
 * #ClutterGstVideoSink is a GStreamer sink element that sends
 * data to a #ClutterTexture.
 */

#ifdef HAVE_CONFIG_H
#include "config.h"
#endif

#include "clutter-gst-video-sink.h"
#include "clutter-gst-shaders.h"
/* include assembly shaders */
#include "I420.h"
#include "YV12.h"

#include <gst/gst.h>
#include <gst/gstvalue.h>
#include <gst/video/video.h>
#include <gst/riff/riff-ids.h>

#include <glib.h>
#include <string.h>

static gchar *ayuv_to_rgba_shader = \
     FRAGMENT_SHADER_VARS
     "uniform sampler2D tex;"
     "void main () {"
     "  vec4 color = texture2D (tex, vec2(" TEX_COORD "));"
     "  float y = 1.1640625 * (color.g - 0.0625);"
     "  float v = color.a - 0.5;"
     "  color.a = color.r;"
     "  color.r = y + 1.59765625 * v;"
     "  color.g = y - 0.390625 * u - 0.8125 * v;"
     "  color.b = y + 2.015625 * u;"
     "  gl_FragColor = color;"
     FRAGMENT_SHADER_END
     "}";

static gchar *dummy_shader = \
     FRAGMENT_SHADER_VARS
     "void main () {"
     "}";

static gchar *yv12_to_rgba_shader = \
     FRAGMENT_SHADER_VARS
     "uniform sampler2D ytex;"
     "uniform sampler2D utex;"
     "uniform sampler2D vtex;"
     "void main () {"
     "  vec2 coord = vec2(" TEX_COORD ");"
     "  float y = 1.1640625 * (texture2D (ytex, coord).g - 0.0625);"
     "  float u = texture2D (utex, coord).g - 0.5;"
     "  float v = texture2D (vtex, coord).g - 0.5;"
     "  vec4 color;"
     "  color.r = y + 1.59765625 * v;"
     "  color.g = y - 0.390625 * u - 0.8125 * v;"
     "  color.b = y + 2.015625 * u;"
     "  color.a = 1.0;"
     "  gl_FragColor = color;"
     FRAGMENT_SHADER_END
     "}";

static GstStaticPadTemplate sinktemplate_all 
 = GST_STATIC_PAD_TEMPLATE ("sink",
                            GST_PAD_SINK,
                            GST_PAD_ALWAYS,
                            GST_STATIC_CAPS (GST_VIDEO_CAPS_YUV("AYUV") ";" \
                                             GST_VIDEO_CAPS_YUV("YV12") ";" \
                                             GST_VIDEO_CAPS_YUV("I420") ";" \
                                             GST_VIDEO_CAPS_RGBA        ";" \
                                             GST_VIDEO_CAPS_BGRA        ";" \
                                             GST_VIDEO_CAPS_RGB         ";" \
                                             GST_VIDEO_CAPS_BGR));

GST_DEBUG_CATEGORY_STATIC (clutter_gst_video_sink_debug);
#define GST_CAT_DEFAULT clutter_gst_video_sink_debug

static GstElementDetails clutter_gst_video_sink_details =
  GST_ELEMENT_DETAILS ("Clutter video sink",
      "Sink/Video",
      "Sends video data from a GStreamer pipeline to a Clutter texture",
      "Jonathan Matthew <jonathan@kaolin.wh9.net>, "
      "Matthew Allum <mallum@o-hand.com, "
      "Chris Lord <chris@o-hand.com>");

enum
{
  PROP_0,
  PROP_TEXTURE,
  PROP_USE_SHADERS,
};

typedef enum
{
  CLUTTER_GST_NOFORMAT,
  CLUTTER_GST_RGB32,
  CLUTTER_GST_RGB24,
  CLUTTER_GST_AYUV,
  CLUTTER_GST_YV12,
  CLUTTER_GST_I420,
} ClutterGstVideoFormat;

typedef void (*GLUNIFORM1IPROC)(GLint location, GLint value);
/* GL_ARB_fragment_program */
typedef void (*GLGENPROGRAMSPROC)(GLsizei n, GLuint *programs);
typedef void (*GLBINDPROGRAMPROC)(GLenum target, GLint program);
typedef void (*GLPROGRAMSTRINGPROC)(GLenum target, GLenum format, GLsizei len,
                                    const void *string);
/* multi-texturing */
typedef void (*GLACTIVETEXTUREPROC)(GLenum unit);
typedef void (*GLMULTITEXCOORD2FPROC)(GLenum target, GLfloat s, GLfloat t);

typedef struct _ClutterGstSymbols
{
  /* GL_ARB_fragment_program */
  GLGENPROGRAMSPROC   glGenProgramsARB;
  GLBINDPROGRAMPROC   glBindProgramARB;
  GLPROGRAMSTRINGPROC glProgramStringARB;

  /* multi-texturing */
  GLACTIVETEXTUREPROC glActiveTextureARB;
  GLMULTITEXCOORD2FPROC glMultiTexCoord2fARB;
} ClutterGstSymbols;

/*
 * features: what does the underlaying video card supports ?
 */
typedef enum _ClutterGstFeatures
{
  CLUTTER_GST_FP             = 0x1, /* fragment programs (ARB fp1.0) */
  CLUTTER_GST_GLSL           = 0x2, /* GLSL */
  CLUTTER_GST_MULTI_TEXTURE  = 0x4, /* multi-texturing */
} ClutterGstFeatures;

/*
 * renderer: abstracts a backend to render a frame.
 */
typedef struct _ClutterGstRenderer
{
 const char            *name;     /* user friendly name */
 ClutterGstVideoFormat  format;   /* the format handled by this renderer */
 int                    flags;    /* ClutterGstFeatures ORed flags */
 GstStaticCaps          caps;     /* caps handled by the renderer */

 void (*init)       (ClutterActor        *actor,
                     ClutterGstVideoSink *sink);
 void (*upload)     (ClutterGstVideoSink *sink,
                     GstBuffer           *buffer);
 void (*paint)      (ClutterActor        *actor,
                     ClutterGstVideoSink *sink);
 void (*post_paint) (ClutterActor        *actor,
                     ClutterGstVideoSink *sink);
} ClutterGstRenderer;

struct _ClutterGstVideoSinkPrivate
{
  ClutterTexture        *texture;
  CoglHandle             u_tex;
  CoglHandle             v_tex;
  CoglHandle             program;
  CoglHandle             shader;
  GLuint                 fp;

  GMutex                *buffer_lock;   /* mutex for the buffer and idle_id */
  GstBuffer             *buffer;
  guint                  idle_id;

  ClutterGstVideoFormat  format;
  gboolean               bgr;
  int                    width;
  int                    height;
  int                    fps_n, fps_d;
  int                    par_n, par_d;
  gboolean               use_shaders;
  gboolean               shaders_init;
  
  ClutterGstSymbols      syms;          /* extra OpenGL functions */
  GLUNIFORM1IPROC        glUniform1iARB;

  GSList                *renderers;
  GstCaps               *caps;
  ClutterGstRenderer    *renderer;
};


#define _do_init(bla) \
  GST_DEBUG_CATEGORY_INIT (clutter_gst_video_sink_debug, \
                                 "cluttersink", \
                                 0, \
                                 "clutter video sink")

GST_BOILERPLATE_FULL (ClutterGstVideoSink,
                          clutter_gst_video_sink,
                      GstBaseSink,
                      GST_TYPE_BASE_SINK,
                      _do_init);

/*
 * Small helpers
 */

static void
_string_array_to_char_array (char	*dst,
                             const char *src[])
{
  int i, n;

  for (i = 0; src[i]; i++) {
      n = strlen (src[i]);
      memcpy (dst, src[i], n);
      dst += n;
  }
  *dst = '\0';
}

static void
clutter_gst_video_sink_fp_paint (ClutterActor        *actor,
                                ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;

  glEnable (GL_FRAGMENT_PROGRAM_ARB);
  priv->syms.glBindProgramARB(GL_FRAGMENT_PROGRAM_ARB, priv->fp);

}

static void
clutter_gst_video_sink_set_fp_shader (ClutterGstVideoSink *sink,
                                      const gchar         *shader_src,
                                      const int            size)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;

  priv->shaders_init = FALSE;

  glEnable (GL_FRAGMENT_PROGRAM_ARB);
  priv->syms.glGenProgramsARB (1, &priv->fp);
  priv->syms.glBindProgramARB (GL_FRAGMENT_PROGRAM_ARB, priv->fp);
  priv->syms.glProgramStringARB (GL_FRAGMENT_PROGRAM_ARB,
                                  GL_PROGRAM_FORMAT_ASCII_ARB,
                                  size,
                                  (const GLbyte *)shader_src);

  glDisable(GL_FRAGMENT_PROGRAM_ARB);

  /* Hook onto the pre-paint signal to bind the shader. */
  g_signal_connect (priv->texture,
                    "paint",
                    G_CALLBACK (clutter_gst_video_sink_fp_paint),
                    sink);
  priv->shaders_init = TRUE;

}

static void
clutter_gst_video_sink_paint (ClutterActor        *actor,
                              ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;
  if (priv->program)
    cogl_program_use (priv->program);
}

static void
clutter_gst_video_sink_set_shader (ClutterGstVideoSink *sink,
                                   const gchar         *shader_src)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;
  
  priv->shaders_init = FALSE;
  if (priv->texture)
    clutter_actor_set_shader (CLUTTER_ACTOR (priv->texture), NULL);

  if (priv->program)
    {
      cogl_program_unref (priv->program);
      priv->program = NULL;
    }
  
  if (priv->shader)
    {
      cogl_program_unref (priv->shader);
      priv->shader = NULL;
    }
  
  if (shader_src)
    {
      ClutterShader *shader;

      /* Set a dummy shader so we don't interfere with the shader stack */
      shader = clutter_shader_new ();
      clutter_shader_set_fragment_source (shader, dummy_shader, -1);
      clutter_actor_set_shader (CLUTTER_ACTOR (priv->texture), shader);
      g_object_unref (shader);

      /* Create shader through COGL - necessary as we need to be able to set
       * integer uniform variables for multi-texturing.
       */
      priv->shader = cogl_create_shader (COGL_SHADER_TYPE_FRAGMENT);
      cogl_shader_source (priv->shader, shader_src);
      cogl_shader_compile (priv->shader);
      
      priv->program = cogl_create_program ();
      cogl_program_attach_shader (priv->program, priv->shader);
      cogl_program_link (priv->program);

      /* Hook onto the pre-paint signal to replace the dummy shader with
       * the real shader.
       */
      g_signal_connect (priv->texture,
                        "paint",
                        G_CALLBACK (clutter_gst_video_sink_paint),
                        sink);
      
      priv->shaders_init = TRUE;
    }
}

/* some renderers don't need all the ClutterGstRenderer vtable */
static void
clutter_gst_dummy_init (ClutterActor        *actor,
                        ClutterGstVideoSink *sink)
{
}

/*
 * RGB 24 / BGR 24
 *
 * 3 bytes per pixel, stride % 4 = 0.
 */

static void
clutter_gst_rgb24_upload (ClutterGstVideoSink *sink,
                          GstBuffer           *buffer)
{
  ClutterGstVideoSinkPrivate *priv= sink->priv;

  clutter_texture_set_from_rgb_data (priv->texture,
                                     GST_BUFFER_DATA (buffer),
                                     FALSE,
                                     priv->width,
                                     priv->height,
                                     GST_ROUND_UP_4 (3 * priv->width),
                                     3,
                                     priv->bgr ?
                                     CLUTTER_TEXTURE_RGB_FLAG_BGR : 0,
                                     NULL);
}

static ClutterGstRenderer rgb24_renderer =
{
  "RGB 24",
  CLUTTER_GST_RGB24,
  0,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_RGB ";" GST_VIDEO_CAPS_BGR),
  clutter_gst_dummy_init,
  clutter_gst_rgb24_upload,
  NULL,
  NULL,
};

/*
 * RGBA / BGRA 8888
 */

static void
clutter_gst_rgb32_upload (ClutterGstVideoSink *sink,
                          GstBuffer           *buffer)
{
  ClutterGstVideoSinkPrivate *priv= sink->priv;

  clutter_texture_set_from_rgb_data (priv->texture,
                                     GST_BUFFER_DATA (buffer),
                                     TRUE,
                                     priv->width,
                                     priv->height,
                                     GST_ROUND_UP_4 (4 * priv->width),
                                     4,
                                     priv->bgr ?
                                     CLUTTER_TEXTURE_RGB_FLAG_BGR : 0,
                                     NULL);
}

static ClutterGstRenderer rgb32_renderer =
{
  "RGB 32",
  CLUTTER_GST_RGB32,
  0,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_RGBA ";" GST_VIDEO_CAPS_BGRA),
  clutter_gst_dummy_init,
  clutter_gst_rgb32_upload,
  NULL,
  NULL,
};

#ifdef CLUTTER_COGL_HAS_GL

/*
 * YV12
 *
 * 8 bit Y plane followed by 8 bit 2x2 subsampled V and U planes.
 */

static void
clutter_gst_yv12_glsl_init (ClutterActor        *actor,
                            ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv= sink->priv;
  GLint location;

  clutter_gst_video_sink_set_shader (sink,
                                     yv12_to_rgba_shader);

  cogl_program_use (priv->program);
  location = cogl_program_get_uniform_location (priv->program, "ytex");
  priv->glUniform1iARB (location, 0);
  location = cogl_program_get_uniform_location (priv->program, "utex");
  priv->glUniform1iARB (location, 1);
  location = cogl_program_get_uniform_location (priv->program, "vtex");
  priv->glUniform1iARB (location, 2);

  cogl_program_use (COGL_INVALID_HANDLE);
}

static void
clutter_gst_yv12_upload (ClutterGstVideoSink *sink,
                         GstBuffer           *buffer)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;

  CoglHandle y_tex = cogl_texture_new_from_data (priv->width,
                                                 priv->height,
                                                 -1,
                                                 COGL_TEXTURE_NONE,
                                                 COGL_PIXEL_FORMAT_G_8,
                                                 COGL_PIXEL_FORMAT_G_8,
                                                 priv->width,
                                                 GST_BUFFER_DATA (buffer));
  cogl_texture_set_filters (y_tex,
                            COGL_TEXTURE_FILTER_LINEAR,
                            COGL_TEXTURE_FILTER_LINEAR);
  clutter_texture_set_cogl_texture (priv->texture, y_tex);
  cogl_texture_unref (y_tex);

  if (priv->u_tex)
    cogl_texture_unref (priv->u_tex);

  if (priv->v_tex)
    cogl_texture_unref (priv->v_tex);

  priv->v_tex = cogl_texture_new_from_data (priv->width/2,
                                            priv->height/2,
                                            -1,
                                            COGL_TEXTURE_NONE,
                                            COGL_PIXEL_FORMAT_G_8,
                                            COGL_PIXEL_FORMAT_G_8,
                                            priv->width/2,
                                            GST_BUFFER_DATA (buffer) +
                                            (priv->width * priv->height));
  cogl_texture_set_filters (priv->v_tex,
                            COGL_TEXTURE_FILTER_LINEAR,
                            COGL_TEXTURE_FILTER_LINEAR);

  priv->u_tex =
    cogl_texture_new_from_data (priv->width/2,
                                priv->height/2,
                                -1,
                                COGL_TEXTURE_NONE,
                                COGL_PIXEL_FORMAT_G_8,
                                COGL_PIXEL_FORMAT_G_8,
                                priv->width/2,
                                GST_BUFFER_DATA (buffer) +
                                (priv->width * priv->height) +
                                (priv->width/2 * priv->height/2));
  cogl_texture_set_filters (priv->u_tex,
                            COGL_TEXTURE_FILTER_LINEAR,
                            COGL_TEXTURE_FILTER_LINEAR);
}

static void
clutter_gst_yv12_paint (ClutterActor        *actor,
                        ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;
  GLuint texture;

  /* Bind the U and V textures in texture units 1 and 2 */
  if (priv->u_tex)
    {
      cogl_texture_get_gl_texture (priv->u_tex, &texture, NULL);
      priv->syms.glActiveTextureARB (GL_TEXTURE1);
      glEnable (GL_TEXTURE_2D);
      glBindTexture (GL_TEXTURE_2D, texture);
    }

  if (priv->v_tex)
    {
      cogl_texture_get_gl_texture (priv->v_tex, &texture, NULL);
      priv->syms.glActiveTextureARB (GL_TEXTURE2);
      glEnable (GL_TEXTURE_2D);
      glBindTexture (GL_TEXTURE_2D, texture);
    }

  priv->syms.glActiveTextureARB (GL_TEXTURE0_ARB);
}

static void
clutter_gst_yv12_glsl_post_paint (ClutterActor        *actor,
                                  ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;

  /* Disable the extra texture units */
  priv->syms.glActiveTextureARB (GL_TEXTURE1);
  glDisable (GL_TEXTURE_2D);
  priv->syms.glActiveTextureARB (GL_TEXTURE2);
  glDisable (GL_TEXTURE_2D);
  priv->syms.glActiveTextureARB (GL_TEXTURE0);
}

static ClutterGstRenderer yv12_glsl_renderer =
{
  "YV12 glsl",
  CLUTTER_GST_YV12,
  CLUTTER_GST_GLSL | CLUTTER_GST_MULTI_TEXTURE,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_YUV ("YV12")),
  clutter_gst_yv12_glsl_init,
  clutter_gst_yv12_upload,
  clutter_gst_yv12_paint,
  clutter_gst_yv12_glsl_post_paint,
};

/*
 * YV12 (fragment program version)
 *
 * 8 bit Y plane followed by 8 bit 2x2 subsampled V and U planes.
 */

static void
clutter_gst_yv12_fp_init (ClutterActor        *actor,
                          ClutterGstVideoSink *sink)
{
  gchar *shader;

  shader = g_malloc(YV12_FP_SZ + 1);
  _string_array_to_char_array (shader, YV12_fp);

  /* the size given to glProgramStringARB is without the trailing '\0', which
   * is precisely YV12_FP_SZ */
  clutter_gst_video_sink_set_fp_shader (sink, shader, YV12_FP_SZ);
  g_free(shader);
}

static void
clutter_gst_yv12_fp_post_paint (ClutterActor        *actor,
                                ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;

  /* Disable the extra texture units */
  priv->syms.glActiveTextureARB (GL_TEXTURE1);
  glDisable (GL_TEXTURE_2D);
  priv->syms.glActiveTextureARB (GL_TEXTURE2);
  glDisable (GL_TEXTURE_2D);
  priv->syms.glActiveTextureARB (GL_TEXTURE0);

  /* Disable the shader */
  glDisable (GL_FRAGMENT_PROGRAM_ARB);
}

static ClutterGstRenderer yv12_fp_renderer =
{
  "YV12 fp",
  CLUTTER_GST_YV12,
  CLUTTER_GST_FP | CLUTTER_GST_MULTI_TEXTURE,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_YUV ("YV12")),
  clutter_gst_yv12_fp_init,
  clutter_gst_yv12_upload,
  clutter_gst_yv12_paint,
  clutter_gst_yv12_fp_post_paint,
};

/*
 * I420
 *
 * 8 bit Y plane followed by 8 bit 2x2 subsampled U and V planes.
 * Basically the same as YV12, but with the 2 chroma planes switched.
 */

static void
clutter_gst_i420_glsl_init (ClutterActor        *actor,
                            ClutterGstVideoSink *sink)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;
  GLint location;

  clutter_gst_video_sink_set_shader (sink,
                                     yv12_to_rgba_shader);

  cogl_program_use (priv->program);
  location = cogl_program_get_uniform_location (priv->program, "ytex");
  priv->glUniform1iARB (location, 0);
  location = cogl_program_get_uniform_location (priv->program, "vtex");
  priv->glUniform1iARB (location, 1);
  location = cogl_program_get_uniform_location (priv->program, "utex");
  priv->glUniform1iARB (location, 2);
  cogl_program_use (COGL_INVALID_HANDLE);
}

static ClutterGstRenderer i420_glsl_renderer =
{
  "I420 glsl",
  CLUTTER_GST_I420,
  CLUTTER_GST_GLSL | CLUTTER_GST_MULTI_TEXTURE,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_YUV ("I420")),
  clutter_gst_i420_glsl_init,
  clutter_gst_yv12_upload,
  clutter_gst_yv12_paint,
  clutter_gst_yv12_glsl_post_paint,
};

/*
 * I420 (fragment program version)
 *
 * 8 bit Y plane followed by 8 bit 2x2 subsampled U and V planes.
 * Basically the same as YV12, but with the 2 chroma planes switched.
 */

static void
clutter_gst_i420_fp_init (ClutterActor        *actor,
                          ClutterGstVideoSink *sink)
{
  gchar *shader;

  shader = g_malloc(I420_FP_SZ + 1);
  _string_array_to_char_array (shader, I420_fp);

  /* the size given to glProgramStringARB is without the trailing '\0', which
   * is precisely I420_FP_SZ */
  clutter_gst_video_sink_set_fp_shader (sink, shader, I420_FP_SZ);
  g_free(shader);
}

static ClutterGstRenderer i420_fp_renderer =
{
  "I420 fp",
  CLUTTER_GST_I420,
  CLUTTER_GST_FP | CLUTTER_GST_MULTI_TEXTURE,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_YUV ("I420")),
  clutter_gst_i420_fp_init,
  clutter_gst_yv12_upload,
  clutter_gst_yv12_paint,
  clutter_gst_yv12_fp_post_paint,
};

#endif /* CLUTTER_COGL_HAS_GL */

/*
 * AYUV
 *
 * This is a 4:4:4 YUV format with 8 bit samples for each component along
 * with an 8 bit alpha blend value per pixel. Component ordering is A Y U V
 * (as the name suggests).
 */

static void
clutter_gst_ayuv_glsl_init(ClutterActor        *actor,
                           ClutterGstVideoSink *sink)
{
  clutter_gst_video_sink_set_shader (sink, ayuv_to_rgba_shader);
}

static void
clutter_gst_ayuv_upload (ClutterGstVideoSink *sink,
                         GstBuffer           *buffer)
{
  ClutterGstVideoSinkPrivate *priv= sink->priv;

  clutter_texture_set_from_rgb_data (priv->texture,
                                     GST_BUFFER_DATA (buffer),
                                     TRUE,
                                     priv->width,
                                     priv->height,
                                     GST_ROUND_UP_4 (4 * priv->width),
                                     4,
                                     0,
                                     NULL);
}

static ClutterGstRenderer ayuv_glsl_renderer =
{
  "AYUV glsl",
  CLUTTER_GST_AYUV,
  CLUTTER_GST_GLSL,
  GST_STATIC_CAPS (GST_VIDEO_CAPS_YUV ("AYUV")),
  clutter_gst_ayuv_glsl_init,
  clutter_gst_ayuv_upload,
  NULL,
  NULL,
};

static GSList *
clutter_gst_build_renderers_list (ClutterGstSymbols *syms)
{
  GSList             *list = NULL;
  const gchar        *gl_extensions;
  GLint               nb_texture_units;
  gint                features = 0, i;
  /* The order of the list of renderers is important. They will be prepended
   * to a GSList and we'll iterate over that list to choose the first matching
   * renderer. Thus if you want to use the fp renderer over the glsl one, the
   * fp renderer has to be put after the glsl one in this array */
  ClutterGstRenderer *renderers[] =
    {
      &rgb24_renderer,
      &rgb32_renderer,
      &yv12_glsl_renderer,
      &yv12_fp_renderer,
      &i420_glsl_renderer,
      &i420_fp_renderer,
      &ayuv_glsl_renderer,
      NULL
    };

  /* get the features */
  gl_extensions = (const gchar*) glGetString (GL_EXTENSIONS);
  if (cogl_check_extension ("GL_ARB_multitexture", gl_extensions))
    {
      /* we need 3 texture units for planar YUV */
      glGetIntegerv (GL_MAX_TEXTURE_UNITS_ARB, &nb_texture_units);

      syms->glActiveTextureARB = (GLACTIVETEXTUREPROC)
        cogl_get_proc_address ("glActiveTextureARB");
      syms->glMultiTexCoord2fARB = (GLMULTITEXCOORD2FPROC)
        cogl_get_proc_address ("glMultiTexCoord2fARB");

      if (nb_texture_units >= 3 &&
          syms->glActiveTextureARB &&
          syms->glMultiTexCoord2fARB)
        {
          features |= CLUTTER_GST_MULTI_TEXTURE;
        }
    }

  if (cogl_check_extension ("GL_ARB_fragment_program", gl_extensions))
    {
      /* the shaders we'll feed to the GPU are simple enough, we don't need
       * to check GL limits for GL_FRAGMENT_PROGRAM_ARB */

      syms->glGenProgramsARB = (GLGENPROGRAMSPROC)
        cogl_get_proc_address ("glGenProgramsARB");
      syms->glBindProgramARB = (GLBINDPROGRAMPROC)
        cogl_get_proc_address ("glBindProgramARB");
      syms->glProgramStringARB = (GLPROGRAMSTRINGPROC)
        cogl_get_proc_address ("glProgramStringARB");

      if (syms->glGenProgramsARB &&
          syms->glBindProgramARB &&
          syms->glProgramStringARB)
        {
          features |= CLUTTER_GST_FP;
        }
    }

  if (cogl_features_available (COGL_FEATURE_SHADERS_GLSL))
    features |= CLUTTER_GST_GLSL;

  GST_INFO ("GL features: 0x%08x", features);

  for (i = 0; renderers[i]; i++)
    {
      gint needed = renderers[i]->flags;

      if ((needed & features) == needed)
        list = g_slist_prepend (list, renderers[i]);
    }

  return list;
}

static void
append_cap (gpointer data, gpointer user_data)
{
  ClutterGstRenderer *renderer = (ClutterGstRenderer *)data;
  GstCaps *caps = (GstCaps *)user_data;
  GstCaps *writable_caps;

  writable_caps =
    gst_caps_make_writable (gst_static_caps_get (&renderer->caps));
  gst_caps_append (caps, writable_caps);
}

static GstCaps *
clutter_gst_build_caps (GSList *renderers)
{
  GstCaps *caps;

  caps = gst_caps_new_empty ();

  g_slist_foreach (renderers, append_cap, caps);

  return caps;
}

ClutterGstRenderer *
clutter_gst_find_renderer_by_format (ClutterGstVideoSink  *sink,
                                     ClutterGstVideoFormat format)
{
  ClutterGstVideoSinkPrivate *priv = sink->priv;
  ClutterGstRenderer *renderer = NULL;
  GSList *element;

  for (element = priv->renderers; element; element = g_slist_next(element))
    {
      ClutterGstRenderer *candidate = (ClutterGstRenderer *)element->data;

      if (candidate->format == format)
        {
          renderer = candidate;
          break;
        }
    }

  return renderer;
}

static gboolean
clutter_gst_video_sink_idle_func (gpointer data)
{
  ClutterGstVideoSink        *sink;
  ClutterGstVideoSinkPrivate *priv;
  GstBuffer                  *buffer;

  sink = data;
  priv = sink->priv;

  g_mutex_lock (priv->buffer_lock);
  if (!priv->buffer)
    {
      priv->idle_id = 0;
      g_mutex_unlock (priv->buffer_lock);
      return FALSE;
    }

  buffer = priv->buffer;
  priv->buffer = NULL;

  if (G_UNLIKELY (!GST_IS_BUFFER (buffer)))
    {
      priv->idle_id = 0;
      g_mutex_unlock (priv->buffer_lock);
      return FALSE;
    }

  priv->idle_id = 0;
  g_mutex_unlock (priv->buffer_lock);


  if ((priv->format == CLUTTER_GST_RGB32) || (priv->format == CLUTTER_GST_AYUV))
    {
      priv->renderer->upload (sink, buffer);

      /* Initialise AYUV shader */
      if ((priv->format == CLUTTER_GST_AYUV) && !priv->shaders_init)
        priv->renderer->upload (sink, buffer);
    }
  else if (priv->format == CLUTTER_GST_RGB24)
    {
      priv->renderer->upload (sink, buffer);
    }
  else if (priv->format == CLUTTER_GST_YV12 || priv->format == CLUTTER_GST_I420)
    {
      
      priv->renderer->upload (sink, buffer);

      /* Initialize renderer */
      if (!priv->shaders_init)
        {
          priv->renderer->init (CLUTTER_ACTOR (priv->texture), sink);
          g_signal_connect (priv->texture,
                            "paint",
                            G_CALLBACK (priv->renderer->paint),
                            sink);
          g_signal_connect_after (priv->texture,
                                  "paint",
                                  G_CALLBACK (priv->renderer->post_paint),
                                  sink);
        }
    }

  gst_buffer_unref (buffer);
  
  return FALSE;
}

static void
clutter_gst_video_sink_base_init (gpointer g_class)
{
  GstElementClass *element_class = GST_ELEMENT_CLASS (g_class);

  gst_element_class_add_pad_template
                     (element_class,
                      gst_static_pad_template_get (&sinktemplate_all));

  gst_element_class_set_details (element_class,
                                 &clutter_gst_video_sink_details);
}

static void
clutter_gst_video_sink_init (ClutterGstVideoSink      *sink,
                             ClutterGstVideoSinkClass *klass)
{
  ClutterGstVideoSinkPrivate *priv;

  sink->priv = priv =
    G_TYPE_INSTANCE_GET_PRIVATE (sink, CLUTTER_GST_TYPE_VIDEO_SINK,
                                 ClutterGstVideoSinkPrivate);

  priv->buffer_lock = g_mutex_new ();
  priv->use_shaders = TRUE;
  priv->renderers = clutter_gst_build_renderers_list (&priv->syms);
  priv->caps = clutter_gst_build_caps (priv->renderers);

#ifdef CLUTTER_COGL_HAS_GL
  priv->glUniform1iARB = (GLUNIFORM1IPROC)
    cogl_get_proc_address ("glUniform1iARB");
#endif
}

static GstFlowReturn
clutter_gst_video_sink_render (GstBaseSink *bsink,
                               GstBuffer   *buffer)
{
  ClutterGstVideoSink *sink;
  ClutterGstVideoSinkPrivate *priv;

  sink = CLUTTER_GST_VIDEO_SINK (bsink);
  priv = sink->priv;


  g_mutex_lock (priv->buffer_lock);
  if (priv->buffer)
    { 
      gst_buffer_unref (priv->buffer);
    }
  priv->buffer = gst_buffer_ref (buffer);

  if (priv->idle_id == 0)
    {
      priv->idle_id = clutter_threads_add_idle_full (G_PRIORITY_HIGH_IDLE,
                                     clutter_gst_video_sink_idle_func,
                                     sink,
                                     NULL);
      /* the lock must be held when adding this idle, if it is not the idle
       * callback would be invoked before priv->idle_id had been assigned
       */
    }
  g_mutex_unlock (priv->buffer_lock);

  return GST_FLOW_OK;
}

static GstCaps *
clutter_gst_video_sink_get_caps (GstBaseSink *bsink)
{
  ClutterGstVideoSink *sink;

  sink = CLUTTER_GST_VIDEO_SINK (bsink);
  return gst_caps_copy (sink->priv->caps);
}

static gboolean
clutter_gst_video_sink_set_caps (GstBaseSink *bsink,
                                 GstCaps     *caps)
{
  ClutterGstVideoSink        *sink;
  ClutterGstVideoSinkPrivate *priv;
  GstCaps                    *intersection;
  GstStructure               *structure;
  gboolean                    ret;
  const GValue               *fps;
  const GValue               *par;
  gint                        width, height;
  guint32                     fourcc;
  int                         red_mask, blue_mask;

  sink = CLUTTER_GST_VIDEO_SINK(bsink);
  priv = sink->priv;

  clutter_gst_video_sink_set_shader (sink, NULL);

  intersection = gst_caps_intersect (priv->caps, caps);
  if (gst_caps_is_empty (intersection)) 
    return FALSE;

  gst_caps_unref (intersection);

  structure = gst_caps_get_structure (caps, 0);

  ret  = gst_structure_get_int (structure, "width", &width);
  ret &= gst_structure_get_int (structure, "height", &height);
  fps  = gst_structure_get_value (structure, "framerate");
  ret &= (fps != NULL);

  par  = gst_structure_get_value (structure, "pixel-aspect-ratio");

  if (!ret)
    return FALSE;

  priv->width  = width;
  priv->height = height;

  /* We dont yet use fps or pixel aspect into but handy to have */
  priv->fps_n  = gst_value_get_fraction_numerator (fps);
  priv->fps_d  = gst_value_get_fraction_denominator (fps);

  if (par) 
    {
      priv->par_n = gst_value_get_fraction_numerator (par);
      priv->par_d = gst_value_get_fraction_denominator (par);
    } 
  else 
    priv->par_n = priv->par_d = 1;

#if CLUTTER_COGL_HAS_GL
  ret = gst_structure_get_fourcc (structure, "format", &fourcc);
  if (ret && (fourcc == GST_MAKE_FOURCC ('Y', 'V', '1', '2')))
    {
      priv->format = CLUTTER_GST_YV12;
    }
  else if (ret && (fourcc == GST_MAKE_FOURCC ('I', '4', '2', '0')))
    {
      priv->format = CLUTTER_GST_I420;
    }
  else if (ret && (fourcc == GST_MAKE_FOURCC ('A', 'Y', 'U', 'V')))
    {
      priv->format = CLUTTER_GST_AYUV;
      priv->bgr = FALSE;
    }
  else
#endif
    {
      guint32 width;
      gst_structure_get_int (structure, "red_mask", &red_mask);
      gst_structure_get_int (structure, "blue_mask", &blue_mask);
      
      width = red_mask | blue_mask;
      if (width < 0x1000000)
        {
          priv->format = CLUTTER_GST_RGB24;
          priv->bgr = (red_mask == 0xff0000) ? FALSE : TRUE;
        }
      else
        {
          priv->format = CLUTTER_GST_RGB32;
          priv->bgr = (red_mask == 0xff000000) ? FALSE : TRUE;
        }
    }

  priv->renderer = clutter_gst_find_renderer_by_format (sink, priv->format);
  if (G_UNLIKELY (priv->renderer == NULL))
    {
      GST_ERROR_OBJECT (sink, "could not find a suitable renderer");
      return FALSE;
    }

  GST_INFO_OBJECT (sink, "using the %s renderer", priv->renderer->name);

  return TRUE;
}

static void
clutter_gst_video_sink_dispose (GObject *object)
{
  ClutterGstVideoSink *self;
  ClutterGstVideoSinkPrivate *priv;

  self = CLUTTER_GST_VIDEO_SINK (object);
  priv = self->priv;

  clutter_gst_video_sink_set_shader (self, NULL);

  if (priv->idle_id > 0)
    {
      g_source_remove (priv->idle_id);
      priv->idle_id = 0;
    }

  if (priv->texture)
    {
      g_object_unref (priv->texture);
      priv->texture = NULL;
    }

  if (priv->buffer_lock)
    {
      g_mutex_free (priv->buffer_lock);
      priv->buffer_lock = NULL;
    }

  if (priv->caps)
    {
      gst_caps_unref (priv->caps);
      priv->caps = NULL;
    }

  G_OBJECT_CLASS (parent_class)->dispose (object);
}

static void
clutter_gst_video_sink_finalize (GObject *object)
{
  ClutterGstVideoSink *self;
  ClutterGstVideoSinkPrivate *priv;

  self = CLUTTER_GST_VIDEO_SINK (object);
  priv = self->priv;

  g_slist_free (priv->renderers);

  G_OBJECT_CLASS (parent_class)->finalize (object);
}

static void
clutter_gst_video_sink_set_property (GObject *object,
                                     guint prop_id,
                                     const GValue *value,
                                     GParamSpec *pspec)
{
  ClutterGstVideoSink *sink;
  ClutterGstVideoSinkPrivate *priv;
  gboolean use_shaders;

  sink = CLUTTER_GST_VIDEO_SINK (object);
  priv = sink->priv;

  switch (prop_id) 
    {
    case PROP_TEXTURE:
      if (priv->texture)
        g_object_unref (priv->texture);

      priv->texture = CLUTTER_TEXTURE (g_value_dup_object (value));
      break;
    case PROP_USE_SHADERS:
      use_shaders = g_value_get_boolean (value);
      if (priv->use_shaders != use_shaders)
        {
          priv->use_shaders = use_shaders;
          g_object_notify (object, "use-shaders");
        }
      break;
    default:
      G_OBJECT_WARN_INVALID_PROPERTY_ID (object, prop_id, pspec);
      break;
    }
}

static void
clutter_gst_video_sink_get_property (GObject *object,
                                     guint prop_id,
                                     GValue *value,
                                     GParamSpec *pspec)
{
  ClutterGstVideoSink *sink;

  sink = CLUTTER_GST_VIDEO_SINK (object);

  switch (prop_id) 
    {
    case PROP_TEXTURE:
      g_value_set_object (value, sink->priv->texture);
      break;
    case PROP_USE_SHADERS:
      g_value_set_boolean (value, sink->priv->use_shaders);
      break;
    default:
      G_OBJECT_WARN_INVALID_PROPERTY_ID (object, prop_id, pspec);
      break;
  }
}

static gboolean
clutter_gst_video_sink_stop (GstBaseSink *base_sink)
{
  ClutterGstVideoSinkPrivate *priv;

  priv = CLUTTER_GST_VIDEO_SINK (base_sink)->priv;

  g_mutex_lock (priv->buffer_lock);
  if (priv->buffer)
    gst_buffer_unref (priv->buffer);
  priv->buffer = NULL;
  g_mutex_unlock (priv->buffer_lock);


  return TRUE;
}

static void
clutter_gst_video_sink_class_init (ClutterGstVideoSinkClass *klass)
{
  GObjectClass *gobject_class = G_OBJECT_CLASS (klass);
  GstBaseSinkClass *gstbase_sink_class = GST_BASE_SINK_CLASS (klass);

  g_type_class_add_private (klass, sizeof (ClutterGstVideoSinkPrivate));

  gobject_class->set_property = clutter_gst_video_sink_set_property;
  gobject_class->get_property = clutter_gst_video_sink_get_property;

  gobject_class->dispose = clutter_gst_video_sink_dispose;
  gobject_class->finalize = clutter_gst_video_sink_finalize;

  gstbase_sink_class->render = clutter_gst_video_sink_render;
  gstbase_sink_class->preroll = clutter_gst_video_sink_render;
  gstbase_sink_class->stop = clutter_gst_video_sink_stop;
  gstbase_sink_class->set_caps = clutter_gst_video_sink_set_caps;
  gstbase_sink_class->get_caps = clutter_gst_video_sink_get_caps;

  g_object_class_install_property 
              (gobject_class, PROP_TEXTURE,
               g_param_spec_object ("texture",
                                    "texture",
                                    "Target ClutterTexture object",
                                    CLUTTER_TYPE_TEXTURE,
                                    G_PARAM_READWRITE));

  g_object_class_install_property 
              (gobject_class, PROP_USE_SHADERS,
               g_param_spec_boolean ("use-shaders",
                                     "Use shaders",
                                     "Use a fragment shader to accelerate "
                                     "colour-space conversion.",
                                     TRUE,
                                     G_PARAM_READWRITE));
}

/**
 * clutter_gst_video_sink_new:
 * @texture: a #ClutterTexture
 *
 * Creates a new GStreamer video sink which uses @texture as the target
 * for sinking a video stream from GStreamer.
 *
 * Return value: a #GstElement for the newly created video sink
 */
GstElement *
clutter_gst_video_sink_new (ClutterTexture *texture)
{
  return g_object_new (CLUTTER_GST_TYPE_VIDEO_SINK,
                       "texture", texture,
                       NULL);
}

static gboolean
plugin_init (GstPlugin *plugin)
{
  gboolean ret = gst_element_register (plugin,
                                             "cluttersink",
                                       GST_RANK_PRIMARY,
                                       CLUTTER_GST_TYPE_VIDEO_SINK);
  return ret;
}

GST_PLUGIN_DEFINE_STATIC (GST_VERSION_MAJOR,
                          GST_VERSION_MINOR,
                          "cluttersink",
                          "Element to render to Clutter textures",
                          plugin_init,
                          VERSION,
                          "LGPL", /* license */
                          PACKAGE,
                          "http://www.clutter-project.org");
