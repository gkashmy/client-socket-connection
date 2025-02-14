using static ClientSocketConnection.model.SocketStatus;

namespace ClientSocketConnection.model
{
    public class PaymentRequestDto
    {
        public string Amount { get; set; }
        public string Currency { get; set; }
        public string TerminalId { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }
        public string ReferenceNo { get; set; }
        public string CallbackURL { get; set; }
        public PaymentType PaymentType { get; set; }
        public bool PreAuth { get; set; }
        public string Signature { get;set; }
    }
}
