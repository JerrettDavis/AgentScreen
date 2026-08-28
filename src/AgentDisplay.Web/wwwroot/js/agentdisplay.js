window.agentDisplay = (() => {
  let device;
  let rx;
  let serviceUuid;
  let rxUuid;
  let connecting;
  const encoder = new TextEncoder();
  const accessKeyName = 'agentdisplay.pairing-key';
  const autoSyncKey = 'agentdisplay.auto-sync';
  const autoSyncIntervalKey = 'agentdisplay.auto-sync-interval-seconds';

  function browserCulture() {
    return navigator.languages?.[0] || navigator.language || 'en-US';
  }

  function autoSyncEnabled() {
    return localStorage.getItem(autoSyncKey) !== 'false';
  }

  function setAutoSyncEnabled(enabled) {
    localStorage.setItem(autoSyncKey, enabled ? 'true' : 'false');
  }

  function autoSyncIntervalSeconds() {
    const value = Number.parseInt(localStorage.getItem(autoSyncIntervalKey) || '30', 10);
    return [30, 60, 300, 900].includes(value) ? value : 30;
  }

  function setAutoSyncIntervalSeconds(seconds) {
    const value = Number(seconds);
    if (![30, 60, 300, 900].includes(value)) throw new Error('Unsupported automatic sync interval.');
    localStorage.setItem(autoSyncIntervalKey, String(value));
  }

  function accessKey() {
    const url = new URL(window.location.href);
    const supplied = url.searchParams.get('key');
    if (supplied) {
      sessionStorage.setItem(accessKeyName, supplied.trim());
      url.searchParams.delete('key');
      history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
    }
    return sessionStorage.getItem(accessKeyName) || '';
  }

  function setAccessKey(value) {
    const normalized = String(value || '').trim();
    if (normalized) sessionStorage.setItem(accessKeyName, normalized);
    else sessionStorage.removeItem(accessKeyName);
  }

  function clearAccessKey() {
    sessionStorage.removeItem(accessKeyName);
  }

  const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

  function bluetoothMessage(error, operation) {
    const raw = String(error?.message || error || '').trim();
    if (error?.name === 'NotFoundError') {
      return 'No display was selected. Choose AgentDisplay from the Bluetooth picker, then retry.';
    }
    if (error?.name === 'NotAllowedError' || /permission|not permitted/i.test(raw)) {
      return 'Bluetooth permission was denied. Allow Bluetooth for this site, then retry.';
    }
    if (/gatt server is disconnected|not connected|networkerror|connection.*lost/i.test(raw)) {
      return 'The Bluetooth connection was lost. Keep the display nearby and awake, then retry. If it continues, reset the display and reconnect.';
    }
    if (/service|characteristic/i.test(raw)) {
      return 'AgentDisplay connected, but its sync service was unavailable. Reset the display, wait for it to finish starting, then reconnect.';
    }
    return `Bluetooth ${operation} failed. Retry the connection. If it continues, reset the display and make sure no other browser or phone is connected.`;
  }

  function clearGattState() {
    rx = undefined;
  }

  async function openGatt() {
    if (!device?.gatt) throw new Error('No AgentDisplay has been selected. Select Connect and choose the display first.');
    if (connecting) return await connecting;
    connecting = (async () => {
      let lastError;
      for (let attempt = 0; attempt < 2; attempt++) {
        try {
          const server = device.gatt.connected ? device.gatt : await device.gatt.connect();
          const service = await server.getPrimaryService(serviceUuid);
          rx = await service.getCharacteristic(rxUuid);
          return rx;
        } catch (error) {
          lastError = error;
          clearGattState();
          if (attempt === 0) await delay(250);
        }
      }
      throw new Error(bluetoothMessage(lastError, 'connection'));
    })();
    try { return await connecting; }
    finally { connecting = undefined; }
  }

  async function connect(requestedServiceUuid, requestedRxUuid) {
    if (!navigator.bluetooth) {
      throw new Error('Web Bluetooth is unavailable. Use Chrome or Edge on a Bluetooth-capable computer, or connect over Wi-Fi.');
    }

    serviceUuid = requestedServiceUuid;
    rxUuid = requestedRxUuid;
    try {
      device = await navigator.bluetooth.requestDevice({
        filters: [{ services: [serviceUuid] }],
        optionalServices: [serviceUuid]
      });
      device.addEventListener('gattserverdisconnected', clearGattState);
      await openGatt();
      return { name: device.name || 'AgentDisplay', connected: device.gatt.connected };
    } catch (error) {
      clearGattState();
      throw new Error(bluetoothMessage(error, 'connection'));
    }
  }

  async function push(snapshot) {
    try {
      const characteristic = rx && device?.gatt?.connected ? rx : await openGatt();
      const bytes = encoder.encode(`${JSON.stringify(snapshot)}\n`);
      for (let offset = 0; offset < bytes.length; offset += 160) {
        const chunk = bytes.slice(offset, Math.min(offset + 160, bytes.length));
        if (typeof characteristic.writeValueWithoutResponse === 'function') {
          await characteristic.writeValueWithoutResponse(chunk);
        } else {
          await characteristic.writeValue(chunk);
        }
        await delay(8);
      }
      return bytes.length;
    } catch (error) {
      clearGattState();
      throw new Error(bluetoothMessage(error, 'sync'));
    }
  }

  async function compactSnapshot() {
    const key = accessKey();
    const response = await fetch('/api/v1/device/snapshot', {
      headers: key ? { 'X-AgentDisplay-Key': key } : {}
    });
    if (!response.ok) {
      throw new Error('Unable to fetch the display snapshot.');
    }
    return await response.json();
  }

  async function copyText(value) {
    if (!navigator.clipboard) {
      throw new Error('Clipboard access is not available in this browser.');
    }
    await navigator.clipboard.writeText(String(value));
  }

  return { connect, push, compactSnapshot, copyText, accessKey, setAccessKey, clearAccessKey, autoSyncEnabled, setAutoSyncEnabled, autoSyncIntervalSeconds, setAutoSyncIntervalSeconds, browserCulture };
})();
