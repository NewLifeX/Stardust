import GatewayCertBlock from './GatewayCertBlock.vue';
import GatewayCertDrawer from './GatewayCertDrawer.vue';
import { emptyCertForm } from './gatewayCert.logic.ts';
import type { CertRow, GatewayCertForm } from './gatewayCert.logic.ts';

const WITH_DATA: CertRow[] = [
  {
    id: 31, domain: 'iot.example.com', enable: true,
    pfxFile: '/data/certs/iot.example.com.pfx', crtFile: '/data/certs/iot.example.com.crt',
    keyFile: '/data/certs/iot.example.com.key', pemFile: '', notAfterMs: Date.parse('2026-12-31T00:00:00Z'),
  },
  {
    id: 32, domain: 'api.example.com', enable: false,
    pfxFile: '', crtFile: '/data/certs/api.example.com.crt', keyFile: '', pemFile: '/data/certs/api.example.com.pem',
    notAfterMs: 0,
  },
];

const EDIT_FORM: GatewayCertForm = {
  ...emptyCertForm(),
  domain: 'iot.example.com',
  pfxFile: '/data/certs/iot.example.com.pfx',
  crtFile: '/data/certs/iot.example.com.crt',
  keyFile: '/data/certs/iot.example.com.key',
  issuer: 'CN=Example CA',
  subject: 'CN=iot.example.com',
  notBefore: '2026-01-01',
  notAfter: '2026-12-31',
  thumbprint: 'AB12CD34EF560123456789ABCDEF0123456789AB',
  autoRenew: true,
  provider: 'HTTP',
  renewDays: 30,
};

export const stories = [
  { id: 'GatewayCert/Data', component: GatewayCertBlock, props: { rows: WITH_DATA } },
  { id: 'GatewayCert/Empty', component: GatewayCertBlock, props: { rows: [] } },
  { id: 'GatewayCert/Loading', component: GatewayCertBlock, props: { rows: [], loading: true } },
  { id: 'GatewayCert/Failed', component: GatewayCertBlock, props: { rows: [], error: 'Request failed with status 500' } },
  { id: 'GatewayCert/CreateDrawer', component: GatewayCertDrawer, props: { open: true, form: emptyCertForm() } },
  { id: 'GatewayCert/EditDrawer', component: GatewayCertDrawer, props: { open: true, form: EDIT_FORM, editing: true } },
  { id: 'GatewayCert/Expanded', component: GatewayCertBlock, props: { rows: WITH_DATA, expandedId: 31, expandedInfo: EDIT_FORM } },
];
