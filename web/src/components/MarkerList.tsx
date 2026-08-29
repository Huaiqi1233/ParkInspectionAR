// MarkerList.tsx —— 管理端主视图：列表 + 筛选 + 分页 + 状态流转 + 删除。
// 状态全部本地 useState 管理（禁止 Redux，确认书铁律 4）。
// 错误处理：请求失败统一 throw ApiError，由外层 ErrorBoundary 捕获显示"服务不可用"。
import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Marker, MarkerStatus, MarkerType } from '../api/types';

// 中文显示映射：枚举值 → 界面文案（type/status）
const TYPE_LABEL: Record<MarkerType, string> = {
  equipment: '设备',
  hazard: '隐患',
  route_point: '巡检点',
  other: '其他',
};

const STATUS_LABEL: Record<MarkerStatus, string> = {
  pending: '待处理',
  processing: '处理中',
  resolved: '已解决',
  closed: '已关闭',
};

const PAGE_SIZE = 10;

export default function MarkerList() {
  // 列表数据与加载态
  const [items, setItems] = useState<Marker[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  // 筛选条件（下拉即时生效，改条件后回到第 1 页）
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');

  // fetchList 用 useCallback 稳定引用，供 useEffect 与按钮复用；
  // 抛出的错误不在此 catch，交给 ErrorBoundary（"服务不可用"场景统一兜底）
  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api.listMarkers({
        status: statusFilter || undefined,
        type: typeFilter || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setItems(data.items);
      setTotal(data.total);
    } finally {
      setLoading(false);
    }
  }, [statusFilter, typeFilter, page]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  // 行内状态流转：乐观更新 UI，PATCH 失败时回滚并交给 ErrorBoundary 提示
  const handleStatusChange = async (marker: Marker, next: MarkerStatus) => {
    const prev = items;
    setItems(items.map((m) => (m.id === marker.id ? { ...m, status: next } : m)));
    try {
      await api.updateMarker(marker.id, { status: next });
    } catch (e) {
      setItems(prev); // 失败回滚
      throw e; // 交给 ErrorBoundary
    }
  };

  // 删除：confirm 确认后 DELETE，成功则刷新当前页
  const handleDelete = async (id: string) => {
    if (!window.confirm('确认删除该标注？此操作不可恢复。')) return;
    await api.deleteMarker(id); // 失败同样抛给 ErrorBoundary
    fetchList();
  };

  // 分页计算：total 驱动的总页数；越界保护（删除末页最后一条后回退一页）
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  return (
    <div>
      {/* 筛选区：status/type 下拉 + 刷新按钮 */}
      <div className="filters">
        <select
          value={statusFilter}
          onChange={(e) => {
            setStatusFilter(e.target.value);
            setPage(1);
          }}
        >
          <option value="">全部状态</option>
          {(Object.keys(STATUS_LABEL) as MarkerStatus[]).map((s) => (
            <option key={s} value={s}>
              {STATUS_LABEL[s]}
            </option>
          ))}
        </select>

        <select
          value={typeFilter}
          onChange={(e) => {
            setTypeFilter(e.target.value);
            setPage(1);
          }}
        >
          <option value="">全部类型</option>
          {(Object.keys(TYPE_LABEL) as MarkerType[]).map((t) => (
            <option key={t} value={t}>
              {TYPE_LABEL[t]}
            </option>
          ))}
        </select>

        <button onClick={fetchList} disabled={loading}>
          刷新
        </button>
        <span>
          共 {total} 条
        </span>
      </div>

      {/* 加载/空态 */}
      {loading ? (
        <div className="loading">加载中…</div>
      ) : items.length === 0 ? (
        <div className="empty">暂无标注数据（Unity 上报或 curl POST 后可查看）</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>标题</th>
              <th>类型</th>
              <th>状态</th>
              <th>巡检员</th>
              <th>GPS</th>
              <th>创建时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {items.map((m) => (
              <tr key={m.id}>
                <td title={m.description}>{m.title}</td>
                <td>{TYPE_LABEL[m.type]}</td>
                <td>
                  {/* 状态下拉：改即 PATCH（乐观更新）。
                      服务端仅校验枚举合法，不强制状态机方向（契约 v0.1 原型阶段），故前端不做跳转限制 */}
                  <select
                    value={m.status}
                    onChange={(e) => handleStatusChange(m, e.target.value as MarkerStatus)}
                  >
                    {(Object.keys(STATUS_LABEL) as MarkerStatus[]).map((s) => (
                      <option key={s} value={s}>
                        {STATUS_LABEL[s]}
                      </option>
                    ))}
                  </select>
                </td>
                <td>{m.reporter}</td>
                <td>{m.geo ? `${m.geo.lat.toFixed(4)}, ${m.geo.lng.toFixed(4)}` : '—'}</td>
                <td>{new Date(m.createdAt).toLocaleString('zh-CN')}</td>
                <td>
                  <button onClick={() => handleDelete(m.id)}>删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* 分页 */}
      <div className="pager">
        <button disabled={page <= 1} onClick={() => setPage(page - 1)}>
          上一页
        </button>
        <span>
          {page} / {totalPages}
        </span>
        <button disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          下一页
        </button>
      </div>
    </div>
  );
}
