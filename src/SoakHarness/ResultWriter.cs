using System;
using System.IO;
using System.Text;

namespace SoakHarness
{
    internal static class ResultWriter
    {
        private static readonly object Gate = new object();
        private static bool _written;

        public static string ResultsDirectory
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("VTOLVR_CONNECT_RESULTS");
                if (!string.IsNullOrEmpty(env))
                    return env;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "vtolvr-connect",
                    "results");
            }
        }

        public static string RunId
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("VTOLVR_CONNECT_RUN_ID");
                return string.IsNullOrEmpty(env) ? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") : env;
            }
        }

        public static void Write(string status, string stage, string detail, int seed)
        {
            lock (Gate)
            {
                if (_written)
                    return;
                _written = true;

                try
                {
                    Directory.CreateDirectory(ResultsDirectory);
                    var path = Path.Combine(ResultsDirectory, RunId + ".json");
                    var json = new StringBuilder();
                    json.AppendLine("{");
                    json.AppendFormat("  \"runId\": \"{0}\",\n", Escape(RunId));
                    json.AppendFormat("  \"status\": \"{0}\",\n", Escape(status));
                    json.AppendFormat("  \"stage\": \"{0}\",\n", Escape(stage ?? ""));
                    json.AppendFormat("  \"detail\": \"{0}\",\n", Escape(detail ?? ""));
                    json.AppendFormat("  \"seed\": {0},\n", seed);
                    json.AppendFormat("  \"utc\": \"{0}\"\n", DateTime.UtcNow.ToString("o"));
                    json.AppendLine("}");

                    var tmp = path + ".tmp";
                    File.WriteAllText(tmp, json.ToString());
                    if (File.Exists(path))
                        File.Delete(path);
                    File.Move(tmp, path);
                    UnityEngine.Debug.Log("[SoakHarness] Wrote result " + path + " status=" + status);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("[SoakHarness] Failed to write result: " + ex);
                }
            }
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
