using NewLife;
using NewLife.Remoting.Clients;

namespace StarAgent;

/// <summary>星尘代理升级。兼容 any（托管包）与 AOT/自包含（原生包）互相覆盖。</summary>
/// <remarks>
/// 基类 Standard 模式只备份 <c>*.exe;*.dll</c> 与 runtimeconfig/deps，Linux 上 AOT 入口是无扩展名的
/// <c>StarAgent</c>，从 AOT 切回 any 时不会被备份，导致冒烟失败后无法回滚。这里在基类更新前把它改名备份，
/// 成功后保留 <c>.del</c> 供 <see cref="Upgrade.Rollback"/> 恢复；失败则由本类立即恢复。
/// </remarks>
internal class StarUpgrade : Upgrade
{
    /// <summary>测试用平台覆盖；为空时按运行时平台判断</summary>
    internal Boolean? ForceLinux { get; set; }

    /// <summary>执行更新。先备份 Linux 原生入口，再交给基类覆盖文件。</summary>
    /// <returns>是否成功</returns>
    /// <remarks>
    /// 基类 <c>Update</c> 开头会删除目标目录内所有 <c>*.del</c>，因此备份不能直接用 <c>.del</c> 后缀，
    /// 否则会被基类当场删掉。这里先改名为 <c>.old</c>，更新成功后再提升为 <c>.del</c>，交给基类
    /// <c>Rollback</c> 在冒烟失败时恢复。
    /// </remarks>
    public override Boolean Update()
    {
        var backup = BackupNativeEntry(DestinationPath, TempPath, ForceLinux ?? Runtime.Linux);
        if (backup != null) WriteLog("MoveTo {0}", backup);

        Boolean rs;
        try
        {
            rs = base.Update();
        }
        catch
        {
            if (RestoreNativeEntry(backup, DestinationPath)) WriteLog("Restore {0}", Path.Combine(DestinationPath!, "StarAgent"));
            throw;
        }

        if (rs)
        {
            var del = PromoteBackup(backup, DestinationPath);
            if (del != null) WriteLog("Backup {0}", del);
        }
        else if (RestoreNativeEntry(backup, DestinationPath))
            WriteLog("Restore {0}", Path.Combine(DestinationPath!, "StarAgent"));

        return rs;
    }

    /// <summary>备份 Linux 原生入口 StarAgent</summary>
    /// <param name="dest">部署目录</param>
    /// <param name="tmp">新包解压目录</param>
    /// <param name="isLinux">是否 Linux；其它平台由基类备份 exe</param>
    /// <returns>备份文件路径，未备份时为空</returns>
    internal static String? BackupNativeEntry(String? dest, String? tmp, Boolean isLinux)
    {
        if (!isLinux || dest.IsNullOrEmpty() || tmp.IsNullOrEmpty()) return null;

        var native = Path.Combine(dest, "StarAgent");
        if (!File.Exists(native)) return null;

        // 新包既可能是原生入口，也可能是托管 DLL；两者都会让旧原生入口失效
        var newNative = Path.Combine(tmp, "StarAgent");
        var newDll = Path.Combine(tmp, "StarAgent.dll");
        if (!File.Exists(newNative) && !File.Exists(newDll)) return null;

        // 不能用 .del：基类 Update 开头会删除目标目录内所有 .del
        var backup = native + ".old";
        if (File.Exists(backup)) backup = $"{native}_{DateTime.Now:yyMMddHHmmss}.old";

        try
        {
            File.Move(native, backup);

            return backup;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>更新成功后把 .old 提升为 .del，供基类 Rollback 在冒烟失败时恢复</summary>
    /// <param name="backup">备份文件路径</param>
    /// <param name="dest">部署目录</param>
    /// <returns>提升后的 .del 路径，失败时为空</returns>
    internal static String? PromoteBackup(String? backup, String? dest)
    {
        if (backup.IsNullOrEmpty() || dest.IsNullOrEmpty() || !File.Exists(backup)) return null;

        var native = Path.Combine(dest, "StarAgent");
        var del = native + ".del";
        if (File.Exists(del)) del = $"{native}_{DateTime.Now:yyMMddHHmmss}.del";

        try
        {
            File.Move(backup, del);

            return del;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>更新失败时恢复原生入口</summary>
    /// <param name="backup">备份文件路径</param>
    /// <param name="dest">部署目录</param>
    /// <returns>是否恢复成功</returns>
    internal static Boolean RestoreNativeEntry(String? backup, String? dest)
    {
        if (backup.IsNullOrEmpty() || dest.IsNullOrEmpty() || !File.Exists(backup)) return false;

        var native = Path.Combine(dest, "StarAgent");
        try
        {
            if (File.Exists(native)) File.Delete(native);
            File.Move(backup, native);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
