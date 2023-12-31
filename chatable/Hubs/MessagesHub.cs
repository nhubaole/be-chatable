﻿using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Helper;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using System;
using System.Security.Claims;

namespace chatable.Hubs
{
    [Authorize]
    public sealed class MessagesHub : Hub
    {
        private readonly Client _supabaseClient;
        private IHttpContextAccessor _httpContextAccessor;


        public MessagesHub(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        public async Task SendPeerMessage(String toUsername, String messageType, String content)
        {
            var senderId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var response = await _supabaseClient.From<Connection>().Where(x => x.UserId == toUsername).Get();
            var receiver = response.Models.FirstOrDefault();

            var messageRes = new MessageResponse()
            {
                SenderId = senderId,
                MessageType = messageType,
                Content = content,
                SentAt = DateTime.UtcNow,
            };

            await Clients
            .Client(receiver.ConnectionId)
            .SendAsync("MessageReceived", messageRes);

            String conversationId = await getConversationId(senderId, receiver.UserId);

            Message message = new Message()
            {
                MessageId = Guid.NewGuid(),
                SenderId = messageRes.SenderId,
                MessageType = messageRes.MessageType,
                Content = messageRes.Content,
                SentAt = messageRes.SentAt,
                ConversationId = conversationId
            };

            var responseInsertMsg = await _supabaseClient.From<Message>().Insert(message);
            var insertedMsg = responseInsertMsg.Models.FirstOrDefault();

            updateConversation(conversationId, insertedMsg);
        }

        public async Task SendGroupMessage(String groupId, String messageType, String content)
        {
            var senderId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var response = await _supabaseClient.From<GroupConnection>().Where(x => x.GroupId == groupId).Get();
            var receiver = response.Models.FirstOrDefault();

            var messageRes = new MessageResponse()
            {
                SenderId = senderId,
                MessageType = messageType,
                Content = content,
                SentAt = DateTime.UtcNow,
                GroupId = groupId
            };

            await Clients
            .GroupExcept(receiver.ConnectionId, Context.ConnectionId)
            .SendAsync("MessageReceivedFromGroup", messageRes);

            String conversationId = await getGroupConversationId(receiver.GroupId);

            Message message = new Message()
            {
                MessageId = Guid.NewGuid(),
                SenderId = messageRes.SenderId,
                MessageType = messageRes.MessageType,
                Content = messageRes.Content,
                SentAt = messageRes.SentAt,
                ConversationId = conversationId
            };

            var responseInsertMsg = await _supabaseClient.From<Message>().Insert(message);
            var insertedMsg = responseInsertMsg.Models.FirstOrDefault();

            updateConversation(conversationId, insertedMsg);
        }

        public async Task<String> getConversationId(String senderId, String receiverId)
        {
            try
            {
                String opt1 = senderId + "_" + receiverId;
                String opt2 = receiverId + "_" + senderId;
                var response = await _supabaseClient.From<Conversation>()
                                                  .Where(x => x.ConversationId == opt1 || x.ConversationId == opt2)
                                                  .Get();
                var conversation = response.Models.FirstOrDefault();
                if (conversation != null)
                {
                    return conversation.ConversationId;
                }
                else
                {
                    await _supabaseClient
                    .From<Conversation>()
                    .Insert(
                    new Conversation
                    {
                        ConversationId = $"{senderId}_{receiverId}",
                        ConversationType = "Peer",
                        LastMessage = Guid.Empty,
                        UnreadMessageCount = 0
                    }
                    );
                    return $"{senderId}_{receiverId}";
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Exception in getConversationId: {ex.Message}");
                throw;
            }
        }

        public async Task<String> getGroupConversationId(String groupId)
        {
            try
            {
                var response = await _supabaseClient.From<Conversation>()
                                                  .Where(x => x.ConversationId == groupId)
                                                  .Get();
                var conversation = response.Models.FirstOrDefault();
                if (conversation != null)
                {
                    return conversation.ConversationId;
                }
                else
                {
                    await _supabaseClient
                    .From<Conversation>()
                    .Insert(
                    new Conversation
                    {
                        ConversationId = groupId,
                        ConversationType = "Group",
                        LastMessage = Guid.Empty,
                        UnreadMessageCount = 0
                    }
                    );
                    return groupId;
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Exception in getGroupConversationId: {ex.Message}");
                throw;
            }
        }

        public async void updateConversation(String conversationId, Message lastMessage)
        {
            await _supabaseClient.From<Conversation>()
                   .Where(x => x.ConversationId == conversationId)
                   .Set(x => x.LastMessage, lastMessage.MessageId)
                   .Update();
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userConnectionId = Context.ConnectionId;
            if (userId is null) return;
            Console.WriteLine($"---> {userId} just joined the chat");

            var updateRes = await _supabaseClient
                                .From<Connection>()
                                .Where(x => x.UserId == userId)
                                .Set(x => x.ConnectionId, userConnectionId)
                                .Update();
            if (updateRes.Models.Count == 0)
            {
                var insertRes = await _supabaseClient
                                    .From<Connection>()
                                    .Insert(new Connection { UserId = userId, ConnectionId = userConnectionId });
            }
            var updateStatusRes = await _supabaseClient
                                .From<Connection>()
                                .Where(x => x.UserId == userId)
                                .Set(x => x.OnlineStatus, "online")
                                .Update();

            //restore group connections
            var res = await _supabaseClient.From<GroupParticipants>().Where(x => x.MemberId == userId).Get();
            var groups = res.Models;
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    var connectionRes = await _supabaseClient.From<GroupConnection>().Where(x => x.GroupId == group.GroupId).Get();
                    var groupConnection = connectionRes.Models.FirstOrDefault();
                    await Groups.AddToGroupAsync(userConnectionId, groupConnection.ConnectionId);
                    //Console.WriteLine($"---> {userId} just joined the group {group.GroupId}");
                }
            }

        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            //var username = Context.User.Identity.Name;
            if (userId is null) return;
            Console.WriteLine($"---> {userId} left the chat right now");
            var updateStatusRes = await _supabaseClient
                                .From<Connection>()
                                .Where(x => x.UserId == userId)
                                .Set(x => x.OnlineStatus, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
                                .Update();
        }

    }
}

/*
nhubaole
LeBaoNhu71!

test
Test1234@
*/
