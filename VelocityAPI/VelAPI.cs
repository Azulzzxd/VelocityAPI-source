using coms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace VelocityAPI
{
    public class VelAPI
    {
        HttpClient client = new HttpClient();
        private string current_version_url = "https://realvelocity.xyz/assets/current_version.txt";
        private string current_download_links_url = "https://realvelocity.xyz/assets/download_links.json";
        private Process decompilerProcess;
        public VelocityStates VelocityStatus = VelocityStates.NotAttached;
        public List<int> injected_pids = new List<int>();
        private System.Timers.Timer CommunicationTimer;

        public static string Base64Encode(string plainText)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        private static DownloadUrlData ParseJson(string json)
        {
            return new DownloadUrlData()
            {
                L1 = Get("L1"),
                L2 = Get("L2"),
                question = Get("question")
            };

            string Get(string key)
            {
                Match match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"(.*?)\"");
                return !match.Success ? (string)null : match.Groups[1].Value;
            }
        }

        public static byte[] Base64Decode(string plainText) => Convert.FromBase64String(plainText);

        private bool IsPidRunning(int pid)
        {
            try
            {
                Process.GetProcessById(pid);
                return true;
            }
            catch (ArgumentException ex)
            {
                return false;
            }
        }

        private void AutoUpdate()
        {
            HttpResponseMessage result1 = this.client.GetAsync(this.current_download_links_url).Result;
            DownloadUrlData json = VelAPI.ParseJson(result1.Content.ReadAsStringAsync().Result);
            string requestUri1 = AESEncryption.Decrypt(json.L1, json.question);
            string requestUri2 = AESEncryption.Decrypt(json.L2, json.question);
            string result2;
            try
            {
                result2 = this.client.GetStringAsync(this.current_version_url).Result;
            }
            catch (Exception ex)
            {
                return;
            }
            string str = "";
            if (File.Exists("Bin\\current_version.txt"))
                str = File.ReadAllText("Bin\\current_version.txt");
            if (result2 != str)
            {
                if (File.Exists("Bin\\erto3e4rortoergn.exe"))
                    File.Delete("Bin\\erto3e4rortoergn.exe");
                if (File.Exists("Bin\\Decompiler.exe"))
                    File.Delete("Bin\\Decompiler.exe");
                HttpResponseMessage result3 = this.client.GetAsync(requestUri2).Result;
                if (result1.IsSuccessStatusCode)
                    File.WriteAllBytes("Bin\\erto3e4rortoergn.exe", result3.Content.ReadAsByteArrayAsync().Result);
                HttpResponseMessage result4 = this.client.GetAsync(requestUri1).Result;
                if (result1.IsSuccessStatusCode)
                    File.WriteAllBytes("Bin\\Decompiler.exe", result4.Content.ReadAsByteArrayAsync().Result);
            }
            File.WriteAllText("Bin\\current_version.txt", result2);
        }

        public void StartCommunication()
        {
            if (!Directory.Exists("Bin"))
                Directory.CreateDirectory("Bin");
            if (!Directory.Exists("AutoExec"))
                Directory.CreateDirectory("AutoExec");
            if (!Directory.Exists("Workspace"))
                Directory.CreateDirectory("Workspace");
            if (!Directory.Exists("Scripts"))
                Directory.CreateDirectory("Scripts");
            this.AutoUpdate();
            this.StopCommunication();
            this.decompilerProcess = new Process();
            this.decompilerProcess.StartInfo.FileName = "Bin\\Decompiler.exe";
            this.decompilerProcess.StartInfo.UseShellExecute = false;
            this.decompilerProcess.EnableRaisingEvents = true;
            this.decompilerProcess.StartInfo.RedirectStandardError = true;
            this.decompilerProcess.StartInfo.RedirectStandardInput = true;
            this.decompilerProcess.StartInfo.RedirectStandardOutput = true;
            this.decompilerProcess.StartInfo.CreateNoWindow = true;
            this.decompilerProcess.Start();
            this.CommunicationTimer = new System.Timers.Timer(100.0);
            this.CommunicationTimer.Elapsed += (ElapsedEventHandler)((source, e) =>
            {
                foreach (int injectedPid in this.injected_pids)
                {
                    if (!this.IsPidRunning(injectedPid))
                        this.injected_pids.Remove(injectedPid);
                }
                string plainText = $"setworkspacefolder: {Directory.GetCurrentDirectory()}\\Workspace";
                foreach (int injectedPid in this.injected_pids)
                    NamedPipes.LuaPipe(VelAPI.Base64Encode(plainText), injectedPid);
            });
            this.CommunicationTimer.Start();
        }

        public void StopCommunication()
        {
            if (this.CommunicationTimer != null)
            {
                this.CommunicationTimer.Stop();
                this.CommunicationTimer = null;
            }
            if (this.decompilerProcess != null)
            {
                this.decompilerProcess.Kill();
                this.decompilerProcess.Dispose();
                this.decompilerProcess = (Process)null;
            }
            this.injected_pids.Clear();
        }

        public bool IsAttached(int pid) => this.injected_pids.Contains(pid);

        public async Task<VelocityStates> Attach(int pid)
        {
            if (this.injected_pids.Contains(pid))
                return VelocityStates.Attached;
            this.VelocityStatus = VelocityStates.Attaching;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "Bin\\erto3e4rortoergn.exe";
            startInfo.Arguments = $"{pid}";
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = false;
            startInfo.RedirectStandardOutput = false;
            Process.Start(startInfo).WaitForExit();
            this.injected_pids.Add(pid);
            this.VelocityStatus = VelocityStates.Attached;
            return VelocityStates.Attached;
        }

        public VelocityStates Execute(string script)
        {
            if (this.injected_pids.Count.Equals(0))
                return VelocityStates.NotAttached;
            foreach (int injectedPid in this.injected_pids)
                NamedPipes.LuaPipe(VelAPI.Base64Encode(script), injectedPid);
            return VelocityStates.Executed;
        }
    }
}