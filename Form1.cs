using System;
using System.Data.SQLite;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sport
{
    public partial class Form1 : Form
    {
        // 4 судьи, 2 спортсмена (0 — синий, 1 — красный)
        private Label[] judgeTitles = new Label[4];
        private Panel[] judgePanels = new Panel[4];
        private Label[,] judgeScoreLabels = new Label[4, 2]; // [judgeIndex, athleteIndex]

        private Label[] penaltyLabels = new Label[2]; // сумма штрафов на экране (синий/красный)

        // Аккумулированные суммы на экране (обычные очки)
        private double[,] athletesScores = new double[2, 4]; // [athleteIndex, judgeIndex]
        // Аккумулированные суммы штрафов (синий/красный)
        private double[] athletesPenalties = new double[2];

        private JudgeServer judgeServer;
        private SQLiteConnection dbConnection;
        private AdminForm adminForm;

        public Form1()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeForms();
            InitializeJudgeServer();
        }

        public SQLiteConnection GetDbConnection() => dbConnection;
        public void UpdateScores() => RecomputeAllFromDatabaseAndRedraw();

        // -------------------- БД --------------------

        private void InitializeDatabase()
        {
            dbConnection = new SQLiteConnection("Data Source=sport.db;Version=3;");
            dbConnection.Open();

            // Схема: история событий. Каждая оценка/штраф — отдельная строка.
            // JudgeId: 1..4 — боковые; 5 — главный (штрафы)
            // AthleteId: 1 — синий; 2 — красный
            using (var cmd = new SQLiteCommand(@"
                PRAGMA foreign_keys = OFF;

                CREATE TABLE IF NOT EXISTS Scores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    JudgeId   INTEGER NOT NULL,
                    AthleteId INTEGER NOT NULL,
                    Score     REAL    NOT NULL DEFAULT 0,
                    Penalty   REAL    NOT NULL DEFAULT 0,
                    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                -- Индексы ускоряют агрегацию
                CREATE INDEX IF NOT EXISTS idx_scores_judge ON Scores(JudgeId);
                CREATE INDEX IF NOT EXISTS idx_scores_athlete ON Scores(AthleteId);
                CREATE INDEX IF NOT EXISTS idx_scores_ts ON Scores(Timestamp);
            ", dbConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        // Вставка события (дельты), НИЧЕГО не перетираем
        public void SaveScoreEventToDatabase(int judgeId, int athleteId, double scoreDelta, double penaltyDelta)
        {
            try
            {
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO Scores (JudgeId, AthleteId, Score, Penalty, Timestamp)
                    VALUES (@j, @a, @s, @p, CURRENT_TIMESTAMP);
                ", dbConnection))
                {
                    cmd.Parameters.AddWithValue("@j", judgeId);
                    cmd.Parameters.AddWithValue("@a", athleteId);
                    cmd.Parameters.AddWithValue("@s", scoreDelta);
                    cmd.Parameters.AddWithValue("@p", penaltyDelta);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error (insert event): {ex.Message}");
            }
        }

        // Полная агрегация из БД при старте/обновлении
        private void RecomputeAllFromDatabaseAndRedraw()
        {
            try
            {
                // Обнуляем локальные суммы
                for (int a = 0; a < 2; a++)
                    for (int j = 0; j < 4; j++)
                        athletesScores[a, j] = 0;

                athletesPenalties[0] = athletesPenalties[1] = 0;

                // 1) Сумма очков по судьям и спортсменам
                using (var cmd = new SQLiteCommand(@"
                    SELECT JudgeId, AthleteId, COALESCE(SUM(Score), 0) AS TotalScore
                    FROM Scores
                    WHERE JudgeId BETWEEN 1 AND 4
                    GROUP BY JudgeId, AthleteId
                    ORDER BY JudgeId, AthleteId;
                ", dbConnection))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int judgeId = Convert.ToInt32(r["JudgeId"]);     // 1..4
                        int athleteId = Convert.ToInt32(r["AthleteId"]); // 1..2
                        double total = r["TotalScore"] == DBNull.Value ? 0 : Convert.ToDouble(r["TotalScore"]);

                        int jIdx = judgeId - 1;  // 0..3
                        int aIdx = athleteId - 1; // 0..1

                        if (jIdx >= 0 && jIdx < 4 && aIdx >= 0 && aIdx < 2)
                            athletesScores[aIdx, jIdx] = total;
                    }
                }

                // 2) Сумма штрафов по спортсменам (главный судья = 5)
                using (var cmd = new SQLiteCommand(@"
                    SELECT AthleteId, COALESCE(SUM(Penalty), 0) AS TotalPenalty
                    FROM Scores
                    WHERE JudgeId = 5
                    GROUP BY AthleteId
                    ORDER BY AthleteId;
                ", dbConnection))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int athleteId = Convert.ToInt32(r["AthleteId"]); // 1..2
                        double totalPenalty = r["TotalPenalty"] == DBNull.Value ? 0 : Convert.ToDouble(r["TotalPenalty"]);
                        int aIdx = athleteId - 1;
                        if (aIdx >= 0 && aIdx < 2)
                            athletesPenalties[aIdx] = totalPenalty;
                    }
                }

                // Перерисовываем всё
                for (int j = 0; j < 4; j++)
                    RedrawJudgePanel(j);

                RedrawPenalty(0);
                RedrawPenalty(1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка агрегации из БД: {ex.Message}");
            }
        }

        // -------------------- UI / Формы --------------------

        private void InitializeForms()
        {
            this.Load += Form1_Load;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
        }

        private void InitializeJudgeServer()
        {
            judgeServer = new JudgeServer(this);

            adminForm = new AdminForm(dbConnection, UpdateScores, judgeServer);
            judgeServer.SetAdminForm(adminForm);

            adminForm.WindowState = FormWindowState.Maximized;
            adminForm.FormBorderStyle = FormBorderStyle.None;
            adminForm.Show();

            Task.Run(async () =>
            {
                try
                {
                    await judgeServer.StartServer();
                    adminForm.AddToTerminal("✅ Сервер успешно запущен\n");
                }
                catch (Exception ex)
                {
                    adminForm.AddToTerminal($"❌ Ошибка запуска сервера: {ex.Message}\n");
                }
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            BuildJudgePanels();
            BuildPenaltyPanels();
            RecomputeAllFromDatabaseAndRedraw();
        }

        private void BuildJudgePanels()
        {
            int blockW = 260;
            int blockH = 160;
            int titleH = 32;

            Point[] pos = new Point[]
            {
                new Point(0, titleH),
                new Point(this.ClientSize.Width - blockW, titleH),
                new Point(0, this.ClientSize.Height - blockH),
                new Point(this.ClientSize.Width - blockW, this.ClientSize.Height - blockH)
            };

            for (int j = 0; j < 4; j++)
            {
                // Заголовок
                judgeTitles[j] = new Label
                {
                    Size = new Size(blockW, titleH),
                    Location = new Point(pos[j].X, pos[j].Y - titleH),
                    BackColor = Color.DimGray,
                    ForeColor = Color.White,
                    Font = new Font("Arial", 11, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = $"Судья {j + 1}",
                    BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(judgeTitles[j]);

                // Панель судьи (фон меняется по лидирующему спортсмену)
                var panel = new Panel
                {
                    Size = new Size(blockW, blockH),
                    Location = pos[j],
                    BackColor = Color.Purple, // по умолчанию равенство
                    BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(panel);
                judgePanels[j] = panel;

                // Верхняя строка — синий
                var blueLabel = new Label
                {
                    Dock = DockStyle.Top,
                    Height = blockH / 2,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    Font = new Font("Arial", 24, FontStyle.Bold),
                    Text = "0"
                };
                panel.Controls.Add(blueLabel);
                judgeScoreLabels[j, 0] = blueLabel;

                // Нижняя строка — красный
                var redLabel = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = blockH / 2,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.White,
                    Font = new Font("Arial", 24, FontStyle.Bold),
                    Text = "0"
                };
                panel.Controls.Add(redLabel);
                judgeScoreLabels[j, 1] = redLabel;
            }
        }

        private void BuildPenaltyPanels()
        {
            int blockW = 180;
            int blockH = 100;
            int spacing = 20;

            int totalW = (blockW * 2) + spacing;
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;
            int startX = centerX - (totalW / 2);

            Label title = new Label
            {
                Size = new Size(totalW, 40),
                Location = new Point(startX, centerY - 160),
                BackColor = Color.DarkGreen,
                ForeColor = Color.White,
                Font = new Font("Arial", 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Штрафные очки (Главный судья)",
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(title);

            penaltyLabels = new Label[2];

            for (int i = 0; i < 2; i++)
            {
                penaltyLabels[i] = new Label
                {
                    Size = new Size(blockW, blockH),
                    Location = new Point(startX + i * (blockW + spacing), centerY - 100),
                    BackColor = i == 0 ? Color.DarkBlue : Color.DarkRed,
                    ForeColor = Color.White,
                    Font = new Font("Arial", 28, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "0",
                    BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(penaltyLabels[i]);

                Label under = new Label
                {
                    Size = new Size(blockW, 30),
                    Location = new Point(startX + i * (blockW + spacing), centerY),
                    BackColor = i == 0 ? Color.DarkBlue : Color.DarkRed,
                    ForeColor = Color.White,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = i == 0 ? "Синий угол" : "Красный угол",
                    BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(under);
            }
        }

        // -------------------- Обновления от сервера --------------------

        // Вызывается сервером — добавляем дельту очков.
        public void UpdateJudgeScore(int judgeIndex, int athleteIndex, double scoreDelta)
        {
            if (judgeIndex < 0 || judgeIndex >= 4 || athleteIndex < 0 || athleteIndex >= 2)
                return;

            // 1) Обновляем локальную сумму
            athletesScores[athleteIndex, judgeIndex] += scoreDelta;

            // 2) Рисуем
            RedrawJudgePanel(judgeIndex);

            // 3) Пишем СОБЫТИЕ (дельту) в БД
            int judgeId = judgeIndex + 1;      // 1..4
            int athleteId = athleteIndex + 1;  // 1..2
            SaveScoreEventToDatabase(judgeId, athleteId, scoreDelta, 0);
        }

        // Вызывается сервером — добавляем дельту штрафа.
        public void UpdatePenaltyScore(int athleteIndex, double penaltyDelta)
        {
            if (athleteIndex < 0 || athleteIndex >= 2) return;

            // 1) Обновляем локальную сумму штрафов
            athletesPenalties[athleteIndex] += penaltyDelta;

            // 2) Рисуем
            RedrawPenalty(athleteIndex);

            // 3) Пишем событие: судья=5, penalty=дельта
            int judgeId = 5;
            int athleteId = athleteIndex + 1;
            SaveScoreEventToDatabase(judgeId, athleteId, 0, penaltyDelta);

            // Лёгкий визуальный эффект
            BlinkPenalty(athleteIndex);
        }

        // -------------------- Отрисовка --------------------

        private void RedrawJudgePanel(int judgeIndex)
        {
            double blue = athletesScores[0, judgeIndex];
            double red = athletesScores[1, judgeIndex];

            judgeScoreLabels[judgeIndex, 0].Text = blue.ToString("0");
            judgeScoreLabels[judgeIndex, 1].Text = red.ToString("0");

            // Сделаем крупнее шрифт у лидера
            judgeScoreLabels[judgeIndex, 0].Font =
                blue > red ? new Font("Arial", 48, FontStyle.Bold) : new Font("Arial", 24, FontStyle.Bold);
            judgeScoreLabels[judgeIndex, 1].Font =
                red > blue ? new Font("Arial", 48, FontStyle.Bold) : new Font("Arial", 24, FontStyle.Bold);

            // Цвет фона панели судьи
            judgePanels[judgeIndex].BackColor =
                blue > red ? Color.DarkBlue :
                red > blue ? Color.DarkRed :
                Color.Purple;
        }

        private void RedrawPenalty(int athleteIndex)
        {
            penaltyLabels[athleteIndex].Text = athletesPenalties[athleteIndex].ToString("0");
        }

        private void BlinkPenalty(int athleteIndex)
        {
            Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    this.Invoke(new Action(() =>
                    {
                        penaltyLabels[athleteIndex].BackColor = Color.OrangeRed;
                    }));
                    await Task.Delay(180);
                    this.Invoke(new Action(() =>
                    {
                        penaltyLabels[athleteIndex].BackColor = athleteIndex == 0 ? Color.DarkBlue : Color.DarkRed;
                    }));
                    await Task.Delay(180);
                }
            });
        }

        // -------------------- Вспомогательное --------------------

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                adminForm?.Close();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            judgeServer?.StopServer();
            dbConnection?.Close();
            base.OnFormClosing(e);
        }
    }
}
