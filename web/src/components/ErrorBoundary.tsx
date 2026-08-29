// ErrorBoundary.tsx —— 后端宕机兜底（确认书铁律 7）。
// 为什么用类组件：React 的 error boundary 只能由类组件的
// componentDidCatch/getDerivedStateFromError 实现，函数组件无法替代。
import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  message: string;
}

export default class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, message: '' };

  // 静态方法：React 在渲染出错时调用，返回的值进入 state（必须先于 componentDidCatch 触发）
  static getDerivedStateFromError(error: unknown): State {
    return {
      hasError: true,
      message: error instanceof Error ? error.message : '发生未知错误',
    };
  }

  componentDidCatch(error: unknown) {
    // 日志出口：原型阶段打印到控制台即可（不引监控 SDK，避免过度设计）
    console.error('[ErrorBoundary]', error);
  }

  // 重试：重置错误状态 → 子树重新挂载 → 子组件 useEffect 重新请求
  private handleRetry = () => {
    this.setState({ hasError: false, message: '' });
  };

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-box" role="alert">
          <strong>⚠ 后端服务不可用</strong>
          <p>{this.state.message}</p>
          <button onClick={this.handleRetry}>重试</button>
        </div>
      );
    }
    return this.props.children;
  }
}
