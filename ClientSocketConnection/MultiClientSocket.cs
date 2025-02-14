using System.Collections.Generic;

namespace ClientSocketConnection
{
    public class MultiClientSocket
    {
        private readonly Dictionary<string, ClientSocket> multiClientSocketDict = new Dictionary<string, ClientSocket>();

        public void AddClient(string TerminalId, ClientSocket clientSocket)
        {          
            multiClientSocketDict.Add(TerminalId, clientSocket);
        }

        public ClientSocket GetClientSocket(string TerminalId)
        {
            multiClientSocketDict.TryGetValue(TerminalId, out ClientSocket socket);
            return socket;
        }
    }
}
