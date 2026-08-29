import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './index.css';

// 挂载入口：React 18 的 createRoot 方式
ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
