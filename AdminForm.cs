using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace sport
{
    public class AdminForm : Form
    {
        private SQLiteConnection dbConnection;
        private Action updateScoresAction;
        private DataGridView scoresGridView;
        private Button saveButton;
        private TabControl tabControl;
        private TabPage athletesTab;
        private TabPage judgesTab;
        private TabPage scoresTab;
        private TabPage reportsTab;
        private TabPage matchTab;
        private DataGridView reportsGridView;
        private DateTimePicker dateFromPicker;
        private DateTimePicker dateToPicker;
        private Button filterButton;
        private ComboBox athleteFilterComboBox;
        private JudgeServer judgeServer;

        private TextBox matchType;
        private TextBox matchIdTextBox;
        private TextBox disciplineTextBox;
        private TextBox ageCategoryTextBox;
        private TextBox weightCategoryTextBox;
        private TextBox blueNameTextBox;
        private TextBox redNameTextBox;
        private Button updateMatchButton;

        private TabPage penaltiesTab;
        private DataGridView penaltiesGridView;
        private Button addPenaltyButton;

        private Button serverStatusButton;
        private Button connectTestButton;
        private TextBox terminalTextBox;
        private Panel terminalPanel;
        private Label terminalLabel;
        private StringBuilder terminalContent = new StringBuilder();

        public AdminForm(SQLiteConnection connection, Action updateAction, JudgeServer server)
        {
            dbConnection = connection;
            updateScoresAction = updateAction;
            judgeServer = server;
            InitializeComponents();
            LoadData();
        }

        private void InitializeComponents()
        {
            this.Text = "Административная панель - Главный судья";
            this.Width = 1000;
            this.Height = 800;

            tabControl = new TabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabControl);

            matchTab = new TabPage("Управление боем");
            tabControl.Controls.Add(matchTab);
            InitializeMatchTab();

            athletesTab = new TabPage("Спортсмены");
            tabControl.Controls.Add(athletesTab);
            InitializeAthletesTab();

            judgesTab = new TabPage("Судьи");
            tabControl.Controls.Add(judgesTab);
            InitializeJudgesTab();

            scoresTab = new TabPage("Оценки");
            tabControl.Controls.Add(scoresTab);
            InitializeScoresTab();

            penaltiesTab = new TabPage("Штрафные очки");
            tabControl.Controls.Add(penaltiesTab);
            InitializePenaltiesTab();

            reportsTab = new TabPage("Отчёты");
            tabControl.Controls.Add(reportsTab);
            InitializeReportsTab();

            TabPage serverTab = new TabPage("Мониторинг сервера");
            tabControl.Controls.Add(serverTab);
            InitializeServerTab(serverTab);

            InitializeTerminal();

            saveButton = new Button
            {
                Text = "Сохранить все изменения",
                Dock = DockStyle.Bottom,
                Height = 50
            };
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);
        }

        private void InitializeMatchTab()
        {
            Panel matchPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            matchPanel.Controls.Add(new Label { Text = "ID боя:", Location = new Point(10, 10), AutoSize = true });
            matchIdTextBox = new TextBox { Location = new Point(120, 10), Width = 200, Text = "M-2025-08-26-001" };
            matchPanel.Controls.Add(matchIdTextBox);

            matchPanel.Controls.Add(new Label { Text = "Дисциплина:", Location = new Point(10, 40), AutoSize = true });
            disciplineTextBox = new TextBox { Location = new Point(120, 40), Width = 200, Text = "Туль" };
            matchPanel.Controls.Add(disciplineTextBox);

            matchPanel.Controls.Add(new Label { Text = "Возрастная категория:", Location = new Point(10, 70), AutoSize = true });
            ageCategoryTextBox = new TextBox { Location = new Point(150, 70), Width = 170, Text = "Юноши 12-13" };
            matchPanel.Controls.Add(ageCategoryTextBox);

            matchPanel.Controls.Add(new Label { Text = "Весовая категория:", Location = new Point(10, 100), AutoSize = true });
            weightCategoryTextBox = new TextBox { Location = new Point(150, 100), Width = 170, Text = "до 45 кг" };
            matchPanel.Controls.Add(weightCategoryTextBox);

            matchPanel.Controls.Add(new Label { Text = "Синий угол:", Location = new Point(10, 130), AutoSize = true });
            blueNameTextBox = new TextBox { Location = new Point(120, 130), Width = 200, Text = "Петров П.П." };
            matchPanel.Controls.Add(blueNameTextBox);

            matchPanel.Controls.Add(new Label { Text = "Красный угол:", Location = new Point(10, 160), AutoSize = true });
            redNameTextBox = new TextBox { Location = new Point(120, 160), Width = 200, Text = "Сидоров С.С." };
            matchPanel.Controls.Add(redNameTextBox);

            updateMatchButton = new Button
            {
                Text = "Обновить информацию о бое",
                Location = new Point(10, 190),
                Width = 200
            };
            updateMatchButton.Click += UpdateMatchButton_Click;
            matchPanel.Controls.Add(updateMatchButton);

            matchTab.Controls.Add(matchPanel);
        }

        private void UpdateMatchButton_Click(object sender, EventArgs e)
        {
            judgeServer.UpdateMatchInfo(
                matchIdTextBox.Text,
                disciplineTextBox.Text,
                ageCategoryTextBox.Text,
                weightCategoryTextBox.Text,
                blueNameTextBox.Text,
                redNameTextBox.Text
            );

            MessageBox.Show("Информация о бое обновлена и отправлена всем судьям");
        }

        private void InitializeAthletesTab()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id",
                HeaderText = "ID",
                DataPropertyName = "Id",
                ReadOnly = true
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "ФИО спортсмена",
                DataPropertyName = "Name",
                Width = 300
            });

            athletesTab.Controls.Add(grid);
        }

        private void InitializeJudgesTab()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id",
                HeaderText = "ID",
                DataPropertyName = "Id",
                ReadOnly = true
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "ФИО судьи",
                DataPropertyName = "Name",
                Width = 300
            });

            judgesTab.Controls.Add(grid);
        }

        private void InitializeScoresTab()
        {
            scoresGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true
            };

            scoresGridView.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "JudgeId",
                HeaderText = "Судья",
                DataPropertyName = "JudgeId",
                Width = 150
            });

            scoresGridView.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "AthleteId",
                HeaderText = "Спортсмен",
                DataPropertyName = "AthleteId",
                Width = 150
            });

            scoresGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Score",
                HeaderText = "Оценка",
                DataPropertyName = "Score"
            });

            scoresGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Penalty",
                HeaderText = "Штраф",
                DataPropertyName = "Penalty"
            });

            scoresTab.Controls.Add(scoresGridView);
        }

        private void InitializeReportsTab()
        {
            Panel filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = SystemColors.Control
            };
            reportsTab.Controls.Add(filterPanel);

            filterPanel.Controls.Add(new Label { Text = "Дата от:", Dock = DockStyle.Left, AutoSize = true });
            dateFromPicker = new DateTimePicker
            {
                Dock = DockStyle.Left,
                Width = 120,
                Format = DateTimePickerFormat.Short
            };
            filterPanel.Controls.Add(dateFromPicker);

            filterPanel.Controls.Add(new Label { Text = "Дата до:", Dock = DockStyle.Left, AutoSize = true });
            dateToPicker = new DateTimePicker
            {
                Dock = DockStyle.Left,
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            filterPanel.Controls.Add(dateToPicker);

            filterPanel.Controls.Add(new Label { Text = "Спортсмен:", Dock = DockStyle.Left, AutoSize = true });
            athleteFilterComboBox = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            filterPanel.Controls.Add(athleteFilterComboBox);

            filterButton = new Button
            {
                Text = "Применить фильтр",
                Dock = DockStyle.Left,
                Width = 120
            };
            filterButton.Click += FilterButton_Click;
            filterPanel.Controls.Add(filterButton);

            reportsGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToOrderColumns = true,
                AllowUserToResizeColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            reportsTab.Controls.Add(reportsGridView);
        }

        private void InitializeServerTab(TabPage serverTab)
        {
            Panel serverPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            // Заголовок
            serverPanel.Controls.Add(new Label
            {
                Text = "Мониторинг TCP/UDP сервера",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            });

            // Кнопка проверки статуса сервера
            serverStatusButton = new Button
            {
                Text = "Статус сервера",
                Location = new Point(10, 50),
                Size = new Size(120, 30)
            };
            serverStatusButton.Click += ServerStatusButton_Click;
            serverPanel.Controls.Add(serverStatusButton);

            // Кнопка тестирования подключения
            connectTestButton = new Button
            {
                Text = "Тест подключения",
                Location = new Point(140, 50),
                Size = new Size(120, 30)
            };
            connectTestButton.Click += ConnectTestButton_Click;
            serverPanel.Controls.Add(connectTestButton);

            // Кнопка очистки терминала
            Button clearTerminalButton = new Button
            {
                Text = "Очистить терминал",
                Location = new Point(270, 50),
                Size = new Size(120, 30)
            };
            clearTerminalButton.Click += ClearTerminalButton_Click;
            serverPanel.Controls.Add(clearTerminalButton);

            // Информация о портах
            Label portsInfo = new Label
            {
                Text = $"TCP порт: 45455\nUDP порт: 45454\nИмя сервера: TKD_Score_Server",
                Location = new Point(10, 90),
                AutoSize = true,
                Font = new Font("Arial", 10)
            };
            serverPanel.Controls.Add(portsInfo);

            // Статистика подключений
            Label statsLabel = new Label
            {
                Text = "Статистика подключений:",
                Location = new Point(10, 140),
                AutoSize = true,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            serverPanel.Controls.Add(statsLabel);

            serverTab.Controls.Add(serverPanel);
        }

        private void InitializeTerminal()
        {
            terminalPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 200,
                BackColor = Color.Black
            };

            terminalLabel = new Label
            {
                Text = "Терминал входящих сообщений:",
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.White,
                BackColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };
            terminalPanel.Controls.Add(terminalLabel);

            terminalTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 10),
                ReadOnly = true
            };
            terminalPanel.Controls.Add(terminalTextBox);

            this.Controls.Add(terminalPanel);
        }

        private void ServerStatusButton_Click(object sender, EventArgs e)
        {
            try
            {
                string status = judgeServer != null ? "Сервер запущен" : "Сервер не инициализирован";
                string message = $"Статус сервера: {status}\n" +
                                $"TCP порт: 45455\n" +
                                $"UDP порт: 45454\n" +
                                $"Время запуска: {DateTime.Now}";

                MessageBox.Show(message, "Статус сервера");

                // Добавляем в терминал
                AddToTerminal($"=== Проверка статуса сервера ===\n{message}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки статуса: {ex.Message}");
                AddToTerminal($"ОШИБКА: {ex.Message}\n");
            }
        }

        private void ConnectTestButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Тестируем подключение к localhost
                AddToTerminal("=== Тестирование подключения ===\n");
                AddToTerminal("Попытка подключения к localhost:45455...\n");

                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var result = client.BeginConnect("localhost", 45455, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

                    if (success)
                    {
                        client.EndConnect(result);
                        AddToTerminal("✓ Подключение успешно\n");
                    }
                    else
                    {
                        AddToTerminal("✗ Не удалось подключиться (таймаут)\n");
                    }
                }
            }
            catch (Exception ex)
            {
                AddToTerminal($"✗ Ошибка подключения: {ex.Message}\n");
            }
        }

        private void ClearTerminalButton_Click(object sender, EventArgs e)
        {
            terminalContent.Clear();
            terminalTextBox.Text = string.Empty;
            AddToTerminal("=== Терминал очищен ===\n");
        }

        // Метод для добавления сообщений в терминал
        public void AddToTerminal(string message)
        {
            if (terminalTextBox.InvokeRequired)
            {
                terminalTextBox.Invoke(new Action<string>(AddToTerminal), message);
                return;
            }

            terminalContent.Append($"[{DateTime.Now:HH:mm:ss}] {message}");

            // Ограничиваем размер терминала
            if (terminalContent.Length > 10000)
            {
                terminalContent.Remove(0, 2000);
            }

            terminalTextBox.Text = terminalContent.ToString();
            terminalTextBox.SelectionStart = terminalTextBox.Text.Length;
            terminalTextBox.ScrollToCaret();
        }

        // ... остальные методы

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Сохраняем настройки или очищаем ресурсы
            base.OnFormClosing(e);
        }
    
        private void InitializePenaltiesTab()
        {
            Panel penaltiesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            // Заголовок
            penaltiesPanel.Controls.Add(new Label
            {
                Text = "Управление штрафными очками (Главный судья)",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            });

            // Таблица штрафных очков
            penaltiesGridView = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(600, 200),
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoGenerateColumns = false
            };

            penaltiesGridView.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "AthleteId",
                HeaderText = "Спортсмен",
                DataPropertyName = "AthleteId",
                Width = 200
            });

            penaltiesGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Penalty",
                HeaderText = "Штрафные очки",
                DataPropertyName = "Penalty",
                Width = 100
            });

            penaltiesPanel.Controls.Add(penaltiesGridView);

            // Кнопка применения штрафа
            addPenaltyButton = new Button
            {
                Text = "Применить штраф",
                Location = new Point(10, 260),
                Size = new Size(120, 30)
            };
            addPenaltyButton.Click += AddPenaltyButton_Click;
            penaltiesPanel.Controls.Add(addPenaltyButton);

            penaltiesTab.Controls.Add(penaltiesPanel);
        }

        private void FilterButton_Click(object sender, EventArgs e)
        {
            LoadReportsData();
        }

        private void LoadReportsData()
        {
            string query = @"
                SELECT
                    a.Name AS AthleteName,
                    j.Name AS JudgeName,
                    s.Score,
                    s.Penalty,
                    s.Timestamp
                FROM Scores s
                JOIN Athletes a ON s.AthleteId = a.Id
                JOIN Judges j ON s.JudgeId = j.Id
                WHERE s.Timestamp BETWEEN @dateFrom AND @dateTo";

            if (athleteFilterComboBox.SelectedItem != null &&
                athleteFilterComboBox.SelectedValue != null &&
                athleteFilterComboBox.SelectedValue.ToString() != "0")
            {
                query += " AND s.AthleteId = @athleteId";
            }

            query += " ORDER BY s.Timestamp DESC, a.Name";

            using (var cmd = new SQLiteCommand(query, dbConnection))
            {
                cmd.Parameters.AddWithValue("@dateFrom", dateFromPicker.Value.Date);
                cmd.Parameters.AddWithValue("@dateTo", dateToPicker.Value.Date.AddDays(1));

                if (athleteFilterComboBox.SelectedItem != null &&
                    athleteFilterComboBox.SelectedValue != null &&
                    athleteFilterComboBox.SelectedValue.ToString() != "0")
                {
                    int athleteId;
                    if (int.TryParse(athleteFilterComboBox.SelectedValue.ToString(), out athleteId))
                    {
                        cmd.Parameters.AddWithValue("@athleteId", athleteId);
                    }
                }

                var table = new DataTable();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    adapter.Fill(table);
                }

                reportsGridView.DataSource = table;

                if (reportsGridView.Columns.Contains("AthleteName"))
                    reportsGridView.Columns["AthleteName"].HeaderText = "Спортсмен";
                if (reportsGridView.Columns.Contains("JudgeName"))
                    reportsGridView.Columns["JudgeName"].HeaderText = "Судья";
                if (reportsGridView.Columns.Contains("Score"))
                    reportsGridView.Columns["Score"].HeaderText = "Оценка";
                if (reportsGridView.Columns.Contains("Penalty"))
                    reportsGridView.Columns["Penalty"].HeaderText = "Штраф";
                if (reportsGridView.Columns.Contains("Timestamp"))
                {
                    reportsGridView.Columns["Timestamp"].HeaderText = "Дата и время";
                    reportsGridView.Columns["Timestamp"].DefaultCellStyle.Format = "g";
                }
            }
        }

        private void LoadData()
        {
            var athletesGrid = (DataGridView)athletesTab.Controls[0];
            athletesGrid.DataSource = LoadTableData("Athletes");

            var judgesGrid = (DataGridView)judgesTab.Controls[0];
            judgesGrid.DataSource = LoadTableData("Judges");

            LoadScoresData();
            FillComboBoxColumns();
            LoadAthletesFilter();
            dateFromPicker.Value = DateTime.Now.AddMonths(-1);
            LoadReportsData();
            LoadPenaltiesData();
        }

        private void LoadAthletesFilter()
        {
            var athletes = LoadTableData("Athletes");
            athletes.DefaultView.Sort = "Name ASC";

            var filterTable = new DataTable();
            filterTable.Columns.Add("Id", typeof(int));
            filterTable.Columns.Add("Name", typeof(string));

            filterTable.Rows.Add(0, "Все спортсмены");

            foreach (DataRow row in athletes.Rows)
            {
                filterTable.Rows.Add(row["Id"], row["Name"]);
            }

            athleteFilterComboBox.DataSource = filterTable;
            athleteFilterComboBox.DisplayMember = "Name";
            athleteFilterComboBox.ValueMember = "Id";
            athleteFilterComboBox.SelectedIndex = 0;
        }

        private DataTable LoadTableData(string tableName)
        {
            return LoadDataFromQuery($"SELECT * FROM {tableName}");
        }

        private DataTable LoadDataFromQuery(string query)
        {
            var table = new DataTable();
            using (var adapter = new SQLiteDataAdapter(query, dbConnection))
            {
                adapter.Fill(table);
            }
            return table;
        }

        private void LoadScoresData()
        {
            string query = @"
                SELECT s.Id, j.Id as JudgeId, a.Id as AthleteId, s.Score, s.Penalty
                FROM Scores s
                JOIN Judges j ON s.JudgeId = j.Id
                JOIN Athletes a ON s.AthleteId = a.Id";

            scoresGridView.DataSource = LoadDataFromQuery(query);
        }

        private void LoadPenaltiesData()
        {
            string query = @"
            SELECT s.Id, a.Id as AthleteId, s.Penalty
            FROM Scores s
            JOIN Athletes a ON s.AthleteId = a.Id
            WHERE s.JudgeId = 5"; // Все записи главного судьи

            var table = LoadDataFromQuery(query);

            // Добавляем столбец JudgeId если его нет
            if (!table.Columns.Contains("JudgeId"))
            {
                table.Columns.Add("JudgeId", typeof(int));
            }

            // Устанавливаем JudgeId = 5 для всех записей
            foreach (DataRow row in table.Rows)
            {
                row["JudgeId"] = 5;
            }

            penaltiesGridView.DataSource = table;

            // Заполняем выпадающий список спортсменов
            var athletes = LoadTableData("Athletes");
            var athleteColumn = (DataGridViewComboBoxColumn)penaltiesGridView.Columns["AthleteId"];
            athleteColumn.DataSource = athletes;
            athleteColumn.DisplayMember = "Name";
            athleteColumn.ValueMember = "Id";
        }

        private void FillComboBoxColumns()
        {
            var judges = LoadTableData("Judges");
            var athletes = LoadTableData("Athletes");

            var judgeColumn = (DataGridViewComboBoxColumn)scoresGridView.Columns["JudgeId"];
            judgeColumn.DataSource = judges;
            judgeColumn.DisplayMember = "Name";
            judgeColumn.ValueMember = "Id";

            var athleteColumn = (DataGridViewComboBoxColumn)scoresGridView.Columns["AthleteId"];
            athleteColumn.DataSource = athletes;
            athleteColumn.DisplayMember = "Name";
            athleteColumn.ValueMember = "Id";
        }

        private void SaveTableData(string tableName, DataGridView grid)
        {
            var table = (DataTable)grid.DataSource;
            using (var adapter = new SQLiteDataAdapter($"SELECT * FROM {tableName}", dbConnection))
            {
                var builder = new SQLiteCommandBuilder(adapter);
                adapter.Update(table);
            }
        }

        private void SaveScoresData()
        {
            var table = (DataTable)scoresGridView.DataSource;
            using (var adapter = new SQLiteDataAdapter("SELECT * FROM Scores", dbConnection))
            {
                var builder = new SQLiteCommandBuilder(adapter);
                adapter.Update(table);
            }
        }

        private void SavePenaltiesData()
        {
            try
            {
                var table = (DataTable)penaltiesGridView.DataSource;

                // Убеждаемся, что все записи имеют JudgeId = 5
                foreach (DataRow row in table.Rows)
                {
                    if (row.RowState != DataRowState.Deleted && row.RowState != DataRowState.Detached)
                    {
                        if (row["JudgeId"] == DBNull.Value || Convert.ToInt32(row["JudgeId"]) != 5)
                        {
                            row["JudgeId"] = 5;
                        }

                        // Устанавливаем Score = 0 для штрафных записей
                        if (!table.Columns.Contains("Score") || row["Score"] == DBNull.Value)
                        {
                            if (!table.Columns.Contains("Score"))
                            {
                                table.Columns.Add("Score", typeof(double));
                            }
                            row["Score"] = 0.0;
                        }
                    }
                }

                using (var adapter = new SQLiteDataAdapter("SELECT * FROM Scores WHERE JudgeId = 5", dbConnection))
                {
                    var builder = new SQLiteCommandBuilder(adapter);
                    adapter.Update(table);
                }

                // Принудительно обновляем отображение в Form1
                updateScoresAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении штрафов: {ex.Message}");
            }
        }

        private void AddPenaltyButton_Click(object sender, EventArgs e)
        {
            try
            {
                var table = (DataTable)penaltiesGridView.DataSource;
                DataRow newRow = table.NewRow();
                newRow["JudgeId"] = 5; // Главный судья
                newRow["Penalty"] = 0.0;
                newRow["Score"] = 0.0; // Обычная оценка = 0
                table.Rows.Add(newRow);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении штрафа: {ex.Message}");
            }
        }

        // Обновляем общий метод сохранения
        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveTableData("Athletes", (DataGridView)athletesTab.Controls[0]);
                SaveTableData("Judges", (DataGridView)judgesTab.Controls[0]);
                SaveScoresData();
                SavePenaltiesData();

                MessageBox.Show("Данные успешно сохранены");

                // Принудительно обновляем отображение
                updateScoresAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }
    }
}