using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CrusaderTrailManager {
    // Only touches validated four-byte fields in this process; never modifies the DLL on disk.
    public sealed class ScopedIntPatch : IDisposable {
        public sealed class Entry {
            public IntPtr Address;
            public int Original;
            public int Replacement;
        }
        readonly List<Entry> applied = new List<Entry>();
        public ScopedIntPatch(IEnumerable<Entry> changes) {
            var entries = new List<Entry>(changes);
            var seen = new HashSet<IntPtr>();
            foreach(var e in entries) {
                if(e.Address == IntPtr.Zero || !seen.Add(e.Address) || Marshal.ReadInt32(e.Address) != e.Original)
                    throw new InvalidOperationException("Mission table validation failed. No lord replacement was applied.");
            }
            try {
                foreach(var e in entries) {
                    applied.Add(e);
                    Write(e.Address,e.Replacement);
                }
            } catch { Dispose(); throw; }
        }
        public void Dispose() {
            Exception failure = null;
            for(int i=applied.Count-1;i>=0;i--) {
                try { Write(applied[i].Address,applied[i].Original); applied.RemoveAt(i); }
                catch(Exception ex) { failure=ex; }
            }
            if(failure != null) throw new InvalidOperationException("Could not restore the original mission table. Restart the game before continuing.",failure);
        }
        static void Write(IntPtr address,int value) {
            uint previous;
            if(!VirtualProtect(address,new UIntPtr(4),0x04,out previous)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { Marshal.WriteInt32(address,value); }
            finally {
                uint ignored;
                if(!VirtualProtect(address,new UIntPtr(4),previous,out ignored)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool VirtualProtect(IntPtr address,UIntPtr length,uint protection,out uint oldProtection);
    }
}
