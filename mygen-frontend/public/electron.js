const { app, BrowserWindow } = require('electron');
const path = require('path');
const { spawn } = require('child_process');
const isDev = process.env.NODE_ENV === 'development';

let mainWindow;
let apiProcess;

function startApiProcess() {
  const apiPath =  path.join(process.resourcesPath, 'api/mygen-api.exe');

  // Spawn the process with the current user's privileges
  apiProcess = spawn(apiPath, [], {
    stdio: 'pipe',
    windowsHide: false,
    // Don't create a new console window
    detached: false,
    // Use the current user's environment
    env: process.env
  });

  apiProcess.stdout.on('data', (data) => {
    console.log(`API stdout: ${data}`);
  });

  apiProcess.stderr.on('data', (data) => {
    console.error(`API stderr: ${data}`);
  });

  apiProcess.on('close', (code) => {
    console.log(`API process exited with code ${code}`);
    // If the process was closed unexpectedly, try to restart it
    if (code !== 0 && !app.isQuitting) {
      console.log('API process closed unexpectedly, attempting to restart...');
      setTimeout(startApiProcess, 1000);
    }
  });

  apiProcess.on('error', (err) => {
    console.error('Failed to start API process:', err);
    // If there's an error starting the process, try again
    if (!app.isQuitting) {
      console.log('Attempting to restart API process...');
      setTimeout(startApiProcess, 1000);
    }
  });
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    },
  });

  // For development, load React dev server; for production, load build
  if (process.env.ELECTRON_START_URL) {
    mainWindow.loadURL(process.env.ELECTRON_START_URL);
  } else {
    mainWindow.loadFile(path.join(__dirname, '../build/index.html'));
  }

  // Start the API process
  startApiProcess();
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.isQuitting = true;
    if (apiProcess) {
      apiProcess.kill();
    }
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});

// Handle app quit
app.on('before-quit', () => {
  app.isQuitting = true;
  if (apiProcess) {
    apiProcess.kill();
  }
}); 