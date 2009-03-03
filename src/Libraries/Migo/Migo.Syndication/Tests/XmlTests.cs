//
// XmlTests.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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

using Migo.Syndication;

using Hyena.Tests;

namespace Migo.Syndication.Tests
{
    [TestFixture]
    public class XmlTests : TestBase
    {
        [Test]
        public void TestParseDates ()
        {
            // The local timezone for tests is America/Chicago (UTC -06:00)
            TransformPair<string, DateTime> [] pairs = TransformPair<string, DateTime>.GetFrom (
                "Fri, 22 Feb 2008 16:00:00 EST",        DateTime.Parse ("22/02/2008 15.00.00"),
                "Fri, 15 Feb 2008 4:10:00 EST",         DateTime.Parse ("15/02/2008 3.10.00"),
                "Tue, 08 Apr 2008 03:37:04 -0400",      DateTime.Parse ("08/04/2008 2.37.04"),
                "Tue, 26 Feb 2008 03:28:51 -0500",      DateTime.Parse ("26/02/2008 2.28.51"),
                "Sun, 11 May 2008 01:33:26 -0400",      DateTime.Parse ("11/05/2008 0.33.26"),
                "Fri, 16 May 2008 16:09:10 -0500",      DateTime.Parse ("16/05/2008 16.09.10"),
                "Fri, 14 Mar 2008 13:44:53 -0500",      DateTime.Parse ("14/03/2008 13.44.53"),
                "Fri, 07 December 2007 17:00:00 EST",   DateTime.Parse ("07/12/2007 16.00.00"),
                "Sat, 08 Mar 2008 12:00:00 EST",        DateTime.Parse ("08/03/2008 11.00.00"),
                "Sat, 17 May 2008 20:47:57 +0000",      DateTime.Parse ("17/05/2008 15.47.57"),
                "Sat, 17 May 2008 19:33:42 +0000",      DateTime.Parse ("17/05/2008 14.33.42")
            );
    
            AssertForEach (pairs, delegate (TransformPair<string, DateTime> pair) {
                Assert.AreEqual (pair.To, Rfc822DateTime.Parse (pair.From));
            });
        }
    
        [Test]
        public void TestParseITunesDuration ()
        {
            TransformPair<string, TimeSpan> [] pairs = TransformPair<string, TimeSpan>.GetFrom (
                null,      TimeSpan.Zero,
                "",        TimeSpan.Zero,
                "0",       TimeSpan.Zero,
                "0:0",     TimeSpan.Zero,
                "0:0:0",   TimeSpan.Zero,
                "1:0:0",   new TimeSpan (1, 0, 0),
                "363",     new TimeSpan (0, 0, 363),
                "2:45",    new TimeSpan (0, 2, 45),
                "1:02:22", new TimeSpan (1, 2, 22),
                "9:0:0",   new TimeSpan (9, 0, 0)
            );
    
            AssertForEach (pairs, delegate (TransformPair<string, TimeSpan> pair) {
                Assert.AreEqual (pair.To, RssParser.GetITunesDuration (pair.From));
            });
        }
    }
}

#endif
