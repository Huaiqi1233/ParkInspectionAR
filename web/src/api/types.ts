// types.ts —— 三端共享数据模型的 TypeScript 声明（对齐契约 v2.0）。
// v2.0 严格对齐任务书：priority 字段、status 三态、position 仅 x/y/z，无 rotation/geo/reporter/photoUrl/type。

export type MarkerPriority = 'high' | 'medium' | 'low';
export type MarkerStatus = 'open' | 'in_progress' | 'resolved';

export interface Position {
  x: number;
  y: number;
  z: number;
}

export interface Marker {
  id: string;
  title: string;
  description: string;
  priority: MarkerPriority;
  status: MarkerStatus;
  position: Position;
  createdAt: string; // RFC3339
  updatedAt: string; // RFC3339
}

// 信封：与 Go Envelope 对应，code=0 恒成功
export interface Envelope<T> {
  code: number;
  message: string;
  data: T;
}

// 列表响应 data 固定为 {total, items}（契约第 2 节）
export interface MarkerListData {
  total: number;
  items: Marker[];
}
