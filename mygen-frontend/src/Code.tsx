import React, { useEffect, useState } from 'react';
import { API_BASE_URL } from './config';

const getConnectionString = () => localStorage.getItem('connectionString') || '';
const getInstructions = () => localStorage.getItem('instructions') || '';
const getOpenAiKey = () => localStorage.getItem('openAiKey') || '';

interface ColumnInfo {
  columnName: string;
  dataType: string;
}

function extractCodeBlocks(text: string): string {
  // Extracts the first code block or returns the whole text if no code block found
  const codeBlockMatch = text.match(/```[a-zA-Z]*\n([\s\S]*?)```/);
  if (codeBlockMatch) {
    return codeBlockMatch[1].trim();
  }
  return text.trim();
}

const Code: React.FC = () => {
  const [tables, setTables] = useState<string[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [connectionString, setConnectionString] = useState(getConnectionString());
  const [openAiKey, setOpenAiKey] = useState(getOpenAiKey());
  const [generating, setGenerating] = useState(false);
  const [generated, setGenerated] = useState<string | null>(null);
  const [genError, setGenError] = useState<string | null>(null);
  const [prompt, setPrompt] = useState('');
  const [tableInfos, setTableInfos] = useState<Record<string, ColumnInfo[]>>({});
  const [loadingTableInfo, setLoadingTableInfo] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!connectionString) {
      setLoading(false);
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    fetch(`${API_BASE_URL}/tables`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ connectionString: connectionString })
    })
      .then(async (res) => {
        const data = await res.json();
        if (Array.isArray(data)) {
          setTables(data);
        } else if (data && data.error) {
          setError(data.error);
        } else {
          setError('Failed to fetch tables');
        }
      })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [connectionString]);

  // Fetch table info when selected tables change
  useEffect(() => {
    if (selected.length === 0) {
      setTableInfos({});
      return;
    }
    setLoadingTableInfo(true);
    const fetchAll = async () => {
      const infos: Record<string, ColumnInfo[]> = {};
      for (const table of selected) {
        try {
          const res = await fetch(`${API_BASE_URL}/metadata/${encodeURIComponent(table)}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ connectionString })
          });
          if (res.ok) {
            infos[table] = await res.json();
          } else {
            infos[table] = [];
          }
        } catch {
          infos[table] = [];
        }
      }
      setTableInfos(infos);
      setLoadingTableInfo(false);
    };
    fetchAll();
  }, [selected, connectionString]);

  // Update prompt when selected tables or tableInfos change
  useEffect(() => {
    const tablesList = selected.length > 0 ? selected.join(', ') : 'all tables';
    let tableInfoText = '';
    if (selected.length > 0 && Object.keys(tableInfos).length > 0) {
      tableInfoText = '\n\nTable information:';
      for (const table of selected) {
        const columns = tableInfos[table];
        if (columns && columns.length > 0) {
          tableInfoText += `\n- ${table}: ` + columns.map(c => `${c.columnName} (${c.dataType})`).join(', ');
        }
      }
    }
    setPrompt(`generate a database class for the tables: ${tablesList}${tableInfoText}`);
  }, [selected, tableInfos]);

  const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const options = Array.from(e.target.selectedOptions).map(opt => opt.value);
    setSelected(options);
  };

  const handleGenerate = async () => {
    setGenerating(true);
    setGenError(null);
    setGenerated(null);
    setCopied(false);
    try {
      const res = await fetch(`${API_BASE_URL}/codegen`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prompt, openAiKey })
      });
      if (!res.ok) throw new Error('Failed to generate code');
      const data = await res.json();
      setGenerated(typeof data === 'string' ? data : JSON.stringify(data));
    } catch (e: any) {
      setGenError(e.message || 'Failed to generate code');
    } finally {
      setGenerating(false);
    }
  };

  const handleCopy = async () => {
    if (!generated) return;
    const code = extractCodeBlocks(generated);
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  // Filtered tables based on filter input
  const filteredTables = tables;

  if (!connectionString) {
    return (
      <div className="card">
        <h2>Code</h2>
        <p style={{ color: 'orange', fontWeight: 500 }}>
          No connection string found. Please go to the <b>Config</b> tab and save your configuration first.
        </p>
      </div>
    );
  }

  return (
    <div className="card">
      <h2>Code</h2>
      {loading ? (
        <p>Loading tables...</p>
      ) : error ? (
        <p style={{ color: 'red' }}>{error}</p>
      ) : (
        <>
          <label htmlFor="table-select">Select Tables</label>
          <select
            id="table-select"
            multiple
            value={selected}
            onChange={handleChange}
            style={{ width: '100%', minHeight: 120, fontSize: 16, marginBottom: 0, marginTop: 0 }}
          >
            {filteredTables.map((table) => (
              <option key={table} value={table}>{table}</option>
            ))}
          </select>
          {selected.length > 0 && (
            <div style={{ marginTop: 8, marginBottom: 0 }}>
              <strong>Selected tables:</strong>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {selected.map((t) => <li key={t}>{t}</li>)}
              </ul>
            </div>
          )}
          {loadingTableInfo && <div style={{ color: '#1976d2', marginBottom: 8 }}>Loading table information...</div>}
          <div style={{ margin: '18px 0 10px 0' }}>
            <label htmlFor="prompt" style={{ fontWeight: 600, marginBottom: 4 }}>Prompt</label>
            <textarea
              id="prompt"
              value={prompt}
              onChange={e => setPrompt(e.target.value)}
              style={{ width: '100%', minHeight: 80, fontSize: 15, marginTop: 0, marginBottom: 0 }}
            />
          </div>
          <button onClick={handleGenerate} disabled={generating || !prompt} style={{ marginTop: 8, minWidth: 180 }}>
            {generating ? 'Generating...' : 'Generate Database Class'}
          </button>
          {genError && <div style={{ color: 'red', marginTop: 8 }}>{genError}</div>}
          {generated && (
            <div style={{ marginTop: 24, position: 'relative' }}>
              <label style={{ fontWeight: 600 }}>Generated Code:</label>
              <div style={{ position: 'relative' }}>
                <button
                  onClick={handleCopy}
                  className="copy-btn"
                  style={{ top: 12, right: 12 }}
                >
                  {copied ? 'Copied!' : 'Copy Code'}
                </button>
                <pre style={{ marginTop: 0 }}>{extractCodeBlocks(generated)}</pre>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default Code; 