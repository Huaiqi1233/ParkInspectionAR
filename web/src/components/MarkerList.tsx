// MarkerList.tsx —— 管理端主视图（任务书 3.2）：列表显示标题/优先级/状态 + 修改状态。
// 状态本地 useState 管理（禁止 Redux）。错误抛 ApiError，由 ErrorBoundary 兜底"后端不可用"。
import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Marker, MarkerPriority, MarkerStatus } from '../api/types';

// 中文显示映射（任务书：优先级 / 状态三态）
const PRIORITY_LABEL: Record<MarkerPriority, string> = {
  high: '高',
  medium: '中',
  low: '低',
};

const STATUS_LABEL: Record<MarkerStatus, string> = {
  open: '待处理',
  in_progress: '处理中',
  resolved: '已解决',
};

const PAGE_SIZE = 10;

export default function MarkerList() {
  const [items, setItems] = useState<Marker[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api.listMarkers({
        status: statusFilter || undefined,
        priority: priorityFilter || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setItems(data.items);
      setTotal(data.total);
    } finally {
      setLoading(false);
    }
  }, [statusFilter, priorityFilter, page]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  // 状态流转：乐观更新，失败回滚
  const handleStatusChange = async (marker: Marker, next: MarkerStatus) => {
    const prev = items;
    setItems(items.map((m) => (m.id === marker.id ? { ...m, status: next } : m)));
    try {
      await api.updateMarker(marker.id, { status: next });
    } catch (e) {
      setItems(prev);
      throw e;
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('确认删除该标记？此操作不可恢复。')) return;
    await api.deleteMarker(id);
    fetchList();
  };

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  return (
    <div>
      {/* 筛选区：status/priority 下拉 + 刷新 */}
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
          value={priorityFilter}
          onChange={(e) => {
            setPriorityFilter(e.target.value);
            setPage(1);
          }}
        >
          <option value="">全部优先级</option>
          {(Object.keys(PRIORITY_LABEL) as MarkerPriority[]).map((p) => (
            <option key={p} value={p}>
              {PRIORITY_LABEL[p]}
            </option>
          ))}
        </select>

        <button onClick={fetchList} disabled={loading}>
          刷新
        </button>
        <span>共 {total} 条</span>
      </div>

      {loading ? (
        <div className="loading">加载中…</div>
      ) : items.length === 0 ? (
        <div className="empty">暂无上报数据</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>标题</th>
              <th>优先级</th>
              <th>状态</th>
              <th>位置</th>
              <th>创建时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {items.map((m) => (
              <tr key={m.id}>
                <td title={m.description}>{m.title}</td>
                <td>
                  <span className={`badge badge-priority-${m.priority}`}>
                    {PRIORITY_LABEL[m.priority]}
                  </span>
                </td>
                <td>
                  {/* 状态下拉：open → in_progress → resolved（任务书 3.2） */}
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
                <td>
                  {m.position.x.toFixed(1)}, {m.position.y.toFixed(1)}, {m.position.z.toFixed(1)}
                </td>
                <td>{new Date(m.createdAt).toLocaleString('zh-CN')}</td>
                <td>
                  <button onClick={() => handleDelete(m.id)}>删除</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

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
