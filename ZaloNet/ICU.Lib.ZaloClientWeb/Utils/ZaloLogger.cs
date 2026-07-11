using System;

namespace ICU.Lib.ZaloClientWeb.Utils;

/// <summary>
/// Simple logger for Zalo API operations.
/// Equivalent to the logger() function in zca-js.
/// </summary>
public class ZaloLogger
{
    private readonly bool _logging;

    public ZaloLogger(bool logging)
    {
        _logging = logging;
    }

    public void Verbose(params object[] args)
    {
        if (_logging) Console.WriteLine($"\x1b[35m🚀 VERBOSE\x1b[0m {string.Join(" ", args)}");
    }

    public void Info(params object[] args)
    {
        if (_logging) Console.WriteLine($"\x1b[34mINFO\x1b[0m {string.Join(" ", args)}");
    }

    public void Warn(params object[] args)
    {
        if (_logging) Console.WriteLine($"\x1b[33mWARN\x1b[0m {string.Join(" ", args)}");
    }

    public void Error(params object[] args)
    {
        if (_logging) Console.WriteLine($"\x1b[31mERROR\x1b[0m {string.Join(" ", args)}");
    }

    public void Success(params object[] args)
    {
        if (_logging) Console.WriteLine($"\x1b[32mSUCCESS\x1b[0m {string.Join(" ", args)}");
    }

    public void Timestamp(params object[] args)
    {
        if (_logging) Console.WriteLine($"\x1b[90m[{DateTime.UtcNow:O}]\x1b[0m {string.Join(" ", args)}");
    }
}