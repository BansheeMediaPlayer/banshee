/***************************************************************************
 *  Copyright 2005 Raphaël Slinckx <raphael@slinckx.net> 
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

/*
 * $Log$
 * Revision 1.7  2005/08/19 02:17:09  abock
 * Updated to entagged-sharp 0.1.4
 *
 * Revision 1.7  2005/02/25 15:37:40  kikidonk
 * Big structure change
 *
 * Revision 1.6  2005/02/25 15:31:16  kikidonk
 * Big structure change
 *
 * Revision 1.5  2005/02/13 17:23:21  kikidonk
 * Moving tag viewing to a dedicated class, that also show images in gtk# where pplicable
 *
 * Revision 1.4  2005/02/08 12:54:41  kikidonk
 * Added cvs log and header
 *
 */

using Entagged.Audioformats.Exceptions;
using Entagged.Audioformats.Util;
using System.Reflection;
using System.Collections;
using System.IO;
using System;

namespace Entagged.Audioformats {
	public class AudioFileIO {		
		//These tables contains all the readers writers associated with extensions/mimetypes
		private static Hashtable extensions = new Hashtable();
		private static Hashtable mimetypes = new Hashtable();
		
		//Initialize the different readers/writers using reflection
		static AudioFileIO() {
			Assembly assembly = Assembly.GetExecutingAssembly();

			foreach (Type type in assembly.GetTypes()) {
				if (! type.IsSubclassOf(typeof(AudioFileReader)))
					continue;

				AudioFileReader reader = (AudioFileReader) Activator.CreateInstance(type);
				Attribute [] attrs = Attribute.GetCustomAttributes(type, typeof(SupportedExtension));
				foreach (SupportedExtension attr in attrs)
					extensions.Add (attr.Extension, reader);

				attrs = Attribute.GetCustomAttributes (type, typeof(SupportedMimeType));
				foreach (SupportedMimeType attr in attrs)
					mimetypes.Add (attr.MimeType, reader);
			}
		}
		
		public static AudioFile Read(string f) {
			string ext = Utils.GetExtension(f);
			
			object afr = extensions[ext];
			if( afr == null)
				throw new CannotReadException("No Reader associated to this extension: "+ext);
			
			return (afr as AudioFileReader).Read(f);
		}

		public static AudioFile Read(string f, string mimetype) {
			object afr = mimetypes[mimetype];
			if( afr == null)
				throw new CannotReadException("No Reader associated to this MimeType: "+mimetype);

			return (afr as AudioFileReader).Read(f);
		}

		public static AudioFile Read(Stream stream, string mimetype)
		{
			object afr = mimetypes[mimetype];
			if ( afr == null)
				throw new CannotReadException("No Reader associated to this MimeType: "+mimetype);

			return (afr as AudioFileReader).Read(stream);
		}

	}
}
