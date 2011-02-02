//
// ColumnCellUnheard.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using Gtk;
using Cairo;

using Mono.Unix;

using Hyena.Gui;
using Hyena.Gui.Theming;
using Hyena.Data.Gui;

using Migo.Syndication;

using Banshee.Gui;
using Banshee.ServiceStack;
using Banshee.Collection.Gui;

namespace Banshee.Podcasting.Gui
{
    public class ColumnCellUnheard : ColumnCellText
    {
        public ColumnCellUnheard () : base (null, true)
        {
        }

        protected override string GetText (object obj)
        {
            OldNewFilter val = (OldNewFilter) obj;
            switch (val) {
                case OldNewFilter.New:    return Catalog.GetString ("New Items");
                case OldNewFilter.Both:   return Catalog.GetString ("All Items");
            }
            return String.Empty;
        }
    }
}
