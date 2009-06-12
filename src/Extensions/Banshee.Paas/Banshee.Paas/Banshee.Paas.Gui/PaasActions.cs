// 
// PaasActions.cs
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

using Gtk;

using Banshee.Gui;
using Banshee.Sources;
using Banshee.ServiceStack;

namespace Banshee.Paas.Gui
{
    public class PaasActions : BansheeActionGroup
    {
        private uint actions_id;
        private PaasService service;
        //private DatabaseSource last_source;
        
        public PaasActions (PaasService service) : base (ServiceManager.Get<InterfaceActionService> (), "Paas")
        {
            this.service = service;
            
            AddImportant (
                new ActionEntry (
                    "PaasUpdateAction", Stock.Refresh,
                     Catalog.GetString ("Update"), null,//"<control><shift>U",
                     Catalog.GetString ("Recieve updates from Miro Guide"), OnPaasUpdateHandler
                )
               
            );

            this["PaasUpdateAction"].ShortLabel = Catalog.GetString ("Update Channels");
            
            actions_id = Actions.UIManager.AddUiFromResource ("GlobalUI.xml");
            Actions.AddActionGroup (this);

            OnSelectionChanged (null, null);
        }
        
        public override void Dispose ()
        {
            Actions.UIManager.RemoveUi (actions_id);
            Actions.RemoveActionGroup (this);
            base.Dispose ();
        }

        private void OnSelectionChanged (object o, EventArgs args)
        {
            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                /*
                    UpdateFeedActions ();
                    UpdateItemActions ();
                */
            });
        }

        private void OnPaasUpdateHandler (object o, EventArgs args)
        {
            service.UpdateAsync ();
        }
    }
}
