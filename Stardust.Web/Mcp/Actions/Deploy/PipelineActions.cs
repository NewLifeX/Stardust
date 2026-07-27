using System.Text.Json;
using NewLife;
using Stardust.Data.Deployment;
using Stardust.Web.Services;

namespace Stardust.Web.Mcp.Actions.Deploy;

/// <summary>触发流水线运行。通过PipelineService.Trigger创建运行记录并异步执行编译→部署</summary>
public class PipelineTriggerAction : McpActionBase
{
    private readonly PipelineService _pipelineService;

    /// <summary>构造函数注入PipelineService</summary>
    public PipelineTriggerAction(PipelineService pipelineService) => _pipelineService = pipelineService;

    public override String Name => "pipeline_trigger";
    public override String Description => "触发指定流水线的运行（创建运行记录，异步执行编译→上传→部署编排）。需要Token已授权该流水线所属项目。";
    public override String Module => "deploy";

    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "project",
        Field = "pipeline_id",
        Indirect = true,
        IndirectEntity = "AppPipeline",
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "pipeline_id": {"type": "integer", "description": "流水线ID"}
              },
              "required": ["pipeline_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override async Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var pipelineId = GetInt32(@params, "pipeline_id");
        if (pipelineId <= 0) throw new McpException(-32602, "Invalid params: pipeline_id must be a positive integer");

        var run = await _pipelineService.Trigger(pipelineId, context.CallerIp);

        return new
        {
            run_id = run.Id,
            pipeline_id = run.PipelineId,
            status = run.Status.ToString(),
            trigger_source = run.TriggerSource,
            branch = run.Branch,
            create_time = run.CreateTime,
        };
    }
}

/// <summary>查询流水线运行详情。返回运行状态、提交信息、各阶段时间戳及步骤列表</summary>
public class PipelineGetRunAction : McpActionBase
{
    public override String Name => "pipeline_get_run";
    public override String Description => "按运行ID查询流水线运行详情（状态、提交信息、阶段时间戳、步骤列表）。需要Token已授权该运行所属流水线的项目。";
    public override String Module => "deploy";

    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "project",
        Field = "run_id",
        Indirect = true,
        IndirectEntity = "AppPipelineRun",
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "run_id": {"type": "integer", "description": "运行记录ID（Int64雪花ID）"}
              },
              "required": ["run_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var runId = GetInt32(@params, "run_id");
        if (runId <= 0) throw new McpException(-32602, "Invalid params: run_id must be a positive integer");

        var run = AppPipelineRun.FindById(runId);
        if (run == null) throw new McpException(-32601, $"PipelineRun not found: id={runId}");

        // 查询该运行的所有步骤
        var steps = AppPipelineStep.FindAll(AppPipelineStep._.RunId == runId);
        var stepRecords = steps.OrderBy(s => s.StepIndex).Select(s => new
        {
            id = s.Id,
            step_type = s.StepType,
            step_index = s.StepIndex,
            node_id = s.NodeId,
            status = s.Status,
            message = s.Message,
            started_time = s.StartedTime,
            finished_time = s.FinishedTime,
        }).ToList();

        return Task.FromResult<Object>(new
        {
            run_id = run.Id,
            pipeline_id = run.PipelineId,
            status = run.Status.ToString(),
            trigger_source = run.TriggerSource,
            commit_id = run.CommitId,
            commit_message = run.CommitMessage,
            commit_author = run.CommitAuthor,
            commit_time = run.CommitTime,
            branch = run.Branch,
            build_node_id = run.BuildNodeId,
            app_version_id = run.AppVersionId,
            build_started_time = run.BuildStartedTime,
            build_finished_time = run.BuildFinishedTime,
            deploy_started_time = run.DeployStartedTime,
            deploy_finished_time = run.DeployFinishedTime,
            trace_id = run.TraceId,
            remark = run.Remark,
            create_time = run.CreateTime,
            steps = stepRecords,
        });
    }
}

/// <summary>取消运行中的流水线。校验状态后置为Cancelled，并中断后续步骤</summary>
public class PipelineCancelAction : McpActionBase
{
    private readonly PipelineService _pipelineService;

    /// <summary>构造函数注入PipelineService</summary>
    public PipelineCancelAction(PipelineService pipelineService) => _pipelineService = pipelineService;

    public override String Name => "pipeline_cancel";
    public override String Description => "取消运行中的流水线（Pending/Building/UploadSucceeded/Deploying 状态可取消，终态不可取消）。需要Token已授权该运行所属流水线的项目。";
    public override String Module => "deploy";

    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "project",
        Field = "run_id",
        Indirect = true,
        IndirectEntity = "AppPipelineRun",
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "run_id": {"type": "integer", "description": "运行记录ID"}
              },
              "required": ["run_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var runId = GetInt32(@params, "run_id");
        if (runId <= 0) throw new McpException(-32602, "Invalid params: run_id must be a positive integer");

        var success = _pipelineService.Cancel(runId, context.CallerIp);

        return Task.FromResult<Object>(new
        {
            run_id = runId,
            cancelled = success,
            message = success ? "运行已取消" : "运行已处于终态，无法取消",
        });
    }
}
