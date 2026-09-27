using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ChatApp.Hubs;

[Authorize]
public class GroupHub : Hub
{
    private readonly GroupService _groups;
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> CallParticipants = new();
    private static readonly ConcurrentDictionary<string, Guid> GroupCalls = new();

    public GroupHub(GroupService groups) => _groups = groups;

    private string UserId => Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private static string Room(Guid id) => $"group:{id}";
    private static string CallRoom(string id) => $"group-call:{id}";

    private async Task EnsureMember(Guid groupId)
    {
        if (string.IsNullOrWhiteSpace(UserId) || !await _groups.IsMemberAsync(groupId, UserId))
            throw new HubException("Não tens acesso a este grupo.");
    }

    public async Task JoinGroup(Guid groupId)
    {
        await EnsureMember(groupId);
        await Groups.AddToGroupAsync(Context.ConnectionId, Room(groupId));
    }

    public async Task LeaveGroup(Guid groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Room(groupId));
    }

    public async Task SendMessage(Guid groupId, string content)
    {
        await EnsureMember(groupId);
        var message = await _groups.SendMessageAsync(UserId, groupId, content);
        await Clients.Group(Room(groupId)).SendAsync("GroupMessageReceived", message);
    }

    public async Task StartGroupCall(Guid groupId, string callId, string type)
    {
        await EnsureMember(groupId);
        if (string.IsNullOrWhiteSpace(callId) || callId.Length > 100)
            throw new HubException("Sala inválida.");
        if (type is not ("audio" or "video"))
            throw new HubException("Tipo de chamada inválido.");

        GroupCalls[callId] = groupId;
        await Clients.Group(Room(groupId)).SendAsync("GroupCallStarted", new { groupId, callId, type, startedBy = UserId });
    }

    public async Task JoinCall(string callId)
    {
        if (string.IsNullOrWhiteSpace(callId) || callId.Length > 100)
            throw new HubException("Sala inválida.");
        if (!GroupCalls.TryGetValue(callId, out var groupId))
            throw new HubException("A chamada não existe ou já terminou.");
        await EnsureMember(groupId);

        var room = CallParticipants.GetOrAdd(callId, _ => new ConcurrentDictionary<string, string>());
        var existing = room.ToArray();
        room[Context.ConnectionId] = UserId;

        await Groups.AddToGroupAsync(Context.ConnectionId, CallRoom(callId));
        foreach (var pair in existing)
            await Clients.Client(Context.ConnectionId).SendAsync("GroupCallPeerJoined", pair.Value, pair.Key);

        await Clients.OthersInGroup(CallRoom(callId)).SendAsync("GroupCallPeerJoined", UserId, Context.ConnectionId);
    }

    public async Task LeaveCall(string callId)
    {
        if (string.IsNullOrWhiteSpace(callId)) return;
        if (CallParticipants.TryGetValue(callId, out var room))
        {
            room.TryRemove(Context.ConnectionId, out _);
            if (room.IsEmpty)
            {
                CallParticipants.TryRemove(callId, out _);
                GroupCalls.TryRemove(callId, out _);
            }
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CallRoom(callId));
        await Clients.Group(CallRoom(callId)).SendAsync("GroupCallPeerLeft", UserId, Context.ConnectionId);
    }

    public async Task SendCallSignal(string targetConnectionId, string signalType, string payload)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionId) || string.IsNullOrWhiteSpace(signalType) || string.IsNullOrWhiteSpace(payload)) return;
        if (payload.Length > 200_000) throw new HubException("Sinalização demasiado grande.");

        var sameCall = CallParticipants.Any(pair =>
            pair.Value.ContainsKey(Context.ConnectionId) && pair.Value.ContainsKey(targetConnectionId));
        if (!sameCall) throw new HubException("Ligação de chamada inválida.");

        await Clients.Client(targetConnectionId).SendAsync("GroupCallSignal", Context.ConnectionId, UserId, signalType, payload);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var pair in CallParticipants.ToArray())
        {
            if (pair.Value.TryRemove(Context.ConnectionId, out _))
            {
                await Clients.Group(CallRoom(pair.Key)).SendAsync("GroupCallPeerLeft", UserId, Context.ConnectionId);
                if (pair.Value.IsEmpty)
                {
                    CallParticipants.TryRemove(pair.Key, out _);
                    GroupCalls.TryRemove(pair.Key, out _);
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
