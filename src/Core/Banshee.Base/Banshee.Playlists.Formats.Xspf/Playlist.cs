/***************************************************************************
 *  Playlist.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
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
 
using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Banshee.Playlists.Formats.Xspf
{
    public class Playlist : XspfBaseObject
    {
        // TODO: Add attribution, extension support
        
        private static string XspfNamespace = "http://xspf.org/ns/0/";
        
        private Uri default_base_uri;
        private Uri document_base_uri;
        
        private bool loaded = false;

        private Uri location;  
        private Uri identifier;
        private Uri license;
        private DateTime date;
        
        private XmlDocument doc;

        private List<Track> tracks = new List<Track>();
        
        public Playlist()
        {
        }
        
        private void Load(XmlDocument doc)
        {
            this.doc = doc;

            XmlNamespaceManager xmlns = new XmlNamespaceManager(doc.NameTable);
            xmlns.AddNamespace("xspf", XspfNamespace);
            
            XmlNode playlist_node = doc.SelectSingleNode("/xspf:playlist", xmlns);

            if(playlist_node == null) {
                // Hack to work with files that don't have a namespace on the <playlist> node, like Last.fm
                xmlns.AddNamespace("xspf", String.Empty);
                playlist_node = doc.SelectSingleNode("/xspf:playlist", xmlns);

                if(playlist_node == null) {
                    throw new ApplicationException("Not a valid XSPF playlist");
                }
            }

            XmlAttribute version_attr = playlist_node.Attributes["version"];
            if(version_attr == null || version_attr.Value == null) {
                throw new ApplicationException("XSPF playlist version must be specified");
            } else {
                try {
                    int version = Int32.Parse(version_attr.Value);
                    if(version < 0 || version > 1) {
                        throw new ApplicationException("Only XSPF versions 0 and 1 are supported");
                    }
                } catch(FormatException) {
                    throw new ApplicationException("Invalid XSPF Version '" + version_attr.Value + "'");
                }
            }
            
            XmlAttribute base_attr = playlist_node.Attributes["xml:base"];
            if(base_attr != null) {
                document_base_uri = new Uri(base_attr.Value);
            }
            
            LoadBase(playlist_node, xmlns);
            
            location = XmlUtil.ReadUri(playlist_node, xmlns, ResolvedBaseUri, "xspf:location");
            identifier = XmlUtil.ReadUri(playlist_node, xmlns, ResolvedBaseUri, "xspf:identifier");
            license = XmlUtil.ReadUri(playlist_node, xmlns, ResolvedBaseUri, "xspf:license");
            
            date = XmlUtil.ReadDate(playlist_node, xmlns, "xspf:date");

            foreach(XmlNode node in playlist_node.SelectNodes("xspf:trackList/xspf:track", xmlns)) {
                Track track = new Track();
                track.Load(this, node, xmlns);
                AddTrack(track);
            }
            
            loaded = true;
        }
        
        public void Load(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            Load(doc);
        }
        
        public void Load(XmlReader reader)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
            Load(doc);
        }
        
        public void Load(TextReader reader)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
            Load(doc);
        }
        
        public void Load(Stream stream)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(stream);
            Load(doc);
        }
        
        public void Save(string path)
        {
            Save(new XmlTextWriter(path, System.Text.Encoding.UTF8));
        }
        
        public void Save(Stream stream)
        {
            Save(new XmlTextWriter(stream, System.Text.Encoding.UTF8));
        }
        
        public void Save(XmlTextWriter writer)
        {
            // FIXME: This is very very limited write support
            
            writer.Indentation = 2;
            writer.IndentChar = ' ';
            writer.Formatting = System.Xml.Formatting.Indented;
            
            writer.WriteStartDocument();
            writer.WriteStartElement("playlist", XspfNamespace);
            writer.WriteAttributeString("version", "1");
            
            SaveBase(writer);
            
            writer.WriteStartElement("trackList");
            foreach(Track track in tracks) {
                writer.WriteStartElement("track");
                track.Save(writer);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            
            writer.WriteEndElement();
            writer.WriteEndDocument();
            
            writer.Flush();
            writer.Close();
        }
        
        public void AddTrack(Track track)
        {
            track.Parent = this;
            tracks.Add(track);
        }
        
        public void RemoveTrack(Track track)
        {
            track.Parent = null;
            tracks.Remove(track);
        }
        
        public Uri DefaultBaseUri {
            get { 
                if(default_base_uri == null) {
                    string path = Path.GetFullPath(Environment.CurrentDirectory);
                    if(path == null) {
                        path = System.Reflection.Assembly.GetEntryAssembly().Location;
                    }
                    path = Path.GetFullPath(path);
                    default_base_uri = new Uri(path);
                }
                
                return default_base_uri;
            }
            
            set { 
                if(loaded) {
                    throw new ApplicationException("Setting DefaultBaseUri must be done before Load()");
                }
                
                default_base_uri = value; 
            }
        }
        
        public Uri DocumentBaseUri {
            get { return document_base_uri; }
        }
        
        public override Uri ResolvedBaseUri {
            get { return DocumentBaseUri == null ? DefaultBaseUri : DocumentBaseUri; }
        }
        
        public Uri Location {
            get { return location; }
            set { location = value; }
        }
        
        public Uri Identifier {
            get { return identifier; }
            set { identifier = value; }
        }

        public Uri License {
            get { return license; }
            set { license = value; }
        }
        
        public DateTime Date {
            get { return date; }
            set { date = value; }
        }
        
        public ReadOnlyCollection<Track> Tracks {
            get { return new ReadOnlyCollection<Track>(tracks); }
        }
        
        public int TrackCount {
            get { return tracks.Count; }
        }
    }
}
