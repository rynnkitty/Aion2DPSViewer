using System;
using System.IO;
using System.Text;

namespace Aion2DPSViewer.Core;

public class TeeTextWriter : TextWriter
{
    private readonly TextWriter _a;
    private readonly TextWriter _b;

    public TeeTextWriter(TextWriter a, TextWriter b)
    {
        _a = a;
        _b = b;
    }

    public override Encoding Encoding => _a.Encoding;

    public override void WriteLine(string? value)
    {
        _a.WriteLine(value);
        _b.WriteLine(value);
    }

    public override void Write(string? value)
    {
        _a.Write(value);
        _b.Write(value);
    }

    public override void Write(char value)
    {
        _a.Write(value);
        _b.Write(value);
    }
}
