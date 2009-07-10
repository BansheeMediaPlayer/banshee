// 
// PodcastFeedPropertiesDialog.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2007-09 Michael C. Urbanski
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
using Pango;

using Banshee.Base;
using Banshee.Paas.Data;

namespace Banshee.Paas.Gui
{
    internal class ChannelPropertiesDialog : Dialog
    {
        private PaasChannel channel;
        private DownloadPreferenceComboBox download_preference_combo; 

        public ChannelPropertiesDialog (PaasChannel channel)
        {
            this.channel = channel;

            Title = channel.Name;
            //IconThemeUtils.SetWindowIcon (this);

            BuildWindow ();
        }

        private void BuildWindow()
        {
            BorderWidth = 6;
            VBox.Spacing = 12;
            HasSeparator = false;

            HBox box = new HBox();
            box.BorderWidth = 6;
            box.Spacing = 12;

            Button save_button = new Button("gtk-save");
            save_button.CanDefault = true;
            save_button.Show();

            // For later additions to the dialog.  (I.E. Feed art)
            HBox content_box = new HBox();
            content_box.Spacing = 12;

            Table table = new Table (2, 4, false);
            table.RowSpacing = 6;
            table.ColumnSpacing = 12;

            Label description_label = new Label (Catalog.GetString ("Description:"));
            description_label.SetAlignment (0f, 0f);
            description_label.Justify = Justification.Left;

            Label last_updated_label = new Label (Catalog.GetString ("Last updated:"));
            last_updated_label.SetAlignment (0f, 0f);
            last_updated_label.Justify = Justification.Left;

            Label name_label = new Label (Catalog.GetString ("Name:"));
            name_label.SetAlignment (0f, 0f);
            name_label.Justify = Justification.Left;

            Label name_label_ = new Label ();
            name_label_.SetAlignment (0f, 0f);            
            name_label_.Text = channel.Name;

            Label channel_url_label = new Label (Catalog.GetString ("URL:"));
            channel_url_label.SetAlignment (0f, 0f);
            channel_url_label.Justify = Justification.Left;

            Label new_episode_option_label = new Label (Catalog.GetString ("When channel is updated:"));
            new_episode_option_label.SetAlignment (0f, 0.5f);
            new_episode_option_label.Justify = Justification.Left;

            Label last_updated_text = new Label (channel.LastDownloadTime.ToString ("f"));
            last_updated_text.Justify = Justification.Left;
            last_updated_text.SetAlignment (0f, 0f);

            Label channel_url_text = new Label (channel.Url.ToString ());
            channel_url_text.Wrap = false;
            channel_url_text.Selectable = true;
            channel_url_text.SetAlignment (0f, 0f);
            channel_url_text.Justify = Justification.Left;
            channel_url_text.Ellipsize = Pango.EllipsizeMode.End;

            string description_string = String.IsNullOrEmpty (channel.Description) ?
                                        Catalog.GetString ("No description available") :
                                        channel.Description;

            Label descrition_text = new Label (description_string);
            descrition_text.Justify = Justification.Left;
            descrition_text.SetAlignment (0f, 0f);
            descrition_text.Wrap = true;
            descrition_text.Selectable = true;

            Viewport description_viewport = new Viewport();
            description_viewport.SetSizeRequest(-1, 150);
            description_viewport.ShadowType = ShadowType.None;

            ScrolledWindow description_scroller = new ScrolledWindow ();
            description_scroller.HscrollbarPolicy = PolicyType.Never;
            description_scroller.VscrollbarPolicy = PolicyType.Automatic;

            description_viewport.Add (descrition_text);
            description_scroller.Add (description_viewport);

            download_preference_combo = new DownloadPreferenceComboBox (channel.DownloadPreference);

            // First column
            uint i = 0;
            table.Attach (
                name_label, 0, 1, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                channel_url_label, 0, 1, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                last_updated_label, 0, 1, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                new_episode_option_label, 0, 1, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                description_label, 0, 1, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            // Second column
            i = 0;
            table.Attach (
                name_label_, 1, 2, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                channel_url_text, 1, 2, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                last_updated_text, 1, 2, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (
                download_preference_combo, 1, 2, i, ++i,
                AttachOptions.Fill, AttachOptions.Fill, 0, 0
            );

            table.Attach (description_scroller, 1, 2, i, ++i,
                AttachOptions.Expand | AttachOptions.Fill,
                AttachOptions.Expand | AttachOptions.Fill, 0, 0
            );

            content_box.PackStart (table, true, true, 0);
            box.PackStart (content_box, true, true, 0);

            Button cancel_button = new Button("gtk-cancel");
            cancel_button.CanDefault = true;
            cancel_button.Show();

            AddActionWidget (cancel_button, ResponseType.Cancel);
            AddActionWidget (save_button, ResponseType.Ok);

            DefaultResponse = Gtk.ResponseType.Cancel;
            ActionArea.Layout = Gtk.ButtonBoxStyle.End;

            box.ShowAll ();
            VBox.Add (box);

            Response += OnResponse;
        }

        private void OnResponse (object sender, ResponseArgs args)
        {
            Destroy ();

            if (args.ResponseId == Gtk.ResponseType.Ok) {
                DownloadPreference new_sync_pref = download_preference_combo.ActiveDownloadPreference;

                if (channel.DownloadPreference != new_sync_pref) {
                    channel.DownloadPreference = new_sync_pref;
                    channel.Save ();
                }
            }

            (sender as Dialog).Response -= OnResponse;
            (sender as Dialog).Destroy();
        }
    }
}
