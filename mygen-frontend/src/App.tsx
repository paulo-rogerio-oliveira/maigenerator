import React from 'react';
import { HashRouter as Router, Routes, Route, Link, Navigate, useLocation } from 'react-router-dom';
import Configuration from './Configuration';
import Code from './Code';
import './App.css';

function NavTabs() {
  const location = useLocation();
  return (
    <header className="navbar">
      <div className="navbar-logo">

        <span className="navbar-title">MyGen App</span>
      </div>
      <nav className="navbar-tabs" aria-label="Main navigation">
        <Link
          to="/config"
          className={location.pathname === '/config' ? 'active' : ''}
          style={{ textDecoration: 'none' }}
        >
          <button className={location.pathname === '/config' ? 'active' : ''} aria-current={location.pathname === '/config' ? 'page' : undefined}>Config</button>
        </Link>
        <Link
          to="/code"
          className={location.pathname === '/code' ? 'active' : ''}
          style={{ textDecoration: 'none' }}
        >
          <button className={location.pathname === '/code' ? 'active' : ''} aria-current={location.pathname === '/code' ? 'page' : undefined}>Code</button>
        </Link>
      </nav>
    </header>
  );
}

function App() {
  return (
    <Router>
      <div className="App">
        <NavTabs />
        <Routes>
          <Route path="/config" element={<Configuration />} />
          <Route path="/code" element={<Code />} />
          <Route path="*" element={<Navigate to="/config" replace />} />
        </Routes>
      </div>
    </Router>
  );
}

export default App;
