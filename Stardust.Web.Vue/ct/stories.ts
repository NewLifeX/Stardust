// 聚合各组件的 story 变体：供 gallery 渲染、Playwright CT 截图。
//
// 为什么用「显式静态导入」而不是 import.meta.glob 自动收集？
//   glob 在 CT 的 vite（HMR 活跃）环境下会为 story 模块附加 ?t= 查询串，
//   导致模块图不稳定、偶发漏注册（表现为部分 story 的 id 不在 gallery 的 registry 中，
//   mountStory 报「未知 story」）。该 flaky 在沙箱/CI 极难排查。
//   显式导入确定性最高；新增组件 story 只需在此处追加一行 import 即可。
import { stories as navSegmentStories } from '../src/layouts/StardustLayout2/NavSegment.story';
import { stories as kIconStories } from '../src/layouts/StardustLayout2/KIcon.story';
import { stories as topBarStories } from '../src/layouts/StardustLayout2/TopBar.story';
import { stories as commandPaletteStories } from '../src/layouts/StardustLayout2/CommandPalette/CommandPalette.story';
import { stories as drawerStories } from '../src/layouts/StardustLayout2/Drawer/Drawer.story';
import { stories as btnStories } from '../src/layouts/StardustLayout2/Btn.story';
import { stories as guardModalStories } from '../src/layouts/StardustLayout2/GuardModal/GuardModal.story';
import { stories as dotStories } from '../src/layouts/StardustLayout2/Dot.story';
import { stories as chipStories } from '../src/layouts/StardustLayout2/Chip.story';
import { stories as appTabsStories } from '../src/layouts/StardustLayout2/AppTabs.story';
import { stories as deployVersionBarStories } from '../src/layouts/StardustLayout2/DeployVersionBar.story';
import { stories as deployVersionListStories } from '../src/layouts/StardustLayout2/DeployVersionList.story';
import { stories as deployNodesStories } from '../src/layouts/StardustLayout2/DeployNodes.story';
import { stories as appLovStories } from '../src/layouts/StardustLayout2/AppLov.story';
import { stories as nodeLovStories } from '../src/layouts/StardustLayout2/NodeLov.story';
import { stories as buildNodesStories } from '../src/layouts/StardustLayout2/BuildNodes.story';
import { stories as deployHistoryStories } from '../src/layouts/StardustLayout2/DeployHistory.story';
import { stories as deployPipelineRunsStories } from '../src/layouts/StardustLayout2/DeployPipelineRuns.story';
import { stories as deployFeedbackStripStories } from '../src/layouts/StardustLayout2/DeployFeedbackStrip.story';
import { stories as attentionListStories } from '../src/layouts/StardustLayout2/AttentionList.story';
import { stories as traceListStories } from '../src/layouts/StardustLayout2/TraceList.story';
import { stories as appLogStories } from '../src/layouts/StardustLayout2/AppLog.story';
import { stories as appConfigTableStories } from '../src/layouts/StardustLayout2/AppConfigTable.story';
import { stories as appInstancesStories } from '../src/layouts/StardustLayout2/AppInstances.story';
import { stories as appObserveStories } from '../src/layouts/StardustLayout2/AppObserve.story';
import { stories as changesListStories } from '../src/layouts/StardustLayout2/ChangesList.story';
import { stories as registryTabStories } from '../src/layouts/StardustLayout2/RegistryTab.story';
import { stories as gatewayTabStories } from '../src/layouts/StardustLayout2/GatewayTab.story';
import { stories as gatewayClusterStories } from '../src/layouts/StardustLayout2/GatewayCluster.story';
import { stories as gatewayCertStories } from '../src/layouts/StardustLayout2/GatewayCert.story';
import { stories as gatewayNodeStories } from '../src/layouts/StardustLayout2/GatewayNode.story';
import { stories as mcpTabStories } from '../src/layouts/StardustLayout2/McpTab.story';
import { stories as platformTabStories } from '../src/layouts/StardustLayout2/PlatformTab.story';
import { stories as middlewareTabStories } from '../src/layouts/StardustLayout2/MiddlewareTab.story';
import { stories as galaxyProjectFormDrawerStories } from '../src/layouts/StardustLayout2/GalaxyProjectFormDrawer.story';
import { stories as nowPageStories } from '../src/layouts/StardustLayout2/NowPage.story';
import { stories as nodesPageStories } from '../src/layouts/StardustLayout2/NodesPage.story';
import { stories as agentDeployPageStories } from '../src/layouts/StardustLayout2/AgentDeployPage.story';
import { stories as appRowStories } from '../src/layouts/StardustLayout2/AppRow.story';
import { stories as installWizardStories } from '../src/layouts/StardustLayout2/InstallWizard.story';
import { stories as configCenterListStories } from '../src/layouts/StardustLayout2/ConfigCenterList.story';
import { stories as configDetailDrawerStories } from '../src/layouts/StardustLayout2/ConfigDetailDrawer.story';
import { stories as alarmsListStories } from '../src/layouts/StardustLayout2/AlarmsList.story';
import { stories as alarmsGroupsStories } from '../src/layouts/StardustLayout2/AlarmsGroups.story';
import { stories as alarmRecordDrawerStories } from '../src/layouts/StardustLayout2/AlarmRecordDrawer.story';
import { stories as mcpTokenFormStories } from '../src/layouts/StardustLayout2/McpTokenForm.story';
import { stories as mcpTokenResourcePanelStories } from '../src/layouts/StardustLayout2/McpTokenResourcePanel.story';
import { stories as starServerSettingsFormStories } from '../src/layouts/StardustLayout2/StarServerSettingsForm.story';
import { stories as mcpServerFormStories } from '../src/layouts/StardustLayout2/McpServerForm.story';
import { stories as appTracerTabStories } from '../src/layouts/StardustLayout2/AppTracerTab.story';
import { stories as dayStatTabStories } from '../src/layouts/StardustLayout2/DayStatTab.story';
import { stories as traceStatCardStories } from '../src/layouts/StardustLayout2/TraceStatCard.story';
import { stories as traceDataPanelStories } from '../src/layouts/StardustLayout2/TraceDataPanel.story';
import { stories as traceItemsPanelStories } from '../src/layouts/StardustLayout2/TraceItemsPanel.story';
import { stories as traceItemDrawerStories } from '../src/layouts/StardustLayout2/TraceItemDrawer.story';
import { stories as nodeFormDrawerStories } from '../src/layouts/StardustLayout2/NodeFormDrawer.story';
import { stories as deployLogDrawerStories } from '../src/layouts/StardustLayout2/DeployLogDrawer.story';
import { stories as deployEditDrawerStories } from '../src/layouts/StardustLayout2/DeployEditDrawer.story';
import { stories as pipelineConfigCardStories } from '../src/layouts/StardustLayout2/PipelineConfigCard.story';
import { stories as pipelineConfigDrawerStories } from '../src/layouts/StardustLayout2/PipelineConfigDrawer.story';
import { stories as pipelineRunDetailDrawerStories } from '../src/layouts/StardustLayout2/PipelineRunDetailDrawer.story';
import { stories as pipelineStepDrawerStories } from '../src/layouts/StardustLayout2/PipelineStepDrawer.story';
import { stories as pipelineWebhookCardStories } from '../src/layouts/StardustLayout2/PipelineWebhookCard.story';
import { stories as configItemDrawerStories } from '../src/layouts/StardustLayout2/ConfigItemDrawer.story';
import { stories as appRuleDrawerStories } from '../src/layouts/StardustLayout2/AppRuleDrawer.story';
import { stories as configHistoryDiffDrawerStories } from '../src/layouts/StardustLayout2/ConfigHistoryDiffDrawer.story';
import { stories as appRuleTableStories } from '../src/layouts/StardustLayout2/AppRuleTable.story';
import { stories as appFormDrawerStories } from '../src/layouts/StardustLayout2/AppFormDrawer.story';
import { stories as serviceDetailDrawerStories } from '../src/layouts/StardustLayout2/ServiceDetailDrawer.story';
import { stories as traceDetailStories } from '../src/layouts/StardustLayout2/TraceDetail.story';
import { stories as alarmsRulesStories } from '../apps/stardust/src/views/Console/AlarmsRules.story';

export const stories: Array<{
  id: string;
  component: unknown;
  props?: Record<string, unknown>;
}> = [
  ...navSegmentStories,
  ...kIconStories,
  ...topBarStories,
  ...commandPaletteStories,
  ...drawerStories,
  ...btnStories,
  ...guardModalStories,
  ...dotStories,
  ...chipStories,
  ...appTabsStories,
  ...deployVersionBarStories,
  ...deployVersionListStories,
  ...deployNodesStories,
  ...appLovStories,
  ...nodeLovStories,
  ...buildNodesStories,
  ...deployHistoryStories,
  ...deployPipelineRunsStories,
  ...deployFeedbackStripStories,
  ...attentionListStories,
  ...traceListStories,
  ...appLogStories,
  ...appConfigTableStories,
  ...appInstancesStories,
  ...appObserveStories,
  ...changesListStories,
  ...registryTabStories,
  ...gatewayTabStories,
  ...gatewayClusterStories,
  ...gatewayCertStories,
  ...gatewayNodeStories,
  ...mcpTabStories,
  ...platformTabStories,
  ...middlewareTabStories,
  ...galaxyProjectFormDrawerStories,
  ...nowPageStories,
  ...nodesPageStories,
  ...agentDeployPageStories,
  ...appRowStories,
  ...installWizardStories,
  ...configCenterListStories,
  ...configDetailDrawerStories,
  ...alarmsListStories,
  ...alarmsGroupsStories,
  ...alarmRecordDrawerStories,
  ...mcpTokenFormStories,
  ...mcpTokenResourcePanelStories,
  ...starServerSettingsFormStories,
  ...mcpServerFormStories,
  ...appTracerTabStories,
  ...dayStatTabStories,
  ...traceStatCardStories,
  ...traceDataPanelStories,
  ...traceItemsPanelStories,
  ...traceItemDrawerStories,
  ...nodeFormDrawerStories,
  ...deployLogDrawerStories,
  ...deployEditDrawerStories,
  ...pipelineConfigCardStories,
  ...pipelineConfigDrawerStories,
  ...pipelineRunDetailDrawerStories,
  ...pipelineStepDrawerStories,
  ...pipelineWebhookCardStories,
  ...configItemDrawerStories,
  ...appRuleDrawerStories,
  ...appRuleTableStories,
  ...appFormDrawerStories,
  ...serviceDetailDrawerStories,
  ...traceDetailStories,
  ...alarmsRulesStories,
  ...configHistoryDiffDrawerStories,
];
