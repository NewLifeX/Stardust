using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Deployment;

/// <summary>恢复结果</summary>
public enum DeployRestoreResult
{
    /// <summary>没有可用的备份</summary>
    None,

    /// <summary>当前包已经是上次可用包，无需恢复</summary>
    AlreadyGood,

    /// <summary>已恢复备份包</summary>
    Restored,
}

/// <summary>部署备份存储</summary>
/// <remarks>
/// 目录结构：{Root}/{服务名}/下载的 zip + deploy.json。
/// zip 保留下载时的原始文件名；deploy.json 保存服务端下发的 DeployInfo。
/// 每个服务只保留一个上次稳定运行的包，不维护多版本历史。
/// </remarks>
public class DeployBackupStore
{
    #region 属性
    /// <summary>同步根。下载覆盖、备份提升与回滚恢复共用，避免并发操作同一个 zip</summary>
    public static Object SyncRoot { get; } = new();

    /// <summary>备份根目录</summary>
    public String Root { get; set; } = null!;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化备份存储</summary>
    /// <param name="root">备份根目录</param>
    public DeployBackupStore(String root) => Root = root;
    #endregion

    #region 目录
    /// <summary>获取服务备份目录</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>备份目录</returns>
    public virtual String GetBackupDir(String serviceName) => Path.Combine(GetRoot(), GetSafeName(serviceName));

    /// <summary>获取 DeployInfo 文件路径</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>deploy.json 路径</returns>
    public virtual String GetDeployFile(String serviceName) => Path.Combine(GetBackupDir(serviceName), "deploy.json");

    /// <summary>获取备份 zip 文件</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>备份 zip 路径，未找到返回 null</returns>
    public virtual String? GetBackupZip(String serviceName)
    {
        var dir = GetBackupDir(serviceName);
        if (!Directory.Exists(dir)) return null;

        return Directory.GetFiles(dir, "*.zip")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }

    private String GetRoot()
    {
        var root = Root;
        if (root.IsNullOrEmpty()) root = Setting.Current.BackupPath;
        if (root.IsNullOrEmpty()) root = "Backup";

        return root.GetBasePath();
    }

    private static String GetSafeName(String serviceName)
    {
        var name = serviceName;
        if (name.IsNullOrEmpty()) return "_";

        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(ch, '_');
        }

        return name.Replace('/', '_').Replace('\\', '_').Replace("..", "__");
    }
    #endregion

    #region 提升与恢复
    /// <summary>把稳定运行过的包保存为回滚包</summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="zipFile">当前 zip 文件路径，文件名沿用下载时的名字</param>
    /// <param name="deploy">服务端下发的部署信息，写入 deploy.json</param>
    /// <returns>是否保存成功</returns>
    public virtual Boolean Promote(String serviceName, String zipFile, DeployInfo? deploy)
    {
        if (zipFile.IsNullOrEmpty() || !File.Exists(zipFile)) return false;
        if (deploy == null || deploy.Hash.IsNullOrEmpty()) return false;

        var hash = GetFileHash(zipFile);
        if (hash.IsNullOrEmpty() || !hash.EqualIgnoreCase(deploy.Hash))
        {
            Log.Warn("Skip saving deploy backup for [{0}]: hash mismatch {1} != {2}", serviceName, hash, deploy.Hash);
            return false;
        }

        using var span = Tracer?.NewSpan("DeployBackup-Promote", new { serviceName, zipFile });

        lock (SyncRoot)
        {
            try
            {
                var dir = GetBackupDir(serviceName);
                Directory.CreateDirectory(dir);

                // 先复制到临时文件，成功后再清理旧包，避免复制失败把唯一备份删掉
                var name = Path.GetFileName(zipFile);
                var temp = Path.Combine(dir, "backup.zip.tmp");
                File.Copy(zipFile, temp, true);

                foreach (var file in Directory.GetFiles(dir, "*.zip"))
                {
                    TryDelete(file);
                }

                var target = Path.Combine(dir, name);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temp, target);

                var stateFile = GetDeployFile(serviceName);
                var tmp = stateFile + ".tmp";
                File.WriteAllText(tmp, deploy.ToJson());
                if (File.Exists(stateFile)) File.Delete(stateFile);
                File.Move(tmp, stateFile);

                Log.Info("Saved deploy backup for [{0}] version [{1}] to {2}", serviceName, deploy.Version, dir);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to save deploy backup for [{0}]: {1}", serviceName, ex.Message);
                return false;
            }
        }
    }

    /// <summary>加载服务端下发的部署信息</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>部署信息，未找到或解析失败返回 null</returns>
    public virtual DeployInfo? LoadDeploy(String serviceName)
    {
        lock (SyncRoot)
        {
            var file = GetDeployFile(serviceName);
            if (!File.Exists(file)) return null;

            try
            {
                return File.ReadAllText(file).ToJsonEntity<DeployInfo>();
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to load deploy backup info [{0}]: {1}", file, ex.Message);
                return null;
            }
        }
    }

    /// <summary>恢复上次可用包</summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="currentZip">当前 zip 文件路径，恢复时会被覆盖</param>
    /// <param name="deploy">恢复使用的部署信息</param>
    /// <returns>恢复结果</returns>
    public virtual DeployRestoreResult Restore(String serviceName, String currentZip, out DeployInfo? deploy)
    {
        deploy = null;

        if (currentZip.IsNullOrEmpty()) return DeployRestoreResult.None;

        lock (SyncRoot)
        {
            try
            {
                deploy = LoadDeploy(serviceName);
                if (deploy == null) return DeployRestoreResult.None;

                var backupZip = GetBackupZip(serviceName);
                if (backupZip.IsNullOrEmpty() || !File.Exists(backupZip)) return DeployRestoreResult.None;

                // 备份包与 DeployInfo 不一致时拒绝恢复，避免把未知包当成上次可用包
                var backupHash = GetFileHash(backupZip);
                if (backupHash.IsNullOrEmpty() || deploy.Hash.IsNullOrEmpty() || !backupHash.EqualIgnoreCase(deploy.Hash))
                {
                    Log.Warn("Deploy backup hash mismatch for [{0}]: {1} != {2}", serviceName, backupHash, deploy.Hash);
                    return DeployRestoreResult.None;
                }

                // 当前包已经是上次可用包，说明失败更可能是环境问题，不再回滚
                var currentHash = File.Exists(currentZip) ? GetFileHash(currentZip) : null;
                if (!deploy.Hash.IsNullOrEmpty() && !currentHash.IsNullOrEmpty() && currentHash.EqualIgnoreCase(deploy.Hash))
                    return DeployRestoreResult.AlreadyGood;

                if (!SafeCopy(backupZip, currentZip)) return DeployRestoreResult.None;

                Log.Info("Restored deploy backup for [{0}] version [{1}] to {2}", serviceName, deploy.Version, currentZip);
                return DeployRestoreResult.Restored;
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to restore deploy backup for [{0}]: {1}", serviceName, ex.Message);
                return DeployRestoreResult.None;
            }
        }
    }
    #endregion

    #region 清理
    /// <summary>清理孤儿备份目录和临时文件</summary>
    /// <param name="activeNames">仍在使用的服务名称集合。为空时不删除任何服务目录</param>
    /// <param name="maxAge">目录的最大保留时长</param>
    /// <returns>清理的目录数</returns>
    public virtual Int32 Cleanup(IEnumerable<String>? activeNames, TimeSpan maxAge)
    {
        var root = GetRoot();
        if (!Directory.Exists(root)) return 0;

        var names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        if (activeNames != null)
        {
            foreach (var item in activeNames)
            {
                if (!item.IsNullOrEmpty()) names.Add(GetSafeName(item));
            }
        }

        var count = 0;
        var expire = DateTime.Now - maxAge;

        lock (SyncRoot)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var name = Path.GetFileName(dir);
                    if (name.IsNullOrEmpty()) continue;

                    // 只清理已不在服务列表中、且超过保留期的目录，避免误删仍在使用的备份
                    if (names.Count == 0 || names.Contains(name)) continue;
                    if (Directory.GetLastWriteTime(dir) > expire) continue;

                    try
                    {
                        Directory.Delete(dir, true);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Failed to clean deploy backup [{0}]: {1}", dir, ex.Message);
                    }
                }

                // 临时文件只需保留一天，避免上次异常留下的半成品长期占用空间
                var tmpExpire = DateTime.Now.AddDays(-1);
                foreach (var file in Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) <= tmpExpire) File.Delete(file);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to clean deploy backups under [{0}]: {1}", root, ex.Message);
            }
        }

        return count;
    }
    #endregion

    #region 辅助
    /// <summary>获取文件 MD5</summary>
    /// <param name="file">文件路径</param>
    /// <returns>MD5 十六进制字符串，文件不存在返回 null</returns>
    public static String? GetFileHash(String file)
    {
        if (file.IsNullOrEmpty()) return null;

        var fi = file.AsFile();
        if (fi == null || !fi.Exists) return null;

        return fi.MD5().ToHex();
    }

    private static Boolean SafeCopy(String source, String target)
    {
        if (source.IsNullOrEmpty() || target.IsNullOrEmpty()) return false;
        if (!File.Exists(source)) return false;

        var tmp = target + ".tmp";
        var bak = target + ".del";
        try
        {
            target.EnsureDirectory(true);
            File.Copy(source, tmp, true);

            if (File.Exists(bak)) File.Delete(bak);
            if (File.Exists(target)) File.Move(target, bak);

            File.Move(tmp, target);

            TryDelete(bak);
            return true;
        }
        catch
        {
            TryDelete(tmp);
            if (!File.Exists(target) && File.Exists(bak)) File.Move(bak, target);
            throw;
        }
    }

    private static void TryDelete(String file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
    }
    #endregion
}
