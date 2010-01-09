//
// CoverArtSpecTests.cs
//
// Author:
//   John Millikin <jmillikin@gmail.com>
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

#if ENABLE_TESTS

using System;
using NUnit.Framework;

using Banshee.Base;

namespace Banshee.Base.Tests
{
    [TestFixture]
    public class EscapePartTests
    {
        private void AssertEscaped (string original, string expected)
        {
            Assert.AreEqual (expected, CoverArtSpec.EscapePart (original));
        }

        [Test]
        public void TestEmpty ()
        {
            AssertEscaped (null, null);
            AssertEscaped ("", null);
        }

        [Test]
        public void TestLowercased ()
        {
            AssertEscaped ("A", "a");
        }

        [Test]
        public void TestUnwanted ()
        {
            // Part of the in-progress media art storage spec
            AssertEscaped ("!", "");
            AssertEscaped ("@", "");
            AssertEscaped ("#", "");
            AssertEscaped ("$", "");
            AssertEscaped ("^", "");
            AssertEscaped ("&", "");
            AssertEscaped ("*", "");
            AssertEscaped ("_", "");
            AssertEscaped ("+", "");
            AssertEscaped ("=", "");
            AssertEscaped ("|", "");
            AssertEscaped ("\\", "");
            AssertEscaped ("/", "");
            AssertEscaped ("?", "");
            AssertEscaped ("~", "");
            AssertEscaped ("`", "");
            AssertEscaped ("'", "");
            AssertEscaped ("\"", "");

            // Banshee-specific: strip *everything* non-ASCII
            AssertEscaped ("\u00e9toile", "toile");
            AssertEscaped ("e\u0301", "e");
        }

        [Test]
        public void TestStripNotes ()
        {
            AssertEscaped ("a(b)cd", "a");
            AssertEscaped ("a(b)c(d)e", "abc");
        }
    }
}

#endif
