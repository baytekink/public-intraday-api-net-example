﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Extend;
using log4net;
using Newtonsoft.Json;
using NPS.public_intraday_api_example.Services.Connection;
using NPS.public_intraday_api_example.Services.Subscription.DTO;
using NPS.public_intraday_api_example.Utilities;
using Stomp.Net.Stomp.Protocol;

namespace NPS.public_intraday_api_example.Services.Subscription
{
    public class TradingService
    {
        private readonly WebSocketSettings _webSocketSettings;
        private readonly BasicCredentials _ssoCredentials;
        private StompConnector _stompConnector;

        public delegate void HandleStompMessageContent(string messageContent);

        private readonly Dictionary<Subscription, HandleStompMessageContent> _subscriptionHandles;

        public bool IsConnected { get; private set; }

        private AutoResetEvent _stompConnected = new AutoResetEvent(false);

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
            {Topic.OrderExecutionReport, "/orderExecutionReport" }
            
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
            _subscriptionHandles = new Dictionary<Subscription, HandleStompMessageContent>();
        }

        public void Subscribe(Subscription subscription, HandleStompMessageContent callBack)
        {
            if (!IsConnected)
                throw new SubscriptionFailedException(
                    "Failed to subscribe because no connection is established! Connect first!");


            _subscriptionHandles.Add(subscription, callBack);

            var destination = BuildFullTopic(subscription);

            var subscribeFrame = StompMessageFactory.SubscribeFrame(destination, subscription.Id);

            _stompConnector.Send(subscribeFrame);

        }

        public void Connect(string authToken, int timeoutInMilliseconds = 20000)
        {
            _logger.Info("Connecting Trading Service...");
            _logger.Info($"Host: {_webSocketSettings.Host}, Uri: {_webSocketSettings.Uri}, Port: {_webSocketSettings.Port}, Protocol: {_webSocketSettings.Protocol}");
            _stompConnector = new StompConnector(_webSocketSettings.Host, _webSocketSettings.Uri,
                _webSocketSettings.Port, _webSocketSettings.Protocol);
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

        private void StompConnectorOnStompFrameReceived(object sender, StompConnector.StompFrameReceivedEventArgs stompFrameReceivedEventArgs)
        {
            var frame = stompFrameReceivedEventArgs.Frame;
            if (frame.Command == StompCommmandConstants.Message)
            {

                if (frame.HasProperty("subscription"))
                {
                    //Find related subscription
                    var subscriptionHandlePair =
                        _subscriptionHandles.First(x => x.Key.Id == frame.Properties["subscription"]);

                    subscriptionHandlePair.Value?.Invoke(Encoding.UTF8.GetString(frame.Content));

                }
            }
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
            var orderFrame =  StompMessageFactory.SendFrame(orderJson, "/v1/orderEntryRequest");
            SendMessage(orderFrame);
        }

        public void SendModificationOrderRequest()
        {
        }

        public void SendLogoutCommand()
        {
            var logoutFrame = StompMessageFactory.SendFrame(@"{""type"":""LOGOUT""}", "/v1/command"); 
            SendMessage(logoutFrame);
        }

        private void SendMessage(StompFrame frame)
        {
            _stompConnector.Send(frame);
        }

        private static string IsGzipped(Subscription subscription)
        {
            return subscription.IsGZipped ? "/gzip" : "";
        }



    }
}