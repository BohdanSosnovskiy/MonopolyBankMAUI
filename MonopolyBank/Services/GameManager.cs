using MonopolyBank.Models;
using MonopolyBank.Networking;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MonopolyBank.Services
{
    public class GameManager : INotifyPropertyChanged
    {
        private static GameManager? _instance;
        public static GameManager Instance => _instance ??= new GameManager();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public bool IsHost { get; private set; }
        private User _currentUser = new("Игрок", 15000);
        public User CurrentUser
        {
            get => _currentUser;
            set
            {
                _currentUser = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<User> Users { get; set; } = new();
        public ObservableCollection<Company> Companys { get; set; } = new();
        public ObservableCollection<History> Historys { get; set; } = new();

        public List<Card_kazna> TreasuryCards { get; set; } = new();
        public List<Card_Shans> ChanceCards { get; set; } = new();

        private int _countStartSum = 200;
        public int CountStartSum
        {
            get => _countStartSum;
            set
            {
                _countStartSum = value;
                OnPropertyChanged();
            }
        }

        private int _countCashStart = 15000;
        public int CountCashStart
        {
            get => _countCashStart;
            set
            {
                _countCashStart = value;
                OnPropertyChanged();
            }
        }

        public GameServer? Server { get; private set; }
        public GameClient Client { get; } = new();

        public event Action<string, string>? OnTradeRequested;
        public event Action? OnTradeClosed;
        public event Action<TradeUpdatePayload>? OnTradeUpdated;
        public event Action<TradeExecutedPayload>? OnTradeConfirmed;
        public event Action<User>? OnGameWon;
        public event Action<AuctionStartPayload>? OnAuctionStarted;
        public event Action<AuctionBidPayload>? OnAuctionBidReceived;
        public event Action<AuctionEndPayload>? OnAuctionEnded;

        public bool IsAuctionRunning { get; private set; } = false;
        public int CurrentAuctionCompanyIndex { get; private set; } = -1;

        public GameManager()
        {
            ResetGameData();
            Client.OnPacketReceived += HandleIncomingPacket;
        }

        public void ResetGameData()
        {
            IsAuctionRunning = false;
            CurrentAuctionCompanyIndex = -1;
            Users.Clear();
            Companys.Clear();
            Historys.Clear();

            // Загружаем компании и колоды из активного пресета (или дефолтного)
            var preset = PresetManager.Instance.ActivePreset ?? PresetManager.Instance.CreateOriginalDefaultPreset();
            foreach (var company in preset.Companies)
            {
                var clone = new Company
                {
                    index = company.index,
                    Name = company.Name,
                    nfc_marker = company.nfc_marker,
                    color = company.color,
                    CountColorGroup = company.CountColorGroup,
                    Price = company.Price,
                    PriceFilial = company.PriceFilial,
                    PriceCorp = company.PriceCorp,
                    Deposit = company.Deposit,
                    Selling = company.Selling,
                    IsCompany = company.IsCompany,
                    IsCorp = company.IsCorp,
                    IsBan = false,
                    CountBulds = 0,
                    UserName = ""
                };
                if (company.Orend != null)
                {
                    clone.Orend = (int[])company.Orend.Clone();
                }
                Companys.Add(clone);
            }

            TreasuryCards = preset.TreasuryCards?.ToList() ?? PresetData.CreateTreasuryCards();
            ChanceCards = preset.ChanceCards?.ToList() ?? PresetData.CreateChanceCards();
        }

        private UdpBeaconHost? _beaconHost;

        public async Task StartHostGameAsync(string username, int initialCash, int passGoCash)
        {
            IsHost = true;
            CountCashStart = initialCash;
            CountStartSum = passGoCash;

            if (Server != null)
            {
                Server.Stop();
                Server = null;
            }
            _beaconHost?.Stop();
            _beaconHost = null;
            Client.Disconnect();

            ResetGameData();

            CurrentUser = new User(username, initialCash);
            Users.Add(CurrentUser);

            Server = new GameServer();
            Server.OnPacketReceived += HandleServerPacket;
            _ = Task.Run(Server.StartAsync);

            _beaconHost = new UdpBeaconHost(username, 8888);
            _beaconHost.Start();

            BackgroundServiceManager.StartHostService();

            AddHistory($"Игра создана хостом {username}", "bank_monopoly_logo.png", 0, false);
        }

        public async Task<bool> JoinGameAsync(string hostIp, string username)
        {
            IsHost = false;

            if (Server != null)
            {
                Server.Stop();
                Server = null;
            }
            _beaconHost?.Stop();
            _beaconHost = null;
            Client.Disconnect();

            ResetGameData();

            CurrentUser = new User(username, CountCashStart);
            Users.Add(CurrentUser);

            bool connected = await Client.ConnectAsync(hostIp);
            if (!connected) return false;

            // Send JoinRequest to host
            var packet = NetworkPacket.Create(PacketTypes.JoinRequest, CurrentUser.Id, CurrentUser.Name, new UserSnapshot
            {
                Id = CurrentUser.Id,
                Name = CurrentUser.Name,
                Cash = CurrentUser.Cash,
                isGameOver = CurrentUser.isGameOver
            });

            await Client.SendPacketAsync(packet);
            await Task.Delay(300);
            return true;
        }

        private async void HandleServerPacket(NetworkPacket packet, string clientId)
        {
            if (packet.Type == PacketTypes.RequestSnapshot)
            {
                var snapshot = CreateGameStateSnapshot();
                var snapshotPacket = NetworkPacket.Create(PacketTypes.GameStateSnapshot, CurrentUser.Id, CurrentUser.Name, snapshot);
                await Server!.SendPacketToClientAsync(clientId, snapshotPacket);
                return;
            }

            if (packet.Type == PacketTypes.JoinRequest)
            {
                var userSnapshot = packet.GetPayload<UserSnapshot>();
                if (userSnapshot != null)
                {
                    // 1. Send full GameStateSnapshot to the newly joined client
                    var snapshot = CreateGameStateSnapshot();
                    var snapshotPacket = NetworkPacket.Create(PacketTypes.GameStateSnapshot, CurrentUser.Id, CurrentUser.Name, snapshot);
                    await Server!.SendPacketToClientAsync(clientId, snapshotPacket);

                    // 2. Broadcast PlayerJoined to all other clients
                    var playerJoinedPacket = NetworkPacket.Create(PacketTypes.PlayerJoined, userSnapshot.Id, userSnapshot.Name, userSnapshot);
                    await Server.BroadcastPacketAsync(playerJoinedPacket, clientId);

                    // 3. Add to host's own list on MainThread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!Users.Any(u => u.Id == userSnapshot.Id))
                        {
                            var newUser = new User(userSnapshot.Name, userSnapshot.Cash) { Id = userSnapshot.Id };
                            Users.Add(newUser);
                            AddHistory($"Игрок {userSnapshot.Name} присоединился к игре", "user.png", 0, false);
                        }
                    });
                }
            }
            else
            {
                // 1. Host processes the packet locally for its own game state
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    HandleIncomingPacket(packet);
                });

                // 2. Relay all other packets to all other connected clients
                await Server!.BroadcastPacketAsync(packet, clientId);
            }
        }

        private GameStateSnapshotPayload CreateGameStateSnapshot()
        {
            var snapshot = new GameStateSnapshotPayload
            {
                CountCashStart = CountCashStart,
                CountStartSum = CountStartSum,
                Users = Users.Select(u => new UserSnapshot
                {
                    Id = u.Id,
                    Name = u.Name,
                    Cash = u.Cash,
                    isGameOver = u.isGameOver
                }).ToList(),
                Companys = Companys.Where(c => !string.IsNullOrEmpty(c.UserName)).Select(c => new CompanySnapshot
                {
                    Index = c.index,
                    OwnerName = c.UserName,
                    CountBulds = c.CountBulds,
                    IsBan = c.IsBan
                }).ToList()
            };
            return snapshot;
        }

        private void HandleIncomingPacket(NetworkPacket packet)
        {
            switch (packet.Type)
            {
                case PacketTypes.GameStateSnapshot:
                    var snapshot = packet.GetPayload<GameStateSnapshotPayload>();
                    if (snapshot != null)
                    {
                        CountCashStart = snapshot.CountCashStart;
                        CountStartSum = snapshot.CountStartSum;
                        CurrentUser.Cash = snapshot.CountCashStart;

                        // Rebuild users
                        Users.Clear();
                        Users.Add(CurrentUser);

                        foreach (var u in snapshot.Users)
                        {
                            if (u.Id != CurrentUser.Id)
                            {
                                var existing = Users.FirstOrDefault(x => x.Id == u.Id);
                                if (existing == null)
                                {
                                    Users.Add(new User(u.Name, u.Cash) { Id = u.Id, isGameOver = u.isGameOver });
                                }
                            }
                        }

                        // Rebuild company states
                        foreach (var compSnapshot in snapshot.Companys)
                        {
                            if (compSnapshot.Index >= 0 && compSnapshot.Index < Companys.Count)
                            {
                                var comp = Companys[compSnapshot.Index];
                                comp.UserName = compSnapshot.OwnerName;
                                comp.CountBulds = compSnapshot.CountBulds;
                                comp.IsBan = compSnapshot.IsBan;

                                var owner = Users.FirstOrDefault(u => u.Name == compSnapshot.OwnerName);
                                if (owner != null && !owner.Companys.Contains(comp))
                                {
                                    owner.Companys.Add(comp);
                                }
                            }
                        }

                        AddHistory("Подключено к игре, состояние синхронизировано", "bank_monopoly_logo.png", 0, false);
                    }
                    break;

                case PacketTypes.PlayerJoined:
                    var newUserSnap = packet.GetPayload<UserSnapshot>();
                    if (newUserSnap != null && !Users.Any(u => u.Id == newUserSnap.Id))
                    {
                        var newUser = new User(newUserSnap.Name, newUserSnap.Cash) { Id = newUserSnap.Id };
                        Users.Add(newUser);
                        AddHistory($"Игрок {newUserSnap.Name} вошел в игру", "user.png", 0, false);
                    }
                    break;

                case PacketTypes.BalanceChanged:
                    var balPayload = packet.GetPayload<BalanceChangedPayload>();
                    if (balPayload != null)
                    {
                        var target = Users.FirstOrDefault(u => u.Id == balPayload.UserId);
                        if (target != null)
                        {
                            target.Cash = balPayload.NewBalance;
                        }
                    }
                    break;

                case PacketTypes.CompanyBought:
                    var buyPayload = packet.GetPayload<CompanyActionPayload>();
                    if (buyPayload != null && buyPayload.CompanyIndex >= 0 && buyPayload.CompanyIndex < Companys.Count)
                    {
                        var comp = Companys[buyPayload.CompanyIndex];
                        var buyer = Users.FirstOrDefault(u => u.Id == buyPayload.UserId);
                        if (buyer != null)
                        {
                            comp.UserName = buyer.Name;
                            if (!buyer.Companys.Contains(comp))
                            {
                                buyer.Companys.Add(comp);
                            }
                        }
                    }
                    break;

                case PacketTypes.CompanyBanChanged:
                    var banPayload = packet.GetPayload<CompanyActionPayload>();
                    if (banPayload != null && banPayload.CompanyIndex >= 0 && banPayload.CompanyIndex < Companys.Count)
                    {
                        Companys[banPayload.CompanyIndex].IsBan = banPayload.IsBan;
                    }
                    break;

                case PacketTypes.CompanySold:
                    var sellPayload = packet.GetPayload<CompanyActionPayload>();
                    if (sellPayload != null && sellPayload.CompanyIndex >= 0 && sellPayload.CompanyIndex < Companys.Count)
                    {
                        var comp = Companys[sellPayload.CompanyIndex];
                        comp.Reset();
                        var seller = Users.FirstOrDefault(u => u.Id == sellPayload.UserId);
                        seller?.Companys.Remove(comp);
                    }
                    break;

                case PacketTypes.BranchBuilt:
                    var branchPayload = packet.GetPayload<CompanyActionPayload>();
                    if (branchPayload != null && branchPayload.CompanyIndex >= 0 && branchPayload.CompanyIndex < Companys.Count)
                    {
                        var comp = Companys[branchPayload.CompanyIndex];
                        comp.CountBulds = branchPayload.BuldCount;
                    }
                    break;

                case PacketTypes.HistoryAdded:
                    var hist = packet.GetPayload<History>();
                    if (hist != null)
                    {
                        Historys.Insert(0, hist);
                    }
                    break;

                case PacketTypes.GameOver:
                    var goUserId = packet.GetPayload<string>() ?? packet.PayloadJson?.Trim('"', ' ') ?? string.Empty;
                    var goUser = Users.FirstOrDefault(u => u.Id == goUserId);
                    if (goUser != null)
                    {
                        goUser.isGameOver = true;
                        goUser.Cash = 0;
                        foreach (var c in Companys.Where(comp => comp.UserName == goUser.Name))
                        {
                            c.Reset();
                        }
                        goUser.Companys.Clear();
                        goUser.Companys.Add(new Company { IsNoEmty = false });
                    }
                    CheckWinner();
                    break;

                case PacketTypes.TradeOpen:
                    var openTargetId = packet.GetPayload<string>() ?? packet.PayloadJson?.Trim('"', ' ');
                    if (openTargetId == CurrentUser.Id)
                    {
                        OnTradeRequested?.Invoke(packet.SenderId, packet.SenderName);
                    }
                    break;

                case PacketTypes.TradeClose:
                    var closeTargetId = packet.GetPayload<string>() ?? packet.PayloadJson?.Trim('"', ' ');
                    if (closeTargetId == CurrentUser.Id || string.IsNullOrEmpty(closeTargetId))
                    {
                        OnTradeClosed?.Invoke();
                    }
                    break;

                case PacketTypes.TradeUpdate:
                    var tradeUpdate = packet.GetPayload<TradeUpdatePayload>();
                    if (tradeUpdate != null && tradeUpdate.TargetUserId == CurrentUser.Id)
                    {
                        OnTradeUpdated?.Invoke(tradeUpdate);
                    }
                    break;

                case PacketTypes.TradeExecuted:
                    var tradeExec = packet.GetPayload<TradeExecutedPayload>();
                    if (tradeExec != null)
                    {
                        ApplyTradeExecution(tradeExec);
                        OnTradeConfirmed?.Invoke(tradeExec);
                    }
                    break;

                case PacketTypes.AuctionStart:
                    var startPayload = packet.GetPayload<AuctionStartPayload>();
                    if (startPayload != null)
                    {
                        IsAuctionRunning = true;
                        CurrentAuctionCompanyIndex = startPayload.CompanyIndex;
                        OnAuctionStarted?.Invoke(startPayload);
                    }
                    break;

                case PacketTypes.AuctionBid:
                    var bidPayload = packet.GetPayload<AuctionBidPayload>();
                    if (bidPayload != null)
                    {
                        OnAuctionBidReceived?.Invoke(bidPayload);
                    }
                    break;

                case PacketTypes.AuctionEnd:
                    var endPayload = packet.GetPayload<AuctionEndPayload>();
                    if (endPayload != null)
                    {
                        if (IsAuctionRunning)
                        {
                            IsAuctionRunning = false;
                            CurrentAuctionCompanyIndex = -1;
                            ApplyAuctionEnd(endPayload);
                            OnAuctionEnded?.Invoke(endPayload);
                        }
                    }
                    break;

                case PacketTypes.GameClosed:
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Application.Current?.MainPage?.DisplayAlert("Игра завершена", "Хост завершил текущую игру и закрыл сервер.", "ОК");
                        Client.Disconnect();
                        ResetGameData();
                        await Application.Current?.MainPage?.Navigation.PopToRootAsync();
                    });
                    break;
            }
        }

        public async Task SendOrBroadcastPacketAsync(NetworkPacket packet)
        {
            try
            {
                if (IsHost)
                {
                    if (Server != null)
                    {
                        await Server.BroadcastPacketAsync(packet);
                    }
                }
                else
                {
                    if (Client != null && Client.IsConnected)
                    {
                        await Client.SendPacketAsync(packet);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendOrBroadcastPacketAsync] Error: {ex.Message}");
            }
        }

        public async Task UpdateBalanceAsync(string userId, int newCash, string reason = "")
        {
            var user = Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.Cash = newCash;
            }
            if (CurrentUser.Id == userId)
            {
                CurrentUser.Cash = newCash;
            }

            var packet = NetworkPacket.Create(PacketTypes.BalanceChanged, CurrentUser.Id, CurrentUser.Name, new BalanceChangedPayload
            {
                UserId = userId,
                NewBalance = newCash,
                Reason = reason
            });
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task BuyCompanyAsync(string userId, int companyIndex)
        {
            if (companyIndex < 0 || companyIndex >= Companys.Count) return;
            var comp = Companys[companyIndex];
            var buyer = Users.FirstOrDefault(u => u.Id == userId);
            if (buyer == null) return;

            comp.UserName = buyer.Name;
            if (!buyer.Companys.Contains(comp))
            {
                buyer.Companys.Add(comp);
            }
            if (CurrentUser.Id == userId && !CurrentUser.Companys.Contains(comp))
            {
                CurrentUser.Companys.Add(comp);
            }

            var packet = NetworkPacket.Create(PacketTypes.CompanyBought, CurrentUser.Id, CurrentUser.Name, new CompanyActionPayload
            {
                UserId = userId,
                CompanyIndex = companyIndex
            });
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task SellCompanyAsync(string userId, int companyIndex)
        {
            if (companyIndex < 0 || companyIndex >= Companys.Count) return;
            var comp = Companys[companyIndex];
            var seller = Users.FirstOrDefault(u => u.Id == userId);

            comp.Reset();
            seller?.Companys.Remove(comp);
            if (CurrentUser.Id == userId)
            {
                CurrentUser.Companys.Remove(comp);
            }

            var packet = NetworkPacket.Create(PacketTypes.CompanySold, CurrentUser.Id, CurrentUser.Name, new CompanyActionPayload
            {
                UserId = userId,
                CompanyIndex = companyIndex
            });
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task BuildBranchAsync(int companyIndex, int newCount)
        {
            if (companyIndex < 0 || companyIndex >= Companys.Count) return;
            var comp = Companys[companyIndex];
            comp.CountBulds = newCount;

            var packet = NetworkPacket.Create(PacketTypes.BranchBuilt, CurrentUser.Id, CurrentUser.Name, new CompanyActionPayload
            {
                UserId = CurrentUser.Id,
                CompanyIndex = companyIndex,
                BuldCount = newCount
            });
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task SetCompanyBanAsync(int companyIndex, bool isBan)
        {
            if (companyIndex < 0 || companyIndex >= Companys.Count) return;
            var comp = Companys[companyIndex];
            comp.IsBan = isBan;

            var packet = NetworkPacket.Create(PacketTypes.CompanyBanChanged, CurrentUser.Id, CurrentUser.Name, new CompanyActionPayload
            {
                CompanyIndex = companyIndex,
                IsBan = isBan
            });
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task AddHistoryAsync(string text, string img, int cash, bool isVisibleCash)
        {
            var hist = new History
            {
                Text = text,
                name_img = img,
                Cash = cash,
                isVisibleCash = isVisibleCash
            };
            AddHistory(text, img, cash, isVisibleCash);

            var packet = NetworkPacket.Create(PacketTypes.HistoryAdded, CurrentUser.Id, CurrentUser.Name, hist);
            await SendOrBroadcastPacketAsync(packet);
        }

        public void AddHistory(string text, string img, int cash, bool isVisibleCash)
        {
            Historys.Insert(0, new History
            {
                Text = text,
                name_img = img,
                Cash = cash,
                isVisibleCash = isVisibleCash
            });
        }

        public async Task BroadcastGameOverAsync(string userId)
        {
            var user = Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.isGameOver = true;
                user.Cash = 0;
                foreach (var c in Companys.Where(comp => comp.UserName == user.Name))
                {
                    c.Reset();
                }
                user.Companys.Clear();
                user.Companys.Add(new Company { IsNoEmty = false });
            }

            var packet = NetworkPacket.Create(PacketTypes.GameOver, CurrentUser.Id, CurrentUser.Name, userId);
            await SendOrBroadcastPacketAsync(packet);

            CheckWinner();
        }

        public void CheckWinner()
        {
            if (Users.Count > 1)
            {
                var activePlayers = Users.Where(u => !u.isGameOver).ToList();
                if (activePlayers.Count == 1)
                {
                    OnGameWon?.Invoke(activePlayers.First());
                }
            }
        }

        public async Task SendTradeOpenAsync(string targetUserId)
        {
            var packet = NetworkPacket.Create(PacketTypes.TradeOpen, CurrentUser.Id, CurrentUser.Name, targetUserId);
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task SendTradeCloseAsync(string targetUserId)
        {
            var packet = NetworkPacket.Create(PacketTypes.TradeClose, CurrentUser.Id, CurrentUser.Name, targetUserId);
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task SendTradeUpdateAsync(TradeUpdatePayload payload)
        {
            var packet = NetworkPacket.Create(PacketTypes.TradeUpdate, CurrentUser.Id, CurrentUser.Name, payload);
            await SendOrBroadcastPacketAsync(packet);
        }

        public async Task ExecuteTradeAsync(TradeExecutedPayload payload)
        {
            ApplyTradeExecution(payload);
            var packet = NetworkPacket.Create(PacketTypes.TradeExecuted, CurrentUser.Id, CurrentUser.Name, payload);
            await SendOrBroadcastPacketAsync(packet);
        }

        private void ApplyTradeExecution(TradeExecutedPayload payload)
        {
            var u1 = Users.FirstOrDefault(u => u.Id == payload.User1Id);
            var u2 = Users.FirstOrDefault(u => u.Id == payload.User2Id);

            if (u1 != null) u1.Cash = payload.User1NewCash;
            if (u2 != null) u2.Cash = payload.User2NewCash;

            if (CurrentUser.Id == payload.User1Id) CurrentUser.Cash = payload.User1NewCash;
            if (CurrentUser.Id == payload.User2Id) CurrentUser.Cash = payload.User2NewCash;

            // Transfer to u1
            foreach (var idx in payload.TransferredToUser1)
            {
                if (idx >= 0 && idx < Companys.Count)
                {
                    var c = Companys[idx];
                    u2?.Companys.Remove(c);
                    c.UserName = u1?.Name ?? "";
                    c.Sdelka = false;
                    if (u1 != null && !u1.Companys.Contains(c)) u1.Companys.Add(c);
                    if (CurrentUser.Id == payload.User1Id && !CurrentUser.Companys.Contains(c)) CurrentUser.Companys.Add(c);
                    if (CurrentUser.Id == payload.User2Id) CurrentUser.Companys.Remove(c);
                }
            }

            // Transfer to u2
            foreach (var idx in payload.TransferredToUser2)
            {
                if (idx >= 0 && idx < Companys.Count)
                {
                    var c = Companys[idx];
                    u1?.Companys.Remove(c);
                    c.UserName = u2?.Name ?? "";
                    c.Sdelka = false;
                    if (u2 != null && !u2.Companys.Contains(c)) u2.Companys.Add(c);
                    if (CurrentUser.Id == payload.User2Id && !CurrentUser.Companys.Contains(c)) CurrentUser.Companys.Add(c);
                    if (CurrentUser.Id == payload.User1Id) CurrentUser.Companys.Remove(c);
                }
            }

            CleanPlaceholder(u1);
            CleanPlaceholder(u2);
            if (CurrentUser.Id == payload.User1Id || CurrentUser.Id == payload.User2Id)
            {
                CleanPlaceholder(CurrentUser);
            }
        }

        public void CleanPlaceholder(User? user)
        {
            if (user == null) return;
            var empties = user.Companys.Where(c => c.IsEmpty).ToList();
            var reals = user.Companys.Where(c => c.IsNoEmty).ToList();
            if (reals.Count > 0)
            {
                foreach (var empty in empties) user.Companys.Remove(empty);
            }
            else if (empties.Count == 0)
            {
                user.Companys.Add(new Company { IsNoEmty = false });
            }
        }

        public async Task StartAuctionAsync(int companyIndex, int startingBid = 10)
        {
            IsAuctionRunning = true;
            CurrentAuctionCompanyIndex = companyIndex;

            var payload = new AuctionStartPayload
            {
                CompanyIndex = companyIndex,
                InitiatorUserId = CurrentUser.Id,
                InitiatorName = CurrentUser.Name,
                StartingBid = startingBid
            };

            var packet = NetworkPacket.Create(PacketTypes.AuctionStart, CurrentUser.Id, CurrentUser.Name, payload);
            await SendOrBroadcastPacketAsync(packet);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnAuctionStarted?.Invoke(payload);
            });
        }

        public async Task SendAuctionBidAsync(int companyIndex, int bidAmount)
        {
            var payload = new AuctionBidPayload
            {
                CompanyIndex = companyIndex,
                BidderUserId = CurrentUser.Id,
                BidderName = CurrentUser.Name,
                BidAmount = bidAmount
            };

            var packet = NetworkPacket.Create(PacketTypes.AuctionBid, CurrentUser.Id, CurrentUser.Name, payload);
            await SendOrBroadcastPacketAsync(packet);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnAuctionBidReceived?.Invoke(payload);
            });
        }

        public async Task SendAuctionEndAsync(int companyIndex, string? winnerUserId, int finalPrice)
        {
            if (!IsAuctionRunning) return;
            IsAuctionRunning = false;
            CurrentAuctionCompanyIndex = -1;

            var winner = Users.FirstOrDefault(u => u.Id == winnerUserId);
            var payload = new AuctionEndPayload
            {
                CompanyIndex = companyIndex,
                WinnerUserId = winnerUserId,
                WinnerName = winner?.Name,
                FinalPrice = finalPrice
            };

            var packet = NetworkPacket.Create(PacketTypes.AuctionEnd, CurrentUser.Id, CurrentUser.Name, payload);
            await SendOrBroadcastPacketAsync(packet);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyAuctionEnd(payload);
                OnAuctionEnded?.Invoke(payload);
            });
        }

        private void ApplyAuctionEnd(AuctionEndPayload payload)
        {
            if (payload.CompanyIndex < 0 || payload.CompanyIndex >= Companys.Count) return;
            var comp = Companys[payload.CompanyIndex];

            if (!string.IsNullOrEmpty(payload.WinnerUserId))
            {
                var winner = Users.FirstOrDefault(u => u.Id == payload.WinnerUserId);
                if (winner != null)
                {
                    winner.Cash -= payload.FinalPrice;
                    if (CurrentUser.Id == winner.Id)
                    {
                        CurrentUser.Cash = winner.Cash;
                    }

                    comp.UserName = winner.Name;
                    if (!winner.Companys.Contains(comp))
                    {
                        winner.Companys.Add(comp);
                    }
                    if (CurrentUser.Id == winner.Id && !CurrentUser.Companys.Contains(comp))
                    {
                        CurrentUser.Companys.Add(comp);
                    }
                    CleanPlaceholder(winner);
                    if (CurrentUser.Id == winner.Id)
                    {
                        CleanPlaceholder(CurrentUser);
                    }

                    AddHistory($"Игрок {winner.Name} выиграл на аукционе {comp.Name} за {payload.FinalPrice}$", "torg_money.png", payload.FinalPrice, true);
                }
            }
            else
            {
                AddHistory($"Аукцион по фирме {comp.Name} завершился без покупателя", "torg_money.png", comp.Price, false);
            }
        }

        public async Task EndGameAsync()
        {
            if (IsHost)
            {
                try
                {
                    var packet = NetworkPacket.Create(PacketTypes.GameClosed, CurrentUser.Id, CurrentUser.Name, "Хост закрыл игру");
                    await SendOrBroadcastPacketAsync(packet);
                    await Task.Delay(200);
                }
                catch { }

                _beaconHost?.Stop();
                _beaconHost = null;

                Server?.Stop();
                Server = null;

                BackgroundServiceManager.StopHostService();
            }
            else
            {
                Client.Disconnect();
            }

            ResetGameData();
        }

        public async Task EnsureConnectedAndSynchronizedAsync()
        {
            if (IsHost) return;

            try
            {
                if (!Client.IsConnected)
                {
                    var hostIp = Preferences.Get("LastHostIp", "");
                    if (!string.IsNullOrEmpty(hostIp))
                    {
                        bool connected = await Client.ConnectAsync(hostIp);
                        if (connected)
                        {
                            var req = NetworkPacket.Create(PacketTypes.RequestSnapshot, CurrentUser.Id, CurrentUser.Name, CurrentUser.Id);
                            await Client.SendPacketAsync(req);
                        }
                    }
                }
                else
                {
                    var req = NetworkPacket.Create(PacketTypes.RequestSnapshot, CurrentUser.Id, CurrentUser.Name, CurrentUser.Id);
                    await Client.SendPacketAsync(req);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Resync] Error: {ex.Message}");
            }
        }

        public static string GetLocalIpAddress()
        {
            try
            {
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        var props = ni.GetIPProperties();
                        foreach (var addr in props.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !System.Net.IPAddress.IsLoopback(addr.Address))
                            {
                                return addr.Address.ToString();
                            }
                        }
                    }
                }

                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "Не удалось определить";
        }
    }
}
