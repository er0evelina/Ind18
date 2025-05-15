using System;
using System.Data;
using System.Windows.Forms;
using Npgsql;
using Excel = Microsoft.Office.Interop.Excel;
using System.Collections.Generic;
using System.Windows.Forms.DataVisualization.Charting;

namespace library
{
    public partial class Form1 : Form
    {
        private NpgsqlConnection con;
        private string connString = "Host=127.0.0.1;Username=postgres;Password=postpass;Database=lena";

        public Form1()
        {
            InitializeComponent();
            con = new NpgsqlConnection(connString);
            InitializeDataTabs();
            LoadUniversities();
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            try
            {
                using (var con = GetConnection())
                {
                    // Очищаем предыдущие данные в графике
                    chart1.Series.Clear();

                    // 1. Статистика по статусам книг
                    string sql = @"SELECT 
                    CASE 
                        WHEN return_date IS NULL AND due_date < CURRENT_DATE THEN 'Просроченные'
                        WHEN return_date IS NULL THEN 'На руках'
                        ELSE 'Возвращенные'
                    END as status,
                    COUNT(*) as count
                   FROM book_loans
                   GROUP BY status";

                    NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                    NpgsqlDataReader dr = cmd.ExecuteReader();

                    // Добавляем серию для статусов книг
                    var series1 = new Series("BookStatus");
                    series1.ChartType = SeriesChartType.Pie;
                    chart1.Series.Add(series1);

                    while (dr.Read())
                    {
                        series1.Points.AddXY(dr["status"].ToString(), dr["count"]);
                    }
                    dr.Close();

                    // 2. Статистика по популярным книгам
                    //sql = @"SELECT b.title, COUNT(bl.loan_id) as loan_count
                    //       FROM book_loans bl
                    //       JOIN books b ON bl.book_id = b.book_id
                    //       GROUP BY b.title
                    //       ORDER BY loan_count DESC
                    //       LIMIT 5";

                    //cmd = new NpgsqlCommand(sql, con);
                    //dr = cmd.ExecuteReader();

                    //// Добавляем серию для популярных книг
                    //var series2 = new Series("PopularBooks");
                    //series2.ChartType = SeriesChartType.Bar;
                    //chart1.Series.Add(series2);

                    //while (dr.Read())
                    //{
                    //    series2.Points.AddXY(dr["title"].ToString(), dr["loan_count"]);
                    //}
                    //dr.Close();

                    //// Настройка внешнего вида графика
                    //chart1.Titles.Clear();
                    //chart1.Titles.Add("Статистика библиотеки");
                    //chart1.ChartAreas[0].AxisX.Title = "Категории";
                    //chart1.ChartAreas[0].AxisY.Title = "Количество";
                    //chart1.Legends[0].Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Bottom;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки статистики: " + ex.Message);
            }
        }

        private void InitializeDataTabs()
        {
            // Основные таблицы для редактирования
            TabPage universitiesTab = new TabPage("Университеты");
            TabPage studentsTab = new TabPage("Студенты");
            TabPage booksTab = new TabPage("Книги");
            TabPage loansTab = new TabPage("Выдачи");

            // Инициализируем все вкладки с редактированием
            InitializeEditableTab(universitiesTab, "universities", new[] { "university_id", "name", "address", "contact_phone" });
            InitializeEditableTab(studentsTab, "students", new[] { "student_id", "university_id", "full_name", "student_card_number", "phone", "email" });
            InitializeEditableTab(booksTab, "books", new[] { "book_id", "title", "author", "publisher", "publication_year", "price", "is_available" });
            InitializeEditableTab(loansTab, "book_loans", new[] { "loan_id", "book_id", "student_id", "loan_date", "due_date", "return_date", "is_lost" });

            // Добавляем вкладки в TabControl
            tabControl1.TabPages.Add(universitiesTab);
            tabControl1.TabPages.Add(studentsTab);
            tabControl1.TabPages.Add(booksTab);
            tabControl1.TabPages.Add(loansTab);
        }

        private void InitializeEditableTab(TabPage tab, string tableName, string[] columns)
        {
            DataGridView dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Name = tableName + "_grid"
            };

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            Button addButton = new Button { Text = "Добавить", Dock = DockStyle.Left, Width = 100 };
            Button editButton = new Button { Text = "Изменить", Dock = DockStyle.Left, Width = 100 };
            Button deleteButton = new Button { Text = "Удалить", Dock = DockStyle.Left, Width = 100 };
            Button refreshButton = new Button { Text = "Обновить", Dock = DockStyle.Right, Width = 100 };

            addButton.Click += (sender, e) => ShowAddEditForm(tableName, null, dataGridView);
            editButton.Click += (sender, e) =>
            {
                if (dataGridView.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Выберите строку для редактирования");
                    return;
                }
                var selectedId = dataGridView.SelectedRows[0].Cells[0].Value;
                if (selectedId != null)
                {
                    ShowAddEditForm(tableName, selectedId, dataGridView);
                }
            };
            deleteButton.Click += (sender, e) =>
            {
                if (dataGridView.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Выберите строку для удаления");
                    return;
                }
                var selectedId = dataGridView.SelectedRows[0].Cells[0].Value;
                if (selectedId != null)
                {
                    if (MessageBox.Show("Вы уверены, что хотите удалить эту запись?", "Подтверждение",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        DeleteRecord(tableName, selectedId, dataGridView);
                    }
                }
            };
            refreshButton.Click += (sender, e) => LoadData(tableName, dataGridView);

            buttonPanel.Controls.Add(deleteButton);
            buttonPanel.Controls.Add(editButton);
            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(refreshButton);

            tab.Controls.Add(dataGridView);
            tab.Controls.Add(buttonPanel);

            LoadData(tableName, dataGridView);
        }

        private void LoadData(string tableName, DataGridView dataGridView)
        {
            try
            {
                using (var con = GetConnection())
                {
                    string sql = $"SELECT * FROM {tableName}";

                    // Для таблиц с внешними ключами добавляем JOIN для удобства просмотра
                    if (tableName == "students")
                    {
                        sql = @"SELECT s.student_id, u.name as university_name, s.full_name, 
                               s.student_card_number, s.phone, s.email
                               FROM students s
                               JOIN universities u ON s.university_id = u.university_id";
                    }
                    else if (tableName == "book_loans")
                    {
                        sql = @"SELECT bl.loan_id, b.title as book_title, s.full_name as student_name, 
                               bl.loan_date, bl.due_date, bl.return_date,
                               CASE WHEN bl.is_lost THEN 'Да' ELSE 'Нет' END as is_lost
                               FROM book_loans bl
                               JOIN books b ON bl.book_id = b.book_id
                               JOIN students s ON bl.student_id = s.student_id";
                    }

                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(sql, con);
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    dataGridView.DataSource = dt;

                    // Скрываем технические колонки
                    foreach (DataGridViewColumn column in dataGridView.Columns)
                    {
                        if (column.Name.EndsWith("_id"))
                        {
                            column.Visible = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных из таблицы {tableName}: {ex.Message}");
            }
        }

        private void ShowAddEditForm(string tableName, object id, DataGridView dataGridView)
        {
            Form editForm = new Form
            {
                Text = id == null ? "Добавить запись" : "Изменить запись",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                Width = 400,
                Height = 300
            };

            try
            {
                using (var con = GetConnection())
                {
                    string sql = id == null ?
                        $"SELECT * FROM {tableName} WHERE 1=0" :
                        $"SELECT * FROM {tableName} WHERE {GetIdColumn(tableName)} = @id";

                    NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                    if (id != null) cmd.Parameters.AddWithValue("@id", id);

                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    TableLayoutPanel tableLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 2,
                        RowCount = dt.Columns.Count + 1,
                        AutoScroll = true
                    };

                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        if (dt.Columns[i].ColumnName == GetIdColumn(tableName)) continue;

                        tableLayout.Controls.Add(new Label
                        {
                            Text = GetDisplayName(dt.Columns[i].ColumnName),
                            Dock = DockStyle.Fill,
                            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                        }, 0, i);

                        Control inputControl = CreateInputControl(dt.Columns[i].DataType, dt.Columns[i].ColumnName, con);

                        if (id != null && dt.Rows.Count > 0 && dt.Rows[0][i] != DBNull.Value)
                        {
                            SetControlValue(inputControl, dt.Rows[0][i]);
                        }

                        tableLayout.Controls.Add(inputControl, 1, i);
                    }

                    Button saveButton = new Button { Text = "Сохранить", Dock = DockStyle.Fill };
                    saveButton.Click += (sender, e) =>
                    {
                        SaveRecord(tableName, id, tableLayout, editForm, dataGridView);
                    };

                    tableLayout.Controls.Add(saveButton, 0, dt.Columns.Count);
                    tableLayout.SetColumnSpan(saveButton, 2);

                    editForm.Controls.Add(tableLayout);
                    editForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии формы редактирования: {ex.Message}");
            }
        }

        private string GetDisplayName(string columnName)
        {
            switch (columnName)
            {
                case "name": return "Название";
                case "address": return "Адрес";
                case "contact_phone": return "Контактный телефон";
                case "university_id": return "Университет";
                case "full_name": return "ФИО";
                case "student_card_number": return "Номер студ. билета";
                case "phone": return "Телефон";
                case "email": return "Email";
                case "title": return "Название книги";
                case "author": return "Автор";
                case "publisher": return "Издательство";
                case "publication_year": return "Год издания";
                case "price": return "Цена";
                case "is_available": return "Доступна";
                case "book_id": return "Книга";
                case "student_id": return "Студент";
                case "loan_date": return "Дата выдачи";
                case "due_date": return "Срок возврата";
                case "return_date": return "Дата возврата";
                case "is_lost": return "Утеряна";
                default: return columnName;
            }
        }

        private Control CreateInputControl(Type dataType, string columnName, NpgsqlConnection con)
        {
            if (columnName.EndsWith("_id"))
            {
                string relatedTable = columnName.StartsWith("university_") ? "universities" :
                                   columnName.StartsWith("student_") ? "students" :
                                   columnName.StartsWith("book_") ? "books" :
                                   columnName.Split('_')[0] + "s"; // общий случай
                ComboBox comboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

                string sql = $"SELECT {GetIdColumn(relatedTable)}, {GetNameColumn(relatedTable)} FROM {relatedTable}";
                NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                NpgsqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    comboBox.Items.Add(new ComboboxItem(dr[1].ToString(), dr[0]));
                }
                dr.Close();

                return comboBox;
            }
            else if (dataType == typeof(DateTime))
            {
                return new DateTimePicker { Dock = DockStyle.Fill };
            }
            else if (dataType == typeof(bool))
            {
                return new CheckBox { Dock = DockStyle.Fill };
            }
            else if (dataType == typeof(decimal) || dataType == typeof(double))
            {
                return new NumericUpDown { Dock = DockStyle.Fill, DecimalPlaces = 2, Maximum = 100000 };
            }
            else if (dataType == typeof(int))
            {
                return new NumericUpDown { Dock = DockStyle.Fill, DecimalPlaces = 0, Maximum = 100000 };
            }
            else
            {
                return new TextBox { Dock = DockStyle.Fill };
            }
        }

        private string GetNameColumn(string tableName)
        {
            switch (tableName)
            {
                case "universities": return "name";
                case "students": return "full_name";
                case "books": return "title";
                default: return "name";
            }
        }

        private void SetControlValue(Control control, object value)
        {
            if (control is DateTimePicker)
                ((DateTimePicker)control).Value = (DateTime)value;
            else if (control is CheckBox)
                ((CheckBox)control).Checked = (bool)value;
            else if (control is NumericUpDown)
                ((NumericUpDown)control).Value = Convert.ToDecimal(value);
            else if (control is TextBox)
                ((TextBox)control).Text = value.ToString();
            else if (control is ComboBox)
            {
                foreach (ComboboxItem item in ((ComboBox)control).Items)
                {
                    if (item.Value.ToString() == value.ToString())
                    {
                        ((ComboBox)control).SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private string GetIdColumn(string tableName)
        {
            switch (tableName)
            {
                case "universities": return "university_id";
                case "students": return "student_id";
                case "books": return "book_id";
                case "book_loans": return "loan_id";
                default: return "id";
            }
        }

        private void SaveRecord(string tableName, object id, TableLayoutPanel tableLayout, Form editForm, DataGridView dataGridView)
        {
            try
            {
                using (var con = GetConnection())
                {
                    string sql;
                    NpgsqlCommand cmd;

                    if (id == null)
                    {
                        sql = $"INSERT INTO {tableName} ({GetColumnsForInsert(tableName, tableLayout)}) VALUES ({GetValuesForInsert(tableName, tableLayout)})";
                    }
                    else
                    {
                        sql = $"UPDATE {tableName} SET {GetSetClause(tableName, tableLayout)} WHERE {GetIdColumn(tableName)} = @id";
                    }

                    cmd = new NpgsqlCommand(sql, con);
                    AddParameters(cmd, tableName, tableLayout);
                    if (id != null) cmd.Parameters.AddWithValue("@id", id);

                    cmd.ExecuteNonQuery();
                    LoadData(tableName, dataGridView);
                    editForm.Close();
                    LoadStatistics(); // Обновляем статистику после изменений
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}");
            }
        }

        private string GetColumnsForInsert(string tableName, TableLayoutPanel tableLayout)
        {
            string columns = "";
            for (int i = 0; i < tableLayout.RowCount - 1; i++)
            {
                var label = tableLayout.GetControlFromPosition(0, i) as Label;
                if (label == null) continue;

                string columnName = GetColumnNameFromDisplayName(label.Text);
                if (columnName == GetIdColumn(tableName)) continue;
                if (columns != "") columns += ", ";
                columns += columnName;
            }
            return columns;
        }

        private string GetColumnNameFromDisplayName(string displayName)
        {
            switch (displayName)
            {
                case "Название": return "name";
                case "Название книги": return "title";
                case "Адрес": return "address";
                case "Телефон": return "phone";
                case "Контактный телефон": return "contact_phone";
                case "Университет": return "university_id";
                case "ФИО": return "full_name";
                case "Номер студ. билета": return "student_card_number";
                case "Email": return "email";
                case "Автор": return "author";
                case "Издательство": return "publisher";
                case "Год издания": return "publication_year";
                case "Цена": return "price";
                case "Доступна": return "is_available";
                case "Книга": return "book_id";
                case "Студент": return "student_id";
                case "Дата выдачи": return "loan_date";
                case "Срок возврата": return "due_date";
                case "Дата возврата": return "return_date";
                case "Утеряна": return "is_lost";
                default: return displayName;
            }
        }

        private string GetValuesForInsert(string tableName, TableLayoutPanel tableLayout)
        {
            string values = "";
            for (int i = 0; i < tableLayout.RowCount - 1; i++)
            {
                var label = tableLayout.GetControlFromPosition(0, i) as Label;
                if (label == null) continue;

                string columnName = GetColumnNameFromDisplayName(label.Text);
                if (columnName == GetIdColumn(tableName)) continue;
                if (values != "") values += ", ";
                values += $"@{columnName}";
            }
            return values;
        }

        private string GetSetClause(string tableName, TableLayoutPanel tableLayout)
        {
            string setClause = "";
            for (int i = 0; i < tableLayout.RowCount - 1; i++)
            {
                var label = tableLayout.GetControlFromPosition(0, i) as Label;
                if (label == null) continue;

                string columnName = GetColumnNameFromDisplayName(label.Text);
                if (columnName == GetIdColumn(tableName)) continue;
                if (setClause != "") setClause += ", ";
                setClause += $"{columnName} = @{columnName}";
            }
            return setClause;
        }

        private void AddParameters(NpgsqlCommand cmd, string tableName, TableLayoutPanel tableLayout)
        {
            for (int i = 0; i < tableLayout.RowCount - 1; i++)
            {
                var label = tableLayout.GetControlFromPosition(0, i) as Label;
                if (label == null) continue;

                string columnName = GetColumnNameFromDisplayName(label.Text);
                if (columnName == GetIdColumn(tableName)) continue;

                Control inputControl = tableLayout.GetControlFromPosition(1, i);
                if (inputControl == null) continue;

                object value = GetControlValue(inputControl);
                cmd.Parameters.AddWithValue($"@{columnName}", value ?? DBNull.Value);
            }
        }

        private object GetControlValue(Control control)
        {
            if (control is DateTimePicker)
                return ((DateTimePicker)control).Value;
            else if (control is CheckBox)
                return ((CheckBox)control).Checked;
            else if (control is NumericUpDown)
                return ((NumericUpDown)control).Value;
            else if (control is TextBox)
                return string.IsNullOrEmpty(((TextBox)control).Text) ? null : ((TextBox)control).Text;
            else if (control is ComboBox)
                return ((ComboboxItem)((ComboBox)control).SelectedItem)?.Value;

            return null;
        }

        private void DeleteRecord(string tableName, object id, DataGridView dataGridView)
        {
            try
            {
                using (var con = GetConnection())
                {
                    string sql = $"DELETE FROM {tableName} WHERE {GetIdColumn(tableName)} = @id";
                    NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    LoadData(tableName, dataGridView);
                    LoadStatistics(); // Обновляем статистику после удаления
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении записи: {ex.Message}");
            }
        }

        private NpgsqlConnection GetConnection()
        {
            var con = new NpgsqlConnection(connString);
            con.Open();
            return con;
        }

        private void LoadUniversities()
        {
            try
            {
                using (var con = GetConnection())
                {
                    string sql = "SELECT university_id, name FROM universities ORDER BY name";
                    NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                    NpgsqlDataReader dr = cmd.ExecuteReader();

                    cmbUniversity.Items.Clear();
                    cmbUniversity.Items.Add("Все ВУЗы");

                    while (dr.Read())
                    {
                        cmbUniversity.Items.Add(new ComboboxItem(dr["name"].ToString(), dr["university_id"]));
                    }
                    cmbUniversity.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки списка ВУЗов: " + ex.Message);
            }
        }

        private void btnGenerateOverdueReport_Click(object sender, EventArgs e)
        {
            if (dtpReportDate.Value == null)
            {
                MessageBox.Show("Выберите дату для отчета");
                return;
            }

            try
            {
                using (var con = GetConnection())
                {
                    string sql = @"SELECT 
                        s.full_name AS ""ФИО студента"", 
                        u.name AS ""ВУЗ"", 
                        b.title AS ""Название книги"", 
                        b.author AS ""Автор"", 
                        b.price AS ""Стоимость"", 
                        bl.loan_date AS ""Дата выдачи"", 
                        bl.due_date AS ""Срок возврата"",
                        CASE 
                            WHEN bl.return_date IS NULL THEN 'Не возвращена'
                            ELSE bl.return_date::text
                        END AS ""Факт возврата"",
                        CASE 
                            WHEN bl.is_lost THEN 'Да'
                            ELSE 'Нет'
                        END AS ""Утеряна""
                      FROM book_loans bl
                      JOIN students s ON bl.student_id = s.student_id
                      JOIN universities u ON s.university_id = u.university_id
                      JOIN books b ON bl.book_id = b.book_id
                      WHERE bl.return_date IS NULL AND bl.due_date < @report_date";

                    // Добавляем условие для фильтрации по ВУЗу, если выбран конкретный ВУЗ
                    if (cmbUniversity.SelectedIndex > 0)
                    {
                        int universityId = ((ComboboxItem)cmbUniversity.SelectedItem).Value.ToInt();
                        sql += " AND s.university_id = @university_id";
                    }

                    sql += " ORDER BY u.name, s.full_name";

                    NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@report_date", dtpReportDate.Value.Date);

                    // Добавляем параметр university_id только если выбран конкретный ВУЗ
                    if (cmbUniversity.SelectedIndex > 0)
                    {
                        cmd.Parameters.AddWithValue("@university_id", ((ComboboxItem)cmbUniversity.SelectedItem).Value.ToInt());
                    }

                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    if (dt.Rows.Count == 0)
                    {
                        MessageBox.Show("Нет данных для отчета");
                        return;
                    }

                    SaveToExcel(dt, $"Задолженности_на_{dtpReportDate.Value:ddMMyyyy}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при формировании отчета: " + ex.Message);
            }
        }

        private void btnGenerateSummaryReport_Click(object sender, EventArgs e)
        {
            if (dtpStartDate.Value == null || dtpEndDate.Value == null)
            {
                MessageBox.Show("Выберите период для отчета");
                return;
            }

            if (dtpStartDate.Value > dtpEndDate.Value)
            {
                MessageBox.Show("Дата начала периода должна быть раньше даты окончания");
                return;
            }

            try
            {
                using (var con = GetConnection())
                {
                    string sql = @"SELECT 
                                u.name AS ""ВУЗ"",
                                COUNT(bl.loan_id) AS ""Всего выдач"",
                                SUM(CASE WHEN bl.return_date IS NULL AND bl.due_date < CURRENT_DATE THEN 1 ELSE 0 END) AS ""Просрочено"",
                                SUM(CASE WHEN bl.is_lost THEN 1 ELSE 0 END) AS ""Утеряно""
                              FROM universities u
                              LEFT JOIN students s ON u.university_id = s.university_id
                              LEFT JOIN book_loans bl ON s.student_id = bl.student_id
                              WHERE bl.loan_date BETWEEN @start_date AND @end_date OR bl.loan_date IS NULL
                              GROUP BY u.university_id, u.name
                              ORDER BY u.name";

                    NpgsqlCommand cmd = new NpgsqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@start_date", dtpStartDate.Value.Date);
                    cmd.Parameters.AddWithValue("@end_date", dtpEndDate.Value.Date);

                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    if (dt.Rows.Count == 0)
                    {
                        MessageBox.Show("Нет данных для отчета");
                        return;
                    }

                    SaveToExcel(dt, $"Сводный_отчет_{dtpStartDate.Value:ddMMyyyy}_по_{dtpEndDate.Value:ddMMyyyy}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при формировании сводного отчета: " + ex.Message);
            }
        }

        private void SaveToExcel(DataTable dt, string defaultFileName)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Excel Files|*.xlsx";
            saveFileDialog.Title = "Сохранить отчет Excel";
            saveFileDialog.FileName = defaultFileName;

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return;

            string filePath = saveFileDialog.FileName;

            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Excel.Application();
                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.ActiveSheet;

                // Заголовок отчета
                Excel.Range headerRange = worksheet.Range["A1"];
                headerRange.EntireRow.Font.Bold = true;
                headerRange.Font.Size = 14;
                headerRange.Value = defaultFileName.Replace("_", " ");
                worksheet.Range["A1"].EntireRow.RowHeight = 25;

                // Заголовки столбцов
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    worksheet.Cells[3, i + 1] = dt.Columns[i].ColumnName;
                }

                Excel.Range columnHeaders = worksheet.Range[worksheet.Cells[3, 1], worksheet.Cells[3, dt.Columns.Count]];
                columnHeaders.Font.Bold = true;
                columnHeaders.Interior.Color = Excel.XlRgbColor.rgbLightGray;
                columnHeaders.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;

                // Данные
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        worksheet.Cells[i + 4, j + 1] = dt.Rows[i][j];
                    }
                }

                // Автоподбор ширины столбцов
                worksheet.Columns.AutoFit();

                // Форматирование для денежных значений
                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    if (dt.Columns[j].DataType == typeof(decimal) ||
                        dt.Columns[j].ColumnName.Contains("Стоимость") ||
                        dt.Columns[j].ColumnName.Contains("Цена"))
                    {
                        Excel.Range range = worksheet.Range[worksheet.Cells[4, j + 1], worksheet.Cells[dt.Rows.Count + 3, j + 1]];
                        range.NumberFormat = "#,##0.00";
                    }
                }

                // Сохраняем файл
                workbook.SaveAs(filePath);
                MessageBox.Show($"Отчет успешно сохранен:\n{filePath}", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении файла Excel: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Закрываем Excel
                workbook?.Close(false);
                excelApp?.Quit();
                ReleaseObject(worksheet);
                ReleaseObject(workbook);
                ReleaseObject(excelApp);
            }
        }

        private void ReleaseObject(object obj)
        {
            try
            {
                if (obj != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
                    obj = null;
                }
            }
            catch (Exception ex)
            {
                obj = null;
                Console.WriteLine("Exception while releasing object: " + ex.ToString());
            }
            finally
            {
                GC.Collect();
            }
        }
    }

    public class ComboboxItem
    {
        public string Text { get; set; }
        public object Value { get; set; }

        public ComboboxItem(string text, object value)
        {
            Text = text;
            Value = value;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public static class Extensions
    {
        public static int ToInt(this object value)
        {
            return Convert.ToInt32(value);
        }
    }
}