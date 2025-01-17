﻿/*
 *  Copyright 2018-2021 Nord Pool.
 *  This library is intended to aid integration with Nord Pool’s Intraday API and comes without any warranty. Users of this library are responsible for separately testing and ensuring that it works according to their own standards.
 *  Please send feedback to idapi@nordpoolgroup.com.
 */

using System.Reflection;
using System.Text;
using log4net;
using Newtonsoft.Json;
using NPS.ID.PublicApi.Client.Connection;
using NPS.ID.PublicApi.Client.Utilities;
using Stomp.Net.Stomp.Protocol;
using Nordpool.ID.PublicApi.v1.Trade.Request;
using Nordpool.ID.PublicApi.v1.Order.Request;
using Nordpool.ID.PublicApi.v1.Command;
using System.Threading.Channels;

namespace NPS.ID.PublicApi.Client.Subscription
{

    public class StompMessageEventArgs : EventArgs
    {
        public string MessageContent { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }

    public class TradingService
    {
        private readonly WebSocketSettings _webSocketSettings;
        private readonly BasicCredentials _ssoCredentials;
        private StompConnector _stompConnector;

        // public delegate void HandleStompMessageContent(string messageContent);
        
        private Dictionary<string, Channel<StompFrame>> _subscriptionHandles;

        private EventHandler<StompMessageEventArgs> allMessagesHandler;

        public bool IsConnected { get; private set; }

        private AutoResetEvent _stompConnected = new AutoResetEvent(false);

        public WebSocketSettings WebSocketSettings => _webSocketSettings;

        private static readonly Dictionary<Topic, string> DestinationPaths = new Dictionary<Topic, string>()
        {
            {Topic.Capacities, "/capacities" },
            {Topic.LocalView, "/localview" },
            {Topic.DeliveryAreas, "/deliveryAreas" },
            {Topic.Configuration, "/configuration" },
            {Topic.Contracts, "/contracts"  },
            {Topic.PrivateTrade, "/privateTrade" },
            {Topic.Ticker, "/ticker" },
            {Topic.PublicStatistics, "/publicStatistics"} ,
            {Topic.OrderExecutionReport, "/orderExecutionReport" },
            {Topic.HeartbeatPing, "/heartbeatping" }

        };

        private static readonly Dictionary<SubscriptionType, string> SubscriptionTypesStrings =
            new Dictionary<SubscriptionType, string>()
            {
                {SubscriptionType.Conflated, "/conflated"},
                {SubscriptionType.Empty, String.Empty},
                {SubscriptionType.Streaming, "/streaming"}
            };

        private static readonly ILog _logger =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public TradingService(WebSocketSettings webSocketSettings, BasicCredentials ssoCredentials)
        {
            _webSocketSettings = webSocketSettings;
            _ssoCredentials = ssoCredentials;
            _subscriptionHandles = new();
        }

        public void SubscribeAll(EventHandler<StompMessageEventArgs> handler)
        {
            allMessagesHandler += handler;
        }

        public void Subscribe(Subscription subscription, EventHandler<StompMessageEventArgs> callBack)
        {
            if (!IsConnected)
                throw new SubscriptionFailedException(
                    "Failed to subscribe because no connection is established! Connect first!");


            Channel<StompFrame> frameQueue = Channel.CreateUnbounded<StompFrame>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true });

            _subscriptionHandles.Add(subscription.Id, frameQueue);

            _ = StartToListenTopic(frameQueue, callBack);

            var destination = BuildFullTopic(subscription);

            var subscribeFrame = StompMessageFactory.SubscribeFrame(destination, subscription.Id);

            _stompConnector.Send(subscribeFrame);

        }

        public void Connect(string authToken, int timeoutInMilliseconds = 20000)
        {
            _logger.Info("Connecting Trading Service...");
            _logger.Info($"Host: {_webSocketSettings.Host}, Uri: {_webSocketSettings.Uri}, Port: {_webSocketSettings.GetUsedPort()}, Use Ssl: {_webSocketSettings.UseSsl}, HeartbeatSending: {_webSocketSettings.HeartbeatOutgoingInterval}");
            _stompConnector = new StompConnector(_webSocketSettings.Host, _webSocketSettings.Uri,
                _webSocketSettings.GetUsedPort(), _webSocketSettings.UseSsl, _webSocketSettings.HeartbeatOutgoingInterval);
            _stompConnector.StompConnectionEstablished += StompConnectorOnStompConnectionEstablished;
            _stompConnector.StompConnectionClosed += StompConnectorOnStompConnectionClosed;
            _stompConnector.StompFrameReceived += StompConnectorOnStompFrameReceived;
            _stompConnector.Connect(authToken);
            var connectedEvent = _stompConnected.WaitOne(timeoutInMilliseconds);
            if (connectedEvent)
            {
                IsConnected = true;
                _logger.Info("Trading Service connected.");
            }
            else
            {
                _logger.Error($"Trading service was unable to connect within {timeoutInMilliseconds} milliseconds!");
            }

        }

        private async Task StartToListenTopic(Channel<StompFrame> frameQueue, EventHandler<StompMessageEventArgs> subscriptionHandlePairHandler)
        {
            await Task.Yield();

            await foreach (var frame in frameQueue.Reader.ReadAllAsync())
                ProcessOneFrame(frame, subscriptionHandlePairHandler);
        }

        private void ProcessOneFrame(StompFrame frame, EventHandler<StompMessageEventArgs> subscriptionHandlePairHandler)
        {
            var content = frame.Content;
            var contentEncoding = frame.Properties.FirstOrDefault(r => r.Key == "Content-Encoding").Value;
           
            var gzipped = string.Equals(contentEncoding, "GZIP", StringComparison.InvariantCultureIgnoreCase);
            if (gzipped)
                content = GzipCompressor.Decompress(frame.Content);

            var headers = frame.Properties.ToDictionary(p => p.Key, p => p.Value);
            var args = new StompMessageEventArgs()
            {
                Headers = headers,
                MessageContent = Encoding.UTF8.GetString(content),
            };
            allMessagesHandler?.Invoke(this, args);
            subscriptionHandlePairHandler?.Invoke(this, args);              
        }

        private void StompConnectorOnStompFrameReceived(object sender, StompConnector.StompFrameReceivedEventArgs stompFrameReceivedEventArgs)
        {
            var frame = stompFrameReceivedEventArgs.Frame;

            if (frame.Command != StompCommandConstants.Message)
                return;

            if (!frame.HasProperty("subscription"))
                return;

            //Find related subscription
            var subscriptionName = frame.Properties["subscription"];
            if (!_subscriptionHandles.TryGetValue(subscriptionName, out var subscriptionHandlePair))
                return;

            subscriptionHandlePair.Writer.TryWrite(frame);
        }

        private static string GetDestinationPathFromTopic(Topic topic)
        {
            return DestinationPaths[topic];
        }

        private string BuildFullTopic(Subscription subscription)
        {
            var fullTopic = $"/user/{_ssoCredentials.UserName}/{subscription.Version}{SubscriptionTypesStrings[subscription.SubscriptionType]}{GetDestinationPathFromTopic(subscription.Topic)}";

            switch (subscription.Topic)
            {
                case Topic.LocalView:
                case Topic.Capacities:
                case Topic.PublicStatistics:
                    return $"{fullTopic}/{subscription.Area}{IsGzipped(subscription)}";
                case Topic.HeartbeatPing:
                case Topic.Configuration:
                case Topic.OrderExecutionReport:
                case Topic.PrivateTrade:
                case Topic.Ticker:
                case Topic.DeliveryAreas:
                case Topic.Contracts:
                    return $"{fullTopic}{IsGzipped(subscription)}";
                default:
                    _logger.Error("Invalid subscription type!");
                    return null;
            }
        }

        private void StompConnectorOnStompConnectionClosed(object sender, EventArgs eventArgs)
        {

        }

        private void StompConnectorOnStompConnectionEstablished(object sender, EventArgs eventArgs)
        {

            _stompConnected.Set();
        }

        public void SendEntryOrderRequest(OrderEntryRequest request)
        {
            var orderJson = JsonConvert.SerializeObject(request);
            var orderFrame = StompMessageFactory.SendFrame(orderJson, "/v1/orderEntryRequest");
            SendMessage(orderFrame);
        }

        public void SendModificationOrderRequest(OrderModificationRequest request)
        {
            var orderJson = JsonConvert.SerializeObject(request);
            var orderFrame = StompMessageFactory.SendFrame(orderJson, "/v1/orderModificationRequest");
            SendMessage(orderFrame);
        }

        public void SendTradeCancellationRequest(TradeRecallRequest request)
        {
            var tradeRecallJson = JsonConvert.SerializeObject(request);
            var tradeRecallFrame = StompMessageFactory.SendFrame(tradeRecallJson, "/v1/tradeCancellationRequest");
            SendMessage(tradeRecallFrame);
        }

        public void SendLogoutCommand()
        {
            var logoutFrame = StompMessageFactory.SendFrame(@"{""type"":""LOGOUT""}", "/v1/command");

            SendMessage(logoutFrame);
        }


        public void SendTokenRefresh(TokenRefreshCommand request)
        {
            var f = StompMessageFactory.SendFrame(@"{""type"":""TOKEN_REFRESH""," +
                "\"oldToken\":\"" + request.OldToken + "\"," +
                "\"newToken\":\"" + request.NewToken + "\"" +
                "}", "/v1/command");

            _logger.Info("Sending token refresh: " + f.ToString());
            SendMessage(f);
        }

        private void SendMessage(StompFrame frame)
        {
            _stompConnector.Send(frame);
        }

        private static string IsGzipped(Subscription subscription)
        {
            return subscription.IsGZipped ? "/gzip" : "";
        }


        public void Unsubscribe()
        {
            allMessagesHandler = null;
            var oldHandlers = _subscriptionHandles;
            _subscriptionHandles = new();

            oldHandlers.Clear();
        }

    }
}