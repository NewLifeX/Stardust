namespace Stardust.Models;

/// <summary>流水线运行状态</summary>
public enum PipelineStatus
{
    /// <summary>待处理。已创建 run 等待执行</summary>
    Pending = 0,

    /// <summary>编译中。已下发到编译节点，等待 git pull + build + pack + upload</summary>
    Building = 1,

    /// <summary>上传成功。编译节点已成功上传 zip，等待部署</summary>
    UploadSucceeded = 2,

    /// <summary>部署中。正在向部署节点 install</summary>
    Deploying = 3,

    /// <summary>成功。全部步骤完成</summary>
    Success = 4,

    /// <summary>失败。任意步骤失败</summary>
    Failed = 5,

    /// <summary>已取消</summary>
    Cancelled = 6,
}
