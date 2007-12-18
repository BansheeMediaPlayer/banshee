/***************************************************************************
 *  StationType.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Gabriel Burt <gabriel.burt@gmail.com>
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
using System.Collections.Generic;
using Mono.Gettext;

namespace Banshee.Plugins.LastFM
{
    public class StationType
    {
        public string Name, Label;
        public string ArgLabel;
        public bool SubscribersOnly;
        public string IconName;
        private string station_format;

        private StationType (string name, string label, string argLabel, string stationFormat, string iconName, bool subscribersOnly)
        {
            Name = name;
            Label = label;
            ArgLabel = argLabel;
            station_format = stationFormat;
            IconName = iconName;
            SubscribersOnly = subscribersOnly;
        }

        public string GetStationFor (string arg)
        {
            return "lastfm://" + String.Format (station_format, arg);
        }

        public override string ToString ()
        {
            return Name;
        }

        public static StationType FindByName (string name)
        {
            foreach (StationType type in Types) {
                if (type.Name == name)
                    return type;
            }
            return null;
        }

        public static StationType FindByLabel (string label)
        {
            foreach (StationType type in Types) {
                if (type.Label == label)
                    return type;
            }
            return null;
        }

        public static List<StationType> Types = new List<StationType> ();
        static StationType () {
            Types.Add (new StationType (
                "Recommended",
                Catalog.GetString ("Recommended"),
                Catalog.GetString ("For User:"),
                "user/{0}/recommended/100",
                "recommended",
                false
            ));

            Types.Add (new StationType (
                "Personal",
                Catalog.GetString ("Personal"),
                Catalog.GetString ("For User:"),
                "user/{0}/personal",
                "system-users",
                true
            ));

            Types.Add (new StationType (
                "Loved",
                Catalog.GetString ("Loved"),
                Catalog.GetString ("By User:"),
                "user/{0}/loved",
                "emblem-favorite",
                true
            ));

            Types.Add (new StationType (
                "Neighbor",
                Catalog.GetString ("Neighbors"),
                Catalog.GetString ("Of User:"),
                "user/{0}/neighbours",
                "system-users",
                false
            ));

            Types.Add (new StationType (
                "Group",
                Catalog.GetString ("Group"),
                Catalog.GetString ("Group Name:"),
                "group/{0}",
                "stock_people",
                false
            ));

            Types.Add (new StationType (
                "Tag",
                Catalog.GetString ("Tag"),
                Catalog.GetString ("Tag Name:"),
                "globaltags/{0}",
                null,
                false
            ));

            Types.Add (new StationType (
                "Fan",
                Catalog.GetString ("Fan"),
                Catalog.GetString ("Fans of:"),
                "artist/{0}/fans",
                "stock_people",
                false
            ));

            Types.Add (new StationType (
                "Similar",
                Catalog.GetString ("Similar"),
                Catalog.GetString ("Similar to:"),
                "artist/{0}/similarartists",
                null,
                false
            ));
        }
    }
}
