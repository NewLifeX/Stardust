using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Threading;
using Renci.SshNet;
using Stardust;
using Stardust.Managers;
using Stardust.Models;

namespace AgentExpansion;

internal sealed class AgentExpansionService
{
    private static readonly HttpClient _httpClient = new();
    private TimerX? _timer;
    private Int32 _running;
    private String? _packageFile;
    private String? _packageUrl;
    private readonly SemaphoreSlim _packageLock = new(1, 1);

    public ILog Log { get; set; } = Logger.Null;

    public void Start()
    {
        if (_timer != null) return;

        var set = AgentExpansionSetting.Current;
        var period = set.Period;
        if (period <= 0) period = 3600;

        _timer = new TimerX(DoWork, null, 1000, period * 1000) { Async = true };
        AgentExpansionSetting.Provider!.Changed += OnSettingChanged;
    }

    public void Stop(String reason)
    {
        _timer.TryDispose();
        _timer = null;

        AgentExpansionSetting.Provider!.Changed -= OnSettingChanged;
    }

    public void RunOnce() => ScanAsync(CancellationToken.None).GetAwaiter().GetResult();

    private void OnSettingChanged(Object? sender, EventArgs eventArgs)
    {
        _timer?.SetNext(-1);
    }

    private void DoWork(Object? state)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await ScanAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        });
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        var set = AgentExpansionSetting.Current;
        if (!set.Enable) return;

        var server = StarSetting.Current.Server;
        if (server.IsNullOrEmpty())
        {
            Log.Info("未配置StarServer，跳过自动安装");
            return;
        }

        var local = AgentInfo.GetLocal(false);
        local.Server = server;

        var targets = GetTargets(set).ToList();
        if (targets.Count == 0) return;

        var localIps = GetLocalIpSet();
        var semaphore = new SemaphoreSlim(Math.Max(1, set.MaxConcurrent));
        var tasks = new List<Task>();

        foreach (var address in targets)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var text = address.ToString();
            if (localIps.Contains(text)) continue;

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await HandleHostAsync(address, set, local, server, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task HandleHostAsync(IPAddress address, AgentExpansionSetting set, AgentInfo local, String server, CancellationToken cancellationToken)
    {
        if (await HasStarAgentAsync(address, local, set.Timeout).ConfigureAwait(false)) return;

        if (await TryInstallBySshAsync(address, set, server, cancellationToken).ConfigureAwait(false)) return;

        await TryInstallByTelnetAsync(address, set, server, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Boolean> HasStarAgentAsync(IPAddress address, AgentInfo local, Int32 timeout)
    {
        try
        {
            using var client = new ApiClient($"udp://{address}:{LocalStarClient.Port}")
            {
                Timeout = timeout,
                Log = Log,
            };

            var info = await client.InvokeAsync<AgentInfo>("Info", local).ConfigureAwait(false);
            if (info != null)
            {
                Log.Info("节点 {0} 已存在StarAgent({1})", address, info.Version);
                return true;
            }
        }
        catch (TimeoutException) { }
        catch (Exception ex)
        {
            Log.Debug("{0} 探测失败：{1}", address, ex.Message);
        }

        return false;
    }

    private async Task<Boolean> TryInstallBySshAsync(IPAddress address, AgentExpansionSetting set, String server, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = ResolveCredential(set.UserName);
        var password = ResolveCredential(set.Password);
        if (user.IsNullOrEmpty() || password.IsNullOrEmpty()) return false;

        var packageFile = await PreparePackageAsync(set, cancellationToken).ConfigureAwait(false);
        if (packageFile.IsNullOrEmpty()) return false;

        var targetPath = GetTargetPath(set, true);
        var remoteFile = $"/tmp/{Path.GetFileName(packageFile)}";

        try
        {
            using var client = new SshClient(address.ToString(), set.SshPort, user, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromMilliseconds(set.Timeout);
            var expectedKey = NormalizeHostKey(set.SshHostKey);
            client.HostKeyReceived += (sender, args) =>
            {
                if (expectedKey.IsNullOrEmpty())
                {
                    Log.Info("节点 {0} 未配置SSH指纹，跳过校验", address);
                    args.CanTrust = true;
                    return;
                }

                var actual = NormalizeHostKey(BitConverter.ToString(args.FingerPrint));
                args.CanTrust = actual.EqualIgnoreCase(expectedKey);
                if (!args.CanTrust)
                    Log.Info("节点 {0} SSH指纹不匹配，期望 {1} 实际 {2}", address, expectedKey, actual);
            };
            client.Connect();
            if (!client.IsConnected) return false;

            using (var sftp = new SftpClient(client.ConnectionInfo))
            {
                sftp.Connect();
                using var stream = File.OpenRead(packageFile);
                sftp.UploadFile(stream, remoteFile, true);
            }

            var command = BuildInstallCommand(remoteFile, targetPath, server);
            var result = client.RunCommand(command);
            if (result.ExitStatus != 0)
                Log.Info("节点 {0} 执行安装命令返回：{1}", address, result.Error);
            else
                Log.Info("节点 {0} 已发送安装命令", address);

            client.Disconnect();
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("{0} SSH失败：{1}", address, ex.Message);
            return false;
        }
    }

    private async Task<Boolean> TryInstallByTelnetAsync(IPAddress address, AgentExpansionSetting set, String server, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = ResolveCredential(set.UserName);
        var password = ResolveCredential(set.Password);
        if (user.IsNullOrEmpty() || password.IsNullOrEmpty()) return false;

        var url = BuildPackageUrl(set);
        if (url.IsNullOrEmpty()) return false;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && set.PackageSha512.IsNullOrEmpty())
        {
            Log.Info("节点 {0} Telnet下载需HTTPS或配置PackageSha512", address);
            return false;
        }
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            Log.Info("节点 {0} Telnet使用HTTP下载存在风险，请评估网络安全", address);

        try
        {
            using var session = await TelnetSession.ConnectAsync(address.ToString(), set.TelnetPort, set.Timeout, cancellationToken).ConfigureAwait(false);
            if (!await session.LoginAsync(user, password, set.Timeout, cancellationToken).ConfigureAwait(false)) return false;

            var targetPath = GetTargetPath(set, true);
            var command = BuildTelnetInstallCommand(url, targetPath, server, set.PackageSha512);
            await session.WriteLineAsync(command, cancellationToken).ConfigureAwait(false);
            await session.ReadAsync(set.Timeout, cancellationToken).ConfigureAwait(false);

            Log.Info("节点 {0} 已发送Telnet安装命令", address);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("{0} Telnet失败：{1}", address, ex.Message);
            return false;
        }
    }

    private async Task<String?> PreparePackageAsync(AgentExpansionSetting set, CancellationToken cancellationToken)
    {
        var url = BuildPackageUrl(set);
        if (url.IsNullOrEmpty()) return null;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && set.PackageSha512.IsNullOrEmpty())
        {
            Log.Info("安装包下载需HTTPS或配置PackageSha512");
            return null;
        }
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            Log.Info("安装包使用HTTP下载存在风险，请评估网络安全");

        await _packageLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_packageFile != null && _packageUrl == url && File.Exists(_packageFile)) return _packageFile;

            var fileName = GetFileName(url);
            var file = Path.Combine(Path.GetTempPath(), fileName);
            if (!File.Exists(file))
            {
                await _httpClient.DownloadFileAsync(url, file).ConfigureAwait(false);
            }

            if (!set.PackageSha512.IsNullOrEmpty())
            {
                var expected = set.PackageSha512.Replace("-", String.Empty).Trim();
                var actual = NetRuntime.GetSHA512(file);
                if (!actual.EqualIgnoreCase(expected))
                {
                    Log.Info("安装包哈希校验失败，期望 {0} 实际 {1}", expected, actual);
                    File.Delete(file);
                    return null;
                }
            }

            _packageFile = file;
            _packageUrl = url;

            return file;
        }
        finally
        {
            _packageLock.Release();
        }
    }

    private static String GetFileName(String url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.AbsolutePath);
            if (!name.IsNullOrEmpty()) return name;
        }

        return "staragent.zip";
    }

    private static String? BuildPackageUrl(AgentExpansionSetting set)
    {
        if (!set.PackageUrl.IsNullOrEmpty()) return set.PackageUrl;

        var baseUrl = NewLife.Setting.Current.PluginServer;
        if (baseUrl.IsNullOrEmpty()) return null;

        var url = baseUrl.EnsureEnd("/") + "star/";
        var major = Environment.Version.Major;
        if (major >= 8)
            url += "staragent80.zip";
        else if (major >= 7)
            url += "staragent70.zip";
        else if (major >= 6)
            url += "staragent60.zip";
        else if (major >= 5)
            url += "staragent50.zip";
        else if (major >= 4)
            url += "staragent45.zip";
        else
            url += "staragent31.zip";

        return url;
    }

    private static String GetTargetPath(AgentExpansionSetting set, Boolean isLinux)
    {
        if (!set.TargetPath.IsNullOrEmpty()) return set.TargetPath;

        return isLinux ? "/root/staragent" : "C:\\StarAgent";
    }

    private static String BuildInstallCommand(String packageFile, String targetPath, String server)
    {
        var serverArg = server.Replace("\"", "\\\"");
        var target = QuoteShell(targetPath);
        var package = QuoteShell(packageFile);
        var agentFile = QuoteShell($"{targetPath.TrimEnd('/')}/StarAgent");
        var agentDll = QuoteShell($"{targetPath.TrimEnd('/')}/StarAgent.dll");

        return $"mkdir -p {target} && unzip -o {package} -d {target} && chmod +x {agentFile} && " +
            $"if [ -f {agentFile} ]; then {agentFile} -server \"{serverArg}\" -run; " +
            $"elif [ -f {agentDll} ]; then dotnet {agentDll} -server \"{serverArg}\" -run; fi";
    }

    private static String BuildTelnetInstallCommand(String url, String targetPath, String server, String? sha512)
    {
        var serverArg = server.Replace("\"", "\\\"");
        var target = QuoteShell(targetPath);
        var package = QuoteShell($"/tmp/{GetFileName(url)}");
        var agentFile = QuoteShell($"{targetPath.TrimEnd('/')}/StarAgent");
        var agentDll = QuoteShell($"{targetPath.TrimEnd('/')}/StarAgent.dll");
        var verify = "";
        if (!sha512.IsNullOrEmpty())
        {
            var hash = sha512.Replace("-", String.Empty).Trim();
            verify = $"(command -v sha512sum >/dev/null 2>&1 && echo \"{hash}  {package}\" | sha512sum -c -) || " +
                $"(echo \"sha512sum not found\" && false) && ";
        }

        return $"rm -f {package}; " +
            $"(command -v curl >/dev/null 2>&1 && curl -L -o {package} {QuoteShell(url)}) || " +
            $"(command -v wget >/dev/null 2>&1 && wget -O {package} {QuoteShell(url)}); " +
            $"{verify}mkdir -p {target} && unzip -o {package} -d {target} && chmod +x {agentFile} && " +
            $"if [ -f {agentFile} ]; then {agentFile} -server \"{serverArg}\" -run; " +
            $"elif [ -f {agentDll} ]; then dotnet {agentDll} -server \"{serverArg}\" -run; fi";
    }

    private static String NormalizeHostKey(String? value)
    {
        if (value.IsNullOrEmpty()) return String.Empty;

        return value.Replace(":", String.Empty).Replace("-", String.Empty).Trim();
    }

    private static String? ResolveCredential(String? value)
    {
        if (value.IsNullOrEmpty()) return value;

        if (value.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var key = value["env:".Length..].Trim();
            if (key.IsNullOrEmpty()) return value;
            return Environment.GetEnvironmentVariable(key);
        }

        return value;
    }

    private static String QuoteShell(String value)
    {
        if (value.IsNullOrEmpty()) return value;

        if (!value.Contains(' ') && !value.Contains('"')) return value;

        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static HashSet<String> GetLocalIpSet()
    {
        var ips = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var localIps = AgentInfo.GetIps();
        if (!localIps.IsNullOrEmpty())
        {
            foreach (var item in localIps.Split(',', ';'))
            {
                var text = item.Trim();
                if (!text.IsNullOrEmpty()) ips.Add(text);
            }
        }

        return ips;
    }

    private static IEnumerable<IPAddress> GetTargets(AgentExpansionSetting set)
    {
        var ranges = ParseNetworks(set.Networks);
        if (ranges.Count == 0) ranges = GetLocalRanges();

        var maxHosts = set.MaxHosts;
        var count = 0;
        foreach (var range in ranges)
        {
            foreach (var address in range.GetAddresses())
            {
                yield return address;
                count++;
                if (maxHosts > 0 && count >= maxHosts) yield break;
            }
        }
    }

    private static List<NetworkRange> ParseNetworks(String? text)
    {
        var list = new List<NetworkRange>();
        if (text.IsNullOrEmpty()) return list;

        var parts = text.Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (TryParseCidr(part, out var range) ||
                TryParseWildcard(part, out range) ||
                TryParseSingle(part, out range))
                list.Add(range);
        }

        return list;
    }

    private static List<NetworkRange> GetLocalRanges()
    {
        var list = new List<NetworkRange>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var props = nic.GetIPProperties();
            if (props == null) continue;

            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;
                if (unicast.IPv4Mask == null) continue;

                var prefix = GetPrefixLength(unicast.IPv4Mask);
                if (prefix <= 0) continue;

                list.Add(NetworkRange.FromCidr(unicast.Address, prefix));
            }
        }

        return list;
    }

    private static Int32 GetPrefixLength(IPAddress mask)
    {
        var bytes = mask.GetAddressBytes();
        var count = 0;
        foreach (var item in bytes)
        {
            var value = item;
            for (var i = 0; i < 8; i++)
            {
                if ((value & 0x80) == 0x80) count++;
                value <<= 1;
            }
        }

        return count;
    }

    private static Boolean TryParseSingle(String text, out NetworkRange range)
    {
        range = default;
        if (!IPAddress.TryParse(text, out var address)) return false;

        range = NetworkRange.FromSingle(address);
        return true;
    }

    private static Boolean TryParseCidr(String text, out NetworkRange range)
    {
        range = default;
        var p = text.IndexOf('/');
        if (p <= 0) return false;

        if (!IPAddress.TryParse(text[..p], out var address)) return false;
        if (!Int32.TryParse(text[(p + 1)..], out var prefix)) return false;
        if (prefix < 0 || prefix > 32) return false;

        range = NetworkRange.FromCidr(address, prefix);
        return true;
    }

    private static Boolean TryParseWildcard(String text, out NetworkRange range)
    {
        range = default;
        if (!text.Contains('*')) return false;

        var parts = text.Split('.');
        if (parts.Length != 4) return false;

        var startBytes = new Byte[4];
        var endBytes = new Byte[4];

        for (var i = 0; i < 4; i++)
        {
            if (parts[i] == "*")
            {
                startBytes[i] = 0;
                endBytes[i] = 255;
            }
            else if (Byte.TryParse(parts[i], out var value))
            {
                startBytes[i] = value;
                endBytes[i] = value;
            }
            else
            {
                return false;
            }
        }

        range = new NetworkRange(new IPAddress(startBytes), new IPAddress(endBytes));
        return true;
    }
}

internal readonly struct NetworkRange
{
    public NetworkRange(IPAddress start, IPAddress end)
    {
        Start = start;
        End = end;
    }

    public IPAddress Start { get; }
    public IPAddress End { get; }

    public IEnumerable<IPAddress> GetAddresses()
    {
        var start = ToUInt32(Start);
        var end = ToUInt32(End);
        if (end < start) yield break;

        for (var i = start; i <= end; i++)
        {
            yield return ToIPAddress(i);

            if (i == UInt32.MaxValue) break;
        }
    }

    public static NetworkRange FromSingle(IPAddress address) => new(address, address);

    public static NetworkRange FromCidr(IPAddress address, Int32 prefix)
    {
        var ip = ToUInt32(address);
        var mask = prefix == 0 ? 0U : UInt32.MaxValue << (32 - prefix);
        var network = ip & mask;
        var broadcast = network | ~mask;

        var start = prefix >= 31 ? network : network + 1;
        var end = prefix >= 31 ? broadcast : broadcast - 1;

        return new NetworkRange(ToIPAddress(start), ToIPAddress(end));
    }

    private static UInt32 ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static IPAddress ToIPAddress(UInt32 value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return new IPAddress(bytes);
    }
}

internal sealed class TelnetSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Encoding _encoding = Encoding.ASCII;
    private static readonly String[] _userPrompts = ["login", "username", "user"];
    private static readonly String[] _passwordPrompts = ["password", "pass"];
    private const Byte TelnetIac = 255;
    private Int32 _skipBytes;

    private TelnetSession(TcpClient client)
    {
        _client = client;
        _stream = _client.GetStream();
    }

    public static async Task<TelnetSession> ConnectAsync(String host, Int32 port, Int32 timeout, CancellationToken cancellationToken)
    {
        var client = new TcpClient
        {
            ReceiveTimeout = timeout,
            SendTimeout = timeout,
        };

        var task = client.ConnectAsync(host, port);
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(timeout, delayCts.Token);
        if (await Task.WhenAny(task, delayTask).ConfigureAwait(false) != task)
            throw new TimeoutException();
        delayCts.Cancel();
        await task.ConfigureAwait(false);

        return new TelnetSession(client);
    }

    public async Task<Boolean> LoginAsync(String user, String password, Int32 timeout, CancellationToken cancellationToken)
    {
        var greeting = await ReadAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!IsPromptMatch(greeting, _userPrompts))
            greeting += await ReadAsync(timeout, cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(user, cancellationToken).ConfigureAwait(false);

        var prompt = await ReadAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!IsPromptMatch(prompt, _passwordPrompts))
            prompt += await ReadAsync(timeout, cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(password, cancellationToken).ConfigureAwait(false);

        var result = await ReadAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (result.IsNullOrEmpty()) return true;

        return !result.Contains("failed", StringComparison.OrdinalIgnoreCase) &&
            !result.Contains("incorrect", StringComparison.OrdinalIgnoreCase);
    }

    private static Boolean IsPromptMatch(String text, IEnumerable<String> prompts)
    {
        if (text.IsNullOrEmpty()) return false;

        foreach (var item in prompts)
        {
            if (text.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    public Task WriteLineAsync(String command, CancellationToken cancellationToken)
    {
        var data = _encoding.GetBytes(command + "\r\n");
        return _stream.WriteAsync(data, 0, data.Length, cancellationToken);
    }

    public async Task<String> ReadAsync(Int32 timeout, CancellationToken cancellationToken)
    {
        var buffer = new Byte[1024];
        var sb = new StringBuilder();
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeout)
        {
            while (_stream.DataAvailable)
            {
                var count = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (count <= 0) break;

                for (var i = 0; i < count; i++)
                {
                    var value = buffer[i];
                    if (_skipBytes > 0)
                    {
                        _skipBytes--;
                        continue;
                    }
                    if (value == TelnetIac)
                    {
                        _skipBytes = 2;
                        continue;
                    }
                    sb.Append((Char)value);
                }
            }

            if (sb.Length > 0) break;
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Close();
    }
}
