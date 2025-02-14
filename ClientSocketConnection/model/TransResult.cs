using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientSocketConnection.model
{
    public class TransResult
    {
        public class CardResult
        {
            public string type { get; set; }
            public TransactionStatus result { get; set; }
        }

        public class TransactionStatus
        {
            public string ApplicationId { get; set; }
            public string AuthIDResponse { get; set; }
            public string ResponseOrderNumber { get; set; }
            public string CardNo { get; set; }
            public string CardType { get; set; }
            public string CartID { get; set; }
            public string CompanyRemID { get; set; }
            public string MID { get; set; }
            public string Message { get; set; }
            public string Method { get; set; }
            public string RemID { get; set; }
            public string SettlementBatchNumber { get; set; }
            public string SignatureRequired { get; set; }
            public string Status { get; set; }
            public string TID { get; set; }
            public string TVR { get; set; }
            public string TraceNo { get; set; }
            public string TransferAmount { get; set; }
            public string TransferCurrency { get; set; }
            public string TransferDate { get; set; }
            public string TxType { get; set; }
            public string Signature { get; set; }
        }
    }
}
