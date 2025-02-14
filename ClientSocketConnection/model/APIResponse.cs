using Newtonsoft.Json;
using System.Collections.Generic;


namespace ClientSocketConnection
{
    class APIResponse
    {
        public class AuthToken
        {
            public string Auth { get; set; }
            public string CompanyName { get; set; }
            public string UserID { get; set; }
            public List<UserMethod> Methods { get; set; }
            public List<TerminalList> Terminals { get; set; }

            [JsonProperty("tidLogin")]
            public string TidLogin { get; set; }
            public List<SettingList> Settings { get; set; }

        }

        public class TerminalList
        {
            public string TerminalID { get; set; }
            public string BranchName { get; set; }

        }

        public class MethodEnable
        {
            public string TerminalID { get; set; }
            [JsonProperty("Enable")]
            public string Enable { get; set; }

        }
        public class SettingList
        {
            public string Method { get; set; }
            public string Visa { get; set; }
            public string Mastercard { get; set; }
            public string Unionpay { get; set; }
        }
        public class UserMethod
        {
            public string Method { get; set; }
            public string Cid { get; set; }
            public string Remarks { get; set; }
            public string Token { get; set; }
            public List<MethodEnable> Enable { get; set; }
        }

        public class SignatureResp
        {
            public string AuthToken { get; set; }
            public Merchants Merchant { get; set; }
        }

        public class Merchants
        {
            public string RemID { get; set; }
            public string SignatureKey { get; set; }
        }

    }
}
