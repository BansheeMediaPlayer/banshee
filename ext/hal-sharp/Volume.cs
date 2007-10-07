using System;
using System.Collections;
using System.Collections.Generic;

using NDesk.DBus;

namespace Hal
{
    [Interface("org.freedesktop.Hal.Device.Volume")]
    internal interface IVolume
    {
        void Mount(string [] args);
        void Unmount(string [] args);
        void Eject(string [] args);
    }
    
    public class Volume : Device
    {
        public Volume(string udi) : base(udi)
        {
        }

        public void Mount()
        {
            Mount(new string [] { String.Empty });
        }
        
        public void Mount(params string [] args)
        {
            CastDevice<IVolume>().Mount(args);
        }
        
        public void Unmount()
        {
            Unmount(new string [] { String.Empty });
        }
        
        public void Unmount(params string [] args)
        {
            CastDevice<IVolume>().Unmount(args);
        }
        
        public void Eject()
        {
            Eject(new string [] { String.Empty });
        }
        
        public void Eject(params string [] args)
        {
            CastDevice<IVolume>().Eject(args);
        }
    }
}
