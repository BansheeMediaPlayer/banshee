/***************************************************************************
 *  AboutBox.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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
using Gtk;
using Mono.Unix;

using Banshee.Base;

namespace Banshee
{
    public class BansheeAboutDialog : AboutDialog
    {
        private static string [] authors = {
            "Aaron Bockover",
            "Dan Winship",
            "Hans Petter Jansson",
            "James Willcox",
            "Chris Lahey",
            "Ben Maurer",
            "Larry Ewing",
            "Miguel de Icaza",
            "Aydemir Ula\u015f \u015eahin",
            "Do\u011facan G\u00fcney",
            "Chris Toshok",
            "Jeff Tickle",
            "Ruben Vermeersch",
            "Fredrik Hedberg",
            "Oscar Forero",
            "Gabriel Burt",
            "Sebastian Dr\u00f6ge"
        };
        
        private static string [] artists = {
            "Garrett LeSage",
            "Jakub Steiner",
            "Ryan Collier"
        };
        
        private static Translator [] translators = {
            new Translator("Jordi Mas", "Catalan"),
            new Translator("Alexander Shopov, Rostislav Raykov", "Bulgarian"),
            new Translator("Adam Weinberger", "Canadian English"),
            new Translator("Francisco Javier F. Serrador", "Spanish"),
            new Translator("Takeshi AIHANA", "Japanese"),
            new Translator("\u017Dygimantas Beru\u010Dka", "Lithuanian"),
            new Translator("Funda Wang", "Simplified Chinese"),
            new Translator("Vincent van Adrighem", "Dutch"),
            new Translator("\u00D8ivind Hoel", "Norwegian Bokm\u00E5l"),
            new Translator("Marco Carvalho", "Brazilian Portuguese"),
			new Translator("Christian Rose", "Swedish"),
			new Translator("Lasse Bang Mikkelsen", "Danish"),
			new Translator("Stephane Raimbault", "French"),
			new Translator("Lukas Novotny", "Czech"),
			new Translator("Theppitak Karoonboonyanan", "Thai"),
			new Translator("Alessandro Gervaso", "Italian"),
			new Translator("Ilkka Tuohela", "Finnish"),
            new Translator("Christopher Orr", "British English")
        };
    
        private class Translator : IComparable
        {
            public Translator(string name, string language)
            {
                Name = name;
                Language = language;
            }
            
            public string Name;
            public string Language;
        
            public override string ToString()
            {
                return String.Format("{0} ({1})", Name, Language);
            }
            
            public int CompareTo(object o)
            {
                if(!(o is Translator)) {
                    throw new ArgumentException("Object must be Translator");
                }    
                
                return -(o as Translator).Name.CompareTo(Name);
            }
            
            public static string ToString(Translator [] translators)
            {
                string str = String.Empty;
                Array.Sort(translators);
                
                for(int i = 0; i < translators.Length; i++) {
                    str += translators[i].ToString() + "\n";
                }
                
                return str;
            }
        }
    
        public BansheeAboutDialog() : base()
        {
            Array.Sort(authors);
            Array.Sort(artists);
            
            IconThemeUtils.SetWindowIcon(this);
            
            Logo = Branding.AboutBoxLogo;
            Name = Catalog.GetString(Branding.ApplicationName); 
            Version = ConfigureDefines.VERSION;
            Comments = Catalog.GetString("Music Management and Playback for Gnome.");
            Copyright = Catalog.GetString(
                "Copyright \u00a9 2005-2006 Novell, Inc.\n" + 
                "Copyright \u00a9 2005 Aaron Bockover"
            );
            
            Website = "http://banshee-project.org/";
            WebsiteLabel = Catalog.GetString("Banshee Wiki");
            
            Authors = authors;
            TranslatorCredits = Translator.ToString(translators);
            Artists = artists;
            
            License = Resource.GetFileContents("COPYING");
            WrapLicense = true;
            
            SetUrlHook(delegate(AboutDialog dialog, string link) {
                Gnome.Url.Show(link);
            });
        }
    }
}
