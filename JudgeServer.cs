using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sport
{
    public class JudgeServer
    {
        private readonly Form1 mainForm;
        private AdminForm adminForm;

        private UdpClient udpServer;
        private TcpListener tcpServer;

        private readonly int tcpPort;
        private readonly string serverName;

        private readonly List<TcpClient> connectedJudges = new List<TcpClient>();
        private readonly Dictionary<TcpClient, string> judgeConnections = new Dictionary<TcpClient, string>();

        // Храним писателей, чтобы не закрывать стримы при рассылке
        private readonly Dictionary<TcpClient, StreamWriter> clientWriters = new Dictionary<TcpClient, StreamWriter>();

        private CancellationTokenSource cts;
        private bool isRunning;

        // Сопоставление judgeId -> индекс судьи (0..3)
        private readonly Dictionary<string, int> judgeIdToIndexMap = new Dictionary<string, int>();
        private int nextJudgeIndex = 0;

        // Текущая информация о бое (рассылается вновь подключившимся и при обновлениях)
        private MatchInfoMessage currentMatchInfo;

        public JudgeServer(Form1 form, int port = 9000, string name = "TKD_Score_Server")
        {
            mainForm = form;
            tcpPort = port;
            serverName = name;

            currentMatchInfo = new MatchInfoMessage
            {
                type = "match_info",
                matchId = "M-" + DateTime.Now.ToString("yyyy-MM-dd") + "-001",
                discipline = "Туль",
                ageCategory = "Взрослые",
                weightCategory = "до 68 кг",
                blueName = "Синий спортсмен",
                redName = "Красный спортсмен"
            };
        }

        public void SetAdminForm(AdminForm adminForm)
        {
            this.adminForm = adminForm;
        }

        public async Task StartServer()
        {
            cts = new CancellationTokenSource();
            isRunning = true;

            _ = Task.Run(() => StartUdpDiscovery(cts.Token));
            _ = Task.Run(() => StartTcpServer(cts.Token));

            await Task.CompletedTask;
        }

        public void StopServer()
        {
            isRunning = false;
            cts?.Cancel();

            try { udpServer?.Close(); } catch { }
            try { tcpServer?.Stop(); } catch { }

            lock (clientWriters)
            {
                foreach (var kv in clientWriters.ToList())
                {
                    try { kv.Value?.Dispose(); } catch { }
                }
                clientWriters.Clear();
            }

            foreach (var client in connectedJudges.ToList())
            {
                try { client.Close(); } catch { }
            }
            connectedJudges.Clear();
            judgeConnections.Clear();
        }

        // ------------------------- UDP DISCOVERY -------------------------

        private async Task StartUdpDiscovery(CancellationToken token)
        {
            try
            {
                udpServer = new UdpClient(45454);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpServer.ReceiveAsync();
                        string message = Encoding.UTF8.GetString(result.Buffer);

                        // более лояльная проверка — достаточно вхождения строки
                        if (message.Contains("TKDJUDGE_DISCOVER"))
                        {
                            string response = $"TKDJUDGE_HERE;port={tcpPort};name={serverName}";
                            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                            await udpServer.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                            adminForm?.AddToTerminal($"[UDP] discover от {result.RemoteEndPoint} -> {response}\n");
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // сокет закрыт при остановке — просто выходим
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (isRunning)
                            adminForm?.AddToTerminal($"[UDP] Ошибка: {ex.Message}\n");
                    }

                    await Task.Delay(100, token);
                }
            }
            catch (Exception ex)
            {
                mainForm.Invoke(new Action(() =>
                    adminForm?.AddToTerminal($"UDP Server Error: {ex.Message}")));
            }
        }

        // ------------------------- TCP SERVER -------------------------

        private async Task StartTcpServer(CancellationToken token)
        {
            try
            {
                tcpServer = new TcpListener(IPAddress.Any, tcpPort);
                tcpServer.Start();

                mainForm.Invoke(new Action(() =>
                    adminForm?.AddToTerminal($"TCP Server started on port {tcpPort}")));

                while (!token.IsCancellationRequested)
                {
                    var tcpClient = await tcpServer.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleJudgeConnection(tcpClient, token));
                }
            }
            catch (ObjectDisposedException)
            {
                // сервер закрыт — нормально при остановке
            }
            catch (Exception ex)
            {
                if (isRunning)
                    mainForm.Invoke(new Action(() =>
                        adminForm?.AddToTerminal($"TCP Server Error: {ex.Message}")));
            }
        }

        private async Task HandleJudgeConnection(TcpClient client, CancellationToken token)
        {
            string clientInfo = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            lock (connectedJudges) connectedJudges.Add(client);
            adminForm?.AddToTerminal($"📡 Подключение: {clientInfo}\n");

            mainForm.Invoke(new Action(() =>
                adminForm?.AddToTerminal($"Judge connected: {clientInfo}")));

            StreamReader reader = null;
            StreamWriter writer = null;

            try
            {
                var stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                lock (clientWriters) clientWriters[client] = writer;

                while (!token.IsCancellationRequested && client.Connected)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break; // разрыв
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    adminForm?.AddToTerminal($"← ВХОД: {line}\n");

                    JudgeMessage message = null;
                    try
                    {
                        message = JsonSerializer.Deserialize<JudgeMessage>(line);
                    }
                    catch (JsonException ex)
                    {
                        adminForm?.AddToTerminal($"❌ Ошибка JSON: {ex.Message}\n");
                        var errorResponse = new ServerMessage
                        {
                            type = "error",
                            status = false,
                            msg_id = 0,
                            message = $"Invalid JSON: {ex.Message}"
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                        continue;
                    }

                    await ProcessJudgeMessage(message, writer, client);
                }
            }
            catch (IOException)
            {
                // клиент отвалился
            }
            catch (Exception ex)
            {
                adminForm?.AddToTerminal($"❌ Ошибка соединения: {ex.Message}\n");
            }
            finally
            {
                lock (connectedJudges) connectedJudges.Remove(client);
                lock (clientWriters) clientWriters.Remove(client);
                judgeConnections.Remove(client);

                try { writer?.Dispose(); } catch { }
                try { reader?.Dispose(); } catch { }
                try { client.Close(); } catch { }

                adminForm?.AddToTerminal($"📡 Отключение: {clientInfo}\n");
            }
        }

        // ------------------------- MESSAGE ROUTING -------------------------

        private async Task ProcessJudgeMessage(JudgeMessage message, StreamWriter writer, TcpClient client)
        {
            try
            {
                // Быстрый путь для "score" (поддерживаем старую ветку)
                if (message.type == "score")
                {
                    await ProcessScoreMessage(message, writer);
                    return;
                }

                switch (message.type)
                {
                    case "hello":
                        await ProcessHelloMessage(message, writer, client);
                        break;

                    case "score_update":
                        await ProcessScoreUpdateMessage(message, writer);
                        break;

                    case "penalty_update":
                        await ProcessPenaltyUpdateMessage(message, writer);
                        break;

                    default:
                        await ProcessUnknownMessageType(message, writer);
                        break;
                }
            }
            catch (Exception ex)
            {
                await ProcessErrorMessage(message, writer, ex);
            }
        }

        // ------------------------- HANDLERS -------------------------

        private async Task ProcessHelloMessage(JudgeMessage message, StreamWriter writer, TcpClient client)
        {
            // Простая валидация
            if (string.IsNullOrWhiteSpace(message.judgeId) || string.IsNullOrWhiteSpace(message.judgeName))
            {
                //adminForm?.AddToTerminal($"false\n");
                var badAck = new
                {
                    type = "ack",
                    ackType = "hello",
                    ok = false
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(badAck));
                return;
            }

            string judgeInfo = $"{message.judgeName} ({message.judgeId})";

            // Регистрируем судью в mapping
            if (!judgeIdToIndexMap.ContainsKey(message.judgeId))
            {
                judgeIdToIndexMap[message.judgeId] = nextJudgeIndex;
                nextJudgeIndex = (nextJudgeIndex + 1) % 4; // циклически 0..3
            }

            if (judgeConnections.ContainsKey(client))
                judgeConnections[client] = judgeInfo;
            else
                judgeConnections.Add(client, judgeInfo);

            // ACK в формате, который ждёт приложение: {"type":"ack","ackType":"hello","ok":true}
            var helloAck = new
            {
                type = "ack",
                ackType = "hello",
                ok = true
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(helloAck));

            // Сразу шлём match_info текущего боя
            await writer.WriteLineAsync(JsonSerializer.Serialize(currentMatchInfo));

            mainForm.Invoke(new Action(() =>
                MessageBox.Show($"Judge registered: {judgeInfo}")));
        }

        private async Task ProcessScoreMessage(JudgeMessage message, StreamWriter writer)
        {
            try
            {
                adminForm?.AddToTerminal($"⚽ Обработка score: judgeId={message.judgeId}, side={message.side}, value={message.value}\n");

                // Валидация
                if (string.IsNullOrEmpty(message.judgeId))
                {
                    await SendAckResponse(writer, "score", message.seq, false, "Judge ID is required");
                    return;
                }
                if (string.IsNullOrEmpty(message.side) || (message.side != "blue" && message.side != "red"))
                {
                    await SendAckResponse(writer, "score", message.seq, false, "Side must be 'blue' or 'red'");
                    return;
                }
                if (message.value < 1 || message.value > 5)
                {
                    await SendAckResponse(writer, "score", message.seq, false, "Value must be between 1 and 5");
                    return;
                }
                if (string.IsNullOrEmpty(message.matchId))
                {
                    await SendAckResponse(writer, "score", message.seq, false, "Match ID is required");
                    return;
                }

                // В какую колонку писать
                int athleteIndex = message.side == "blue" ? 0 : 1;

                // Индекс судьи по его ID
                int judgeIndex = GetJudgeIndexFromId(message.judgeId);

                // Обновляем UI (оценка)
                mainForm.Invoke(new Action(() =>
                {
                    mainForm.UpdateJudgeScore(judgeIndex, athleteIndex, message.value);
                }));

                // Сохраняем в БД
                mainForm.Invoke(new Action(() =>
                {
                    mainForm.SaveScoreEventToDatabase(judgeIndex + 1, athleteIndex + 1, message.value, 0);
                }));

                adminForm?.AddToTerminal("✓ Score обработан успешно\n");

                // Положительный ACK
                await SendAckResponse(writer, "score", message.seq, true, null);
            }
            catch (Exception ex)
            {
                adminForm?.AddToTerminal($"❌ Ошибка обработки score: {ex.Message}\n");
                await SendAckResponse(writer, "score", message.seq, false, ex.Message);
            }
        }

        private async Task ProcessScoreUpdateMessage(JudgeMessage message, StreamWriter writer)
        {
            // Обычные судьи (1-4) могут выставлять только оценки через эту ветку
            if (message.judge_id >= 1 && message.judge_id <= 4)
            {
                mainForm.Invoke(new Action(() =>
                {
                    int athleteIndex = message.athlete == "athlete_a" ? 0 : 1;
                    mainForm.UpdateJudgeScore(message.judge_id - 1, athleteIndex, message.score);
                }));

                var ackResponse = new ServerMessage
                {
                    type = "ack",
                    status = true,
                    msg_id = message.msg_id,
                    ackType = "score_update",
                    ok = true,
                    message = "Score updated successfully"
                };
                string ackJson = JsonSerializer.Serialize(ackResponse);
                await writer.WriteLineAsync(ackJson);
            }
            else
            {
                var errorResponse = new ServerMessage
                {
                    type = "error",
                    status = false,
                    msg_id = message.msg_id,
                    message = "Главный судья может выставлять только штрафные очки"
                };
                string errorJson = JsonSerializer.Serialize(errorResponse);
                await writer.WriteLineAsync(errorJson);
            }
        }

        private async Task ProcessPenaltyUpdateMessage(JudgeMessage message, StreamWriter writer)
        {
            // Только главный судья (judge_id = 5) может выставлять штрафы
            if (message.judge_id == 5)
            {
                mainForm.Invoke(new Action(() =>
                {
                    int athleteIndex = message.athlete == "athlete_a" ? 0 : 1;
                    mainForm.UpdatePenaltyScore(athleteIndex, message.penalty);
                }));

                var penaltyAck = new ServerMessage
                {
                    type = "ack",
                    status = true,
                    msg_id = message.msg_id,
                    ackType = "penalty_update",
                    ok = true,
                    message = "Penalty updated successfully"
                };
                string penaltyJson = JsonSerializer.Serialize(penaltyAck);
                await writer.WriteLineAsync(penaltyJson);
            }
            else
            {
                var errorResponse = new ServerMessage
                {
                    type = "error",
                    status = false,
                    msg_id = message.msg_id,
                    message = "Только главный судья может выставлять штрафные очки"
                };
                string errorJson = JsonSerializer.Serialize(errorResponse);
                await writer.WriteLineAsync(errorJson);
            }
        }

        private async Task ProcessUnknownMessageType(JudgeMessage message, StreamWriter writer)
        {
            var errorResponse = new ServerMessage
            {
                type = "error",
                status = false,
                msg_id = message.msg_id,
                message = $"Unknown message type: {message.type}"
            };
            string errorJson = JsonSerializer.Serialize(errorResponse);
            await writer.WriteLineAsync(errorJson);
        }

        private async Task ProcessErrorMessage(JudgeMessage message, StreamWriter writer, Exception ex)
        {
            var errorResponse = new ServerMessage
            {
                type = "error",
                status = false,
                msg_id = message?.msg_id ?? 0,
                message = $"Processing error: {ex.Message}"
            };
            string errorJson = JsonSerializer.Serialize(errorResponse);
            await writer.WriteLineAsync(errorJson);
        }

        // ------------------------- HELPERS -------------------------

        private int GetJudgeIndexFromId(string judgeId)
        {
            if (judgeIdToIndexMap.TryGetValue(judgeId, out int index))
                return index;

            int newIndex = nextJudgeIndex;
            judgeIdToIndexMap[judgeId] = newIndex;
            nextJudgeIndex = (nextJudgeIndex + 1) % 4;
            return newIndex;
        }

        private async Task SendAckResponse(StreamWriter writer, string ackType, long seq, bool ok, string reason)
        {
            var ackResponse = new AckMessage
            {
                ackType = ackType,
                seq = seq,
                ok = ok,
                reason = reason
            };

            string ackJson = JsonSerializer.Serialize(ackResponse);
            await writer.WriteLineAsync(ackJson);
        }

        public void UpdateMatchInfo(string matchId, string discipline, string ageCategory,
                                   string weightCategory, string blueName, string redName)
        {
            currentMatchInfo = new MatchInfoMessage
            {
                matchId = matchId,
                discipline = discipline,
                ageCategory = ageCategory,
                weightCategory = weightCategory,
                blueName = blueName,
                redName = redName
            };

            BroadcastMatchInfo();
        }

        private void BroadcastMatchInfo()
        {
            string matchInfoJson = JsonSerializer.Serialize(currentMatchInfo);

            List<(TcpClient c, StreamWriter w)> targets;
            lock (clientWriters)
                targets = clientWriters.Select(kv => (kv.Key, kv.Value)).ToList();

            foreach (var (client, writer) in targets)
            {
                if (!client.Connected) continue;
                try
                {
                    writer.WriteLine(matchInfoJson);
                }
                catch (Exception ex)
                {
                    adminForm?.AddToTerminal($"Ошибка рассылки match_info: {ex.Message}\n");
                }
            }
        }
    }

    // ------------------------- DTOs -------------------------

    public class JudgeMessage
    {
        public string type { get; set; }
        public int judge_id { get; set; }
        public double score { get; set; }
        public double penalty { get; set; }
        public string athlete { get; set; }
        public long msg_id { get; set; }
        public string judgeId { get; set; }
        public string judgeName { get; set; }
        public string app { get; set; }
        public string ver { get; set; }

        // Поля для сообщения score (Android-клиент)
        public string side { get; set; }   // "blue" | "red"
        public int value { get; set; }     // 1..5
        public string matchId { get; set; }
        public long ts { get; set; }       // epoch ms
        public long seq { get; set; }      // локальный счётчик клиента
    }

    public class ServerMessage
    {
        public string type { get; set; }
        public bool status { get; set; }
        public long msg_id { get; set; }
        public string message { get; set; }
        public string ackType { get; set; }
        public bool ok { get; set; }
    }

    public class MatchInfoMessage
    {
        public string type { get; set; } = "match_info";
        public string matchId { get; set; }
        public string discipline { get; set; }
        public string ageCategory { get; set; }
        public string weightCategory { get; set; }
        public string blueName { get; set; }
        public string redName { get; set; }
    }

    public class AckMessage
    {
        public string type { get; set; } = "ack";
        public string ackType { get; set; }
        public long seq { get; set; }
        public bool ok { get; set; }
        public string reason { get; set; }
    }
}
