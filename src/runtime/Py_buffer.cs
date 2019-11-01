using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /* buffer interface */
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct Py_buffer {
        public IntPtr buf;
        public IntPtr obj;        /* owned reference */
        [MarshalAs(UnmanagedType.SysInt)]
        public IntPtr len;
        [MarshalAs(UnmanagedType.SysInt)]
        public IntPtr itemsize;  /* This is Py_ssize_t so it can be
                             pointed to by strides in simple case.*/
        [MarshalAs(UnmanagedType.Bool)]
        public bool _readonly;
        public int ndim;
        [MarshalAs(UnmanagedType.LPStr)]
        public string format;
        public IntPtr shape;
        public IntPtr strides;
        public IntPtr suboffsets;
        public IntPtr _internal;
    }
}
