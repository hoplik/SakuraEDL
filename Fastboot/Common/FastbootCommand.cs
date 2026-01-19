using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LoveAlways.Fastboot.Common
{
    /// <summary>
    /// Fastboot 命令执行器
    /// 封装 fastboot.exe 命令行工具
    /// </summary>
    public class FastbootCommand : IDisposable
    {
        private Process _process;
        private static string _fastbootPath;

        public StreamReader StdOut { get; private set; }
        public StreamReader StdErr { get; private set; }
        public StreamWriter StdIn { get; private set; }

        /// <summary>
        /// 设置 fastboot.exe 路径
        /// </summary>
        public static void SetFastbootPath(string path)
        {
            _fastbootPath = path;
        }

        /// <summary>
        /// 获取 fastboot.exe 路径
        /// </summary>
        public static string GetFastbootPath()
        {
            if (string.IsNullOrEmpty(_fastbootPath))
            {
                // 默认在程序目录下查找
                _fastbootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fastboot.exe");
            }
            return _fastbootPath;
        }

        /// <summary>
        /// 创建 Fastboot 命令实例
        /// </summary>
        /// <param name="serial">设备序列号（可为null表示使用默认设备）</param>
        /// <param name="action">要执行的命令</param>
        public FastbootCommand(string serial, string action)
        {
            string fastbootExe = GetFastbootPath();
            if (!File.Exists(fastbootExe))
            {
                throw new FileNotFoundException("fastboot.exe 不存在", fastbootExe);
            }

            _process = new Process();
            _process.StartInfo.FileName = fastbootExe;
            _process.StartInfo.Arguments = string.IsNullOrEmpty(serial) 
                ? action 
                : $"-s \"{serial}\" {action}";
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            _process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
            _process.Start();

            StdOut = _process.StandardOutput;
            StdErr = _process.StandardError;
            StdIn = _process.StandardInput;
        }

        /// <summary>
        /// 等待命令执行完成
        /// </summary>
        public void WaitForExit()
        {
            _process?.WaitForExit();
        }

        /// <summary>
        /// 等待命令执行完成（带超时）
        /// </summary>
        public bool WaitForExit(int milliseconds)
        {
            return _process?.WaitForExit(milliseconds) ?? true;
        }

        /// <summary>
        /// 获取退出码
        /// </summary>
        public int ExitCode => _process?.ExitCode ?? -1;

        /// <summary>
        /// 异步执行命令并返回输出
        /// </summary>
        public static async Task<FastbootResult> ExecuteAsync(string serial, string action, 
            CancellationToken ct = default, Action<string> onOutput = null)
        {
            var result = new FastbootResult();
            
            try
            {
                using (var cmd = new FastbootCommand(serial, action))
                {
                    var stdoutTask = cmd.StdOut.ReadToEndAsync();
                    var stderrTask = cmd.StdErr.ReadToEndAsync();

                    // 使用 Task.Run 来等待进程，以支持取消
                    await Task.Run(() =>
                    {
                        while (!cmd._process.HasExited)
                        {
                            ct.ThrowIfCancellationRequested();
                            Thread.Sleep(100);
                        }
                    }, ct);

                    result.StdOut = await stdoutTask;
                    result.StdErr = await stderrTask;
                    result.ExitCode = cmd.ExitCode;
                    result.Success = cmd.ExitCode == 0;

                    onOutput?.Invoke(result.StdErr);
                }
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.StdErr = "操作已取消";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StdErr = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 同步执行命令并返回输出
        /// </summary>
        public static FastbootResult Execute(string serial, string action)
        {
            var result = new FastbootResult();

            try
            {
                using (var cmd = new FastbootCommand(serial, action))
                {
                    result.StdOut = cmd.StdOut.ReadToEnd();
                    result.StdErr = cmd.StdErr.ReadToEnd();
                    cmd.WaitForExit();
                    result.ExitCode = cmd.ExitCode;
                    result.Success = cmd.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StdErr = ex.Message;
            }

            return result;
        }

        public void Dispose()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                }
                catch { }
                _process.Close();
                _process.Dispose();
                _process = null;
            }
        }

        ~FastbootCommand()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Fastboot 命令执行结果
    /// </summary>
    public class FastbootResult
    {
        public bool Success { get; set; }
        public string StdOut { get; set; } = "";
        public string StdErr { get; set; } = "";
        public int ExitCode { get; set; }

        /// <summary>
        /// 获取所有输出（stdout + stderr）
        /// </summary>
        public string AllOutput => string.IsNullOrEmpty(StdOut) ? StdErr : $"{StdOut}\n{StdErr}";
    }
}
