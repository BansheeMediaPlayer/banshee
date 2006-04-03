/***************************************************************************
 *  PlayerEngineCore.cs
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
using System.Collections;
using System.Reflection;
using Mono.Unix;

using Banshee.MediaEngine;

namespace Banshee.Base
{
    public static class PlayerEngineCore
    {
        private const string RootEngineDir = ConfigureDefines.InstallDir + "Banshee.MediaEngine/";   
        
        private static ArrayList engines = new ArrayList();
        private static PlayerEngine active_engine;
        private static PlayerEngine default_engine;
        private static PlayerEngine pending_engine;

        public static event PlayerEngineEventHandler EventChanged;
        public static event PlayerEngineStateHandler StateChanged;

        private static void InstantiateEngines()
        {
            DirectoryInfo info = new DirectoryInfo(RootEngineDir);
            
            if(!info.Exists) {
                throw new IOException("Directory " + RootEngineDir + " does not exist");
            }
            
            string preferred_id = null;
            try {
                preferred_id = (string)Globals.Configuration.Get(GConfKeys.PlayerEngine);
            } catch {
            }
            
            foreach(DirectoryInfo sub_info in info.GetDirectories()) {
                DirectoryInfo directory_info = new DirectoryInfo(sub_info.FullName + "/");
                if(!directory_info.Exists) {
                    continue;
                }
                
                foreach(FileInfo file_info in directory_info.GetFiles("*.dll")) {
                    Assembly assembly = Assembly.LoadFrom(file_info.FullName);
                    foreach(Type type in assembly.GetTypes()) {
                        if(!type.IsSubclassOf(typeof(PlayerEngine))) {
                            continue;
                        }

                        try {
                            PlayerEngine engine = (PlayerEngine)Activator.CreateInstance(type);
                            engine.StateChanged += OnEngineStateChanged;
                            engine.EventChanged += OnEngineEventChanged;

                            if(engine.Id == preferred_id) {
                                DefaultEngine = engine;
                            } else {
                                engines.Add(engine);
                            }
                        } catch(Exception e) {
                            LogCore.Instance.PushError("Could not load a PlayerEngine", e.ToString());
                        }
                    }
                }
            }
            
            if(default_engine == null && engines.Count > 0) {
                default_engine = engines[0] as PlayerEngine;
            }
            
            active_engine = default_engine;
            
            LogCore.Instance.PushDebug(Catalog.GetString("Default player engine"), active_engine.Name);
        }
        
        public static void Initialize()
        {
            try {
                InstantiateEngines();
                if(default_engine == null || active_engine == null || engines == null || engines.Count == 0) {
                    throw new ApplicationException("No player engines found");
                }
            } catch(Exception e) {
                Console.Error.WriteLine("Cannot load any PlayerEngine:\n\n{0}\n", e);
                System.Environment.Exit(1);
            }   
        }

        public static void Dispose()
        {
            foreach(PlayerEngine engine in engines) {
                engine.Dispose();
            }
        }

        private static void OnEngineStateChanged(object o, PlayerEngineStateArgs args)
        {
            if(o != active_engine) {
                return;
            }
            
            PlayerEngineStateHandler handler = StateChanged;
            if(handler != null) {
                handler(o, args);
            }
        }

        private static void OnEngineEventChanged(object o, PlayerEngineEventArgs args)
        {
            if(o != active_engine) {
                return;
            }
            
            PlayerEngineEventHandler handler = EventChanged;
            if(handler != null) {
                handler(o, args);
            }
        }
        
        public static void Open(TrackInfo track)
        {
            if(!track.CanPlay) {
                return;
            }
               
            OpenCheck(track);
        }
        
        public static void Open(Uri uri)
        {
            OpenCheck(uri);
        }
        
        public static void OpenPlay(TrackInfo track)
        {
            if(!track.CanPlay) {
                return;
            }
        
            try {
                OpenCheck(track);
                active_engine.Play();
            } catch(Exception e) {
                LogCore.Instance.PushError(Catalog.GetString("Problem with Player Engine"), e.Message);
                Close();
                ActiveEngine = default_engine;
            }
        }
        
        private static void OpenCheck(object o)
        {
            Uri uri = null;
            TrackInfo track = null;
        
            if(o is Uri) {
                uri = o as Uri;
            } else if(o is TrackInfo) {
                track = o as TrackInfo;
                uri = track.Uri;
            } else {
                return;
            }
            
            FindSupportingEngine(uri);
            CheckPending();
            
            if(track != null) {
                active_engine.Open(track);
            } else if(uri != null) {
                active_engine.Open(uri);
            }
        }
        
        private static void FindSupportingEngine(Uri uri)
        {
            foreach(PlayerEngine engine in engines) {
                foreach(string extension in engine.ExplicitDecoderCapabilities) {
                    if(!uri.AbsoluteUri.EndsWith(extension)) {
                        continue;
                    } else if(active_engine != engine) {
                        Close();
                        pending_engine = engine;
                        Console.WriteLine("Switching engine to: " + engine.GetType());
                    }
                    return;
                }
            }
        
            foreach(PlayerEngine engine in engines) {
                foreach(string scheme in engine.SourceCapabilities) {
                    bool supported = scheme == uri.Scheme;
                    if(supported && active_engine != engine) {
                        Close();
                        pending_engine = engine;
                        Console.WriteLine("Switching engine to: " + engine.GetType());
                        return;
                    } else if(supported) {
                        return;
                    }
                }
            }
        }
        
        public static void Close()
        {
            active_engine.Reset();
            active_engine.Close();
        }
        
        public static void Play()
        {
            active_engine.Play();
        }
        
        public static void Pause()
        {
            active_engine.Pause();
        }

        private static void CheckPending()
        {
            if(pending_engine != null && pending_engine != active_engine) {
                if(active_engine.CurrentState == PlayerEngineState.Idle) {
                    Close();
                }
                
                active_engine = pending_engine;
                pending_engine = null;
            } 
        }
    
        public static TrackInfo CurrentTrack {
            get { return active_engine.CurrentTrack; }
        }
        
        public static Uri CurrentUri {
            get { return active_engine.CurrentUri; }
        }
        
        public static PlayerEngineState CurrentState {
            get { return active_engine.CurrentState; }
        }
        
        public static PlayerEngineState LastState {
            get { return active_engine.LastState; }
        }
        
        public static ushort Volume {
            get { return active_engine.Volume; }
            set { active_engine.Volume = value; }
        }
        
        public static uint Position {
            get { return active_engine.Position; }
            set { active_engine.Position = value; }
        }
        
        public static bool CanSeek {
            get { return active_engine.CanSeek; }
        }
        
        public static uint Length {
            get { 
                uint length = active_engine.Length;
                if(length > 0) {
                    return length;
                } else if(active_engine.CurrentTrack == null) {
                    return 0;
                }
                
                return (uint)active_engine.CurrentTrack.Duration.TotalSeconds;
            }
        }
    
        public static PlayerEngine ActiveEngine {
            get { return active_engine; }
            set { pending_engine = value; }
        }
        
        public static PlayerEngine DefaultEngine {
            get { return default_engine; }
            set { 
                if(engines.Contains(value)) {
                    engines.Remove(value);
                }
                
                engines.Insert(0, value);
            
                default_engine = value;
                Globals.Configuration.Set(GConfKeys.PlayerEngine, value.Id);
            }
        }
        
        public static IEnumerable Engines {
            get { return engines; }
        }
    }
}
