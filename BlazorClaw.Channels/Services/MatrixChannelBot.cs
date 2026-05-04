using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Utils;
using Matrix.Sdk;
using Matrix.Sdk.Core.Domain.RoomEvent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.Channels.Services
{

    public class MatrixChannelBot(PathHelper pathHelper, SystemJsonVaultProvider vault, MatrixClientFactory factory, ILogger<MatrixChannelBot> logger) : AbstractConfigChannelBot<MatrixBotEntry>("Matrix")
    {
        internal IMatrixClient? Client { get; private set; }

        public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Client?.SendMessageAsync(channelId.ChannelId, message.Text) ?? Task.CompletedTask;
        }

        public override async Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            if (Client == null) throw new InvalidOperationException("Not configured");
            var content = message.Text;

            foreach (var item in message.Contents)
            {
                if (item is UriContent uri)
                {
                    var uriStr = uri.Uri.ToString();
                    var mInfo = pathHelper.GetMediaFile(uriStr);
                    if (mInfo == null) continue;
                    using var stream = mInfo.GetStream();
                    var data = await stream.ReadToBytesAsync();

                    if (uri.MediaType.StartsWith("image/"))
                    {
                        await Client.SendImageAsync(channelId.ChannelId, mInfo.FileName, data);
                    }
                    else
                    {
                        await Client.SendFileAsync(channelId.ChannelId, mInfo.FileName, data);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                var i = 0;
                foreach (var item in SplitMessageHybrid(content))
                {
                    if (i++ > 0) await Task.Delay(1000, cancellationToken);
                    await Client.SendMessageAsync(channelId.ChannelId, item);
                }
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (Client == null || Config == null) throw new InvalidOperationException("Not configured");
            var homeserver = new Uri(Config.Homeserver ?? "https://matrix.org", UriKind.Absolute);

            var token = await vault.GetSecretAsync($"Matrix_{Config.UserId}");
            var userId = Config.UserId;

            if (token != null)
            {
                await Client.LoginAsync(homeserver, token.Secret, userId);
            }
            else
            {
                var password = Config.Password;
                var ret = await Client.LoginAsync(homeserver, userId, password, BotId);
                if (ret != null)
                {
                    await vault.SetSecretAsync($"Matrix_{Config.UserId}", ret.AccessToken);
                }
            }
            Client.Start();
        }

        public override Task StopAsync(CancellationToken cancellationToken = default)
        {
            Client?.Stop();
            return Task.CompletedTask;
        }

        protected override ValueTask<bool> ConfigureAsync()
        {
            if (Client != null) Client.OnMatrixRoomEventsReceived -= HandleUpdate;
            Client = factory.Create();
            Client.OnMatrixRoomEventsReceived += HandleUpdate;
            return ValueTask.FromResult(true);
        }

        private async void HandleUpdate(object? sender, MatrixRoomEventsEventArgs eventArgs)
        {
            if (sender is not IMatrixClient client) return;
            try
            {
                foreach (var roomEvent in eventArgs.MatrixRoomEvents)
                {
                    if (client.UserId != roomEvent.SenderUserId)
                    {
                        if (roomEvent is TextMessageEvent textMessageEvent)
                        {
                            logger.LogInformation("Matrix received message in {RoomId}", roomEvent.RoomId);
                            await ProcessMatrixMessage(roomEvent.RoomId, roomEvent.SenderUserId, textMessageEvent.Message);
                        }
                        else if (roomEvent is CreateRoomEvent crRoomEvent)
                        {
                            logger.LogInformation("Matrix received CreateRoom event in {RoomId}", crRoomEvent.RoomId);
                            await client.LeaveRoomAsync(crRoomEvent.RoomId);

                            var ret = await client.CreateTrustedPrivateRoomAsync([crRoomEvent.SenderUserId]);
                            await client.SendMessageAsync(ret.RoomId, "Hallo");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
        }

        private async Task ProcessMatrixMessage(string roomId, string senderUserId, string message)
        {
            try
            {
                await Client!.SendTypingSignal(roomId, true);
                OnMessageReceived(new ChannelSession(this, roomId, senderUserId), message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
            finally
            {
                await Client!.SendTypingSignal(roomId, false);
            }
        }
    }
    public class MatrixBotEntry : BotEntry
    {
        public string Homeserver { get; set; } = "https://matrix.org";
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

}
