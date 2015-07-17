//
// CollectionIndexer.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using System.Xml;
using System.Threading;
using System.Collections.Generic;

using Hyena;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Library;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.Collection.Indexer
{
    public class CollectionIndexer : ICollectionIndexer, IService, IDBusExportable, IDisposable
    {
        private static int instance_count = 0;

        private CollectionIndexerService service;
        private List<CachedList<DatabaseTrackInfo>> model_caches = new List<CachedList<DatabaseTrackInfo>> ();
        private string [] export_fields;

        private event ActionHandler indexing_finished;
        event ActionHandler ICollectionIndexer.IndexingFinished {
            add { indexing_finished += value; }
            remove { indexing_finished -= value; }
        }

        private event SaveToXmlFinishedHandler save_to_xml_finished;
        event SaveToXmlFinishedHandler ICollectionIndexer.SaveToXmlFinished {
            add { save_to_xml_finished += value; }
            remove { save_to_xml_finished -= value; }
        }

        public event EventHandler IndexingFinished;

        internal CollectionIndexer (CollectionIndexerService service)
        {
            this.service = service;
        }

        public void Dispose ()
        {
            lock (this) {
                DisposeModels ();

                if (service != null) {
                    service.DisposeIndexer (this);
                }
            }
        }

        private void DisposeModels ()
        {
            foreach (CachedList<DatabaseTrackInfo> model in model_caches) {
                model.Dispose ();
            }

            model_caches.Clear ();
        }

        public void SetExportFields (string [] fields)
        {
            lock (this) {
                export_fields = fields;
            }
        }

        public void Index ()
        {
            lock (this) {
                DisposeModels ();

                foreach (Source source in ServiceManager.SourceManager.Sources) {
                    LibrarySource library = source as LibrarySource;
                    if (library != null && library.Indexable) {
                        model_caches.Add (CachedList<DatabaseTrackInfo>.CreateFromSourceModel (
                            (DatabaseTrackListModel)library.TrackModel));
                    }
                }
            }

            OnIndexingFinished ();
        }

        void ICollectionIndexer.Index ()
        {
            ThreadPool.QueueUserWorkItem (delegate { Index (); });
        }

        public void SaveToXml (string path)
        {
            lock (this) {
                uint timer_id = Hyena.Log.DebugTimerStart ();
                bool success = false;

                try {
                    XmlTextWriter writer = new XmlTextWriter (path, System.Text.Encoding.UTF8);
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 2;
                    writer.IndentChar = ' ';

                    writer.WriteStartDocument (true);

                    writer.WriteStartElement ("banshee-collection");
                    writer.WriteStartAttribute ("version");
                    writer.WriteString (TrackInfo.ExportVersion);
                    writer.WriteEndAttribute ();

                    for (int i = 0; i < model_caches.Count; i++) {
                        CachedList<DatabaseTrackInfo> model = model_caches[i];
                        if (model.Count <= 0) {
                            continue;
                        }

                        writer.WriteStartElement ("model");
                        for (int j = 0; j < model.Count; j++) {
                            writer.WriteStartElement ("item");

                            foreach (KeyValuePair<string, object> item in model[j].GenerateExportable (export_fields)) {
                                string type = "string";
                                if      (item.Value is Boolean) type = "bool";
                                else if (item.Value is Byte)    type = "byte";
                                else if (item.Value is SByte)   type = "sbyte";
                                else if (item.Value is Int16)   type = "short";
                                else if (item.Value is UInt16)  type = "ushort";
                                else if (item.Value is Int32)   type = "int";
                                else if (item.Value is UInt32)  type = "uint";
                                else if (item.Value is Int64)   type = "long";
                                else if (item.Value is UInt64)  type = "ulong";
                                else if (item.Value is Char)    type = "char";
                                else if (item.Value is Double)  type = "double";
                                else if (item.Value is Single)  type = "float";

                                writer.WriteStartElement (item.Key);
                                writer.WriteStartAttribute ("type");
                                writer.WriteString (type);
                                writer.WriteEndAttribute ();
                                writer.WriteString (item.Value.ToString ());
                                writer.WriteEndElement ();
                            }

                            writer.WriteEndElement ();
                        }

                        writer.WriteEndElement ();
                    }

                    writer.WriteEndElement ();
                    writer.WriteEndDocument ();
                    writer.Close ();

                    success = true;
                } catch (Exception e) {
                    Log.Error (e);
                }

                Hyena.Log.DebugTimerPrint (timer_id, "CollectionIndexer.SaveToXml: {0}");

                SaveToXmlFinishedHandler handler = save_to_xml_finished;
                if (handler != null) {
                    handler (success, path);
                }
            }
        }

        void ICollectionIndexer.SaveToXml (string path)
        {
            ThreadPool.QueueUserWorkItem (delegate { SaveToXml (path); });
        }

        public IDictionary<string, object> GetResult (int modelIndex, int itemIndex)
        {
            lock (this) {
                if (modelIndex < 0 || modelIndex >= model_caches.Count) {
                    throw new IndexOutOfRangeException ("modelIndex");
                }

                CachedList<DatabaseTrackInfo> model = model_caches[modelIndex];

                if (itemIndex < 0 || itemIndex >= model.Count) {
                    throw new IndexOutOfRangeException ("itemIndex");
                }

                return model[itemIndex].GenerateExportable (export_fields);
            }
        }

        public int GetModelCounts ()
        {
            lock (this) {
                return model_caches.Count;
            }
        }

        public int GetModelResultsCount (int modelIndex)
        {
            lock (this) {
                if (modelIndex < 0 || modelIndex >= model_caches.Count) {
                    return -1;
                }

                return model_caches[modelIndex].Count;
            }
        }

        protected virtual void OnIndexingFinished ()
        {
            EventHandler handler = IndexingFinished;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }

            ActionHandler dbus_handler = indexing_finished;
            if (dbus_handler != null) {
                dbus_handler ();
            }
        }

        private string service_name = String.Format ("CollectionIndexer_{0}", instance_count++);
        string IService.ServiceName {
            get { return service_name; }
        }

        IDBusExportable IDBusExportable.Parent {
            get { return service; }
        }
    }
}
