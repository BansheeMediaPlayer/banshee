//
// AsxPlaylistFormat.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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
using System.IO;
using System.Xml;

using Mono.Unix;

using Hyena;

using Banshee.Sources;

namespace Banshee.Playlists.Formats
{
    // REFERENCE: http://msdn2.microsoft.com/en-us/library/ms925291.aspx

    public class AsxPlaylistFormat : PlaylistFormatBase
    {
        public static readonly PlaylistFormatDescription FormatDescription = new PlaylistFormatDescription(
            typeof(AsxPlaylistFormat), MagicHandler, Catalog.GetString("Windows Media ASX (*.asx)"),
            "asx", new string [] {"video/x-ms-asx", "video/asx", "video/x-ms-asf"});

        public static bool MagicHandler(StreamReader reader)
        {
            try {
                XmlTextReader xml_reader = new XmlTextReader(reader);
                xml_reader.WhitespaceHandling = WhitespaceHandling.None;
                return CheckAsxHeader(xml_reader);
            } catch {
                return false;
            }
        }

        private static bool CheckAsxHeader(XmlTextReader xml_reader)
        {
            xml_reader.Read();
            if(xml_reader.NodeType == XmlNodeType.XmlDeclaration) {
                xml_reader.Read();
            }

            return xml_reader.Depth == 0 && xml_reader.NodeType == XmlNodeType.Element
                && String.Compare("asx", xml_reader.LocalName, true) == 0;
        }

        public AsxPlaylistFormat()
        {
        }

        public override void Load(StreamReader reader, bool validateHeader)
        {
            XmlTextReader xml_reader = new XmlTextReader(reader);
            xml_reader.WhitespaceHandling = WhitespaceHandling.None;
            xml_reader.EntityHandling = EntityHandling.ExpandCharEntities;

            if(!CheckAsxHeader(xml_reader)) {
                throw new InvalidPlaylistException();
            }

            while (xml_reader.Read ()) {
                if (xml_reader.NodeType != XmlNodeType.Element || xml_reader.Depth != 1) {
                    continue;
                }

                switch (xml_reader.LocalName.ToLower ()) {
                    case "title":
                        try {
                            xml_reader.Read ();
                            Title = xml_reader.Value;
                        }
                        catch {
                        }
                        break;

                    case "entry":
                        LoadEntry (xml_reader);
                        break;

                    case "entryref":
                        string href = xml_reader["HREF"] ?? xml_reader["href"];
                        if (href != null) {
                            var secondary = PlaylistParser.Parse (new SafeUri (ResolveUri (href)));
                            if (secondary != null) {
                                // splice in Elements of secondary
                                foreach (PlaylistElement e in secondary.Elements) {
                                    Elements.Add (e);
                                }
                            }
                        }
                        break;
                }
            }
        }

        private void LoadEntry (XmlTextReader xml_reader)
        {
            PlaylistElement element = AddElement ();

            do {
                try {
                    xml_reader.Read ();
                } catch {
                    xml_reader.Skip ();
                }

                if (xml_reader.NodeType != XmlNodeType.Element) {
                    continue;
                }

                switch (xml_reader.LocalName.ToLower ()) {
                    case "title":
                        xml_reader.Read ();
                        element.Title = xml_reader.Value;
                        break;

                    case "ref":
                        // asf links say they are http, but are mmsh instead
                        string uri_aux = xml_reader["HREF"] ?? xml_reader["href"];
                        if (uri_aux.StartsWith ("http", StringComparison.CurrentCultureIgnoreCase)) {
                            uri_aux = "mmsh" + uri_aux.Substring (4);
                        }

                        element.Uri = ResolveUri (uri_aux);
                        break;

                    case "duration":
                        try {
                            xml_reader.Read ();
                            element.Duration = TimeSpan.Parse (xml_reader.Value);
                        } catch {
                        }
                        break;
                }
            } while (!xml_reader.EOF && xml_reader.Depth > 1);
        }

        public override void Save(Stream stream, ITrackModelSource source)
        {
            throw new NotImplementedException();
        }
    }
}
