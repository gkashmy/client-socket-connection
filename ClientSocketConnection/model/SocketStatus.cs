namespace ClientSocketConnection.model
{
    public class SocketStatus
    {
        private SocketConnectivityCallback socketStatus = SocketConnectivityCallback.DEFAULT;
        private TransactionEventCallback transactionEventCallback = TransactionEventCallback.DEFAULT;
        private PaymentType transactionType = PaymentType.CARD_PAYMENT;
        private IDataCallback callback;

        public SocketStatus(IDataCallback callback)
        {
            this.callback = callback;
        }

        public void SetSocketStatus(SocketConnectivityCallback status)
        {
            socketStatus = status;
            callback.SocketStatusCallback(status);
        }

        public void SetTransactionStatus(TransactionEventCallback status)
        {
            transactionEventCallback = status;
            callback.TransactionEventCallback(status);
        }

        public void SetTransactionType(PaymentType type)
        {
            transactionType = type;
        }

        public SocketConnectivityCallback GetSocketStatus()
        {
            return socketStatus;
        }

        public TransactionEventCallback GetTransactionStatus()
        {
            return transactionEventCallback;
        }

        public PaymentType GetTransactionType()
        {
            return transactionType;
        }

        public enum PaymentType
        {       
            EWALLET_SCAN_PAYMENT,
            TAPTOPHONE_PAYMENT,
            CARD_PAYMENT,
            MAYBANK_QR,
            GRABPAY_QR,
            TNG_QR,
            GKASH_QR,
            BOOST_QR,
            WECHAT_QR,
            SHOPEE_PAY_QR,
            ALIPAY_QR,
            ATOME_QR,
            MCASH_QR,
            DUITNOW_QR,
            FINEXUS_DUITNOW_QR,
            SARAWAK_PAY_QR
        }

        public enum TransactionEventCallback
        {
            CONNECTION_OK,
            CHECK_CONNECTION,            
            INIT_PAYMENT,
            TRANSACTION_PROCESSING,
            DISPLAYING_QR,
            SCANNING_QR,
            READY_TO_READ_CARD,
            INPUT_PIN,
            RETRIEVING_PAYMENT_STATUS,
            RETRIEVED_STATUS,
            QUERY_STATUS,
            INVALID_SIGNATURE,
            CANCEL_PAYMENT,
            INVALID_PAYMENT_TYPE,
            INVALID_METHOD,
            INVALID_AMOUNT,
            GET_KEY_FAIL,
            DEVICE_OFFLINE,
            NO_CARD_DETECTED_TIMEOUT,
            NO_PIN_DETECTED_TIMEOUT,
            CHECK_TERMINAL_STATUS,
            TRY_AGAIN,
            RECONNECTED,
            LAST_TRANS_STATUS,
            DONE,
            DEFAULT
        }

        public enum SocketConnectivityCallback
        {          
            ONLINE,
            CONNECTED,
            DISCONNECTED,
            RECONNECTING,
            HOST_NOT_FOUND,
            LOGIN_FAIL,
            RETRIEVE_IP_FAIL,
            RETRIEVE_KEY_FAIL,
            AUTH_ERROR,
            DEFAULT
        }

        public enum TransactionCallbackType
        {
            TRANSACTION_RESULT,
            TRANSACTION_STATUS
        }
    }
}
