namespace IDisposableAnalyzers.Test.IDISP016DontUseDisposedInstanceTests
{
    using Gu.Roslyn.Asserts;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using NUnit.Framework;

    public class ValidCode
    {
        public class DisposeCall
        {
            private static readonly DiagnosticAnalyzer Analyzer = new DisposeCallAnalyzer();
            private static readonly DiagnosticDescriptor Descriptor = IDISP016DontUseDisposedInstance.Descriptor;

            [Test]
            public void CreateTouchDispose()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        public void Bar()
        {
            var stream = File.OpenRead(string.Empty);
            var b = stream.ReadByte();
            stream.Dispose();
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [Test]
            public void UsingFileOpenRead()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public class Foo
    {
        public void Bar()
        {
            using (var stream = File.OpenRead(string.Empty))
            {
                 var b = stream.ReadByte();
            }
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [Test]
            public void DisposeInUsing()
            {
                // this is weird but should not warn I think
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public class Foo
    {
        public void Bar()
        {
            using (var stream = File.OpenRead(string.Empty))
            {
                stream.Dispose();
            }
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [Test]
            public void IfDisposeReturn()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        public void Bar(bool b)
        {
            var stream = File.OpenRead(string.Empty);
            if (b)
            {
                stream.Dispose();
                return;
            }

            var bb = stream.ReadByte();
            stream.Dispose();
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [Test]
            public void IfDisposeThrow()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.IO;

    public sealed class Foo
    {
        public void Bar(bool b)
        {
            var stream = File.OpenRead(string.Empty);
            if (b)
            {
                stream.Dispose();
                throw new Exception();
            }

            var bb = stream.ReadByte();
            stream.Dispose();
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [Test]
            public void ReassignAfterDispose()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        public void Bar()
        {
            var stream = File.OpenRead(string.Empty);
            var b = stream.ReadByte();
            stream.Dispose();
            stream = File.OpenRead(string.Empty);
            b = stream.ReadByte();
            stream.Dispose();
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [Test]
            public void ReassignViaOutAfterDispose()
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System.IO;

    public sealed class Foo
    {
        public void Bar()
        {
            Stream stream;
            Create(out stream);
            var b = stream.ReadByte();
            stream.Dispose();
            Create(out stream);
            b = stream.ReadByte();
            stream.Dispose();
        }

        private static void Create(out Stream stream)
        {
            stream = File.OpenRead(string.Empty);
        }
    }
}";
                AnalyzerAssert.Valid(Analyzer, Descriptor, testCode);
            }

            [TestCase("Tuple.Create(File.OpenRead(file1), File.OpenRead(file2))")]
            [TestCase("new Tuple<FileStream, FileStream>(File.OpenRead(file1), File.OpenRead(file2))")]
            public void Tuple(string expression)
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly Tuple<FileStream, FileStream> tuple;

        public C(string file1, string file2)
        {
            this.tuple = Tuple.Create(File.OpenRead(file1), File.OpenRead(file2));
        }

        public void Dispose()
        {
            this.tuple.Item1.Dispose();
            this.tuple.Item2.Dispose();
        }
    }
}".AssertReplace("Tuple.Create(File.OpenRead(file1), File.OpenRead(file2))", expression);

                AnalyzerAssert.Valid(Analyzer, testCode);
            }

            [TestCase("Tuple.Create(File.OpenRead(file), File.OpenRead(file))")]
            [TestCase("new Tuple<FileStream, FileStream>(File.OpenRead(file), File.OpenRead(file))")]
            public void LocalTuple(string expression)
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.IO;

    public sealed class C
    {
        public C(string file)
        {
            var tuple = Tuple.Create(File.OpenRead(file), File.OpenRead(file));
            tuple.Item1.Dispose();
            tuple.Item2.Dispose();
        }
    }
}".AssertReplace("Tuple.Create(File.OpenRead(file), File.OpenRead(file))", expression);

                AnalyzerAssert.Valid(Analyzer, testCode);
            }

            [TestCase("Tuple.Create(File.OpenRead(file), File.OpenRead(file))")]
            [TestCase("new Tuple<FileStream, FileStream>(File.OpenRead(file), File.OpenRead(file))")]
            public void ListOfTuple(string expression)
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    sealed class C : IDisposable
    {
        private readonly List<Tuple<FileStream, FileStream>> xs = new List<Tuple<FileStream, FileStream>>();

        public C(string file)
        {
            this.xs.Add(Tuple.Create(File.OpenRead(file), File.OpenRead(file)));
        }

        public void Dispose()
        {
            foreach (var tuple in this.xs)
            {
                tuple.Item1.Dispose();
                tuple.Item2.Dispose();
            }
        }
    }
}".AssertReplace("Tuple.Create(File.OpenRead(file), File.OpenRead(file))", expression);

                AnalyzerAssert.Valid(Analyzer, testCode);
            }

            [TestCase("Tuple.Create(File.OpenRead(file), File.OpenRead(file))")]
            [TestCase("new Tuple<FileStream, FileStream>(File.OpenRead(file), File.OpenRead(file))")]
            public void ListOfValueTuple(string expression)
            {
                var testCode = @"
namespace RoslynSandbox
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    sealed class C : IDisposable
    {
        private readonly List<(FileStream, FileStream)> xs = new List<(FileStream, FileStream)>();

        public C(string file)
        {
            this.xs.Add((File.OpenRead(file), File.OpenRead(file)));
        }

        public void Dispose()
        {
            foreach (var tuple in this.xs)
            {
                tuple.Item1.Dispose();
                tuple.Item2.Dispose();
            }
        }
    }
}".AssertReplace("Tuple.Create(File.OpenRead(file), File.OpenRead(file))", expression);

                AnalyzerAssert.Valid(Analyzer, testCode);
            }
        }
    }
}
