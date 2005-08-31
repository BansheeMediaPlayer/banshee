/* -*- Mode: csharp; tab-width: 4; c-basic-offset: 4; indent-tabs-mode: t -*- */
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
 * Revision 1.2  2005/08/31 07:59:04  jwillcox
 * 2005-08-31  James Willcox  <snorp@snorp.net>
 *
 *         * add an emacs modeline to all the .cs sources
 *         * src/IpodCore.cs: fix iPod syncing.
 *         * src/PlayerInterface.cs (OnSimpleSearch): fix a null reference that
 *         was causing some crashes.
 *
 * Revision 1.1  2005/08/25 21:12:07  abock
 * Forgot Ogg too
 *
 * Revision 1.4  2005/02/08 12:54:42  kikidonk
 * Added cvs log and header
 *
 */

using System.IO;
using Entagged.Audioformats.Util;
using Entagged.Audioformats.Ogg.Util;

namespace Entagged.Audioformats.Ogg 
{
	[SupportedMimeType ("application/ogg")]
	[SupportedMimeType ("application/x-ogg")]
	[SupportedMimeType ("audio/vorbis")]
	[SupportedMimeType ("audio/x-vorbis")]
	[SupportedMimeType ("audio/ogg")]
	[SupportedMimeType ("audio/x-ogg")]
	[SupportedMimeType ("entagged/ogg")]
	public class OggFileReader : AudioFileReader 
	{
		private OggInfoReader ir = new OggInfoReader();
		private VorbisTagReader otr = new VorbisTagReader();
		
		protected override EncodingInfo GetEncodingInfo(Stream raf, 
			string mime)  
		{
			return ir.Read(raf);
		}
		
		protected override Tag GetTag(Stream raf, string mime)  
		{
			return otr.Read(raf);
		}
	}
}

