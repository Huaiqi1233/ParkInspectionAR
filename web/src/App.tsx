// App.tsx —— 组装：标题栏 + ErrorBoundary(数据区)。
// 为什么 ErrorBoundary 只包 MarkerList 而不包整个页面：
// 静态标题栏不依赖后端，宕机时仍能显示，用户能明确看到"哪部分挂了"。
import ErrorBoundary from './components/ErrorBoundary';
import MarkerList from './components/MarkerList';

export default function App() {
  return (
    <div className="page">
      <h1>园区巡检 AR 标注 · 管理端</h1>
      <ErrorBoundary>
        <MarkerList />
      </ErrorBoundary>
    </div>
  );
}
