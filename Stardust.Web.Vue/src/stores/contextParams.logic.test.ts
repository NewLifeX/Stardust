import { describe, expect, it } from 'vitest';
import { mergeContextParams } from './contextParams.logic.ts';

describe('mergeContextParams', () => {
  it('没写 projectId 时用顶栏上下文补上', () => {
    expect(mergeContextParams({ PageSize: 500 }, { projectId: 3, environment: 'prod' })).toEqual({
      projectId: 3,
      environment: 'prod',
      PageSize: 500,
    });
  });

  it('调用方显式的 projectId 盖过顶栏', () => {
    expect(mergeContextParams({ projectId: 9 }, { projectId: 3, environment: 'prod' })).toEqual({
      projectId: 9,
      environment: 'prod',
    });
  });

  it('上下文为空时原样返回显式参数', () => {
    expect(mergeContextParams({ q: 'a' }, {})).toEqual({ q: 'a' });
    expect(mergeContextParams(undefined, {})).toEqual({});
  });
});
