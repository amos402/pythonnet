using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
#if PYTHON37
    public class PyBuffer
    {
        private IntPtr _handle;
        private Runtime.Py_buffer _view;
        private GCHandle _gchandle;

        internal PyBuffer(IntPtr handle, GCHandle gc_handle)
        {
            _handle = handle;
            _gchandle = gc_handle;
            _view = (Runtime.Py_buffer)Marshal.PtrToStructure(handle, typeof(Runtime.Py_buffer));
        }

        public PyObject Object
        {
            get
            {
                return new PyObject(_view.obj);
            }
        }

        public long Length
        {
            get
            {
                return _view.len;
            }
        }

        public long[] Shape
        {
            get
            {
                if (_view.shape == IntPtr.Zero)
                    return null;
                long[] shape = new long[_view.ndim];
                Marshal.Copy(_view.shape, shape, 0, (int)_view.len * sizeof(long));
                return shape;
            }
        }

        public long[] Strides
        {
            get
            {
                if (_view.strides == IntPtr.Zero)
                    return null;
                long[] strides = new long[_view.ndim];
                Marshal.Copy(_view.strides, strides, 0, (int)_view.len * sizeof(long));
                return strides;
            }
        }

        public long[] SubOffsets
        {
            get
            {
                if (_view.suboffsets == IntPtr.Zero)
                    return null;
                long[] suboffsets = new long[_view.ndim];
                Marshal.Copy(_view.suboffsets, suboffsets, 0, (int)_view.len * sizeof(long));
                return suboffsets;
            }
        }

        /// <summary>
        /// Release the buffer view and decrement the reference count for view->obj. This function MUST be called when the buffer is no longer being used, otherwise reference leaks may occur.
        /// It is an error to call this function on a buffer that was not obtained via <see cref="PyObject.GetBuffer()"/>.
        /// </summary>
        public void Release()
        {
            Runtime.PyBuffer_Release(_handle);
            _gchandle.Free();
        }

        /// <summary>
        /// Return the implied itemsize from format. On error, raise an exception and return -1.
        /// New in version 3.9.
        /// </summary>
        public static long SizeFromFormat(string format)
        {
            if (Runtime.pyversionnumber == 39)
                return Runtime.PyBuffer_SizeFromFormat(format);
            return -1;
        }

        /// <summary>
        /// Returns true if the memory defined by the view is C-style (order is 'C') or Fortran-style (order is 'F') contiguous or either one (order is 'A'). Returns false otherwise.
        /// </summary>
        /// <param name="order">C-style (order is 'C') or Fortran-style (order is 'F') contiguous or either one (order is 'A')</param>
        public bool IsContiguous(char order)
        {
            return Convert.ToBoolean(Runtime.PyBuffer_IsContiguous(_handle, order));
        }

        /// <summary>
        /// Get the memory area pointed to by the indices inside the given view. indices must point to an array of view->ndim indices.
        /// </summary>
        public IntPtr GetPointer(long[] indices)
        {
            return Runtime.PyBuffer_GetPointer(_handle, indices);
        }

        /// <summary>
        /// Copy contiguous len bytes from buf to view. fort can be 'C' or 'F' (for C-style or Fortran-style ordering). 0 is returned on success, -1 on error.
        /// </summary>
        /// <param name="fort">fort can be 'C' or 'F' (for C-style or Fortran-style ordering)</param>
        /// <returns>0 is returned on success, -1 on error.</returns>
        public int FromContiguous(IntPtr buf, long len, char fort)
        {
            return Runtime.PyBuffer_FromContiguous(_handle, buf, len, fort);
        }

        /// <summary>
        /// Copy len bytes from view to its contiguous representation in buf. order can be 'C' or 'F' or 'A' (for C-style or Fortran-style ordering or either one). 0 is returned on success, -1 on error.
        /// </summary>
        /// <param name="order">order can be 'C' or 'F' or 'A' (for C-style or Fortran-style ordering or either one).</param>
        /// <returns>0 is returned on success, -1 on error.</returns>
        public int ToContiguous(IntPtr buf, char order)
        {
            return Runtime.PyBuffer_ToContiguous(buf, _handle, Length, order);
        }

        /// <summary>
        /// Fill the strides array with byte-strides of a contiguous (C-style if order is 'C' or Fortran-style if order is 'F') array of the given shape with the given number of bytes per element.
        /// </summary>
        /// <param name="order">C-style if order is 'C' or Fortran-style if order is 'F'</param>
        public static void FillContiguousStrides(int ndims, IntPtr shape, IntPtr strides, int itemsize, char order)
        {
            Runtime.PyBuffer_FillContiguousStrides(ndims, shape, strides, itemsize, order);
        }

        /// <summary>
        /// FillInfo Method
        /// </summary>
        /// <remarks>
        /// Handle buffer requests for an exporter that wants to expose buf of size len with writability set according to readonly. buf is interpreted as a sequence of unsigned bytes.
        /// The flags argument indicates the request type. This function always fills in view as specified by flags, unless buf has been designated as read-only and PyBUF_WRITABLE is set in flags.
        /// On success, set view->obj to a new reference to exporter and return 0. Otherwise, raise PyExc_BufferError, set view->obj to NULL and return -1;
        /// If this function is used as part of a getbufferproc, exporter MUST be set to the exporting object and flags must be passed unmodified.Otherwise, exporter MUST be NULL.
        /// </remarks>
        /// <param name="flags">The flags argument indicates the request type. This function always fills in view as specified by flags, unless buf has been designated as read-only and PyBUF_WRITABLE is set in flags.</param>
        /// <returns>On success, set view->obj to a new reference to exporter and return 0. Otherwise, raise PyExc_BufferError, set view->obj to NULL and return -1;</returns>
        public int PyBuffer_FillInfo(IntPtr exporter, IntPtr buf, long len, int _readonly, int flags)
        {
            return Runtime.PyBuffer_FillInfo(_handle, exporter, buf, len, _readonly, flags);
        }

        /// <summary>
        /// Writes a managed byte array into the buffer of a python object. This can be used to pass data like images from managed to python.
        /// </summary>
        public void WriteToBuffer(byte[] buffer, int offset, int count)
        {
            if (count > buffer.Length) throw new ArgumentOutOfRangeException("count", "Count is bigger than the buffer.");
            if (_view.ndim != 1)
            {
                // we dont support multidimensional arrays or scalars
                throw new NotSupportedException("Multidimensional arrays, scalars and objects without a buffer are not supported.");
            }

            int copylen = count < _view.len ? count : (int)_view.len;
            Marshal.Copy(buffer, 0, _view.buf, copylen);
        }
    }
#endif
}
