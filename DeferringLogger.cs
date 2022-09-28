using System;
using System.IO;
using System.Text;

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
            internal StreamWriter Sw;

            public LogWriter(StreamWriter sw)
            {
                Sw = sw;
                sw.AutoFlush = true;
            }

            internal void Log(object input)
            {
                Sw.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {(string.IsNullOrEmpty(input?.ToString()) ? "IsNullOrEmpty" : input)}");
                Sw.Flush();
            }
        }

        internal void Restart()
        {
            logWriter.Sw.Close();
            File.Copy(logPath, OldLogPath, true);
            File.Delete(logPath);
            logWriter.Sw = new StreamWriter(logPath, true, Encoding.ASCII);
            logWriter.Sw.AutoFlush = true;
        }
    }
}
