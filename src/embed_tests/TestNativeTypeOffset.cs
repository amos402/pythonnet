using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingPythonTest
{
    public class TestNativeTypeOffset
    {
        private Py.GILState _gs;

        [SetUp]
        public void SetUp()
        {
            _gs = Py.GIL();
        }

        [TearDown]
        public void Dispose()
        {
            _gs.Dispose();
        }

        /// <summary>
        /// Tests that installation has generated code for NativeTypeOffset and that it can be loaded.
        /// </summary>        
        [Test]
        public void LoadNativeTypeOffsetClass()
        {
            PyObject sys = Py.Import("sys");
            // We can safely ignore the "m" abi flag
            var abiflags = sys.GetAttr("abiflags", "".ToPython()).ToString().Replace("m", "");
            if (!string.IsNullOrEmpty(abiflags))
            {
                string typeName = "Python.Runtime.NativeTypeOffset, Python.Runtime";
                Assert.NotNull(Type.GetType(typeName), $"{typeName} does not exist and sys.abiflags={abiflags}");
            }
        }
    }
}
