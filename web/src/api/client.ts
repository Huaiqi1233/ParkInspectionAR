// client.ts —— Axios 实例 + 信封解包 + 错误归一化。
// 为什么统一在这里解信封：所有 API 响应都是 {code,message,data}，
// 组件层只关心 data 或抛出的 ApiError，不重复处理信封结构。
import axios, { AxiosError } from 'axios';
import type { Envelope, Marker, MarkerListData } from './types';

// ApiError：业务/网络错误统一形态，ErrorBoundary 与组件据此渲染
export class ApiError extends Error {
  code: number; // 业务码：0 无（网络错误时），40001/40401/... 业务错误
  status: number | null; // HTTP 状态码，网络错误为 null

  constructor(message: string, code: number, status: number | null) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.status = status;
  }
}

// 为什么 baseURL 用 /api：Vite dev proxy 已把 /api 转发到 :8080（见 vite.config.ts），
// 同源请求，天然规避 CORS；生产部署由反代或改 Go CORS 处理。
export const apiClient = axios.create({
  baseURL: '/api',
  timeout: 8000, // 8s 超时：后端宕机时快速失败，避免列表一直转圈
});

// 请求拦截器：无需 token（原型无鉴权），暂为空，保留扩展位
apiClient.interceptors.request.use((config) => config);

// 响应拦截器：解信封。
// - 网络层失败（宕机/超时/非 2xx）：包装为 ApiError（code 取 HTTP 状态码取反区分）
// - 业务失败（HTTP 2xx 但 code != 0）：抛 ApiError(code=业务码)
apiClient.interceptors.response.use(
  (response) => {
    const body = response.data as Envelope<unknown>;
    // 契约：code=0 恒成功；非 0 是业务错误（如 40001 参数非法）
    if (body && typeof body.code === 'number' && body.code !== 0) {
      throw new ApiError(body.message || '业务错误', body.code, response.status);
    }
    // 成功：直接把 data 交给调用方，组件层无需再 .data.data
    return { ...response, data: body.data };
  },
  (error: AxiosError<Envelope<unknown>>) => {
    if (error.response) {
      // 服务端有响应但非 2xx：取服务端信封里的 message（如 40401 marker not found）
      const body = error.response.data;
      throw new ApiError(
        body?.message || `HTTP ${error.response.status}`,
        body?.code ?? error.response.status * -1,
        error.response.status,
      );
    }
    // 网络层失败（后端宕机/超时/跨域）：给出明确提示，供 ErrorBoundary 显示
    throw new ApiError('无法连接后端服务（后端可能未启动）', 0, null);
  },
);

// 类型化 API 方法：组件只 import 这里，不直接碰 axios
export const api = {
  // GET /api/v1/markers?status=&priority=&page=&pageSize=
  listMarkers(params: { status?: string; priority?: string; page: number; pageSize: number }) {
    return apiClient.get<MarkerListData, MarkerListData>('/v1/markers', { params });
  },
  // PATCH /api/v1/markers/:id —— 状态流转（白名单字段服务端已校验）
  updateMarker(id: string, patch: Partial<Pick<Marker, 'status' | 'title' | 'description' | 'priority'>>) {
    return apiClient.patch<Marker, Marker>(`/v1/markers/${id}`, patch);
  },
  // DELETE /api/v1/markers/:id
  deleteMarker(id: string) {
    return apiClient.delete<void, void>(`/v1/markers/${id}`);
  },
};
