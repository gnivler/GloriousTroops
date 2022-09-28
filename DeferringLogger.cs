using System;
using System.IO;
using System.Net;
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
        private LogWriter logWriter;
        private readonly string logPath;
        private string OldLogPath => @$"{new DirectoryInfo(logPath).FullName.Split('.')[0]}.old.txt";

        internal DeferringLogger(string logFilename)
        {
            logPath = logFilename;
            logWriter = new LogWriter(new StreamWriter(logFilename, true, Encoding.ASCII));
        }

        internal LogWriter? Debug
        {
            get
            {
                if (Globals.Settings?.Debug is null)
                    return null;
                return Globals.Settings.Debug ? logWriter : null;
            }
        }

        internal struct LogWriter
        {
            internal StreamWriter sw;

            public LogWriter(StreamWriter sw)
            {
                this.sw = sw;
                sw.AutoFlush = true;
            }

            internal void Log(object input)
            {
                sw.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {(string.IsNullOrEmpty(input?.ToString()) ? "IsNullOrEmpty" : input)}");
                sw.Flush();
            }
        }

        internal void Restart()
        {
            logWriter.sw.Close();
            File.Copy(logPath, OldLogPath, true);
            File.Delete(logPath);
            logWriter.sw = new StreamWriter(logPath, true, Encoding.ASCII);
            logWriter.sw.AutoFlush = true;
        }
    }
}
