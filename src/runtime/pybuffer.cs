using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
    public class PyBuffer : IDisposable
    {
        private PyObject _exporter;
        private IntPtr _handle;
        private Runtime.Py_buffer _view;
        private GCHandle _gchandle;

        internal PyBuffer(PyObject exporter, int flags)
        {
            int size = Marshal.SizeOf(typeof(Runtime.Py_buffer));
            byte[] rawData = new byte[size];
            _gchandle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
            _handle = _gchandle.AddrOfPinnedObject();


            if (Runtime.PyObject_GetBuffer(exporter.Handle, _handle, flags) < 0)
            {
                _gchandle.Free();
                throw new PythonException();
            }

            _exporter = exporter;
            _view = (Runtime.Py_buffer)Marshal.PtrToStructure(_handle, typeof(Runtime.Py_buffer));

            if (_view.shape != IntPtr.Zero)
            {
                Shape = new long[_view.ndim];
                Marshal.Copy(_view.shape, Shape, 0, (int)_view.len * sizeof(long));
            }

            if (_view.strides != IntPtr.Zero) {
                Strides = new long[_view.ndim];
                Marshal.Copy(_view.strides, Strides, 0, (int)_view.len * sizeof(long));
            }

            if (_view.suboffsets != IntPtr.Zero) {
                SubOffsets = new long[_view.ndim];
                Marshal.Copy(_view.suboffsets, SubOffsets, 0, (int)_view.len * sizeof(long));
            }
        }

        public PyObject Object => _exporter;
        public long Length => _view.len;
        public int Dimensions => _view.ndim;
        public bool ReadOnly => Convert.ToBoolean(_view._readonly);

        /// <summary>
        /// An array of length <see cref="Dimensions"/> indicating the shape of the memory as an n-dimensional array.
        /// </summary>
        public long[] Shape { get; private set; }

        /// <summary>
        /// An array of length <see cref="Dimensions"/> giving the number of bytes to skip to get to a new element in each dimension.
        /// Will be null except when PyBUF_STRIDES or PyBUF_INDIRECT flags in GetBuffer/>.
        /// </summary>
        public long[] Strides { get; private set; }

        /// <summary>
        /// An array of Py_ssize_t of length ndim. If suboffsets[n] >= 0,
        /// the values stored along the nth dimension are pointers and the suboffset value dictates how many bytes to add to each pointer after de-referencing.
        /// A suboffset value that is negative indicates that no de-referencing should occur (striding in a contiguous memory block).
        /// </summary>
        public long[] SubOffsets { get; private set; }

        /// <summary>
        /// Return the implied itemsize from format. On error, raise an exception and return -1.
        /// New in version 3.9.
        /// </summary>
        public static long SizeFromFormat(string format)
        {
            if (Runtime.pyversionnumber < 39)
                throw new NotSupportedException("GetPointer requires at least Python 3.9");
            return Runtime.PyBuffer_SizeFromFormat(format);
        }

        /// <summary>
        /// Returns true if the memory defined by the view is C-style (order is 'C') or Fortran-style (order is 'F') contiguous or either one (order is 'A'). Returns false otherwise.
        /// </summary>
        /// <param name="order">C-style (order is 'C') or Fortran-style (order is 'F') contiguous or either one (order is 'A')</param>
        public bool IsContiguous(char order)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            return Convert.ToBoolean(Runtime.PyBuffer_IsContiguous(_handle, order));
        }

        /// <summary>
        /// Get the memory area pointed to by the indices inside the given view. indices must point to an array of view->ndim indices.
        /// </summary>
        public IntPtr GetPointer(long[] indices)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            if (Runtime.pyversionnumber < 37)
                throw new NotSupportedException("GetPointer requires at least Python 3.7");
            return Runtime.PyBuffer_GetPointer(_handle, indices);
        }

        /// <summary>
        /// Copy contiguous len bytes from buf to view. fort can be 'C' or 'F' (for C-style or Fortran-style ordering). 0 is returned on success, -1 on error.
        /// </summary>
        /// <param name="fort">fort can be 'C' or 'F' (for C-style or Fortran-style ordering)</param>
        /// <returns>0 is returned on success, -1 on error.</returns>
        public int FromContiguous(IntPtr buf, long len, char fort)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            if (Runtime.pyversionnumber < 37)
                throw new NotSupportedException("FromContiguous requires at least Python 3.7");
            return Runtime.PyBuffer_FromContiguous(_handle, buf, len, fort);
        }

        /// <summary>
        /// Copy len bytes from view to its contiguous representation in buf. order can be 'C' or 'F' or 'A' (for C-style or Fortran-style ordering or either one). 0 is returned on success, -1 on error.
        /// </summary>
        /// <param name="order">order can be 'C' or 'F' or 'A' (for C-style or Fortran-style ordering or either one).</param>
        /// <returns>0 is returned on success, -1 on error.</returns>
        public int ToContiguous(IntPtr buf, char order)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            if (Runtime.pyversionnumber < 36)
                throw new NotSupportedException("ToContiguous requires at least Python 3.6");
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
        public int FillInfo(IntPtr exporter, IntPtr buf, long len, int _readonly, int flags)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            return Runtime.PyBuffer_FillInfo(_handle, exporter, buf, len, _readonly, flags);
        }

        /// <summary>
        /// Writes a managed byte array into the buffer of a python object. This can be used to pass data like images from managed to python.
        /// </summary>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            if (ReadOnly)
                throw new Exception("Buffer is read-only");
            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException("count", "Count is bigger than the buffer.");
            // we dont support multidimensional arrays or scalars
            if (_view.ndim != 1)
                throw new NotSupportedException("Multidimensional arrays, scalars and objects without a buffer are not supported.");

            int copylen = count < _view.len ? count : (int)_view.len;
            Marshal.Copy(buffer, offset, _view.buf, copylen);
        }

        /// <summary>
        /// Reads the buffer of a python object into a managed byte array. This can be used to pass data like images from python to managed.
        /// </summary>
        public int Read(byte[] buffer, int offset, int count) {
            if (disposedValue)
                throw new ObjectDisposedException("PyBuffer");
            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException("count", "Count is bigger than the buffer.");
            // we dont support multidimensional arrays or scalars
            if (_view.ndim != 1)
                throw new NotSupportedException("Multidimensional arrays, scalars and objects without a buffer are not supported.");

            int copylen = count < _view.len ? count : (int)_view.len;
            Marshal.Copy(_view.buf, buffer, offset, copylen);
            return copylen;
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                Runtime.PyBuffer_Release(_handle);
                _gchandle.Free();
                _exporter = null;
                Shape = null;
                Strides = null;
                SubOffsets = null;

                disposedValue = true;
            }
        }

        ~PyBuffer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Release the buffer view and decrement the reference count for view->obj. This function MUST be called when the buffer is no longer being used, otherwise reference leaks may occur.
        /// It is an error to call this function on a buffer that was not obtained via <see cref="PyObject.GetBuffer()"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
