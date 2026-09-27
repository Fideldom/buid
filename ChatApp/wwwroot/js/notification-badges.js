(function () {
    "use strict";

    const BADGES_API = "/api/Notifications";

    let badgePollingInterval = null;
    let isInitialized = false;

    /*
     * ============================================================
     * UTILITÁRIOS
     * ============================================================
     */

    function getElement(selector) {
        return document.querySelector(selector);
    }

    function setBadge(selector, count) {
        const badge = getElement(selector);

        if (!badge) {
            return;
        }

        const safeCount = Number.isFinite(Number(count))
            ? Math.max(0, Number(count))
            : 0;

        if (safeCount <= 0) {
            badge.textContent = "";
            badge.hidden = true;
            badge.classList.remove("show");
            badge.setAttribute("aria-hidden", "true");
            return;
        }

        badge.textContent = safeCount > 99 ? "99+" : String(safeCount);
        badge.hidden = false;
        badge.classList.add("show");
        badge.setAttribute("aria-hidden", "false");
    }

    function getCountFromResponse(data) {
        if (typeof data === "number") {
            return data;
        }

        if (!data || typeof data !== "object") {
            return 0;
        }

        const possibleValues = [
            data.count,
            data.total,
            data.unreadCount,
            data.notificationsCount,
            data.messagesCount,
            data.meetingsCount
        ];

        for (const value of possibleValues) {
            const number = Number(value);

            if (Number.isFinite(number)) {
                return Math.max(0, number);
            }
        }

        return 0;
    }

    async function fetchJson(url, options = {}) {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store",
            ...options,
            headers: {
                Accept: "application/json",
                ...(options.headers || {})
            }
        });

        if (!response.ok) {
            throw new Error(
                `HTTP ${response.status} ao acessar ${url}`
            );
        }

        const contentType = response.headers.get("content-type") || "";

        if (!contentType.includes("application/json")) {
            return null;
        }

        return await response.json();
    }

    /*
     * ============================================================
     * NOTIFICAÇÕES
     *
     * Apenas estas notificações entram no badge/central:
     *
     * - MissedCall
     * - GroupInvite
     * - ChannelInvite
     * ============================================================
     */

    async function fetchNotificationCount() {
        try {
            const data = await fetchJson(
                `${BADGES_API}/count`
            );

            const count = getCountFromResponse(data);

            setBadge(
                '[data-notification-badge], #notificationBadge, .notification-badge',
                count
            );

            return count;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível obter o contador de notificações:",
                error
            );

            return 0;
        }
    }

    /*
     * ============================================================
     * MENSAGENS
     * ============================================================
     */

    async function fetchMessageCount() {
        try {
            const data = await fetchJson(
                `${BADGES_API}/messages-count`
            );

            const count = getCountFromResponse(data);

            setBadge(
                '[data-message-badge], #messageBadge, .message-badge',
                count
            );

            return count;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível obter o contador de mensagens:",
                error
            );

            return 0;
        }
    }

    /*
     * ============================================================
     * REUNIÕES
     * ============================================================
     */

    async function fetchMeetingCount() {
        try {
            const data = await fetchJson(
                `${BADGES_API}/meetings-count`
            );

            const count = getCountFromResponse(data);

            setBadge(
                '[data-meeting-badge], #meetingBadge, .meeting-badge',
                count
            );

            return count;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível obter o contador de reuniões:",
                error
            );

            return 0;
        }
    }

    /*
     * ============================================================
     * ATUALIZAÇÃO INDIVIDUAL
     * ============================================================
     */

    async function updateNotificationBadge() {
        return await fetchNotificationCount();
    }

    async function updateMessageBadge() {
        return await fetchMessageCount();
    }

    async function updateMeetingBadge() {
        return await fetchMeetingCount();
    }

    /*
     * ============================================================
     * ATUALIZAÇÃO COMPLETA
     * ============================================================
     */

    async function updateAllBadges() {
        await Promise.allSettled([
            fetchNotificationCount(),
            fetchMessageCount(),
            fetchMeetingCount()
        ]);
    }

    /*
     * ============================================================
     * MARCAR NOTIFICAÇÕES COMO LIDAS
     *
     * O centro de notificações contém apenas:
     * - chamadas perdidas
     * - convites de grupo
     * - convites de canal
     * ============================================================
     */

    async function markNotificationsAsRead() {
        try {
            const response = await fetch(
                `${BADGES_API}/read-all`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status} ao marcar notificações como lidas`
                );
            }

            setBadge(
                '[data-notification-badge], #notificationBadge, .notification-badge',
                0
            );

            return true;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível marcar notificações como lidas:",
                error
            );

            return false;
        }
    }

    /*
     * ============================================================
     * MARCAR UMA NOTIFICAÇÃO ESPECÍFICA COMO LIDA
     * ============================================================
     */

    async function markNotificationAsRead(notificationId) {
        if (!notificationId) {
            return false;
        }

        try {
            const response = await fetch(
                `${BADGES_API}/${encodeURIComponent(notificationId)}/read`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status} ao marcar notificação`
                );
            }

            await fetchNotificationCount();

            return true;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível marcar a notificação como lida:",
                error
            );

            return false;
        }
    }

    /*
     * ============================================================
     * MARCAR MENSAGENS COMO LIDAS
     * ============================================================
     */

    async function markMessagesAsRead() {
        try {
            const response = await fetch(
                `${BADGES_API}/messages/read-all`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status} ao marcar mensagens como lidas`
                );
            }

            setBadge(
                '[data-message-badge], #messageBadge, .message-badge',
                0
            );

            return true;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível marcar mensagens como lidas:",
                error
            );

            return false;
        }
    }

    /*
     * ============================================================
     * MARCAR REUNIÕES COMO LIDAS
     * ============================================================
     */

    async function markMeetingsAsRead() {
        try {
            const response = await fetch(
                `${BADGES_API}/meetings/read-all`,
                {
                    method: "POST",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status} ao marcar reuniões como lidas`
                );
            }

            setBadge(
                '[data-meeting-badge], #meetingBadge, .meeting-badge',
                0
            );

            return true;
        } catch (error) {
            console.warn(
                "[ChatBadges] Não foi possível marcar reuniões como lidas:",
                error
            );

            return false;
        }
    }

    /*
     * ============================================================
     * POLLING
     * ============================================================
     */

    function stopBadgePolling() {
        if (badgePollingInterval !== null) {
            clearInterval(badgePollingInterval);
            badgePollingInterval = null;
        }
    }

    function startBadgePolling() {
        stopBadgePolling();

        /*
         * Primeira atualização imediatamente.
         */
        updateAllBadges();

        /*
         * Atualização periódica.
         */
        badgePollingInterval = window.setInterval(
            function () {
                updateAllBadges();
            },
            10000
        );
    }

    /*
     * ============================================================
     * EVENTOS DO DOM
     * ============================================================
     */

    function setupNotificationEvents() {
        /*
         * Procura qualquer elemento que possa representar
         * a aba/botão das notificações.
         */
        const notificationSelectors = [
            "[data-notifications-tab]",
            "[data-open-notifications]",
            "#notificationsTab",
            "#notificationTab",
            "#btnNotifications",
            ".notifications-tab"
        ];

        const elements = document.querySelectorAll(
            notificationSelectors.join(",")
        );

        elements.forEach(function (element) {
            if (element.dataset.badgesBound === "true") {
                return;
            }

            element.dataset.badgesBound = "true";

            element.addEventListener("click", function () {
                /*
                 * O usuário abriu a central.
                 * As notificações exibidas nessa central
                 * devem deixar de contar como não lidas.
                 */
                markNotificationsAsRead();
            });
        });
    }

    function setupMessageEvents() {
        const selectors = [
            "[data-messages-tab]",
            "[data-open-messages]",
            "#messagesTab",
            "#messageTab",
            "#btnMessages",
            ".messages-tab"
        ];

        const elements = document.querySelectorAll(
            selectors.join(",")
        );

        elements.forEach(function (element) {
            if (element.dataset.badgesBound === "true") {
                return;
            }

            element.dataset.badgesBound = "true";

            element.addEventListener("click", function () {
                markMessagesAsRead();
            });
        });
    }

    function setupMeetingEvents() {
        const selectors = [
            "[data-meetings-tab]",
            "[data-open-meetings]",
            "#meetingsTab",
            "#meetingTab",
            "#btnMeetings",
            ".meetings-tab"
        ];

        const elements = document.querySelectorAll(
            selectors.join(",")
        );

        elements.forEach(function (element) {
            if (element.dataset.badgesBound === "true") {
                return;
            }

            element.dataset.badgesBound = "true";

            element.addEventListener("click", function () {
                markMeetingsAsRead();
            });
        });
    }

    function setupEvents() {
        setupNotificationEvents();
        setupMessageEvents();
        setupMeetingEvents();
    }

    /*
     * ============================================================
     * REALTIME — SIGNALR
     * ============================================================
     *
     * O ChatHub pode disparar:
     *
     * ReceiveNotification
     *
     * Não criamos uma nova conexão aqui.
     * Aproveitamos a conexão global caso ela exista.
     * ============================================================
     */

    function bindRealtimeEvents() {
        const connection =
            window.chatConnection ||
            window.chatHubConnection ||
            window.ChatConnection ||
            window.signalRConnection;

        if (!connection || typeof connection.on !== "function") {
            return false;
        }

        if (connection.__chatBadgesBound) {
            return true;
        }

        connection.__chatBadgesBound = true;

        connection.on(
            "ReceiveNotification",
            function () {
                /*
                 * Uma nova notificação chegou.
                 * Atualizamos somente o badge de notificações.
                 */
                updateNotificationBadge();
            }
        );

        connection.on(
            "NotificationReceived",
            function () {
                updateNotificationBadge();
            }
        );

        connection.on(
            "ReceiveMessage",
            function () {
                updateMessageBadge();
            }
        );

        connection.on(
            "MessageReceived",
            function () {
                updateMessageBadge();
            }
        );

        connection.on(
            "MeetingNotification",
            function () {
                updateMeetingBadge();
            }
        );

        connection.on(
            "MeetingReceived",
            function () {
                updateMeetingBadge();
            }
        );

        return true;
    }

    /*
     * ============================================================
     * MUTATIONS / EVENTOS PERSONALIZADOS
     * ============================================================
     */

    function setupCustomEvents() {
        document.addEventListener(
            "chatapp:notification-received",
            function () {
                updateNotificationBadge();
            }
        );

        document.addEventListener(
            "chatapp:notification-read",
            function () {
                updateNotificationBadge();
            }
        );

        document.addEventListener(
            "chatapp:message-received",
            function () {
                updateMessageBadge();
            }
        );

        document.addEventListener(
            "chatapp:message-read",
            function () {
                updateMessageBadge();
            }
        );

        document.addEventListener(
            "chatapp:meeting-received",
            function () {
                updateMeetingBadge();
            }
        );

        document.addEventListener(
            "chatapp:meeting-read",
            function () {
                updateMeetingBadge();
            }
        );
    }

    /*
     * ============================================================
     * VISIBILIDADE DA PÁGINA
     * ============================================================
     */

    function setupVisibilityEvents() {
        document.addEventListener(
            "visibilitychange",
            function () {
                if (document.visibilityState === "visible") {
                    updateAllBadges();
                }
            }
        );
    }

    /*
     * ============================================================
     * INICIALIZAÇÃO
     * ============================================================
     */

    function initialize() {
        if (isInitialized) {
            return;
        }

        isInitialized = true;

        setupEvents();
        setupCustomEvents();
        setupVisibilityEvents();

        /*
         * Tenta ligar ao SignalR global.
         *
         * Como o connection pode ser criado depois
         * deste script, fazemos algumas tentativas curtas.
         */
        let attempts = 0;

        const realtimeTimer = window.setInterval(
            function () {
                attempts++;

                if (bindRealtimeEvents() || attempts >= 20) {
                    clearInterval(realtimeTimer);
                }
            },
            500
        );

        startBadgePolling();
    }

    /*
     * ============================================================
     * API PÚBLICA
     * ============================================================
     */

    window.ChatBadges = {
        update: updateAllBadges,

        notifications: updateNotificationBadge,

        messages: updateMessageBadge,

        meetings: updateMeetingBadge,

        markNotificationsAsRead: markNotificationsAsRead,

        markNotificationAsRead: markNotificationAsRead,

        markMessagesAsRead: markMessagesAsRead,

        markMeetingsAsRead: markMeetingsAsRead,

        startPolling: startBadgePolling,

        stopPolling: stopBadgePolling
    };

    /*
     * ============================================================
     * START
     * ============================================================
     */

    if (
        document.readyState === "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initialize,
            {
                once: true
            }
        );
    } else {
        initialize();
    }
})();

