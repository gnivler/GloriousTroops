using System;
using System.IO;
using System.Text;
using TaleWorlds.Engine;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using Path = System.IO.Path;

namespace UniqueTroopsGoneWild
{
    // simplified from https://github.com/BattletechModders/IRBTModUtils
    internal class DeferringLogger
    {
        private readonly LogWriter logWriter;
        internal static readonly string logFilename = Path.Combine(ModuleHelper.GetModuleFullPath("UniqueTroopsGoneWild"), "log.txt");

        internal DeferringLogger()
        {
            logWriter = new LogWriter(new StreamWriter(logFilename, true, Encoding.ASCII));
        }

        internal LogWriter? Debug => Globals.Settings?.Debug is not null ? logWriter : null;

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
