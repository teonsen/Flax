using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Flax.Windows
{
    public class FlaxProcess
    {
        public int Run(string executablePath, string args = "", bool waitForExit = false, bool runAsAdmin = false)
        {
            int exitCode = -1;
            var psi = new ProcessStartInfo();
            psi.FileName = executablePath;
            psi.Arguments = args;

            if (executablePath.ToLower().EndsWith(".exe"))
            {
                psi.WindowStyle = ProcessWindowStyle.Normal;
                psi.UseShellExecute = false;
                psi.Verb = runAsAdmin ? "runas" : "";
                if (System.IO.File.Exists(executablePath))
                {
                    System.IO.DirectoryInfo di = System.IO.Directory.GetParent(executablePath);
                    psi.WorkingDirectory = di.FullName;
                }

                var p = Process.Start(psi);
                if (waitForExit)
                {
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                p.Close();
                p.Dispose();
            }
            else if (executablePath.ToLower().EndsWith(".bat"))
            {
                RunCMD(executablePath, waitForExit);
            }
            else if (executablePath.ToLower().EndsWith(".vbs"))
            {
                psi.Arguments = args;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            return exitCode;
        }

        internal string RunCMD(string commandOrBatFilePath, bool waitForExit)
        {
            Process p = new Process();
            p.StartInfo.FileName = getSysNative(Environment.GetEnvironmentVariable("ComSpec"));
            p.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(commandOrBatFilePath.Replace("\"", ""));
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.Arguments = @"/c " + commandOrBatFilePath + " /w";
            p.Start();
            string results = p.StandardOutput.ReadToEnd();
            if (waitForExit)
            {
                p.WaitForExit();
            }
            p.Close();
            return results;
        }

        private string getSysNative(string pathContainsSystem32)
        {
            if (pathContainsSystem32.Contains("System32") && Environment.Is64BitProcess)
            {
                pathContainsSystem32 = pathContainsSystem32.Replace("System32", "Sysnative");
            }
            return pathContainsSystem32;
        }

    }
}
