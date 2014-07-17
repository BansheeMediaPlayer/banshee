/***************************************************************************
 *  AsyncCommandQueue_.cs
 *
 *  Copyright (C) 2007 Michael C. Urbanski
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
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
using System.Collections.Generic;

using Hyena;

namespace Migo.TaskCore
{
    delegate void ExecuteCommand (ICommand command);

    public class AsyncCommandQueue : IDisposable
    {
        private bool disposed;
        private bool executing;

        private Queue<ICommand> eventQueue;
        private RegisteredWaitHandle registeredHandle;
        private AutoResetEvent are = new AutoResetEvent (false);
        private ManualResetEvent executingHandle = new ManualResetEvent (true);

        private readonly ExecuteCommand execCommand;

        private readonly object userSync;
        private readonly object sync = new object ();

        public EventHandler<EventArgs> QueueProcessed;
        public EventHandler<EventArgs> QueueProcessing;

        private bool IsProcessed {
            get {
                bool ret = false;

                lock (sync) {
                    if (eventQueue.Count == 0) {
                        ret = true;
                    }
                }

                return ret;
            }
        }

        public virtual WaitHandle WaitHandle {
            get { return executingHandle; }
        }

        public AsyncCommandQueue () : this (null)
        {
        }

        public AsyncCommandQueue (object sync)
        {
            userSync = sync;

            if (userSync == null) {
                execCommand = delegate (ICommand command) {
                    command.Execute ();
                };
            } else {
                execCommand = delegate (ICommand command) {
                    lock (userSync) {
                        command.Execute ();
                    }
                };
            }

            eventQueue = new Queue<ICommand> ();

            registeredHandle = ThreadPool.RegisterWaitForSingleObject (
                are, ProcessEventQueue, null, -1, false
            );
        }

        public virtual void Dispose ()
        {
            if (SetDisposed ()) {

                executingHandle.WaitOne ();

                if (registeredHandle != null) {
                    registeredHandle.Unregister (null);
                    registeredHandle = null;
                }

                if (are != null) {
                    are.Close ();
                    are = null;
                }

                if (executingHandle != null) {
                    executingHandle.Close ();
                    executingHandle = null;
                }

                eventQueue = null;
            }
        }

        public virtual bool Register (ICommand command)
        {
            lock (sync) {
                if (disposed) {
                    return false;
                }

                return Register (command, true);
            }
        }

        protected virtual bool Register (ICommand command, bool pumpQueue)
        {
            if (command == null) {
                throw new ArgumentNullException ("command");
            }

            eventQueue.Enqueue (command);

            if (!executing && pumpQueue) {
                SetExecuting (true);
            }

            return true;
        }

        public virtual bool Register (IEnumerable<ICommand> commands)
        {
            if (commands == null) {
                throw new ArgumentNullException ("commands");
            }

            lock (sync) {
                if (disposed) {
                    return false;
                }

                foreach (ICommand c in commands) {
                    Register (c, false);
                }

                if (!executing) {
                    SetExecuting (true);
                }
            }

            return true;
        }

        protected virtual void SetExecuting (bool exec)
        {
            if (exec) {
                executing = true;
                are.Set ();
                executingHandle.Reset ();
            } else {
                executing = false;
                executingHandle.Set ();
            }
        }

        protected virtual bool SetDisposed ()
        {
            bool ret = false;

            lock (sync) {
                if (!disposed) {
                    ret = disposed = true;
                }
            }
            return ret;
        }

        protected virtual void ProcessEventQueue (object state, bool timedOut)
        {
            ICommand e;
            bool done = false;

            while (!done) {
                RaiseEvent (QueueProcessing);

                while (true) {
                    lock (sync) {
                        e = eventQueue.Dequeue ();
                    }

                    if (e != null) {
                        try {
                            execCommand (e);
                        } catch (Exception ex) {
                            Log.Error (ex);
                        }
                    }

                    if (IsProcessed) {
                        RaiseEvent (QueueProcessed);
                        done = true;
                    }

                    if (done) {
                        lock (sync) {
                            if (eventQueue.Count == 0) {
                                SetExecuting (false);
                            } else {
                                done = false;
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void RaiseEvent (EventHandler<EventArgs> eve)
        {
            EventHandler<EventArgs> handler = eve;

            if (handler != null) {
                try {
                    handler (this, new EventArgs ());
                } catch (Exception ex) {
                    Log.Error (ex);
                }
            }
        }
    }
}
