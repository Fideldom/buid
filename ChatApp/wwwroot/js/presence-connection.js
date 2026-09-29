"use strict";
(function () {
  if (typeof signalR === "undefined") return;
  if (window.ChatPresenceConnectionStarted) return;
  window.ChatPresenceConnectionStarted = true;

  let connection = window.ChatConnection;
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/chat")
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();
    window.ChatConnection = connection;
  }

  window.ChatConnection = connection;
  window.ChatPresenceConnection = connection;

  async function start() {
    if (connection.state === signalR.HubConnectionState.Disconnected) {
      try {
        await connection.start();
      } catch (e) {
        console.warn("[Presence] ligação falhou", e);
        setTimeout(start, 3000);
        return;
      }
    }
    window.ChatPresence?.register?.(connection);
    window.ChatPresence?.refresh?.();
  }

  connection.onreconnected(() => {
    window.ChatPresence?.register?.(connection);
    window.ChatPresence?.refresh?.();
  });
  document.addEventListener("DOMContentLoaded", start, { once: true });
})();
