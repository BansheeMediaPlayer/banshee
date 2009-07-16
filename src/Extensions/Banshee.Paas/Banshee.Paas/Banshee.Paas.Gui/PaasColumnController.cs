// 
// PaasColumnController.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

using Mono.Unix;

using Hyena.Data;
using Hyena.Data.Gui;
using Banshee.Collection.Gui;

namespace Banshee.Paas.Gui
{  
    public class PaasColumnController : XmlColumnController
    {
        private static readonly string ColumnXml = String.Format (@"
                <column-controller>
                  <add-all-defaults/>
                  <column modify-default=""IndicatorColumn"">
                    <renderer type=""Banshee.Paas.Gui.ColumnCellPaasStatusIndicator"" />
                  </column>                  
                  <remove-default column=""TrackColumn"" />
                  <remove-default column=""DiscColumn"" />
                  <remove-default column=""ComposerColumn"" />
                  <remove-default column=""ArtistColumn"" />
                  <column modify-default=""AlbumColumn"">
                    <title>{0}</title>
                    <long-title>{0}</long-title>
                  </column>
                  <column>
                      <visible>false</visible>
                      <title>{4}</title>
                      <renderer type=""Hyena.Data.Gui.ColumnCellText"" property=""ExternalObject.Description"" />
                      <sort-key>Description</sort-key>
                  </column>
                  <column modify-default=""FileSizeColumn"">
                      <visible>true</visible>
                  </column>
                  <!--
                  <column>
                      <visible>false</visible>
                      <title>{2}</title>
                      <renderer type=""Banshee.Podcasting.Gui.ColumnCellYesNo"" property=""ExternalObject.IsNew"" />
                      <sort-key>IsNew</sort-key>
                  </column>
                  <column>
                      <visible>false</visible>
                      <title>{3}</title>
                      <renderer type=""Banshee.Podcasting.Gui.ColumnCellYesNo"" property=""ExternalObject.IsDownloaded"" />
                      <sort-key>IsDownloaded</sort-key>
                  </column>
                  -->
                  
                  <column>
                      <visible>true</visible>
                      <title>{1}</title>
                      <renderer type=""Banshee.Paas.Gui.ColumnCellPublished"" property=""ExternalObject.PubDate"" />
                      <sort-key>PubDate</sort-key>
                  </column>
                  <sort-column direction=""desc"">published_date</sort-column>                      
                </column-controller>
            ",
            Catalog.GetString ("Channel"), Catalog.GetString ("Published"), Catalog.GetString ("New"),
            Catalog.GetString ("Downloaded"), Catalog.GetString ("Description")
        );

        public PaasColumnController () : base (ColumnXml)
        {           
        }

        public void SetIndicatorColumnDataHelper (ColumnCellDataHelper dataHelper)
        {
            ColumnCellPaasStatusIndicator indicator = IndicatorColumn.GetCell (0) as ColumnCellPaasStatusIndicator;
            indicator.DataHelper = dataHelper;
        }
    }
}
