using ClientSocketConnection.model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Web;
using static ClientSocketConnection.APIResponse;
using static ClientSocketConnection.model.EventCallback;
using static ClientSocketConnection.model.SocketStatus;
using static ClientSocketConnection.model.TransResult;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace ClientSocketConnection
{
    public interface IDataCallback
    {
        void TransactionResult( TransactionStatus result);
        void QueryTransactionResult(TransactionStatus result);
        void TransactionEventCallback(TransactionEventCallback transactionEventCallback);
        void SocketStatusCallback(SocketConnectivityCallback socketConnectivityCallback);
        void QueryTransMessage(string message);
        void CurrentTransactionCartId(string cartId);
    }

    public class ClientSocket
    {
        private readonly HttpClient _client;
        private readonly Ping _myPing = new Ping();
        private PingReply _serverIpReply;
        private PingReply _dnsReply;
        private readonly PingOptions _pingOptions = new PingOptions();
        private readonly SocketStatus _socketStatus;
        private readonly string _PRODUCTION_URL= "https://api.gkash.my";
        private readonly string _HOST_URL = "https://api-staging.pay.asia";
        private readonly string _SERVER_NAME = "t1.gkash.my";
        private readonly int _port = 38300;
        private string _ipAddress, _authToken, _signatureKey, _loginUsername, _certPath;   
        private SslStream _sslStream;
        private TcpClient _tcpClient;
        private PaymentRequestDto _dto;
        private IDataCallback _dataCallback;
        private readonly ILogger _logger;

        // State object for receiving data from remote device.  
        // Size of receive buffer.  
        private const int _BufferSize = 4096;
        // Receive buffer.  
        private readonly byte[] _buffer = new byte[_BufferSize];
        // The response from the remote device.  
        private static string _response = string.Empty;
        private Timer _timer, _internetTimer;
        private bool _isConnResp = true;
        private int _pingCount = 0;
        private bool _cancelPayment = false;

        //Memory cache
        private readonly MemoryCache _memCaches = MemoryCache.Default;
        private readonly string TOKEN_CACHE_KEY = "TOKEN";
        private readonly int _tokenExpiryDays = 6;
        private readonly int _ipCacheMinutes = 10;

        public ClientSocket(IDataCallback dataCallback, string certPath, bool environment = false, ILogger logger = null)
        {
            _logger?.LogInformation("Creating Client Socket");
            _dataCallback = dataCallback;
            _certPath = certPath;
            if (logger != null)
            {
                _logger = logger;
            }

            _socketStatus = new SocketStatus(dataCallback);
      
            if (environment)
            {
                _HOST_URL = _PRODUCTION_URL;                
            }

            _client = new HttpClient();
        }

        public ClientSocket(IDataCallback dataCallback, string certPath, bool environment = false, ILogger logger = null, HttpClient httpClient = null)
        {
            _logger?.LogInformation("Creating Client Socket");
            _dataCallback = dataCallback;
            _certPath = certPath;
            if (logger != null)
            {
                _logger = logger;
            }

            _socketStatus = new SocketStatus(dataCallback);

            if (environment)
            {
                _HOST_URL = _PRODUCTION_URL;
            }
            _client = httpClient ?? new HttpClient();
        }

        private static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            //if (sslPolicyErrors == SslPolicyErrors.None)
            //    return true;

            //   Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            //return false;
            return true;
        }

        private void RunClient()
        {
            try
            {
                // Create a TCP/IP client socket.
                // machineName is the host running the server application.
                _logger?.LogInformation("tcpClient: try connect");

                _tcpClient = new TcpClient(_ipAddress, _port);

                _logger?.LogInformation("RunClient: client connected");

                // Create an SSL stream that will close the client's stream.
                _sslStream = new SslStream(
                    _tcpClient.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null
                    );

                //Read timeout
                _sslStream.ReadTimeout = 5000;

                byte[] pfxData = File.ReadAllBytes(_certPath);
                X509CertificateCollection x509Certificates = new X509CertificateCollection();

                X509Certificate2 cert = new X509Certificate2(pfxData, "9pcTOBjq");
                x509Certificates.Add(cert);
                _logger?.LogInformation("RunClient: AuthenticateAsClient");

                // The server name must match the name on the server certificate.
                _sslStream.AuthenticateAsClient(_SERVER_NAME, x509Certificates, SslProtocols.Tls12, false);
            }
            catch (AuthenticationException e)
            {
                _dataCallback.SocketStatusCallback(SocketConnectivityCallback.AUTH_ERROR);
                _logger?.LogError(e, "AuthenticationException:");
                _tcpClient.Close();
                return;
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "RunClient Exception:");
                if (_sslStream != null)
                {
                    StartCheckInternetConn(0);
                }
                else
                {
                    _dataCallback.SocketStatusCallback(SocketConnectivityCallback.HOST_NOT_FOUND);
                }
                return;
            }

            _logger?.LogInformation("current socketStatus: " + _socketStatus.GetSocketStatus().ToString());
            if (_socketStatus.GetSocketStatus() == SocketConnectivityCallback.RECONNECTING)
            {
                _logger?.LogInformation("RECONNECTED");
                StartCheckConnection();
            }
            else
            {
                SendRequestToApp(_dto);
            }
            _socketStatus.SetSocketStatus(SocketConnectivityCallback.CONNECTED);
        }

        public void RequestPayment(PaymentRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(_ipAddress))
            {
                throw new Exception("Invalid Ip address, please enable servor mode on Gkash Business APP");
            }

            if (string.IsNullOrWhiteSpace(dto.TerminalId))
            {
                throw new Exception("Invalid Terminal Id");
            }

            if (!double.TryParse(dto.Amount, out _))
            {            
                throw new Exception("Invalid amount");
            };

            dto.Amount = double.Parse(dto.Amount).ToString("0.00");
            _cancelPayment = false;
            _dto = dto;
            RunClient();
        }       

        private void SendRequestToApp(PaymentRequestDto requestDto)
        {
            requestDto.Signature = SignRequest(requestDto);
            string JsonString = ConvertToJsonString(requestDto);
            _logger?.LogInformation("Send request to app");
            _dataCallback.CurrentTransactionCartId(_dto.ReferenceNo);
            SendMessage(JsonString);
        }

        private void SendMessage(string data)
        {
            try
            {
                // Encode a test message into a byte array.
                // Signal the end of the message using the "<EOF>".
                byte[] messsage = Encoding.UTF8.GetBytes(data);
                _logger?.LogInformation("SendMessage: " + data);
                // Send hello message to the server.
                _sslStream.Write(messsage);
                _sslStream.Flush();
                _isConnResp = false;
                ReadMessage();
            }
            catch (SocketException e)
            {
                _logger?.LogError(e, "Send SocketException:");
            }
            catch (Exception e)
            {
                //Try Reconnect
                _logger?.LogError(e, "Send Exception:");

                if (_timer != null)
                {
                    _timer.Dispose();
                }
                _logger?.LogInformation("timer dispose");
                _socketStatus.SetSocketStatus(SocketConnectivityCallback.RECONNECTING);

                StartCheckInternetConn(0);
            }
        }


        private void ReadMessage()
        {           
            _logger?.LogInformation("Begin read message");
            StringBuilder messageData = new StringBuilder();
            int bytes;
            do
            {
                bytes = _sslStream.Read(_buffer, 0, _buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(_buffer, 0, bytes)];
                decoder.GetChars(_buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                // Check for EOF.
                if (messageData.ToString().IndexOf("<EOF>") != -1)
                {
                    break;
                }
            } while (bytes != 0);

            _response = messageData.ToString().Replace("<EOF>", "");
            _logger?.LogInformation("Server: " + _response);

            if (string.IsNullOrWhiteSpace(_response))
            {
                //host disconnected
                //try run reconnect
                _logger?.LogInformation("host disconnected");
                if(_timer != null)
                {
                    _timer.Dispose();
                }
                _socketStatus.SetSocketStatus(SocketConnectivityCallback.RECONNECTING);

                StartCheckInternetConn(0);
                return;
            }
            _isConnResp = true;
         //   dataCallback.ResponseData(response);

            SocketEventCallback eventCallback = JsonConvert.DeserializeObject<SocketEventCallback>(_response);

            if (eventCallback.type == (int)TransactionCallbackType.TRANSACTION_RESULT)
            {
                _logger?.LogInformation("TransactionCallbackType: " + TransactionCallbackType.TRANSACTION_RESULT.ToString());
                _socketStatus.SetTransactionStatus(TransactionEventCallback.DONE);
                _logger?.LogInformation("SetTransactionStatus: DONE");
                SendTransactionResult(_response);
            }
            else if (eventCallback.type == (int)TransactionCallbackType.TRANSACTION_STATUS)
            {
                _logger?.LogInformation("TransactionCallbackType: " + TransactionCallbackType.TRANSACTION_STATUS.ToString() + ", TransactionEventCallback: " + ((TransactionEventCallback)eventCallback.eventIndex).ToString());
                //Get transaction status
                _socketStatus.SetTransactionStatus((TransactionEventCallback)eventCallback.eventIndex);
                if (eventCallback.eventIndex == (int)TransactionEventCallback.INIT_PAYMENT)
                {
                    //  checkConnection
                    StartCheckConnection();
                }
                else if (eventCallback.eventIndex == (int)TransactionEventCallback.DEFAULT)
                {
                    _timer.Dispose();
                    DisconnectServerSocket();
                }
            }
        }     

        private void StartCheckInternetConn(int pingCountIndex)
        {
            _pingCount = pingCountIndex;
            _logger?.LogInformation("Start StartCheckInternetConn");
            _internetTimer = new Timer(CheckInternetConnection, null, 1000, 5000);
        }

        private void CheckInternetConnection(object obj)
        {
            try
            {
                _pingCount++;
                _logger?.LogInformation("internetTimer ping count: " + _pingCount);
                string ipHost = _ipAddress;
                string googleDNS = "8.8.8.8";

                byte[] buffer = new byte[32];
                int timeout = 1000;

                _serverIpReply = _myPing.Send(ipHost, timeout, buffer, _pingOptions);                
                _logger?.LogInformation("checkInternetConnection try ping " + ipHost + " status: " + _serverIpReply.Status);
                _dnsReply = _myPing.Send(googleDNS, timeout, buffer, _pingOptions);
                _logger?.LogInformation("checkInternetConnection try ping " + googleDNS + " status: " + _dnsReply.Status);

                if (_serverIpReply.Status == IPStatus.Success)
                {
                    _logger?.LogInformation("internetTimer Dispose");
                    _internetTimer.Dispose();
                    _logger?.LogInformation("checkInternetConnection ping success");
                    ReconnectServerSocket();
                }
                else
                {
                    if (_dnsReply.Status == IPStatus.Success)
                    {
                        _logger?.LogInformation("internetTimer Dispose");
                        _internetTimer.Dispose();
                        GetIpAddressFromServer();
                        StartCheckInternetConn(_pingCount);
                    }
                    else
                    {
                        if (_pingCount > 6)
                        {
                            _logger?.LogInformation("internetTimer Dispose");
                            _internetTimer.Dispose();
                            //Get new Ip
                            _logger?.LogInformation("SetSocketStatus DISCONNECTED");
                            _socketStatus.SetSocketStatus(SocketConnectivityCallback.DISCONNECTED);
                            _logger?.LogInformation("SetTransactionStatus TRY_AGAIN");
                            _socketStatus.SetTransactionStatus(TransactionEventCallback.TRY_AGAIN);
                        }
                    }                 
                }                                     
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "CheckInternetConnection checkInternetConnection:");
                if (_pingCount > 6)
                {
                    _internetTimer.Dispose();
                    //Get new Ip
                    _socketStatus.SetSocketStatus(SocketConnectivityCallback.DISCONNECTED);
                    _socketStatus.SetTransactionStatus(TransactionEventCallback.TRY_AGAIN);
                }
            }
        }

        private void StartCheckConnection()
        {
            _logger?.LogInformation("StartCheckConnection");
            _isConnResp = true;
            _timer = new Timer(CheckConnection, null, 1000, 2000);
        }

        private void CheckConnection(object obj)
        {
            _logger?.LogInformation("OnTimedEvent: " + _socketStatus.GetTransactionStatus().ToString());
            if (_socketStatus.GetTransactionStatus() == TransactionEventCallback.DONE ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.CANCEL_PAYMENT ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.INVALID_METHOD ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.CHECK_TERMINAL_STATUS ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.NO_CARD_DETECTED_TIMEOUT ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.GET_KEY_FAIL ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.INVALID_SIGNATURE ||
                _socketStatus.GetTransactionStatus() == TransactionEventCallback.DEVICE_OFFLINE
                )
            {
                _socketStatus.SetSocketStatus(SocketConnectivityCallback.ONLINE);
                _logger?.LogInformation("OnTimedEvent: timer dispose");
                _timer.Dispose();
                _logger?.LogInformation("OnTimedEvent: DisconnectServerSocket");
                DisconnectServerSocket();
            }
            else
            {
                _logger?.LogInformation("OnTimedEvent: checkConnResp:" + _isConnResp.ToString());
                if (_isConnResp)
                {
                    string data;
                    if (!_cancelPayment)
                    {
                        data = ((int)TransactionEventCallback.CHECK_CONNECTION).ToString();
                        _logger?.LogInformation("Checking Connection");
                    }
                    else
                    {
                        data = ((int)TransactionEventCallback.CANCEL_PAYMENT).ToString();
                        _logger?.LogInformation("Cancel ing Payment");
                        _socketStatus.SetSocketStatus(SocketConnectivityCallback.ONLINE);
                    }

                    SendMessage(data);
                }
            }
        }

        private void DisconnectServerSocket()
        {
            try
            {              
                _sslStream.Close();
                _logger?.LogInformation("sslStream.Close");
                _tcpClient.Close();
                _logger?.LogInformation("tcpClient.Close");
                _sslStream.Dispose();
                _logger?.LogInformation("sslStream.Dispose");
                _tcpClient.Dispose();
                _logger?.LogInformation("tcpClient.Dispose\n");
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "DisconnectServerSocket e:");
            }
        }

        private void ReconnectServerSocket()
        {
            _logger?.LogInformation("ReconnectServerSocket");
            DisconnectServerSocket();
            RunClient();
        }

        public async Task<string> LoginAsync(string username, string password)
        {
            try
            {             
                _loginUsername = username;
                _logger?.LogInformation("Login: " + _loginUsername);
                string token = GetToken();

                if(token == null)
                {
                    _logger?.LogInformation("token is null, re-login: " + _loginUsername);
                    var data = new { Username = username, password, CompanyRemID = "ANDROID" };
                    var json = JsonConvert.SerializeObject(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = _client.PostAsync(_HOST_URL + "/apim/auth/userlogin", content).Result;
                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger?.LogInformation("Login response: " + response.StatusCode);
                    if (response.IsSuccessStatusCode)
                    {
                        AuthToken loginResponse = JsonConvert.DeserializeObject<AuthToken>(responseString);
                        _authToken = loginResponse.Auth;

                        UpdateTokenToCache(_authToken);
                    }
                    else
                    {
                        _dataCallback.SocketStatusCallback(SocketConnectivityCallback.LOGIN_FAIL);
                        return "Login Failed: " + responseString;
                    }
                }
                else
                {
                    _authToken = token;
                }

                if (GetIpAddressFromServer() == null)
                {
                    _dataCallback.SocketStatusCallback(SocketConnectivityCallback.RETRIEVE_IP_FAIL);
                    return "Login Failed: Retrieve Ip address fail";
                }

                if (!GetSignatureKey())
                {
                    _dataCallback.SocketStatusCallback(SocketConnectivityCallback.RETRIEVE_KEY_FAIL);
                    return "Login Failed: Retrieve key fail";
                }

                _dataCallback.SocketStatusCallback(SocketConnectivityCallback.ONLINE);
                return "Login Successful";
            }
            catch(Exception e)
            {
                _logger?.LogError(e, "LoginAsync Exception:");
                _dataCallback.SocketStatusCallback(SocketConnectivityCallback.LOGIN_FAIL);

                return "Login Failed: " + e.Message;
            }
        }

        private bool GetSignatureKey()
        {
            try
            {
                _logger?.LogInformation("GetSignatureKey");
                if (_signatureKey == null)
                {                  
                    _logger?.LogInformation("Key is null, Retrieving from server");
                
                    var response = _client.GetAsync(_HOST_URL + "/apim/merchant/signaturekey" + "?authToken=" + _authToken + "&sourceOfRequest=ANDROID").Result;

                    var responseString = response.Content.ReadAsStringAsync().Result;
                    _logger?.LogInformation($"GetSignatureKey response: {response.StatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                       
                        SignatureResp signatureResp = JsonConvert.DeserializeObject<SignatureResp>(responseString);
                        
                        _authToken = signatureResp.AuthToken;
                        _signatureKey = signatureResp.Merchant.SignatureKey;
                        _logger?.LogInformation("GetSignatureKey done: " + signatureResp.Merchant.RemID);

                        return true;
                    }
                    else
                    {
                        _logger?.LogError("GetSignatureKey failed " + responseString);
                    }
                }
                else
                {
                    return true;
                }              
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "GetSignatureKey Exception:");
            }

            return false;
        }

        private string GetIpAddressFromServer()
        {
            try
            {
                _logger?.LogInformation("GetServerIpAddress");

                _ipAddress = GetIpAddress();

                if(_ipAddress == null)
                {                   
                    var data = new { RemID = _loginUsername, AuthToken = _authToken, CompanyRemID = "ANDROID" };
                    var json = JsonConvert.SerializeObject(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    _logger?.LogInformation("Ip address is null, Retrieving from server: " + json);

                    var response = _client.PostAsync(_HOST_URL + "/apim/auth/ServerIpAddress", content).Result;
                    string responseString = response.Content.ReadAsStringAsync().Result;
                    _logger?.LogInformation($"Response from server: {response.StatusCode} {responseString}");
                    if (response.IsSuccessStatusCode)
                    {
                        if (responseString.Contains("\""))
                        {
                            responseString = responseString.Substring(1, responseString.Length - 2);
                        }
                        _ipAddress = responseString;
                        UpdateIpAddressToCache(_ipAddress);
                    }
                    else
                    {
                        _dataCallback.SocketStatusCallback(SocketConnectivityCallback.RETRIEVE_IP_FAIL);
                        return null;
                    }
                }           
               
                _logger?.LogInformation(_ipAddress);
                return _ipAddress;
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "GetIpAddressFromServer Exception:");
                _dataCallback.SocketStatusCallback(SocketConnectivityCallback.RETRIEVE_IP_FAIL);
                return null;
            }
        }  

        public void CancelPayment()
        {
            _cancelPayment = true;
        }

        public async Task<TransactionStatus> QueryStatusAsync(string remId)
        {
            try
            {
                _logger?.LogInformation("QueryStatus: " + remId);
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["authToken"] = _authToken;
                query["sourceOfRequest"] = "ANDROID";
                query["remID"] = remId;
                string queryString = query.ToString();
                string URL = _HOST_URL + "/apim/transaction/detail?" + queryString;
                _logger?.LogInformation("start query: " + remId);
                var response = _client.GetAsync(URL).Result;
                string responseString = await response.Content.ReadAsStringAsync();
                _logger?.LogInformation("QueryStatus: " + responseString);
                JObject obj = JObject.Parse(responseString);
                if (responseString.Contains("AuthToken"))
                {                    
                    string name = obj["Transaction"].ToString();
                    JsonSerializer serializer = new JsonSerializer();
                    TransactionStatus transactionStatus = (TransactionStatus)serializer.Deserialize(new JTokenReader(obj["Transaction"]), typeof(TransactionStatus));
                    _dataCallback.QueryTransactionResult(transactionStatus);

                    return transactionStatus;
                }
                else
                {
                    string message = obj["Message"].ToString();
                    _logger?.LogInformation("QueryStatus: " + message);
                    _dataCallback.QueryTransMessage(message);
                    return null;
                }                               
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "QueryStatusAsync Exception:");
                _dataCallback.QueryTransMessage(e.Message);
                return null;
            }
        }

        public async Task<List<TransactionStatus>> QueryCardAndDuitNowStatusAsync(string remId)
        {
            try
            {
                List<TransactionStatus> transactions = new List<TransactionStatus>();

                _logger?.LogInformation("QueryStatus: " + remId);
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["authToken"] = _authToken;
                query["sourceOfRequest"] = "ANDROID";
                query["remID"] = remId;
                string queryString = query.ToString();
                string URL = _HOST_URL + "/apim/transaction/detail?" + queryString;
                _logger?.LogInformation("start query: " + remId);
                var response = _client.GetAsync(URL).Result;
                string responseString = await response.Content.ReadAsStringAsync();
                _logger?.LogInformation("QueryStatus: " + responseString);
                JObject obj = JObject.Parse(responseString);
                if (responseString.Contains("AuthToken"))
                {
                    string name = obj["Transaction"].ToString();
                    JsonSerializer serializer = new JsonSerializer();
                    TransactionStatus transactionStatus = (TransactionStatus)serializer.Deserialize(new JTokenReader(obj["Transaction"]), typeof(TransactionStatus));
                    _dataCallback.QueryTransactionResult(transactionStatus);

                    transactions.Add(transactionStatus);
                }
                else
                {
                    string message = obj["Message"].ToString();
                    _logger?.LogInformation("QueryStatus: " + message);
                    _dataCallback.QueryTransMessage(message);

                    TransactionStatus transactionStatus = new TransactionStatus
                    {
                        CartID = remId,
                        Message = message
                    };
                   
                    transactions.Add(transactionStatus);
                }

                URL += "-QR";
                string newCartId = remId + "-QR";
                _logger?.LogInformation("start query: " + newCartId);
                response = _client.GetAsync(URL).Result;
                responseString = response.Content.ReadAsStringAsync().Result;
                _logger?.LogInformation("QueryStatus: " + responseString);

                obj = JObject.Parse(responseString);
                if (responseString.Contains("AuthToken"))
                {
                    string name = obj["Transaction"].ToString();
                    JsonSerializer serializer = new JsonSerializer();
                    TransactionStatus transactionStatus = (TransactionStatus)serializer.Deserialize(new JTokenReader(obj["Transaction"]), typeof(TransactionStatus));
                    _dataCallback.QueryTransactionResult(transactionStatus);

                    transactions.Add(transactionStatus);
                }
                else
                {
                    string message = obj["Message"].ToString();
                    _logger?.LogInformation("QueryStatus: " + message);
                    _dataCallback.QueryTransMessage(message);

                    TransactionStatus transactionStatus = new TransactionStatus
                    {
                        CartID = newCartId,
                        Message = message
                    };

                    transactions.Add(transactionStatus);
                }

                return transactions;

            }
            catch (Exception e)
            {
                _logger?.LogError(e, "QueryCardAndDuitNowStatusAsync Exception:");
                _dataCallback.QueryTransMessage(e.Message);
                return null;
            }
        }

        public SocketStatus GetSocketStatus()
        {
            return _socketStatus;
        }

        public string GetReferenceNo()
        {
            return _dto.ReferenceNo;
        }

        private string ConvertToJsonString(PaymentRequestDto requestDto)
        {           
            return JsonConvert.SerializeObject(requestDto);
        }

        private bool ValidateResultSignature(string[] signatureInput, string hostSignature)
        {
            string signature =  Sha512(signatureInput);

            if(signature != hostSignature)
            {
                return false;
            }

            return true;
        }

        private string SignRequest(PaymentRequestDto requestDto)
        {

            if (string.IsNullOrWhiteSpace(requestDto.ReferenceNo))
            {
                requestDto.ReferenceNo = "TCP-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            }

            if (requestDto.MobileNo == null)
            {
                requestDto.MobileNo = "";
            }
            _dto = requestDto;
            string[] signatureInput = new string[7];
            signatureInput[0] = _signatureKey;
            signatureInput[1] = double.Parse(requestDto.Amount).ToString("0.00").Replace(".", "");
            signatureInput[2] = requestDto.Email;
            signatureInput[3] = requestDto.ReferenceNo;
            signatureInput[4] = requestDto.MobileNo;
            signatureInput[5] = ((int)requestDto.PaymentType).ToString();
            signatureInput[6] = requestDto.PreAuth.ToString();

            _socketStatus.SetTransactionType(requestDto.PaymentType);

            return Sha512(signatureInput);
        }

        private void SendTransactionResult(string jsonString)
        {
            CardResult cardTrans = JsonConvert.DeserializeObject<CardResult>(jsonString);
            string[] signatureInput = new string[8];
            signatureInput[0] = _signatureKey;
            signatureInput[1] = double.Parse(cardTrans.result.TransferAmount).ToString("0.00").Replace(".", "");
            signatureInput[2] = cardTrans.result.TransferCurrency;
            signatureInput[3] = cardTrans.result.CartID;
            signatureInput[4] = cardTrans.result.RemID;
            signatureInput[5] = cardTrans.result.Status;
            signatureInput[6] = cardTrans.result.Message;
            signatureInput[7] = cardTrans.result.Method;

            if (!ValidateResultSignature(signatureInput, cardTrans.result.Signature))
            {
                _dataCallback.TransactionEventCallback(TransactionEventCallback.INVALID_SIGNATURE);
                return;
            };
            _socketStatus.SetSocketStatus(SocketConnectivityCallback.ONLINE);
            _dataCallback.TransactionResult(cardTrans.result);
        }

        private string Sha512(string[] signatureInput)
        {
            StringBuilder str = new StringBuilder();
            string sep = "";

            foreach (string s in signatureInput)
            {
                str = str.Append(sep).Append(s);
                sep = ";";
            }
            SHA512CryptoServiceProvider HashProvider = new SHA512CryptoServiceProvider();
            byte[] arrHash = HashProvider.ComputeHash(Encoding.UTF8.GetBytes(str.ToString().ToUpper()));
            return (BitConverter.ToString(arrHash).Replace("-", "")).ToLower();
        }

        private string Sha512ForRemote(string[] signatureInput)
        {
            StringBuilder str = new StringBuilder();
            string sep = "";

            foreach (string s in signatureInput)
            {
                str = str.Append(sep).Append(s);
                sep = ";";
            }
            SHA512CryptoServiceProvider HashProvider = new SHA512CryptoServiceProvider();
            byte[] arrHash = HashProvider.ComputeHash(Encoding.UTF8.GetBytes(str.ToString()));
            return (BitConverter.ToString(arrHash).Replace("-", "")).ToUpper();
        }

        private void UpdateIpAddressToCache(string ipAddress)
        {
            _logger?.LogInformation($"AddIpAddressToCache {_loginUsername} {ipAddress}");
            _memCaches.Set(_loginUsername, ipAddress, DateTime.Now.AddMinutes(_ipCacheMinutes));
        }
        public string GetIpAddress()
        {
            string ipAddress = _memCaches[_loginUsername] as string;
            _logger?.LogInformation($"GetIpAddress {_loginUsername} {ipAddress}");
            return ipAddress;
        }
        private string GetToken()
        {
            string token = _memCaches[TOKEN_CACHE_KEY] as string;
            _logger?.LogInformation("GetToken " + token);
            return token;
        }
        private void UpdateTokenToCache(string token)
        {
            _logger?.LogInformation("UpdateToken " + token);
            _memCaches.Set(TOKEN_CACHE_KEY, token, DateTime.Now.AddDays(_tokenExpiryDays));
        }

        public bool RequestRemotePayment(PaymentRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TerminalId))
            {
                throw new Exception("Invalid Terminal Id");
            }

            if (!double.TryParse(dto.Amount, out _))
            {
                throw new Exception("Invalid amount");
            };

            dto.Amount = double.Parse(dto.Amount).ToString("0.00");
            _cancelPayment = false;
            _dto = dto;
            return RemotePayment(dto);
        }

        private bool RemotePayment(PaymentRequestDto dto)
        {
            dto.Currency = "MYR";
            string signature = SignRemoteRequest(dto);

            dto.Signature = signature;

            try
            {
                _logger?.LogInformation("RemotePayment");

                _ipAddress = GetIpAddress();
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger?.LogInformation("RemotePayment : " + json);

                var response = _client.PostAsync(_HOST_URL + "/apim/merchant/SoftPOSPay", content).Result;
                string responseString = response.Content.ReadAsStringAsync().Result;
                _logger?.LogInformation($"RemotePayment Response from server: {response.StatusCode} {responseString}");
                if (response.IsSuccessStatusCode)
                {
                 

                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception e)
            {
                _logger?.LogError(e, "RemotePayment Exception:");
                return false;
            }
        }

        private string SignRemoteRequest(PaymentRequestDto requestDto)
        {

            if (string.IsNullOrWhiteSpace(requestDto.ReferenceNo))
            {
                requestDto.ReferenceNo = "TCP-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            }

            _dto = requestDto;
            string[] signatureInput = new string[5];
            signatureInput[0] = _signatureKey;
            signatureInput[1] = double.Parse(requestDto.Amount).ToString("0.00").Replace(".", "");
            signatureInput[2] = requestDto.Currency;
            signatureInput[3] = requestDto.ReferenceNo;
            signatureInput[4] = requestDto.TerminalId;

            return Sha512ForRemote(signatureInput);
        }

        public bool CancelRemotePayment(string terminalId)
        {
            try
            {
                string[] signatureInput = new string[2];
                signatureInput[0] = _signatureKey;
                signatureInput[1] = terminalId;

                string signature = Sha512ForRemote(signatureInput);
                _logger?.LogInformation("CancelRemotePayment");

                var data = new { TerminalId = terminalId, Signature = signature };
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger?.LogInformation("CancelRemotePayment : " + json);

                var response = _client.PostAsync(_HOST_URL + "/apim/merchant/SoftPOSCancel", content).Result;
                string responseString = response.Content.ReadAsStringAsync().Result;
                _logger?.LogInformation($"CancelRemotePayment Response from server: {response.StatusCode} {responseString}");
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "CancelRemotePayment Exception:");
                return false;
            }
        }
    }
}
