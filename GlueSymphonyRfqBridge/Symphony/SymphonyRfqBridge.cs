// Copyright (c) 2016 Tick42 OOD
// -- COPYRIGHT END --

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DOT.Logging;
using GlueSymphonyRfqBridge.Extensions;
using GlueSymphonyRfqBridge.Models;
using GlueSymphonyRfqBridge.Models.Extensions;
using GlueSymphonyRfqBridge.Symphony.Extensions;
using SymphonyOSS.RestApiClient.Api.AgentApi;
using SymphonyOSS.RestApiClient.Api.PodApi;
using SymphonyOSS.RestApiClient.Authentication;
using SymphonyOSS.RestApiClient.Entities;
using SymphonyOSS.RestApiClient.Factories;
using SymphonyOSS.RestApiClient.MessageML;

namespace GlueSymphonyRfqBridge.Symphony
{
    // NB: the bridge doesn't hold any state, except who's supposed to receive and what kind of messages
    public class SymphonyRfqBridge : IGlueRfqBridge
    {
        private static ISmartLogger Logger = new SmartLogger(typeof(SymphonyRfqBridge));

        #region Command Parsing Regular Expressions

        // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR (15 min)
        // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR
        private static Regex PatternRequest = new Regex(
            @"(?<requestParty>[^:]+):(?<requestId>\d+)\s+at\s+(?<counterParty>\S+)\s+(?<action>buy|sell)\s+(?<quantity>\d+(,\d+)*(?:k|m)*)\s+(?<product>\S+)(?:\s+[(](?<requestExpirationDate>(\d+)\s+(min|m|sec|s|hour|hours|h))[)])?",
            RegexOptions.IgnoreCase);

        // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.3 for 200m
        // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.5
        private static Regex PatternResponse = new Regex(
            @"from\s(?<counterParty>\S+)\s+to\s+(?<requestParty>[^:]+):(?<requestId>\d+)\s+@\s+(?<price>\d+(?:\.\d+)*)(?:\s+for\s(?<quantity>\d+(,\d+)*(?:k|m)*))?",
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        // expired stoyan@tick42.com:1
        private static Regex PatternExpired = new Regex(
            @"(?<party>[^:]+):(?<requestId>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        // error stoyan@tick42.com:1 Wrong RFQ request
        private static Regex PatternError = new Regex(
            @"(?<party>[^:]+):(?<requestId>\d+)\s+(?<message>.+)",
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        // rtc stoyan@tick42.com to lspiro@tick42.com re @1 Are you kidding me?
        // rtc stoyan@tick42.com to lspiro@tick42.com Are you kidding me?
        private static Regex PatternRequestToCounterPartyComment = new Regex(
            @"(?<requestParty>\S+)\s+to\s+(?<counterParty>\S+)(?:\s+(?:re|wrt)\s+@(?<requestId>\d+))?\s+(?<comment>.+)",
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        // ctr lspiro@tick42.com to stoyan@tick42.com re @1 Nope
        // ctr lspiro@tick42.com to stoyan@tick42.com Nope
        private static Regex PatternCounterToRequestPartyComment = new Regex(
            @"(?<counterParty>\S+)\s+to\s+(?<requestParty>\S+)(?:\s+(?:re|wrt)\s+@(?<requestId>\d+))?\s+(?<comment>.+)",
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        #endregion Command Parsing Regular Expressions

        private readonly SymphonyRfqBridgeConfiguration config_;

        private MessagesApi messagesApi_;
        private StreamsApi streamsApi_;
        private UsersApi usersApi_;
        private DatafeedApi datafeedApi_;

        private readonly object userCacheMx_ = new object();
        private readonly Dictionary<string, User> usersByEmail_ = new Dictionary<string, User>();
        private readonly Dictionary<long, User> usersById_ = new Dictionary<long, User>();

        private readonly Dictionary<RfqCommand, Action<BotCommand, User>> commandHandlers_ =
            new Dictionary<RfqCommand, Action<BotCommand, User>>();

        private readonly object mx_ = new object();
        private HashSet<string> counterPartiesTrackingCommentsFromRequestParties_ = new HashSet<string>();
        private HashSet<string> counterPartiesTrackingRequestsFromRequestParties_ = new HashSet<string>();
        private HashSet<string> requestPartiesTrackingRequestsFromCounterParties_ = new HashSet<string>();

        public SymphonyRfqBridge(SymphonyRfqBridgeConfiguration config)
        {
            config_ = config;

            commandHandlers_[RfqCommand.Help] = HandleHelpCommand;
            commandHandlers_[RfqCommand.Error] = HandleErrorCommand;
            commandHandlers_[RfqCommand.Request] = HandleRequestCommand;
            commandHandlers_[RfqCommand.Response] = HandleResponseCommand;
            commandHandlers_[RfqCommand.Expired] = HandleExpiredCommand;
            commandHandlers_[RfqCommand.RequestToCounterPartyComment] = HandleRequestToCounterPartyCommentCommand;
            commandHandlers_[RfqCommand.CounterToRequestPartyComment] = HandleCounterPartyToRequestCommentCommand;
        }

        public string Name
        {
            get
            {
                return "Symphony";
            }
        }

        public event EventHandler<Event<RfqComment>> CounterPartyCommentReceived;
        public event EventHandler<Event<RfqQuoteInquiryResponse>> QuoteInquiryResponseReceived;
        public event EventHandler<Event<RfqQuoteRequest>> QuoteRequestReceived;
        public event EventHandler<Event<RfqComment>> RequestPartyCommentReceived;

        public void Start()
        {
            Logger.InfoFormat("Starting up with {0}...", config_);

            StartupSymphonyBot();
        }

        public void Stop()
        {
            Logger.Info("Stopping...");

            StopSymphonyBot();
        }

        public void SubscribeForRequestPartyComments(string counterParty)
        {
            Logger.InfoFormat("Subscribing counter party {0} for request party comments", counterParty);

            lock (mx_)
            {
                counterPartiesTrackingCommentsFromRequestParties_.Add(counterParty);
            }
        }

        public void UnsubscribeForRequestPartyComments(string counterParty)
        {
            Logger.InfoFormat("Unsubscribing counter party {0} for request party comments", counterParty);

            lock (mx_)
            {
                counterPartiesTrackingCommentsFromRequestParties_.Remove(counterParty);
            }
        }

        public void SubscribeForQuoteRequests(string counterParty)
        {
            Logger.InfoFormat("Subscribing counter party {0} for request party RFQ requests", counterParty);

            lock (mx_)
            {
                counterPartiesTrackingRequestsFromRequestParties_.Add(counterParty);
            }
        }

        public void UnsubscribeForQuoteRequests(string counterParty)
        {
            Logger.InfoFormat("Unsubscribing counter party {0} for request party RFQ requests", counterParty);

            lock (mx_)
            {
                counterPartiesTrackingRequestsFromRequestParties_.Remove(counterParty);
            }
        }

        public void SendCommentToCounterParty(RfqComment comment)
        {
            Logger.InfoFormat("Sending comment {0}", comment);

            SendMessage(comment.CounterParty, comment.ToRequestToCounterPartyCommentMessage());
        }

        public void SendCommentToRequestParty(RfqComment comment)
        {
            Logger.InfoFormat("Sending comment {0}", comment);

            SendMessage(comment.RequestParty, comment.ToCounterToRequestPartyCommentMessage());
        }

        public void SendQuoteInquiry(RfqQuoteInquiry quoteInquiry)
        {
            Logger.InfoFormat("Sending quote inquiry {0}", quoteInquiry);

            foreach (var party in quoteInquiry.CounterParties)
            {
                SendMessage(party, quoteInquiry.ToMessage(party));
            }
        }

        public void SendQuoteInquiryResponse(RfqQuoteInquiryResponse quoteInquiryResponse)
        {
            Logger.InfoFormat("Sending quote inquiry response {0}", quoteInquiryResponse);

            SendMessage(quoteInquiryResponse.RequestParty, quoteInquiryResponse.ToMessage());
        }

        private void RaiseQuoteInquiryResponseReceived(RfqQuoteInquiryResponse response)
        {
            Logger.InfoFormat("Quote inquiry response received: {0}", response);

            var handler = QuoteInquiryResponseReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqQuoteInquiryResponse>(response));
            }
        }

        private void RaiseCounterPartyCommentReceived(RfqComment comment)
        {
            Logger.InfoFormat("Counter party comment received: {0}", comment);

            var handler = CounterPartyCommentReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqComment>(comment));
            }
        }

        private void RaiseQuoteRequestReceived(RfqQuoteRequest request)
        {
            Logger.InfoFormat("Quote request received: {0}", request);

            var handler = QuoteRequestReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqQuoteRequest>(request));
            }
        }

        private void RaiseRequestPartyCommentReceived(RfqComment comment)
        {
            Logger.InfoFormat("Request party comment received: {0}", comment);

            var handler = RequestPartyCommentReceived;
            if (handler != null)
            {
                handler(this, new Event<RfqComment>(comment));
            }
        }

        // Symphony-specific implementation

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Received {0}", e.Message);
            }

            var msg = e.Message;
            if (msg == null)
            {
                Logger.WarnFormat("Ignoring non-V2Message message: {0}", e.Message);
                return;
            }
            var message = new MessageParser().GetPlainText(msg.Body);
            var userId = msg.FromUserId;
            if (userId == -1)
            {
                Logger.WarnFormat("Ignoring message without user ID: {0}", e.Message);
                return;
            }
            User user;
            if (!TryGetUserById(userId, out user))
            {
                Logger.WarnFormat("Ignoring message, can't resolve user with ID {0}: {1}", userId, e.Message);
                return;
            }
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Received (from {0}): {1}", user.EmailAddress, message);
            }
            try
            {
                var command = ParseMessage(message);
                if (command == null)
                {
                    throw new InvalidOperationException("Internal error - failed parsing command " + message);
                }
                var handler = commandHandlers_[command.Command];
                handler(command, user);
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    string.Format("Unexpected error while processing message: {0}", ex.Message),
                    ex);
            }
        }

        public static BotCommand ParseMessage(string message)
        {
            // get the 1st token
            var tokenEndPos = message.IndexOf(' ');
            if (tokenEndPos < 0)
            {
                return new BotCommand(RfqCommand.Help);
            }
            var token = message.Substring(0, tokenEndPos).Trim();

            // no token (so empty string) or "help" -> help
            if (token.Length == 0 || 
                token.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                return new BotCommand(RfqCommand.Help);
            }

            var rest = message.Substring(tokenEndPos).Trim();
            if (token.Equals("rfq", StringComparison.InvariantCultureIgnoreCase))
            {
                // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR (15 min)
                // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR
                return ParseCommandArgs(PatternRequest, RfqCommand.Request, rest,
                                    "requestParty", "requestId", "counterParty", "action",
                                    "quantity", "product", "requestExpirationDate");
            }
            if (token.Equals("quote", StringComparison.InvariantCultureIgnoreCase))
            {
                // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.3 for 200m
                // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.5
                return ParseCommandArgs(PatternResponse, RfqCommand.Response, rest,
                                    "requestParty", "requestId", "counterParty", "price", "quantity");
            }
            if (token.Equals("expired", StringComparison.InvariantCultureIgnoreCase))
            {
                // expired stoyan@tick42.com:1
                return ParseCommandArgs(PatternExpired, RfqCommand.Expired, rest,
                                    "party", "requestId");
            }
            if (token.Equals("error", StringComparison.InvariantCultureIgnoreCase))
            {
                // error stoyan@tick42.com:1 Wrong RFQ request
                return ParseCommandArgs(PatternError, RfqCommand.Error, rest,
                                    "party", "requestId", "message");
            }
            if (token.Equals("rtc", StringComparison.InvariantCultureIgnoreCase))
            {
                // rtc stoyan@tick42.com to lspiro@tick42.com re @1 Are you kidding me?
                // rtc stoyan@tick42.com to lspiro@tick42.com Are you kidding me?
                return ParseCommandArgs(PatternRequestToCounterPartyComment,
                                        RfqCommand.RequestToCounterPartyComment,
                                        rest,
                                        "requestParty", "requestId", "counterParty", "comment");
            }
            if (token.Equals("ctr", StringComparison.InvariantCultureIgnoreCase))
            {
                // ctr lspiro@tick42.com to stoyan@tick42.com re @1 Nope
                // ctr lspiro@tick42.com to stoyan@tick42.com Nope
                return ParseCommandArgs(PatternCounterToRequestPartyComment,
                                        RfqCommand.CounterToRequestPartyComment,
                                        rest,
                                        "requestParty", "requestId", "counterParty", "comment");
            }

            return new BotCommand(RfqCommand.Help);
        }

        private static BotCommand ParseCommandArgs(
            Regex pattern,
            RfqCommand command,
            string argumentsText,
            params string[] parameters)
        {
            var matches = pattern.Matches(argumentsText);
            if (matches.Count != 1)
            {
                return null;
            }
            return new BotCommand(command)
                .WithRegexGroup(
                    matches[0].Groups,
                    parameters);
        }

        private void HandleHelpCommand(BotCommand cmd, User sender)
        {
            SendMessage(sender, @"

GLUE RFQ Chat Bot Messages By Example

1. Request party issues an RFQ (request for quote) request

rfq REQUEST_PARTY:REQUEST_ID at COUNTER_PARTY buy|sell QUANTITY PRODUCT_NAME (REQUEST_EXPIRY)
rfq alice@request.com:123 at bob@response.com buy 100m GBP/EUR (15 min)

2. Counter party quotes requesting party

quote from COUNTER_PARTY to REQUEST_PARTY:REQUEST_ID @ PRICE
quote from bob@response.com to alice@request:123 @ 57.3

3. Requesting party sends a comment to counter party

rtc REQUEST_PARTY to COUNTER_PARTY re|wrt @REQUEST_ID @COMMENT
rtc alice@request.com to bob@response.com re @123 Are you kidding me?
rtc alice@request.com to bob@response.com Are you kidding me?

4. Counter party to request party comment

ctr COUNTER_PARTY to REQUEST_PARTY re|wrt @REQUEST_ID @COMMENT
ctr bob@response.com to alice@request.com re @1 Nope
ctr bob@response.com to alice@request.com Nope
");
        }

        private void HandleErrorCommand(BotCommand cmd, User sender)
        {
            // error stoyan@tick42.com:1 Wrong RFQ request
            RaiseQuoteInquiryResponseReceived(
                new RfqQuoteInquiryResponse(
                    RfqResponseType.Error,
                    cmd["message"],
                    cmd["requestId"],
                    cmd["party"],
                    sender.EmailAddress,
                    null,
                    null,
                    null,
                    null));
        }

        private void HandleRequestCommand(BotCommand cmd, User sender)
        {
            var quantitySign = cmd["action"].Equals("buy", StringComparison.InvariantCultureIgnoreCase) ?
                1 : -1;
            var request = new RfqQuoteRequest(
                    cmd["requestId"],
                    cmd["requestParty"],
                    cmd["counterParty"],
                    cmd["product"],
                    null,
                    quantitySign * ParseQuantity(cmd["quantity"]).Value,
                ParseExpiry(cmd.Get("requestExpirationDate")));

            lock (mx_)
            {
                if (!counterPartiesTrackingRequestsFromRequestParties_.Contains(request.CounterParty))
                {
                    return;
                }
            }
            
            // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR (15 min)
            // rfq stoyan@tick42.com:1 at lspiro@tick42.com buy 100m GBP/EUR
            RaiseQuoteRequestReceived(request);
        }

        private void HandleResponseCommand(BotCommand cmd, User sender)
        {
            // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.3 for 200m
            // quote from lspiro@tick42.com to stoyan@tick42.com:1 @ 57.5
            RaiseQuoteInquiryResponseReceived(
                new RfqQuoteInquiryResponse(
                    RfqResponseType.Quote,
                    null,
                    cmd["requestId"],
                    cmd["requestParty"],
                    cmd["counterParty"],
                    cmd.Get("product"),
                    null,
                    ParseQuantity(cmd.Get("quantity")),
                    ParsePrice(cmd["price"])));
        }

        private void HandleExpiredCommand(BotCommand cmd, User sender)
        {
            RaiseQuoteInquiryResponseReceived(
                new RfqQuoteInquiryResponse(
                    RfqResponseType.Expired,
                    null,
                    cmd["requestId"],
                    cmd["party"],
                    sender.EmailAddress,
                    null,
                    null,
                    null,
                    null));
        }

        private void HandleRequestToCounterPartyCommentCommand(BotCommand cmd, User sender)
        {
            var comment = ParseComment(cmd);

            lock (mx_)
            {
                if (!counterPartiesTrackingCommentsFromRequestParties_.Contains(comment.CounterParty))
                {
                    Logger.InfoFormat("Ignoring comment for {0}, not subscribed as this party for request party comments", comment.CounterParty);
                    return;
                }
            }

            RaiseRequestPartyCommentReceived(comment);
        }

        private void HandleCounterPartyToRequestCommentCommand(BotCommand cmd, User sender)
        {
            var comment = ParseComment(cmd);

            lock (mx_)
            {
                if (!requestPartiesTrackingRequestsFromCounterParties_.Contains(comment.RequestParty))
                {
                    Logger.InfoFormat("Ignoring comment for {0}, not subscribed as this party for counter party comments", comment.RequestParty);
                    return;
                }
            }

            RaiseCounterPartyCommentReceived(comment);
        }

        private static RfqComment ParseComment(BotCommand command)
        {
            return new RfqComment(
                    command.Get("requestId"),
                    command["requestParty"],
                    command["counterParty"],
                    null,
                command["comment"]);
        }

        public static double? ParseQuantity(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var factor = 1;
            if (text.EndsWith("k", StringComparison.InvariantCultureIgnoreCase))
            {
                text = text.Remove(text.Length - 1);
                factor = 1000;
            }
            else if (text.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
            {
                text = text.Remove(text.Length - 1);
                factor = 1000000;
            }
            return factor * text.ParseDouble();
        }

        public static double ParsePrice(string text)
        {
            return text.ParseDouble();
        }

        private DateTime ParseExpiry(string text)
        {
            var now = DateTime.UtcNow;
            if (string.IsNullOrEmpty(text))
            {
                return now.Add(config_.DefaultRfqExpiry);
            }
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var value = int.Parse(parts[0], NumberStyles.Integer | NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var suffix = parts[1];
            if (suffix.StartsWith("s", StringComparison.CurrentCultureIgnoreCase))
            {
                return now.AddSeconds(value);
            }
            if (suffix.StartsWith("m", StringComparison.CurrentCultureIgnoreCase))
            {
                return now.AddMinutes(value);
            }
            if (suffix.StartsWith("h", StringComparison.CurrentCultureIgnoreCase))
            {
                return now.AddHours(value);
            }
            throw new ArgumentException("Invalid RFQ expiration " + text);
        }

        private void StartupSymphonyBot()
        {
            // Symphony start up boilerplate start

            SymphonyOSS.RestApiClient.Generated.OpenApi.PodApi.Client.Configuration.Default = 
                new SymphonyOSS.RestApiClient.Generated.OpenApi.PodApi.Client.Configuration(timeout: config_.TimeoutInMillis);
            SymphonyOSS.RestApiClient.Generated.OpenApi.AuthenticatorApi.Client.Configuration.Default = 
                new SymphonyOSS.RestApiClient.Generated.OpenApi.AuthenticatorApi.Client.Configuration(timeout: config_.TimeoutInMillis);
            SymphonyOSS.RestApiClient.Generated.OpenApi.AgentApi.Client.Configuration.Default = 
                new SymphonyOSS.RestApiClient.Generated.OpenApi.AgentApi.Client.Configuration(timeout: config_.TimeoutInMillis);

            var certificate = new X509Certificate2(
                config_.BotCertificateFilePath,
                config_.BotCertificatePassword);
            var sessionManager = new UserSessionManager(
                string.Format("{0}/sessionauth/", config_.BaseApiUrl),
                string.Format("{0}/keyauth/", config_.BaseApiUrl),
                certificate);
            var agentApiFactory = new AgentApiFactory(
                string.Format("{0}/agent", config_.BaseApiUrl));
            var podApiBaseUrl = string.Format("{0}/pod", config_.BasePodUrl);
            var podApiFactory = new PodApiFactory(podApiBaseUrl);

            // Symphony start up boilerplate end

            // create a data feed API to listen for chat messages
            datafeedApi_ = agentApiFactory.CreateDatafeedApi(sessionManager);
            datafeedApi_.OnMessage += OnMessageReceived;

            // create streams API to initiate chats
            streamsApi_ = podApiFactory.CreateStreamsApi(sessionManager);

            // create messages API to send messages
            messagesApi_ = agentApiFactory.CreateMessagesApi(sessionManager);

            // create users API to resolve users by e-mail or id
            usersApi_ = podApiFactory.CreateUsersApi(sessionManager);

            // start listening for messages from out bot
            // NB: Listen() is blocking, so run on a dedicated thread
            Task.Factory.StartNew(datafeedApi_.Listen, TaskCreationOptions.LongRunning);
        }

        private void StopSymphonyBot()
        {
            datafeedApi_.Stop();
        }

        //

        private void SendMessage(string partyName, string messageText)
        {
            User user;
            if (!TryGetUser(partyName, out user))
            {
                throw new Exception("Could not resolve user " + partyName);
            }
            SendMessage(user, messageText);
        }

        private void SendMessage(User user, string messageText)
        {
            string streamId;
            try
            {
                streamId = streamsApi_.CreateStream(new List<long> { user.Id });
            }
            catch (Exception e)
            {
                Logger.Error(string.Format("Failed to create chat with {0}: {1}",
                                           user.EmailAddress,
                                           e),
                             e);
                throw new Exception("Failed to create chat with " + user.EmailAddress, e);
            }

            Logger.InfoFormat("Sending {0} to {1}", messageText, user.EmailAddress);

            try
            {
                var message = new Message(streamId, MessageFormat.Text, messageText);
                messagesApi_.PostMessage(message);
            }
            catch (Exception e)
            {
                Logger.Error(string.Format("Failed to send {0} to {1}: {2}",
                                           messageText,
                                           user.EmailAddress,
                                           e),
                             e);
                throw new Exception("Failed to send " + messageText + " to " + user.EmailAddress, e);
            }
        }

        private bool TryGetUser(string partyName, out User user)
        {
            lock (userCacheMx_)
            {
                if (usersByEmail_.TryGetValue(partyName, out user))
                {
                    return true;
                }
                try
                {
                    var userId = usersApi_.GetUserId(partyName);
                    if (userId == -1)
                    {
                        Logger.WarnFormat("Could not resolve user " + partyName);
                        throw new Exception("User " + partyName + " could not be resolved");
                    }
                    else
                    {
                        user = usersApi_.GetUser(userId);
                    }
                    usersByEmail_.Add(partyName, user);
                    usersById_.Add(user.Id, user);
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("WARN: Could not resolve user {0}: {1}", partyName, e.ToString());
                    return false;
                }
            }
        }

        private bool TryGetUserById(long id, out User user)
        {
            user = null;
            lock (userCacheMx_)
            {
                if (usersById_.TryGetValue(id, out user))
                {
                    return true;
                }
                try
                {
                    user = usersApi_.GetUser(id);
                    if (user == null || user.EmailAddress == null)
                    {
                        throw new Exception("User " + id + " could not be resolved");
                    }
                    usersByEmail_.Add(user.EmailAddress, user);
                    usersById_.Add(id, user);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Warn(string.Format("Could not resolve user {0}: {1}", id, e), e);
                    return false;
                }
            }
        }

        public void SubscribeForCounterPartyComments(string requestParty)
        {
            Logger.InfoFormat("Subscribing request party {0} for counter party comments", requestParty);

            lock (mx_)
            {
                requestPartiesTrackingRequestsFromCounterParties_.Add(requestParty);
            }
        }

        public void UnsubscribeForCounterPartyComments(string requestParty)
        {
            Logger.InfoFormat("Unsubscribing request party {0} for counter party comments", requestParty);

            lock (mx_)
            {
                requestPartiesTrackingRequestsFromCounterParties_.Remove(requestParty);
            }
        }
    }
}
