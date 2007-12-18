/***************************************************************************
 *  Utilities.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
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
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions; 
using System.Diagnostics;
using System.Globalization;
using Mono.Unix;
 
namespace Banshee.Base
{
    public static class UidGenerator
    {
        private static int uid = 0;
        
        public static int Next
        {
            get {
                return ++uid;
            }
        }
    }
    
    public static class Utilities
    {    
        [DllImport("libglib-2.0.so")]
        private static extern IntPtr g_get_real_name();

        public static string GetRealName()
        {
            try {
                string name = GLib.Marshaller.Utf8PtrToString(g_get_real_name());
                string [] parts = name.Split(' ');
                return parts[0].Replace(',', ' ').Trim();
            } catch(Exception) { 
                return null;
            }
        }
        
        public static string BytesToString(ulong bytes)
        {
            double mb = (double)bytes / 1048576.0;
            return mb > 1024.0
                ? String.Format(Catalog.GetString("{0:0.00} GB"), mb / 1024.0)
                : String.Format(Catalog.GetString("{0} MB"), Math.Round(mb));
        }
        
        public static bool UnmountVolume(string device)
        {
            try {
                if(ExecProcess("pumount", device) != 0) {
                    throw new ApplicationException("pumount returned error");
                }
                
                return true;
            } catch(Exception) {
                try {
                    return ExecProcess("umount", device) == 0;
                } catch(Exception) {
                }
            }
            
            return false;
        }
        
        public static int ExecProcess(string command, string args)
        {
            Process process = Process.Start(command, args == null ? "" : args);
            process.WaitForExit();
            return process.ExitCode;
        }
        
        public static Gdk.Color ColorBlend(Gdk.Color a, Gdk.Color b)
        {
            // at some point, might be nice to allow any blend?
            double blend = 0.5;

            if(blend < 0.0 || blend > 1.0) {
                throw new ApplicationException("blend < 0.0 || blend > 1.0");
            }
            
            double blendRatio = 1.0 - blend;

            int aR = a.Red >> 8;
            int aG = a.Green >> 8;
            int aB = a.Blue >> 8;

            int bR = b.Red >> 8;
            int bG = b.Green >> 8;
            int bB = b.Blue >> 8;

            double mR = aR + bR;
            double mG = aG + bG;
            double mB = aB + bB;

            double blR = mR * blendRatio;
            double blG = mG * blendRatio;
            double blB = mB * blendRatio;

            Gdk.Color color = new Gdk.Color((byte)blR, (byte)blG, (byte)blB);
            Gdk.Colormap.System.AllocColor(ref color, true, true);
            return color;
        }
        
        [DllImport("libc")] // Linux
        private static extern int prctl(int option, byte [] arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5);
        
        [DllImport("libc")] // BSD
        private static extern void setproctitle(byte [] fmt, byte [] str_arg);

        public static void SetProcessName(string name)
        {
            try {
                if(prctl(15 /* PR_SET_NAME */, Encoding.ASCII.GetBytes(name + "\0"), 
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0) {
                    throw new ApplicationException("Error setting process name: " + 
                        Mono.Unix.Native.Stdlib.GetLastError());
                }
            } catch(EntryPointNotFoundException) {
                setproctitle(Encoding.ASCII.GetBytes("%s\0"), 
                    Encoding.ASCII.GetBytes(name + "\0"));
            }
        }
    }
    
    public static class ReflectionUtil
    {
        public static bool IsVirtualMethodImplemented(Type type, string methodName)
        {
            return IsVirtualMethodImplemented(type, methodName, null);
        }
        
        public static bool IsVirtualMethodImplemented(Type type, string methodName, Type [] parameterTypes)
        {
            BindingFlags binding_flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo methodInfo = parameterTypes == null 
                ? type.GetMethod(methodName, binding_flags)
                : type.GetMethod (methodName, binding_flags, null, parameterTypes, null);  
            
            if(methodInfo == null) {
                return false;
            }
            
            return methodInfo.IsVirtual ? methodInfo != methodInfo.GetBaseDefinition() : true;
        }
        
        public static object InvokeMethod(Assembly assembly, string typeName, string methodName)
        {
            if(assembly == null) {
                throw new ArgumentNullException("assembly");
            }
            
            Type type = assembly.GetType(typeName, true);
            MethodInfo method = type.GetMethod(methodName);
            return method.Invoke(null, null);
        }
        
        public static Type [] ModuleGetTypes(Assembly assembly, string typeName)
        {
            return (Type [])InvokeMethod(assembly, typeName, "GetTypes");
        }
    }
    
    public class DateTimeUtil
    {
        public static readonly DateTime LocalUnixEpoch = new DateTime(1970, 1, 1).ToLocalTime();

        public static DateTime ToDateTime(long time)
        {
            return FromTimeT(time);
        }

        public static long FromDateTime(DateTime time)
        {
            return ToTimeT(time);
        }

        public static DateTime FromTimeT(long time)
        {
            return LocalUnixEpoch.AddSeconds(time);
        }

        public static long ToTimeT(DateTime time)
        {
            return (long)time.Subtract(LocalUnixEpoch).TotalSeconds;
        }

        public static string FormatDuration(long time) {
            return (time > 3600 ? 
                    String.Format("{0}:{1:00}:{2:00}", time / 3600, (time / 60) % 60, time % 60) :
                    String.Format("{0}:{1:00}", time / 60, time % 60));
        }
    }

    public class Timer : IDisposable
    {
        private DateTime start;
        private string label;
        
        public Timer(string label) 
        {
            this.label = label;
            start = DateTime.Now;
        }

        public TimeSpan ElapsedTime {
            get {
                return DateTime.Now - start;
            }
        }

        public void WriteElapsed(string message)
        {
            Console.WriteLine("{0} {1} {2}", label, message, ElapsedTime);
        }

        public void Dispose()
        {
            WriteElapsed("timer stopped:");
        }
    }
    
    public static class ThreadAssist
    {
        private static Thread main_thread;
        
        static ThreadAssist()
        {
            main_thread = Thread.CurrentThread;
        }
        
        public static bool InMainThread {
            get {
                return main_thread.Equals(Thread.CurrentThread);
            }
        }
        
        public static void ProxyToMain(EventHandler handler)
        {
            if(!InMainThread) {
                Gtk.Application.Invoke(handler);
            } else {
                handler(null, new EventArgs());
            }
        }
        
        public static Thread Spawn(ThreadStart threadedMethod, bool autoStart)
        {
            Thread thread = new Thread(threadedMethod);
            thread.IsBackground = true;
            if(autoStart) {
                thread.Start();
            }
            return thread;
        }
        
        public static Thread Spawn(ThreadStart threadedMethod)
        {
            return Spawn(threadedMethod, true);
        }
    }
    
    public static class PathUtil
    {
        public static string MakeFileNameKey(SafeUri uri)
        {
            string path = uri.LocalPath;
            return Path.GetDirectoryName(path) + 
                Path.DirectorySeparatorChar + 
                Path.GetFileNameWithoutExtension(path);
        }
        
        public static long GetDirectoryAvailableSpace(string path)
        {
            try {
                Mono.Unix.Native.Statvfs statvfs_info;
                if(Mono.Unix.Native.Syscall.statvfs(path, out statvfs_info) == 0) {
                    return (long)(statvfs_info.f_bavail * statvfs_info.f_bsize);
                }
                
                return -1;
            } catch {
                return -1;
            }   
        }
    }
    
    public class Resource
    {
        public static string GetFileContents(string name)
        {
            Assembly asm = Assembly.GetCallingAssembly();
            Stream stream = asm.GetManifestResourceStream(name);
            StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();    
        }
    }
    
    public static class NamingUtil
    {
        public delegate bool PostfixDuplicateIncrementHandler(string check);
    
        public static string GenerateTrackCollectionName(IEnumerable tracks, string fallback)
        {
            Dictionary<string, int> weight_map = new Dictionary<string, int>();
            
            if(tracks == null) {
                return fallback;
            }
            
            foreach(TrackInfo track in tracks) {
                string artist = null;
                string album = null;
                
                if(track.Artist != null) {
                    artist = track.Artist.Trim();
                    if(artist == String.Empty) {
                        artist = null;
                    }
                }
                
                if(track.Album != null) {
                    album = track.Album.Trim();
                    if(album == String.Empty) {
                        album = null;
                    }
                }
                
                if(artist != null && album != null) {
                    IncrementCandidate(weight_map, "\0" + artist + " - " + album);
                    IncrementCandidate(weight_map, artist);
                    IncrementCandidate(weight_map, album);
                } else if(artist != null) {
                    IncrementCandidate(weight_map, artist);
                } else if(album != null) {
                    IncrementCandidate(weight_map, album);
                }
            }
            
            int max_hit_count = 0;
            string max_candidate = fallback;
            
            List<string> sorted_keys = new List<string>(weight_map.Keys);
            sorted_keys.Sort();
            
            foreach(string candidate in sorted_keys) {
                int current_hit_count = weight_map[candidate];
                if(current_hit_count > max_hit_count) {
                    max_hit_count = current_hit_count;
                    max_candidate = candidate;
                }
            }
            
            if(max_candidate[0] == '\0') {
                return max_candidate.Substring(1);
            }
            
            return max_candidate;
        }
        
        private static void IncrementCandidate(Dictionary<string, int> map, string hit)
        {
            if(map.ContainsKey(hit)) {
                map[hit]++;
            } else {
                map.Add(hit, 1);
            }
        }
        
        public static string PostfixDuplicate(string prefix, PostfixDuplicateIncrementHandler duplicateHandler)
        {
            if(duplicateHandler == null) {
                throw new ArgumentNullException("A PostfixDuplicateIncrementHandler delegate must be given");
            }
            
            string name = prefix;
            for(int i = 1; true; i++) {
                if(!duplicateHandler(name)) {
                    return name;
                }
                
                name = prefix + " " + i;
            }
        }
    }
}
