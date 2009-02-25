//
// RecommendationActions.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.Collections.Generic;
using Gtk;

using Mono.Unix;

using Lastfm;
using Lastfm.Gui;
using SortType = Hyena.Data.SortType;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Widgets;
using Banshee.MediaEngine;
using Banshee.Database;
using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.Gui;
using Banshee.Collection;
using Banshee.PlaybackController;

using Browser = Banshee.Web.Browser;

namespace Banshee.Lastfm.Recommendations
{
    public class RecommendationActions : BansheeActionGroup
    {
        private uint actions_id;
        private RecommendationService service;

        public RecommendationActions (RecommendationService service) : base (ServiceManager.Get<InterfaceActionService> (), "LastfmRecommendations")
        {
            this.service = service;

            Add (new ToggleActionEntry [] {
                new ToggleActionEntry (
                    "ShowRecommendationAction", null,
                    Catalog.GetString("Show Recommendations"), "<control>R",
                    Catalog.GetString("Show Recommendations"), OnToggleShow,
                    RecommendationService.ShowSchema.Get ()
                )
            });

            actions_id = Actions.UIManager.AddUiFromResource ("RecommendationMenu.xml");
            Actions.AddActionGroup (this);
        }

        public override void Dispose ()
        {
            Actions.UIManager.RemoveUi (actions_id);
            Actions.RemoveActionGroup (this);
            base.Dispose ();
        }

#region Action Handlers 

        private void OnToggleShow(object o, EventArgs args)
        {
            service.RecommendationsShown = (o as ToggleAction).Active;
        }

#endregion

    }
}
