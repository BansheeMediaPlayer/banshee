//
// XmlConfigurationClient.cs
//
// Author:
//   Scott Peterson <lunchtimemama@gmail.com>
//
// Copyright (C) 2007-2008 Scott Peterson
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
using System.IO;
using System.Timers;
using System.Xml;
using System.Xml.Serialization;

using Banshee.Base;

namespace Banshee.Configuration
{
    public class XmlConfigurationClient : IConfigurationClient
    {
        private const string null_namespace = "null";
        private const string namespace_tag_name = "namespace";
        private const string value_tag_name = "value";
        private const string tag_identifier_attribute_name = "name";
        private const string skel_path = "/etc/skel/.config/banshee-1/config.xml";

        private static string file_path {
            get {
                return Path.Combine(Paths.ApplicationData, "config.xml");
            }
        }
        private static XmlDocument xml_document;

        private static System.Timers.Timer timer;
        private static object timer_mutex = new object();
        private static volatile bool delay_write;

        public XmlConfigurationClient()
        {
            timer = new System.Timers.Timer(100); // a 10th of a second
            timer.Elapsed += new ElapsedEventHandler(OnTimerElapsedEvent);
            timer.AutoReset = true;

            xml_document = new XmlDocument();
            bool make_new_xml = true;

            string load_path = null;
            if (File.Exists (file_path)) {
                load_path = file_path;
            } else if (File.Exists (skel_path)) {
                Hyena.Log.InformationFormat ("Restoring config.xml from {0}", skel_path);
                load_path = skel_path;
            }

            if (load_path != null && File.Exists (load_path)) {
                try {
                    xml_document.Load (load_path);
                    make_new_xml = false;
                } catch { // TODO try recovery?
                }
            }
            if(make_new_xml) {
                xml_document.LoadXml("<configuration />");
            }
        }

        public T Get<T>(SchemaEntry<T> entry)
        {
            return Get<T>(entry.Namespace, entry.Key, entry.DefaultValue);
        }

        public T Get<T>(SchemaEntry<T> entry, T fallback)
        {
            return Get<T>(entry.Namespace, entry.Key, fallback);
        }

        public T Get<T>(string key, T fallback)
        {
            return Get<T>(null, key, fallback);
        }

        public T Get<T>(string namespce, string key, T fallback)
        {
            lock(xml_document) {
                XmlNode namespace_node = GetNamespaceNode(namespce == null
                    ? new string [] {null_namespace}
                    : namespce.Split('.'), false);

                if(namespace_node == null) {
                    return fallback;
                }

                foreach(XmlNode node in namespace_node.ChildNodes) {
                    if(node.Attributes[tag_identifier_attribute_name].Value == key && node.Name == value_tag_name) {
                        XmlSerializer serializer = new XmlSerializer(typeof(T));
                        using (var reader = new StringReader(node.InnerXml) ) {
                            return (T) serializer.Deserialize(reader);
                        }
                    }
                }
                return fallback;
            }
        }

        public void Set<T>(SchemaEntry<T> entry, T value)
        {
            Set(entry.Namespace, entry.Key, value);
        }

        public void Set<T>(string key, T value)
        {
            Set(null, key, value);
        }

        public void Set<T>(string namespce, string key, T value)
        {
            lock(xml_document) {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                var fragment = xml_document.CreateDocumentFragment();
                using (var writer = new StringWriter()) {
                    serializer.Serialize(writer, value);
                    fragment.InnerXml = writer.ToString();
                }

                if(fragment.FirstChild is XmlDeclaration) {
                    fragment.RemoveChild(fragment.FirstChild); // This is only a problem with Microsoft's System.Xml
                }

                XmlNode namespace_node = GetNamespaceNode(namespce == null
                    ? new string [] {null_namespace}
                    : namespce.Split('.'), true);

                bool found = false;
                foreach(XmlNode node in namespace_node.ChildNodes) {
                    if(node.Attributes[tag_identifier_attribute_name].Value == key && node.Name == value_tag_name) {
                        node.InnerXml = fragment.InnerXml;
                        found = true;
                        break;
                    }
                }
                if(!found) {
                    XmlNode new_node = xml_document.CreateElement(value_tag_name);
                    XmlAttribute attribute = xml_document.CreateAttribute(tag_identifier_attribute_name);
                    attribute.Value = key;
                    new_node.Attributes.Append(attribute);
                    new_node.AppendChild(fragment);
                    namespace_node.AppendChild(new_node);
                }
                QueueWrite();
            }
        }

        private XmlNode GetNamespaceNode(string [] namespace_parts, bool create)
        {
            return GetNamespaceNode(xml_document.DocumentElement, namespace_parts, create);
        }

        private XmlNode GetNamespaceNode(XmlNode parent_node, string [] namespace_parts, bool create)
        {
            XmlNode node = parent_node.FirstChild ?? parent_node;

            do {
                if(node.Name == namespace_tag_name && node.Attributes[tag_identifier_attribute_name].Value == namespace_parts[0]) {
                    if(namespace_parts.Length > 1) {
                        string [] new_namespace_parts = new string[namespace_parts.Length - 1];
                        for(int i = 1; i < namespace_parts.Length; i++) {
                            new_namespace_parts[i - 1] = namespace_parts[i];
                        }
                        node = GetNamespaceNode(node, new_namespace_parts, create);
                    }
                    return node;
                } else {
                    node = node.NextSibling;
                }
            } while(node != null);

            if(create) {
                XmlNode appending_node = parent_node;
                foreach(string s in namespace_parts) {
                    XmlNode new_node = xml_document.CreateElement(namespace_tag_name);
                    XmlAttribute attribute = xml_document.CreateAttribute(tag_identifier_attribute_name);
                    attribute.Value = s;
                    new_node.Attributes.Append(attribute);
                    appending_node.AppendChild(new_node);
                    appending_node = new_node;
                }
                node = appending_node;
            }
            return node;
        }

        // Queue XML file writes to minimize disk access
        private static void QueueWrite()
        {
            lock(timer_mutex) {
                if(!timer.Enabled) {
                    timer.Start();
                } else {
                    delay_write = true;
                }
            }
        }

        private static void OnTimerElapsedEvent(object o, ElapsedEventArgs args)
        {
            lock(timer_mutex) {
                if(delay_write) {
                    delay_write = false;
                } else {
                    xml_document.Save(file_path);
                    timer.Stop();
                }
            }
        }
    }
}
