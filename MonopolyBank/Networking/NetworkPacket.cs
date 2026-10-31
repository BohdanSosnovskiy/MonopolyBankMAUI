using MonopolyBank.Models;
using System.Text.Json;

namespace MonopolyBank.Networking
{
    public static class PacketTypes
    {
        public const string JoinRequest = "JoinRequest";
        public const string GameStateSnapshot = "GameStateSnapshot";
        public const string PlayerJoined = "PlayerJoined";
        public const string BalanceChanged = "BalanceChanged";
        public const string CompanyBought = "CompanyBought";
        public const string CompanyBanChanged = "CompanyBanChanged";
        public const string CompanySold = "CompanySold";
        public const string BranchBuilt = "BranchBuilt";
        public const string HistoryAdded = "HistoryAdded";
        public const string TradeOpen = "TradeOpen";
        public const string TradeClose = "TradeClose";
        public const string TradeUpdate = "TradeUpdate";
        public const string TradeConfirmed = "TradeConfirmed";
        public const string TradeExecuted = "TradeExecuted";
        public const string GameOver = "GameOver";
        public const string AuctionStart = "AuctionStart";
        public const string AuctionBid = "AuctionBid";
        public const string AuctionEnd = "AuctionEnd";
        public const string RequestSnapshot = "RequestSnapshot";
        public const string GameClosed = "GameClosed";
    }

    public class NetworkPacket
    {
        public string Type { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? PayloadJson { get; set; }

        public static NetworkPacket Create<T>(string type, string senderId, string senderName, T payload)
        {
            return new NetworkPacket
            {
                Type = type,
                SenderId = senderId,
                SenderName = senderName,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        public T? GetPayload<T>()
        {
            if (string.IsNullOrEmpty(PayloadJson)) return default;
            try
            {
                return JsonSerializer.Deserialize<T>(PayloadJson);
            }
            catch
            {
                return default;
            }
        }
    }

    public class GameStateSnapshotPayload
    {
        public int CountStartSum { get; set; }
        public int CountCashStart { get; set; }
        public List<UserSnapshot> Users { get; set; } = new();
        public List<CompanySnapshot> Companys { get; set; } = new();
    }

    public class UserSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Cash { get; set; }
        public bool isGameOver { get; set; }
        public bool IsNoNfc { get; set; }
    }

    public class CompanySnapshot
    {
        public int Index { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public int CountBulds { get; set; }
        public bool IsBan { get; set; }
    }

    public class BalanceChangedPayload
    {
        public string UserId { get; set; } = string.Empty;
        public int NewBalance { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class CompanyActionPayload
    {
        public string UserId { get; set; } = string.Empty;
        public int CompanyIndex { get; set; }
        public int BuldCount { get; set; }
        public bool IsBan { get; set; }
    }

    public class TradeUpdatePayload
    {
        public string TargetUserId { get; set; } = string.Empty;
        public int SendingMoney { get; set; }
        public int TargetMoney { get; set; }
        public List<int> OfferedCompanyIndexes { get; set; } = new();
        public List<int> RequestedCompanyIndexes { get; set; } = new();
        public bool IsReadySender { get; set; }
    }

    public class TradeExecutedPayload
    {
        public string User1Id { get; set; } = string.Empty;
        public int User1NewCash { get; set; }
        public string User2Id { get; set; } = string.Empty;
        public int User2NewCash { get; set; }
        public List<int> TransferredToUser1 { get; set; } = new();
        public List<int> TransferredToUser2 { get; set; } = new();
    }

    public class AuctionStartPayload
    {
        public int CompanyIndex { get; set; }
        public string InitiatorUserId { get; set; } = string.Empty;
        public string InitiatorName { get; set; } = string.Empty;
        public int StartingBid { get; set; } = 10;
    }

    public class AuctionBidPayload
    {
        public int CompanyIndex { get; set; }
        public string BidderUserId { get; set; } = string.Empty;
        public string BidderName { get; set; } = string.Empty;
        public int BidAmount { get; set; }
    }

    public class AuctionEndPayload
    {
        public int CompanyIndex { get; set; }
        public string? WinnerUserId { get; set; }
        public string? WinnerName { get; set; }
        public int FinalPrice { get; set; }
    }
}
