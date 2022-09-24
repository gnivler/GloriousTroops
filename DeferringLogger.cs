using System;
using System.IO;
using System.Text;
using TaleWorlds.Engine;

namespace UniqueTroopsGoneWild
{
    // simplified from https://github.com/BattletechModders/IRBTModUtils
    internal class DeferringLogger
    {
        private readonly LogWriter logWriter;

        internal DeferringLogger()
        {
            var logFilename = Utilities.GetFullModulePath("UniqueTroopsGoneWild") + "log.txt";
            logWriter = new LogWriter(new StreamWriter(logFilename, true, Encoding.ASCII));
        }

        internal LogWriter? Debug => Globals.Settings.Debug ? logWriter : null;

        internal readonly struct LogWriter
        {
            private readonly StreamWriter sw;

            public LogWriter(StreamWriter sw)
            {
                this.sw = sw;
                sw.AutoFlush = true;
            }

            internal void Log(object input)
            {
                sw.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {(string.IsNullOrEmpty(input?.ToString()) ? "IsNullOrEmpty" : input)}");
            }
        }
    }
}
