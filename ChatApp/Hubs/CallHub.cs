using System.Collections.Concurrent;
using ChatApp.Models;
using ChatApp.Services.Interfaces;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Hubs;

[Authorize]
public class CallHub : Hub
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPrivacyService _privacy;
    private readonly INotificationService _notifications;

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ActiveCalls = new();
    private static readonly ConcurrentDictionary<string, byte> AcceptedCalls = new();

    public CallHub(UserManager<ApplicationUser> userManager, IPrivacyService privacy, INotificationService notifications)
    {
        _userManager = userManager;
        _privacy = privacy;
        _notifications = notifications;
    }

    private string UserId => Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private static string UserGroup(string userId) => $"call-user:{userId}";
    private static string CallGroup(string roomId) => $"call:{roomId}";

    private static bool IsCallParticipant(string callId, string userId)
        => ActiveCalls.TryGetValue(callId, out var participants) && participants.ContainsKey(userId);

    public override async Task OnConnectedAsync()
    {
        if (!string.IsNullOrWhiteSpace(UserId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(UserId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var pair in ActiveCalls.ToArray())
        {
            if (pair.Value.TryRemove(UserId, out _))
            {
                await Clients.Group(CallGroup(pair.Key)).SendAsync("PeerLeft", UserId, Context.ConnectionId);
                if (pair.Value.IsEmpty) ActiveCalls.TryRemove(pair.Key, out _);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task CallUser(string receiverId, string callId, string type)
    {
        if (string.IsNullOrWhiteSpace(receiverId) || string.IsNullOrWhiteSpace(callId) || callId.Length > 100)
            throw new HubException("Dados da chamada inválidos.");
        if (type is not ("audio" or "video")) throw new HubException("Tipo de chamada inválido.");
        if (string.IsNullOrWhiteSpace(UserId) || receiverId == UserId) throw new HubException("Destinatário inválido.");

        var canCall = await _privacy.CanCallAsync(UserId, receiverId);
        if (!canCall) throw new HubException("Este utilizador não permite receber chamadas de si.");

        var receiver = await _userManager.FindByIdAsync(receiverId);
        var caller = await _userManager.FindByIdAsync(UserId);
        if (receiver == null || caller == null) throw new HubException("Utilizador da chamada não encontrado.");

        var participants = ActiveCalls.GetOrAdd(callId, _ => new ConcurrentDictionary<string, byte>());
        participants[UserId] = 0;
        participants[receiverId] = 0;

        await Clients.Group(UserGroup(receiverId)).SendAsync(
            "IncomingCall", callId, UserId,
            string.IsNullOrWhiteSpace(caller.FullName) ? "Utilizador" : caller.FullName,
            string.IsNullOrWhiteSpace(caller.ProfilePhotoUrl) ? "/images/default-avatar.png" : caller.ProfilePhotoUrl,
            type);
    }

    public async Task AnswerCall(string callerId, string callId, bool accepted)
    {
        if (!IsCallParticipant(callId, UserId) || !IsCallParticipant(callId, callerId))
            throw new HubException("Chamada inválida ou expirada.");

        if (!accepted) { ActiveCalls.TryRemove(callId, out _); } else { AcceptedCalls[callId]=0; }
        await Clients.Group(UserGroup(callerId)).SendAsync("CallAnswered", callId, accepted);
    }

    public async Task EndCall(string otherUserId, string callId)
    {
        if (!IsCallParticipant(callId, UserId) || !IsCallParticipant(callId, otherUserId)) return;
        var wasAccepted = AcceptedCalls.TryRemove(callId, out _);
        ActiveCalls.TryRemove(callId, out _);
        if (!wasAccepted) await _notifications.CreateAsync(otherUserId, NotificationType.MissedCall, "Chamada perdida", "Perdeste uma chamada.", callId);
        await Clients.Group(UserGroup(otherUserId)).SendAsync("CallEnded", callId);
    }

    public async Task JoinRoom(string roomId)
    {
        if (!IsCallParticipant(roomId, UserId)) throw new HubException("Não tens acesso a esta chamada.");
        await Groups.AddToGroupAsync(Context.ConnectionId, CallGroup(roomId));
        await Clients.OthersInGroup(CallGroup(roomId)).SendAsync("PeerJoined", UserId, Context.ConnectionId);
    }

    public async Task LeaveRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CallGroup(roomId));
        await Clients.OthersInGroup(CallGroup(roomId)).SendAsync("PeerLeft", UserId, Context.ConnectionId);
    }

    public async Task SendSignal(string targetConnectionId, string signalType, string payload)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionId) || string.IsNullOrWhiteSpace(signalType) || string.IsNullOrWhiteSpace(payload)) return;
        if (payload.Length > 200_000) throw new HubException("Sinalização demasiado grande.");
        await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", Context.ConnectionId, UserId, signalType, payload);
    }
}
