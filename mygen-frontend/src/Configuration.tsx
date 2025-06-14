import React, { useState, useEffect } from 'react';
import { API_BASE_URL } from './config';

interface ConfigurationProps {
  onSave?: (connectionString: string, openAiKey: string) => void;
}

interface ConfigData {
  connectionString: string;
  openAiKey: string;
}

const CONFIG_FILE = 'config.json';

const Configuration: React.FC<ConfigurationProps> = ({ onSave }) => {
  const [connectionString, setConnectionString] = useState('');
  const [openAiKey, setOpenAiKey] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<string | null>(null);
  const [modelFile, setModelFile] = useState<File | null>(null);
  const [repositoryFile, setRepositoryFile] = useState<File | null>(null);
  const [uploadStatus, setUploadStatus] = useState<{ model: string; repository: string }>({ model: '', repository: '' });
  const [existingFiles, setExistingFiles] = useState<{ model: string; repository: string }>({ model: '', repository: '' });

  // Load config from backend on mount
  useEffect(() => {
    setLoading(true);
    setError(null);
    
    // Load configuration
    fetch(`${API_BASE_URL}/file/load/${CONFIG_FILE}`)
      .then(async (res) => {
        if (!res.ok) throw new Error('No config found');
        const text = await res.json();
        const data: ConfigData = JSON.parse(text);
        console.log(data.connectionString);
        setConnectionString(data.connectionString || '');
        setOpenAiKey(data.openAiKey || '');
      })
      .catch(() => {
        // fallback: try localStorage
        setConnectionString(localStorage.getItem('connectionString') || '');
        setOpenAiKey(localStorage.getItem('openAiKey') || '');
      });

    // Check for existing files
    const checkFiles = async () => {
      try {
        const [modelRes, repoRes] = await Promise.all([
          fetch(`${API_BASE_URL}/file/exists/model`),
          fetch(`${API_BASE_URL}/file/exists/repository`)
        ]);

        const modelData = await modelRes.json();
        const repoData = await repoRes.json();

        setExistingFiles({
          model: modelData.exists ? modelData.path : '',
          repository: repoData.exists ? repoData.path : ''
        });
      } catch (error) {
        console.error('Error checking existing files:', error);
      }
    };

    checkFiles().finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    const config: ConfigData = { connectionString, openAiKey };
    try {
      const res = await fetch(`${API_BASE_URL}/file/save`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fileName: CONFIG_FILE, content: JSON.stringify(config) })
      });
      if (!res.ok) throw new Error('Failed to save config');
      if (onSave) onSave(connectionString, openAiKey);
      // Also save to localStorage as backup
      localStorage.setItem('connectionString', connectionString);
      localStorage.setItem('openAiKey', openAiKey);
      alert('Configuration saved!');
    } catch (e: any) {
      setError(e.message || 'Failed to save');
    } finally {
      setSaving(false);
    }
  };

  const handleTestConnection = async () => {
    setTesting(true);
    setTestResult(null);
    setError(null);
    try {
      const res = await fetch(`${API_BASE_URL}/test-connection`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body:  JSON.stringify({ connectionString: connectionString })
      });
      console.log( JSON.stringify({ connectionString: connectionString }));
      const data = await res.json();
      console.log(data);
      if ( data.success) {
        setTestResult('? Connection successful!');
      } else {
        setTestResult(`? Connection failed: ${data.error || 'Unknown error'}`);
      }
    } catch (e: any) {
      setTestResult(`? Connection failed: ${e.message}`);
    } finally {
      setTesting(false);
    }
  };

  const handleFileUpload = async (file: File, type: 'model' | 'repository') => {
    try {
      const formData = new FormData();
      formData.append('file', file);
      
      // Get antiforgery token
      const tokenResponse = await fetch(`${API_BASE_URL}/antiforgery/token`, {
        credentials: 'include'
      });
      const { token } = await tokenResponse.json();
      
      const response = await fetch(`${API_BASE_URL}/file/upload/${type}`, {
        method: 'POST',
        body: formData,
        headers: {
          'X-XSRF-TOKEN': token
        },
        credentials: 'include'
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `Failed to upload ${type} file`);
      }

      setUploadStatus(prev => ({ ...prev, [type]: 'Upload successful!' }));
      setTimeout(() => {
        setUploadStatus(prev => ({ ...prev, [type]: '' }));
      }, 3000);
    } catch (error: any) {
      setUploadStatus(prev => ({ ...prev, [type]: `Error: ${error.message || 'Unknown error'}` }));
    }
  };

  return (
    <div className="card">
      <h2>Configuration</h2>
      {loading ? (
        <p>Loading configuration...</p>
      ) : (
        <>
          <div style={{ marginBottom: 16 }}>
            <label htmlFor="connectionString">Connection String</label>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input
                id="connectionString"
                type="text"
                value={connectionString}
                onChange={e => setConnectionString(e.target.value)}
                placeholder="Enter your connection string"
                style={{ flex: 1 }}
              />
            </div>
            <button 
              type="button" 
              onClick={handleTestConnection} 
              disabled={testing || !connectionString} 
              style={{ minWidth: 120 }}
            >
              {testing ? 'Testing...' : 'Test Connection'}
            </button>
            {testResult && <div style={{ marginTop: 6, color: testResult.startsWith('?') ? 'green' : 'red' }}>{testResult}</div>}
          </div>

          <div style={{ marginBottom: 16 }}>
            <label htmlFor="openAiKey">OpenAI API Key</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input
                id="openAiKey"
                type={showKey ? 'text' : 'password'}
                value={openAiKey}
                onChange={e => setOpenAiKey(e.target.value)}
                placeholder="sk-..."
                autoComplete="off"
                style={{ flex: 1 }}
              />
            </div>
            <button
              type="button"             
              onClick={() => setShowKey(v => !v)}
              tabIndex={-1}
              style={{ minWidth: 120, marginTop: 8 }}
            >
              {showKey ? 'Hide' : 'Show'}
            </button>
          </div>

          <div style={{ marginBottom: 16 }}>
            <label htmlFor="model-file">Model File</label>
            {existingFiles.model && (
              <div style={{ color: '#666', fontSize: '0.9em', marginBottom: 4 }}>
                Current file: {existingFiles.model}
              </div>
            )}
            <input
              id="model-file"
              type="file"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) {
                  setModelFile(file);
                  handleFileUpload(file, 'model');
                }
              }}
              style={{ marginBottom: 8 }}
            />
            {uploadStatus.model && (
              <div style={{ color: uploadStatus.model.startsWith('Error') ? 'red' : 'green', marginTop: 4 }}>
                {uploadStatus.model}
              </div>
            )}
          </div>

          <div style={{ marginBottom: 16 }}>
            <label htmlFor="repository-file">Repository File</label>
            {existingFiles.repository && (
              <div style={{ color: '#666', fontSize: '0.9em', marginBottom: 4 }}>
                Current file: {existingFiles.repository}
              </div>
            )}
            <input
              id="repository-file"
              type="file"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) {
                  setRepositoryFile(file);
                  handleFileUpload(file, 'repository');
                }
              }}
              style={{ marginBottom: 8 }}
            />
            {uploadStatus.repository && (
              <div style={{ color: uploadStatus.repository.startsWith('Error') ? 'red' : 'green', marginTop: 4 }}>
                {uploadStatus.repository}
              </div>
            )}
          </div>

          <button
            type="button"
            onClick={handleSave}
            disabled={saving}
            style={{ marginTop: 8 }}
          >
            {saving ? 'Saving...' : 'Save Configuration'}
          </button>
          {error && <div style={{ color: 'red', marginTop: 8 }}>{error}</div>}
        </>
      )}
    </div>
  );
};

export default Configuration; 