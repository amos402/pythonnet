using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
    public enum BufferOrderStyle
    {
        C,
        Fortran,
        EitherOne,
    }

    public sealed class PyBuffer : IDisposable
    {
        private PyObject _exporter;
        private Py_buffer _view;
        private GCHandle _gchandle;

        unsafe internal PyBuffer(PyObject exporter, int flags)
        {
            int size = Marshal.SizeOf(typeof(Py_buffer));
            byte[] rawData = new byte[size];
            _gchandle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
            IntPtr handle = _gchandle.AddrOfPinnedObject();

            if (Runtime.PyObject_GetBuffer(exporter.Handle, handle, flags) < 0)
            {
                _gchandle.Free();
                throw new PythonException();
            }

            _exporter = exporter;
            _view = (Py_buffer)Marshal.PtrToStructure(handle, typeof(Py_buffer));

            var intPtrBuf = new IntPtr[_view.ndim];
            if (_view.shape != IntPtr.Zero)
            {
                Marshal.Copy(_view.shape, intPtrBuf, 0, (int)_view.len * sizeof(IntPtr));
                Shape = intPtrBuf.Select(x => (long)x).ToArray();
            }

            if (_view.strides != IntPtr.Zero) {
                Marshal.Copy(_view.strides, intPtrBuf, 0, (int)_view.len * sizeof(IntPtr));
                Strides = intPtrBuf.Select(x => (long)x).ToArray();
            }

            if (_view.suboffsets != IntPtr.Zero) {
                Marshal.Copy(_view.suboffsets, intPtrBuf, 0, (int)_view.len * sizeof(IntPtr));
                SubOffsets = intPtrBuf.Select(x => (long)x).ToArray();
            }
        }

        public PyObject Object => _exporter;
        public long Length => (long)_view.len;
        public long ItemSize => (long)_view.itemsize;
        public int Dimensions => _view.ndim;
        public bool ReadOnly => _view._readonly;
        public IntPtr Buffer => _view.buf;

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
                throw new NotSupportedException("SizeFromFormat requires at least Python 3.9");
            return (long)Runtime.PyBuffer_SizeFromFormat(format);
        }

        /// <summary>
        /// Returns true if the memory defined by the view is C-style (order is 'C') or Fortran-style (order is 'F') contiguous or either one (order is 'A'). Returns false otherwise.
        /// </summary>
        /// <param name="order">C-style (order is 'C') or Fortran-style (order is 'F') contiguous or either one (order is 'A')</param>
        public bool IsContiguous(BufferOrderStyle order)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));

            char style = 'C';
            if (order == BufferOrderStyle.C)
                style = 'C';
            else if (order == BufferOrderStyle.Fortran)
                style = 'F';
            else if (order == BufferOrderStyle.EitherOne)
                style = 'A';

            return Convert.ToBoolean(Runtime.PyBuffer_IsContiguous(_gchandle.AddrOfPinnedObject(), style));
        }

        /// <summary>
        /// Get the memory area pointed to by the indices inside the given view. indices must point to an array of view->ndim indices.
        /// </summary>
        public IntPtr GetPointer(long[] indices)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));
            if (Runtime.pyversionnumber < 37)
                throw new NotSupportedException("GetPointer requires at least Python 3.7");
            return Runtime.PyBuffer_GetPointer(_gchandle.AddrOfPinnedObject(), indices.Select(x => (IntPtr)x).ToArray());
        }

        /// <summary>
        /// Copy contiguous len bytes from buf to view. fort can be 'C' or 'F' (for C-style or Fortran-style ordering).
        /// </summary>
        public void FromContiguous(IntPtr buf, long len, BufferOrderStyle fort)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));
            if (Runtime.pyversionnumber < 37)
                throw new NotSupportedException("FromContiguous requires at least Python 3.7");

            char style = 'C';
            if (fort == BufferOrderStyle.C)
                style = 'C';
            else if (fort == BufferOrderStyle.Fortran)
                style = 'F';
            else if (fort == BufferOrderStyle.EitherOne)
                throw new ArgumentException("BufferOrderStyle can not be EitherOne and has to be C or Fortran", nameof(fort));

            if (Runtime.PyBuffer_FromContiguous(_gchandle.AddrOfPinnedObject(), buf, (IntPtr)len, style) < 0)
                throw new PythonException();
        }

        /// <summary>
        /// Copy len bytes from view to its contiguous representation in buf. order can be 'C' or 'F' or 'A' (for C-style or Fortran-style ordering or either one). 0 is returned on success, -1 on error.
        /// </summary>
        /// <param name="order">order can be 'C' or 'F' or 'A' (for C-style or Fortran-style ordering or either one).</param>
        /// <param name="buf">Buffer to copy to</param>
        public void ToContiguous(IntPtr buf, BufferOrderStyle order)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));
            if (Runtime.pyversionnumber < 36)
                throw new NotSupportedException("ToContiguous requires at least Python 3.6");

            char style = 'C';
            if (order == BufferOrderStyle.C)
                style = 'C';
            else if (order == BufferOrderStyle.Fortran)
                style = 'F';
            else if (order == BufferOrderStyle.EitherOne)
                style = 'A';

            if (Runtime.PyBuffer_ToContiguous(buf, _gchandle.AddrOfPinnedObject(), _view.len, style) < 0)
                throw new PythonException();
        }

        /// <summary>
        /// Fill the strides array with byte-strides of a contiguous (C-style if order is 'C' or Fortran-style if order is 'F') array of the given shape with the given number of bytes per element.
        /// </summary>
        public static void FillContiguousStrides(int ndims, IntPtr shape, IntPtr strides, int itemsize, BufferOrderStyle order)
        {
            char style = 'C';
            if (order == BufferOrderStyle.C)
                style = 'C';
            else if (order == BufferOrderStyle.Fortran)
                style = 'F';
            else if (order == BufferOrderStyle.EitherOne)
                throw new ArgumentException("BufferOrderStyle can not be EitherOne and has to be C or Fortran", nameof(order));

            Runtime.PyBuffer_FillContiguousStrides(ndims, shape, strides, itemsize, style);
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
        /// <returns>On success, set view->obj to a new reference to exporter and return 0. Otherwise, raise PyExc_BufferError, set view->obj to NULL and return -1;</returns>
        public void FillInfo(IntPtr exporter, IntPtr buf, long len, bool _readonly, int flags)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));
            if (Runtime.PyBuffer_FillInfo(_gchandle.AddrOfPinnedObject(), exporter, buf, (IntPtr)len, Convert.ToInt32(_readonly), flags) < 0)
                throw new PythonException();
        }

        /// <summary>
        /// Writes a managed byte array into the buffer of a python object. This can be used to pass data like images from managed to python.
        /// </summary>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));
            if (ReadOnly)
                throw new InvalidOperationException("Buffer is read-only");
            if ((long)_view.len > int.MaxValue)
                throw new NotSupportedException("Python buffers bigger than int.MaxValue are not supported.");
            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException("count", "Count is bigger than the buffer.");
            if (count > (int)_view.len)
                throw new ArgumentOutOfRangeException("count", "Count is bigger than the python buffer.");
            if (_view.ndim != 1)
                throw new NotSupportedException("Multidimensional arrays, scalars and objects without a buffer are not supported.");

            Marshal.Copy(buffer, offset, _view.buf, count);
        }

        /// <summary>
        /// Reads the buffer of a python object into a managed byte array. This can be used to pass data like images from python to managed.
        /// </summary>
        public int Read(byte[] buffer, int offset, int count) {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(PyBuffer));
            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException("count", "Count is bigger than the buffer.");
            if (_view.ndim != 1)
                throw new NotSupportedException("Multidimensional arrays, scalars and objects without a buffer are not supported.");
            if (_view.len.ToInt64() > int.MaxValue)
                throw new NotSupportedException("Python buffers bigger than int.MaxValue are not supported.");

            int copylen = count < (int)_view.len ? count : (int)_view.len;
            Marshal.Copy(_view.buf, buffer, offset, copylen);
            return copylen;
        }

        private bool disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue) {
                Runtime.PyBuffer_Release(_gchandle.AddrOfPinnedObject());
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
        /// It is an error to call this function on a buffer that was not obtained via <see cref="PyObject.GetBuffer"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
