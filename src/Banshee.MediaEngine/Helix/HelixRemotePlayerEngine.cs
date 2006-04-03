/***************************************************************************
 *  HelixRemotePlayerEngine.cs
 *
 *  Copyright (C) 2006 Novell, Inc
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
using System.Collections;

using Helix;

using Banshee.Base;
using Banshee.MediaEngine;

namespace Banshee.MediaEngine.Helix
{    
    public class HelixRemotePlayerEngine : PlayerEngine
    {
        private RemotePlayer player;
        private uint timeout_id;
        private uint ping_id;
        
        public HelixRemotePlayerEngine()
        {
            player = RemotePlayer.Connect();
            player.Stop();
            player.Message += OnRemotePlayerMessage;
            
            ping_id = GLib.Timeout.Add(5000, delegate {
                if(player == null) {
                    return false;
                }
                
                player.Ping();
                return true;
            });
        }
        
        public override void Dispose()
        {
            base.Dispose();
            player.Dispose();
            player = null;
            GLib.Source.Remove(ping_id);
        }
                
        protected override void OpenUri(Uri uri)
        {
            if(!player.OpenUri(uri.AbsoluteUri)) {
                throw new ApplicationException("Cannot open URI");
            }

            timeout_id = GLib.Timeout.Add(500, delegate {
                if(CurrentState == PlayerEngineState.Playing) {
                    OnEventChanged(PlayerEngineEvent.Iterate);
                }
                return true;
            });
        }
        
        public override void Close()
        {
            if(timeout_id > 0) {
                GLib.Source.Remove(timeout_id);
            }
            
            player.Stop();
            base.Close();
        }
        
        public override void Play()
        {
            player.Play();
        }
        
        public override void Pause()
        {
            player.Pause();
        }
        
        public void OnRemotePlayerMessage(object o, MessageArgs args)
        {
            Message message = args.Message;
            
            switch(message.Type) {
                case MessageType.ContentConcluded:
                    Close();
                    OnEventChanged(PlayerEngineEvent.EndOfStream);
                    break;
                case MessageType.ContentState:
                    switch((ContentState)message["NewState"]) {
                        case ContentState.Paused:
                            OnStateChanged(PlayerEngineState.Paused);
                            break;
                        case ContentState.Playing:
                            OnStateChanged(PlayerEngineState.Playing);
                            break;
                        case ContentState.Loading:
                        case ContentState.Contacting:
                            OnEventChanged(PlayerEngineEvent.Buffering);
                            break;
                        default:
                            OnStateChanged(PlayerEngineState.Idle);
                            break;
                    }
                    break;
            }
        }
         
        public override ushort Volume {
            get { return (ushort)player.GetVolume(); }
            set {
                player.SetVolume((uint)value);
                OnEventChanged(PlayerEngineEvent.Volume);
            }
        }
    
        public override uint Position {
            get { return (uint)player.GetPosition() / 1000; }
            set {
                if(player.StartSeeking()) {
                    if(player.SetPosition(value * 1000)) {
                        OnEventChanged(PlayerEngineEvent.Seek);
                    }
                    
                    player.StopSeeking();
                }
            }
        }
        
        public override uint Length { 
            get { return (uint)player.GetLength() / 1000; }
        }        
        
        public override string Id {
            get { return "helix-remote"; }
        }
        
        public override string Name {
            get { return "Helix Remote"; }
        }
        
        private static string [] source_capabilities = { "file" };
        public override IEnumerable SourceCapabilities {
            get { return source_capabilities; }
        }
        
        private static string [] decoder_capabilities = { "m4a", "mp3" };
        public override IEnumerable ExplicitDecoderCapabilities {
            get { return decoder_capabilities; }
        }
    }
}
