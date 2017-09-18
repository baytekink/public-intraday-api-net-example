﻿using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema.Generation;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NPS.ID.PublicApi.Client.Connection;
using NPS.ID.PublicApi.Client.Security;
using NPS.ID.PublicApi.Client.Subscription;
using NPS.ID.PublicApi.Client.Utilities;
using NPS.ID.PublicApi.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPS.ID.PublicApi.Client.WinFormsExample
{
    public partial class Form1 : Form
    {
        private static ILog _logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const int DemoArea = 2;

        private ContractRow sampleContract = null;
        private ConfigurationRow currentConfiguration = null;


        public Form1()
        {
            InitializeComponent();
        }

        private TradingService tradingService;

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                Log("Starting to connect trading service..");
                // Authorize to get auth token
                var token = await AuthorizeToSSOService();
                Log($"Got token {token}");
                // Connect to trading service
                tradingService = ConnectToTradingService(token);
                Log($"Connected to Trading Service");
                // Subscribe to topics
                SubscribeToServices(tradingService);
                Log($"Subscribed to services..");

            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.ToString());
            }
        }
        delegate void SetTextCallback(string text);
        private void Log(string text)
        {
            if (this.textBoxLog.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(Log);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBoxLog.Text += text + Environment.NewLine;
            }

        }

        /// <summary>
        /// Returns the auth token after authorizing.
        /// </summary>
        /// <returns></returns>
        public async Task<string> AuthorizeToSSOService()
        {
            var credentials = ReadUserCredentials();
            var ssoSettings = ReadSSOSettings();
            var ssoService = new SSOService(ssoSettings);

            return await ssoService.GetAuthToken(credentials.UserName, credentials.Password);
        }

        /// <summary>
        /// Creates instance of trading service and connects to it.
        /// </summary>
        /// <param name="authToken"></param>
        /// <returns>connected trading service</returns>
        public TradingService ConnectToTradingService(string authToken)
        {
            var wsSettings = ReadWebSocketSettings();
            var credentials = ReadUserCredentials();
            var tradingService = new TradingService(wsSettings, credentials);

            tradingService.Connect(authToken);

            return tradingService;
        }

        /// <summary>
        /// Subscribes to different topics with connected TradingService
        /// </summary>
        /// <param name="tradingService"></param>
        public void SubscribeToServices(TradingService tradingService)
        {
            if (!tradingService.IsConnected)
                throw new InvalidOperationException("Trading service not connected! Can't subscribe to any topics.");

            var apiVersion = ConfigurationManager.AppSettings["api-version"];

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.Ticker)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .Build(), TickerCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.DeliveryAreas)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .WithArea(DemoArea)
                .Build(), DeliveryAreasCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.OrderExecutionReport)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .Build(), OrderExecutionCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.Configuration)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Empty)
                .Build(), ConfigurationCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.Contracts)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .Build(), ContractsCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.LocalView)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .WithArea(DemoArea)
                .WithIsGzipped(true)
                .Build(), LocalViewCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.PrivateTrade)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .Build(), PrivateTradeCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.PublicStatistics)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Conflated)
                .WithArea(DemoArea)
                .Build(), PublicStatisticsCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.Capacities)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .WithArea(DemoArea)
                .Build(), CapacitiesCallBack);

            tradingService.Subscribe(Subscription.Subscription.Builder()
                .WithTopic(Topic.HeartbeatPing)
                .WithVersion(apiVersion)
                .WithSubscriptionType(SubscriptionType.Streaming)
                .WithArea(DemoArea)
                .Build(), HeartbeatCallBack);

            try
            {
                Thread.Sleep(3000);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }

        private void HeartbeatCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Heartbeat");
            var heartbeatData = JsonHelper.DeserializeData<List<PublicStatisticRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(heartbeatData));
        }

        private void CapacitiesCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Capacities");
            var capacitiesData = JsonHelper.DeserializeData<List<PublicStatisticRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(capacitiesData));
        }

        private void PublicStatisticsCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Public Statistics");
            var publicStatisticsData = JsonHelper.DeserializeData<List<PublicStatisticRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(publicStatisticsData));
        }

        private void PrivateTradeCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Private Trade");
            var privateTradesData = JsonHelper.DeserializeData<List<PrivateTradeRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(privateTradesData));
        }

        private void LocalViewCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Local View", true);
            var localViewData = JsonHelper.DeserializeData<List<LocalViewRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(localViewData));
        }

        private void ConfigurationCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Configuration");
            var configurationData = JsonHelper.DeserializeData<List<ConfigurationRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(configurationData));
            if(currentConfiguration == null)
                currentConfiguration = configurationData.FirstOrDefault();
        }

        private void ContractsCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Contracts");
            var contractsData = JsonHelper.DeserializeData<List<ContractRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(contractsData));
            if(sampleContract == null) { 
                sampleContract = contractsData.FirstOrDefault(r=> r.State == ContractRowState.ACTI && r.DlvryStart > DateTimeOffset.Now.AddHours(3) && r.DlvryStart > DateTimeOffset.Now.AddHours(5));
                Log($"Sample contract: {Environment.NewLine}{JsonHelper.SerializeObjectPrettyPrinted(contractsData)}");
            }
        }

        private void OrderExecutionCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Order Execution Report");
            var orderExecutionsData = JsonHelper.DeserializeData<List<OrderExecutionReport>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(orderExecutionsData));
        }

        private void DeliveryAreasCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Delivery Areas");
            var deliveryAreasData = JsonHelper.DeserializeData<List<DeliveryAreaRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(deliveryAreasData));

        }

        private void TickerCallBack(string messageContent)
        {
            ShowMessage(messageContent, "Ticker");
            var tickerData = JsonHelper.DeserializeData<List<PublicTradeRow>>(messageContent);
            Log(JsonHelper.SerializeObjectPrettyPrinted(tickerData));
        }



        private static OrderEntryRequest SampleIncorrectOrderRequest()
        {
            var request = new OrderEntryRequest()
            {
                RequestId = Guid.NewGuid().ToString(),
                RejectPartially = false,

                Orders = new List<OrderEntry>
                {
                    new OrderEntry()
                    {
                        ClientOrderId = Guid.NewGuid().ToString(),
                         PortfolioId = "Z00001-5"

                    },
                }
            };

            return request;
        }

        private OrderEntryRequest SampleOrderRequest()
        {
            var portFolio = currentConfiguration.Portfolios.Last();
            var request = new OrderEntryRequest()
            {
                RequestId = Guid.NewGuid().ToString(),
                RejectPartially = false,
                Orders = new List<OrderEntry>
                {
                    new OrderEntry()
                    {
                        ClientOrderId = Guid.NewGuid().ToString(),
                        PortfolioId  = portFolio.Id,
                        Side = OrderEntrySide.SELL,
                        ContractIds = new List<string> { sampleContract.ContractId },
                        OrderType = OrderEntryOrderType.LIMIT,
                        Quantity = 3000,
                        State = OrderEntryState.ACTI,
                        UnitPrice = 2500,
                        TimeInForce = OrderEntryTimeInForce.GFS,
                        DeliveryAreaId = portFolio.Areas.First().AreaId,
                        //ExecutionRestriction = OrderEntryExecutionRestriction.AON,
                        //ExpireTime = DateTimeOffset.Now.AddHours(6)
                    },
                }
            };

            return request;
        }
        private void ShowMessage(string message, string fromTopic, bool isGzipped = false)
        {
            Log($"Message from \"{fromTopic}\" topic");

            //var messageContent = isGzipped ? GzipCompressor.Decompress(message) : message;
            //Log($"JSON: {messageContent}");
        }

        private BasicCredentials ReadUserCredentials()
        {
            var credentials = new BasicCredentials()
            {
                UserName = ConfigurationManager.AppSettings["sso-user"],
                Password = ConfigurationManager.AppSettings["sso-password"]

            };

            Log($"Credentials read from App.config. User: {credentials.UserName} Password: {credentials.Password}");

            return credentials;
        }

        private SSOSettings ReadSSOSettings()
        {
            var ssoSettings = new SSOSettings()
            {
                ClientId = ConfigurationManager.AppSettings["sso-clientId"],
                ClientSecret = ConfigurationManager.AppSettings["sso-clientSecret"],
                Protocol = ConfigurationManager.AppSettings["sso-protocol"],
                Host = ConfigurationManager.AppSettings["sso-host"],
                TokenUri = ConfigurationManager.AppSettings["sso-tokenUri"]
            };

            Log($"SSO settings read from App.config:");
            Log($"Client Id: {ssoSettings.ClientId}");
            Log($"Client Secret: {ssoSettings.ClientSecret}");
            Log($"Protocol: {ssoSettings.Protocol}");
            Log($"Host: {ssoSettings.Host}");
            Log($"Token URI: {ssoSettings.TokenUri}");

            return ssoSettings;
        }

        private WebSocketSettings ReadWebSocketSettings()
        {
            var webSocketSettings = new WebSocketSettings()
            {
                Host = ConfigurationManager.AppSettings["ws-host"],
                Port = Convert.ToInt32(ConfigurationManager.AppSettings["ws-port"]),
                Protocol = ConfigurationManager.AppSettings["ws-protocol"],
                Uri = ConfigurationManager.AppSettings["ws-uri"]
            };

            Log($"Web socket settings read from App.config:");
            Log($"WS Host: {webSocketSettings.Host}");
            Log($"WS Port: {webSocketSettings.Port}");
            Log($"WS Protocol: {webSocketSettings.Protocol}");
            Log($"WS Uri: {webSocketSettings.Host}");

            return webSocketSettings;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                var order = SampleOrderRequest();
                tradingService.SendEntryOrderRequest(order);
                Log($"Sent order:{Environment.NewLine}{JsonHelper.SerializeObjectPrettyPrinted(order)}");

            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.ToString());
            }
        }

        private void buttonGenerateSchemas_Click(object sender, EventArgs e)
        {
            try
            {

                var types = new Type[] {
                    //typeof(Models.Draft.ConfigurationRow),
                    //typeof(Models.Draft.HeartbeatMessage),
                    //typeof(Models.Draft.DeliveryAreaRow),
                    //typeof(Models.Draft.ContractRow),
                    //typeof(Models.Draft.LocalViewRow),
                    //typeof(Models.Draft.PublicStatisticRow),
                    //typeof(Models.Draft.PublicTradeRow),
                    //typeof(Models.Draft.CapacityRow),
                    //typeof(Models.Draft.OrderEntryRequest),
                    //typeof(Models.Draft.OrderModificationRequest),
                    typeof(Models.Draft.OrderExecutionReport),
                    //typeof(Models.Draft.PrivateTradeRow)
                };

                this.textBoxLog.Text = "";

                var path = @"C:\NordPool\public-intraday-api-jsonschema\v1";

                foreach (var jsonType in types)
                {

                    var jsonSchemaGenerator = new JSchemaGenerator();
                    jsonSchemaGenerator.GenerationProviders.Add(new StringEnumGenerationProvider());
                    jsonSchemaGenerator.GenerationProviders.Add(new FormatSchemaProvider());
                    jsonSchemaGenerator.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    jsonSchemaGenerator.SchemaLocationHandling = SchemaLocationHandling.Definitions;
                    jsonSchemaGenerator.SchemaPropertyOrderHandling = SchemaPropertyOrderHandling.Default;
                    jsonSchemaGenerator.SchemaReferenceHandling = SchemaReferenceHandling.Objects;
                    jsonSchemaGenerator.DefaultRequired = Required.Default;

                    var myType = jsonType;
                    var schema = jsonSchemaGenerator.Generate(myType);

                    schema.Title = myType.Name; // this doesn't seem to get done within the generator
                    var writer = new StringWriter();
                    var jsonTextWriter = new JsonTextWriter(writer);
                    schema.WriteTo(jsonTextWriter);
                    dynamic parsedJson = JsonConvert.DeserializeObject(writer.ToString());
                    var prettyString = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
                    File.WriteAllText(Path.Combine(path, $"{jsonType.Name}.json"), prettyString);
                    this.textBoxLog.Text += prettyString + Environment.NewLine;
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void buttonGenerateCS_Click(object sender, EventArgs e)
        {
            try
            {
                var schemaFolder = new DirectoryInfo(@"C:\NordPool\public-intraday-api-jsonschema\v1\");
                foreach (var schemaFile in schemaFolder.EnumerateFiles())
                {
                    var contents = File.ReadAllText(schemaFile.FullName);
                    var schema = await JsonSchema4.FromJsonAsync(contents);
                    var generator = new CSharpGenerator(schema);
                    generator.Settings.ArrayType = "System.Collections.Generic.List";
                    generator.Settings.Namespace = "NPS.ID.PublicApi.Models";
                    generator.Settings.DateTimeType = "System.DateTimeOffset";
                    generator.Settings.ClassStyle = CSharpClassStyle.Poco;


                    var csCode = generator.GenerateFile();
                    File.WriteAllText(Path.Combine(@"C:\NordPool\public-intraday-net-api\NPS.ID.PublicApi.Models", $"{schema.Title}.cs"), csCode);
                }

            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.ToString());
            }
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            this.textBoxLog.Text = "";
        }

        private void buttonLogout_Click(object sender, EventArgs e)
        {
            try
            {
                if (tradingService == null)
                {
                    MessageBox.Show("Trading service not initialized");
                    return;
                }
                tradingService.SendLogoutCommand();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                var request = new OrderModificationRequest()
                {
                    RequestId = Guid.NewGuid().ToString(),
                    OrderModificationType = OrderModificationRequestOrderModificationType.DEAC
                };

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
