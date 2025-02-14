using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientSocketConnection
{
    public class PaymentMethod
    {
        //public readonly string GRABPAY_SCAN = "GrabPayScan";
        //public readonly string GRABPAY_QR = "GrabPayQR";
        //public readonly string BOOST_SCAN = "BoostScan";
        //public readonly string BOOST_QR = "BoostQR";
        //public readonly string WECHAT_SCAN = "WechatScan";
        //public readonly string WECHAT_QR = "WechatQR";
        //public readonly string MAYBANK_SCAN = "MaybankScan";
        //public readonly string TOUCHNGO_SCAN = "TouchNgoScan";
        //public readonly string ALIPAY_SCAN = "AlipayScan";
        //public readonly string ALIPAY_QR = "AlipayQR";
        //public readonly string RAZERPAY_SCAN = "RazerPayScan";
        //public readonly string GKASH_EWALLET_SCAN = "Gkash eWalletScan";
        //public readonly string GKASH_EWALLET_QR = "Gkash eWalletQR";

        //public readonly string TAP_TO_PHONE = "Tap-to-Phone";
        //public readonly string VISA_MASTER = "Visa/Master";
        //public readonly string VIRTUAL_TERMINAL_VISA_MASTER = "Virtual Terminal (Visa/Master)";
        //public readonly string UNIONPAY = "UnionPay";
        //public readonly string VIRTUAL_TERMINAL_UNIONPAY = "Virtual Terminal (UnionPay)";

        public readonly int EWALLET_PAYMENT = 1;
        public readonly int TAP_TO_PHONE = 2;
        public readonly int CARD_PAYMENT = 3;
    }
}
