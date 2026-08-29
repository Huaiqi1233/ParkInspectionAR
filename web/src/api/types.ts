// types.ts —— 三端共享数据模型的 TypeScript 声明。
// 字段必须与 docs/api-contract.md v0.1 及 Go models.go 的 json tag 逐字对齐，
// 否则 axios 拿到的字段名与类型会错位（这是三端契约一致性的落点）。

export type MarkerType = 'equipment' | 'hazard' | 'route_point' | 'other';
export type MarkerStatus = 'pending' | 'processing' | 'resolved' | 'closed';

export interface Position {
  x: number;
  y: number;
  z: number;
}

export interface Rotation {
  x: number;
  y: number;
  z: number;
  w: number;
}

export interface Geo {
  lat: number;
  lng: number;
}

export interface Marker {
  id: string;
  type: MarkerType;
  title: string;
  description: string;
  position: Position;
  rotation: Rotation;
  geo: Geo | null; // 契约：geo 可空（无 GPS 时服务端返回 null）
  status: MarkerStatus;
  reporter: string;
  photoUrl: string | null; // 原型恒 null
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
