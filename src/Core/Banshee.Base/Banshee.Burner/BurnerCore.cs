/***************************************************************************
 *  BurnerCore.cs
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
using System.Collections;
using System.Collections.Generic;

using Mono.Unix;
using Gtk;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Cdrom;

namespace Banshee.Burner
{
    public static class BurnerCore
    {
        private static bool initialized = false;
        private static IDriveFactory drive_factory;
        private static IDiscDuplicator disc_duplicator;
        private static List<BurnerSource> burners = new List<BurnerSource>();
    
        public static void Initialize()
        {
            if(initialized) {
                return;
            }
            
            initialized = true;
            
            InstallActions();
            
            SourceManager.SourceAdded += delegate(SourceAddedArgs args) {
                if(args.Source is BurnerSource) {
                    burners.Add(args.Source as BurnerSource);
                }
            };
            
            SourceManager.SourceRemoved += delegate(SourceEventArgs args) {
                if(args.Source is BurnerSource) {
                    burners.Remove(args.Source as BurnerSource);
                }
            };
            
            drive_factory = new Banshee.Cdrom.Nautilus.NautilusDriveFactory();
            disc_duplicator = new Banshee.Cdrom.Nautilus.NautilusDiscDuplicator();
            
            foreach(IDrive drive in drive_factory) {
                if(!(drive is IRecorder)) {
                    continue;
                   }
                   
                if(drive.HaveMedia && FindSourceForDrive(drive, false) == null) {
                    CreateSource(drive as IRecorder);
                }
            }
            
            drive_factory.MediaAdded += delegate(object o, MediaArgs args) {
                if(!(args.Drive is IRecorder)) {
                    return;
                }
                
                BurnerSource source = FindSourceForDrive(args.Drive, true);

                if(source == null) {
                    if(burners.Count > 0) {
                        return;
                    }
                    
                    source = CreateSource(args.Drive as IRecorder);
                }    
             
                foreach(BurnerSource iter_source in burners) {
                    if(iter_source.Session.Recorder == null) {
                        iter_source.Session.Recorder = args.Drive as IRecorder;
                    }
                }
            };
            
            drive_factory.MediaRemoved += delegate(object o, MediaArgs args) {
                BurnerSource source = FindSourceForDrive(args.Drive, false);
                if(source != null && source.Count <= 0) {
                    source.Unmap();
                }
            };
        }
        
        private static void InstallActions()
        {
            Globals.ActionManager.GlobalActions.Add(new ActionEntry [] {
                new ActionEntry("NewCDAction", null,
                Catalog.GetString("New Audio C_D"), null,
                Catalog.GetString("Create a new audio CD"), OnNewCDAction)
            });
        }
        
        private static BurnerSource FindSourceForDrive(IDrive drive, bool allowEmpty)
        {
            if(!(drive is IRecorder)) {
                return null;
            }
        
            foreach(BurnerSource source in burners) {
                if(source.Session.Recorder == drive || (allowEmpty && 
                    (source.Session.Recorder == null || source.Count == 0))) {
                    return source;
                }
            }
            
            return null;
        }
        
        public static BurnerSource CreateOrFindEmptySource()
        {
            foreach(BurnerSource source in burners) {
                if(source.Count == 0) {
                    return source;
                }
            }
            
            return CreateSource();
        }
        
        public static BurnerSource CreateSource()
        {
            BurnerSource source = CreateSource(null);
            SourceManager.AddSource(source);
            return source;
        }
        
        private static BurnerSource CreateSource(IRecorder recorder)
        {
            if(drive_factory == null || drive_factory.RecorderCount <= 0) {
                LogCore.Instance.PushWarning(
                    Catalog.GetString("Problem creating CD"),
                    Catalog.GetString("No CD recording hardware was found."));
                return null;
            }
            
            if(recorder == null) {
                return new BurnerSource();
            }
            
            return new BurnerSource(recorder);
        }
        
        private static void OnNewCDAction(object o, EventArgs args)
        {
            BurnerSource source = FindSourceForDrive(null, true);
            if(source == null) {
                source = CreateSource(null);
            }
            
            if(source != null) {
                SourceManager.SetActiveSource(source);
            }
        }
        
        public static IDriveFactory DriveFactory {
            get { return drive_factory; }
        }
        
        public static IDiscDuplicator DiscDuplicator {
            get { return disc_duplicator; }
        }
    }
}
