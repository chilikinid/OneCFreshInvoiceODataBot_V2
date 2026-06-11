using Microsoft.Extensions.Logging;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OneCFreshInvoiceODataBot;

public sealed partial class SettingsForm : Form
{
    private const string DefaultDateFormat = "yyyy-MM-dd";
    private const string ProcessingTabName = "Processing";
    private const string ODataTabName = "OData";
    private const string MailTabName = "Mail";
    private static readonly string[] _sectionNames = [ProcessingTabName, ODataTabName, MailTabName];
    private static readonly HashSet<string> _metadataOnlyDisabledProcessingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "InputXlsx",
        "StopOnFirstError",
        "DryRun",
        "SendEmail",
        "GeneratePrintForms",
        "CreateInvoicesOnly",
        "InvoiceFromDate",
        "RealizationDate",
        "ProcessExistingInvoices",
        "INN"
    };

    private static readonly HashSet<string> _processExistingInvoicesDisabledProcessingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "InputXlsx",
        "DownloadMetadataOnly",
        "GeneratePrintForms",
        "CreateInvoicesOnly",
        "INN"
    };

    private readonly Dictionary<string, List<SettingBinding>> _bindingsBySection = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDirty;
    private bool _loadingSettingsToControls;
    private bool _updatingModeRules;
    private bool _updatingSettingsFileList;
    private JsonObject _root;
    private List<string> _settingsFilesList = [];

    public SettingsForm()
        : this(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json"))
    {
    }

    public SettingsForm(string settingsPath)
    {
        SettingsPath = settingsPath;
        _root = _LoadSettings(settingsPath);

        InitializeComponent();
        _ConfigureDesignerControls();
        _RefreshSettingsFileList();
        _RegisterBindings();
        _LoadSettingsToControls();
        _ApplyModeRules();
        _SetDirty(false);
    }

    public string SettingsPath { get; private set; }

    private void _ConfigureDesignerControls()
    {
        _ConfigureModeLabel(processingCommonModeLabel, "Общие");

        _ConfigureLabel(processingInputXlsxLabel, "Файл Excel");
        _ConfigureTextBox(processingInputXlsxTextBox);
        _ConfigureLabel(processingOutputDirLabel, "Папка вывода");
        _ConfigureTextBox(processingOutputDirTextBox);
        _ConfigureLabel(processingODataMapPathLabel, "Карта OData");
        _ConfigureTextBox(processingODataMapPathTextBox);
        _ConfigureCheckBox(processingStopOnFirstErrorCheckBox, "Остановить при первой ошибке");
        _ConfigureCheckBox(processingDryRunCheckBox, "Пробный запуск");
        _ConfigureCheckBox(processingDownloadMetadataOnlyCheckBox, "Только скачать метаданные");
        _ConfigureCheckBox(processingSendEmailCheckBox, "Отправлять email");
        _ConfigureCheckBox(processingGeneratePrintFormsCheckBox, "Формировать печатные формы");
        _ConfigureCheckBox(processingCreateInvoicesOnlyCheckBox, "Создавать только счета");
        _ConfigureLabel(processingInvoiceFromDateLabel, "Счета с даты");
        _ConfigureDateTimePicker(processingInvoiceFromDateDateTimePicker, DefaultDateFormat, showCheckBox: true);
        _ConfigureLabel(processingRealizationDateLabel, "Дата реализации");
        _ConfigureDateTimePicker(processingRealizationDateDateTimePicker, DefaultDateFormat, showCheckBox: true);
        _ConfigureLabel(processingInnLabel, "ИНН организации");
        _ConfigureTextBox(processingInnTextBox);

        _ConfigureLabel(oDataServiceRootLabel, "Адрес сервиса");
        _ConfigureTextBox(oDataServiceRootTextBox);
        _ConfigureLabel(oDataLoginLabel, "Логин");
        _ConfigureTextBox(oDataLoginTextBox);
        _ConfigureLabel(oDataPasswordLabel, "Пароль");
        _ConfigureTextBox(oDataPasswordTextBox);
        oDataPasswordTextBox.UseSystemPasswordChar = true;
        _ConfigureLabel(oDataTimeoutSecondsLabel, "Таймаут, сек.");
        _ConfigureNumericUpDown(oDataTimeoutSecondsNumericUpDown, 120);

        _ConfigureLabel(mailSmtpHostLabel, "SMTP-сервер");
        _ConfigureTextBox(mailSmtpHostTextBox);
        _ConfigureLabel(mailSmtpPortLabel, "SMTP-порт");
        _ConfigureNumericUpDown(mailSmtpPortNumericUpDown, 587);
        _ConfigureCheckBox(mailUseStartTlsCheckBox, "Использовать StartTLS");
        _ConfigureLabel(mailLoginLabel, "Логин");
        _ConfigureTextBox(mailLoginTextBox);
        _ConfigureLabel(mailPasswordLabel, "Пароль");
        _ConfigureTextBox(mailPasswordTextBox);
        mailPasswordTextBox.UseSystemPasswordChar = true;
        _ConfigureLabel(mailFromLabel, "Отправитель");
        _ConfigureTextBox(mailFromTextBox);
        _ConfigureLabel(mailResultEmailLabel, "Email получателя");
        _ConfigureTextBox(mailResultEmailTextBox);
    }

    private static void _ConfigureLabel(Label label, string text)
    {
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.Height = 34;
        label.Margin = new Padding(0, 5, 12, 5);
        label.Text = text;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static void _ConfigureModeLabel(Label label, string text)
    {
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.Font = new Font(label.Font, FontStyle.Bold);
        label.Margin = new Padding(0, 8, 0, 2);
        label.Text = text;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static void _ConfigureTextBox(TextBox textBox)
    {
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textBox.Dock = DockStyle.None;
        textBox.Margin = new Padding(0, 5, 0, 5);
    }

    private static void _ConfigureCheckBox(CheckBox checkBox, string text)
    {
        checkBox.AutoSize = true;
        checkBox.Dock = DockStyle.Left;
        checkBox.Margin = new Padding(0, 8, 0, 5);
        checkBox.Text = text;
    }

    private static void _ConfigureNumericUpDown(NumericUpDown numericUpDown, int value)
    {
        numericUpDown.Anchor = AnchorStyles.Left;
        numericUpDown.Dock = DockStyle.None;
        numericUpDown.Margin = new Padding(0, 5, 0, 5);
        numericUpDown.Maximum = 1000000000;
        numericUpDown.Minimum = -1000000000;
        numericUpDown.Value = value;
        numericUpDown.Width = 180;
    }

    private static void _ConfigureDateTimePicker(DateTimePicker dateTimePicker, string customFormat, bool showCheckBox)
    {
        dateTimePicker.Anchor = AnchorStyles.Left;
        dateTimePicker.Dock = DockStyle.None;
        dateTimePicker.Format = DateTimePickerFormat.Custom;
        dateTimePicker.CustomFormat = customFormat;
        dateTimePicker.Margin = new Padding(0, 5, 0, 5);
        dateTimePicker.ShowCheckBox = showCheckBox;
        dateTimePicker.Width = 200;
    }

    private static JsonObject _LoadSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath))
            return [];

        var json = File.ReadAllText(settingsPath, Encoding.UTF8);
        return JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException($"Не удалось прочитать настройки: {settingsPath}");
    }

    private void _RegisterBindings()
    {
        _bindingsBySection.Clear();

        _AddBinding(ProcessingTabName, "InputXlsx", processingInputXlsxLabel, processingInputXlsxTextBox, SettingValueKind.String);
        _AddBinding(ProcessingTabName, "OutputDir", processingOutputDirLabel, processingOutputDirTextBox, SettingValueKind.String);
        _AddBinding(ProcessingTabName, "ODataMapPath", processingODataMapPathLabel, processingODataMapPathTextBox, SettingValueKind.String);
        _AddBinding(ProcessingTabName, "StopOnFirstError", processingStopOnFirstErrorCheckBox, processingStopOnFirstErrorCheckBox, SettingValueKind.Boolean);
        _AddBinding(ProcessingTabName, "DryRun", processingDryRunCheckBox, processingDryRunCheckBox, SettingValueKind.Boolean);
        _AddBinding(ProcessingTabName, "DownloadMetadataOnly", processingDownloadMetadataOnlyCheckBox, processingDownloadMetadataOnlyCheckBox, SettingValueKind.Boolean);
        _AddBinding(ProcessingTabName, "SendEmail", processingSendEmailCheckBox, processingSendEmailCheckBox, SettingValueKind.Boolean);
        _AddBinding(ProcessingTabName, "GeneratePrintForms", processingGeneratePrintFormsCheckBox, processingGeneratePrintFormsCheckBox, SettingValueKind.Boolean);
        _AddBinding(ProcessingTabName, "CreateInvoicesOnly", processingCreateInvoicesOnlyCheckBox, processingCreateInvoicesOnlyCheckBox, SettingValueKind.Boolean);
        _AddBinding(ProcessingTabName, "InvoiceFromDate", processingInvoiceFromDateLabel, processingInvoiceFromDateDateTimePicker, SettingValueKind.NullableIsoDate);
        _AddBinding(ProcessingTabName, "RealizationDate", processingRealizationDateLabel, processingRealizationDateDateTimePicker, SettingValueKind.NullableIsoDate);
        _AddBinding(ProcessingTabName, "ProcessExistingInvoices", rbFrom1C, rbFrom1C, SettingValueKind.Boolean, rbFromExcel);
        _AddBinding(ProcessingTabName, "INN", processingInnLabel, processingInnTextBox, SettingValueKind.String);

        _AddBinding(ODataTabName, "ServiceRoot", oDataServiceRootLabel, oDataServiceRootTextBox, SettingValueKind.String);
        _AddBinding(ODataTabName, "Login", oDataLoginLabel, oDataLoginTextBox, SettingValueKind.String);
        _AddBinding(ODataTabName, "Password", oDataPasswordLabel, oDataPasswordTextBox, SettingValueKind.String);
        _AddBinding(ODataTabName, "TimeoutSeconds", oDataTimeoutSecondsLabel, oDataTimeoutSecondsNumericUpDown, SettingValueKind.Integer);

        _AddBinding(MailTabName, "SmtpHost", mailSmtpHostLabel, mailSmtpHostTextBox, SettingValueKind.String);
        _AddBinding(MailTabName, "SmtpPort", mailSmtpPortLabel, mailSmtpPortNumericUpDown, SettingValueKind.Integer);
        _AddBinding(MailTabName, "UseStartTls", mailUseStartTlsCheckBox, mailUseStartTlsCheckBox, SettingValueKind.Boolean);
        _AddBinding(MailTabName, "Login", mailLoginLabel, mailLoginTextBox, SettingValueKind.String);
        _AddBinding(MailTabName, "Password", mailPasswordLabel, mailPasswordTextBox, SettingValueKind.String);
        _AddBinding(MailTabName, "From", mailFromLabel, mailFromTextBox, SettingValueKind.String);
        _AddBinding(MailTabName, "ResultEmail", mailResultEmailLabel, mailResultEmailTextBox, SettingValueKind.String);



        processingDownloadMetadataOnlyCheckBox.CheckedChanged += _ProcessingCheckBox_CheckedChanged;
        processingSendEmailCheckBox.CheckedChanged += _ProcessingCheckBox_CheckedChanged;
        rbFromExcel.CheckedChanged += _ProcessingRadioButton_CheckedChanged;
        rbFrom1C.CheckedChanged += _ProcessingRadioButton_CheckedChanged;
    }

    private void _ProcessingRadioButton_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updatingModeRules)
            return;

        _ApplyModeRules(sender as RadioButton);
    }

    private void _AddBinding(string sectionName, string key, Control label, Control editor, SettingValueKind valueKind, params Control[]? alternativeEditors)
    {
        if (!_bindingsBySection.TryGetValue(sectionName, out var bindings))
        {
            bindings = [];
            _bindingsBySection[sectionName] = bindings;
        }

        bindings.Add(new SettingBinding(sectionName, key, label, editor, valueKind, alternativeEditors));
        _RegisterDirtyTracking(editor);
        if (alternativeEditors != null)
        {
            foreach (var altEditor in alternativeEditors)
                _RegisterDirtyTracking(altEditor);
        }
    }

    private void _LoadSettingsToControls()
    {
        _loadingSettingsToControls = true;
        try
        {
            foreach (var sectionName in _sectionNames)
            {
                if (_root[sectionName] is not JsonObject section)
                {
                    section = [];
                    _root[sectionName] = section;
                }

                if (!_bindingsBySection.TryGetValue(sectionName, out var bindings))
                    continue;

                foreach (var binding in bindings)
                {
                    if (section.TryGetPropertyValue(binding.Key, out var value))
                        _SetEditorValue(binding, value);
                    else
                        _SetEditorValue(binding, null);
                }
            }
        }
        finally
        {
            _loadingSettingsToControls = false;
            _SetDirty(false);
        }
    }

    private static void _SetEditorValue(SettingBinding binding, JsonNode? value)
    {
        switch (binding.Editor)
        {
            case CheckBox checkBox:
                checkBox.Checked = value?.GetValue<bool>() == true;
                break;
            case RadioButton radioButton:
                radioButton.Checked = value?.GetValue<bool>() == true;
                if (binding.AlternativeEditors != null)
                {
                    foreach (var alternativeEditor in binding.AlternativeEditors)
                    {
                        if (alternativeEditor is RadioButton altRadioButton)
                        {
                            altRadioButton.Checked = !radioButton.Checked;
                        }
                    }
                }
                break;
            case NumericUpDown numeric:
                if (_TryGetDecimal(value, out var number))
                    numeric.Value = Math.Clamp(number, numeric.Minimum, numeric.Maximum);
                break;
            case TextBox textBox:
                textBox.Text = value?.GetValue<string>() ?? string.Empty;
                break;
            case DateTimePicker dateTimePicker:
                if (_TryGetDate(value, out var date))
                {
                    dateTimePicker.Value = date;
                    dateTimePicker.Checked = true;
                }
                else
                {
                    dateTimePicker.Value = DateTime.Today;
                    dateTimePicker.Checked = false;
                }

                break;
        }
    }

    private bool _SaveSettings(bool showSuccess = false, string? targetPath = null)
    {
        try
        {
            _ApplyModeRules();

            foreach (var sectionName in _sectionNames)
            {
                if (_root[sectionName] is not JsonObject section)
                {
                    section = [];
                    _root[sectionName] = section;
                }

                foreach (var binding in _bindingsBySection[sectionName])
                {
                    section[binding.Key] = _CreateJsonNode(binding);
                }
            }

            var savePath = targetPath ?? SettingsPath;
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(savePath, _root.ToJsonString(options), Encoding.UTF8);
            SettingsPath = savePath;
            _RefreshSettingsFileList();
            _SetDirty(false);

            if (showSuccess)
                MessageBox.Show(this, "Настройки сохранены.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось сохранить настройки", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void _SaveButton_Click(object? sender, EventArgs e)
    {
        _SaveSettings(showSuccess: true);
    }

    private void _SaveAsButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить настройки как",
            Filter = "Файлы настроек (*.json;*.settings)|*.json;*.settings|Все файлы (*.*)|*.*",
            FileName = Path.GetFileName(SettingsPath),
            InitialDirectory = _GetSettingsInitialDirectory(),
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _SaveSettings(showSuccess: true, targetPath: dialog.FileName);
    }

    private async void _RunButton_Click(object? sender, EventArgs e)
    {
        if (_isDirty && !_SaveSettings())
        {
            return;
        }

        var logForm = new ProcessingLogForm();
        using var loggerFactory = _CreateFormLoggerFactory(logForm.AppendLogLine);

        runButton.Enabled = false;
        logForm.Show(this);
        logForm.AppendLogLine("Запуск обработки...");

        try
        {
            var exitCode = await Task.Run(() => Program.RunProcessingAsync(
                [SettingsPath],
                loggerFactory,
                registerConsoleCancelKeyPress: false));
            logForm.AppendLogLine(exitCode == 0
                ? "Обработка завершена успешно."
                : $"Обработка завершена с кодом {exitCode}.");
        }
        finally
        {
            runButton.Enabled = true;
        }
    }

    private void _CloseButton_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void _BrowseSettingsFileButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файл настроек",
            Filter = "Файлы настроек (*.json;*.settings)|*.json;*.settings|Все файлы (*.*)|*.*",
            FileName = Path.GetFileName(SettingsPath),
            InitialDirectory = _GetSettingsInitialDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            _LoadSettingsFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось открыть настройки", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        workingDirectoryPathLabel.Text = Directory.GetCurrentDirectory();
    }

    private void _BrowseInputXlsxButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файл Excel",
            Filter = "Файлы Excel (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|Все файлы (*.*)|*.*",
            FileName = Path.GetFileName(processingInputXlsxTextBox.Text),
            InitialDirectory = _GetInputXlsxInitialDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var selected = dialog.FileName;
        var root = Directory.GetCurrentDirectory();
        var relative = Path.GetFullPath(selected);
        if (relative.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            processingInputXlsxTextBox.Text = relative.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        else
            processingInputXlsxTextBox.Text = selected;
    }

    private void _OpenInputXlsxButton_Click(object? sender, EventArgs e)
    {
        var inputPath = _ResolveInputXlsxPath();
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            MessageBox.Show(this, "Укажите файл Excel.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!File.Exists(inputPath))
        {
            MessageBox.Show(this, $"Файл Excel не найден: {inputPath}", "Не удалось открыть файл", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(inputPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось открыть файл Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void _SettingsFileComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updatingSettingsFileList || settingsFileComboBox.SelectedItem is not string selectedPath)
            return;

        if (selectedPath.Equals(SettingsPath, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            _LoadSettingsFile(selectedPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось открыть настройки", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _RefreshSettingsFileList();
        }
    }

    private void _LoadSettingsFile(string settingsPath)
    {
        SettingsPath = settingsPath;
        _root = _LoadSettings(SettingsPath);
        _RefreshSettingsFileList();
        _LoadSettingsToControls();
        _ApplyModeRules();
    }

    private void _RefreshSettingsFileList()
    {
        var launchDirectory = Directory.GetCurrentDirectory();
        var settingsFiles = Directory.Exists(launchDirectory)
            ? Directory.EnumerateFiles(launchDirectory, "appsettings.*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTime)
                .ToList()
            : [];

        if (!settingsFiles.Contains(SettingsPath, StringComparer.OrdinalIgnoreCase))
            settingsFiles.Insert(0, SettingsPath);

        // Build a mapping of unique file names to a single full path. If multiple full paths share the same file name,
        // prefer the currently selected SettingsPath so the displayed selection maps to the expected full path.
        var fileNameToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in settingsFiles)
        {
            var name = Path.GetFileName(path);
            if (!fileNameToPath.ContainsKey(name))
                fileNameToPath[name] = path;
        }

        var selectedFileName = Path.GetFileName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(selectedFileName))
            fileNameToPath[selectedFileName] = SettingsPath; // ensure selected maps to actual SettingsPath

        _settingsFilesList = [.. fileNameToPath.Values];
        _updatingSettingsFileList = true;
        try
        {
            settingsFileComboBox.Items.Clear();
            settingsFileComboBox.Items.AddRange([.. fileNameToPath.Keys.Cast<object>()]);
            settingsFileComboBox.SelectedItem = selectedFileName;
        }
        finally
        {
            _updatingSettingsFileList = false;
        }
    }

    private void _RegisterDirtyTracking(Control editor)
    {
        switch (editor)
        {
            case TextBox textBox:
                textBox.TextChanged += _SettingEditor_ValueChanged;
                break;
            case CheckBox checkBox:
                checkBox.CheckedChanged += _SettingEditor_ValueChanged;
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.ValueChanged += _SettingEditor_ValueChanged;
                break;
            case DateTimePicker dateTimePicker:
                dateTimePicker.ValueChanged += _SettingEditor_ValueChanged;
                break;
        }
    }

    private void _SettingEditor_ValueChanged(object? sender, EventArgs e)
    {
        if (_loadingSettingsToControls || _updatingModeRules)
            return;

        _SetDirty(true);
    }

    private void _SetDirty(bool isDirty)
    {
        _isDirty = isDirty;
        saveButton.Enabled = _isDirty;
    }

    private static ILoggerFactory _CreateFormLoggerFactory(Action<string> writeLine)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new FormLoggerProvider(writeLine));
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    private string _GetSettingsInitialDirectory()
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : Directory.GetCurrentDirectory();
    }

    private string _GetInputXlsxInitialDirectory()
    {
        var inputPath = _ResolveInputXlsxPath();
        var directory = string.IsNullOrWhiteSpace(inputPath)
            ? null
            : Path.GetDirectoryName(inputPath);

        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : Directory.GetCurrentDirectory();
    }

    private string _ResolveInputXlsxPath()
    {
        var inputPath = processingInputXlsxTextBox.Text.Trim();
        return string.IsNullOrWhiteSpace(inputPath)
            ? string.Empty
            : OneCFreshInvoiceODataBot.Services.PathResolver.ResolveFromProjectRoot(inputPath);
    }

    private void _ProcessingCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updatingModeRules)
            return;

        _ApplyModeRules();
    }

    private void _ApplyModeRules(RadioButton? changedRadioButton = null)
    {
        if (_updatingModeRules)
            return;

        _updatingModeRules = true;
        try
        {
            if (changedRadioButton == rbFromExcel || changedRadioButton == rbFrom1C)
            {
                processingDownloadMetadataOnlyCheckBox.Checked = false;
            }


            var isMetadataOnly = processingDownloadMetadataOnlyCheckBox.Checked;
            var isFrom1C = rbFrom1C.Checked;

            _SetProcessingFieldsEnabled(_metadataOnlyDisabledProcessingKeys, !isMetadataOnly);
            if (!isMetadataOnly)
                _SetProcessingFieldsEnabled(_processExistingInvoicesDisabledProcessingKeys, !isFrom1C);

            _SetSectionFieldsEnabled("Mail", !isMetadataOnly && processingSendEmailCheckBox.Checked);
        }
        finally
        {
            _updatingModeRules = false;
        }
    }

    private void _SetProcessingFieldsEnabled(HashSet<string> keys, bool enabled)
    {
        foreach (var key in keys)
        {
            var binding = _FindBinding("Processing", key);
            if (binding is not null)
                _SetBindingEnabled(binding, enabled);
        }
    }

    private void _SetSectionFieldsEnabled(string sectionName, bool enabled)
    {
        if (!_bindingsBySection.TryGetValue(sectionName, out var bindings))
            return;

        foreach (var binding in bindings)
        {
            _SetBindingEnabled(binding, enabled);
        }
    }

    private SettingBinding? _FindBinding(string sectionName, string key)
    {
        if (!_bindingsBySection.TryGetValue(sectionName, out var bindings))
            return null;

        return bindings.FirstOrDefault(binding => binding.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private static void _SetBindingEnabled(SettingBinding binding, bool enabled)
    {
        binding.Label.Enabled = enabled;
        binding.Editor.Enabled = enabled;
    }

    private static JsonValue? _CreateJsonNode(SettingBinding binding)
    {
        return binding.Editor switch
        {
            CheckBox checkBox => JsonValue.Create(checkBox.Checked),
            RadioButton radioButton => JsonValue.Create(radioButton.Checked),
            NumericUpDown numeric when binding.ValueKind == SettingValueKind.Integer => JsonValue.Create((int)numeric.Value),
            NumericUpDown numeric => JsonValue.Create(numeric.Value),
            DateTimePicker dateTimePicker when !dateTimePicker.Checked => null,
            DateTimePicker dateTimePicker when binding.ValueKind == SettingValueKind.NullableDotDate => JsonValue.Create(dateTimePicker.Value.ToString("yyyy.MM.dd")),
            DateTimePicker dateTimePicker => JsonValue.Create(dateTimePicker.Value.ToString(DefaultDateFormat)),
            TextBox textBox when binding.ValueKind == SettingValueKind.NullableString && string.IsNullOrWhiteSpace(textBox.Text) => null,
            TextBox textBox => JsonValue.Create(textBox.Text),
            _ => null
        };
    }

    private static bool _TryGetDate(JsonNode? value, out DateTime date)
    {
        date = DateTime.Today;

        if (value is null)
            return false;

        var text = value.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] formats = [DefaultDateFormat, "yyyy.MM.dd", "dd.MM.yyyy", "M/d/yyyy", "MM/dd/yyyy"];
        return DateTime.TryParseExact(
            text,
            formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out date)
            || DateTime.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out date);
    }

    private static bool _TryGetDecimal(JsonNode? value, out decimal number)
    {
        number = 0;

        if (value is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<decimal>(out number))
            return true;

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            number = (decimal)doubleValue;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            number = intValue;
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            number = longValue;
            return true;
        }

        return false;
    }

    private sealed record SettingBinding(string SectionName, string Key, Control Label, Control Editor, SettingValueKind ValueKind, Control[]? AlternativeEditors);

    private enum SettingValueKind
    {
        String,
        NullableString,
        NullableIsoDate,
        NullableDotDate,
        Boolean,
        Integer,
        Decimal
    }
}
